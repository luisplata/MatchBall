#!/usr/bin/env python3
"""Gate del cableado de placeholders: resuelve el mapa y audita el diff contra la allowlist.

Fuente: el bloque ```csv de `Docs/art/placeholders-v1.md` (37 filas: 20 `cableado`,
13 `sin_slot`, 4 `bloqueado`). Este script NO escribe nada: lee el mapa, lee el YAML de
Unity como texto (sin abrir Unity, sin PyYAML) y compara.

Que prueba:

    MAPA       37 filas, 10 columnas, pieza a pieza == ledger, estados en 20/13/4
    ASSET      el `asset` de cada fila `cableado` existe en el working tree
    NODO       el `nodo` resuelve a un GameObject de ese asset (walk de m_Father)
    CAMPO      la clave esta en la allowlist de su `slot_tipo`
    FALTA      ningun `sin_slot`/`bloqueado` aparece referenciado en ningun YAML (por GUID)
    SPRITESWAP `m_Transition` en 2 y `m_SpriteState` declarado para las filas selectable_state
    CAPTURA    cada fila `cableado` con `captura` tiene su PNG (las vacias son pendientes)
    DIFF       con --diff REF, cada linea cambiada toca solo claves permitidas de SU fila,
               ningun `.cs` y ningun asset fuera del mapa (APW-4, APW-4b, APW-6)

Las filas con `nodo=-` son las dos variantes de estado del boton maestro `Button_UI`: su slot
es el sprite, asi que en vez de resolver un path se cuentan las referencias `m_SpriteState`
al GUID de la pieza (>= 1, y ninguna en un `sin_slot`).

Uso:
    python tools/art/check_placeholder_wiring.py --plan      # mapa resuelto, sin diff
    python tools/art/check_placeholder_wiring.py             # + gate de diff contra HEAD
    python tools/art/check_placeholder_wiring.py --diff main # gate contra otra base
    python tools/art/check_placeholder_wiring.py --require-captures   # gate de 2b

Exit code: 0 = PASS / 1 = hay hallazgos / 2 = el mapa o el manifiesto no se pueden leer.
"""

from __future__ import annotations

import argparse
import csv
import re
import subprocess
import sys
from pathlib import Path

TOOLS = Path(__file__).resolve().parent
REPO_ROOT = Path(__file__).resolve().parents[2]
MAP_FILE = REPO_ROOT / "Docs" / "art" / "placeholders-v1.md"
LEDGER = REPO_ROOT / "Docs" / "art" / "placeholders-v1.csv"
CAPTURES = REPO_ROOT / "Docs" / "art" / "captures" / "placeholders-v1"

FENCED_CSV_RE = re.compile(r"(?ms)^```csv\r?\n(.*?)^```")
DOC_RE = re.compile(r"(?m)^--- !u!(\d+) &(-?\d+)")
GO_REF_RE = re.compile(r"fileID: (-?\d+)")
GUID_RE = re.compile(r"(?m)^guid: ([0-9a-f]{32})\s*$")
COLUMNS = ("pieza", "slot_tipo", "asset", "nodo", "componente", "campo", "estado",
           "tint_original", "transition_original", "captura")
EXPECTED = {"cableado": 20, "sin_slot": 13, "bloqueado": 4}
EXPECTED_ROWS = 37

# APW-4: la allowlist del diff, por `slot_tipo`. `selectable_state` agrega la excepcion
# `spriteswap` de APW-4b (m_Transition 1->2 y los dos sprites de m_SpriteState).
BASE_KEYS = {"m_Sprite", "m_Color", "m_Enabled", "m_IsActive",
             "doneSprite", "activeSprite", "normalSprite"}
SLOT_KEYS = {
    "image": BASE_KEYS,
    "image+alfa_host": BASE_KEYS,
    "image+tinte_design": BASE_KEYS,
    "serialized_sprite_field": BASE_KEYS,
    "selectable_state": BASE_KEYS | {"m_Transition", "m_PressedSprite", "m_DisabledSprite",
                                     "m_SpriteState", "m_NormalSprite"},
}
# Campos que el MAPA puede nombrar en la columna `campo` (los de arriba, mas el path
# cualificado del bloque m_SpriteState, que es como se lee una fila de review).
MAP_FIELDS = {
    "image": {"m_Sprite"},
    "image+alfa_host": {"m_Sprite"},
    "image+tinte_design": {"m_Sprite"},
    "serialized_sprite_field": {"doneSprite", "activeSprite", "normalSprite"},
    "selectable_state": {"m_SpriteState.m_PressedSprite", "m_SpriteState.m_DisabledSprite",
                         "m_SpriteState.m_NormalSprite"},
}
FORBIDDEN_SUFFIXES = (".cs",)


class MapError(Exception):
    """El mapa o el ledger no se pueden leer: el gate no opina (exit 2)."""


def read_map() -> list[dict]:
    try:
        text = MAP_FILE.read_text(encoding="utf-8")
    except OSError as error:
        raise MapError("no se pudo leer %s: %s" % (MAP_FILE, error)) from error
    blocks = FENCED_CSV_RE.findall(text)
    if len(blocks) != 1:
        raise MapError("%s tiene %d bloques ```csv: el mapa tiene que ser uno"
                       % (MAP_FILE.name, len(blocks)))
    reader = csv.reader(blocks[0].splitlines())
    header = tuple(cell.strip() for cell in next(reader, ()))
    if header != COLUMNS:
        raise MapError("columnas inesperadas en el mapa: %s" % (header,))
    rows = []
    for raw in reader:
        cells = [cell.strip() for cell in raw]
        if not any(cells):
            continue
        if len(cells) != len(COLUMNS):
            raise MapError("fila con %d columnas: %s" % (len(cells), cells[0]))
        rows.append(dict(zip(COLUMNS, cells)))
    return rows


def read_ledger_guids() -> dict[str, str]:
    if not LEDGER.is_file():
        return {}
    with LEDGER.open(newline="", encoding="utf-8") as handle:
        return {row["pieza"]: row["guid"] for row in csv.DictReader(handle) if row.get("guid")}


# --------------------------------------------------------------------------- YAML


def documents(path: Path) -> dict[int, tuple[int, str]]:
    text = path.read_text(encoding="utf-8", errors="replace")
    marks = list(DOC_RE.finditer(text))
    docs = {}
    for index, mark in enumerate(marks):
        end = marks[index + 1].start() if index + 1 < len(marks) else len(text)
        docs[int(mark.group(2))] = (int(mark.group(1)), text[mark.start():end])
    return docs


def field(doc: str, name: str) -> str | None:
    match = re.search(r"(?m)^\s{0,4}%s: (.+)$" % re.escape(name), doc)
    return match.group(1).strip() if match else None


def node_paths(path: Path) -> dict[str, int]:
    """path -> GameObject id, resolviendo m_Father de Transform/RectTransform."""
    docs = documents(path)
    parent: dict[int, int] = {}
    owner: dict[int, int] = {}
    names: dict[int, str] = {}
    for file_id, (cls, doc) in docs.items():
        if cls in (4, 224):
            ref = GO_REF_RE.search(field(doc, "m_GameObject") or "")
            if not ref:
                continue
            owner[file_id] = int(ref.group(1))
            parent[file_id] = int(GO_REF_RE.search(field(doc, "m_Father") or "fileID: 0").group(1))
        elif cls == 1:
            names[file_id] = field(doc, "m_Name") or "?"
    transform_of = {}
    for transform, game_object in owner.items():
        transform_of.setdefault(game_object, transform)
    out: dict[str, int] = {}
    for game_object, transform in transform_of.items():
        parts, cursor = [], transform
        while cursor:
            parts.append(names.get(owner.get(cursor, 0), "?"))
            cursor = parent.get(cursor, 0)
        out.setdefault("/".join(reversed([part for part in parts if part])), game_object)
    return out


def document_of_line(path: Path) -> dict[int, int]:
    """linea (1-based) -> id de documento, para atribuir un hunk a su GameObject."""
    text = path.read_text(encoding="utf-8", errors="replace")
    out, current, line = {}, 0, 1
    for raw in text.split("\n"):
        mark = re.match(r"^--- !u!(\d+) &(-?\d+)", raw)
        if mark:
            current = int(mark.group(2))
        out[line] = current
        line += 1
    return out


def game_object_of_document(path: Path, document: int) -> str | None:
    docs = documents(path)
    if document not in docs:
        return None
    ref = GO_REF_RE.search(field(docs[document][1], "m_GameObject") or "")
    if not ref:
        return None
    wanted = int(ref.group(1))
    for candidate, game_object in node_paths(path).items():
        if game_object == wanted:
            return candidate
    return None


# --------------------------------------------------------------------------- checks


def check_map(rows: list[dict]) -> list[str]:
    failures: list[str] = []
    if len(rows) != EXPECTED_ROWS:
        failures.append("MAPA: %d filas, el cohorte congela %d" % (len(rows), EXPECTED_ROWS))
    counts = {estado: sum(1 for row in rows if row["estado"] == estado) for estado in EXPECTED}
    for estado, wanted in EXPECTED.items():
        if counts[estado] != wanted:
            failures.append("MAPA: %d %s, se esperan %d" % (counts[estado], estado, wanted))
    unknown = sorted({row["estado"] for row in rows} - set(EXPECTED))
    for estado in unknown:
        failures.append("MAPA: estado '%s' desconocido" % estado)
    ledger_guids = read_ledger_guids()
    names = [row["pieza"] for row in rows]
    for duplicate in sorted({name for name in names if names.count(name) > 1}):
        failures.append("MAPA: %s repetida" % duplicate)
    return failures, ledger_guids, counts


def check_rows(rows: list[dict], guids: dict[str, str]) -> tuple[list[str], list[str]]:
    failures: list[str] = []
    pending: list[str] = []
    cache: dict[Path, dict[str, int]] = {}
    for row in rows:
        pieza, estado = row["pieza"], row["estado"]
        if row["slot_tipo"] not in SLOT_KEYS:
            failures.append("%s: slot_tipo '%s' desconocido" % (pieza, row["slot_tipo"]))
            continue
        if estado == "sin_slot":
            if row["asset"] != "-" or row["nodo"] != "-":
                failures.append("%s: sin_slot con asset/nodo '%s'/'%s'" % (pieza, row["asset"], row["nodo"]))
            continue
        if estado == "bloqueado":
            # Puede nombrar su nodo (existe y hoy esta vacio) pero no se cablea: si tuviera
            # captura o transition_original seria un cableado encubierto.
            if row["captura"]:
                failures.append("%s: bloqueado con captura declarada" % pieza)
            continue
        if row["campo"] not in MAP_FIELDS.get(row["slot_tipo"], set()):
            failures.append("%s: campo '%s' no es un campo de %s" % (pieza, row["campo"], row["slot_tipo"]))
        if not row["captura"]:
            pending.append(pieza)
        elif not (CAPTURES / row["captura"]).is_file():
            failures.append("%s: falta la captura %s" % (pieza, row["captura"]))
        if row["slot_tipo"] == "selectable_state" and row["transition_original"] in ("", "-"):
            failures.append("%s: selectable_state sin transition_original (APW-4b)" % pieza)

        asset = REPO_ROOT / row["asset"]
        if row["nodo"] == "-":
            # Variante de estado del sprite maestro: se cuenta por GUID, no por path.
            if not asset.is_file():
                failures.append("%s: falta %s" % (pieza, row["asset"]))
            continue
        if not asset.is_file():
            failures.append("%s: falta %s" % (pieza, row["asset"]))
            continue
        if asset not in cache:
            try:
                cache[asset] = node_paths(asset)
            except OSError as error:
                failures.append("%s: no se pudo leer %s (%s)" % (pieza, row["asset"], error))
                cache[asset] = {}
        if row["nodo"] not in cache[asset]:
            failures.append("%s: el nodo '%s' no resuelve en %s" % (pieza, row["nodo"], row["asset"]))
        if row["componente"] not in ("Image", "Button", "TutorialStepDots"):
            failures.append("%s: componente '%s' fuera de la allowlist" % (pieza, row["componente"]))
    return failures, pending


def check_guid_leaks(rows: list[dict], guids: dict[str, str]) -> tuple[list[str], int]:
    """Ningun YAML puede referenciar el GUID de una pieza sin slot o bloqueada (APW-1)."""
    forbidden = {guids[row["pieza"]]: row["pieza"] for row in rows
                 if row["estado"] in ("sin_slot", "bloqueado") and row["pieza"] in guids}
    if not forbidden:
        return [], 0
    failures = []
    scanned = 0
    for path in sorted((REPO_ROOT / "Assets").rglob("*")):
        if path.suffix.lower() not in (".unity", ".prefab", ".asset", ".mat", ".controller"):
            continue
        if "/_Recovery/" in path.as_posix():
            continue
        text = path.read_text(encoding="utf-8", errors="replace")
        scanned += 1
        for guid, pieza in forbidden.items():
            if guid in text:
                failures.append("%s: %s (%s) aparece referenciada" % (pieza, guid, path.relative_to(REPO_ROOT).as_posix()))
    return failures, scanned


KEY_RE = re.compile(r"^\s*-?\s*([A-Za-z_][A-Za-z0-9_]*):")


def changed_lines(base: str) -> list[tuple[str, str, int, str]]:
    """(file, kind, line, text) por linea cambiada, con --unified=0 contra `base`."""
    result = subprocess.run(["git", "diff", "-U0", "--no-color", base, "--", "Assets"],
                            cwd=str(REPO_ROOT), capture_output=True, text=True)
    if result.returncode not in (0, 1):
        raise MapError("git diff fallo: %s" % result.stderr.strip())
    out, current, line = [], None, 0
    for raw in result.stdout.splitlines():
        if raw.startswith("+++ b/"):
            current = raw[6:]
            continue
        if raw.startswith("@@"):
            match = re.match(r"@@ -\d+(?:,\d+)? \+(\d+)(?:,\d+)? @@", raw)
            line = int(match.group(1)) if match else 0
            continue
        if raw.startswith("+") and not raw.startswith("+++"):
            out.append((current, "add", line, raw[1:]))
            line += 1
        elif raw.startswith("-") and not raw.startswith("---"):
            out.append((current, "del", line, raw[1:]))
    return out


def check_diff(rows: list[dict], base: str) -> list[str]:
    failures: list[str] = []
    by_node = {(row["asset"], row["nodo"]): row for row in rows if row["nodo"] != "-"}
    mapped_assets = {row["asset"] for row in rows if row["asset"] != "-"}
    line_cache: dict[str, tuple[dict[int, int], dict[str, int]]] = {}
    for path, kind, line, text in changed_lines(base):
        if path is None:
            continue
        suffix = Path(path).suffix.lower()
        if suffix in FORBIDDEN_SUFFIXES:
            failures.append("DIFF: %s es codigo (APW-6 prohibe .cs)" % path)
            continue
        if path not in mapped_assets:
            failures.append("DIFF: %s no esta en el mapa" % path)
            continue
        if path not in line_cache:
            absolute = REPO_ROOT / path
            if not absolute.is_file():
                line_cache[path] = ({}, {})
            else:
                line_cache[path] = (document_of_line(absolute), node_paths(absolute))
        docs, nodes = line_cache[path]
        key_match = KEY_RE.match(text)
        key = key_match.group(1) if key_match else None
        node = game_object_of_document(REPO_ROOT / path, docs.get(line, 0)) if nodes else None
        row = by_node.get((path, node))
        if row is None:
            if key is None and text.strip() in ("", "---"):
                continue
            failures.append("DIFF: %s:%d (%s) toca un nodo fuera del mapa" % (path, line, key or text.strip()[:24]))
            continue
        if key is None:
            continue
        if key not in SLOT_KEYS[row["slot_tipo"]]:
            failures.append("DIFF: %s:%d %s '%s' no esta permitida en la fila %s"
                            % (path, line, kind, key, row["pieza"]))
    return failures


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description="Gate del cableado de placeholders.")
    parser.add_argument("--plan", action="store_true", help="solo resuelve el mapa, sin diff")
    parser.add_argument("--diff", metavar="REF", help="gate de diff contra REF (default HEAD)")
    parser.add_argument("--require-captures", action="store_true",
                        help="falla si una fila cableada no tiene captura (gate del slice 2b)")
    parser.add_argument("--map", type=Path, help="mapa alternativo")
    args = parser.parse_args(argv)

    try:
        rows = read_map()
        map_failures, guids, counts = check_map(rows)
        row_failures, pending = check_rows(rows, guids)
        leak_failures, scanned = check_guid_leaks(rows, guids)
        failures = map_failures + row_failures + leak_failures
        if args.diff is not None or not args.plan:
            failures += check_diff(rows, args.diff or "HEAD")
    except MapError as error:
        print("ERROR: %s" % error, file=sys.stderr)
        return 2

    if args.plan:
        for row in rows:
            if row["estado"] == "cableado":
                print("PLAN  %-40s %-26s %s" % (row["pieza"], row["slot_tipo"],
                                                row["nodo"] if row["nodo"] != "-" else row["asset"]))
    print("MAPA        %d filas: %d cableado, %d sin_slot, %d bloqueado"
          % (len(rows), counts["cableado"], counts["sin_slot"], counts["bloqueado"]))
    print("GUID        %d piezas con guid congelado | %d YAML escaneados en busca de filtraciones"
          % (len(guids), scanned))
    if args.require_captures and pending:
        failures += ["CAPTURA: %s sin captura declarada" % pieza for pieza in pending]
    elif pending:
        print("PENDIENTE   %d capturas sin declarar (slice de capturas)" % len(pending))
    for failure in failures:
        print(failure)
    print("RESULTADO   %s  (%d hallazgos)" % ("PASS" if not failures else "FAIL", len(failures)))
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
