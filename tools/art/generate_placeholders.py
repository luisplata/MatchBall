#!/usr/bin/env python3
"""Genera los placeholders PNG del cohorte NEW de la sec.9 del contrato de arte.

Fuente de verdad: el bloque ```csv de la sec.9 de ``Docs/art/manifest-refresh-visual-v1.md``
(las filas NEW; hoy 37). Este script NO inventa medidas: lee ``px_entrega``, ``border``,
``max_tex``, ``pivot`` y ``ppu`` de esa tabla y escribe, por pieza:

    * ``Assets/Sprites/<pieza>``  PNG RGBA 8-bit, tamano exacto ``px_entrega``.
    * ``Docs/art/placeholders-v1.csv``  ledger derivado (hoy 33 GENERATED + 4 BLOCKED_*).
      Nunca se escribe a mano.

El cohorte NO es una constante: se deriva de la sec.9 (``122 = R + N``) y el ledger lo
declara con las cinco etiquetas de ``tools/art/art_ledger.py``. Las bloqueadas no generan
archivo: ``BLOCKED_PX`` (falta canvas en §9) y ``BLOCKED_BY_DESIGN`` (decision de design
abierta). Su fila lleva ``motivo`` no vacio con el artefacto que las desbloquea, el rol que
decide y el efecto en los conteos (APH-11). Una ``DELIVERED``/``EXCLUDED`` que ya esta en el
ledger se preserva verbatim: regenerar no puede reescribir una decision de entrega.

Familias visuales (APH-5). El contrato exige que un placeholder no se pueda confundir con
arte, y que su borde 9-slice sea verificable al pixel:

    frame       relleno opaco #E8EAF0 · marco 2 px #FF00FF inclusivo en el borde del lienzo ·
                4 bloques blancos 4x4 dentro del marco (detectan flip) · banda cian #00E5FF
                de 1 px inset por ``border`` cuando ``border != 0`` · placa #1A1D26 con
                "SIN ARTE" + nombre + WxH cuando ``w >= 100``.
    silhouette  lienzo transparente (el alfa ES la informacion) · forma de un solo tono y
                sin antialias · notch #FF00FF 6x3 dentro del alfa (la firma) · misma placa
                cuando ``w >= 100``, forzada opaca dentro del bbox de la forma.
    flat4       ``min(w,h) < 16``: relleno solido #FFFFFF sin ninguna marca. A 4x4 el marco
                de 2 px deja 0 px de interior y el sprite se estira a pantalla completa; el
                registro es ``familia=flat4`` (``except: sin_firma``).

Determinismo (APH-7). Dos corridas producen bytes identicos en el build canonico: fuente
unica ``ImageFont.load_default(size=...)`` (la Aileron embebida en Pillow, leida por FreeType
desde memoria: cero acceso a ``C:\\Windows\\Fonts`` ni a fontconfig), paleta y puntos
literales en este archivo, orden de iteracion = orden de §9, ``compress_level=9`` y
``optimize=False`` fijos, sin ``dpi``/``pnginfo`` (nada de chunks tIME, pHYs, tEXt, iTXt) y
cero ids o timestamps derivados del reloj. ``--twice`` lo autoprueba en la misma corrida.
Entre maquinas con otra version de zlib la verdad es el pixel decodificado (APH-8), no el
sha256: el ledger lo declara en su encabezado.

Uso:
    python tools/art/generate_placeholders.py                       # PNG + ledger desde §9
    python tools/art/generate_placeholders.py --out Temp/art --twice  # fuera de Assets/
    python tools/art/generate_placeholders.py --check               # gate: 0 ok / 1 drift / 2 §9
    python tools/art/generate_placeholders.py --manifest            # sha256 + agregado
    python tools/art/generate_placeholders.py --freeze-guid         # copia GUID de los .meta
    python tools/art/generate_placeholders.py --deliver PIEZA       # ledger-only: DELIVERED
    python tools/art/generate_placeholders.py --ack-sha PIEZA       # ledger-only: celda sha256
    python tools/art/generate_placeholders.py --check --hosts Temp/hosts.csv

El gate (`--check`) vive en `tools/art/check_placeholders.py` —plan B declarado en
`sdd/placeholders-arte-v2/tasks` cuando este archivo pasaba de 420 lineas— e importa el
parser de §9 y el render de aca: se revisa una sola vez y no puede divergir. El CLI se
mantiene en un solo flag para que el contrato no cambie.

``--out DIR`` renderiza fuera de ``Assets/`` y escribe el ledger en ``DIR/`` (mas el
``.meta``: los escribe Unity, nunca este script). ``--check`` evalua ``SURVIVE`` con los
hosts vivos del mapa (``Docs/art/placeholders-v1.md``); ``--hosts`` es el override.

Dos comandos tocan el ledger y NINGUNO escribe bytes de PNG (ART-2):

    --deliver PIEZA   exige exactamente una fila de ``deliveries-v1.csv`` con ese sha y autor
                      no vacio, y escribe ``estado=DELIVERED`` + ``sha256`` en el ledger.
    --ack-sha PIEZA   reescribe SOLO la celda ``sha256`` y solo si el sha en disco es el del
                      render derivado. Sobre una fila ``DELIVERED`` sale 1
                      ``DELIVERED-IMMUTABLE``; con un sha que no es el del render, 1
                      ``ACK-REFUSED``.

La corrida de escritura corre ``guard_write()`` completo ANTES del primer ``write_bytes``:
si el sha en disco difiere del ledger y no hay fila en ``deliveries-v1.csv``, sale 1
``WRITE-REFUSED`` con **0 archivos escritos**. El camino de reconocimiento no puede
regenerar el placeholder sobre arte.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import io
import math
import re
import struct
import sys
from dataclasses import dataclass
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

import art_ledger as ledger_mod

REPO_ROOT = Path(__file__).resolve().parents[2]
MANIFEST = REPO_ROOT / "Docs" / "art" / "manifest-refresh-visual-v1.md"
SPRITES_DIR = REPO_ROOT / "Assets" / "Sprites"
DEFAULT_LEDGER = REPO_ROOT / "Docs" / "art" / "placeholders-v1.csv"

MANIFEST_COLUMNS = ("pieza", "estado", "origen", "px_actual", "px_entrega", "mode_target",
                    "mode_actual", "border", "max_tex", "pivot", "ppu", "address")
LEDGER_COLUMNS = ledger_mod.LEDGER_COLUMNS
STATES = ledger_mod.STATES
EXPECTED_ROWS = ledger_mod.EXPECTED_ROWS
NAME_RE = re.compile(r"^[a-z0-9]+(_[a-z0-9]+)+_[0-9]{2}\.png$")
GUID_RE = re.compile(r"^guid: ([0-9a-f]{32})\s*$", re.MULTILINE)
FENCED_CSV_RE = re.compile(r"(?ms)^```csv\r?\n(.*?)^```")
PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"
FORBIDDEN_CHUNKS = (b"tIME", b"pHYs", b"tEXt", b"iTXt")
GENERIC_MOTIVO = ("tbd", "falta decision", "falta decisión", "sin definir todavia",
                  "pendiente", "a definir")

# Paleta literal: cero muestreo del entorno (APH-7).
FILL = (232, 234, 240)      # #E8EAF0, piso FRAME-FILL (los 3 canales >= 232)
MAGENTA = (255, 0, 255)     # #FF00FF, firma
CYAN = (0, 229, 255)        # #00E5FF, banda 9-slice
WHITE = (255, 255, 255)     # #FFFFFF, bloques de esquina y texto
PLATE = (26, 29, 38)        # #1A1D26, placa informativa
FRAME_PX = 2
CORNER_BLOCK = 4
NOTCH = (6, 3)
TEXT_MIN, TEXT_MAX = 7, 32
LEGIBLE_MIN = 4.5
SURVIVE_FLOOR = 0.004
SURVIVE_DELTA = 0.002

FLAT4 = ("ui_tutorial_dim_01.png", "game_board_zone_veil_01.png")
FRAME = (
    "ui_tutorial_bubble_body_01.png",
    "ui_tutorial_btn_skip_normal_01.png", "ui_tutorial_btn_skip_pressed_01.png",
    "ui_tutorial_btn_continue_normal_01.png", "ui_tutorial_btn_continue_pressed_01.png",
    "ui_tutorial_btn_retry_normal_01.png", "ui_tutorial_btn_retry_pressed_01.png",
    "ui_tutorial_panel_retry_01.png",
    "ui_hud_avatar_frame_player_01.png", "ui_hud_avatar_frame_ai_01.png",
    "ui_hud_score_pill_player_01.png", "ui_hud_score_pill_ai_01.png",
    "ui_hud_panel_player_01.png",
    "ui_menu_bg_panel_01.png",
    "ui_common_btn_primary_pressed_01.png", "ui_common_btn_primary_disabled_01.png",
    "ui_menu_btn_play_pressed_01.png",
)
SILHOUETTE = (
    "ui_tutorial_spotlight_ring_01.png",
    "ui_tutorial_hand_tap_01.png", "ui_tutorial_hand_tap_02.png", "ui_tutorial_hand_tap_03.png",
    "ui_tutorial_hand_point_left_01.png", "ui_tutorial_hand_point_right_01.png",
    "ui_tutorial_icon_star_01.png",
    "ui_tutorial_step_dot_normal_01.png", "ui_tutorial_step_dot_active_01.png",
    "ui_tutorial_step_dot_done_01.png",
    "ui_tutorial_icon_danger_01.png",
    "ui_hud_btn_settings_pressed_01.png",
    "ball_state_selected_ring_01.png", "ball_state_matched_01.png",
)
FAMILY = ({piece: "flat4" for piece in FLAT4}
          | {piece: "frame" for piece in FRAME}
          | {piece: "silhouette" for piece in SILHOUETTE})

CONTRACT_OWNER = "dueño del contrato de arte (Docs/art/manifest-refresh-visual-v1.md)"
DESIGN_OWNER = ("autor del design (sdd/placeholders-arte-v2, obs #4046) con firma del "
                "dueño del contrato de arte")
BLOCKED = {
    "ui_tutorial_bubble_tail_left_01.png": ("BLOCKED_PX",
        "la desbloquea §9 v3 del contrato con px_entrega definido para la cola y border "
        "l/b/r/t al pix. La decide el " + CONTRACT_OWNER + ". Efecto en los conteos: pasa a "
        "GENERATED, 34 GENERATED + 1 BLOCKED_PX + 2 BLOCKED_BY_DESIGN = 37."),
    "ui_tutorial_caption_pill_01.png": ("BLOCKED_PX",
        "la desbloquea §9 v3 del contrato con px_entrega definido para la pildora respetando "
        "h >= 48 (24 de padding + 16 de texto) y border l/b/r/t al pix. La decide el "
        + CONTRACT_OWNER + ". Efecto en los conteos: pasa a GENERATED, 34 GENERATED + 1 "
        "BLOCKED_PX + 2 BLOCKED_BY_DESIGN = 37."),
    "ui_tutorial_spotlight_fill_01.png": ("BLOCKED_BY_DESIGN",
        "la desbloquea una decision escrita en sdd/placeholders-arte-v2 (design, obs #4046) "
        "con uno de dos resultados: exclusion permanente (estado EXCLUDED, sin PNG) o "
        "placeholder con marcador verificable que el gate distinga del arte al 100%. La firma "
        "el " + DESIGN_OWNER + ". Efecto en los conteos: EXCLUDED deja 37 = 33 GENERATED + 2 "
        "BLOCKED_PX + 1 BLOCKED_BY_DESIGN + 1 EXCLUDED."),
    "ball_state_frozen_01.png": ("BLOCKED_BY_DESIGN",
        "la desbloquea una decision escrita en sdd/placeholders-arte-v2 (design, obs #4046) "
        "con uno de dos resultados: exclusion permanente (estado EXCLUDED, sin PNG) o "
        "placeholder con marcador verificable que impida leerlo como escarcha entregada. La "
        "firma el " + DESIGN_OWNER + ". Efecto en los conteos: EXCLUDED deja 37 = 33 "
        "GENERATED + 2 BLOCKED_PX + 1 BLOCKED_BY_DESIGN + 1 EXCLUDED."),
}

# Base literal de la mano (normalizada 0..1, y crece hacia abajo). Las 5 poses son la misma
# base con un offset y una escala por pose: tres poses de un unico nodo no pueden diferir en
# topologia, solo en gesto (proposal, riesgo "manos x3 con silueta distinta").
HAND_BASE = (
    (0.30, 0.28), (0.32, 0.14), (0.37, 0.08), (0.43, 0.12), (0.45, 0.24),
    (0.50, 0.10), (0.56, 0.07), (0.61, 0.13), (0.60, 0.28),
    (0.66, 0.18), (0.72, 0.20), (0.75, 0.28), (0.71, 0.38),
    (0.68, 0.46), (0.65, 0.56), (0.62, 0.66), (0.58, 0.76), (0.54, 0.86),
    (0.46, 0.91), (0.37, 0.88), (0.32, 0.78), (0.29, 0.62), (0.27, 0.46),
)
HAND_POSES = {
    "ui_tutorial_hand_tap_01.png": (0.00, 0.00, 1.00),
    "ui_tutorial_hand_tap_02.png": (0.01, -0.02, 0.98),
    "ui_tutorial_hand_tap_03.png": (0.02, -0.04, 0.96),
    "ui_tutorial_hand_point_left_01.png": (-0.09, 0.00, 1.00),
    "ui_tutorial_hand_point_right_01.png": (0.09, 0.00, 1.00),
}
# Parametros literales por pieza: (tipo, radios normalizados al semilado menor).
SHAPE = {
    "ui_tutorial_spotlight_ring_01.png": ("ring", (0.460, 0.400)),
    "ui_tutorial_icon_star_01.png": ("star", (0.460, 0.190)),
    "ui_tutorial_step_dot_normal_01.png": ("disc", (0.300,)),
    "ui_tutorial_step_dot_active_01.png": ("disc", (0.430,)),
    "ui_tutorial_step_dot_done_01.png": ("ring", (0.340, 0.210)),
    "ui_tutorial_icon_danger_01.png": ("triangle", (0.460,)),
    "ui_hud_btn_settings_pressed_01.png": ("ring", (0.420, 0.290)),
    "ball_state_selected_ring_01.png": ("ring", (0.460, 0.400)),
    "ball_state_matched_01.png": ("disc", (0.340,)),
}


class ContractError(Exception):
    """§9 o el ledger no se pueden leer: el generador no opina (exit 2)."""


class RowError(Exception):
    """Violacion de fila NOMBRADA (pieza + campo): exit 1, nunca 2 (DBT-4).

    Exit 2 queda reservado al §9 ausente o ilegible como bloque. Un `motivo` generico, un
    `px_entrega` vacio o un `border` que no entra son datos de UNA fila: el gate los nombra
    y sale 1.
    """

    def __init__(self, pieza: str, campo: str, regla: str, detail: str):
        super().__init__("%s %s %s: %s" % (regla, pieza, campo, detail))
        self.pieza = pieza
        self.campo = campo
        self.regla = regla
        self.detail = detail


def display(path: Path) -> str:
    """Path relativo al repo cuando se puede; absoluto si vive fuera del working tree."""
    try:
        return path.resolve().relative_to(REPO_ROOT).as_posix()
    except ValueError:
        return path.as_posix()


@dataclass(frozen=True)
class Manifest:
    """La sec.9 tipada: filas NEW + el conteo REPLACE (la identidad 122 = R + N)."""

    new: list["Row"]
    total: int
    replace: int

    @property
    def expected_new(self) -> int:
        return self.total - self.replace


@dataclass(frozen=True)
class Row:
    """Una fila NEW de §9, ya tipada (cero strings sueltos en el render)."""

    pieza: str
    px: tuple[int, int] | None
    border: tuple[int, int, int, int]
    mode_target: int
    max_tex: int
    pivot: tuple[float, float]
    ppu: float


@dataclass(frozen=True)
class Entry:
    """Pieza clasificada: qué familia la dibuja o por qué queda bloqueada."""

    row: Row
    estado: str
    familia: str
    motivo: str

    @property
    def generated(self) -> bool:
        return self.estado == "GENERATED"

    @property
    def writes_png(self) -> bool:
        """Las filas con PNG propio: GENERATED (derivado) y DELIVERED (arte real)."""
        return self.estado in ("GENERATED", "DELIVERED")


# --------------------------------------------------------------------------- §9 y ledger


def parse_px(value: str) -> tuple[int, int] | None:
    match = re.fullmatch(r"(\d+)\s*[xX]\s*(\d+)", value.strip())
    return (int(match.group(1)), int(match.group(2))) if match else None


def parse_border(value: str) -> tuple[int, int, int, int]:
    parts = [part.strip() for part in value.strip().split("/")]
    if len(parts) != 4 or not all(part.isdigit() for part in parts):
        raise ContractError("border '%s' no es l/b/r/t" % value)
    return tuple(int(part) for part in parts)  # type: ignore[return-value]


def read_manifest(path: Path) -> Manifest:
    try:
        text = path.read_text(encoding="utf-8")
    except OSError as error:
        raise ContractError("no se pudo leer %s: %s" % (path, error)) from error
    blocks = FENCED_CSV_RE.findall(text)
    if len(blocks) != 1:
        raise ContractError("%s tiene %d bloques ```csv: la verdad tiene que ser una"
                            % (path.name, len(blocks)))
    reader = csv.reader(blocks[0].splitlines())
    header = tuple(cell.strip() for cell in next(reader, ()))
    if header != MANIFEST_COLUMNS:
        raise ContractError("columnas inesperadas en la sec.9: %s" % (header,))
    rows: list[Row] = []
    total = 0
    replace = 0
    for raw in reader:
        cells = [cell.strip() for cell in raw]
        if not any(cells):
            continue
        if len(cells) != len(MANIFEST_COLUMNS):
            raise ContractError("fila con %d columnas en la sec.9: %s" % (len(cells), cells[0]))
        total += 1
        if cells[1] == "REPLACE":
            replace += 1
            continue
        if cells[1] != "NEW":
            continue
        try:
            rows.append(Row(
                pieza=cells[0],
                px=parse_px(cells[4]),
                border=parse_border(cells[7]),
                mode_target=int(cells[5]),
                max_tex=int(cells[8]),
                pivot=(float(cells[9].split("/")[0]), float(cells[9].split("/")[1])),
                ppu=float(cells[10]),
            ))
        except (ValueError, IndexError) as error:
            raise ContractError("fila ilegible en la sec.9 (%s): %s" % (cells[0], error)) from error
    if total != EXPECTED_ROWS:
        raise ContractError("la sec.9 tiene %d filas, el contrato congela %d" % (total, EXPECTED_ROWS))
    manifest = Manifest(new=rows, total=total, replace=replace)
    if len(rows) != manifest.expected_new:
        raise ContractError("la sec.9 declara %d filas NEW y la identidad 122 = %d + %d pide %d"
                            % (len(rows), replace, len(rows), manifest.expected_new))
    return manifest


def classify(rows: list[Row], ledger: dict[str, dict] | None = None,
             deliveries: list[dict] | None = None) -> list[Entry]:
    """Estado por fila: deriva GENERATED/BLOCKED_*, PRESERVA DELIVERED/EXCLUDED.

    El ledger entra como dato: una pieza que ya fue entregada o excluida no puede volver a
    GENERATED por regenerar (ART-1, ART-4). Una violacion de fila sale como `RowError`
    (exit 1, pieza + campo), no como `ContractError` (exit 2, DBT-4).
    """
    ledger = ledger or {}
    if deliveries is None:
        deliveries = ledger_mod.read_deliveries()
    entries: list[Entry] = []
    for row in rows:
        previous = ledger.get(row.pieza) or {}
        previous_estado = (previous.get("estado") or "").strip()
        if previous_estado in ledger_mod.PRESERVED_STATES:
            entries.append(Entry(row, previous_estado,
                                 previous.get("familia") or FAMILY.get(row.pieza) or "-",
                                 previous.get("motivo") or ""))
            continue
        if row.pieza in BLOCKED:
            blocked_estado, motivo = BLOCKED[row.pieza]
            if any(token in motivo.casefold() for token in GENERIC_MOTIVO):
                raise RowError(row.pieza, "motivo", "TBD-CAMPO",
                               "el motivo cae en un generico (APH-11)")
            entries.append(Entry(row, blocked_estado, "-", motivo))
            continue
        familia = FAMILY.get(row.pieza)
        if familia is None:
            raise RowError(row.pieza, "familia", "TBD-CAMPO",
                           "NEW sin familia ni bloqueo declarado")
        if row.px is None:
            raise RowError(row.pieza, "px_entrega", "TBD-CAMPO",
                           "px_entrega vacio y la pieza no esta bloqueada")
        left, bottom, right, top = row.border
        if left + right >= row.px[0] or top + bottom >= row.px[1]:
            raise RowError(row.pieza, "border", "TBD-CAMPO",
                           "border %s no entra en %dx%d (APH-4 aborta, no clampea)"
                           % ("/".join(map(str, row.border)), *row.px))
        if max(row.px) > row.max_tex:
            raise RowError(row.pieza, "max_tex", "TBD-CAMPO",
                           "px_entrega %dx%d excede max_tex %d" % (*row.px, row.max_tex))
        entries.append(Entry(row, "GENERATED", familia, ""))
    if len(entries) != len(rows):
        raise ContractError("clasificacion incompleta: %d de %d" % (len(entries), len(rows)))
    return entries


def ledger_text(entries: list[Entry], shas: dict[str, str], guids: dict[str, str],
                previous: dict[str, dict] | None = None) -> str:
    """Ledger derivado. DELIVERED/EXCLUDED se escriben VERBATIM desde el ledger previo."""
    previous = previous or {}
    buffer = io.StringIO()
    writer = csv.writer(buffer, lineterminator="\n")
    writer.writerow(LEDGER_COLUMNS)
    for entry in entries:
        row = entry.row
        if entry.estado in ledger_mod.PRESERVED_STATES and row.pieza in previous:
            writer.writerow([previous[row.pieza].get(column, "") for column in LEDGER_COLUMNS])
            continue
        px = "%dx%d" % row.px if row.px else "-"
        writer.writerow((
            row.pieza,
            entry.estado,
            px,
            "/".join(map(str, row.border)),
            entry.familia,
            "Assets/Sprites/" + row.pieza if entry.writes_png else "-",
            guids.get(row.pieza, ""),
            shas.get(row.pieza, ""),
            entry.motivo,
        ))
    return buffer.getvalue()


def read_ledger(path: Path) -> dict[str, dict]:
    """`pieza -> fila` del ledger; el formato y el encabezado los valida `art_ledger`."""
    return ledger_mod.ledger_index(ledger_mod.read_ledger(path))


# ------------------------------------------------------------------------------- render


def shape_points(pieza: str, width: int, height: int) -> tuple[list[tuple[int, int]], float]:
    """Puntos literales de la silueta. Devuelve (poligono, radio interno a perforar)."""
    center_x, center_y = (width - 1) / 2.0, (height - 1) / 2.0
    radius = min(width, height) / 2.0
    if pieza in HAND_POSES:
        offset_x, offset_y, scale = HAND_POSES[pieza]
        points = [
            (round((0.5 + (x - 0.5) * scale + offset_x) * (width - 1)),
             round((0.5 + (y - 0.5) * scale + offset_y) * (height - 1)))
            for x, y in HAND_BASE
        ]
        return points, 0.0
    kind, params = SHAPE[pieza]
    if kind == "disc":
        outer, inner = params[0], 0.0
    elif kind == "ring":
        outer, inner = params
    elif kind == "star":
        points = [
            (round(center_x + (params[0] if index % 2 == 0 else params[1]) * radius
                   * math.cos(-math.pi / 2 + index * math.pi / 5)),
             round(center_y + (params[0] if index % 2 == 0 else params[1]) * radius
                   * math.sin(-math.pi / 2 + index * math.pi / 5)))
            for index in range(10)
        ]
        return points, 0.0
    else:  # triangle
        outer = params[0]
        return [
            (round(center_x), round(center_y - outer * radius)),
            (round(center_x + outer * radius * 0.94), round(center_y + outer * radius * 0.82)),
            (round(center_x - outer * radius * 0.94), round(center_y + outer * radius * 0.82)),
        ], 0.0
    return [
        (round(center_x + outer * radius * math.cos(index * math.pi / 32)),
         round(center_y + outer * radius * math.sin(index * math.pi / 32)))
        for index in range(64)
    ], inner * radius


def text_width(font, text: str) -> int:
    box = font.getbbox(text)
    return box[2] - box[0]


def text_height(font, text: str) -> int:
    box = font.getbbox(text)
    return box[3] - box[1]


def fit_label(pieza: str, width: int, height: int, box: tuple[int, int, int, int]):
    """(tamano, fuente, lineas) que entran en ``box``; truncado sin '…' (design D2).

    Orden de truncado literal del design: primero se saca el ultimo caracter del nombre,
    despues la linea ``WxH`` y despues el nombre. Nunca se agrega un glifo nuevo. Si aun asi
    no entran las 3 lineas, se baja el tamano hasta que entren todas; recien cuando ninguna
    talla las admite se dibuja el prefijo mas largo en la talla mas grande que admita una.
    """
    available_w = box[2] - box[0] + 1 - 8  # 8 = padding de la placa a izquierda y derecha
    available_h = box[3] - box[1] + 1
    start = max(TEXT_MIN, min(TEXT_MAX, int(round(width / 12.0))))
    partial = None
    for size in range(start, TEXT_MIN - 1, -1):
        font = ImageFont.load_default(size=size)
        lines = ["SIN ARTE", pieza[:-4], "%dx%d" % (width, height)]
        while lines[1] and text_width(font, lines[1]) > available_w:
            lines[1] = lines[1][:-1]
        if text_width(font, lines[2]) > available_w:
            lines.pop(2)
        if lines[1] and text_width(font, lines[1]) > available_w:
            lines.pop(1)
        lines = [line for line in lines if line]
        line_h = max(text_height(font, line) for line in lines) + 1
        count = sum(1 for index in range(len(lines)) if (index + 1) * line_h + 2 <= available_h)
        if count == len(lines):
            return size, font, lines
        if count and partial is None:
            partial = (size, font, lines[:count])
    return partial


def draw_label(image: Image.Image, pieza: str, box: tuple[int, int, int, int]) -> bool:
    """Placa #1A1D26 opaca + texto blanco centrado en ``box``. True si dibujo algo."""
    width, height = image.size
    fitted = fit_label(pieza, width, height, box)
    if fitted is None:
        return False
    _, font, lines = fitted
    line_h = max(text_height(font, line) for line in lines) + 1
    plate_w = max(text_width(font, line) for line in lines) + 8
    plate_h = len(lines) * line_h + 2
    plate_x = max(0, min(width - plate_w, box[0] + ((box[2] - box[0] + 1) - plate_w) // 2))
    plate_y = max(0, min(height - plate_h, box[1] + ((box[3] - box[1] + 1) - plate_h) // 2))
    draw = ImageDraw.Draw(image)
    draw.rectangle([plate_x, plate_y, plate_x + plate_w - 1, plate_y + plate_h - 1], fill=PLATE + (255,))
    for index, line in enumerate(lines):
        draw.text((plate_x + plate_w / 2.0, plate_y + 1 + index * line_h + line_h / 2.0 - 0.5),
                  line, font=font, fill=WHITE + (255,), anchor="mm")
    return True


def render(entry: Entry) -> tuple[Image.Image, bool]:
    """Devuelve (imagen RGBA, etiqueta_dibujada). Determinista: cero fuentes del sistema."""
    width, height = entry.row.px
    if entry.familia == "flat4":
        return Image.new("RGBA", (width, height), WHITE + (255,)), False

    if entry.familia == "frame":
        image = Image.new("RGBA", (width, height), FILL + (255,))
        draw = ImageDraw.Draw(image)
        draw.rectangle([0, 0, width - 1, height - 1], outline=MAGENTA + (255,), width=FRAME_PX)
        for x, y in ((FRAME_PX, FRAME_PX), (width - FRAME_PX - CORNER_BLOCK, FRAME_PX),
                     (FRAME_PX, height - FRAME_PX - CORNER_BLOCK),
                     (width - FRAME_PX - CORNER_BLOCK, height - FRAME_PX - CORNER_BLOCK)):
            draw.rectangle([x, y, x + CORNER_BLOCK - 1, y + CORNER_BLOCK - 1], fill=WHITE + (255,))
        left, _, right, top = entry.row.border
        if any(entry.row.border):
            draw.rectangle([left, top, width - 1 - right, height - 1 - top],
                           outline=CYAN + (255,), width=1)
        # La placa vive primero dentro de la banda 9-slice (dejarla visible es criterio de
        # review, APA-2); solo si ahi no entra ninguna linea usa el interior del marco.
        boxes = []
        if any(entry.row.border):
            boxes.append((left + 2, top + 2, width - 3 - right, height - 3 - top))
        boxes.append((FRAME_PX + CORNER_BLOCK + 1, FRAME_PX + 1,
                      width - FRAME_PX - CORNER_BLOCK - 2, height - FRAME_PX - 2))
        labeled = False
        for box in boxes if width >= 100 else ():
            if box[2] > box[0] and box[3] > box[1] and draw_label(image, entry.row.pieza, box):
                labeled = True
                break
        return image, labeled

    image = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    points, inner_radius = shape_points(entry.row.pieza, width, height)
    draw.polygon(points, fill=FILL + (255,))
    if inner_radius > 0:
        center_x, center_y = (width - 1) / 2.0, (height - 1) / 2.0
        draw.ellipse([center_x - inner_radius, center_y - inner_radius,
                      center_x + inner_radius, center_y + inner_radius], fill=(0, 0, 0, 0))
    labeled = False
    if width >= 100:
        bbox = image.getchannel("A").getbbox()
        if bbox:
            inset = max(4, (bbox[2] - bbox[0]) // 12)
            labeled = draw_label(image, entry.row.pieza,
                                 (bbox[0] + inset, bbox[1] + inset,
                                  bbox[2] - inset - 1, bbox[3] - inset - 1))
    paint_notch(image)
    return image, labeled


def paint_notch(image: Image.Image) -> None:
    """Notch #FF00FF 6x3 sobre el primer tramo de alfa solido de >= 8 px (APH-5)."""
    width, height = image.size
    alpha = image.load()
    for y in range(height):
        run_start, run = None, 0
        for x in range(width + 1):
            solid = x < width and alpha[x, y][3] == 255
            if solid and run_start is None:
                run_start = x
            if solid:
                run += 1
            elif run_start is not None:
                if run >= NOTCH[0] + 2:
                    for dy in range(NOTCH[1]):
                        for dx in range(NOTCH[0]):
                            alpha[run_start + 1 + dx, y + dy] = MAGENTA + (255,)
                    return
                run_start, run = None, 0


def encode(image: Image.Image) -> bytes:
    buffer = io.BytesIO()
    # Sin dpi= (evita pHYs), sin pnginfo (evita tEXt/iTXt/tIME): mismo input -> mismos bytes.
    image.save(buffer, format="PNG", compress_level=9, optimize=False)
    return buffer.getvalue()


# ------------------------------------------------------------------------------- gate


def collisions(entries: list[Entry], render_dir: Path) -> list[str]:
    """APH-3: nombres unicos entre si y sin colision con otro path del repo.

    La colision con `Assets/` solo aplica cuando el destino ES `Assets/Sprites`: una corrida
    de ensayo en `Temp/` no entrega y por lo tanto no puede colisionar con lo ya entregado.
    """
    failures: list[str] = []
    names = [entry.row.pieza for entry in entries]
    for name in sorted({name for name in names if names.count(name) > 1}):
        failures.append("%s NOMBRE: repetido en la sec.9" % name)
    for name in names:
        if not NAME_RE.match(name):
            failures.append("%s NOMBRE: no cumple ^[a-z0-9]+(_[a-z0-9]+)+_[0-9]{2}\\.png$" % name)
    if render_dir.resolve() != SPRITES_DIR:
        return failures
    index: dict[str, list[str]] = {}
    for path in sorted((REPO_ROOT / "Assets").rglob("*")):
        if path.is_file() and path.suffix.lower() in (".png", ".jpg", ".jpeg"):
            relative = path.relative_to(REPO_ROOT).as_posix()
            index.setdefault(path.name, []).append(relative)
    for entry in entries:
        expected = display(render_dir / entry.row.pieza)
        others = [path for path in index.get(entry.row.pieza, []) if path != expected]
        if others:
            failures.append("%s NOMBRE: colisiona con %s" % (entry.row.pieza, ", ".join(others)))
    return failures


# --------------------------------------------------------------------------------- cli


def render_all(entries: list[Entry]) -> dict[str, tuple[Image.Image, bytes, bool]]:
    rendered = {}
    for entry in entries:
        if not entry.generated:
            continue
        image, labeled = render(entry)
        rendered[entry.row.pieza] = (image, encode(image), labeled)
    return rendered


def do_manifest(entries, render_dir) -> int:
    lines = []
    for entry in entries:
        if not entry.generated:
            continue
        path = render_dir / entry.row.pieza
        if not path.is_file():
            print("FALTA  %s" % display(path), file=sys.stderr)
            return 1
        lines.append("%s  Assets/Sprites/%s" % (hashlib.sha256(path.read_bytes()).hexdigest(),
                                                entry.row.pieza))
    generated = sum(1 for entry in entries if entry.generated)
    print("\n".join(lines))
    print("AGREGADO  %s  %d/%d" % (hashlib.sha256("\n".join(lines).encode("ascii")).hexdigest(),
                                   len(lines), generated))
    return 0 if len(lines) == generated else 1


def do_freeze_guid(entries, render_dir, ledger_path) -> int:
    guids: dict[str, str] = {}
    for entry in entries:
        if not entry.generated:
            continue
        meta = Path(str(render_dir / entry.row.pieza) + ".meta")
        if not meta.is_file():
            print("ERROR: falta %s: los .meta los escribe Unity (ArtMetaMigrator, cohorte new)"
                  % display(meta), file=sys.stderr)
            return 2
        match = GUID_RE.search(meta.read_text(encoding="utf-8", errors="replace"))
        if not match:
            print("ERROR: %s no declara guid" % meta.name, file=sys.stderr)
            return 2
        guids[entry.row.pieza] = match.group(1)
    if len(set(guids.values())) != len(guids):
        print("ERROR: GUID repetidos en los .meta: %s"
              % sorted(g for g in guids.values() if list(guids.values()).count(g) > 1), file=sys.stderr)
        return 2
    ledger = read_ledger(ledger_path)
    shas = {pieza: row["sha256"] for pieza, row in ledger.items()}
    ledger_path.write_text(ledger_text(entries, shas, guids, ledger), encoding="utf-8",
                           newline="\n")
    print("escrito %s: %d GUID congelados desde los .meta"
          % (display(ledger_path), len(guids)))
    return 0


def guard_write(entries, render_dir: Path, ledger: dict[str, dict],
                deliveries: list[dict]) -> list[str]:
    """ART-2: corre COMPLETO antes del primer `write_bytes`.

    Regla: si el sha del PNG en disco difiere del sha del ledger y no hay una fila de
    `deliveries-v1.csv` que declare esos bytes, el generador NO escribe nada. Un ledger sin
    sha es adopcion (el PNG todavia no fue reconocido por nadie) y no acusa.
    """
    failures: list[str] = []
    for entry in entries:
        if not entry.generated:
            continue
        pieza = entry.row.pieza
        path = render_dir / pieza
        if not path.is_file():
            continue
        previous = ((ledger.get(pieza) or {}).get("sha256") or "").strip()
        if not previous:
            continue  # adopcion: no hay sha contra el cual acusar drift
        disk = ledger_mod.sha256_of(path)
        if disk == previous:
            continue
        if ledger_mod.delivery_rows(deliveries, pieza, disk):
            continue
        failures.append(
            "%s WRITE-REFUSED: disco %s != ledger %s y sin fila en deliveries-v1.csv"
            % (pieza, disk, previous))
    return failures


def do_write(entries, render_dir, ledger_path, twice, deliveries_path=None) -> int:
    # Un solo validador para las dos mitades (plan B de tasks): el generador se niega a
    # escribir algo que su propio gate rechazaria.
    from check_placeholders import validate

    deliveries = ledger_mod.read_deliveries(deliveries_path or ledger_mod.DELIVERIES)
    rendered = render_all(entries)
    if twice:
        second = render_all(entries)
        for pieza, (_, data, _) in rendered.items():
            if data != second[pieza][1]:
                print("FALLA  %s: la segunda corrida cambio los bytes (APH-7)" % pieza, file=sys.stderr)
                return 1
        print("twice       %d piezas identicas en dos corridas" % len(rendered))
    failures = collisions(entries, render_dir)
    shas = {pieza: hashlib.sha256(data).hexdigest() for pieza, (_, data, _) in rendered.items()}
    by_piece = {entry.row.pieza: entry for entry in entries}
    for pieza, (image, data, _) in sorted(rendered.items()):
        failures.extend(validate(by_piece[pieza], data, image, None))
    ledger = read_ledger(ledger_path)
    for pieza, sha in shas.items():
        previous = (ledger.get(pieza) or {}).get("sha256") or ""
        if previous and previous != sha:
            failures.append("%s SHA: ledger %s, render %s (reconocer con --ack-sha %s, que es"
                            " ledger-only y no escribe PNG)"
                            % (pieza, previous, sha, pieza))
    # El guard corre completo ANTES de cualquier escritura (ART-2): drift sin entrega = 0 bytes.
    failures.extend(guard_write(entries, render_dir, ledger, deliveries))
    if failures:
        for failure in failures:
            print(failure, file=sys.stderr)
        print("FALLA  nada escrito: %d hallazgos (0 archivos escritos)" % len(failures),
              file=sys.stderr)
        return 1
    render_dir.mkdir(parents=True, exist_ok=True)
    for pieza, (_, data, _) in sorted(rendered.items()):
        (render_dir / pieza).write_bytes(data)
    guids = {pieza: row.get("guid", "") for pieza, row in ledger.items()}
    for pieza, row in ledger.items():
        if (row.get("estado") or "").strip() in ledger_mod.PRESERVED_STATES:
            shas[pieza] = row.get("sha256", "")
    ledger_path.parent.mkdir(parents=True, exist_ok=True)
    ledger_path.write_text(ledger_text(entries, shas, guids, ledger), encoding="utf-8",
                           newline="\n")
    print("escritos %d PNG en %s y el ledger %s"
          % (len(rendered), display(render_dir), display(ledger_path)))
    return 0


def do_ack(entries, render_dir: Path, ledger_path: Path, pieces: list[str],
           deliveries_path: Path | None = None) -> int:
    """ART-2: `--ack-sha` reescribe SOLO la celda `sha256`; jamas un PNG.

    Condiciones: la fila existe y su estado no es DELIVERED/EXCLUDED (`DELIVERED-IMMUTABLE`),
    y el sha en disco es exactamente el del render derivado (`ACK-REFUSED` si no).
    """
    rendered = render_all(entries)
    by_piece = {entry.row.pieza: entry for entry in entries}
    rows = ledger_mod.read_ledger(ledger_path)
    index = ledger_mod.ledger_index(rows)
    failures: list[str] = []
    changed = 0
    for pieza in pieces:
        entry = by_piece.get(pieza)
        row = index.get(pieza)
        if entry is None or row is None:
            failures.append("ACK-REFUSED %s: no esta en la sec.9 ni en el ledger" % pieza)
            continue
        estado = (row.get("estado") or "").strip()
        if estado in ledger_mod.PRESERVED_STATES or estado != "GENERATED":
            failures.append("DELIVERED-IMMUTABLE %s: estado '%s' (una entrega no se re-reconoce)"
                            % (pieza, estado or "(vacio)"))
            continue
        new_sha = hashlib.sha256(rendered[pieza][1]).hexdigest()
        path = render_dir / pieza
        if not path.is_file():
            failures.append("ACK-REFUSED %s: falta %s, no hay sha en disco que reconocer"
                            % (pieza, display(path)))
            continue
        disk = ledger_mod.sha256_of(path)
        if disk != new_sha:
            failures.append("ACK-REFUSED %s: disco %s != render derivado %s"
                            % (pieza, disk, new_sha))
            continue
        if (row.get("sha256") or "") == new_sha:
            print("SIN-CAMBIO  %s: el ledger ya declara %s" % (pieza, new_sha))
            continue
        previous = row["sha256"] if row["sha256"] else "(vacio)"
        row["sha256"] = new_sha
        changed += 1
        print("ACK  %s: sha256 %s -> %s" % (pieza, previous, new_sha))
    if failures:
        for failure in failures:
            print(failure)
        print("RESULTADO   FAIL  (%d hallazgos, 0 archivos escritos)" % len(failures))
        return 1
    ledger_mod.write_ledger(rows, ledger_path)
    print("escrito %s: %d celdas sha256 (0 PNG)" % (display(ledger_path), changed))
    return 0


def do_deliver(entries, ledger_path: Path, pieza: str, deliveries_path: Path | None = None) -> int:
    """ART-1: `--deliver` escribe `estado` + `sha256` desde `deliveries-v1.csv` (ledger-only)."""
    deliveries = ledger_mod.read_deliveries(deliveries_path or ledger_mod.DELIVERIES)
    rows = ledger_mod.read_ledger(ledger_path)
    index = ledger_mod.ledger_index(rows)
    row = index.get(pieza)
    if row is None:
        print("DELIVERY-ROW %s: no tiene fila en el ledger" % pieza)
        return 1
    declared = ledger_mod.delivery_rows(deliveries, pieza)
    if len(declared) != 1:
        shas = ", ".join((entry.get("sha256") or "").strip() or "(vacio)" for entry in declared)
        print("DELIVERY-ROW %s: %d filas con autor en deliveries-v1.csv (se exige exactamente 1)%s"
              % (pieza, len(declared), (": " + shas) if shas else ""))
        return 1
    sha = (declared[0].get("sha256") or "").strip()
    if not re.fullmatch(r"[0-9a-f]{64}", sha):
        print("DELIVERY-SHA %s: '%s' no es un sha256" % (pieza, sha))
        return 1
    path = ledger_mod.REPO_ROOT / (row.get("path") or "") if row.get("path") not in ("-", "") else None
    if path is not None and path.is_file():
        disk = ledger_mod.sha256_of(path)
        if disk != sha:
            print("DELIVERY-SHA %s: disco %s != deliveries %s" % (pieza, disk, sha))
            return 1
    row["estado"] = "DELIVERED"
    row["sha256"] = sha
    ledger_mod.write_ledger(rows, ledger_path)
    print("DELIVERED   %s %s (ledger-only, 0 PNG)" % (pieza, sha))
    return 0


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Genera y verifica los placeholders PNG del cohorte NEW de la sec.9.")
    parser.add_argument("--out", type=Path, help="directorio de salida (default Assets/Sprites)")
    parser.add_argument("--ledger", type=Path, help="ledger derivado (default Docs/art/placeholders-v1.csv)")
    parser.add_argument("--contract", type=Path, help="manifiesto con el bloque ```csv de la sec.9")
    parser.add_argument("--check", action="store_true", help="gate: 0 ok / 1 drift / 2 sec.9 ilegible")
    parser.add_argument("--manifest", action="store_true", help="imprime sha256 por pieza + agregado")
    parser.add_argument("--twice", action="store_true", help="renderiza dos veces y compara sha256")
    parser.add_argument("--freeze-guid", action="store_true", help="copia el GUID de los .meta al ledger")
    parser.add_argument("--hosts", type=Path, help="csv pieza,r,g,b: override de los hosts del mapa")
    parser.add_argument("--deliver", metavar="PIEZA",
                        help="ledger-only: marca DELIVERED desde deliveries-v1.csv (0 PNG)")
    parser.add_argument("--ack-sha", action="append", default=[], metavar="PIEZA",
                        help="ledger-only: reescribe la celda sha256, nunca un PNG")
    parser.add_argument("--deliveries", type=Path,
                        help="csv pieza,sha256,autor,fecha (default Docs/art/deliveries-v1.csv)")
    return parser


def main(argv: list[str]) -> int:
    args = build_parser().parse_args(argv)
    manifest_path = args.contract or MANIFEST
    render_dir = (args.out or SPRITES_DIR).resolve()
    ledger_path = args.ledger or (render_dir / "placeholders-v1.csv" if args.out else DEFAULT_LEDGER)
    try:
        manifest = read_manifest(manifest_path)
        entries = classify(manifest.new, read_ledger(ledger_path),
                           ledger_mod.read_deliveries(args.deliveries or ledger_mod.DELIVERIES))
    except ContractError as error:
        print("ERROR: %s" % error, file=sys.stderr)
        return 2
    except RowError as error:
        print(error, file=sys.stderr)
        return 1
    if args.check:
        # Plan B de tasks: el gate vive en check_placeholders.py (mismo importa-render, sin
        # re-derivar). El CLI se mantiene aca para que `--check` siga siendo uno solo.
        import check_placeholders

        return check_placeholders.run(entries, render_dir, ledger_path, args.hosts, manifest)
    if args.deliver:
        return do_deliver(entries, ledger_path, args.deliver, args.deliveries)
    if args.ack_sha:
        return do_ack(entries, render_dir, ledger_path, list(args.ack_sha), args.deliveries)
    if args.manifest:
        return do_manifest(entries, render_dir)
    if args.freeze_guid:
        return do_freeze_guid(entries, render_dir, ledger_path)
    return do_write(entries, render_dir, ledger_path, args.twice, args.deliveries)


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
