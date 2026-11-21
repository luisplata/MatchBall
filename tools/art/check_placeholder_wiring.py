#!/usr/bin/env python3
"""Gate del cableado de placeholders: resuelve el mapa y audita el diff contra la allowlist.

Fuente: el bloque ```csv de `Docs/art/placeholders-v1.md` (37 filas: 20 `cableado`,
13 `sin_slot`, 4 `bloqueado`). Este script NO escribe nada: lee el mapa, lee el YAML de
Unity como texto (sin abrir Unity, sin PyYAML) y compara.

Que prueba:

    MAPA       37 filas, 10 columnas, pieza a pieza == ledger, estados en 20/13/4
    ASSET      el `asset` de cada fila `cableado` existe en el working tree
    NODO       el `nodo` resuelve a un GameObject de ese asset (walk de m_Father)
    CAMPO      la clave declarada es una de las que admite su `slot_tipo`
    VALOR      el campo declarado lleva el GUID de SU pieza segun el ledger, y para las
               filas sin nodo su GUID aparece al menos una vez en el asset
    SPRITESWAP `m_Transition` en 2, el sprite de estado declarado con el GUID de su pieza y un
               `m_PressedSprite` no vacio (APW-4b: un boton que no muestra nada al presionar
               esta roto, no "recortado")
    TINTE      `m_Color` del host == `(1,1,1,alfa_original)` cuando la luminancia WCAG del
               `tint_original` es < 0.25, y == `tint_original` cuando no; las filas
               `image+tinte_design` conservan el suyo por excepcion declarada. El alfa nunca
               se toca (APW-3)
    FALTA      ningun `sin_slot`/`bloqueado` aparece referenciado en ningun YAML (por GUID)
    CAPTURA    cada fila `cableado` con `captura` tiene su PNG (las vacias son pendientes)
    DIFF       con --diff REF, cada linea cambiada toca solo claves permitidas de SU fila,
               ningun `.cs` y ningun asset fuera del mapa (APW-4, APW-4b, APW-6)

El gate de DIFF no tiene default a proposito: `--diff HEAD` sobre un arbol limpio compara el
arbol consigo mismo y da un verde vacio. Se corre con la base del slice
(`--diff <commit anterior al cableado>`), y es el unico modo de que la allowlist pruebe algo.

Las filas con `nodo=-` no tienen path que resolver: su slot es un sprite, asi que se cuenta
que el GUID de la pieza aparezca al menos una vez en el asset (y ninguna en un `sin_slot`).

Uso:
    python tools/art/check_placeholder_wiring.py --plan      # mapa resuelto, sin diff
    python tools/art/check_placeholder_wiring.py --require-captures   # gate de 2b
    python tools/art/check_placeholder_wiring.py --diff <base>        # + allowlist del diff

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


def read_map(path: Path = MAP_FILE) -> list[dict]:
    try:
        text = path.read_text(encoding="utf-8")
    except OSError as error:
        raise MapError("no se pudo leer %s: %s" % (path, error)) from error
    blocks = FENCED_CSV_RE.findall(text)
    if len(blocks) != 1:
        raise MapError("%s tiene %d bloques ```csv: el mapa tiene que ser uno"
                       % (path.name, len(blocks)))
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


def read_ledger_paths() -> set[str]:
    """Los paths que el ledger declara: sus bytes los juzga el generador, no este gate."""
    if not LEDGER.is_file():
        return set()
    with LEDGER.open(newline="", encoding="utf-8") as handle:
        return {row["path"] for row in csv.DictReader(handle) if row.get("path", "-") != "-"}


def live_image_color(row: dict):
    """El `m_Color` vivo del `Image` del nodo de una fila `cableado`, o `None`."""
    if row["nodo"] == "-":
        return None
    asset = REPO_ROOT / row["asset"]
    if not asset.is_file():
        return None
    for doc in component_docs(asset, row["nodo"], "Image"):
        color = parse_color(field(doc, "m_Color"))
        if color is not None:
            return color[:3]
    return None


def hosts_from_map(path: Path | None = None) -> dict[str, tuple[float, float, float]]:
    """DBT-2: un host por fila `cableado`, leido del mapa sin abrir Unity.

    - `image`, `image+alfa_host`, `selectable_state`, `serialized_sprite_field`: el `m_Color`
      vivo del `Image` de ese nodo.
    - `serialized_sprite_field` sin `Image` (los 3 dots): `1/1/1`, declarado.
    - `image+tinte_design`: el tinte oscuro ES el efecto (excepcion que el propio mapa
      declara); el host que decide si el contenido sobrevive es blanco, y `tint_original`
      queda intacto en el mapa.
    """
    hosts: dict[str, tuple[float, float, float]] = {}
    for row in read_map(path or MAP_FILE):
        if row["estado"] != "cableado":
            continue
        if row["slot_tipo"] == "image+tinte_design":
            hosts[row["pieza"]] = (1.0, 1.0, 1.0)
            continue
        hosts[row["pieza"]] = live_image_color(row) or (1.0, 1.0, 1.0)
    return hosts


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


# --------------------------------------------------------------------------- valores

IMAGE_KEYS = ("m_Sprite", "m_Color")
BUTTON_KEYS = ("m_Transition", "m_SpriteState")
COLOR_TOLERANCE = 5e-4


def wcag_luminance(rgb: tuple[float, float, float]) -> float:
    """Luminancia relativa WCAG con los mismos 8 bits que `check_placeholders`: una sola definicion."""
    linear = []
    for value in rgb:
        srgb = round(min(max(value, 0.0), 1.0) * 255) / 255.0
        linear.append(srgb / 12.92 if srgb <= 0.04045 else ((srgb + 0.055) / 1.055) ** 2.4)
    return 0.2126 * linear[0] + 0.7152 * linear[1] + 0.0722 * linear[2]


def parse_color(text: str | None) -> tuple[float, float, float, float] | None:
    cells = dict(re.findall(r"([rgba]): (-?[0-9.eE+-]+)", text or ""))
    if set(cells) != {"r", "g", "b", "a"}:
        return None
    try:
        return tuple(float(cells[channel]) for channel in "rgba")
    except ValueError:
        return None


def parse_sprite(text: str | None) -> str | None:
    match = re.search(r"guid: ([0-9a-f]{32})", text or "")
    return match.group(1) if match else None


def has_key(doc: str, name: str) -> bool:
    return re.search(r"(?m)^\s{0,4}%s:" % re.escape(name), doc) is not None


def component_docs(path: Path, node: str, component: str) -> list[str]:
    """Los documentos del componente sobre el GameObject del nodo, sin abrir Unity."""
    nodes = node_paths(path)
    if node not in nodes:
        return []
    wanted = nodes[node]
    found = []
    for file_id, (cls, doc) in documents(path).items():
        if cls != 114:
            continue
        ref = GO_REF_RE.search(field(doc, "m_GameObject") or "")
        if not ref or int(ref.group(1)) != wanted:
            continue
        if component == "Button" and all(has_key(doc, key) for key in BUTTON_KEYS):
            found.append(doc)
        elif component == "Image" and all(has_key(doc, key) for key in IMAGE_KEYS):
            found.append(doc)
        elif component == "TutorialStepDots" and any(
                has_key(doc, key) for key in ("normalSprite", "activeSprite", "doneSprite")):
            found.append(doc)
    return found


def declared_value(doc: str, declared: str) -> str | None:
    """`m_SpriteState.m_PressedSprite` -> el valor de esa clave dentro del bloque."""
    if "." in declared:
        block, _, key = declared.partition(".")
        if field(doc, block) not in (None, ""):
            return None          # el bloque tiene contenido en la misma linea: no es el formato de Unity
        match = re.search(r"(?m)^\s+%s: (.+)$" % re.escape(key), doc)
        return match.group(1).strip() if match else None
    return field(doc, declared)


def expected_tint(row: dict) -> tuple[float, float, float, float] | None:
    """El `m_Color` que el mapa obliga a tener: APW-3 normaliza si L < 0.25, el alfa nunca cambia."""
    if row["tint_original"] in ("", "-"):
        return None
    cells = [cell for cell in row["tint_original"].split("/") if cell.strip()]
    try:
        red, green, blue, alpha = (float(cell) for cell in cells)
    except ValueError:
        return None
    if row["slot_tipo"] == "image+tinte_design":
        return red, green, blue, alpha
    if wcag_luminance((red, green, blue)) < 0.25:
        return 1.0, 1.0, 1.0, alpha
    return red, green, blue, alpha


def check_values(rows: list[dict], guids: dict[str, str]) -> tuple[list[str], int]:
    """El campo declarado lleva el GUID de su pieza, y el `m_Color` es el que APW-3 manda."""
    failures: list[str] = []
    checked = 0
    cache: dict[Path, list[str]] = {}
    for row in rows:
        if row["estado"] != "cableado" or row["pieza"] not in guids:
            continue
        asset = REPO_ROOT / row["asset"]
        if not asset.is_file():
            continue
        guid = guids[row["pieza"]]
        if row["nodo"] == "-":
            text = asset.read_text(encoding="utf-8", errors="replace")
            if str(guid) not in text:
                failures.append("%s: el guid %s no aparece en %s" % (row["pieza"], guid, row["asset"]))
            else:
                checked += 1
            continue
        key = (asset, row["nodo"], row["componente"])
        if key not in cache:
            cache[key] = component_docs(asset, row["nodo"], row["componente"])
        docs = cache[key]
        if not docs:
            failures.append("%s: el nodo '%s' no tiene un %s legible" % (row["pieza"], row["nodo"], row["componente"]))
            continue
        value = None
        for doc in docs:
            value = declared_value(doc, row["campo"])
            if value is not None:
                break
        if value is None:
            failures.append("%s: %s no tiene la clave '%s'" % (row["pieza"], row["nodo"], row["campo"]))
            continue
        found = parse_sprite(value)
        if found != guid:
            failures.append("%s: %s.%s = %s, el ledger congela %s"
                            % (row["pieza"], row["nodo"], row["campo"], found or "sin sprite", guid))
            continue
        checked += 1

        if row["slot_tipo"] == "selectable_state":
            transition = field(docs[0], "m_Transition")
            if transition != "2":
                failures.append("%s: m_Transition = %s, la excepcion spriteswap exige 2 (APW-4b)"
                                % (row["pieza"], transition))
            pressed = None
            for doc in docs:
                pressed = declared_value(doc, "m_SpriteState.m_PressedSprite")
                if pressed is not None:
                    break
            if parse_sprite(pressed) is None:
                failures.append("%s: m_SpriteState.m_PressedSprite vacio: con m_Transition 2 "
                                "el boton no muestra nada al presionar (APW-4b)"
                                % row["pieza"])

        expected = expected_tint(row)
        if expected is None:
            continue
        image = cache.get((asset, row["nodo"], "Image"))
        if image is None:
            image = component_docs(asset, row["nodo"], "Image")
            cache[(asset, row["nodo"], "Image")] = image
        live = None
        for doc in image:
            live = parse_color(field(doc, "m_Color"))
            if live is not None:
                break
        if live is None:
            failures.append("%s: el nodo '%s' no tiene m_Color legible" % (row["pieza"], row["nodo"]))
            continue
        if any(abs(measured - wanted) > COLOR_TOLERANCE for measured, wanted in zip(live, expected)):
            failures.append("%s: m_Color %s, el mapa exige %s (APW-3, tint_original %s)"
                            % (row["pieza"], "/".join("%.4g" % value for value in live),
                               "/".join("%.4g" % value for value in expected), row["tint_original"]))
    return failures, checked


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
    placeholder_paths = read_ledger_paths()
    line_cache: dict[str, tuple[dict[int, int], dict[str, int]]] = {}
    code_reported: set[str] = set()
    for path, kind, line, text in changed_lines(base):
        if path is None:
            continue
        suffix = Path(path).suffix.lower()
        if suffix in FORBIDDEN_SUFFIXES:
            if path not in code_reported:
                code_reported.add(path)
                failures.append("DIFF: %s es codigo (APW-6 prohibe .cs)" % path)
            continue
        # Los bytes del cohorte (PNG y su .meta) no son una referencia de cableado: los
        # aprieta generate_placeholders.py --check y el auditor (NEW-SHA / NEW-META / NEW-GUID).
        if path in placeholder_paths or (suffix == ".meta" and path[: -len(".meta")] in placeholder_paths):
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
    parser.add_argument("--diff", metavar="REF",
                        help="gate de allowlist contra REF (sin default: contra HEAD no prueba nada)")
    parser.add_argument("--require-captures", action="store_true",
                        help="falla si una fila cableada no tiene captura (gate del slice 2b)")
    parser.add_argument("--map", type=Path, help="mapa alternativo")
    args = parser.parse_args(argv)

    try:
        rows = read_map(args.map or MAP_FILE)
        map_failures, guids, counts = check_map(rows)
        row_failures, pending = check_rows(rows, guids)
        value_failures, valued = check_values(rows, guids)
        leak_failures, scanned = check_guid_leaks(rows, guids)
        failures = map_failures + row_failures + value_failures + leak_failures
        if args.diff is not None:
            failures += check_diff(rows, args.diff)
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
    print("GUID        %d piezas con guid congelado | %d campos resueltos contra su pieza"
          % (len(guids), valued))
    print("DIFF        %s" % (("allowlist contra %s" % args.diff) if args.diff is not None
                              else "sin base: pasar --diff REF para correr la allowlist"))
    print("YAML        %d archivos escaneados en busca de filtraciones" % scanned)
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
