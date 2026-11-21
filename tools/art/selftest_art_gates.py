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
    tbd           DBT-4: una fila nombrada sale 1 (TBD-CAMPO); un §9 ilegible sigue saliendo 2
    cohort-sum    ART-3: una etiqueta inventada en el ledger falla COHORT-SUM con pieza
    zlib-limit    DBT-3: la declaracion existe y el gate la consulta (>= 1)

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
import shutil
import sys
from pathlib import Path

import art_ledger as ledger_mod
import audit_art_manifest as audit
import check_placeholder_wiring as wiring
import check_placeholders as check
import generate_placeholders as gen

ROOT = gen.REPO_ROOT
SANDBOX = ROOT / "Temp" / "art-selftest"
FIXTURE = ROOT / "tools" / "art" / "fixtures" / "rect-stale" / "mode2-1344x768.png.meta"
DEAD_BLOCK = ROOT / "tools" / "art" / "fixtures" / "rect-stale" / "mode1-residual-1344x768.png.meta"
PLAYER_AVATAR = ROOT / "Assets" / "Sprites" / "PlayerAvatar.png.meta"
PIECE = "ui_tutorial_dim_01.png"
RETRY = "ui_tutorial_btn_retry_normal_01.png"
BLOCKED = "ui_tutorial_spotlight_fill_01.png"


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
    "tbd": case_tbd,
    "cohort-sum": case_cohort_sum,
    "zlib-limit": case_zlib_limit,
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
