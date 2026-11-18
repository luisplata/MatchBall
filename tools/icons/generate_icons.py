#!/usr/bin/env python3
"""Genera los PNG de icono de launcher Android (y el de la ficha de Play) desde el artwork canonico.

Fuente de verdad: ``Assets/Sprites/matchballance_icon.png`` (512x512 RGB, verde #22B14C
full-bleed, dos circulos rellenos #ED1C24 / #3F48CC, wordmark abajo).

El launcher NO lleva wordmark: se recorta la banda del arte (circulos) y se reescala
la diagonal de ese bbox al 60% del lado del canvas, centrada. Se usa LANCZOS en un
unico paso desde el original: no se redibuja nada, no se encadena resampleo.

Uso:
    python tools/icons/generate_icons.py            # escribe los 5 PNG
    python tools/icons/generate_icons.py --check    # exit 1 si algun PNG difiere en pixeles
    python tools/icons/generate_icons.py --manifest # imprime sha256 de los 5 PNG

``--check`` compara modo + tamano + bytes de pixel decodificados, NO el archivo:
el byte comprimido depende de la version de zlib de cada maquina.
"""

from __future__ import annotations

import hashlib
import math
import sys
from pathlib import Path

from PIL import Image

REPO_ROOT = Path(__file__).resolve().parents[2]
SOURCE = REPO_ROOT / "Assets" / "Sprites" / "matchballance_icon.png"
ICON_DIR = REPO_ROOT / "Assets" / "Art" / "AppIcon"
STORE_DIR = REPO_ROOT / "Docs" / "release" / "assets"

# Paleta canonica del artwork (literal, no muestreada).
GREEN = (34, 177, 76)
RED = (237, 28, 36)
BLUE = (63, 72, 204)

# BBox del arte a launcher (circulos + contorno) en la fuente 512, convencion
# exclusiva: (left, upper, right, lower). El wordmark vive en y >= 383: fuera del crop.
ART_BBOX = (19, 124, 485, 341)
ART_BBOX_TOLERANCE = 1
SOURCE_SIZE = (512, 512)

# Diagonal del bbox = 60% del lado del canvas. 60% (y no 61.1%) para cumplir a la vez
# la safe zone radial (max_dist <= 132 en 432) y la lectura literal de R2 (diagonal <= 264).
DIAGONAL_RATIO = 0.60
SAFE_ZONE_DIAMETER_RATIO = 66.0 / 108.0  # 0.6111: safe zone adaptativa sobre canvas 108dp

# Keying del foreground: se compara contra el verde literal. La tolerancia por canal absorbe
# el ruido de redondeo de LANCZOS (+-1..3) sobre las zonas planas de verde; cualquier pixel
# de mezcla real (hacia negro/rojo/azul) queda a decenas de unidades y sigue opaco.
KEY_TOLERANCE = 8

# Los slots que Unity genera por kind; el asset se comparte entre las 6 densidades,
# el canvas mas grande es el que define la resolucion del asset.
ADAPTIVE_CANVAS = 432
LEGACY_CANVAS = 192
STORE_CANVAS = 512

OUTPUTS = (
    # (ruta, canvas, modo, rol)
    (ICON_DIR / "app_icon_background.png", ADAPTIVE_CANVAS, "RGB", "adaptive layer 0 (background)"),
    (ICON_DIR / "app_icon_foreground.png", ADAPTIVE_CANVAS, "RGBA", "adaptive layer 1 (foreground)"),
    (ICON_DIR / "app_icon_legacy.png", LEGACY_CANVAS, "RGB", "legacy"),
    (ICON_DIR / "app_icon_round.png", LEGACY_CANVAS, "RGB", "round"),
    (STORE_DIR / "store-icon-512.png", STORE_CANVAS, "RGBA", "Play listing icon"),
)


class ArtworkError(Exception):
    """El artwork fuente no es el canonico: hay que parar, no regenerar a ciegas."""


def load_source() -> Image.Image:
    if not SOURCE.is_file():
        raise ArtworkError("falta el artwork fuente: %s" % SOURCE.relative_to(REPO_ROOT))
    with Image.open(SOURCE) as im:
        im.load()
        if im.size != SOURCE_SIZE:
            raise ArtworkError("la fuente mide %dx%d, se esperaba %dx%d" % (im.size + SOURCE_SIZE))
        if im.mode != "RGB":
            raise ArtworkError("la fuente esta en modo %s, se esperaba RGB (sin alpha)" % im.mode)
        return im.copy()


def art_bbox(im: Image.Image) -> tuple[int, int, int, int]:
    """BBox del arte de launcher: todo pixel != verde por encima de la banda del wordmark."""
    w, h = im.size
    px = im.load()
    wordmark_top = 350
    left, upper, right, lower = w, wordmark_top, -1, -1
    for y in range(wordmark_top):
        for x in range(w):
            if px[x, y] != GREEN:
                if x < left:
                    left = x
                if x > right:
                    right = x
                if y < upper:
                    upper = y
                if y > lower:
                    lower = y
    if right < 0 or lower < 0:
        raise ArtworkError("no se encontro arte sobre el verde en la fuente")
    return (left, upper, right + 1, lower + 1)


def validate_source(im: Image.Image) -> dict:
    bbox = art_bbox(im)
    for got, want, name in zip(bbox, ART_BBOX, ("left", "upper", "right", "lower")):
        if abs(got - want) > ART_BBOX_TOLERANCE:
            raise ArtworkError(
                "bbox del arte %r no coincide con el esperado %r (%s=%d, se esperaba %d +/-%d): "
                "alguien cambio el artwork" % (bbox, ART_BBOX, name, got, want, ART_BBOX_TOLERANCE)
            )

    colors = im.getcolors(maxcolors=1 << 24) or []
    palette = {c for _, c in colors}
    missing = [c for c in (GREEN, RED, BLUE) if c not in palette]
    if missing:
        raise ArtworkError("la fuente perdio colores de marca: %r" % (missing,))

    wordmark = any(y >= 350 and x < im.width and im.getpixel((x, y)) != GREEN
                   for y in range(350, im.height) for x in range(im.width))
    return {"bbox": bbox, "palette_size": len(palette), "has_wordmark": wordmark}


def frame(canvas: int, bbox: tuple[int, int, int, int]) -> tuple[int, int, int, int]:
    """Tamano de la pieza y offsets para dejar el bbox centrado con diagonal = 60% del canvas."""
    bw = bbox[2] - bbox[0]
    bh = bbox[3] - bbox[1]
    diagonal = math.hypot(bw, bh)
    scale = (DIAGONAL_RATIO * canvas) / diagonal
    piece_w = int(round(bw * scale))
    piece_h = int(round(bh * scale))
    offset_x = (canvas - piece_w + 1) // 2
    offset_y = (canvas - piece_h + 1) // 2
    return piece_w, piece_h, offset_x, offset_y


def render(source: Image.Image, bbox: tuple[int, int, int, int], canvas: int, mode: str) -> Image.Image:
    piece_w, piece_h, offset_x, offset_y = frame(canvas, bbox)
    piece = source.crop(bbox).resize((piece_w, piece_h), Image.LANCZOS)

    if mode == "RGBA" and canvas == ADAPTIVE_CANVAS:
        # Foreground: keying contra el verde. Verde (con tolerancia) -> alpha 0, resto opaco.
        # No se premultiplica a proposito: la franja de antialiasing queda opaca y el
        # fringe es invisible porque el background es exactamente ese verde.
        out = Image.new("RGBA", (canvas, canvas), (0, 0, 0, 0))
        out.paste(piece, (offset_x, offset_y))
        pixels = out.load()
        for y in range(offset_y, offset_y + piece_h):
            for x in range(offset_x, offset_x + piece_w):
                r, g, b, _ = pixels[x, y]
                near_green = max(abs(r - GREEN[0]), abs(g - GREEN[1]), abs(b - GREEN[2])) <= KEY_TOLERANCE
                pixels[x, y] = (r, g, b, 0 if near_green else 255)
        return out

    if mode == "RGBA":
        # Ficha de Play: 32-bit RGBA con alpha 255 en todo el lienzo (Play lo exige).
        out = Image.new("RGBA", (canvas, canvas), GREEN + (255,))
        out.paste(piece, (offset_x, offset_y))
        return out

    out = Image.new("RGB", (canvas, canvas), GREEN)
    out.paste(piece, (offset_x, offset_y))
    return out


def save(im: Image.Image, path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    # Sin dpi= (evita el chunk pHYs), sin metadatos: mismo input -> mismos bytes.
    im.save(path, format="PNG", optimize=False, compress_level=9)


def measure_foreground(im: Image.Image) -> dict:
    """Geometria del foreground: bbox del alpha, centro, radio maximo y diagonal."""
    w, h = im.size
    alpha = im.getchannel("A").load()
    center = w / 2.0
    left, upper, right, lower = w, h, -1, -1
    max_radius = 0.0
    for y in range(h):
        for x in range(w):
            if alpha[x, y] == 0:
                continue
            if x < left:
                left = x
            if x > right:
                right = x
            if y < upper:
                upper = y
            if y > lower:
                lower = y
            radius = math.hypot((x + 0.5) - center, (y + 0.5) - center)
            if radius > max_radius:
                max_radius = radius
    if right < 0:
        raise ArtworkError("el foreground quedo sin contenido visible")
    bbox_center_x = (left + right + 1) / 2.0
    bbox_center_y = (upper + lower + 1) / 2.0
    return {
        "bbox": (left, upper, right + 1, lower + 1),
        "width": right - left + 1,
        "height": lower - upper + 1,
        "diagonal": math.hypot(right - left + 1, lower - upper + 1),
        "max_radius": max_radius,
        "center_offset": (bbox_center_x - center, bbox_center_y - center),
    }


def assert_geometry(source_bbox: tuple[int, int, int, int], foreground: Image.Image, verbose: bool) -> dict:
    """Asserts de R2: safe zone radial, diagonal, centrado y cero wordmark."""
    canvas = foreground.width
    piece_w, piece_h, offset_x, offset_y = frame(canvas, source_bbox)
    expected_bbox = (offset_x, offset_y, offset_x + piece_w, offset_y + piece_h)
    m = measure_foreground(foreground)

    safe_diameter = SAFE_ZONE_DIAMETER_RATIO * canvas
    failures = []

    if m["max_radius"] > safe_diameter / 2.0:
        failures.append("radio maximo %.2f > safe zone %.2f (canvas %d)"
                        % (m["max_radius"], safe_diameter / 2.0, canvas))
    if m["diagonal"] > safe_diameter + 0.5:
        failures.append("diagonal del bbox %.2f > %.2f" % (m["diagonal"], safe_diameter))
    if abs(m["center_offset"][0]) > 2 or abs(m["center_offset"][1]) > 2:
        failures.append("centro desviado %.2f, %.2f px (tolerancia 2)"
                        % m["center_offset"])
    if m["bbox"] != expected_bbox:
        failures.append("bbox %r != esperado %r (aparecio contenido fuera del crop: wordmark?)"
                        % (m["bbox"], expected_bbox))
    if m["bbox"][0] < 1 or m["bbox"][1] < 1 or m["bbox"][2] > canvas - 1 or m["bbox"][3] > canvas - 1:
        failures.append("el bbox toca el borde del canvas %d" % canvas)

    if verbose:
        print("  geometria fg %d: bbox=%r (%dx%d) diagonal=%.2f radio_max=%.2f "
              "centro=(%+.2f,%+.2f) safe=%.2f"
              % (canvas, m["bbox"], m["width"], m["height"], m["diagonal"], m["max_radius"],
                 m["center_offset"][0], m["center_offset"][1], safe_diameter))

    if failures:
        raise ArtworkError("geometria fuera de contrato: " + "; ".join(failures))
    return m


def build_all(verbose: bool = True) -> dict:
    """Renderiza los 5 PNG en memoria, valida contrato y devuelve {ruta: imagen}."""
    source = load_source()
    info = validate_source(source)
    if verbose:
        print("fuente %s 512x512 RGB  bbox arte %r (%.0fx%.0f, diagonal %.2f)  colores %d  wordmark=%s"
              % (SOURCE.name, info["bbox"], info["bbox"][2] - info["bbox"][0],
                 info["bbox"][3] - info["bbox"][1],
                 math.hypot(info["bbox"][2] - info["bbox"][0], info["bbox"][3] - info["bbox"][1]),
                 info["palette_size"], info["has_wordmark"]))
    rendered = {}
    for path, canvas, mode, role in OUTPUTS:
        im = render(source, info["bbox"], canvas, mode)
        if verbose:
            print("  %-34s %3dx%d %-4s  %s" % (path.relative_to(REPO_ROOT), canvas, canvas, mode, role))
        rendered[path] = im
    assert_geometry(info["bbox"], rendered[ICON_DIR / "app_icon_foreground.png"], verbose)
    return rendered


def do_check(rendered: dict) -> int:
    failures = []
    for path, im in rendered.items():
        rel = path.relative_to(REPO_ROOT)
        if not path.is_file():
            failures.append("%s: falta (correr sin --check para generarlo)" % rel)
            continue
        with Image.open(path) as current:
            current.load()
            if current.size != im.size:
                failures.append("%s: mide %dx%d, se esperaba %dx%d"
                                % (rel, current.size + im.size))
                continue
            if current.mode != im.mode:
                failures.append("%s: modo %s, se esperaba %s" % (rel, current.mode, im.mode))
                continue
            if current.tobytes() != im.tobytes():
                failures.append("%s: los pixeles difieren del artwork commiteado" % rel)
    if failures:
        print("FALLA: los PNG commiteados no coinciden con el artwork:", file=sys.stderr)
        for f in failures:
            print("  - " + f, file=sys.stderr)
        print("  Regenerar: python tools/icons/generate_icons.py", file=sys.stderr)
        return 1
    print("OK: %d archivos identicos a los commiteados" % len(rendered))
    return 0


def do_manifest(rendered: dict) -> int:
    for path in rendered:
        rel = path.relative_to(REPO_ROOT)
        if not path.is_file():
            print("FALTA  " + str(rel), file=sys.stderr)
            return 1
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        print("%s  %s" % (digest, rel))
    return 0


def main(argv: list[str]) -> int:
    known = {"--check", "--manifest"}
    unknown = [a for a in argv if a not in known]
    if unknown:
        print("argumentos desconocidos: %r (validos: %s)" % (unknown, " ".join(sorted(known))),
              file=sys.stderr)
        return 2

    try:
        rendered = build_all(verbose="--manifest" not in argv)
    except ArtworkError as err:
        print("FALLA: %s" % err, file=sys.stderr)
        return 1

    if "--check" in argv:
        return do_check(rendered)
    if "--manifest" in argv:
        return do_manifest(rendered)

    for path, im in rendered.items():
        save(im, path)
    print("escritos %d PNG" % len(rendered))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
