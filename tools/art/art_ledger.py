#!/usr/bin/env python3
"""Una sola definicion del ledger de arte: estados, entregas y allowlist de `px_entrega`.

Los tres gates —`generate_placeholders.py`, `check_placeholders.py` y
`audit_art_manifest.py`— leen de aca el vocabulario de estados, las columnas, la identidad
del cohorte y la lista cerrada de `px_entrega` pendientes. Este archivo NO importa Unity ni
PyYAML y no abre Assets/: solo csv, hashlib y git.

Que declara (regla -> req):

    STATES               cinco estados: GENERATED, DELIVERED, EXCLUDED, BLOCKED_PX,
                         BLOCKED_BY_DESIGN. Una etiqueta fuera de estos cinco es COHORT-SUM
    EXPECTED_ROWS        122 filas en la sec.9 del contrato (la identidad 122 = R + N)
    PX_ENTREGA_PENDING   las 6 piezas de §4 cuyo header sigue en `px_actual`; una septima
                         exige diff de codigo y review
    PROVENANCE           `Docs/art/provenance-v1.csv`: la procedencia CONGELADA de las
                         REPLACE (86 filas). Archivo SEPARADO del ledger a proposito
    ORPHAN_MOTIVO        las 4 REPLACE sin consumidor de PRV-4, con su motivo escrito
    read_ledger          filas del ledger (columnas exactas)
    read_deliveries      filas de `deliveries-v1.csv` (pieza, sha256, autor, fecha)
    delivery_rows        las filas que declaran una entrega concreta (autor no vacio)
    read_provenance      filas de `provenance-v1.csv` (baseline congelado de cada REPLACE)
    write_provenance     escribe o APPENDEA una fila por pieza, sin re-basar las existentes
    sha256_of            sha256 de un archivo, por bloques de 1 MiB
    git_tracked          `git ls-files --error-unmatch`: la evidencia versionada existe

Por que DOS archivos y no uno (design D-A): `generate_placeholders.ledger_text()` reescribe
el ledger entero desde disco en cada corrida, asi que un sha de REPLACE guardado ahi se
auto-sanaria y `REPLACE-SHA` (PRV-2) no podria observar nunca un reemplazo. `deliveries-v1.csv`
dice quien y cuando; `provenance-v1.csv` dice que bytes son el baseline (y no se reescribe).

Uso:
    python -c "import sys;sys.path.insert(0,'tools/art');import art_ledger as a;print(len(a.PX_ENTREGA_PENDING))"
"""

from __future__ import annotations

import csv
import hashlib
import subprocess
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
LEDGER = REPO_ROOT / "Docs" / "art" / "placeholders-v1.csv"
DELIVERIES = REPO_ROOT / "Docs" / "art" / "deliveries-v1.csv"
PROVENANCE = REPO_ROOT / "Docs" / "art" / "provenance-v1.csv"
SPRITES_DIR = REPO_ROOT / "Assets" / "Sprites"

LEDGER_COLUMNS = ("pieza", "estado", "px", "border", "familia", "path", "guid", "sha256",
                  "motivo")
DELIVERY_COLUMNS = ("pieza", "sha256", "autor", "fecha")
PROVENANCE_COLUMNS = ("pieza", "origen", "guid", "sha256", "px_actual", "px_entrega", "rol",
                      "motivo")

EXPECTED_ROWS = 122

# Los cinco estados del ledger (design, "Maquina de estados"). El orden es el de la suma que
# imprime COHORT-SUM y no se reordena sin cambiar el contrato.
STATES = ("GENERATED", "DELIVERED", "EXCLUDED", "BLOCKED_PX", "BLOCKED_BY_DESIGN")
BLOCKED_STATES = ("BLOCKED_PX", "BLOCKED_BY_DESIGN")
# Estados que se declaran fuera del generador: regenerar el ledger los escribe verbatim.
PRESERVED_STATES = ("DELIVERED", "EXCLUDED")

# CTR-1: las 6 piezas de §4 con header en `px_actual`. La lista es CERRADA: agregar la septima
# es un diff de codigo (y su review), no una fila de §9.
PX_ENTREGA_PENDING = {
    "ui_pass_banner_01.png": "2048x1596",
    "ui_pass_reward_avatar_01.png": "2048x2048",
    "ui_pass_reward_avatar_02.png": "1028x2048",
    "ui_pass_reward_background_01.png": "2048x831",
    "ui_pass_reward_background_02.png": "2048x831",
    "game_board_backdrop_01.png": "921x2048",
}

# PRV-4: `rol` de una fila de procedencia. Solo `sin_pantalla` se mide: es la declaracion de
# que la pieza no tiene consumidor, y el auditor la contrasta contra el barrido GUID->YAML.
ROLE_SCREEN = "con_pantalla"
ROLE_NO_SCREEN = "sin_pantalla"
ROLES = (ROLE_SCREEN, ROLE_NO_SCREEN)

# PRV-4: las 4 REPLACE con cero consumidores, con su motivo escrito. Lista CERRADA por la misma
# razon que PX_ENTREGA_PENDING: una quinta huerfana es un diff de codigo (y su review), no una
# fila nueva del CSV que nadie mira. `--capture-provenance` se niega a escribir si lo medido no
# es exactamente este conjunto: el baseline de procedencia se congela sobre un hecho declarado.
ORPHAN_MOTIVO = {
    "game_board_balance_base_01.png":
        "0 referencias por GUID: el tablero arma la barra con balance_bar_left/right_01 y "
        "ningun prefab dibuja la base de la balanza. La consumiria el prefab de la balanza "
        "cuando la use como fondo; hasta entonces no cuenta como cobertura.",
    "game_board_shadow_long_01.png":
        "0 referencias por GUID: la sombra viva de la linea es game_board_line_shadow_01 y la "
        "sombra larga no esta cableada en ningun prefab. La consumiria el tablero si separa la "
        "sombra de la barra; hasta entonces no cuenta como cobertura.",
    "ui_pass_state_lock_01.png":
        "0 referencias por GUID: el estado bloqueado del pase no tiene pantalla (el panel de "
        "pase no existe en Game.unity). Lo consumiria ese panel cuando se construya; hasta "
        "entonces no cuenta como cobertura.",
    "ui_common_divider_01.png":
        "0 referencias por GUID: Separador.png entra al manifiesto sin slot y ningun prefab de "
        "MainMenu lo dibuja. Lo consumiria un divisor de panel cuando el diseno lo pida; hasta "
        "entonces no cuenta como cobertura.",
}


class LedgerError(Exception):
    """El ledger o las entregas no se pueden leer: el gate no opina."""


def read_ledger(path: Path = LEDGER) -> list[dict]:
    """Filas del ledger derivado, validando el encabezado exacto."""
    if not path.is_file():
        return []
    with path.open(newline="", encoding="utf-8") as handle:
        rows = list(csv.DictReader(handle))
    if rows and tuple(rows[0].keys()) != LEDGER_COLUMNS:
        raise LedgerError("%s tiene columnas %s, se esperan %s"
                          % (path.name, tuple(rows[0].keys()), LEDGER_COLUMNS))
    return rows


def ledger_index(rows: list[dict]) -> dict[str, dict]:
    return {row["pieza"]: row for row in rows}


def write_ledger(rows: list[dict], path: Path) -> None:
    """Reescribe el ledger completo respetando el orden de columnas y el fin de linea."""
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.writer(handle, lineterminator="\n")
        writer.writerow(LEDGER_COLUMNS)
        for row in rows:
            writer.writerow([row.get(column, "") for column in LEDGER_COLUMNS])


def read_deliveries(path: Path = DELIVERIES) -> list[dict]:
    """La unica declaracion manual: `pieza,sha256,autor,fecha` (header-only es valido)."""
    if not path.is_file():
        return []
    with path.open(newline="", encoding="utf-8") as handle:
        rows = list(csv.DictReader(handle))
    if rows and tuple(rows[0].keys()) != DELIVERY_COLUMNS:
        raise LedgerError("%s tiene columnas %s, se esperan %s"
                          % (path.name, tuple(rows[0].keys()), DELIVERY_COLUMNS))
    return rows


def delivery_rows(rows: list[dict], pieza: str, sha: str | None = None) -> list[dict]:
    """Filas que declaran una entrega concreta: mismo `pieza`, mismo `sha256` (si se pide) y
    `autor` no vacio. Una fila sin autor no declara nada (ART-1b)."""
    wanted = (pieza or "").strip()
    matches = []
    for row in rows:
        if (row.get("pieza") or "").strip() != wanted:
            continue
        if sha is not None and (row.get("sha256") or "").strip().lower() != sha.strip().lower():
            continue
        if not (row.get("autor") or "").strip():
            continue
        matches.append(row)
    return matches


def read_provenance(path: Path = PROVENANCE) -> list[dict]:
    """Filas de `provenance-v1.csv`: el baseline congelado de cada REPLACE (PRV-1).

    Un archivo AUSENTE devuelve [] (el gate decide que hacer con eso); un archivo con otro
    encabezado es un error del contrato y no se adivina.
    """
    if not path.is_file():
        return []
    with path.open(newline="", encoding="utf-8") as handle:
        rows = list(csv.DictReader(handle))
    if rows and tuple(rows[0].keys()) != PROVENANCE_COLUMNS:
        raise LedgerError("%s tiene columnas %s, se esperan %s"
                          % (path.name, tuple(rows[0].keys()), PROVENANCE_COLUMNS))
    return rows


def provenance_index(rows: list[dict]) -> dict[str, dict]:
    return {row["pieza"]: row for row in rows}


def write_provenance(rows: list[dict], path: Path, append: bool = False) -> None:
    """Escribe (o appendea) sin re-basar: las filas existentes no se tocan. PRV-1.

    `append=False` es la corrida one-shot completa; `append=True` agrega las filas dadas al
    final de un archivo que ya existe (o lo crea con encabezado si no esta).
    """
    path.parent.mkdir(parents=True, exist_ok=True)
    exists = path.is_file() and path.stat().st_size > 0
    with path.open("a" if append and exists else "w", newline="", encoding="utf-8") as handle:
        writer = csv.writer(handle, lineterminator="\n")
        if not (append and exists):
            writer.writerow(PROVENANCE_COLUMNS)
        for row in rows:
            writer.writerow([row.get(column, "") for column in PROVENANCE_COLUMNS])


def sha256_of(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1 << 20), b""):
            digest.update(block)
    return digest.hexdigest()


def git_tracked(path: Path, root: Path = REPO_ROOT) -> bool:
    """`git ls-files --error-unmatch`: la evidencia esta versionada, no solo en Temp/."""
    try:
        relative = Path(path).resolve().relative_to(Path(root).resolve()).as_posix()
    except ValueError:
        return False
    result = subprocess.run(["git", "ls-files", "--error-unmatch", relative],
                            cwd=str(root), capture_output=True, text=True)
    return result.returncode == 0


def cohort_counts(rows: list[dict]) -> dict[str, int]:
    """Las cinco etiquetas, siempre presentes (0 si nadie las usa), en el orden de STATES."""
    return {estado: sum(1 for row in rows if (row.get("estado") or "").strip() == estado)
            for estado in STATES}


def unknown_states(rows: list[dict]) -> list[tuple[str, str]]:
    """`(pieza, etiqueta)` de cada fila con una etiqueta fuera de los cinco estados."""
    return [(row.get("pieza", "?"), (row.get("estado") or "").strip())
            for row in rows if (row.get("estado") or "").strip() not in STATES]


def cohort_sum_text(total: int, counts: dict[str, int]) -> str:
    """`37 = 33 + 0 + 0 + 2 + 2` con los cinco sumandos en el orden de STATES."""
    return "%d = %s" % (total, " + ".join(str(counts[estado]) for estado in STATES))


def cohort_identity_text(total: int, replace: int, new: int) -> str:
    """`122 = 85 REPLACE + 37 NEW`: la identidad del contrato, derivada y no constante."""
    return "%d = %d REPLACE + %d NEW" % (total, replace, new)
