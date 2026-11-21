#!/usr/bin/env python3
"""Gate de las capturas: el rig es versionado (DRL-3) y el diff cae dentro del bbox (DRL-4).

Dos modos, un solo archivo porque las dos reglas miran el mismo manifiesto:

    --verify-manifest [PATH]     DRL-3: el rig esta versionado, hay una captura por fila
                                 `cableado` con `captura` declarada en el mapa, los px
                                 declarados son los del PNG y ninguna captura es uniforme
                                 (>= 2 colores distintos). Una fila `cableado` sin captura
                                 solo pasa si esta declarada en `art_ledger.CAPTURE_EXCEPTIONS`.
    --piece P --before A --after B   DRL-4: `changed ⊆ bbox` y `changed >= 1` sobre el marco
                                 completo. Sin igualdad de hash: las capturas dependen de
                                 Unity y del frame, y el hash no dice DONDE cambio.

Por que el diff se mide sobre el marco y no sobre el recorte: un recorte ES el bbox, asi que
`changed ⊆ bbox` se cumpliria siempre. `DIFF-OUTSIDE` solo tiene sentido si la imagen comparada
incluye pixels fuera del nodo, y esa es la unica forma de probar que un swap de bytes no movio
nada mas. El rig escribe los dos marcos del mismo estado (`CaptureSwapDrill`) en `Temp/`.

Uso:
    python tools/art/check_capture_diff.py --verify-manifest
    python tools/art/check_capture_diff.py --piece ui_tutorial_btn_skip_normal_01.png \
        --before Temp/art-captures/drill-pass/before.png \
        --after  Temp/art-captures/drill-pass/after.png

Exit: 0 = PASS / 1 = hay hallazgos / 2 = el mapa o el manifiesto no se pueden leer.
"""

from __future__ import annotations

import argparse
import csv
import re
import sys
from pathlib import Path

from PIL import Image, ImageChops

import art_ledger as ledger_mod

TOOLS = Path(__file__).resolve().parent
REPO_ROOT = Path(__file__).resolve().parents[2]
MAP_FILE = REPO_ROOT / "Docs" / "art" / "placeholders-v1.md"
MANIFEST = REPO_ROOT / "Docs" / "art" / "evidence" / "captures-v1.md"
CAPTURES = REPO_ROOT / "Docs" / "art" / "captures" / "placeholders-v1"
RIG = REPO_ROOT / "Assets" / "Editor" / "ArtContract" / "ArtCaptureRig.cs"

FENCED_CSV_RE = re.compile(r"(?ms)^```csv\r?\n(.*?)^```")
SIZE_RE = re.compile(r"^(\d+)x(\d+)$")
BBOX_RE = re.compile(r"^(\d+),(\d+),(\d+),(\d+)$")
COLUMNS = ("pieza", "px", "marco", "bbox", "colores")
MAP_COLUMNS = ("pieza", "slot_tipo", "asset", "nodo", "componente", "campo", "estado",
               "tint_original", "transition_original", "captura")


class ManifestError(Exception):
    """El mapa o el manifiesto no se pueden leer: el gate no opina (exit 2)."""


def fenced_csv(path: Path) -> tuple[tuple[str, ...], list[list[str]]]:
    try:
        text = path.read_text(encoding="utf-8")
    except OSError as error:
        raise ManifestError("no se pudo leer %s: %s" % (path, error)) from error
    blocks = FENCED_CSV_RE.findall(text)
    if len(blocks) != 1:
        raise ManifestError("%s tiene %d bloques ```csv: tiene que ser uno" % (path.name, len(blocks)))
    reader = csv.reader(blocks[0].splitlines())
    header = tuple(cell.strip() for cell in next(reader, ()))
    rows = [[cell.strip() for cell in raw] for raw in reader if any(cell.strip() for cell in raw)]
    return header, rows


def read_map(path: Path = MAP_FILE) -> list[dict]:
    header, rows = fenced_csv(path)
    if header != MAP_COLUMNS:
        raise ManifestError("columnas inesperadas en el mapa: %s" % (header,))
    out = []
    for cells in rows:
        if len(cells) != len(MAP_COLUMNS):
            raise ManifestError("fila con %d columnas: %s" % (len(cells), cells[0]))
        out.append(dict(zip(MAP_COLUMNS, cells)))
    return out


def read_manifest(path: Path = MANIFEST) -> list[dict]:
    header, rows = fenced_csv(path)
    if header != COLUMNS:
        raise ManifestError("%s tiene columnas %s, se esperan %s" % (path.name, header, COLUMNS))
    out = []
    for cells in rows:
        if len(cells) != len(COLUMNS):
            raise ManifestError("fila con %d columnas en %s: %s"
                                % (len(cells), path.name, cells[0]))
        out.append(dict(zip(COLUMNS, cells)))
    return out


def expected_captures(rows: list[dict]) -> dict[str, str]:
    """`pieza -> archivo` de las filas `cableado` que declaran captura en el mapa."""
    return {row["pieza"]: row["captura"] for row in rows
            if row["estado"] == "cableado" and row["captura"]}


def unclaimed(rows: list[dict]) -> list[str]:
    """Filas `cableado` sin captura declarada: tienen que estar en CAPTURE_EXCEPTIONS."""
    return [row["pieza"] for row in rows if row["estado"] == "cableado" and not row["captura"]]


def parse_size(text: str) -> tuple[int, int] | None:
    match = SIZE_RE.match((text or "").strip())
    return (int(match.group(1)), int(match.group(2))) if match else None


def parse_bbox(text: str) -> tuple[int, int, int, int] | None:
    match = BBOX_RE.match((text or "").strip())
    if not match:
        return None
    return tuple(int(match.group(index)) for index in range(1, 5))


def completed_file(path: Path) -> bool:
    """El PNG se decodifica sin `truncated image` ni metadata incoherente."""
    try:
        with Image.open(path) as image:
            image.load()
        return True
    except OSError:
        return False


def display(path: Path) -> str:
    """Path relativo al repo cuando se puede; asoluto si vive fuera del working tree.

    Un manifiesto de `Temp/` se lee relativo; uno de afuera (un sandbox, un archivo de review)
    tambien tiene que poder nombrarse sin reventar en `relative_to`.
    """
    try:
        return Path(path).resolve().relative_to(REPO_ROOT).as_posix()
    except ValueError:
        return Path(path).as_posix()


def verify_manifest(path: Path = MANIFEST, map_path: Path = MAP_FILE,
                    captures: Path = CAPTURES) -> tuple[list[str], list[str]]:
    """DRL-3: rig versionado, cobertura exacta, px declarados y capturas no uniformes.

    `map_path` y `captures` se pasan explicitamente (no por global) para que el selftest pueda
    medir el gate sobre un mapa y un directorio de `Temp/` sin tocar `Docs/`.
    """
    failures: list[str] = []
    notes: list[str] = []
    rows = read_map(map_path)
    manifest = read_manifest(path)
    wanted = expected_captures(rows)
    exceptions = dict(ledger_mod.CAPTURE_EXCEPTIONS)

    if not ledger_mod.git_tracked(RIG):
        failures.append("RIG: %s no esta versionado (`git ls-files --error-unmatch`): sin el "
                        "archivo, la captura no se puede regenerar"
                        % RIG.relative_to(REPO_ROOT).as_posix())

    manifest_index: dict[str, dict] = {}
    for entry in manifest:
        pieza = entry["pieza"]
        if pieza in manifest_index:
            failures.append("CAPTURA: %s repetida en %s" % (pieza, path.name))
        manifest_index[pieza] = entry

    for pieza, captura in sorted(wanted.items()):
        entry = manifest_index.get(pieza)
        if entry is None:
            failures.append("CAPTURA-FALTA %s: el mapa declara `%s` y el manifiesto no la tiene"
                            % (pieza, captura))
            continue
        png = captures / captura
        if not png.is_file():
            failures.append("CAPTURA-FALTA %s: no existe %s"
                            % (pieza, png.relative_to(REPO_ROOT).as_posix()))
            continue
        if not completed_file(png):
            failures.append("CAPTURA %s: el PNG no se decodifica" % pieza)
            continue
        declared = parse_size(entry["px"])
        if declared is None:
            failures.append("CAPTURA %s: `px` declarado '%s' no es WxH" % (pieza, entry["px"]))
            continue
        marco = parse_size(entry["marco"])
        bbox = parse_bbox(entry["bbox"])
        if marco is None or bbox is None:
            failures.append("CAPTURA %s: `marco` o `bbox` ilegible (%s / %s)"
                            % (pieza, entry["marco"], entry["bbox"]))
            continue
        with Image.open(png) as image:
            measured = image.size
            colours = len(image.convert("RGBA").getcolors(maxcolors=1 << 24) or [])
        if measured != declared:
            failures.append("CAPTURA %s: el PNG mide %dx%d y el manifiesto declara %dx%d"
                            % (pieza, measured[0], measured[1], declared[0], declared[1]))
            continue
        if colours < 2:
            failures.append("CAPTURA %s: PNG de un solo color (%d): una captura uniforme no es "
                            "una captura" % (pieza, colours))
        left, top, width, height = bbox
        if width <= 0 or height <= 0:
            failures.append("CAPTURA %s: bbox vacio %s" % (pieza, entry["bbox"]))
        elif left < 0 or top < 0 or left + width > marco[0] or top + height > marco[1]:
            failures.append("CAPTURA %s: bbox %s no entra en el marco %s"
                            % (pieza, entry["bbox"], entry["marco"]))
        try:
            entry["colores"] = int(entry["colores"])
        except ValueError:
            failures.append("CAPTURA %s: `colores` declarado '%s' no es un numero"
                            % (pieza, entry["colores"]))
            continue
        if entry["colores"] != colours:
            failures.append("CAPTURA %s: el manifiesto declara %s colores y el PNG mide %d"
                            % (pieza, entry["colores"], colours))

    for pieza in sorted(set(manifest_index) - set(wanted)):
        what = "declarada" if pieza in exceptions else "no es una fila cableada con captura"
        failures.append("CAPTURA-STALE %s: fila del manifiesto %s del mapa (re-ejecutar el rig)"
                        % (pieza, what))

    for pieza in sorted(unclaimed(rows)):
        if pieza not in exceptions:
            failures.append("CAPTURA-UNDECLARED %s: fila cableada sin captura y sin declarar en "
                            "CAPTURE_EXCEPTIONS" % pieza)
    for pieza, reason in sorted(exceptions.items()):
        if pieza not in {row["pieza"] for row in rows}:
            failures.append("EXCEPCION-STALE %s: declarada como excepcion y no esta en el mapa"
                            % pieza)
        elif pieza in wanted:
            failures.append("EXCEPCION-STALE %s: declarada como excepcion y el mapa ya le declara "
                            "captura" % pieza)
        if (captures / pieza).is_file():
            failures.append("EXCEPCION %s: hay un PNG en %s y la fila declara que no hay captura"
                            % (pieza, captures.name))
        notes.append("EXCEPCION %s: %s" % (pieza, reason))

    notes.insert(0, "CAPTURAS %d/%d | marco %s | rig %s"
                 % (min(len(manifest_index), len(wanted)), len(wanted),
                    next((entry["marco"] for entry in manifest), "-"),
                    RIG.relative_to(REPO_ROOT).as_posix()))
    if ledger_mod.git_tracked(path):
        notes.append("MANIFIESTO %s versionado" % display(path))
    else:
        failures.append("EVIDENCE-UNTRACKED %s: `git ls-files --error-unmatch` no lo conoce"
                        % display(path))
    return failures, notes


def diff(before: Path, after: Path, bbox: tuple[int, int, int, int],
         width: int, height: int) -> tuple[list[str], str, int, int]:
    """DRL-4: `changed ⊆ bbox` y `changed >= 1`, pixel a pixel y sin hashes.

    La mascara sale de la diferencia por canal (los cuatro, alfa incluido): un cambio que solo
    mueve el alfa tambien cuenta, porque en un placeholder transparente el alfa ES la forma.
    """
    try:
        with Image.open(before) as image:
            first = image.convert("RGBA")
            first_size = image.size
        with Image.open(after) as image:
            second = image.convert("RGBA")
            second_size = image.size
    except OSError as error:
        return (["DIFF-SIZE: no se pudieron leer los marcos (%s)" % error], "ERROR", 0, 0)
    if first_size != second_size:
        return (["DIFF-SIZE: before %dx%d != after %dx%d"
                 % (first_size[0], first_size[1], second_size[0], second_size[1])], "ERROR", 0, 0)
    if first_size != (width, height):
        return [], "SIZE", 0, 0

    difference = ImageChops.difference(first, second)
    red, green, blue, alpha = difference.split()
    mask = ImageChops.lighter(ImageChops.lighter(red, green),
                              ImageChops.lighter(blue, alpha))
    changed = sum(mask.histogram()[1:])
    if changed == 0:
        return [], "DIFF-EMPTY", 0, 0
    left, top, box_width, box_height = bbox
    # Lo que queda despues de tapar el bbox es, exactamente, lo que cayo fuera.
    mask.paste(0, (left, top, left + box_width, top + box_height))
    outside = sum(mask.histogram()[1:])
    if outside:
        return [], "DIFF-OUTSIDE", changed, outside
    return [], "PASS", changed, 0


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description="Gate de capturas: DRL-3 y DRL-4.")
    parser.add_argument("--verify-manifest", metavar="PATH", nargs="?", const=str(MANIFEST),
                        default=None,
                        help="DRL-3: cobertura, px declarados y capturas no uniformes")
    parser.add_argument("--manifest", metavar="PATH", type=Path, default=MANIFEST,
                        help="manifiesto de capturas (default %s)" % MANIFEST)
    parser.add_argument("--piece", help="pieza del bbox, para el diff de DRL-4")
    parser.add_argument("--before", type=Path, help="marco previo al swap")
    parser.add_argument("--after", type=Path, help="marco posterior al swap")
    args = parser.parse_args(argv)

    if args.verify_manifest is not None:
        # W-3: el argumento llega relativo al cwd y el gate compara contra REPO_ROOT, que es
        # absoluto. Sin resolverlo, el comando literal de T-36 moria en `relative_to` con un
        # ValueError que no hablaba de manifiestos sino de paths.
        try:
            failures, notes = verify_manifest(Path(args.verify_manifest).resolve())
        except ManifestError as error:
            print("ERROR: %s" % error, file=sys.stderr)
            return 2
        for note in notes:
            print(note)
        for failure in failures:
            print(failure)
        print("RESULTADO   %s  (%d hallazgos)" % ("PASS" if not failures else "FAIL", len(failures)))
        return 1 if failures else 0

    if args.piece is None:
        parser.error("elegir --verify-manifest o --piece con --before/--after")

    if args.before is None or args.after is None:
        parser.error("--piece exige --before y --after")
    try:
        entries = {entry["pieza"]: entry for entry in read_manifest(args.manifest)}
    except ManifestError as error:
        print("ERROR: %s" % error, file=sys.stderr)
        return 2
    entry = entries.get(args.piece)
    if entry is None:
        print("ERROR: %s no esta en %s" % (args.piece, args.manifest), file=sys.stderr)
        return 2
    bbox = parse_bbox(entry["bbox"])
    marco = parse_size(entry["marco"])
    if bbox is None or marco is None:
        print("ERROR: bbox/marco ilegible para %s" % args.piece, file=sys.stderr)
        return 2

    failures, verdict, changed, outside = diff(args.before, args.after, bbox,
                                               marco[0], marco[1])
    print("PIEZA       %s" % args.piece)
    print("BBOX        %d,%d,%d,%d de %s" % (bbox[0], bbox[1], bbox[2], bbox[3], entry["marco"]))
    print("MARCOS      before %s | after %s"
          % (args.before.as_posix(), args.after.as_posix()))
    if verdict == "SIZE":
        failures.append("DIFF-SIZE: el marco no mide %s (no se puede mapear el bbox)"
                        % entry["marco"])
    elif verdict == "DIFF-EMPTY":
        failures.append("DIFF-EMPTY %s: 0 pixeles cambiados dentro de %s (un swap invisible no "
                        "es comparable)" % (args.piece, entry["bbox"]))
    elif verdict == "DIFF-OUTSIDE":
        failures.append("DIFF-OUTSIDE %s: %d de %d pixeles cambiados caen fuera del bbox %s"
                        % (args.piece, outside, changed, entry["bbox"]))
    elif verdict == "PASS":
        print("CAMBIADOS   %d px, todos dentro del bbox" % changed)
    for failure in failures:
        print(failure)
    print("RESULTADO   %s  (%d hallazgos)"
          % ("PASS" if not failures and verdict == "PASS" else "FAIL", len(failures)))
    return 0 if (not failures and verdict == "PASS") else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
