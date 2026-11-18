#!/usr/bin/env python3
"""Verifica que el AAB que se publica lleve los iconos de launcher del repo.

Lee el AAB con ``zipfile`` (no usa ``unzip`` externo) y compara contra
``EXPECTED_DENSITIES``, que es la unica fuente de verdad de tamanos por densidad.
El dhash del icono legacy mdpi contra ``Docs/release/assets/store-icon-512.png``
es el detector de "la ficha y el launcher se fueron a la mierda otra vez".

Dos cosas que el AAB real hace distinto de lo que uno espera:

* Los XML de ``res/`` NO son texto: aapt2 los guarda como XML protobuf. Los
  nombres de los elementos y los ``@mipmap/...`` viajan como strings del pool,
  asi que se validan por token exacto, no con ``"<background" in text``.
* Los recursos de mipmap pueden venir con una sola densidad (``mipmap-mdpi-v4``)
  cuando el contenido es identico en todas o cuando el build uso el template de
  Unity. Por eso: un tamano menor al esperado es error, uno mayor es normal
  (Unity copia el asset), y 1/6 densidades es aviso; 2..5/6 es error (slot sin
  asignar).

No depende del orden de los entries del zip ni de los avisos: un warning nunca
cambia el exit code.

Uso:
    python tools/icons/verify_aab_icons.py --aab release/MatchBall-0.3.1.aab
    python tools/icons/verify_aab_icons.py --aab otro.aab --dhash-max 8
    python tools/icons/verify_aab_icons.py --aab otro.aab --store-icon Docs/release/assets/store-icon-512.png

Exit code: 0 = PASS / 1 = el AAB no cumple el contrato / 2 = no se pudo leer la entrada.
"""

from __future__ import annotations

import argparse
import io
import re
import sys
import zipfile
from pathlib import Path

from PIL import Image

REPO_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_STORE_ICON = REPO_ROOT / "Docs" / "release" / "assets" / "store-icon-512.png"

# El unico modulo que lleva los iconos de launcher.
AAB_MODULE = "base"

# Densidad -> (lado del adaptive en px, lado del legacy/round en px).
# adaptive = 108dp * factor, legacy = 48dp * factor. 81/36 ... 432/192.
EXPECTED_DENSITIES = {
    "ldpi": (81, 36),
    "mdpi": (108, 48),
    "hdpi": (162, 72),
    "xhdpi": (216, 96),
    "xxhdpi": (324, 144),
    "xxxhdpi": (432, 192),
}

# Canvas mas grande que Unity puede emitir por kind: techo de cualquier entrada.
KIND_MAX_CANVAS = {"adaptive": 432, "legacy": 192}

ADAPTIVE_BACKGROUND = "ic_launcher_background.png"
ADAPTIVE_FOREGROUND = "ic_launcher_foreground.png"
LEGACY = "app_icon.png"
LEGACY_ROUND = "app_icon_round.png"

ANYDPI_DIR = "mipmap-anydpi-v26"
# (archivo, requerido). El round puede no existir en el primer AAB: se avisa.
ANYDPI_XML = (
    ("app_icon.xml", True),
    ("app_icon_round.xml", False),
)

# Drawables que el XML adaptativo DEBE referenciar (R2, escenario 2.4).
EXPECTED_LAYER_REFS = (
    "@mipmap/" + ADAPTIVE_BACKGROUND.rsplit(".", 1)[0],
    "@mipmap/" + ADAPTIVE_FOREGROUND.rsplit(".", 1)[0],
)
ADAPTIVE_LAYERS = ("background", "foreground")

BRAND_GREEN = (34, 177, 76)
GREEN_TOLERANCE = 3
BRAND_GREEN_HEX = "#22B14C"
STORE_ICON_SIZE = 512

# Full-bleed de la capa 0: un background correcto es UN color, asi que todo pixel
# que difiera del dominante es contenido pegado en la capa equivocada. Medido en
# esta maquina: con los circulos pegados en la capa 0, 19676/186624 = 10.54% en
# 432px (bbox no-verde == bbox del foreground); con la capa corregida, 0 px en las
# 6 densidades. Se tolera 0.5% (933 px en 432, 33 px en 81) para el ringing que
# pueda dejar un resampleo en densidades chicas: 21x por debajo del bug.
BACKGROUND_OFF_COLOR_MAX_RATIO = 0.005

# En el canvas 432 el foreground es ~10.2% opaco (medido por generate_icons.py):
# el umbral de 10% tiene 0.2 pp de margen y solo se aplica ahi. A 81/108 px el
# resampleo de Unity interpola el borde, asi que en el resto de las densidades
# se exige que existan LAS DOS poblaciones (eso es lo que detecta capas
# invertidas) sin exigir proporcion.
ADAPTIVE_CLEAR_MIN_RATIO = 0.02
ADAPTIVE_OPAQUE_MIN_RATIO = 0.10
ALPHA_PRESENCE_MIN_RATIO = 0.01

# Aviso (no falla) si el dhash cae en la banda alta pero dentro del maximo.
DHASH_WARN_AT = 5


class Report:
    """Acumula hallazgos. Los warnings nunca afectan el exit code."""

    def __init__(self) -> None:
        self.errors: list[str] = []
        self.warnings: list[str] = []
        self.lines: list[str] = []

    def error(self, message: str) -> None:
        self.errors.append(message)

    def warn(self, message: str) -> None:
        self.warnings.append(message)

    def info(self, message: str = "") -> None:
        self.lines.append(message)


class Aab:
    """Indice de nombres del zip. Nunca recorre entries en orden."""

    def __init__(self, zf: zipfile.ZipFile) -> None:
        self.zf = zf
        self.names = [n for n in zf.namelist() if not n.endswith("/")]
        self.index = set(self.names)
        self.variants: list[str] = []

    def res_dirs(self, prefix: str) -> list[str]:
        root = "%s/res/" % AAB_MODULE
        found = {n[len(root):].split("/", 1)[0] for n in self.names if n.startswith(root)}
        return sorted(d for d in found if d.startswith(prefix))

    def resolve(self, dir_name: str, filename: str) -> tuple[str | None, str | None]:
        """(ruta, variante). Acepta ``mipmap-mdpi`` y ``mipmap-mdpi-v4``."""
        exact = "%s/res/%s/%s" % (AAB_MODULE, dir_name, filename)
        if exact in self.index:
            return exact, None
        root = "%s/res/" % AAB_MODULE
        matches = sorted(
            n
            for n in self.names
            if n.startswith(root)
            and n.count("/") == 3
            and n.rsplit("/", 1)[-1] == filename
            and n.split("/")[2].startswith(dir_name + "-")
        )
        if len(matches) == 1:
            self.variants.append(matches[0].split("/")[2])
            return matches[0], matches[0]
        if len(matches) > 1:
            return None, "ambiguo: %s" % ", ".join(matches)
        return None, None

    def read(self, name: str) -> bytes:
        with self.zf.open(name) as fh:
            return fh.read()


def load_png(aab: Aab, name: str, report: Report) -> Image.Image | None:
    try:
        data = aab.read(name)
    except KeyError:
        report.error("no se puede leer %s (entry ausente)" % name)
        return None
    try:
        image = Image.open(io.BytesIO(data))
        image.load()
        return image
    except Exception as exc:  # cualquier byte corrupto tiene que ser un error legible
        report.error("%s: no decodifica como imagen (%s)" % (name, exc))
        return None


def human_bytes(size: int) -> str:
    if size < 1024:
        return "%d B" % size
    if size < 1024 * 1024:
        return "%.1f kB" % (size / 1024.0)
    return "%.1f MB" % (size / (1024.0 * 1024.0))


def alpha_ratios(image: Image.Image) -> tuple[float, float]:
    """(fraccion con alpha<16, fraccion con alpha>240). Sin canal alfa: (0.0, 1.0)."""
    if "A" not in image.getbands():
        return 0.0, 1.0
    histogram = image.convert("RGBA").getchannel("A").histogram()
    total = float(sum(histogram)) or 1.0
    clear = sum(histogram[:16]) / total
    opaque = sum(histogram[241:]) / total
    return clear, opaque


def dominant_color(image: Image.Image) -> tuple[int, int, int]:
    colors = image.convert("RGB").getcolors(maxcolors=1 << 24) or []
    return max(colors)[1]


def off_color_ratio(image: Image.Image, color: tuple[int, int, int]) -> float:
    """Fraccion de pixeles que NO son exactamente ``color``."""
    colors = image.convert("RGB").getcolors(maxcolors=1 << 24) or []
    total = sum(count for count, _ in colors) or 1
    off = sum(count for count, pixel in colors if pixel != color)
    return off / float(total)


def dhash_bits(image: Image.Image, hash_size: int = 8) -> int:
    """dhash de 64 bits: gris 9x8, compara cada pixel con el de su derecha."""
    gray = image.convert("L").resize((hash_size + 1, hash_size), Image.LANCZOS)
    pixels = gray.tobytes()  # modo L: un byte por pixel, en orden de fila
    bits = 0
    for row in range(hash_size):
        offset = row * (hash_size + 1)
        for col in range(hash_size):
            bits = (bits << 1) | (1 if pixels[offset + col] > pixels[offset + col + 1] else 0)
    return bits


def string_tokens(blob: bytes) -> list[str]:
    """Strings imprimibles del blob (pool de strings del XML protobuf)."""
    return [s.decode("ascii") for s in re.findall(rb"[\x20-\x7e]{4,}", blob)]


def adaptive_layers(blob: bytes) -> tuple[bool, set[str], set[str]]:
    """(es_adaptive_icon, capas_declaradas, refs @mipmap/... del XML).

    Soporta las dos formas que aparecen en la practica: XML de texto (fixtures,
    APK de debug) y XML protobuf (lo que guarda el AAB).
    """
    try:
        text = blob.decode("utf-8")
    except UnicodeDecodeError:
        text = None

    if text is not None and "<" in text and ">" in text:
        return (
            "<adaptive-icon" in text,
            {tag for tag in ADAPTIVE_LAYERS if re.search(r"<%s\b" % tag, text)},
            set(re.findall(r"@mipmap/[A-Za-z0-9_]+", text)),
        )

    tokens = string_tokens(blob)
    normalized = {re.sub(r"^[^A-Za-z]+|[^A-Za-z]+$", "", token) for token in tokens}
    return (
        any("adaptive-icon" in token for token in normalized),
        {tag for tag in ADAPTIVE_LAYERS if tag in normalized},
        set(re.findall(r"@mipmap/[A-Za-z0-9_]+", "\n".join(tokens))),
    )


def collect_density(aab: Aab, density: str, sizes: tuple[int, int], report: Report) -> tuple[dict, list, list]:
    """Valida los archivos que existen: (imagenes, faltantes_requeridos, faltantes_opcionales).

    Las densidades enteras ausentes las decide ``report_density_coverage``: el AAB
    puede legitimamente venir con una sola densidad.
    """
    adaptive_px, legacy_px = sizes
    checks = (
        (ADAPTIVE_BACKGROUND, adaptive_px, "adaptive", "capa 0 (background)", True),
        (ADAPTIVE_FOREGROUND, adaptive_px, "adaptive", "capa 1 (foreground)", True),
        (LEGACY, legacy_px, "legacy", "legacy", True),
        # El manifest no declara android:roundIcon: si falta el round el usuario no
        # lo nota, asi que se avisa en vez de bloquear.
        (LEGACY_ROUND, legacy_px, "legacy", "round", False),
    )
    found: dict = {}
    missing_required: list = []
    missing_optional: list = []
    for filename, expected, kind, role, required in checks:
        dir_name = "mipmap-%s" % density
        path, variant = aab.resolve(dir_name, filename)
        if path is None:
            if variant:
                report.error("%s (densidad %s, %s)" % (variant, density, role))
            elif required:
                missing_required.append((filename, role, expected))
            else:
                missing_optional.append((filename, role, expected))
            continue
        image = load_png(aab, path, report)
        if image is None:
            continue

        max_canvas = KIND_MAX_CANVAS[kind]
        if image.width != image.height:
            report.error(
                "%s: mide %dx%d, el icono tiene que ser cuadrado (densidad %s, %s)"
                % (path, image.width, image.height, density, role)
            )
            continue
        if image.width < expected:
            report.error(
                "%s: mide %dx%d, se esperaba >=%dx%d para la densidad %s (%s)"
                % (path, image.width, image.height, expected, expected, density, role)
            )
            continue
        if image.width > max_canvas:
            report.error(
                "%s: mide %dx%d, el maximo del kind %s es %dx%d (densidad %s, %s)"
                % (path, image.width, image.height, kind, max_canvas, max_canvas, density, role)
            )
            continue
        if image.width != expected:
            report.warn(
                "%s: mide %dx%d; el tamano de la densidad %s es %dx%d (Unity copio el asset sin reescalar)"
                % (path, image.width, image.height, density, expected, expected)
            )

        found[filename] = image
        report.info(
            "  %-8s %-26s %3dx%-3d %9s"
            % (density, filename, image.width, image.height, human_bytes(len(aab.read(path))))
        )
    return found, missing_required, missing_optional


def report_density_coverage(by_density: dict, gaps: dict, report: Report) -> None:
    present = [d for d in EXPECTED_DENSITIES if by_density[d]]
    absent = [d for d in EXPECTED_DENSITIES if d not in present]
    if not present:
        report.error(
            "el AAB no trae ningun mipmap de launcher (ningun app_icon.png): el build se hizo "
            "sin iconos asignados"
        )
        return
    if not absent:
        report.info("  densidades mipmap: 6/6 (%s)" % ", ".join(present))
    elif len(present) == 1:
        report.warn(
            "el AAB trae 1 de 6 densidades de mipmap (%s); faltan %s. Layout esperable cuando "
            "el contenido es identico en todas las densidades (aapt2 dedupea) o cuando el build "
            "uso el template default: se valida la que vino. Si el primer build con los 18 slots "
            "asignados reporta 6/6, endurecer esto a error"
            % (present[0], ", ".join(absent))
        )
    else:
        report.error(
            "faltan densidades de mipmap: %s (presentes %d/6: %s) - algun slot Android quedo "
            "vacio en Player Settings > Icon"
            % (", ".join(absent), len(present), ", ".join(present))
        )
    for density in present:
        for filename, role, expected in gaps[density][0]:
            report.error(
                "falta %s/res/mipmap-%s/%s (densidad %s, %s): se esperaba %dx%d"
                % (AAB_MODULE, density, filename, density, role, expected, expected)
            )
        for filename, role, _expected in gaps[density][1]:
            report.warn(
                "falta %s/res/mipmap-%s/%s (densidad %s, %s): opcional, el manifest no usa roundIcon"
                % (AAB_MODULE, density, filename, density, role)
            )



def check_anydpi(aab: Aab, report: Report) -> None:
    for filename, required in ANYDPI_XML:
        path, variant = aab.resolve(ANYDPI_DIR, filename)
        if path is None:
            message = "falta %s/res/%s/%s (sin ese XML no hay icono adaptativo)" % (
                AAB_MODULE,
                ANYDPI_DIR,
                filename,
            )
            if required:
                report.error(message)
            else:
                report.warn(message + " - se continua: el manifest no declara android:roundIcon")
            continue
        if variant:
            report.warn("XML adaptativo publicado como %s (se valida igual)" % variant)
        try:
            blob = aab.read(path)
        except KeyError:
            report.error("no se puede leer %s (entry ausente)" % path)
            continue

        is_adaptive, layers, refs = adaptive_layers(blob)
        if not is_adaptive:
            report.error(
                "%s: no declara <adaptive-icon> (un icono adaptativo necesita background + foreground)"
                % path
            )
        missing_layers = [layer for layer in ADAPTIVE_LAYERS if layer not in layers]
        if missing_layers:
            report.error(
                "%s: no declara %s"
                % (path, " ni ".join("<%s>" % layer for layer in missing_layers))
            )
        if refs:
            for expected in EXPECTED_LAYER_REFS:
                if expected not in refs:
                    report.error(
                        "%s: no referencia %s (declara: %s)"
                        % (path, expected, ", ".join(sorted(refs)) or "ninguna")
                    )
        else:
            report.warn("%s: no se pudieron leer los @mipmap/... del XML (se valida por geometria)" % path)
        if not missing_layers and is_adaptive:
            report.info(
                "  %-8s %-26s %s"
                % (
                    "anydpi",
                    filename,
                    "adaptive-icon con " + " + ".join("<%s>" % layer for layer in ADAPTIVE_LAYERS),
                )
            )


def check_background_color(by_density: dict, report: Report) -> None:
    """Color, opacidad y full-bleed de la capa 0 en cada densidad publicada.

    El color dominante solo no alcanza: 89.5% de verde pasa la tolerancia aunque la
    capa 0 lleve el artwork pegado (incidente de S1). Por eso ademas se cuenta
    cuantos pixeles difieren del dominante y se falla si superan
    ``BACKGROUND_OFF_COLOR_MAX_RATIO``.
    """
    present = [
        density
        for density in reversed(list(EXPECTED_DENSITIES))
        if by_density.get(density, {}).get(ADAPTIVE_BACKGROUND) is not None
    ]
    if not present:
        report.error("no hay ninguna capa 0 (ic_launcher_background.png) que validar")
        return

    for density in present:
        image = by_density[density][ADAPTIVE_BACKGROUND]
        red, green, blue = dominant_color(image)
        delta = max(
            abs(red - BRAND_GREEN[0]), abs(green - BRAND_GREEN[1]), abs(blue - BRAND_GREEN[2])
        )
        description = "bg %-8s %3dpx color dominante #%02X%02X%02X (esperado %s +-%d)" % (
            density,
            image.width,
            red,
            green,
            blue,
            BRAND_GREEN_HEX,
            GREEN_TOLERANCE,
        )
        if delta > GREEN_TOLERANCE:
            report.error(
                description + " - el fondo adaptativo no es el verde de marca (icono default o ajeno)"
            )
        else:
            report.info("  %s" % description)

        off = off_color_ratio(image, (red, green, blue))
        if off > BACKGROUND_OFF_COLOR_MAX_RATIO:
            report.error(
                "bg %s: %0.2f%% de los pixeles (%d de %d) no son el color dominante #%02X%02X%02X "
                "- la capa 0 tiene que ser full-bleed: el artwork va SOLO en la capa 1 "
                "(Android 12+ anima el foreground y se veria duplicado)"
                % (
                    density,
                    off * 100.0,
                    round(off * image.width * image.height),
                    image.width * image.height,
                    red,
                    green,
                    blue,
                )
            )
        else:
            report.info(
                "  bg %-8s full-bleed: %0.2f%% de pixeles fuera del color dominante (maximo %0.2f%%)"
                % (density, off * 100.0, BACKGROUND_OFF_COLOR_MAX_RATIO * 100.0)
            )

        clear, _ = alpha_ratios(image)
        if clear > 0.0:
            report.error(
                "bg %s tiene %0.1f%% de pixeles transparentes: la capa 0 debe ser opaca full-bleed"
                % (density, clear * 100.0)
            )


def check_foreground_alpha(by_density: dict, report: Report) -> None:
    """Detecta capas invertidas: el fg tiene que tener transparencia y contenido opaco."""
    largest = EXPECTED_DENSITIES["xxxhdpi"][0]
    checked_strict = False
    checked_any = False
    for density, sizes in EXPECTED_DENSITIES.items():
        image = by_density.get(density, {}).get(ADAPTIVE_FOREGROUND)
        if image is None:
            continue
        checked_any = True
        clear, opaque = alpha_ratios(image)
        description = "fg %-8s %3dpx transparente %5.1f%% opaco %5.1f%%" % (
            density,
            image.width,
            clear * 100.0,
            opaque * 100.0,
        )
        if image.width >= largest:
            checked_strict = True
            if clear < ADAPTIVE_CLEAR_MIN_RATIO or opaque < ADAPTIVE_OPAQUE_MIN_RATIO:
                report.error(
                    description
                    + " - se esperaba >=%0.0f%% con alpha<16 y >=%0.0f%% con alpha>240 (capas invertidas?)"
                    % (ADAPTIVE_CLEAR_MIN_RATIO * 100.0, ADAPTIVE_OPAQUE_MIN_RATIO * 100.0)
                )
            else:
                report.info("  %s" % description)
            continue
        if clear < ALPHA_PRESENCE_MIN_RATIO or opaque < ALPHA_PRESENCE_MIN_RATIO:
            report.error(
                description
                + " - falta una de las dos poblaciones de alfa en la densidad %s (capas invertidas?)"
                % density
            )
        else:
            report.info("  %s" % description)
    if not checked_strict and checked_any:
        report.warn(
            "no hay foreground de %dpx: el check estricto de alfa no corrio (solo se valido "
            "que existan las dos poblaciones)" % largest
        )
    if not checked_any:
        report.error("no hay ninguna capa 1 (ic_launcher_foreground.png) en el AAB")


def pick_legacy(by_density: dict) -> tuple[str, Image.Image] | tuple[None, None]:
    if LEGACY in by_density.get("mdpi", {}):
        return "mdpi", by_density["mdpi"][LEGACY]
    for density in EXPECTED_DENSITIES:
        image = by_density.get(density, {}).get(LEGACY)
        if image is not None:
            return density, image
    return None, None


def check_dhash(by_density: dict, store_icon_path: Path, dhash_max: int, report: Report) -> None:
    density, legacy = pick_legacy(by_density)
    if legacy is None:
        return
    if not store_icon_path.is_file():
        report.error(
            "falta el icono de ficha %s: no se puede comparar el launcher contra la ficha"
            % store_icon_path
        )
        return
    try:
        with Image.open(store_icon_path) as store:
            store.load()
            if store.size != (STORE_ICON_SIZE, STORE_ICON_SIZE):
                report.error(
                    "%s: mide %dx%d, la ficha de Play exige %dx%d"
                    % (store_icon_path, store.size[0], store.size[1], STORE_ICON_SIZE, STORE_ICON_SIZE)
                )
            elif store.mode != "RGBA":
                report.warn("%s: modo %s, la ficha de Play pide RGBA" % (store_icon_path, store.mode))
            reference_image = store.resize(legacy.size, Image.LANCZOS)
    except Exception as exc:
        report.error("no se puede leer el icono de ficha %s (%s)" % (store_icon_path, exc))
        return

    owner = dhash_bits(legacy)
    reference = dhash_bits(reference_image)
    distance = (owner ^ reference).bit_count()
    description = "dhash legacy %s (%dpx) vs store-icon: %d/64 (maximo %d)" % (
        density,
        legacy.width,
        distance,
        dhash_max,
    )
    if distance > dhash_max:
        report.error(
            description
            + " - el icono que se instala no coincide con el de la ficha (artwork divergente)"
        )
    elif distance >= DHASH_WARN_AT:
        report.warn(description + " - cerca del limite, revisar el artwork")
        report.info("  %s" % description)
    else:
        report.info("  %s" % description)


def verify(aab_path: Path, store_icon_path: Path, dhash_max: int) -> tuple[Report, int]:
    report = Report()
    try:
        zf = zipfile.ZipFile(aab_path)
    except (OSError, zipfile.BadZipFile) as exc:
        report.error("no se puede abrir %s como zip/AAB (%s)" % (aab_path, exc))
        return report, 2

    with zf:
        aab = Aab(zf)
        report.info(
            "AAB: %s (%s, %d entries)"
            % (aab_path, human_bytes(aab_path.stat().st_size), len(aab.names))
        )
        if not any(n.startswith(AAB_MODULE + "/") for n in aab.names):
            report.error(
                "el AAB no contiene el modulo %s/ (no hay res/ que verificar)" % AAB_MODULE
            )
            return report, 1

        report.info("Iconos de launcher (%s/res):" % AAB_MODULE)
        by_density = {}
        gaps = {}
        for density, sizes in EXPECTED_DENSITIES.items():
            found, missing_required, missing_optional = collect_density(aab, density, sizes, report)
            by_density[density] = found
            gaps[density] = (missing_required, missing_optional)
        report_density_coverage(by_density, gaps, report)
        if aab.variants:
            report.warn(
                "densidades publicadas con sufijo de version: %s (se validan igual)"
                % ", ".join(sorted(set(aab.variants)))
            )

        report.info("Recursos adaptativos y comparacion con la ficha:")
        check_anydpi(aab, report)
        check_background_color(by_density, report)
        check_foreground_alpha(by_density, report)
        check_dhash(by_density, store_icon_path, dhash_max, report)

        if report.errors:
            report.info("")
            report.info(
                "Directorio(s) res/mipmap-* en el AAB: %s" % (aab.res_dirs("mipmap-") or "(ninguno)")
            )

    return report, (1 if report.errors else 0)


def print_report(report: Report, exit_code: int) -> None:
    for line in report.lines:
        print(line)
    if report.warnings:
        print("")
        print("Avisos (no bloquean):")
        for warning in report.warnings:
            print("  - " + warning)
    if exit_code == 0:
        print("")
        print("PASS: los iconos del AAB son los del artwork commiteado.")
        return
    print("")
    print("FALLA: %d problema(s) en el AAB:" % len(report.errors), file=sys.stderr)
    for error in report.errors:
        print("  - " + error, file=sys.stderr)
    if exit_code == 2:
        print("  Revisar la invocacion: el archivo existe y es un AAB/zip valido?", file=sys.stderr)
        return
    print(
        "  Revisar Player Settings > Icon (18 slots Android) y el guard AppIconBuildGuard.",
        file=sys.stderr,
    )


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="verify_aab_icons.py",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        description=(
            "Verifica los iconos de launcher dentro de un AAB ya construido.\n"
            "Lee el AAB con zipfile + Pillow: no necesita unzip ni Android SDK."
        ),
        epilog=(
            "Tabla de tamanos por densidad (base/res/mipmap-<densidad>/):\n"
            "  adaptive 81/108/162/216/324/432 / legacy y round 36/48/72/96/144/192\n"
            "Checks: app_icon.xml con <background>+<foreground>, fondo #22B14C opaco y\n"
            "full-bleed (0.5% maximo de pixeles fuera del color dominante), foreground\n"
            "con alfa, y dhash(legacy mdpi) vs icono de ficha.\n"
            "\n"
            "Exit: 0 PASS / 1 no cumple el contrato / 2 no se pudo leer la entrada."
        ),
    )
    parser.add_argument("--aab", required=True, type=Path, help="AAB a verificar (el que se publica)")
    parser.add_argument(
        "--store-icon",
        type=Path,
        default=DEFAULT_STORE_ICON,
        help="PNG 512 de la ficha de Play (default: Docs/release/assets/store-icon-512.png)",
    )
    parser.add_argument(
        "--dhash-max",
        type=int,
        default=8,
        help="distancia de Hamming maxima contra el icono de ficha, sobre 64 (default: 8)",
    )
    return parser


def main(argv: list[str]) -> int:
    args = build_parser().parse_args(argv)
    if not args.aab.is_file():
        print("FALLA: no existe el AAB: %s" % args.aab, file=sys.stderr)
        return 2
    if args.dhash_max < 0 or args.dhash_max > 64:
        print("FALLA: --dhash-max debe estar entre 0 y 64", file=sys.stderr)
        return 2

    report, exit_code = verify(args.aab, args.store_icon, args.dhash_max)
    print_report(report, exit_code)
    return exit_code


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
