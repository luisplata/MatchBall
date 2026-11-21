#!/usr/bin/env python3
"""Selftest de los gates de arte: casos nombrados, en proceso, todo bajo `Temp/art-selftest/`.

No toca `Assets/` (solo lee) ni `Docs/`: cada caso arma su sandbox en `Temp/` (ignorado,
`.gitignore:6`) y verifica un comportamiento que los gates de S1 deben sostener. Los casos
son la prueba falsable de la spec; ninguno es un "smoke test":

    fixture       CTR-2 ambas direcciones: el fixture mode 2 mide 1 rect, PlayerAvatar 0
    rect-stale    la regla sobre el arbol REAL da 0 RECT-STALE (los 10 eran el bloque muerto)
    ack           ART-2: ACK-REFUSED con sha que no es el render; DELIVERED-IMMUTABLE; 0 PNG
    write-guard   ART-2: el tren de escritura se niega con drift sin entrega y deja 0 archivos
    delivered     ART-1: una DELIVERED con fila de entrega pasa por merito (0 hallazgos)
    delivery-row  ART-1: la misma pieza sin fila en deliveries → DELIVERY-ROW
    excluded      ART-4: EXCLUDED cierra conteos; sin motivo → EXCLUDED-MOTIVO; con PNG → -PNG
    px-entrega    CTR-1: 6/6 pendientes, entregar baja el contador, la septima es violacion
    legible       DBT-1: el ratio se mide; cambiar la placa cambia el numero y puede fallar
    plate-decoded  DBT-1/W-8: la placa medida es el color DECODIFICADO del render, no la
                  constante; sin placa localizable el hallazgo aparece, no se saltea
    tbd           DBT-4: una fila nombrada sale 1 (TBD-CAMPO); un §9 ilegible sigue saliendo 2
    cohort-sum    ART-3: una etiqueta inventada en el ledger falla COHORT-SUM con pieza
    cohort-manifest  DBT-5: una etiqueta inventada en §9 sale 1 COHORT-SUM en las dos rutas
                  (check y auditor), sin traceback; el §9 ilegible sigue saliendo 2
    diff-allowlist  APW-6: el diff admite el tooling declarado y el `.meta` de un origen de la
                  procedencia; un `.cs` de juego y un asset fuera del contrato fallan, y el
                  mismo path se cuenta una vez (no una por hunk)
    verify-manifest-path  W-3: `--verify-manifest <PATH>` acepta un path relativo (el comando
                  de T-36) y un manifiesto fuera del indice falla EVIDENCE-UNTRACKED
    provenance    PRV-1: la captura cubre R/R, es one-shot (CAPTURE-IMMUTABLE) y --add no reescribe
    replace-sha   PRV-2: BASELINE / DELIVERED / UNKNOWN; solo UNKNOWN falla, con los dos shas
    replace-guid  PRV-3: el swap no mueve las referencias; un guid nuevo falla con los dos
    orphan        PRV-4: las 4 declaradas; una quinta → UNDECLARED y una con consumidor → STALE
    wiring-mono   CTR-4: `MonoBehaviour.freezeSprite` resuelve contra la procedencia y el
                  vaciado da FALTA; los buckets del mapa suman el cohorte del ledger
    zlib-limit    DBT-3: la declaracion existe y el gate la consulta (>= 1)
    evidence      DRL-2: el reporte del drill re-hashea su cuerpo y esta versionado; uno solo
                  en `Temp/` (ignorado) cuenta como ausente y falla EVIDENCE-UNTRACKED
    capture-diff  DRL-4: `changed ⊆ bbox` y `>= 1`; fuera del bbox → DIFF-OUTSIDE, sin cambio
                  → DIFF-EMPTY, tamanos distintos → ERROR (nada de hashes de captura)
    capture-manifest  DRL-3: el px declarado es el del PNG, una captura de un color falla y el
                  manifiesto cubre exactamente las filas cableadas con captura
    capture-declared  DRL-3: una fila cableada sin captura solo pasa declarada en
                  `CAPTURE_EXCEPTIONS`; una declaracion que envejece da CAPTURA-STALE

Uso:
    python tools/art/selftest_art_gates.py
    python tools/art/selftest_art_gates.py --only fixture,ack
Exit: 0 = todos los casos pasan / 1 = alguno falla.
"""

from __future__ import annotations

import argparse
import contextlib
import hashlib
import io
import os
import re
import shutil
import sys
from pathlib import Path

import art_ledger as ledger_mod
import audit_art_manifest as audit
import check_capture_diff as capture_diff
import check_placeholder_wiring as wiring
import check_placeholders as check
import generate_placeholders as gen
from PIL import Image

ROOT = gen.REPO_ROOT
SANDBOX = ROOT / "Temp" / "art-selftest"
FIXTURE = ROOT / "tools" / "art" / "fixtures" / "rect-stale" / "mode2-1344x768.png.meta"
DEAD_BLOCK = ROOT / "tools" / "art" / "fixtures" / "rect-stale" / "mode1-residual-1344x768.png.meta"
PLAYER_AVATAR = ROOT / "Assets" / "Sprites" / "PlayerAvatar.png.meta"
PIECE = "ui_tutorial_dim_01.png"
RETRY = "ui_tutorial_btn_retry_normal_01.png"
BLOCKED = "ui_tutorial_spotlight_fill_01.png"
REPLACE = "ui_common_btn_primary_normal_01.png"
ORPHAN = "ui_common_divider_01.png"
# La base del arte del change: `--diff` es el unico modo que prueba algo y su transcript es el
# del verify. El caso `diff-allowlist` la mide de verdad, y si el ref no existe lo dice.
DIFF_BASE = "5014093"


def fresh(name: str) -> Path:
    directory = SANDBOX / name
    if directory.exists():
        shutil.rmtree(directory)
    directory.mkdir(parents=True)
    return directory


def capture(function, *args, **kwargs):
    """Corre `function` capturando stdout+stderr combinados."""
    buffer = io.StringIO()
    with contextlib.redirect_stdout(buffer), contextlib.redirect_stderr(buffer):
        code = function(*args, **kwargs)
    return code, buffer.getvalue()


def manifest_copy(directory: Path, replacements: list[tuple[str, str]]) -> Path:
    text = gen.MANIFEST.read_text(encoding="utf-8")
    for old, new in replacements:
        if old not in text:
            raise AssertionError("el manifiesto no contiene %r" % old)
        text = text.replace(old, new, 1)
    path = directory / "manifest.md"
    path.write_text(text, encoding="utf-8")
    return path


def ledger_copy(directory: Path, mutate, name: str = "placeholders-v1.csv") -> Path:
    rows = ledger_mod.read_ledger()
    mutate(ledger_mod.ledger_index(rows))
    path = directory / name
    ledger_mod.write_ledger(rows, path)
    return path


def deliveries_copy(directory: Path, rows: list[tuple[str, str, str, str]]) -> Path:
    path = directory / "deliveries-v1.csv"
    import csv

    with path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.writer(handle, lineterminator="\n")
        writer.writerow(ledger_mod.DELIVERY_COLUMNS)
        for row in rows:
            writer.writerow(row)
    return path


def provenance_copy(directory: Path, mutate, name: str = "provenance-v1.csv") -> Path:
    """Copia del baseline real con una mutacion: los negativos de S2 nunca tocan Docs/."""
    rows = ledger_mod.read_provenance()
    if not rows:
        raise AssertionError("%s no existe: correr --capture-provenance" % ledger_mod.PROVENANCE)
    mutate(ledger_mod.provenance_index(rows))
    path = directory / name
    ledger_mod.write_provenance(rows, path)
    return path


@contextlib.contextmanager
def swapped(module, **attributes):
    """Cambia globals del gate (PROVENANCE/DELIVERIES/LEDGER) y los restaura pase lo que pase."""
    original = {name: getattr(module, name) for name in attributes}
    try:
        for name, value in attributes.items():
            setattr(module, name, value)
        yield
    finally:
        for name, value in original.items():
            setattr(module, name, value)


def real_entries(ledger_path: Path | None = None, deliveries: list[dict] | None = None):
    manifest = gen.read_manifest(gen.MANIFEST)
    ledger = gen.read_ledger(ledger_path or ledger_mod.LEDGER)
    return manifest, gen.classify(manifest.new, ledger,
                                  deliveries if deliveries is not None else [])


def snapshot(directory: Path) -> dict[str, str]:
    return {path.name: hashlib.sha256(path.read_bytes()).hexdigest()
            for path in sorted(directory.iterdir()) if path.is_file()}


def mirror_metas(render: Path) -> None:
    """El sandbox refleja el arbol de entrega: los `.meta` reales se COPIAN a Temp/."""
    for png in sorted(render.glob("*.png")):
        meta = gen.SPRITES_DIR / (png.name + ".meta")
        if meta.is_file():
            (render / meta.name).write_bytes(meta.read_bytes())


def violation_count(result: dict, rule: str) -> int:
    return sum(1 for violation in result["violations"] if violation.rule == rule)


# ------------------------------------------------------------------ casos


def case_fixture() -> list[str]:
    """CTR-2: el fixture mode 2 mide su rect; el mode 1 con bloque muerto no mide ninguno."""
    failures: list[str] = []
    meta = audit.read_meta(FIXTURE)
    rects = audit.live_rects(meta, (1344, 768))
    print("fixture mode2: %d rect" % len(rects))
    if len(rects) != 1 or rects[0]["rect"] != (321, 249, 10, 28):
        failures.append("fixture mode2 deberia medir 1 rect (321,249 10x28), midio %s"
                        % (rects,))
    # El bloque muerto era el de PlayerAvatar; S1b lo borro, asi que el falso positivo se
    # prueba sobre un .meta de fixture con spriteMode 1 y un rect serializado distinto del
    # lienzo. Sin este fixture la direccion "mode 1 -> 0 hallazgos" quedaria sin evidencia.
    dead_meta = audit.read_meta(DEAD_BLOCK)
    if dead_meta is None:
        failures.append("no se pudo leer %s" % DEAD_BLOCK.name)
    else:
        dead_rects = audit.live_rects(dead_meta, (1344, 768))
        print("fixture mode1 con %d sprite(s) serializado(s): %d rect vivo"
              % (len(dead_meta.sprites), len(dead_rects)))
        if not dead_meta.sprites:
            failures.append("el fixture mode 1 deberia serializar un bloque muerto")
        if dead_meta.sprite_mode != 1:
            failures.append("el fixture mode 1 tiene spriteMode %s" % dead_meta.sprite_mode)
        if dead_rects:
            failures.append("spriteMode 1 no deberia medir el bloque serializado, midio %s"
                            % (dead_rects,))
    real = audit.read_meta(PLAYER_AVATAR)
    if real is None:
        failures.append("no se pudo leer %s" % PLAYER_AVATAR.name)
    else:
        print("PlayerAvatar spriteMode %s: %d rect, %d sprite(s) serializado(s)"
              % (real.sprite_mode, len(audit.live_rects(real, (1344, 768))), len(real.sprites)))
        if audit.live_rects(real, (1344, 768)):
            failures.append("PlayerAvatar (spriteMode 1) no deberia medir rects muertos")
        if real.sprites:
            failures.append("PlayerAvatar conserva %d sprite(s) serializado(s): el sheet-clear"
                            " de S1b no corrio sobre el arbol real" % len(real.sprites))
    return failures


def case_rect_stale() -> list[str]:
    """CTR-2 sobre el arbol real: 0 RECT-STALE y el total declarado de S1 es 4."""
    failures: list[str] = []
    result = audit.audit(gen.MANIFEST, audit.ASSETS_ROOT, with_refs=False)
    stale = violation_count(result, "RECT-STALE")
    print("real: %d RECT-STALE, %d violaciones" % (stale, len(result["violations"])))
    if stale != 0:
        failures.append("RECT-STALE deberia ser 0 tras S1, es %d" % stale)
    if len(result["violations"]) != 4:
        failures.append("el baseline de S1 es 4 violaciones (3 FORMAT-JPG + 1 MODE-TARGET),"
                        " hay %d" % len(result["violations"]))
    return failures


def case_ack() -> list[str]:
    """ART-2: --ack-sha es ledger-only; las dos negativas nombradas y 0 bytes de PNG."""
    failures: list[str] = []
    work = fresh("ack")
    _, entries = real_entries()
    sprite = gen.SPRITES_DIR / PIECE
    png_before = ledger_mod.sha256_of(sprite)
    disk_sha = png_before

    corrupted = ledger_copy(work, lambda index: index[PIECE].__setitem__("sha256", "0" * 64))
    code, output = capture(gen.do_ack, entries, gen.SPRITES_DIR, corrupted, [PIECE])
    print("ack positivo: exit %d" % code)
    if code != 0:
        failures.append("ack de un sha viejo con disco == render deberia salir 0: %s" % output)
    fixed = ledger_mod.ledger_index(ledger_mod.read_ledger(corrupted))[PIECE]
    if fixed["sha256"] != disk_sha:
        failures.append("ack no reescribio la celda sha256 (%s != %s)"
                        % (fixed["sha256"], disk_sha))

    immutable = ledger_copy(work, lambda index: index[PIECE].__setitem__("estado", "DELIVERED"))
    code, output = capture(gen.do_ack, entries, gen.SPRITES_DIR, immutable, [PIECE])
    print("ack sobre DELIVERED: exit %d" % code)
    if code != 1 or "DELIVERED-IMMUTABLE" not in output:
        failures.append("--ack-sha sobre DELIVERED deberia salir 1 DELIVERED-IMMUTABLE: %s"
                        % output)

    tampered = work / "render"
    tampered.mkdir()
    data = bytearray(sprite.read_bytes())
    data[-1] ^= 0xFF
    (tampered / PIECE).write_bytes(bytes(data))
    refused_ledger = ledger_copy(work, lambda index: index[PIECE].__setitem__("sha256", "0" * 64))
    code, output = capture(gen.do_ack, entries, tampered, refused_ledger, [PIECE])
    print("ack con disco != render: exit %d" % code)
    if code != 1 or "ACK-REFUSED" not in output:
        failures.append("un sha que no es el del render deberia salir 1 ACK-REFUSED: %s" % output)
    if ledger_mod.sha256_of(sprite) != png_before:
        failures.append("--ack-sha modifico el PNG")
    return failures


def case_write_guard() -> list[str]:
    """ART-2: el tren de escritura con drift sin fila de entrega deja 0 archivos escritos."""
    failures: list[str] = []
    work = fresh("write")
    _, entries = real_entries(ledger_path=work / "none.csv")
    render = work / "render"
    target = render / PIECE

    code, output = capture(gen.do_write, entries, render, work / "ledger.csv", False)
    print("primera corrida (ledger vacio = adopcion): exit %d, %d PNG"
          % (code, len(list(render.glob("*.png")))))
    if code != 0:
        failures.append("la adopcion con ledger sin sha deberia escribir: %s" % output)
    before = snapshot(render) | {"ledger.csv": ledger_mod.sha256_of(work / "ledger.csv")}

    # Trampa de ART-2: el PNG en disco ya no es el placeholder que el ledger declara.
    data = bytearray(target.read_bytes())
    data[-1] ^= 0xFF
    target.write_bytes(bytes(data))
    before = before | {PIECE: ledger_mod.sha256_of(target)}
    code, output = capture(gen.do_write, entries, render, work / "ledger.csv", False)
    after = snapshot(render) | {"ledger.csv": ledger_mod.sha256_of(work / "ledger.csv")}
    print(output.strip().splitlines()[-1] if output.strip() else "")
    print("segunda corrida (drift sin entrega): exit %d" % code)
    if code != 1:
        failures.append("el drift sin fila de entrega deberia salir 1, salio %d" % code)
    if "WRITE-REFUSED" not in output:
        failures.append("falta WRITE-REFUSED en la salida: %s" % output)
    if "0 archivos escritos" not in output:
        failures.append("la salida no declara '0 archivos escritos': %s" % output)
    changed = sorted(name for name in before if after.get(name) != before[name])
    if changed:
        failures.append("la corrida negada escribio %d archivos: %s" % (len(changed), changed))
    else:
        print("archivos escritos: 0 de %d" % len(before))
    return failures


def _delivered_sandbox(work: Path, with_delivery: bool):
    render = work / "render"
    ledger_path = work / "ledger.csv"
    _, entries = real_entries(ledger_path=work / "none.csv")
    code, output = capture(gen.do_write, entries, render, ledger_path, False)
    if code != 0:
        raise AssertionError("no se pudo preparar el sandbox: %s" % output)
    mirror_metas(render)
    sha = ledger_mod.sha256_of(render / PIECE)
    path = ledger_copy(work, lambda index: index[PIECE].update(estado="DELIVERED", sha256=sha))
    rows = [(PIECE, sha, "rehearsal", "2026-11-20")] if with_delivery else []
    return render, path, deliveries_copy(work, rows)


def case_delivered() -> list[str]:
    """ART-1: una entrega declarada pasa por merito, sin --ack-sha."""
    failures: list[str] = []
    work = fresh("delivered")
    render, path, deliveries_path = _delivered_sandbox(work, True)
    manifest = gen.read_manifest(gen.MANIFEST)
    entries = gen.classify(manifest.new, gen.read_ledger(path),
                           ledger_mod.read_deliveries(deliveries_path))
    code, output = capture(check.run, entries, render, path, None, manifest, deliveries_path)
    print("--check con 1 DELIVERED: exit %d" % code)
    if code != 0 or "1 DELIVERED" not in output:
        failures.append("una DELIVERED con fila deberia dar 0 hallazgos y 1 DELIVERED: %s" % output)

    old_ledger, old_deliveries = audit.LEDGER, audit.DELIVERIES
    try:
        audit.LEDGER, audit.DELIVERIES = path, deliveries_path
        result = audit.audit(gen.MANIFEST, audit.ASSETS_ROOT, with_refs=False)
        rules = sorted(violation.rule for violation in result["violations"])
        print("auditor con 1 DELIVERED: %d violaciones %s" % (len(rules), rules))
        if any(rule.startswith("DELIVERY") for rule in rules):
            failures.append("el auditor acusa una DELIVERED valida: %s" % rules)
        if len(rules) != 4:
            failures.append("el auditor deberia seguir en 4 deudas, esta en %d" % len(rules))
    finally:
        audit.LEDGER, audit.DELIVERIES = old_ledger, old_deliveries
    return failures


def case_delivery_row() -> list[str]:
    """ART-1: la misma pieza sin fila de entrega falla DELIVERY-ROW con pieza y sha."""
    failures: list[str] = []
    work = fresh("delivery-row")
    render, path, deliveries_path = _delivered_sandbox(work, False)
    manifest = gen.read_manifest(gen.MANIFEST)
    entries = gen.classify(manifest.new, gen.read_ledger(path),
                           ledger_mod.read_deliveries(deliveries_path))
    code, output = capture(check.run, entries, render, path, None, manifest, deliveries_path)
    print("--check sin fila de entrega: exit %d" % code)
    if code != 1 or "DELIVERY-ROW" not in output:
        failures.append("sin fila de entrega deberia salir 1 DELIVERY-ROW: %s" % output)
    if PIECE not in output:
        failures.append("DELIVERY-ROW no nombra la pieza: %s" % output)
    return failures


def case_excluded() -> list[str]:
    """ART-4: EXCLUDED cierra los conteos, exige motivo y prohibe PNG."""
    failures: list[str] = []
    work = fresh("excluded")
    motivo = ("la desbloquea un panel de achievements en Game.unity (artefacto); decide el "
              "dueno del contrato de arte (rol); efecto: EXCLUDED, 37 = 32 GENERATED + 1 EXCLUDED"
              " + 2 BLOCKED_PX + 2 BLOCKED_BY_DESIGN (conteos)")
    render = work / "render"
    ledger_path = work / "ledger.csv"
    _, entries = real_entries(ledger_path=work / "none.csv")
    code, output = capture(gen.do_write, entries, render, ledger_path, False)
    if code != 0:
        raise AssertionError("no se pudo preparar el sandbox: %s" % output)
    mirror_metas(render)
    path = ledger_copy(work, lambda index: index[BLOCKED].update(estado="EXCLUDED", path="-",
                                                                  motivo=motivo), "excluded.csv")
    manifest = gen.read_manifest(gen.MANIFEST)
    deliveries_path = deliveries_copy(work, [])
    entries = gen.classify(manifest.new, gen.read_ledger(path), [])
    code, output = capture(check.run, entries, render, path, None, manifest, deliveries_path)
    print("EXCLUDED valida: exit %d" % code)
    if code != 0:
        failures.append("una EXCLUDED con motivo deberia cerrar sin hallazgos: %s" % output)

    empty = work / "sin-motivo.csv"
    rows = ledger_mod.read_ledger(path)
    ledger_mod.ledger_index(rows)[BLOCKED]["motivo"] = ""
    ledger_mod.write_ledger(rows, empty)
    entries = gen.classify(manifest.new, gen.read_ledger(empty), [])
    code, output = capture(check.run, entries, render, empty, None, manifest, deliveries_path)
    print("EXCLUDED sin motivo: exit %d" % code)
    if code != 1 or "EXCLUDED-MOTIVO" not in output:
        failures.append("EXCLUDED sin motivo deberia salir 1 EXCLUDED-MOTIVO: %s" % output)

    entries = gen.classify(manifest.new, gen.read_ledger(path), [])
    (render / BLOCKED).write_bytes(
        gen.encode(gen.Image.new("RGBA", (4, 4), (255, 255, 255, 255))))
    code, output = capture(check.run, entries, render, path, None, manifest, deliveries_path)
    print("EXCLUDED con PNG: exit %d" % code)
    if code != 1 or "EXCLUDED-PNG" not in output:
        failures.append("EXCLUDED con PNG deberia salir 1 EXCLUDED-PNG: %s" % output)
    return failures


def case_px_entrega() -> list[str]:
    """CTR-1: las 6 pendientes, la entrega que baja el contador y la septima que falla."""
    failures: list[str] = []
    result = audit.audit(gen.MANIFEST, audit.ASSETS_ROOT, with_refs=False)
    info = "\n".join(result["info"])
    print("pendientes reales: %d/%d" % (len(ledger_mod.PX_ENTREGA_PENDING),
                                        len(ledger_mod.PX_ENTREGA_PENDING)))
    if "PX-ENTREGA-PENDING  6/6" not in info:
        failures.append("el auditor no reporta 6/6 pendientes: %s" % info[-400:])
    if violation_count(result, "PX-ENTREGA"):
        failures.append("PX-ENTREGA deberia estar en 0: %d" % violation_count(result, "PX-ENTREGA"))
    if len(ledger_mod.PX_ENTREGA_PENDING) != 6:
        failures.append("la allowlist cerrada tiene %d piezas, no 6"
                        % len(ledger_mod.PX_ENTREGA_PENDING))

    rows = {row["pieza"]: row for row in audit.read_csv_block(gen.MANIFEST)}
    banner = rows["ui_pass_banner_01.png"]
    verdicts = {
        "header en px_actual": audit.px_entrega_verdict(banner, (4096, 1596))[0],
        "entregada en su px_entrega": audit.px_entrega_verdict(banner, (2048, 1596))[0],
        "header distinto de ambos": audit.px_entrega_verdict(banner, (3000, 1596))[0],
        "septima fuera de la allowlist": audit.px_entrega_verdict(rows["ui_menu_bg_01.png"],
                                                                  (999, 999))[0],
    }
    print("veredictos: %s" % verdicts)
    expected = {"header en px_actual": "pending", "entregada en su px_entrega": "ok",
                "header distinto de ambos": "violation",
                "septima fuera de la allowlist": "violation"}
    for label, wanted in expected.items():
        if verdicts[label] != wanted:
            failures.append("%s deberia ser %s, fue %s" % (label, wanted, verdicts[label]))
    return failures


def case_legible() -> list[str]:
    """DBT-1: el ratio se mide en pixeles; cambiar la placa cambia el numero y puede fallar."""
    failures: list[str] = []
    _, entries = real_entries(deliveries=[])
    entry = [item for item in entries if item.row.pieza == RETRY][0]
    original = gen.PLATE
    try:
        image, _ = gen.render(entry)
        measured = check.label_contrast(image)
        if measured is None:
            failures.append("no se midio texto/placa en %s" % RETRY)
            measured = (0.0, (0, 0, 0), (0, 0, 0))
        print("placa #%02X%02X%02X: %.2f:1" % (*original, measured[0]))

        gen.PLATE = (110, 110, 110)
        light_image, _ = gen.render(entry)
        light = check.label_contrast(light_image)
        if light is None or abs(light[0] - measured[0]) < 1e-6:
            failures.append("cambiar la placa no cambio el ratio reportado")
        else:
            print("placa #6E6E6E: %.2f:1 (el numero cambio)" % light[0])

        gen.PLATE = (170, 170, 170)
        bad_image, _ = gen.render(entry)
        findings = check.validate_render(entry, bad_image, None)
        legible = [finding for finding in findings if "LEGIBLE" in finding]
        print("placa #AAAAAA: %s" % (legible[0].split("LEGIBLE")[-1].strip() if legible
                                     else "sin hallazgo"))
        if not legible:
            failures.append("una placa clara deberia fallar LEGIBLE")
        elif "%02X%02X%02X" % gen.PLATE not in legible[0]:
            failures.append("LEGIBLE no muestra las dos muestras RGB: %s" % legible[0])
    finally:
        gen.PLATE = original
    return failures


def case_plate_decoded() -> list[str]:
    """DBT-1/W-8: el operando "placa" es el color DECODIFICADO del render, no `gen.PLATE`.

    Se repinta la placa sin tocar la constante: el gate tiene que medir el color nuevo (y fallar
    si ese par es ilegible). Una imagen con etiqueta y sin placa localizable NO se saltea.
    """
    from PIL import ImageDraw

    failures: list[str] = []
    _, entries = real_entries(deliveries=[])
    entry = [item for item in entries if item.row.pieza == RETRY][0]
    original = gen.PLATE
    try:
        image, _ = gen.render(entry)
        found = check.plate_region(image)
        if found is None:
            return ["no se localizo la placa en el render derivado de %s" % RETRY]
        plate, rect = found
        measured = check.label_contrast(image)
        print("placa decodificada #%02X%02X%02X, ratio %.2f:1" % (*plate, measured[0]))
        if plate != original:
            failures.append("la placa medida #%02X%02X%02X no es la dibujada #%02X%02X%02X"
                            % (*plate, *original))

        # La placa se dibuja de OTRO color y la constante se restaura antes de medir: si el
        # operando fuera `gen.PLATE`, `label_contrast` devolveria None (W-8).
        gen.PLATE = (170, 170, 170)
        repainted, _ = gen.render(entry)
    finally:
        gen.PLATE = original
    decoded = check.label_contrast(repainted)
    print("placa dibujada #AAAAAA con la constante ya restaurada: %s"
          % ("%.2f:1 placa #%02X%02X%02X" % (decoded[0], *decoded[2]) if decoded else "None"))
    if decoded is None:
        failures.append("una placa de otro color dejo LEGIBLE sin medir: devolvio None (el"
                        " operando sigue siendo la constante)")
    else:
        if decoded[2] != (170, 170, 170):
            failures.append("la placa medida no es la decodificada: #%02X%02X%02X"
                            % decoded[2])
        if abs(decoded[0] - measured[0]) < 1e-6:
            failures.append("cambiar el color dibujado no cambio el ratio reportado")
        findings = [finding for finding in check.validate_render(entry, repainted, None,
                                                                labeled=True)
                    if "LEGIBLE" in finding]
        if not findings or "AAAAAA" not in findings[0]:
            failures.append("LEGIBLE deberia medir la placa decodificada (#AAAAAA): %s" % findings)

    unplated = image.copy()
    ImageDraw.Draw(unplated).rectangle([rect[0], rect[1], rect[2], rect[3]],
                                       fill=gen.FILL + (255,))
    leftover = check.label_contrast(unplated)
    if leftover is not None:
        failures.append("sin placa localizable el operando deberia ser None (el caso no prueba"
                        " nada); se midio %s" % (leftover,))
    silent = [finding for finding in check.validate_render(entry, unplated, None, labeled=True)
              if "LEGIBLE" in finding]
    print("sin placa localizable: %s" % (silent[0].split("LEGIBLE")[-1].strip() if silent
                                         else "sin hallazgo"))
    if not silent:
        failures.append("una etiqueta sin placa localizable no puede saltear LEGIBLE en silencio")
    unlabeled = [finding for finding in check.validate_render(entry, unplated, None)
                 if "LEGIBLE" in finding]
    if unlabeled:
        failures.append("sin etiqueta dibujada (labeled=False) no corresponde el hallazgo: %s"
                        % unlabeled)
    return failures


def case_tbd() -> list[str]:
    """DBT-4: una fila nombrada sale 1 (TBD-CAMPO); §9 ilegible sigue saliendo 2."""
    failures: list[str] = []
    work = fresh("tbd")
    original = gen.BLOCKED["ui_tutorial_bubble_tail_left_01.png"]
    try:
        gen.BLOCKED["ui_tutorial_bubble_tail_left_01.png"] = ("BLOCKED_PX", "TBD")
        code, output = capture(gen.main, ["--check"])
    finally:
        gen.BLOCKED["ui_tutorial_bubble_tail_left_01.png"] = original
    print("motivo generico: exit %d" % code)
    if code != 1 or "TBD-CAMPO" not in output:
        failures.append("un motivo generico deberia salir 1 TBD-CAMPO: %s" % output)
    if "ui_tutorial_bubble_tail_left_01.png" not in output or "motivo" not in output:
        failures.append("TBD-CAMPO no nombra pieza y campo: %s" % output)

    broken = manifest_copy(work, [(",TBD,400x520,", ",TBD,TBD,")])
    code, output = capture(gen.main, ["--check", "--contract", str(broken)])
    print("px_entrega vacio: exit %d" % code)
    if code != 1 or "TBD-CAMPO" not in output or "px_entrega" not in output:
        failures.append("un px_entrega vacio deberia salir 1 nombrando el campo: %s" % output)

    empty = work / "sin-seccion.md"
    empty.write_text("# sin bloque csv\n", encoding="utf-8")
    code, output = capture(gen.main, ["--check", "--contract", str(empty)])
    print("sec.9 ilegible: exit %d" % code)
    if code != 2:
        failures.append("un §9 ilegible deberia salir 2, salio %d: %s" % (code, output))
    return failures


def case_cohort_sum() -> list[str]:
    """ART-3: una etiqueta inventada falla COHORT-SUM nombrando pieza y etiqueta."""
    failures: list[str] = []
    work = fresh("cohort")
    path = ledger_copy(work, lambda index: index[PIECE].__setitem__("estado", "ARTE"))
    original = audit.LEDGER
    try:
        audit.LEDGER = path
        result = audit.audit(gen.MANIFEST, audit.ASSETS_ROOT, with_refs=False)
    finally:
        audit.LEDGER = original
    matches = [violation for violation in result["violations"] if violation.rule == "COHORT-SUM"]
    print("estado inventado: %d COHORT-SUM" % len(matches))
    if not matches:
        failures.append("estado=ARTE deberia fallar COHORT-SUM")
    elif not any(PIECE == violation.pieza and "ARTE" in violation.detail
                 for violation in matches):
        failures.append("COHORT-SUM no nombra pieza y etiqueta: %s"
                        % [(violation.pieza, violation.detail) for violation in matches])
    return failures


def case_cohort_manifest() -> list[str]:
    """DBT-5: un `estado` inventado en §9 sale 1 COHORT-SUM en las DOS rutas, sin traceback;
    el §9 ilegible sigue saliendo 2 (la frontera de DBT-4 no se mueve)."""
    failures: list[str] = []
    work = fresh("cohort-manifest")
    doctored = manifest_copy(work, [(
        "ui_tutorial_dim_01.png,NEW,-,TBD,4x4,1,-,0/0/0/0,2048,0.5/0.5,100,-",
        "ui_tutorial_dim_01.png,ARTE,-,TBD,4x4,1,-,0/0/0/0,2048,0.5/0.5,100,-")])
    for name, function, flags in (("check", check.main, ["--contract", str(doctored)]),
                                  ("audit", audit.main, ["--manifest", str(doctored)])):
        try:
            code, output = capture(function, flags)
        except BaseException as error:  # un traceback no es un veredicto: el caso lo falla
            failures.append("%s: un estado inventado termino en %s (%s)"
                            % (name, type(error).__name__, error))
            continue
        print("%s estado=ARTE: exit %d" % (name, code))
        if code != 1:
            failures.append("%s: un estado inventado deberia salir 1, salio %d" % (name, code))
        if "Traceback" in output:
            failures.append("%s: el gate imprimio un traceback: %s" % (name, output.strip()))
        if "COHORT-SUM" not in output:
            failures.append("%s: falta COHORT-SUM: %s" % (name, output.strip()))
        elif PIECE not in output or "ARTE" not in output:
            failures.append("%s: COHORT-SUM no nombra pieza y etiqueta: %s"
                            % (name, output.strip()))

    empty = work / "sin-seccion.md"
    empty.write_text("# sin bloque csv\n", encoding="utf-8")
    for name, function, flags in (("check", check.main, ["--contract", str(empty)]),
                                  ("audit", audit.main, ["--manifest", str(empty)])):
        code, _ = capture(function, flags)
        print("%s sec.9 ilegible: exit %d" % (name, code))
        if code != 2:
            failures.append("%s: un §9 ilegible deberia seguir saliendo 2, salio %d"
                            % (name, code))
    return failures


def case_provenance() -> list[str]:
    """PRV-1: la captura es one-shot, cubre 85/85 y `--add` no reescribe una fila."""
    failures: list[str] = []
    work = fresh("provenance")
    target = work / "provenance-v1.csv"

    code, output = capture(audit.capture_provenance, gen.MANIFEST, audit.ASSETS_ROOT, target)
    rows = ledger_mod.read_provenance(target)
    replace_rows = [row for row in audit.read_csv_block(gen.MANIFEST)
                    if row["estado"] == "REPLACE"]
    print("captura: exit %d, %d filas (%d REPLACE en sec.9)" % (code, len(rows),
                                                                len(replace_rows)))
    if code != 0:
        failures.append("la captura deberia salir 0: %s" % output)
    if len(rows) != len(replace_rows):
        failures.append("la captura escribio %d filas y sec.9 declara %d REPLACE"
                        % (len(rows), len(replace_rows)))
    if any(not row["sha256"] or len(row["guid"]) != 32 for row in rows):
        failures.append("una fila capturada quedo sin sha o sin guid de 32 hex")

    frozen = ledger_mod.sha256_of(target)
    code, output = capture(audit.capture_provenance, gen.MANIFEST, audit.ASSETS_ROOT, target)
    print("segunda captura: exit %d" % code)
    if code != 1 or "CAPTURE-IMMUTABLE" not in output:
        failures.append("la segunda captura deberia salir 1 CAPTURE-IMMUTABLE: %s" % output)
    if ledger_mod.sha256_of(target) != frozen:
        failures.append("la corrida negada reescribio el baseline")

    code, output = capture(audit.capture_provenance, gen.MANIFEST, audit.ASSETS_ROOT, target,
                           REPLACE)
    print("--add de una pieza presente: exit %d" % code)
    if code != 1 or "ADD-REFUSED" not in output:
        failures.append("--add sobre una fila existente deberia salir 1 ADD-REFUSED: %s" % output)
    if ledger_mod.sha256_of(target) != frozen:
        failures.append("--add reescribio una fila existente")

    with swapped(audit, PROVENANCE=target):
        result = audit.audit(gen.MANIFEST, audit.ASSETS_ROOT, with_refs=False)
    info = "\n".join(result["info"])
    print("cobertura declarada: %s" % next((line for line in result["info"]
                                            if line.startswith("REPLACE-LEDGER")), "-"))
    if "%d/%d" % (len(replace_rows), len(replace_rows)) not in info:
        failures.append("el auditor no reporta %d/%d: %s" % (len(replace_rows),
                                                             len(replace_rows), info[-300:]))
    if any(violation.rule == "REPLACE-LEDGER" for violation in result["violations"]):
        failures.append("una captura completa no deberia fallar REPLACE-LEDGER")

    empty = provenance_copy(work, lambda index: index[REPLACE].__setitem__("sha256", ""),
                            "sin-sha.csv")
    with swapped(audit, PROVENANCE=empty):
        result = audit.audit(gen.MANIFEST, audit.ASSETS_ROOT, with_refs=False)
    matches = [violation for violation in result["violations"]
               if violation.rule == "REPLACE-LEDGER"]
    print("sha vacio: %d REPLACE-LEDGER -> %s"
          % (len(matches), matches[0].detail if matches else "-"))
    if len(matches) != 1 or matches[0].pieza != REPLACE:
        failures.append("un sha vacio deberia fallar REPLACE-LEDGER nombrando %s: %s"
                        % (REPLACE, [(v.pieza, v.detail) for v in matches]))
    elif "%d/%d" % (len(replace_rows) - 1, len(replace_rows)) not in matches[0].detail:
        failures.append("REPLACE-LEDGER no imprime la cobertura %d/%d: %s"
                        % (len(replace_rows) - 1, len(replace_rows), matches[0].detail))
    return failures


def case_replace_sha() -> list[str]:
    """PRV-2: BASELINE / DELIVERED / UNKNOWN; solo UNKNOWN falla, con los dos shas."""
    failures: list[str] = []
    work = fresh("replace-sha")
    rows = ledger_mod.provenance_index(ledger_mod.read_provenance())
    disk = ledger_mod.sha256_of(ROOT / rows[REPLACE]["origen"])
    broken = provenance_copy(work, lambda index: index[REPLACE].__setitem__("sha256", "0" * 64),
                             "sin-declarar.csv")

    with swapped(audit, PROVENANCE=broken):
        result = audit.audit(gen.MANIFEST, audit.ASSETS_ROOT, with_refs=False)
    unknown = [violation for violation in result["violations"] if violation.rule == "REPLACE-SHA"]
    info = "\n".join(result["info"])
    print("bytes cambiados sin declarar: %d REPLACE-SHA | %s" % (
        len(unknown), next((line for line in result["info"]
                            if line.startswith("REPLACE-SHA  ")
                            and "reemplazadas" in line), "-")))
    if len(unknown) != 1 or unknown[0].pieza != REPLACE:
        failures.append("UNKNOWN deberia ser 1 y nombrar %s: %s"
                        % (REPLACE, [(v.pieza, v.detail) for v in unknown]))
    elif "0" * 12 not in unknown[0].detail or disk[:12] not in unknown[0].detail:
        failures.append("REPLACE-SHA no imprime los dos shas: %s" % unknown[0].detail)
    if "0/%d reemplazadas" % len(rows) not in info:
        failures.append("el auditor no imprime el contador de reemplazadas (0/%d): %s"
                        % (len(rows), info[-300:]))

    declared = deliveries_copy(work, [(REPLACE, disk, "rehearsal", "2026-11-20")])
    with swapped(audit, PROVENANCE=broken, DELIVERIES=declared):
        result = audit.audit(gen.MANIFEST, audit.ASSETS_ROOT, with_refs=False)
    left = [violation for violation in result["violations"] if violation.rule == "REPLACE-SHA"]
    summary = next((line for line in result["info"]
                    if line.startswith("REPLACE-SHA  ") and "reemplazadas" in line), "-")
    print("declaracion valida: %d REPLACE-SHA | %s" % (len(left), summary))
    if left:
        failures.append("una entrega declarada no deberia fallar REPLACE-SHA: %s"
                        % [(v.pieza, v.detail) for v in left])
    if "1/%d reemplazadas" % len(rows) not in summary or "1 DELIVERED" not in summary:
        failures.append("el contador no subio a 1 DELIVERED (1/%d): %s" % (len(rows), summary))
    return failures


def case_replace_guid() -> list[str]:
    """PRV-3: el swap no mueve las referencias (son del GUID); un guid nuevo falla nombrando los dos."""
    failures: list[str] = []
    work = fresh("replace-guid")
    rows = ledger_mod.provenance_index(ledger_mod.read_provenance())
    disk = ledger_mod.sha256_of(ROOT / rows[REPLACE]["origen"])

    def refs(result: dict) -> tuple[int, int]:
        total = sum(piece.refs_total for piece in result["pieces"].values())
        files = len({path for paths in result["consumers"]["files_by_guid"].values()
                     for path in paths})
        return total, files

    before = audit.audit(gen.MANIFEST, audit.ASSETS_ROOT, with_refs=False)
    print("barrido real: %d referencias en %d archivos" % refs(before))

    swapped_bytes = provenance_copy(
        work, lambda index: index[REPLACE].__setitem__("sha256", "1" * 64), "swap.csv")
    declared = deliveries_copy(work, [(REPLACE, disk, "rehearsal", "2026-11-20")])
    with swapped(audit, PROVENANCE=swapped_bytes, DELIVERIES=declared):
        after = audit.audit(gen.MANIFEST, audit.ASSETS_ROOT, with_refs=False)
    print("tras simular el swap: %d referencias en %d archivos" % refs(after))
    if refs(after) != refs(before):
        failures.append("las referencias cambiaron con el swap: %s -> %s"
                        % (refs(before), refs(after)))
    if any(violation.rule == "REPLACE-GUID" for violation in after["violations"]):
        failures.append("un swap con el .meta intacto no deberia fallar REPLACE-GUID")

    new_guid = "0123456789abcdef0123456789abcdef"
    moved = provenance_copy(work, lambda index: index[REPLACE].__setitem__("guid", new_guid),
                            "guid-nuevo.csv")
    with swapped(audit, PROVENANCE=moved):
        result = audit.audit(gen.MANIFEST, audit.ASSETS_ROOT, with_refs=False)
    matches = [violation for violation in result["violations"]
               if violation.rule == "REPLACE-GUID"]
    print("guid nuevo: %d REPLACE-GUID -> %s"
          % (len(matches), matches[0].detail if matches else "-"))
    if len(matches) != 1 or matches[0].pieza != REPLACE:
        failures.append("un guid distinto deberia fallar REPLACE-GUID en %s: %s"
                        % (REPLACE, [(v.pieza, v.detail) for v in matches]))
    elif new_guid not in matches[0].detail or rows[REPLACE]["guid"] not in matches[0].detail:
        failures.append("REPLACE-GUID no imprime los dos valores: %s" % matches[0].detail)
    return failures


def case_orphan() -> list[str]:
    """PRV-4: las 4 declaradas; una quinta sin declarar y una declarada con consumidor."""
    failures: list[str] = []
    work = fresh("orphan")
    result = audit.audit(gen.MANIFEST, audit.ASSETS_ROOT, with_refs=False)
    summary = next((line for line in result["info"] if line.startswith("ORPHAN")), "-")
    print(summary)
    if not summary.startswith("ORPHAN      %d REPLACE sin pantalla" % len(ledger_mod.ORPHAN_MOTIVO)):
        failures.append("el auditor no imprime las %d sin pantalla: %s"
                        % (len(ledger_mod.ORPHAN_MOTIVO), summary))
    for pieza in ledger_mod.ORPHAN_MOTIVO:
        if pieza not in summary:
            failures.append("%s no aparece en la linea de huerfanas" % pieza)
    if any(violation.rule.startswith("ORPHAN") for violation in result["violations"]):
        failures.append("el baseline declarado no deberia fallar ORPHAN-*: %s"
                        % [(v.pieza, v.detail) for v in result["violations"]
                           if v.rule.startswith("ORPHAN")])

    undeclared = provenance_copy(
        work, lambda index: index[ORPHAN].update(rol=ledger_mod.ROLE_SCREEN, motivo=""),
        "sin-declarar.csv")
    with swapped(audit, PROVENANCE=undeclared):
        result = audit.audit(gen.MANIFEST, audit.ASSETS_ROOT, with_refs=False)
    matches = [violation for violation in result["violations"]
               if violation.rule == "ORPHAN-UNDECLARED"]
    print("huerfana sin declarar: %d ORPHAN-UNDECLARED -> %s"
          % (len(matches), matches[0].detail if matches else "-"))
    if len(matches) != 1 or matches[0].pieza != ORPHAN:
        failures.append("una huerfana sin declarar deberia fallar ORPHAN-UNDECLARED en %s: %s"
                        % (ORPHAN, [(v.pieza, v.detail) for v in matches]))

    stale = provenance_copy(
        work, lambda index: index[REPLACE].update(rol=ledger_mod.ROLE_NO_SCREEN,
                                                  motivo="declarada sin pantalla"),
        "declarada-con-consumidor.csv")
    with swapped(audit, PROVENANCE=stale):
        result = audit.audit(gen.MANIFEST, audit.ASSETS_ROOT, with_refs=False)
    matches = [violation for violation in result["violations"]
               if violation.rule == "ORPHAN-STALE"]
    print("declarada que gano consumidor: %d ORPHAN-STALE -> %s"
          % (len(matches), matches[0].detail if matches else "-"))
    if len(matches) != 1 or matches[0].pieza != REPLACE:
        failures.append("una declarada con consumidor deberia fallar ORPHAN-STALE en %s: %s"
                        % (REPLACE, [(v.pieza, v.detail) for v in matches]))
    return failures


def case_evidence() -> list[str]:
    """DRL-2: el reporte re-hashea y esta versionado; uno solo en `Temp/` no es evidencia."""
    failures: list[str] = []
    work = fresh("evidence")

    def report(path: Path, tamper: bool = False) -> None:
        body = "# drill\n\n| Piezas | 1 |\n| exact | 1 |\n\n"
        digest = hashlib.sha256(body.encode("utf-8")).hexdigest()
        # write_bytes, no write_text: en Windows `write_text` traduce "\n" a "\r\n" y el sha del
        # cuerpo dejaria de re-hashear por una razon que no tiene nada que ver con el gate.
        path.write_bytes((body + "sha256: " + ("0" * 64 if tamper else digest) + "\n")
                         .encode("utf-8"))

    missing = audit.check_evidence(work / "no-existe.md")[0]
    print("ausente: %d hallazgo(s) -> %s" % (len(missing), missing[0].detail if missing else "-"))
    if len(missing) != 1 or "no existe" not in missing[0].detail:
        failures.append("un reporte ausente deberia dar EVIDENCE-UNTRACKED: %s" % missing)

    untracked = work / "drill-new-v1.md"
    report(untracked)
    found = audit.check_evidence(untracked)[0]
    print("solo en Temp/: %d hallazgo(s) -> %s"
          % (len(found), found[0].detail if found else "-"))
    if len(found) != 1 or found[0].rule != "EVIDENCE-UNTRACKED":
        failures.append("un reporte en Temp/ deveria fallar EVIDENCE-UNTRACKED: %s" % found)
    elif "versionado" not in found[0].detail:
        failures.append("el hallazgo no dice que el reporte no esta versionado: %s"
                        % found[0].detail)

    tampered = work / "drill-tampered-v1.md"
    report(tampered, tamper=True)
    forged = audit.check_evidence(tampered)[0]
    print("sha forjado: %d hallazgo(s) -> %s"
          % (len(forged), forged[0].detail if forged else "-"))
    if len(forged) != 1 or "sha256" not in forged[0].detail:
        failures.append("un sha de cuerpo que no re-hashea deberia fallar: %s" % forged)

    real, info = audit.check_evidence()
    print("real: %d hallazgo(s) -> %s" % (len(real), info[0] if info else "-"))
    if real:
        failures.append("el reporte versionado del drill falla: %s"
                        % [(v.rule, v.detail) for v in real])
    elif not info:
        failures.append("el gate no reporta la evidencia cuando pasa")
    return failures


def case_capture_declared() -> list[str]:
    """DRL-3: una fila cableada sin captura solo pasa declarada; la lista envejece ruidosa."""
    failures: list[str] = []
    rows = wiring.read_map()
    pending = [row["pieza"] for row in rows
               if row["estado"] == "cableado" and not row["captura"]]
    print("cableadas sin captura: %s" % (", ".join(pending) or "-"))
    allowed, declared, _ = wiring.capture_report(rows, pending)
    if allowed or len(declared) != len(pending):
        failures.append("una fila declarada en CAPTURE_EXCEPTIONS sigue fallando: %s" % allowed)
    if not any("ball_state_frozen_01.png" in note for note in declared):
        failures.append("la excepcion declarada no se imprime por nombre: %s" % declared)

    undeclared, _, _ = wiring.capture_report(rows, ["ui_tutorial_dim_01.png"])
    print("no declarada: %s" % (undeclared[0] if undeclared else "-"))
    if len(undeclared) != 1 or "sin captura declarada" not in undeclared[0]:
        failures.append("una fila sin captura y sin declarar deberia fallar: %s" % undeclared)

    # La lista cerrada se mide en la otra direccion: si la fila ya declara captura, la
    # declaracion envejecio y el gate tiene que acusarla (no quedar verde por silencio).
    stale = [dict(row) for row in rows]
    for row in stale:
        if row["pieza"] == "ball_state_frozen_01.png":
            row["captura"] = "ball_state_frozen_01.png"
    stale_failures, _, _ = wiring.capture_report(stale, [])
    print("excepcion envejecida: %s" % (stale_failures[0] if stale_failures else "-"))
    if not stale_failures or "CAPTURA-STALE" not in stale_failures[0]:
        failures.append("una excepcion que ya tiene captura deberia dar CAPTURA-STALE: %s"
                        % stale_failures)
    return failures


def case_capture_manifest() -> list[str]:
    """DRL-3: px declarados, captura uniforme y cobertura; el manifiesto es falsable."""
    failures: list[str] = []
    work = fresh("capture-manifest")
    captures = work / "captures"
    captures.mkdir()
    piece = "ui_test_01.png"

    sandbox_map = work / "map.md"
    sandbox_map.write_text(
        "```csv\n" + ",".join(capture_diff.MAP_COLUMNS) + "\n"
        + "%s,image,Assets/Scenes/Game.unity,N,Image,m_Sprite,cableado,-,-,%s\n"
        % (piece, piece) + "```\n", encoding="utf-8")
    manifest = work / "captures-v1.md"

    def write_manifest(px: str = "4x4", colours: int = 1) -> None:
        manifest.write_text(
            "```csv\n" + ",".join(capture_diff.COLUMNS) + "\n"
            + '%s,%s,20x20,"4,3,4,4",%d\n' % (piece, px, colours) + "```\n", encoding="utf-8")

    def measure():
        return capture_diff.verify_manifest(manifest, map_path=sandbox_map, captures=captures)

    png = captures / piece
    image = Image.new("RGBA", (4, 4), (7, 8, 9, 255))
    image.save(png)
    write_manifest(colours=1)

    with swapped(ledger_mod, CAPTURE_EXCEPTIONS={}):
        uniform, _ = measure()
        print("uniforme: %s" % (uniform[0] if uniform else "-"))
        if not any("un solo color" in failure for failure in uniform):
            failures.append("un PNG de un solo color deberia fallar: %s" % uniform)

        image.putpixel((0, 0), (200, 0, 200, 255))
        image.save(png)
        write_manifest(colours=2)
        ok, notes = measure()
        # El rig y el manifiesto se versionan con el commit: en el sandbox la unica condicion
        # que no se puede cumplir es `git ls-files`, y por eso se filtra junto con `Temp/`.
        kept = [failure for failure in ok
                if not failure.startswith(("EVIDENCE-UNTRACKED", "RIG:"))]
        print("dos colores: %d hallazgo(s) -> %s" % (len(kept), kept or notes[0]))
        if kept:
            failures.append("una captura valida de 2 colores no deberia fallar: %s" % kept)

        image.resize((8, 4)).save(png)
        wrong, _ = measure()
        print("px mentidos: %s" % (wrong[0] if wrong else "-"))
        if not any("manifiesto declara" in failure for failure in wrong):
            failures.append("un px declarado distinto del PNG deberia fallar: %s" % wrong)

        with swapped(capture_diff, RIG=work / "no-existe.cs"):
            untracked, _ = measure()
        if not any(failure.startswith("RIG:") for failure in untracked):
            failures.append("sin el rig versionado, la captura no es regenerable: %s" % untracked)
    return failures


def case_capture_diff() -> list[str]:
    """DRL-4: `changed ⊆ bbox` y `changed >= 1`, medido sobre el marco y sin hashes."""
    failures: list[str] = []
    work = fresh("capture-diff")
    bbox = (4, 3, 6, 5)
    base = Image.new("RGBA", (20, 20), (10, 20, 30, 255))
    before = work / "before.png"
    base.save(before)

    same = work / "same.png"
    base.save(same)
    inside = work / "inside.png"
    inside_image = base.copy()
    inside_image.putpixel((5, 4), (255, 0, 255, 255))
    inside_image.save(inside)
    outside = work / "outside.png"
    outside_image = base.copy()
    outside_image.putpixel((15, 15), (255, 0, 255, 255))
    outside_image.save(outside)

    cases = (
        ("sin cambio", same, "DIFF-EMPTY", 0, 0),
        ("un px dentro", inside, "PASS", 1, 0),
        ("un px fuera", outside, "DIFF-OUTSIDE", 1, 1),
    )
    for name, path, verdict, changed, parked in cases:
        issues, measured, total, outside_count = capture_diff.diff(before, path, bbox, 20, 20)
        print("%s: %s (%d cambiados, %d fuera)" % (name, measured, total, outside_count))
        if issues:
            failures.append("%s: el gate devolvio un error: %s" % (name, issues))
        if measured != verdict or total != changed or outside_count != parked:
            failures.append("%s: se esperaba %s con %d dentro / %d fuera, midio %s con %d / %d"
                            % (name, verdict, changed, parked, measured, total, outside_count))

    bigger = work / "bigger.png"
    Image.new("RGBA", (40, 40), (0, 0, 0, 255)).save(bigger)
    issues, measured, _, _ = capture_diff.diff(before, bigger, bbox, 20, 20)
    print("tamanos distintos: %s -> %s" % (measured, issues[0] if issues else "-"))
    if measured != "ERROR" or not issues:
        failures.append("dos marcos de distinto tamano deberian dar ERROR: %s" % measured)
    return failures


def case_diff_allowlist() -> list[str]:
    """APW-6: el diff de un PR de arte admite SOLO el tooling declarado y el `.meta` de un
    origen de la procedencia; un `.cs` de juego o un asset fuera del contrato siguen fallando,
    y el mismo path se cuenta una vez, no una por hunk."""
    failures: list[str] = []
    rows = wiring.read_map()
    tooling = wiring.TOOLING_ALLOWED[0]
    contract = sorted(wiring.read_contract_assets())[0] + ".meta"
    foreign = "Assets/Sprites/BALLPASS/fuera-del-contrato.png.meta"
    code = "Assets/Scripts/App/Game.cs"

    changed = [(tooling, "add", 10, "  m_Name: ArtCaptureRig"),
               (tooling, "add", 11, "  m_EditorVersion: 6000.3.10f1")]
    # Tres hunks sobre el MISMO `.meta` del contrato: el hallazgo, si lo hubiera, es uno.
    changed += [(contract, "add", line, "") for line in (196, 197, 240)]
    changed += [(foreign, "add", line, "") for line in (3, 4, 5)]
    changed += [(code, "add", line, "") for line in (7, 8)]

    with swapped(wiring, changed_lines=lambda base: list(changed)):
        findings = wiring.check_diff(rows, "no-hace-falta-una-base-real")
    print("allowlist: %d hallazgo(s) -> %s" % (len(findings), findings))
    if len(findings) != 2:
        failures.append("el diff deberia dejar 2 hallazgos (un .cs de juego y un asset fuera del"
                        " contrato), dejo %d: %s" % (len(findings), findings))
    if any("no esta en el mapa" in finding and foreign not in finding for finding in findings):
        failures.append("el path fuera del contrato no se nombra: %s" % findings)
    if any(target in finding for finding in findings
           for target in ("DIFF: %s " % tooling, "DIFF: %s " % contract)):
        failures.append("el tooling declarado o el .meta del contrato siguen fallando: %s"
                        % findings)
    if not any(finding.startswith("DIFF: %s es codigo" % code) for finding in findings):
        failures.append("un .cs de juego deberia fallar nombrando el path: %s" % findings)

    # La direccion real: sobre el arbol del cambio y su base, el gate no tiene nada que decir.
    try:
        real = wiring.check_diff(rows, DIFF_BASE)
    except wiring.MapError as error:
        print("base %s no disponible (%s): se omite la medicion real" % (DIFF_BASE, error))
        real = None
    if real is not None:
        print("arbol real contra %s: %d hallazgo(s)" % (DIFF_BASE, len(real)))
        if real:
            failures.append("el arbol del cambio no cierra contra su base %s: %s"
                            % (DIFF_BASE, real))
    return failures


def case_verify_manifest_path() -> list[str]:
    """W-3: `--verify-manifest <PATH>` acepta el path relativo del runbook (T-36) sin reventar
    en `relative_to`; un manifiesto fuera del indice sigue fallando `EVIDENCE-UNTRACKED`, no con
    un ValueError que habla de paths y no de manifiestos."""
    failures: list[str] = []
    real = ROOT / "Docs" / "art" / "evidence" / "captures-v1.md"
    relative = os.path.relpath(real, Path.cwd())
    try:
        code, output = capture(capture_diff.main, ["--verify-manifest", relative])
    except BaseException as error:  # un traceback es exactamente el defecto que se cierra
        return ["--verify-manifest con path relativo termino en %s (%s)"
                % (type(error).__name__, error)]
    print("manifiesto relativo: exit %d" % code)
    if code != 0:
        failures.append("un manifiesto versionado por path relativo deberia salir 0: %s"
                        % output.strip())
    if "versionado" not in output:
        failures.append("el path relativo no resolvio al manifiesto versionado: %s"
                        % output.strip())

    work = fresh("verify-manifest-path")
    copy = work / "captures-v1.md"
    copy.write_bytes(real.read_bytes())
    try:
        code, output = capture(capture_diff.main,
                               ["--verify-manifest", os.path.relpath(copy, Path.cwd())])
    except BaseException as error:
        return failures + ["un manifiesto fuera del indice termino en %s (%s)"
                           % (type(error).__name__, error)]
    print("copia en Temp/: exit %d" % code)
    if code != 1:
        failures.append("un manifiesto fuera del indice deberia salir 1, salio %d: %s"
                        % (code, output.strip()))
    if "EVIDENCE-UNTRACKED" not in output:
        failures.append("la copia en Temp/ deberia fallar EVIDENCE-UNTRACKED: %s"
                        % output.strip())
    if "ValueError" in output:
        failures.append("el gate sigue nombrando un ValueError de paths: %s" % output.strip())
    return failures


def case_zlib_limit() -> list[str]:
    """DBT-3: la declaracion existe y el gate la consulta."""
    failures: list[str] = []
    work = fresh("zlib")
    limit = check.zlib_limit()
    print("zlib declarado: %d linea(s)" % limit)
    if limit < 1:
        failures.append("el limite de determinismo no esta declarado (0 coincidencias)")
    missing = work / "sin-zlib.md"
    missing.write_text("# nada\n", encoding="utf-8")
    if check.zlib_limit(missing) != 0:
        failures.append("el gate declararia un limite inexistente")
    return failures


def case_wiring_mono() -> list[str]:
    """CTR-4: `MonoBehaviour.freezeSprite` resuelve contra la procedencia, y el vaciado da FALTA."""
    failures: list[str] = []
    work = fresh("wiring-mono")
    piece = "ball_state_frozen_01.png"
    prefab = work / "FreezerMono.prefab"
    original = ROOT / "Assets" / "Prefabs" / "Ballzone" / "FreezerMono.prefab"
    prefab.write_bytes(original.read_bytes())
    relative = prefab.relative_to(ROOT).as_posix()
    map_path = work / "mapa.md"
    map_path.write_text(
        "```csv\n%s\n%s,serialized_sprite_field,%s,FreezerMono,MonoBehaviour,freezeSprite,"
        "cableado,-,-,\n```\n" % (",".join(wiring.COLUMNS), piece, relative), encoding="utf-8")
    guids = {piece: ledger_mod.provenance_index(ledger_mod.read_provenance())[piece]["guid"]}
    rows = wiring.read_map(map_path)
    problems, resolved = wiring.check_values(rows, guids)
    print("freezeSprite declarado: %d campo(s) resuelto(s), %d hallazgo(s)"
          % (resolved, len(problems)))
    if problems or resolved != 1:
        failures.append("el campo freezeSprite no resuelve contra el guid de la procedencia: %s"
                        % (problems or "0 campos medidos"))

    empty = work / "vaciado.prefab"
    text = original.read_text(encoding="utf-8")
    emptied = re.sub(r"(?m)^  freezeSprite: \{fileID: \d+, guid: [0-9a-f]{32}, type: 3\}$",
                     "  freezeSprite: {fileID: 0}", text, count=1)
    if emptied == text:
        failures.append("el prefab no declara freezeSprite: el caso no prueba nada")
    empty.write_text(emptied, encoding="utf-8")
    empty_map = work / "vaciado.md"
    empty_map.write_text(
        "```csv\n%s\n%s,serialized_sprite_field,%s,FreezerMono,MonoBehaviour,freezeSprite,"
        "cableado,-,-,\n```\n"
        % (",".join(wiring.COLUMNS), piece, empty.relative_to(ROOT).as_posix()),
        encoding="utf-8")
    problems, _ = wiring.check_values(wiring.read_map(empty_map), guids)
    print("slot vaciado: %d hallazgo(s) -> %s"
          % (len(problems), problems[0] if problems else "-"))
    if len(problems) != 1 or not problems[0].startswith("FALTA %s" % piece):
        failures.append("un freezeSprite vacio deberia dar un FALTA nombrando %s: %s"
                        % (piece, problems))

    real, _, counts, replace_rows = wiring.check_map(wiring.read_map())
    cohort = len(wiring.cohort_from_ledger())
    total = sum(counts.values())
    print("mapa real: %d = %d + %d + %d | REPLACE cableadas: %s"
          % (total, counts["cableado"], counts["sin_slot"], counts["bloqueado"],
             [row["pieza"] for row in replace_rows]))
    if real:
        failures.append("el mapa real no cierra: %s" % real)
    if total != cohort:
        failures.append("los buckets suman %d y el ledger declara %d piezas" % (total, cohort))
    if [row["pieza"] for row in replace_rows] != [piece]:
        failures.append("la unica REPLACE cableada deberia ser %s" % piece)
    return failures


CASES = {
    "fixture": case_fixture,
    "rect-stale": case_rect_stale,
    "ack": case_ack,
    "write-guard": case_write_guard,
    "delivered": case_delivered,
    "delivery-row": case_delivery_row,
    "excluded": case_excluded,
    "px-entrega": case_px_entrega,
    "legible": case_legible,
    "plate-decoded": case_plate_decoded,
    "tbd": case_tbd,
    "cohort-sum": case_cohort_sum,
    "cohort-manifest": case_cohort_manifest,
    "diff-allowlist": case_diff_allowlist,
    "verify-manifest-path": case_verify_manifest_path,
    "provenance": case_provenance,
    "replace-sha": case_replace_sha,
    "replace-guid": case_replace_guid,
    "orphan": case_orphan,
    "wiring-mono": case_wiring_mono,
    "zlib-limit": case_zlib_limit,
    "evidence": case_evidence,
    "capture-diff": case_capture_diff,
    "capture-manifest": case_capture_manifest,
    "capture-declared": case_capture_declared,
}


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description="Selftest de los gates de arte (Temp-only).")
    parser.add_argument("--only", help="casos separados por coma: %s" % ", ".join(CASES))
    args = parser.parse_args(argv)
    selected = list(CASES)
    if args.only:
        selected = [name.strip() for name in args.only.split(",") if name.strip()]
        unknown = [name for name in selected if name not in CASES]
        if unknown:
            print("ERROR: casos desconocidos: %s" % ", ".join(unknown), file=sys.stderr)
            return 2
    failures = 0
    for name in selected:
        print("CASE  %s" % name)
        try:
            problems = CASES[name]()
        except Exception as error:  # el selftest reporta, no oculta
            problems = ["excepcion: %s: %s" % (type(error).__name__, error)]
        if problems:
            failures += len(problems)
            for problem in problems:
                print("  FALLA  %s" % problem)
        else:
            print("  OK")
    print("RESULTADO   %s  (%d casos, %d hallazgos)"
          % ("PASS" if not failures else "FAIL", len(selected), failures))
    return 0 if not failures else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
