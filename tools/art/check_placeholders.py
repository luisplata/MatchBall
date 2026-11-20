#!/usr/bin/env python3
"""Gate de los placeholders PNG de la sec.9: valida bytes, pixeles y ledger.

Es la mitad "check" del generador (plan B declarado en ``sdd/placeholders-arte-v2/tasks``:
si ``generate_placeholders.py`` pasa de 420 lineas, ``--check`` y sus validadores se mueven
a este archivo **sin re-derivar nada**: importa el parser de §9 y el render del generador,
asi que el gate y la generacion no pueden divergir.

Que mide (regla -> req):

    IHDR            16-24 big-endian crudos == px_entrega; 8 bit, colorType 6   APH-2
    CHUNK           sin tIME / pHYs / tEXt / iTXt                              APH-7
    NAME            regex + unicidad + sin colision con Assets/                APH-3
    PIXEL           tobytes() RGBA contra el render derivado, con (x,y) y canal APH-8
    SHA             bytes distintos con pixeles iguales (nunca inventa coordenada) APH-8
    NEW-BLOCKED     una BLOCKED_* con PNG en disco                             APH-10
    MOTIVO          motivo vacio o generico en una BLOCKED_*                   APH-11
    LEDGER          px / border / familia / path / sha256 de cada fila         APH-9, APH-10
    GUID            32 hex, igual al .meta, sin repetir                        APH-4
    FRAME           esquinas #FF00FF opaco, cero alfa parcial                  APH-5, APH-6
    ALFA            esquinas de silueta en 0, sin alfa parcial                 APH-5
    FLAT4           relleno solido #FFFFFF sin marcas                          APH-5
    FIRMA           tramo #FF00FF >= 6 px si min(w,h) >= 16                    APH-5
    FRAME-FILL      los 3 canales del relleno >= 232                          APH-6
    LEGIBLE         texto/placa >= 4.5:1 pre-tinte                            APH-6b
    SURVIVE         L_tinte(relleno) >= 0.004 y delta >= 0.002 (solo con host) APH-6b

El orden es el de la spec: primero los bytes crudos (IHDR), despues el pixel decodificado,
y el sha como atajo. Un solo canal de un solo pixel distinto es drift.

``--hosts`` es el ``m_Color`` vivo del host (csv ``pieza,r,g,b``, 0..1). Sin hosts, la
metrica ``SURVIVE`` no tiene con que medirse y se reporta como no evaluable: el host existe
recien cuando hay slot cableado (slice 2). El caso de la spec —host oscuro, pieza que no
sobrevive— se reproduce con ``--hosts`` y un relleno oscuro; con el relleno claro que exige
APH-6 la pieza sobrevive incluso a 0.07 (ver la evidencia del change).

Exit code: 0 = PASS / 1 = hay hallazgos / 2 = §9 ilegible.

Uso:
    python tools/art/check_placeholders.py
    python tools/art/generate_placeholders.py --check          # mismo gate, mismo exit
    python tools/art/check_placeholders.py --out Temp/art --hosts Temp/hosts.csv
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import io
import re
import struct
import sys
from pathlib import Path

from PIL import Image

import generate_placeholders as gen

FORBIDDEN_CHUNKS = (b"tIME", b"pHYs", b"tEXt", b"iTXt")
LEGIBLE_MIN = 4.5
SURVIVE_FLOOR = 0.004
SURVIVE_DELTA = 0.002


def wcag_luminance(color: tuple[int, int, int]) -> float:
    channels = []
    for value in color:
        srgb = value / 255.0
        channels.append(srgb / 12.92 if srgb <= 0.04045 else ((srgb + 0.055) / 1.055) ** 2.4)
    return 0.2126 * channels[0] + 0.7152 * channels[1] + 0.0722 * channels[2]


def contrast_ratio(first: tuple[int, int, int], second: tuple[int, int, int]) -> float:
    high, low = sorted((wcag_luminance(first), wcag_luminance(second)), reverse=True)
    return (high + 0.05) / (low + 0.05)


def tinted_luminance(color: tuple[int, int, int], host: tuple[float, float, float]) -> float:
    """Gamma: el `m_Color` multiplica el valor sRGB, no el lineal (design D4)."""
    return wcag_luminance(tuple(max(0, min(255, round(channel * factor)))
                                for channel, factor in zip(color, host)))


def png_chunks(data: bytes) -> list[bytes]:
    chunks, offset = [], 8
    while offset + 8 <= len(data):
        length = struct.unpack(">I", data[offset:offset + 4])[0]
        chunks.append(data[offset + 4:offset + 8])
        offset += 12 + length
    return chunks


def magenta_run(image: Image.Image) -> int:
    """Tramo contiguo horizontal mas largo de #FF00FF opaco."""
    width, height = image.size
    pixels = image.load()
    best = 0
    for y in range(height):
        run = 0
        for x in range(width):
            run = run + 1 if pixels[x, y] == gen.MAGENTA + (255,) else 0
            best = max(best, run)
    return best


def fill_color(image: Image.Image, familia: str):
    """Color de relleno medido: el dominante opaco que no es ninguna marca del lenguaje."""
    colors = image.getcolors(maxcolors=1 << 24) or []
    if familia == "flat4":
        return colors[0][1][:3] if len(colors) == 1 else None
    marks = (gen.MAGENTA, gen.CYAN, gen.WHITE, gen.PLATE)
    candidates = [(count, color[:3]) for count, color in colors
                  if color[3] == 255 and color[:3] not in marks]
    return max(candidates)[1] if candidates else None


def validate(entry, data: bytes, image: Image.Image, host) -> list[str]:
    """Reglas de pixel y archivo de una pieza. Devuelve hallazgos legibles."""
    failures: list[str] = []
    width, height = entry.row.px
    label = "%-40s" % entry.row.pieza

    if data[:8] != gen.PNG_SIGNATURE:
        return ["%s IHDR: firma PNG alterada" % label]
    header_w, header_h = struct.unpack(">II", data[16:24])
    if (header_w, header_h) != (width, height):
        failures.append("%s IHDR: %dx%d, px_entrega %dx%d"
                        % (label, header_w, header_h, width, height))
    if data[24] != 8 or data[25] != 6:
        failures.append("%s IHDR: bitDepth %d colorType %d, se exige 8/6 (RGBA)"
                        % (label, data[24], data[25]))
    for chunk in png_chunks(data):
        if chunk in FORBIDDEN_CHUNKS:
            failures.append("%s CHUNK: %s varia entre corridas" % (label, chunk.decode("ascii")))

    pixels = image.load()
    corners = (pixels[0, 0], pixels[width - 1, 0], pixels[0, height - 1], pixels[width - 1, height - 1])
    if entry.familia == "frame":
        if corners != (gen.MAGENTA + (255,),) * 4:
            failures.append("%s FRAME: las 4 esquinas no son #FF00FF opaco" % label)
        if any(pixels[x, y][3] != 255 for y in range(height) for x in range(width)):
            failures.append("%s FRAME: hay pixeles con alfa < 1 (APA-2 no veria el relleno)" % label)
    elif entry.familia == "silhouette":
        if any(corner[3] != 0 for corner in corners):
            failures.append("%s ALFA: las 4 esquinas no son alfa 0" % label)
        if any(0 < pixels[x, y][3] < 255 for y in range(height) for x in range(width)):
            failures.append("%s ALFA: hay alfa parcial (marca no opaca, APH-6)" % label)
    else:
        colors = image.getcolors(maxcolors=2) or []
        if len(colors) != 1 or colors[0][1] != gen.WHITE + (255,) or colors[0][0] != width * height:
            failures.append("%s FLAT4: no es relleno solido #FFFFFF sin marcas" % label)

    if min(width, height) >= 16 and entry.familia != "flat4":
        run = magenta_run(image)
        if run < gen.NOTCH[0]:
            failures.append("%s FIRMA: tramo #FF00FF de %d px, minimo %d"
                            % (label, run, gen.NOTCH[0]))
    if entry.familia in ("frame", "flat4"):
        fill = fill_color(image, entry.familia)
        if fill is None or min(fill) < 232:
            failures.append("%s FRAME-FILL: relleno %s, los 3 canales deben ser >= 232"
                            % (label, "sin relleno visible" if not fill
                               else "#%02X%02X%02X" % tuple(fill)))
    has_plate = any(color[:3] == gen.PLATE and color[3] == 255
                    for _, color in (image.getcolors(maxcolors=1 << 24) or []))
    if has_plate:
        ratio = contrast_ratio(gen.WHITE, gen.PLATE)
        if ratio < LEGIBLE_MIN:
            failures.append("%s LEGIBLE: texto/placa %.2f:1, minimo %.1f"
                            % (label, ratio, LEGIBLE_MIN))
    if host is not None:
        failures.extend(survive(entry, image, host))
    return failures


def survive(entry, image: Image.Image, host: tuple[float, float, float]) -> list[str]:
    label = "%-40s" % entry.row.pieza
    colors = [color for _, color in (image.getcolors(maxcolors=1 << 24) or []) if color[3] == 255]
    fill = fill_color(image, entry.familia)
    if not colors or fill is None:
        return []
    marks = [color[:3] for color in colors if color[:3] != fill] or [gen.MAGENTA]
    darkest = min(marks, key=wcag_luminance)
    lit_fill = tinted_luminance(fill, host)
    lit_dark = tinted_luminance(darkest, host)
    failures = []
    if lit_fill < SURVIVE_FLOOR:
        failures.append("%s SURVIVE: L_tinte(relleno) %.5f < %.3f con host %s"
                        % (label, lit_fill, SURVIVE_FLOOR, "/".join("%.2f" % channel for channel in host)))
    elif lit_fill - lit_dark < SURVIVE_DELTA:
        failures.append("%s SURVIVE: L_tinte(relleno) - L_tinte(marca) %.5f < %.3f"
                        % (label, lit_fill - lit_dark, SURVIVE_DELTA))
    return failures


def read_hosts(path: Path) -> dict[str, tuple[float, float, float]]:
    hosts: dict[str, tuple[float, float, float]] = {}
    with path.open(newline="", encoding="utf-8") as handle:
        for row in csv.DictReader(handle):
            hosts[row["pieza"].strip()] = (float(row["r"]), float(row["g"]), float(row["b"]))
    return hosts


def verify_pixels(entry, data: bytes, expected: Image.Image) -> tuple[list[str], str]:
    """Compara IHDR, luego pixeles decodificados (APH-8). Devuelve (hallazgos, sha)."""
    sha = hashlib.sha256(data).hexdigest()
    if data[:8] != gen.PNG_SIGNATURE:
        return ["%-40s IHDR: firma PNG alterada" % entry.row.pieza], sha
    with Image.open(io.BytesIO(data)) as raw:
        raw.load()
        if raw.mode != "RGBA":
            return ["%-40s IHDR: modo %s, se exige RGBA (colorType 6)"
                    % (entry.row.pieza, raw.mode)], sha
        actual = raw.tobytes()
    wanted = expected.tobytes()
    if actual == wanted:
        return [], sha
    for index, (left, right) in enumerate(zip(actual, wanted)):
        if left != right:
            pixel, channel = divmod(index, 4)
            x, y = pixel % entry.row.px[0], pixel // entry.row.px[0]
            return ["%-40s PIXEL: (%d,%d) canal %s %d->%d"
                    % (entry.row.pieza, x, y, "RGBA"[channel], right, left)], sha
    return [], sha


def check_guid(pieza: str, guid: str, render_dir: Path) -> list[str]:
    if not re.fullmatch(r"[0-9a-f]{32}", guid):
        return ["%s GUID: '%s' no es 32 hex" % (pieza, guid)]
    meta = Path(str(render_dir / pieza) + ".meta")
    if not meta.is_file():
        return ["%s GUID: falta %s.meta" % (pieza, pieza)]
    match = gen.GUID_RE.search(meta.read_text(encoding="utf-8", errors="replace"))
    if not match:
        return ["%s GUID: el .meta no tiene guid" % pieza]
    if match.group(1) != guid:
        return ["%s GUID: ledger %s != .meta %s" % (pieza, guid, match.group(1))]
    return []


def run(entries, render_dir: Path, ledger_path: Path, hosts_path=None) -> int:
    hosts = read_hosts(hosts_path) if hosts_path else {}
    failures = gen.collisions(entries, render_dir)
    ledger = gen.read_ledger(ledger_path)
    if not ledger:
        print("INFO  sin ledger en %s: se compara solo contra el render derivado"
              % gen.display(ledger_path))
    rendered = gen.render_all(entries)
    for entry in entries:
        pieza = entry.row.pieza
        path = render_dir / pieza
        if not entry.generated:
            if path.exists():
                failures.append("%s NEW-BLOCKED: la pieza esta bloqueada y hay PNG en disco" % pieza)
            row = ledger.get(pieza)
            if row and (not row["motivo"] or any(token in row["motivo"].casefold()
                                                 for token in gen.GENERIC_MOTIVO)):
                failures.append("%s MOTIVO: motivo vacio o generico (APH-11)" % pieza)
            continue
        image, data, _ = rendered[pieza]
        if not path.is_file():
            failures.append("%s FALTA: no existe %s" % (pieza, gen.display(path)))
            continue
        on_disk = path.read_bytes()
        findings, sha = verify_pixels(entry, on_disk, image)
        failures.extend(findings)
        if not findings and sha != hashlib.sha256(data).hexdigest():
            failures.append("%s SHA: bytes distintos del render derivado (pixeles iguales)" % pieza)
        failures.extend(validate(entry, on_disk, image, hosts.get(pieza)))
        row = ledger.get(pieza)
        if not row:
            failures.append("%s LEDGER: sin fila (37 filas obligatorias)" % pieza)
            continue
        expected = {"estado": "GENERATED", "px": "%dx%d" % entry.row.px,
                    "border": "/".join(map(str, entry.row.border)),
                    "familia": entry.familia, "path": "Assets/Sprites/" + pieza}
        for column, value in expected.items():
            if row.get(column) != value:
                failures.append("%s LEDGER: %s '%s' != '%s'"
                                % (pieza, column, row.get(column), value))
        if row.get("sha256") != sha and not findings:
            failures.append("%s SHA: ledger %s != disco %s"
                            % (pieza, row.get("sha256") or "(vacio)", sha))
        if row.get("guid"):
            failures.extend(check_guid(pieza, row["guid"], render_dir))
    for failure in failures:
        print(failure)
    total = sum(1 for entry in entries if entry.generated)
    print("RESULTADO   %s  (%d GENERATED, %d bloqueadas, %d hallazgos)"
          % ("PASS" if not failures else "FAIL", total, len(entries) - total, len(failures)))
    return 1 if failures else 0


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Gate de los placeholders PNG de la sec.9.")
    parser.add_argument("--out", type=Path, help="directorio de los PNG (default Assets/Sprites)")
    parser.add_argument("--ledger", type=Path, help="ledger derivado")
    parser.add_argument("--contract", type=Path, help="manifiesto con el bloque ```csv de la sec.9")
    parser.add_argument("--hosts", type=Path, help="csv pieza,r,g,b con el m_Color vivo del host")
    return parser


def main(argv: list[str]) -> int:
    args = build_parser().parse_args(argv)
    render_dir = (args.out or gen.SPRITES_DIR).resolve()
    ledger_path = args.ledger or (render_dir / "placeholders-v1.csv" if args.out else gen.DEFAULT_LEDGER)
    try:
        entries = gen.classify(gen.read_manifest(args.contract or gen.MANIFEST))
    except gen.ContractError as error:
        print("ERROR: %s" % error, file=sys.stderr)
        return 2
    return run(entries, render_dir, ledger_path, args.hosts)


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
