# `arte-trajeado-v1` — evidencia por slice

Branch `arte-trajeado-v1` · base `84968cf` · store engram (`sdd/arte-trajeado-v1/{proposal,spec,design,tasks}`)
Python 3.13.6 / Pillow 13.0.0 · **S1 es Unity-free**: los tres gates corren en CI sin el editor.

Cada sección pega el comando y su salida. El ledger y los PNG **no se escriben a mano**: lo que
sigue está transcrito de la corrida, no compuesto.

---

## §S1 — gates, estados y deuda cerrada (T-01…T-14)

### S1.1 El cohorte se deriva, no se declara (ART-3)

`EXPECTED_GENERATED = 33` deja de decidir: el gate deriva el cohorte del ledger y publica las
dos identidades. Una etiqueta fuera de las cinco falla `COHORT-SUM` nombrando pieza y etiqueta.

```
$ python tools/art/check_placeholders.py | grep -E "COHORT|RESULTADO"
COHORT      122 = 85 REPLACE + 37 NEW
COHORT-SUM  37 = 33 + 0 + 0 + 2 + 2
RESULTADO   PASS  (33 GENERATED, 4 bloqueadas; 0 DELIVERED, 0 EXCLUDED; 37 filas, 0 hallazgos)

$ python tools/art/audit_art_manifest.py | sed -n '1,4p'
MANIFIESTO  Docs/art/manifest-refresh-visual-v1.md
FILAS       122  (REPLACE 85 | NEW 37 | addresses 11)
LEDGER NEW  37 filas | 33 GENERATED, 0 DELIVERED, 0 EXCLUDED registradas
VIOLACIONES 4 en 2 reglas
```

Una fila `estado=ARTE` en una copia del ledger:

```
CASE  cohort-sum
estado inventado: 2 COHORT-SUM
  OK
```

### S1.2 Un arte entregado pasa por mérito, sin `--ack-sha` (ART-1)

Rehearsal real sobre el árbol de trabajo (después se revierte con `git checkout -- Docs/art`,
que es el rollback declarado del slice):

```
$ SHA=$(python -c "...art_ledger.sha256_of('Assets/Sprites/ui_tutorial_dim_01.png')")
sha disco = 84595179392c2754adbac6c6f6e53be180854cea518afc2485d66a8942b5e37c
$ printf 'ui_tutorial_dim_01.png,<sha>,rehearsal,2026-11-20\n' >> Docs/art/deliveries-v1.csv
$ python tools/art/generate_placeholders.py --deliver ui_tutorial_dim_01.png
DELIVERED   ui_tutorial_dim_01.png 84595179…b5e37c (ledger-only, 0 PNG)
$ echo $?
0

$ python tools/art/check_placeholders.py | tail -3
COHORT      122 = 85 REPLACE + 37 NEW
COHORT-SUM  37 = 32 + 1 + 0 + 2 + 2
RESULTADO   PASS  (32 GENERATED, 4 bloqueadas; 1 DELIVERED, 0 EXCLUDED; 37 filas, 0 hallazgos)
$ echo $?
0

$ python tools/art/audit_art_manifest.py --no-refs | grep -E "LEDGER NEW|VIOLACIONES|DELIVERY"
LEDGER NEW  37 filas | 32 GENERATED, 1 DELIVERED, 0 EXCLUDED registradas
VIOLACIONES 4 en 2 reglas
$ python tools/art/check_placeholder_wiring.py --plan | tail -1
RESULTADO   PASS  (0 hallazgos)
```

Dos cosas que la corrida deja claras:

1. La entrega **no introduce una violación nueva** en el auditor: el set sigue siendo las 4
   deudas declaradas (3 `FORMAT-JPG` + 1 `MODE-TARGET`). El `exit 0` literal de ART-1 en el
   auditor es inalcanzable mientras esas 4 deudas existan; el criterio honesto es "mismas 4,
   `DELIVERY-*` en 0", y eso es lo que se verifica.
2. `37 = 32 + 1 + 0 + 2 + 2` cierra sin tocar ningún literal: la transición de estado no
   rompe la aritmética.

Rollback ejecutado:

```
$ git checkout -- Docs/art
$ git status --porcelain -- Docs Assets
(empty)
$ wc -l Docs/art/deliveries-v1.csv
1 Docs/art/deliveries-v1.csv          # header-only otra vez
```

La negativa (misma pieza, sin fila de entrega):

```
CASE  delivery-row
--check sin fila de entrega: exit 1
  OK
```

### S1.3 `--ack-sha` ya no escribe bytes, y el tren de escritura tampoco puede pisar arte (ART-2)

```
CASE  ack
ack positivo: exit 0
ack sobre DELIVERED: exit 1        # DELIVERED-IMMUTABLE
ack con disco != render: exit 1    # ACK-REFUSED
  OK
```

El caso `write-guard` es una corrida de escritura **real** (no una lectura del código): escribe
33 PNG en `Temp/`, después alguien cambia un byte de un PNG (el arte llega) y el ledger sigue
declarando el placeholder. La corrida se niega y **no escribe un solo archivo**:

```
CASE  write-guard
primera corrida (ledger vacio = adopcion): exit 0, 33 PNG
FALLA  nada escrito: 1 hallazgos (0 archivos escritos)
segunda corrida (drift sin entrega): exit 1
archivos escritos: 0 de 34
  OK
```

`guard_write()` corre completo **antes del primer `write_bytes`**: el conteo `0 de 34` compara
el sha256 de los 34 archivos del sandbox antes y después de la corrida negada.

### S1.4 `px_entrega` es el gate, `px_actual` es histórico (CTR-1)

```
$ python tools/art/audit_art_manifest.py | grep "PX-ENTREGA-PENDING"
  PX-ENTREGA-PENDING  game_board_backdrop_01.png: header 1220x2712 == px_actual, px_entrega 921x2048 de sec.4 pendiente
  …
  PX-ENTREGA-PENDING  6/6: game_board_backdrop_01.png, ui_pass_banner_01.png, ui_pass_reward_avatar_01.png,
                       ui_pass_reward_avatar_02.png, ui_pass_reward_background_01.png, ui_pass_reward_background_02.png
```

Los cuatro veredictos de la regla, sobre la allowlist cerrada de 6:

```
CASE  px-entrega
pendientes reales: 6/6
veredictos: {'header en px_actual': 'pending', 'entregada en su px_entrega': 'ok',
             'header distinto de ambos': 'violation', 'septima fuera de la allowlist': 'violation'}
  OK
```

`PX-ACTUAL` se degradó a `PX-ACTUAL-DRIFT` (INFO): hoy no hay ninguna pieza en drift, así que
el baseline no se mueve por esta vía.

### S1.5 `RECT-STALE` mide el sprite vivo (CTR-2)

```
CASE  fixture
fixture mode2: 1 rect
PlayerAvatar spriteMode 1: 0 rect
  OK
CASE  rect-stale
real: 0 RECT-STALE, 4 violaciones
  OK
```

Las dos direcciones están probadas: el `.meta` real `PlayerAvatar.png.meta` (`spriteMode: 1`,
bloque muerto `271,71 746x631`) mide **0**; el fixture `tools/art/fixtures/rect-stale/mode2-1344x768.png.meta`
(`spriteMode: 2`, rect `321,249 10x28` sobre lienzo `1344x768`) mide **1**. La regla no quedó muda.

### S1.6 DBT-1 / DBT-2 / DBT-3 / DBT-4

```
CASE  legible
placa #1A1D26: 16.83:1
placa #6E6E6E: 5.10:1 (el numero cambio)
placa #AAAAAA: texto/placa 2.32:1, minimo 4.5 (texto #FFFFFF, placa #AAAAAA)
  OK
CASE  tbd
motivo generico: exit 1
px_entrega vacio: exit 1
sec.9 ilegible: exit 2
  OK
CASE  zlib-limit
zlib declarado: 1 linea(s)
  OK
```

`LEGIBLE` se mide en los píxeles decodificados del par texto/placa: cambiar la placa cambia el
número (16.83 → 5.10 → 2.32) y por debajo de 4.5 falla con el ratio a dos decimales y las dos
muestras RGB. `SURVIVE` corre en el `--check` canónico con los hosts del mapa:

```
$ python tools/art/check_placeholders.py | grep -cE "^SURVIVE"
20
$ python tools/art/check_placeholders.py | grep -c "n/a"
0
```

20 hosts = las 20 filas `cableado` del mapa. Tres de los tres dots no tienen `Image` y declaran
`1/1/1`; las dos piezas `image+tinte_design` (`dim`, `zone_veil`) declaran también `1/1/1`
porque su tinte oscuro **es** el efecto —la excepción que el propio mapa documenta— y el gate
imprime el host usado en cada línea. Un host forzado a `(0,0,0)` sigue fallando `SURVIVE`
(caso cubierto por `write-guard`/`--hosts` override en el selftest de esta misma corrida).

### S1.7 Baseline del auditor: 14 → 4, y por qué

| Regla | Antes de S1 | Después de S1 | Por qué se movió |
|---|---|---|---|
| `FORMAT-JPG` | 3 | 3 | deuda declarada (3 orígenes JPEG): no se toca en S1 |
| `MODE-TARGET` | 1 | 1 | deuda declarada (`ball_shadow_01`): la arregla el migrador |
| `RECT-STALE` | 10 | **0** | los 10 eran el bloque `spriteSheet` de `spriteMode: 1`, que no dibuja (CTR-2). El bloque residual lo borra S1b; la regla queda armada y el fixture pruebe que dispara |
| **Total** | **14** | **4** | |

Los 9 `.meta` con bloque residual **siguen** teniendo el bloque: S1 no los toca (es el trabajo
Unity-bound de S1b). Lo que cambió es qué se mide.

### S1.8 Corrida completa del selftest

```
$ python tools/art/selftest_art_gates.py
CASE  fixture … OK
CASE  rect-stale … OK
CASE  ack … OK
CASE  write-guard … OK
CASE  delivered … OK
CASE  delivery-row … OK
CASE  excluded … OK
CASE  px-entrega … OK
CASE  legible … OK
CASE  tbd … OK
CASE  cohort-sum … OK
CASE  zlib-limit … OK
RESULTADO   PASS  (12 casos, 0 hallazgos)
$ echo $?
0
```

Todo el selftest escribe **sólo** bajo `Temp/art-selftest/` (ignorado por `.gitignore:6`); el
sandbox copia los `.meta` reales para que el gate de GUID mida el árbol de entrega real. Al
terminar, `git status --porcelain -- Docs Assets` está vacío.

### S1.9 Notas de implementación (desvíos declarados)

- `hosts_from_map()` vive en `check_placeholder_wiring.py`, no en `art_ledger.py` (T-11): el
  walker de YAML (`node_paths`/`component_docs`) ya vive ahí y moverlo duplicaba el parser.
  `art_ledger.py` queda sin PyYAML, como pide T-01.
- `classify()` no se niega a una `DELIVERED` sin fila de entrega: la reporta `--check` como
  `DELIVERY-ROW` con pieza y sha, que es exactamente lo que pide el escenario de ART-1. El
  camino de regeneración sigue protegido porque `ledger_text()` escribe las filas
  `DELIVERED`/`EXCLUDED` verbatim y `guard_write()` protege los bytes.
- `collisions()` sólo compara contra `Assets/` cuando el destino **es** `Assets/Sprites`: una
  corrida de ensayo en `Temp/` no entrega y no puede colisionar con lo ya entregado.
