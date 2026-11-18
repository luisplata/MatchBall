#!/usr/bin/env python3
"""Verifica que el AAB publicado NO exija la feature de orientacion bloqueada.

Google Play deduce (``implied feature``) que la app REQUIERE
``android.hardware.screen.portrait`` cuando alguna ``<activity>`` fija
``android:screenOrientation="portrait"``. Si esa feature queda como requerida,
Play filtra la app de tablets, PC, Google Play Games on PC y Android XR, y
Play Console rechaza el envio a revision con "Declaraciones de funciones de
hardware obligatorias restringen la disponibilidad".

La unica forma documentada de desactivar esa deduccion es declarar la feature
explicitamente con ``android:required="false"``:

    <uses-feature android:name="android.hardware.screen.portrait"
                  android:required="false" />

Este script verifica el manifiesto FUSIONADO que viaja dentro del AAB
(``base/manifest/AndroidManifest.xml``), que es exactamente lo que Play lee.

Dos cosas que el AAB real hace distinto de lo que uno espera:

* El manifiesto NO es texto: aapt2 lo guarda como protobuf (``aapt.pb.XmlNode``)
  con los strings inline, no en un pool. Por eso se decodifica el protobuf en
  vez de buscar bytes: un ``grep`` del nombre de la feature encuentra el string
  si esta declarada, pero NO puede distinguir ``required="false"`` de
  ``required="true"`` ni detectar el caso implicado (feature ausente).
* El caso peligroso no es "la feature esta escrita": es "la feature esta
  requerida o implicada". Un assert de ausencia pura daria verde justamente en
  el manifiesto que Play rechaza (comprobado contra el AAB de la 0.3.1).

Uso:
    python tools/android/verify_aab_orientation_feature.py --aab release/MatchBall-0.3.2.aab
    python tools/android/verify_aab_orientation_feature.py --aab otro.aab --dump

Exit code: 0 = PASS / 1 = la feature queda requerida / 2 = no se pudo leer el AAB.
"""

from __future__ import annotations

import argparse
import sys
import zipfile
from pathlib import Path

MANIFEST_ENTRY = "base/manifest/AndroidManifest.xml"

ANDROID_NS = "http://schemas.android.com/apk/res/android"

# Features de orientacion que Play trata como requeridas, y el set de valores de
# android:screenOrientation que las implica.
ORIENTATION_FEATURES = {
    "android.hardware.screen.portrait": "portrait",
    "android.hardware.screen.landscape": "landscape",
}

# Valores de android:screenOrientation que NO fijan una orientacion unica.
NEUTRAL_ORIENTATIONS = {
    "unspecified",
    "behind",
    "sensor",
    "fullSensor",
    "user",
    "fullUser",
    "nosensor",
    "locked",
}

ACTIVITY_ELEMENTS = {"activity", "activity-alias"}


class ManifestError(Exception):
    """No se pudo decodificar el manifiesto del AAB."""


def _varint(buf: bytes, i: int) -> tuple[int, int]:
    shift = 0
    value = 0
    while True:
        if i >= len(buf):
            raise ManifestError("varint truncado")
        byte = buf[i]
        i += 1
        value |= (byte & 0x7F) << shift
        if not byte & 0x80:
            return value, i
        shift += 7
        if shift > 63:
            raise ManifestError("varint demasiado largo")


def _fields(buf: bytes) -> list[tuple[int, int, object]]:
    """Decodifica un mensaje protobuf en (numero de campo, wire type, valor)."""
    out: list[tuple[int, int, object]] = []
    i = 0
    n = len(buf)
    while i < n:
        tag, i = _varint(buf, i)
        number, wire = tag >> 3, tag & 7
        if number == 0:
            raise ManifestError("campo protobuf 0")
        if wire == 0:
            value, i = _varint(buf, i)
            out.append((number, wire, value))
        elif wire == 2:
            length, i = _varint(buf, i)
            if i + length > n:
                raise ManifestError("campo length-delimited truncado")
            out.append((number, wire, buf[i : i + length]))
            i += length
        elif wire == 5:
            if i + 4 > n:
                raise ManifestError("fixed32 truncado")
            out.append((number, wire, buf[i : i + 4]))
            i += 4
        elif wire == 1:
            if i + 8 > n:
                raise ManifestError("fixed64 truncado")
            out.append((number, wire, buf[i : i + 8]))
            i += 8
        else:
            raise ManifestError("wire type no soportado: %d" % wire)
    return out


def _text(raw: object) -> str | None:
    """Devuelve el string si el campo es texto plano UTF-8, si no ``None``."""
    if not isinstance(raw, bytes):
        return None
    try:
        text = raw.decode("utf-8")
    except UnicodeDecodeError:
        return None
    for char in text:
        if ord(char) < 0x20 and char not in "\t\n\r":
            return None
    return text


def _attributes(message: list[tuple[int, int, object]]) -> dict[str, str] | None:
    """Interpreta el mensaje como lista de atributos (``f2``=nombre, ``f3``=valor).

    Devuelve ``None`` si alguna entrada con forma de atributo no lo es, para no
    confundir un ``XmlAttribute`` (que tambien tiene ``f2``/``f3``) con un
    elemento.
    """
    attributes: dict[str, str] = {}
    seen = False
    for number, wire, raw in message:
        if number != 4 or wire != 2:
            continue
        try:
            inner = _fields(raw)  # type: ignore[arg-type]
        except ManifestError:
            continue
        name = value = None
        for inner_number, inner_wire, inner_raw in inner:
            if inner_wire != 2:
                continue
            if inner_number == 2:
                name = _text(inner_raw)
            elif inner_number == 3:
                value = _text(inner_raw)
        if name is None or value is None:
            return None
        seen = True
        attributes[name] = value
    return attributes if seen else None


def _walk(buf: bytes, elements: list[tuple[str, dict[str, str]]]) -> None:
    """Recorre el protobuf y acumula ``(nombre, atributos)`` de cada elemento."""
    try:
        message = _fields(buf)
    except ManifestError:
        return
    name = None
    for number, wire, raw in message:
        if number == 3 and wire == 2:
            name = _text(raw)
    attributes = _attributes(message)
    if name is not None and attributes is not None:
        elements.append((name, attributes))
    for number, wire, raw in message:
        if wire == 2 and isinstance(raw, bytes):
            _walk(raw, elements)


def read_manifest(aab: Path) -> bytes:
    try:
        with zipfile.ZipFile(aab) as archive:
            return archive.read(MANIFEST_ENTRY)
    except KeyError as error:
        raise ManifestError("el AAB no tiene %s" % MANIFEST_ENTRY) from error
    except zipfile.BadZipFile as error:
        raise ManifestError("el AAB no es un zip valido: %s" % error) from error


def _orientation_kind(value: str) -> str | None:
    """Traduce un valor de ``android:screenOrientation`` a portrait/landscape."""
    if value in NEUTRAL_ORIENTATIONS:
        return None
    lowered = value.lower()
    if "portrait" in lowered:
        return "portrait"
    if "landscape" in lowered:
        return "landscape"
    return None


def analyse(elements: list[tuple[str, dict[str, str]]]) -> dict[str, object]:
    """Resume que features de orientacion estan declaradas y cuales implicadas."""
    declared: dict[str, str] = {}
    implied: dict[str, list[str]] = {}

    for name, attributes in elements:
        if name == "uses-feature":
            feature = attributes.get("name")
            if feature in ORIENTATION_FEATURES:
                declared[feature] = attributes.get("required", "<ausente>")
        elif name in ACTIVITY_ELEMENTS:
            orientation = attributes.get("screenOrientation")
            if orientation is None:
                continue
            kind = _orientation_kind(orientation)
            if kind is None:
                continue
            activity = attributes.get("name", "<activity sin nombre>")
            implied.setdefault(kind, []).append("%s=%s" % (activity, orientation))

    problems: list[str] = []
    for feature, kind in ORIENTATION_FEATURES.items():
        required = declared.get(feature)
        if required is None:
            if kind in implied:
                problems.append(
                    "FALLA: %s NO esta declarada como no requerida, y alguna activity "
                    "la implica: %s" % (feature, ", ".join(implied[kind]))
                )
        elif required != "false":
            problems.append(
                'FALLA: %s esta declarada con required="%s" (debe ser "false")'
                % (feature, required)
            )

    return {
        "declared": declared,
        "implied": implied,
        "problems": problems,
        "uses_features": sorted(
            attributes.get("name")
            or "glEsVersion=%s" % attributes.get("glEsVersion", "?")
            for name, attributes in elements
            if name == "uses-feature"
        ),
    }


def print_report(report: dict[str, object], exit_code: int) -> None:
    print("Features de hardware declaradas: %s" % ", ".join(report["uses_features"]))  # type: ignore[arg-type]
    declared: dict[str, str] = report["declared"]  # type: ignore[assignment]
    if declared:
        for feature, required in sorted(declared.items()):
            print("  declarada: %s required=%s" % (feature, required))
    else:
        print("  ninguna feature de orientacion declarada")

    implied: dict[str, list[str]] = report["implied"]  # type: ignore[assignment]
    for kind, sources in sorted(implied.items()):
        print("  implicada: %s (%s)" % (ORIENTATION_FEATURES_BY_KIND[kind], "; ".join(sources)))

    problems: list[str] = report["problems"]  # type: ignore[assignment]
    if problems:
        for problem in problems:
            print(problem)
        print(
            "\nQue significa: Google Play deduce que la app REQUIERE esa orientacion y\n"
            "filtra la disponibilidad (tablets, PC, Google Play Games on PC, Android XR).\n"
            "Play Console rechaza el envio a revision con 'Declaraciones de funciones de\n"
            "hardware obligatorias restringen la disponibilidad'."
        )
        print(
            "\nComo se arregla: en Assets/Plugins/Android/AndroidManifest.xml, antes de\n"
            "<application>, declarar la feature como opcional y volver a compilar:\n"
            '\n  <uses-feature android:name="android.hardware.screen.portrait"\n'
            '                android:required="false" />\n'
        )
        print("FALLA: el AAB publicado restringe la disponibilidad por orientacion.")
    else:
        print("OK: sin features de hardware obligatorias de orientacion")


ORIENTATION_FEATURES_BY_KIND = {kind: feature for feature, kind in ORIENTATION_FEATURES.items()}


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Verifica que el AAB no exija android.hardware.screen.portrait/landscape"
    )
    parser.add_argument("--aab", required=True, type=Path, help="ruta al .aab a revisar")
    parser.add_argument(
        "--dump",
        action="store_true",
        help="imprime todos los elementos del manifiesto fusionado (diagnostico)",
    )
    return parser


def main(argv: list[str]) -> int:
    args = build_parser().parse_args(argv)
    if not args.aab.is_file():
        print("FALLA: no existe el AAB: %s" % args.aab, file=sys.stderr)
        return 2

    try:
        raw = read_manifest(args.aab)
        elements: list[tuple[str, dict[str, str]]] = []
        _walk(raw, elements)
        if not elements:
            raise ManifestError("no se decodifico ningun elemento del manifiesto")
        report = analyse(elements)
    except ManifestError as error:
        print("FALLA: no se pudo leer %s: %s" % (MANIFEST_ENTRY, error), file=sys.stderr)
        return 2

    if args.dump:
        print("Elementos del manifiesto fusionado (%d):" % len(elements))
        for name, attributes in elements:
            print("  <%s> %s" % (name, attributes))

    exit_code = 1 if report["problems"] else 0
    print_report(report, exit_code)
    return exit_code


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
