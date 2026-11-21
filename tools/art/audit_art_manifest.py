#!/usr/bin/env python3
"""Auditor file-level del contrato de arte (sec.9 de manifest-refresh-visual-v1.md).

La sec.9 del manifiesto es la unica fuente: un bloque ```csv cercado con una fila por
pieza (122) y 12 columnas. Este script NO abre Unity, NO usa PyYAML y NO escribe
nada bajo Assets/: lee el CSV, lee los bytes reales (header PNG IHDR / JPEG SOF) y
el YAML de cada `.meta`, y emite pass/fail. Cumple ASC-8 de forma literal.

Que compara (regla -> columna de sec.9 -> archivo):

  NAME-REGEX        pieza          regex de nombres RT-7
  CSV-ROW           -              122 filas, 12 columnas, sin duplicados
  ESTADO            estado         REPLACE con origen, NEW sin archivo
  ORIGEN-EXISTS     origen         el path declarado existe en el working tree
  FORMAT-JPG        origen         ASC-5: nada de JPG en la entrega
  PX-ENTREGA        px_entrega     header del archivo == px_entrega (CTR-1)
  PX-ACTUAL-DRIFT   px_actual      informativo: el header ya no es px_actual
  MODE-TARGET       mode_target    spriteMode medido en el .meta vs objetivo (1 = Single)
  RECT-STALE        -              rect del sprite VIVO (spriteMode 2) vs lienzo (CTR-2)
  BORDER-META       border         spriteBorder del .meta (l/b/r/t)
  BORDER-SLICED     border         borde declarado si un Image lo dibuja Sliced/Tiled
  MAX-TEX           max_tex        maxTextureSize del .meta
  PIVOT             pivot          spritePivot del .meta
  PPU               ppu            spritePixelsToUnits del .meta
  PX-ENTREGA-TECHO  px_entrega     max(px_entrega) <= max_tex
  ADDRESS-RESOLVE   address        la entry existe en AddressableAssetsData
  ADDRESS-PATH      address        address -> m_GUID -> path == origen

Cohorte NEW (ledger derivado, `tools/art/art_ledger.py`):

  NEW-LEDGER        filas del ledger == sec.9 NEW, sin duplicados
  NEW-SHA           sha(disco) != ledger sin fila en deliveries-v1.csv
  DELIVERY-ROW      una DELIVERED exige exactamente una fila con autor
  DELIVERY-SHA      una DELIVERED exige sha(disco) == ledger.sha256
  EXCLUDED-MOTIVO   una EXCLUDED exige motivo no vacio
  EXCLUDED-PNG      una EXCLUDED no puede tener PNG en disco
  COHORT-SUM        122 = R + N y N = la suma de las cinco etiquetas (ART-3)

`mode_actual` NO es una regla: es el snapshot del escaneo con el que se congelo la sec.9
("como estaba el arte el 2026-11-11"). Perseguirlo como estado vivo hace que cada pieza
migrada produzca una violacion nueva (F-S2a-2), por eso solo se reporta como INFO
(`MODE-ACTUAL-DRIFT`) cuando el .meta ya no coincide. El unico gate de import es
`mode_target`: el objetivo no cambia cuando el asset se migra. `px_actual` corre la misma
suerte desde CTR-1: el gate de pixeles es `px_entrega`.

Exit code: 0 = PASS / 1 = hay violaciones / 2 = el manifiesto no se pudo leer.

Uso:
    python tools/art/audit_art_manifest.py
    python tools/art/audit_art_manifest.py --json Docs/art/audit/v1.json
    python tools/art/audit_art_manifest.py --verbose --no-refs
    python tools/art/audit_art_manifest.py --plan                # plan del migrador (dry-run)
    python tools/art/audit_art_manifest.py --plan --plan-piece game_board_

`--plan` NO escribe: proyecta, pieza por pieza, las operaciones que ejecutaria
`Assets/Editor/ArtContract/ArtMetaMigrator.cs` en modo dry-run. El migrador es el
unico escritor; este script sigue siendo de solo lectura.
"""

from __future__ import annotations

import argparse
import csv
import json
import re
import struct
import sys
from collections import Counter
from dataclasses import dataclass, field
from pathlib import Path

import art_ledger as ledger_mod

REPO_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_MANIFEST = REPO_ROOT / "Docs" / "art" / "manifest-refresh-visual-v1.md"
ASSETS_ROOT = REPO_ROOT / "Assets"
ADDRESSABLES_GROUPS = ASSETS_ROOT / "AddressableAssetsData" / "AssetGroups"
LEDGER = ledger_mod.LEDGER
DELIVERIES = ledger_mod.DELIVERIES
SPRITES_DIR = ASSETS_ROOT / "Sprites"

CONSUMER_SUFFIXES = (".unity", ".prefab", ".asset", ".mat", ".controller")
CONSUMER_BACKUP_MARKER = "_Recovery"

EXPECTED_ROWS = ledger_mod.EXPECTED_ROWS
COLUMNS = (
    "pieza",
    "estado",
    "origen",
    "px_actual",
    "px_entrega",
    "mode_target",
    "mode_actual",
    "border",
    "max_tex",
    "pivot",
    "ppu",
    "address",
)

# Cohort de placeholders (sec.9 estado=NEW). El ledger es derivado; estas son las unicas
# reglas que pueden leerlo, y su namespace es el de las piezas NEW: un archivo suelto
# fuera de ese namespace no es atribuible al cohorte (Assets/Sprites/ es plana y convive
# con assets legados que sec.9 no nombra).
LEDGER_COLUMNS = ledger_mod.LEDGER_COLUMNS
DELIVERY_COLUMNS = ledger_mod.DELIVERY_COLUMNS
NEW_PREFIXES = ("ui_tutorial_", "ui_hud_", "ui_common_", "ui_menu_", "ball_state_", "game_board_")
GENERATED = "GENERATED"
BLOCKED_ESTADOS = ledger_mod.BLOCKED_STATES
DELIVERED = "DELIVERED"
EXCLUDED = "EXCLUDED"

NAME_RE = re.compile(r"^[a-z0-9]+(_[a-z0-9]+)+_[0-9]{2}\.png$")
GUID_RE = re.compile(r"^guid: ([0-9a-f]{32})\s*$", re.MULTILINE)
REF_RE = re.compile(r"fileID:\s*(-?\d+),\s*guid:\s*([0-9a-f]{32})")
ANY_GUID_RE = re.compile(r"guid:\s*([0-9a-f]{32})")
SPRITE_REF_RE = re.compile(r"m_Sprite:\s*\{fileID:\s*(-?\d+),\s*guid:\s*([0-9a-f]{32})")
MONO_DOC_RE = re.compile(r"(?m)^--- !u!114 &(-?\d+).*$")
FENCED_CSV_RE = re.compile(r"(?ms)^```csv\r?\n(.*?)^```")


class ToolError(Exception):
    """El manifiesto o el repo no se pudieron leer: el auditor no puede opinar."""


# --------------------------------------------------------------------------- modelo


@dataclass
class Meta:
    guid: str
    sprite_mode: int | None
    border: tuple[int, int, int, int] | None
    max_texture_size: int | None
    pivot: tuple[float, float] | None
    ppu: float | None
    texture_type: int | None = None
    alpha_is_transparency: int | None = None
    sprites: list[dict] = field(default_factory=list)  # {name, rect, internal_id}
    sheet_ids: list[int] = field(default_factory=list)


@dataclass
class Piece:
    row: dict
    size: tuple[int, int] | None = None
    kind: str | None = None
    meta: Meta | None = None
    meta_path: Path | None = None
    refs_total: int = 0
    refs_by_fileid: Counter = field(default_factory=Counter)
    ref_files: int = 0


@dataclass
class Violation:
    rule: str
    pieza: str
    detail: str

    def as_dict(self) -> dict:
        return {"rule": self.rule, "pieza": self.pieza, "detail": self.detail}


# ----------------------------------------------------------------------- manifiesto


def read_csv_block(manifest: Path) -> list[dict]:
    """Devuelve las filas del unico bloque ```csv cercado de la sec.9."""
    try:
        text = manifest.read_text(encoding="utf-8")
    except OSError as error:
        raise ToolError("no se pudo leer %s: %s" % (manifest, error)) from error

    blocks = FENCED_CSV_RE.findall(text)
    if not blocks:
        raise ToolError("%s no tiene un bloque ```csv" % manifest)
    if len(blocks) > 1:
        raise ToolError("%s tiene %d bloques ```csv: la verdad tiene que ser una" % (manifest, len(blocks)))

    rows: list[dict] = []
    reader = csv.reader(blocks[0].splitlines())
    try:
        header = tuple(cell.strip() for cell in next(reader))
    except StopIteration as error:
        raise ToolError("el bloque ```csv esta vacio") from error
    if header != COLUMNS:
        raise ToolError("columnas inesperadas en sec.9: %s" % (header,))

    for index, raw in enumerate(reader, start=2):
        if not raw or not any(cell.strip() for cell in raw):
            continue
        if len(raw) != len(COLUMNS):
            rows.append({"_row": index, "_bad_columns": str(len(raw)), "pieza": raw[0] if raw else "?"})
            continue
        rows.append(dict(zip(COLUMNS, (cell.strip() for cell in raw))))
    return rows


def parse_px(value: str) -> tuple[int, int] | None:
    match = re.fullmatch(r"(\d+)\s*[xX]\s*(\d+)", value.strip())
    return (int(match.group(1)), int(match.group(2))) if match else None


def parse_border(value: str) -> tuple[int, int, int, int] | None:
    parts = value.strip().split("/")
    if len(parts) != 4 or not all(part.strip().isdigit() for part in parts):
        return None
    return tuple(int(part) for part in parts)  # type: ignore[return-value]


def parse_pair(value: str) -> tuple[float, float] | None:
    parts = value.strip().split("/")
    if len(parts) != 2:
        return None
    try:
        return (float(parts[0]), float(parts[1]))
    except ValueError:
        return None


# ------------------------------------------------------------------------ imagenes


def image_size(path: Path) -> tuple[tuple[int, int] | None, str | None]:
    """(ancho, alto) real del archivo: PNG IHDR o JPEG SOF. No decodifica pixeles."""
    try:
        with path.open("rb") as handle:
            head = handle.read(32)
            if head[:8] == b"\x89PNG\r\n\x1a\n":
                width, height = struct.unpack(">II", head[16:24])
                return (width, height), "png"
            if head[:2] != b"\xff\xd8":
                return None, None
            handle.seek(2)
            while True:
                byte = handle.read(1)
                if not byte:
                    return None, "jpeg"
                if byte != b"\xff":
                    continue
                marker = handle.read(1)
                while marker == b"\xff":
                    marker = handle.read(1)
                if not marker:
                    return None, "jpeg"
                code = marker[0]
                if code in (0xD8, 0x01) or 0xD0 <= code <= 0xD7:
                    continue
                length_bytes = handle.read(2)
                if len(length_bytes) < 2:
                    return None, "jpeg"
                length = struct.unpack(">H", length_bytes)[0]
                if 0xC0 <= code <= 0xCF and code not in (0xC4, 0xC8, 0xCC):
                    payload = handle.read(5)
                    if len(payload) < 5:
                        return None, "jpeg"
                    height, width = struct.unpack(">HH", payload[1:5])
                    return (width, height), "jpeg"
                handle.seek(length - 2, 1)
    except OSError:
        return None, None


# ---------------------------------------------------------------------------- meta


def read_meta(path: Path) -> Meta | None:
    try:
        text = path.read_text(encoding="utf-8", errors="replace")
    except OSError:
        return None

    guid_match = GUID_RE.search(text)
    if not guid_match:
        return None

    sprite_mode = _first_int(text, r"^  spriteMode: (-?\d+)$")
    max_tex = _first_int(text, r"^  maxTextureSize: (\d+)$")
    border = None
    border_match = re.search(
        r"^  spriteBorder: \{x: (-?\d+), y: (-?\d+), z: (-?\d+), w: (-?\d+)\}", text, re.MULTILINE
    )
    if border_match:
        border = tuple(int(group) for group in border_match.groups())  # type: ignore[assignment]

    pivot = None
    pivot_match = re.search(r"^  spritePivot: \{x: (-?[\d.]+), y: (-?[\d.]+)\}", text, re.MULTILINE)
    if pivot_match:
        pivot = (float(pivot_match.group(1)), float(pivot_match.group(2)))

    ppu = None
    ppu_match = re.search(r"^  spritePixelsToUnits: (-?[\d.]+)", text, re.MULTILINE)
    if ppu_match:
        ppu = float(ppu_match.group(1))

    sheet_ids = [int(value) for value in re.findall(r"^\s+213: (-?\d+)\s*$", text, re.MULTILINE)]
    sprites = _read_spritesheet(text)
    for sprite in sprites:
        if sprite["internal_id"] not in sheet_ids:
            sheet_ids.append(sprite["internal_id"])

    return Meta(
        guid=guid_match.group(1),
        sprite_mode=sprite_mode,
        border=border,
        max_texture_size=max_tex,
        pivot=pivot,
        ppu=ppu,
        texture_type=_first_int(text, r"^  textureType: (-?\d+)$"),
        alpha_is_transparency=_first_int(text, r"^  alphaIsTransparency: (-?\d+)$"),
        sprites=sprites,
        sheet_ids=sheet_ids,
    )


def _first_int(text: str, pattern: str) -> int | None:
    match = re.search(pattern, text, re.MULTILINE)
    return int(match.group(1)) if match else None


def _read_spritesheet(text: str) -> list[dict]:
    """Lee los sprites de `spriteSheet.sprites` (name, rect, internalID)."""
    lines = text.splitlines()
    start = None
    for index, line in enumerate(lines):
        if line == "  spriteSheet:":
            start = index + 1
            break
    if start is None:
        return []

    sprites: list[dict] = []
    index = start
    current: dict | None = None
    while index < len(lines):
        line = lines[index]
        if line and not line.startswith(" "):
            break  # fin del bloque TextureImporter
        if line.startswith("    - serializedVersion:"):
            if current is not None:
                sprites.append(current)
            current = {"name": "", "rect": None, "internal_id": None}
        elif current is not None and line.startswith("      name: "):
            current["name"] = line.split(":", 1)[1].strip() if line.strip() != "name:" else ""
        elif current is not None and line.startswith("      internalID: "):
            current["internal_id"] = int(line.split(":", 1)[1].strip())
        elif current is not None and line.strip() == "rect:":
            index = _read_rect(lines, index + 1, current)
            continue
        index += 1
    if current is not None:
        sprites.append(current)
    return [sprite for sprite in sprites if sprite["rect"] is not None]


def _read_rect(lines: list[str], index: int, sprite: dict) -> int:
    values: dict[str, int] = {}
    base_indent = len(lines[index]) - len(lines[index].lstrip())
    while index < len(lines):
        line = lines[index]
        if not line.strip():
            index += 1
            continue
        indent = len(line) - len(line.lstrip())
        if indent < base_indent:
            break
        key, _, value = line.strip().partition(":")
        if indent == base_indent and key in ("x", "y", "width", "height"):
            try:
                values[key] = int(value.strip())
            except ValueError:
                pass
        index += 1
    if len(values) == 4:
        sprite["rect"] = (values["x"], values["y"], values["width"], values["height"])
    return index


# ------------------------------------------------------------------ assets y refs


def collect_guid_index(assets_root: Path) -> dict[str, str]:
    """guid -> path relativo al repo (lee solo metas, no abre assets)."""
    index: dict[str, str] = {}
    for meta in assets_root.rglob("*.meta"):
        try:
            with meta.open("r", encoding="utf-8", errors="replace") as handle:
                head = handle.read(200)
        except OSError:
            continue
        match = GUID_RE.search(head)
        if match:
            index[match.group(1)] = str(meta.with_suffix("").relative_to(REPO_ROOT)).replace("\\", "/")
    return index


def scan_consumers(assets_root: Path, targets: dict[str, str]) -> dict:
    """Una pasada por los YAML de Assets: refs por GUID y draw mode de los Image."""
    refs_by_guid: dict[str, Counter] = {guid: Counter() for guid in targets}
    files_by_guid: dict[str, set[str]] = {guid: set() for guid in targets}
    draw_modes: dict[str, Counter] = {guid: Counter() for guid in targets}
    excluded_refs = 0
    excluded_files = 0
    files_scanned = 0

    for path in sorted(assets_root.rglob("*")):
        if path.suffix.lower() not in CONSUMER_SUFFIXES or not path.is_file():
            continue
        rel = str(path.relative_to(REPO_ROOT)).replace("\\", "/")
        try:
            text = path.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue
        files_scanned += 1

        present = {guid for guid in ANY_GUID_RE.findall(text) if guid in targets}
        if not present:
            continue

        if CONSUMER_BACKUP_MARKER in path.parts:
            excluded_refs += sum(1 for _, guid in REF_RE.findall(text) if guid in targets)
            excluded_files += 1
            continue

        for file_id, guid in REF_RE.findall(text):
            if guid in targets:
                refs_by_guid[guid][int(file_id)] += 1
                files_by_guid[guid].add(rel)

        for match in MONO_DOC_RE.finditer(text):
            end = text.find("\n--- !u!", match.end())
            doc = text[match.start() : end if end != -1 else len(text)]
            sprite = SPRITE_REF_RE.search(doc)
            if not sprite or sprite.group(2) not in targets:
                continue
            if "m_FillMethod:" not in doc or "m_Type:" not in doc:
                continue  # no es un UI.Image
            type_match = re.search(r"^  m_Type: (\d+)$", doc, re.MULTILINE)
            if type_match:
                draw_modes[sprite.group(2)][int(type_match.group(1))] += 1

    return {
        "refs_by_guid": refs_by_guid,
        "files_by_guid": files_by_guid,
        "draw_modes": draw_modes,
        "files_scanned": files_scanned,
        "excluded_refs": excluded_refs,
        "excluded_files": excluded_files,
    }


def collect_addresses() -> dict[str, str]:
    """address -> guid, de todos los grupos de Addressables."""
    addresses: dict[str, str] = {}
    if not ADDRESSABLES_GROUPS.is_dir():
        return addresses
    for group in sorted(ADDRESSABLES_GROUPS.glob("*.asset")):
        try:
            text = group.read_text(encoding="utf-8", errors="replace")
        except OSError:
            continue
        for match in re.finditer(
            r"- m_GUID: ([0-9a-f]{32})\s*\n\s+m_Address: (.+?)\s*\n", text
        ):
            addresses[match.group(2).strip()] = match.group(1)
    return addresses


# ------------------------------------------------ cohorte NEW (ledger derivado, APH-10)


def read_ledger() -> list[dict]:
    """Filas de `Docs/art/placeholders-v1.csv` (derivado del generador, nunca a mano)."""
    try:
        return ledger_mod.read_ledger(LEDGER)
    except ledger_mod.LedgerError as error:
        raise ToolError(str(error)) from error


def read_deliveries() -> list[dict]:
    """`Docs/art/deliveries-v1.csv`: la unica declaracion manual de arte entregado."""
    try:
        return ledger_mod.read_deliveries(DELIVERIES)
    except ledger_mod.LedgerError as error:
        raise ToolError(str(error)) from error


def sha256_of(path: Path) -> str:
    return ledger_mod.sha256_of(path)


# ------------------------------------------------------------------------ auditor


DRAW_MODE_SLICED = 1
DRAW_MODE_TILED = 2


def live_rects(meta: Meta | None, size: tuple[int, int] | None) -> list[dict]:
    """CTR-2: el rect del sprite VIVO, no el bloque serializado muerto.

    En `spriteMode == 1` el `Sprite.rect` vivo es el lienzo completo y `spriteSheet.sprite`
    no dibuja: cualquier bloque residual es un falso positivo (10 hoy). En `spriteMode == 2`
    los rects de la hoja SI dibujan y se miden.
    """
    if meta is None or size is None or meta.sprite_mode != 2:
        return []
    return list(meta.sprites)


def px_entrega_verdict(row: dict, size: tuple[int, int] | None) -> tuple[str, str]:
    """CTR-1: `(veredicto, detalle)` de la comparacion header vs `px_entrega`.

    `ok` si coinciden; `pending` si el header es el `px_actual` de una de las 6 de §4 (la
    allowlist cerrada de `art_ledger.PX_ENTREGA_PENDING`); `violation` para todo el resto.
    """
    if size is None:
        return "unknown", ""
    entrega = parse_px(row["px_entrega"])
    if entrega is None:
        return "tbd", ""
    if size == entrega:
        return "ok", ""
    expected = ledger_mod.PX_ENTREGA_PENDING.get(row["pieza"])
    actual = parse_px(row["px_actual"])
    if expected is not None and row["px_entrega"] == expected and actual == size:
        return "pending", "header %dx%d == px_actual, px_entrega %s de sec.4 pendiente" % (
            size[0], size[1], expected)
    return "violation", "header %dx%d != px_entrega %s" % (size[0], size[1], row["px_entrega"])


def audit(manifest: Path, assets_root: Path, with_refs: bool) -> dict:
    rows = read_csv_block(manifest)
    violations: list[Violation] = []
    info: list[str] = []

    def fail(rule: str, pieza: str, detail: str) -> None:
        violations.append(Violation(rule, pieza, detail))

    # CSV base
    if len(rows) != EXPECTED_ROWS:
        fail("CSV-ROW", "-", "%d filas, el contrato congela %d" % (len(rows), EXPECTED_ROWS))
    for row in rows:
        if "_bad_columns" in row:
            fail("CSV-ROW", row["pieza"], "%s columnas (se esperan 12)" % row["_bad_columns"])
            continue
        if not NAME_RE.match(row["pieza"]):
            fail("NAME-REGEX", row["pieza"], "no cumple ^[a-z0-9]+(_[a-z0-9]+)+_[0-9]{2}\\.png$")
        if row["estado"] not in ("REPLACE", "NEW"):
            fail("ESTADO", row["pieza"], "estado '%s' no es REPLACE ni NEW" % row["estado"])
        if row["estado"] == "REPLACE" and row["origen"] in ("-", ""):
            fail("ESTADO", row["pieza"], "REPLACE sin path de origen")
        if row["estado"] == "NEW" and row["origen"] != "-":
            fail("ESTADO", row["pieza"], "NEW con origen '%s'" % row["origen"])

    duplicates = [name for name, count in Counter(row["pieza"] for row in rows).items() if count > 1]
    for name in duplicates:
        fail("CSV-ROW", name, "pieza duplicada en sec.9")

    valid_rows = [row for row in rows if "_bad_columns" not in row]
    replace_rows = [row for row in valid_rows if row["estado"] == "REPLACE"]
    new_rows = [row for row in valid_rows if row["estado"] == "NEW"]

    # facts por pieza REPLACE
    pieces: dict[str, Piece] = {}
    for row in replace_rows:
        piece = Piece(row=row)
        origin = REPO_ROOT / row["origen"]
        if not origin.is_file():
            fail("ORIGEN-EXISTS", row["pieza"], "no existe %s" % row["origen"])
        else:
            piece.size, piece.kind = image_size(origin)
            if piece.kind == "jpeg":
                fail("FORMAT-JPG", row["pieza"], "origen JPEG: ASC-5 pide PNG-24 con alfa")
            declared = parse_px(row["px_actual"])
            if piece.size is None:
                fail("PX-ACTUAL", row["pieza"], "no se pudo leer el header de %s" % row["origen"])
            elif declared is not None and declared != piece.size:
                # Historico, nunca gate (CTR-1): el gate de pixeles es `px_entrega`.
                info.append(
                    "PX-ACTUAL-DRIFT  %s: header %dx%d != px_actual %s; informativo, el gate es"
                    " px_entrega %s"
                    % (row["pieza"], piece.size[0], piece.size[1], row["px_actual"],
                       row["px_entrega"]))
            elif declared is None:
                info.append("PX-ACTUAL-TBD  %s: px_actual '%s' no es un par WxH (informativo)"
                            % (row["pieza"], row["px_actual"]))
            piece.meta_path = Path(str(origin) + ".meta")
            if not piece.meta_path.is_file():
                fail("META-EXISTS", row["pieza"], "falta %s.meta" % row["origen"])
            else:
                piece.meta = read_meta(piece.meta_path)
                if piece.meta is None:
                    fail("META-EXISTS", row["pieza"], "el .meta no tiene guid")
        pieces[row["pieza"]] = piece

    # refs de consumidores (una sola pasada)
    guids = {piece.meta.guid: name for name, piece in pieces.items() if piece.meta}
    consumers = scan_consumers(assets_root, guids) if guids else {}
    for guid, name in guids.items():
        piece = pieces[name]
        piece.refs_by_fileid = consumers["refs_by_guid"][guid]
        piece.ref_files = len(consumers["files_by_guid"][guid])
        piece.refs_total = sum(piece.refs_by_fileid.values())

    sliced_pieces = set()
    sliced_instances = 0
    for guid, modes in consumers.get("draw_modes", {}).items():
        if DRAW_MODE_SLICED in modes or DRAW_MODE_TILED in modes:
            sliced_pieces.add(guids[guid])
            sliced_instances += modes[DRAW_MODE_SLICED] + modes[DRAW_MODE_TILED]

    # cohorte NEW: el ledger es la unica declaracion (APH-9, APH-10, APH-11)
    new_by_pieza = {row["pieza"]: row for row in new_rows}
    new_pieces: dict[str, Piece] = {}
    ledger_rows = read_ledger()
    if not ledger_rows:
        info.append("NEW-LEDGER  sin %s: el cohorte NEW no esta declarado todavia"
                    % LEDGER.relative_to(REPO_ROOT).as_posix())
    else:
        deliveries = read_deliveries()
        declared = Counter(row["pieza"] for row in ledger_rows)
        for pieza, count in sorted(declared.items()):
            if count > 1:
                fail("NEW-LEDGER", pieza, "%d filas en el ledger" % count)
        if len(ledger_rows) != len(new_by_pieza):
            fail("NEW-LEDGER", "-", "%d filas en el ledger, la sec.9 declara %d NEW"
                 % (len(ledger_rows), len(new_by_pieza)))
        for pieza in sorted(set(new_by_pieza) - set(declared)):
            fail("NEW-LEDGER", pieza, "NEW de sec.9 sin fila en el ledger")
        for pieza in sorted(set(declared) - set(new_by_pieza)):
            fail("NEW-LEDGER", pieza, "fila del ledger que no es NEW de sec.9")
        frozen: list[str] = []
        pending = 0
        for row in ledger_rows:
            pieza = row["pieza"]
            estado = (row["estado"] or "").strip()
            source = REPO_ROOT / row["path"] if row["path"] not in ("-", "") else None
            if estado in BLOCKED_ESTADOS:
                if row["path"] not in ("-", ""):
                    fail("NEW-LEDGER", pieza, "BLOCKED_* con path '%s'" % row["path"])
                if not row["motivo"]:
                    fail("NEW-LEDGER", pieza, "BLOCKED_* sin motivo (APH-11)")
                if (SPRITES_DIR / pieza).exists():
                    fail("NEW-BLOCKED", pieza, "la pieza esta bloqueada y hay PNG en disco")
                continue
            if estado == EXCLUDED:
                if not (row["motivo"] or "").strip():
                    fail("EXCLUDED-MOTIVO", pieza, "campo 'motivo' vacio (ART-4)")
                if source is not None and source.is_file():
                    fail("EXCLUDED-PNG", pieza, "una EXCLUDED no puede tener PNG (%s)" % row["path"])
                elif (SPRITES_DIR / pieza).exists():
                    fail("EXCLUDED-PNG", pieza,
                         "una EXCLUDED no puede tener PNG (Assets/Sprites/%s)" % pieza)
                continue
            if estado not in (GENERATED, DELIVERED):
                fail("COHORT-SUM", pieza, "estado '%s' fuera de %s"
                     % (estado, "/".join(ledger_mod.STATES)))
                continue
            if source is None or not source.is_file():
                fail("NEW-LEDGER", pieza, "FALTA el PNG declarado (%s)" % (row["path"] or "-"))
                continue
            disk_sha = sha256_of(source)
            if estado == GENERATED:
                if row["sha256"] != disk_sha:
                    if ledger_mod.delivery_rows(deliveries, pieza, disk_sha):
                        info.append("NEW-DELIVERY  %s: sha distinto del ledger, declarado en"
                                    " deliveries-v1.csv" % pieza)
                    else:
                        fail("NEW-SHA", pieza, "ledger %s != disco %s (sin fila en"
                             " deliveries-v1.csv)" % (row["sha256"] or "(vacio)", disk_sha))
            else:  # DELIVERED: ART-1(a) y ART-1(b); los pixeles no se comparan (APH-8 no aplica)
                ledger_sha = (row["sha256"] or "").strip()
                declaring = ledger_mod.delivery_rows(deliveries, pieza, ledger_sha)
                if len(declaring) != 1:
                    fail("DELIVERY-ROW", pieza, "pieza %s sha %s, %d filas con autor en"
                         " deliveries-v1.csv (se exige 1)"
                         % (pieza, ledger_sha or "(vacio)", len(declaring)))
                if disk_sha != ledger_sha:
                    fail("DELIVERY-SHA", pieza, "ledger %s != disco %s"
                         % (ledger_sha or "(vacio)", disk_sha))
            piece = Piece(row=new_by_pieza.get(pieza) or {"pieza": pieza})
            piece.size, piece.kind = image_size(source)
            piece.meta_path = Path(str(source) + ".meta")
            if piece.meta_path.is_file():
                piece.meta = read_meta(piece.meta_path)
            guid = (row["guid"] or "").strip()
            if not guid:
                pending += 1
            else:
                frozen.append(guid)
                if not re.fullmatch(r"[0-9a-f]{32}", guid):
                    fail("NEW-GUID", pieza, "'%s' no es 32 hex" % guid)
                elif piece.meta is None:
                    fail("NEW-GUID", pieza, "el ledger congela guid y falta/ilegible %s.meta" % pieza)
                elif piece.meta.guid != guid:
                    fail("NEW-GUID", pieza, "ledger %s != .meta %s" % (guid, piece.meta.guid))
                if piece.meta is not None:
                    if piece.meta.texture_type != 8:
                        fail("NEW-META", pieza, "textureType %s != 8 (Sprite)"
                             % piece.meta.texture_type)
                    if piece.meta.alpha_is_transparency != 1:
                        fail("NEW-META", pieza, "alphaIsTransparency %s != 1"
                             % piece.meta.alpha_is_transparency)
            new_pieces[pieza] = piece
        for guid, count in sorted(Counter(frozen).items()):
            if count > 1:
                fail("NEW-GUID", "-", "guid %s congelado en %d filas" % (guid, count))
        if frozen:
            index = collect_guid_index(ASSETS_ROOT)
            for row in ledger_rows:
                owner = index.get((row["guid"] or "").strip())
                if owner and owner != row["path"]:
                    fail("NEW-GUID", row["pieza"], "guid tambien usado por %s" % owner)
        legal_names = set(new_by_pieza) | set(declared)
        for path in sorted(SPRITES_DIR.glob("*.png")):
            if path.name.startswith(NEW_PREFIXES) and path.name not in legal_names:
                fail("NEW-ORPHAN", path.name, "PNG del namespace del cohorte sin fila en el ledger")
        counts = ledger_mod.cohort_counts(ledger_rows)
        info.append("COHORT      %s"
                    % ledger_mod.cohort_identity_text(EXPECTED_ROWS, len(replace_rows),
                                                      len(ledger_rows)))
        info.append("COHORT-SUM  %s"
                    % ledger_mod.cohort_sum_text(len(ledger_rows), counts))
        if len(replace_rows) + len(ledger_rows) != EXPECTED_ROWS:
            fail("COHORT-SUM", "-", "%s: la diferencia es %+d"
                 % (ledger_mod.cohort_identity_text(EXPECTED_ROWS, len(replace_rows),
                                                    len(ledger_rows)),
                    EXPECTED_ROWS - len(replace_rows) - len(ledger_rows)))
        if sum(counts.values()) != len(ledger_rows):
            fail("COHORT-SUM", "-", "%s: %d filas sin etiqueta valida"
                 % (ledger_mod.cohort_sum_text(len(ledger_rows), counts),
                    len(ledger_rows) - sum(counts.values())))
        info.append("NEW-LEDGER  %d filas: %d GENERATED, %d DELIVERED, %d EXCLUDED, %d bloqueadas,"
                    " %d guid congelados"
                    % (len(ledger_rows), counts[GENERATED], counts[DELIVERED], counts[EXCLUDED],
                       counts["BLOCKED_PX"] + counts["BLOCKED_BY_DESIGN"], len(frozen)))
        if pending:
            info.append("NEW-PENDING  %d piezas sin guid congelado: los .meta los escribe Unity "
                        "(cohorte new) y --freeze-guid los congela" % pending)
    checked = dict(pieces)
    checked.update(new_pieces)

    # reglas meta vs fila
    px_pending: list[str] = []
    for name, piece in checked.items():
        row, meta = piece.row, piece.meta
        if "_bad_columns" in row:
            continue
        border = parse_border(row["border"])
        if border is None:
            fail("BORDER-META", name, "border '%s' no es l/b/r/t" % row["border"])
        elif name in sliced_pieces and border == (0, 0, 0, 0):
            fail("BORDER-SLICED", name, "un Image lo dibuja Sliced/Tiled y border es 0/0/0/0")
        if meta is None:
            continue

        if row["mode_actual"].isdigit() and meta.sprite_mode != int(row["mode_actual"]):
            # Historico, no gate (F-S2a-2): `mode_actual` es el snapshot del escaneo. Se reporta
            # como INFO para que la migracion quede trazable, pero jamas puede fallar el audit.
            info.append(
                "MODE-ACTUAL-DRIFT  %s: mode_actual %s (snapshot del escaneo), spriteMode medido %s"
                "; informativo, el gate es mode_target %s"
                % (name, row["mode_actual"], meta.sprite_mode, row["mode_target"])
            )
        if meta.sprite_mode != int(row["mode_target"]):
            fail(
                "MODE-TARGET",
                name,
                "spriteMode %s != target %s (ASC-2: el swap de bytes no es seguro)"
                % (meta.sprite_mode, row["mode_target"]),
            )
        if piece.size:
            for sprite in live_rects(meta, piece.size):
                if sprite["rect"] != (0, 0, piece.size[0], piece.size[1]):
                    fail(
                        "RECT-STALE",
                        name,
                        'sprite "%s" rect %s != lienzo %dx%d'
                        % (sprite["name"] or "?", _fmt_rect(sprite["rect"]), piece.size[0], piece.size[1]),
                    )
        verdict, detail = px_entrega_verdict(row, piece.size)
        if verdict == "violation":
            fail("PX-ENTREGA", name, detail)
        elif verdict == "pending":
            px_pending.append(name)
            info.append("PX-ENTREGA-PENDING  %s: %s" % (name, detail))
        if border is not None and meta.border != border:
            fail("BORDER-META", name, "spriteBorder %s != %s" % (_fmt_border(meta.border), row["border"]))
        if row["max_tex"].isdigit() and meta.max_texture_size != int(row["max_tex"]):
            fail(
                "MAX-TEX",
                name,
                "maxTextureSize %s != max_tex %s" % (meta.max_texture_size, row["max_tex"]),
            )
        pivot = parse_pair(row["pivot"])
        if pivot and meta.pivot:
            if abs(meta.pivot[0] - pivot[0]) > 1e-4 or abs(meta.pivot[1] - pivot[1]) > 1e-4:
                fail("PIVOT", name, "spritePivot %.3f/%.3f != %s" % (meta.pivot[0], meta.pivot[1], row["pivot"]))
        if row["ppu"].replace(".", "", 1).isdigit() and meta.ppu is not None:
            if abs(meta.ppu - float(row["ppu"])) > 1e-4:
                fail("PPU", name, "spritePixelsToUnits %s != ppu %s" % (meta.ppu, row["ppu"]))

    if px_pending:
        info.append("PX-ENTREGA-PENDING  %d/%d: %s"
                    % (len(px_pending), len(ledger_mod.PX_ENTREGA_PENDING),
                       ", ".join(sorted(px_pending))))
    else:
        info.append("PX-ENTREGA-PENDING  0/%d (ninguna de §4 espera entrega)"
                    % len(ledger_mod.PX_ENTREGA_PENDING))

    # px_entrega vs techo de textura (REPLACE y NEW)
    for row in valid_rows:
        entrega = parse_px(row["px_entrega"])
        if entrega is None:
            info.append("PX-ENTREGA-TBD  %s (pendiente declarado en sec.9)" % row["pieza"])
            continue
        max_tex = int(row["max_tex"]) if row["max_tex"].isdigit() else None
        if max_tex is None:
            fail("MAX-TEX", row["pieza"], "max_tex '%s' no es numerico" % row["max_tex"])
        elif max(entrega) > max_tex:
            fail(
                "PX-ENTREGA-TECHO",
                row["pieza"],
                "px_entrega %dx%d excede max_tex %d" % (entrega[0], entrega[1], max_tex),
            )
        piece = pieces.get(row["pieza"])
        if piece and piece.size and max_tex and max(piece.size) > max_tex:
            info.append(
                "PX-ACTUAL-DOWNSAMPLE  %s header %dx%d > max_tex %d: hoy el import lo baja a ~%dx%d"
                % (row["pieza"], piece.size[0], piece.size[1], max_tex, *_downscaled(piece.size, max_tex))
            )

    # Addressables
    addresses = collect_addresses()
    address_rows = [row for row in valid_rows if row["address"] not in ("-", "")]
    for row in address_rows:
        guid = addresses.get(row["address"])
        if guid is None:
            fail("ADDRESS-RESOLVE", row["pieza"], "address '%s' no existe en AssetGroups" % row["address"])
            continue
        piece = pieces.get(row["pieza"])
        if piece and piece.meta and piece.meta.guid != guid:
            fail(
                "ADDRESS-PATH",
                row["pieza"],
                "address '%s' -> guid %s, la fila apunta a %s"
                % (row["address"], guid, piece.meta.guid),
            )

    return {
        "rows": rows,
        "pieces": pieces,
        "violations": violations,
        "info": info,
        "consumers": consumers,
        "guids": guids,
        "sliced": sorted(sliced_pieces),
        "sliced_instances": sliced_instances,
        "addresses": address_rows,
        "counts": {
            "rows": len(valid_rows),
            "replace": len(replace_rows),
            "new": len(new_rows),
            "addresses": len(address_rows),
            "ledger": len(ledger_rows),
            "generated": sum(1 for row in ledger_rows if row["estado"] == GENERATED),
            "delivered": sum(1 for row in ledger_rows if row["estado"] == DELIVERED),
            "excluded": sum(1 for row in ledger_rows if row["estado"] == EXCLUDED),
        },
    }


def _fmt_rect(rect: tuple[int, int, int, int]) -> str:
    return "%d,%d %dx%d" % rect


def _fmt_border(border: tuple[int, int, int, int] | None) -> str:
    return "/".join(str(part) for part in border) if border else "?"


def _downscaled(size: tuple[int, int], max_tex: int) -> tuple[int, int]:
    width, height = size
    scale = max_tex / max(width, height)
    return (max(1, round(width * scale)), max(1, round(height * scale)))


# --------------------------------------------------------------------------- plan


def project_plan(result: dict, filters: list[str]) -> list[str]:
    """Espejo de solo lectura del plan que imprime ArtMetaMigrator (dry-run)."""
    lines: list[str] = []
    planned = 0
    refs_total = 0

    for name in sorted(result["pieces"]):
        if filters and not any(name.startswith(prefix) for prefix in filters):
            continue
        piece = result["pieces"][name]
        row, meta = piece.row, piece.meta
        if meta is None or not piece.size:
            continue

        ops: list[tuple[str, str]] = []
        notes: list[str] = []
        flip = meta.sprite_mode != int(row["mode_target"])
        stale = [sprite for sprite in meta.sprites if sprite["rect"] != (0, 0, piece.size[0], piece.size[1])]

        if stale and not flip and int(row["mode_target"]) == 2:
            for sprite in stale:
                ops.append(
                    (
                        "rect-resync",
                        'sprite "%s" rect %s -> %s'
                        % (
                            sprite["name"] or "?",
                            _fmt_rect(sprite["rect"]),
                            _fmt_rect((0, 0, piece.size[0], piece.size[1])),
                        ),
                    )
                )
        if flip:
            ops.append(("spriteMode", "%s -> %s" % (meta.sprite_mode, row["mode_target"])))
            for sprite in stale:
                notes.append(
                    'rect "%s" %s != lienzo %dx%d: se descarta con el flip'
                    % (sprite["name"] or "?", _fmt_rect(sprite["rect"]), piece.size[0], piece.size[1])
                )
        border = parse_border(row["border"])
        if border is not None and meta.border != border:
            ops.append(("border", "%s -> %s" % (_fmt_border(meta.border), row["border"])))
        if row["max_tex"].isdigit() and meta.max_texture_size != int(row["max_tex"]):
            ops.append(("maxTextureSize", "%s -> %s" % (meta.max_texture_size, row["max_tex"])))

        if flip:
            old_ids = [
                (file_id, count)
                for file_id, count in sorted(piece.refs_by_fileid.items())
                if file_id not in (21300000, 2800000, 11400000)
            ]
            if old_ids:
                for file_id, count in old_ids:
                    ops.append(
                        (
                            "fileID-remap",
                            "fileID %d, guid %s -> 21300000, guid %s (%d ocurrencias, %d archivos)"
                            % (file_id, meta.guid, meta.guid, count, piece.ref_files),
                        )
                    )
                    refs_total += count
            else:
                notes.append("sin referencias a sus fileIDs: el flip no deja nada colgado")

        if not ops:
            continue

        planned += 1
        lines.append("PLAN  %s" % name)
        lines.append("      asset   %s  (guid %s)" % (row["origen"], meta.guid))
        for index, (op, detail) in enumerate(ops, start=1):
            lines.append("      op%-2d   %-14s %s" % (index, op, detail))
        for note in notes:
            lines.append("      nota  %s" % note)
        lines.append("")

    lines.append("TOTAL %d piezas con operaciones planificadas | %d ocurrencias de fileID a remapear" % (planned, refs_total))
    lines.append("MODO  dry-run: 0 escrituras (el unico que escribe es ArtMetaMigrator con apply=true)")
    return lines


# ------------------------------------------------------------------------- reporte


def print_report(result: dict, manifest: Path, with_refs: bool, verbose: bool, limit: int) -> None:
    counts = result["counts"]
    violations: list[Violation] = result["violations"]
    by_rule: dict[str, list[Violation]] = {}
    for violation in violations:
        by_rule.setdefault(violation.rule, []).append(violation)

    print("MANIFIESTO  %s" % manifest.relative_to(REPO_ROOT).as_posix() if manifest.is_relative_to(REPO_ROOT) else "MANIFIESTO  %s" % manifest)
    print(
        "FILAS       %d  (REPLACE %d | NEW %d | addresses %d)"
        % (counts["rows"], counts["replace"], counts["new"], counts["addresses"])
    )
    print(
        "LEDGER NEW  %d filas | %d GENERATED, %d DELIVERED, %d EXCLUDED registradas"
        % (counts["ledger"], counts["generated"], counts["delivered"], counts["excluded"])
    )
    print("VIOLACIONES %d en %d reglas" % (len(violations), len(by_rule)))
    print("")

    header = "%-18s %7s  %s" % ("REGLA", "PIEZAS", "QUE MIDE")
    print(header)
    print("-" * len(header))
    descriptions = {
        "NAME-REGEX": "regex de nombres RT-7",
        "CSV-ROW": "122 filas, 12 columnas, sin duplicados",
        "ESTADO": "REPLACE con origen / NEW sin archivo",
        "ORIGEN-EXISTS": "el path de origen existe",
        "META-EXISTS": "el .meta del origen existe y tiene guid",
        "FORMAT-JPG": "ASC-5: la entrega es PNG, no JPG",
        "PX-ACTUAL": "header ilegible (px_actual es historico, informativo)",
        "PX-ENTREGA": "header del archivo vs px_entrega (el gate de pixeles)",
        "MODE-TARGET": "spriteMode medido vs mode_target",
        "RECT-STALE": "rect del sprite vivo (spriteMode 2) vs lienzo completo",
        "BORDER-META": "spriteBorder vs columna border",
        "BORDER-SLICED": "borde declarado si se dibuja Sliced/Tiled",
        "MAX-TEX": "maxTextureSize vs max_tex",
        "PIVOT": "spritePivot vs pivot",
        "PPU": "spritePixelsToUnits vs ppu",
        "PX-ENTREGA-TECHO": "max(px_entrega) <= max_tex",
        "ADDRESS-RESOLVE": "la address existe en AssetGroups",
        "ADDRESS-PATH": "address -> guid -> path == origen",
        "NEW-LEDGER": "filas derivadas, sin duplicados, == sec.9 NEW",
        "NEW-ORPHAN": "PNG del namespace del cohorte sin fila en el ledger",
        "NEW-BLOCKED": "una BLOCKED_* con PNG en disco",
        "NEW-SHA": "sha distinto del ledger sin fila en deliveries-v1.csv",
        "NEW-GUID": "guid 32 hex, unico, == .meta",
        "NEW-META": "textureType 8 y alphaIsTransparency 1",
        "DELIVERY-ROW": "DELIVERED exige exactamente una fila con autor en deliveries-v1.csv",
        "DELIVERY-SHA": "DELIVERED exige sha(disco) == ledger.sha256",
        "EXCLUDED-MOTIVO": "EXCLUDED exige motivo no vacio (ART-4)",
        "EXCLUDED-PNG": "EXCLUDED no puede tener PNG en disco (ART-4)",
        "COHORT-SUM": "las dos identidades del cohorte: 122 = R + N y N = la suma de 5",
    }
    for rule in sorted(by_rule):
        print("%-18s %7d  %s" % (rule, len(by_rule[rule]), descriptions.get(rule, "")))

    if violations:
        print("")
        print("DETALLE")
        for rule in sorted(by_rule):
            print("  [%s]" % rule)
            items = by_rule[rule]
            shown = items if verbose else items[:limit]
            for violation in shown:
                print("    %-42s %s" % (violation.pieza, violation.detail))
            if len(items) > len(shown):
                print("    ... y %d mas (--verbose para ver todas)" % (len(items) - len(shown)))

    if result["info"]:
        print("")
        print("INFO / pendientes declarados")
        for line in result["info"]:
            print("  %s" % line)

    if with_refs:
        _print_refs(result)

    print("")
    print("Sliced/Tiled (Image m_Type 1/2): %d piezas con borde obligatorio (%d instancias)"
          % (len(result["sliced"]), result["sliced_instances"]))
    print("RESULTADO   %s" % ("PASS" if not violations else "FAIL"))


def _print_refs(result: dict) -> None:
    pieces: dict[str, Piece] = result["pieces"]
    consumers = result["consumers"]
    total = sum(piece.refs_total for piece in pieces.values())
    files = len({path for paths in consumers["files_by_guid"].values() for path in paths})
    by_fileid: Counter = Counter()
    for piece in pieces.values():
        by_fileid.update(piece.refs_by_fileid)

    print("")
    print("REFERENCIAS POR GUID (85 REPLACE, .unity/.prefab/.asset/.mat/.controller)")
    print("  total %d referencias en %d archivos" % (total, files))
    print("  por fileID: %s" % ", ".join("%d=%d" % (key, value) for key, value in sorted(by_fileid.items(), key=lambda kv: -kv[1])))
    sprite_refs = sum(count for file_id, count in by_fileid.items() if file_id not in (2800000, 11400000))
    mode2 = [piece for piece in pieces.values() if piece.meta and piece.meta.sprite_mode == 2]
    mode2_refs = sum(piece.refs_total for piece in mode2)
    print("  refs de sprite (no Texture2D ni ScriptableObject): %d" % sprite_refs)
    print("  piezas en spriteMode 2: %d | referencias a ellas: %d" % (len(mode2), mode2_refs))
    excluded = consumers.get("excluded_refs", 0)
    if excluded:
        print(
            "  fuera de la cuenta (Assets/_Recovery, no versionado): %d refs en %d archivos -> %d con backup"
            % (excluded, consumers.get("excluded_files", 0), total + excluded)
        )
    print("  sec.5 del manifiesto afirma '89 referencias por GUID': la medicion real es %d" % total)

    ranked = sorted(pieces.values(), key=lambda piece: -piece.refs_total)
    top = ranked[0] if ranked else None
    if top and top.refs_total:
        print("  top: %s con %d referencias en %d archivos" % (top.row["pieza"], top.refs_total, top.ref_files))


def build_json(result: dict, manifest: Path, exit_code: int) -> dict:
    pieces: dict[str, Piece] = result["pieces"]
    by_rule = Counter(violation.rule for violation in result["violations"])
    by_fileid = Counter()
    for piece in pieces.values():
        by_fileid.update(piece.refs_by_fileid)
    return {
        "tool": "tools/art/audit_art_manifest.py",
        "manifest": str(manifest),
        "counts": result["counts"],
        "violations": [violation.as_dict() for violation in result["violations"]],
        "violations_by_rule": dict(sorted(by_rule.items())),
        "info": result["info"],
        "refs": {
            "total": sum(piece.refs_total for piece in pieces.values()),
            "by_fileid": {str(key): value for key, value in sorted(by_fileid.items())},
            "pieces_in_mode2": sorted(piece.row["pieza"] for piece in pieces.values() if piece.meta and piece.meta.sprite_mode == 2),
            "excluded_recovery_refs": result["consumers"].get("excluded_refs", 0),
        },
        "sliced_or_tiled": result["sliced"],
        "sliced_or_tiled_instances": result["sliced_instances"],
        "exit_code": exit_code,
    }


# ----------------------------------------------------------------------------- cli


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Audita el contrato de arte sec.9 contra los archivos reales (sin Unity, sin PyYAML).",
    )
    parser.add_argument(
        "--manifest",
        type=Path,
        default=DEFAULT_MANIFEST,
        help="manifiesto con el bloque ```csv de la sec.9 (default: %s)" % DEFAULT_MANIFEST.relative_to(REPO_ROOT),
    )
    parser.add_argument("--json", type=Path, help="escribe el resumen machine-readable en este path")
    parser.add_argument("--verbose", action="store_true", help="imprime todas las violaciones, no solo las primeras")
    parser.add_argument("--limit", type=int, default=12, help="violaciones por regla impresas sin --verbose (default: 12)")
    parser.add_argument("--no-refs", action="store_true", help="omite el conteo de referencias por GUID")
    parser.add_argument("--plan", action="store_true", help="imprime el plan dry-run del migrador (solo lectura)")
    parser.add_argument(
        "--plan-piece",
        action="append",
        default=[],
        metavar="PREFIJO",
        help="con --plan, limita a las piezas que empiezan con PREFIJO (repetible)",
    )
    return parser


def main(argv: list[str]) -> int:
    args = build_parser().parse_args(argv)
    manifest = args.manifest if args.manifest.is_absolute() else (REPO_ROOT / args.manifest)

    try:
        result = audit(manifest, ASSETS_ROOT, with_refs=not args.no_refs)
    except ToolError as error:
        print("ERROR: %s" % error, file=sys.stderr)
        return 2

    exit_code = 1 if result["violations"] else 0

    if args.plan:
        for line in project_plan(result, args.plan_piece):
            print(line)
        return exit_code

    print_report(result, manifest, with_refs=not args.no_refs, verbose=args.verbose, limit=args.limit)

    if args.json:
        target = args.json if args.json.is_absolute() else (REPO_ROOT / args.json)
        target.parent.mkdir(parents=True, exist_ok=True)
        payload = build_json(result, manifest, exit_code)
        target.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
        print("JSON        %s" % target)

    return exit_code


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
