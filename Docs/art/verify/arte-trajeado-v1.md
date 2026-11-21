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
(Tras S1b el bloque real desapareció: la dirección "mode 1 → 0" pasa a probarse con
`mode1-residual-1344x768.png.meta`; ver §S1b.2.)

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

## §S1b — los 10 rects muertos, borrados por el migrador (T-15…T-17)

`size:exception` declarado: los 9 `.meta` son **salida del serializer de Unity**, no código
revisable línea a línea. Se acepta por muestreo + gate propio. Los 33 PNG NEW no se tocaron:
`git status --porcelain -- "*.png"` vacío antes y después.

### S1b.1 La operación y su plan (T-15 / T-16)

`ArtMetaMigrator.BuildPlan` gana `sheet-clear` cuando `ModeNow == ModeTarget == 1 && staleRects`
(la pieza ya está en Single y el `.meta` todavía serializa sub-sprites). En Single el importer no
expone las rects y el data provider 2D sólo escribe en Multiple, así que
`ClearSheetSprites()` es un round-trip: **Multiple → `SetSpriteRects(Array.Empty<SpriteRect>())`
→ Single → `SaveAndReimport`**. La verificación **re-parsea** el `.meta`
(`SheetSpriteCount` + `SheetHasStaleRect`): una sola entrada que sobreviva es
`RECT-CLEAR-FAILED`, `LogError`, y el apply sale **4** — nunca un log verde. Los planes con
`sheet-clear` se excluyen de `StartAssetEditing`, porque con la importación diferida el re-parse
leería el bloque viejo y daría un fallo falso.

Dry-run sobre el árbol real — `python tools/art/audit_art_manifest.py --plan` (espejo de sólo
lectura, 0 escrituras) y `ArtMetaMigrator.DryRun` en el editor abierto. **10 ops sobre 9 `.meta`**:

```
PLAN  game_board_shadow_mid_01.png
      asset   Assets/Sprites/V3_images/sombra 2.png  (guid fe1e615ea23596446a79254f62873b1a)
      op1    sheet-clear    rect "sombra 2_0" 105,2 159x165 -> bloque vaciado (2 -> 0 rects -> 1)

PLAN  ui_menu_avatar_player_01.png
      asset   Assets/Sprites/PlayerAvatar.png  (guid 80793527c4e2c0843a5ef10a262777f6)
      op1    sheet-clear    rect "PlayerAvatar_0" 271,71 746x631 -> bloque vaciado (2 -> 0 rects -> 1)

PLAN  ui_menu_avatar_npc_01.png
      asset   Assets/Sprites/NpcAvatar.png  (guid 996c402736a5ed84ebf2d7f809bef329)
      op1    sheet-clear    rect "NpcAvatar_0" 63,155 905x675 -> bloque vaciado (2 -> 0 rects -> 1)

PLAN  hero_rojo_portrait_idle_01.png
      asset   Assets/Sprites/Heroes/rojo_idle.png  (guid c385d6896bfd3e04e919ebc124d6b4ee)
      op1    sheet-clear    rect "rojo_idle_0" 175,0 716x1024 -> bloque vaciado (2 -> 0 rects -> 1)

PLAN  hero_rojo_portrait_happy_01.png
      asset   Assets/Sprites/Heroes/rojo_alegre.png  (guid 7c968f1d484f63d4d97eba7f28d1fb45)
      op1    sheet-clear    rect "rojo_alegre_0" 40,0 723x1024 -> bloque vaciado (2 -> 0 rects -> 1)

PLAN  hero_rojo_portrait_angry_01.png
      asset   Assets/Sprites/Heroes/rojo_enojo.png  (guid 3617c5dfa6f6e9a4f910347fbaa75fa1)
      op1    sheet-clear    rect "rojo_enojo_0" 88,0 925x1024 -> bloque vaciado (2 -> 0 rects -> 1)

PLAN  hero_rojo_portrait_determined_01.png
      asset   Assets/Sprites/Heroes/rojo_determinacion.png  (guid 7eff919c860052f498742cbaa5faecb4)
      op1    sheet-clear    rect "rojo_determinacion_0" 24,0 924x1009 -> bloque vaciado (2 -> 0 rects -> 1)

PLAN  hero_rojo_portrait_surprised_01.png
      asset   Assets/Sprites/Heroes/rojo_sorpresa.png  (guid c275f44f572fa0e4ea521a65c2eff1be)
      op1    sheet-clear    rect "rojo_sorpresa_0" 177,0 606x1024 -> bloque vaciado (2 -> 0 rects -> 1)

PLAN  hero_rojo_banner_01.png
      asset   Assets/Sprites/Heroes/HeroeRojo.png  (guid aa48d609dc0280e47bcb8867e0f45604)
      op1    sheet-clear    rect "HeroeRojo_0" 321,249 10x28 -> bloque vaciado (2 -> 0 rects -> 1)
      op2    sheet-clear    rect "HeroeRojo_1" 326,0 696x768 -> bloque vaciado (2 -> 0 rects -> 1)

TOTAL 9 piezas con operaciones planificadas | 0 ocurrencias de fileID a remapear
MODO  dry-run: 0 escrituras (el unico que escribe es ArtMetaMigrator con apply=true)
```

9 piezas, 10 ops ( `hero_rojo_banner_01` acumula dos), 0 refs: este op no cambia el modo, así
que no hay `fileID-remap`. `ball_shadow_01` (el `MODE-TARGET` de la deuda) sigue en el plan de
`all` y **no** se aplicó aquí: es S3/su propia corrida.

### S1b.2 Aplicación y cierre (T-17)

Aplicado por MCP contra el editor abierto (`DarkVoid.ArtContract.ArtMetaMigrator.Apply`), no por
`-batchmode` (el proyecto ya está abierto y lockea la instancia). Glob real:

```
game_board_shadow_mid_01*,ui_menu_avatar_player_01*,ui_menu_avatar_npc_01*,hero_rojo_*
```

Desvío declarado: el comando literal de T-17 (`game_board_shadow_mid_01,ui_menu_avatar_player_01,
ui_menu_avatar_npc_01,hero_rojo_*`) selecciona **6** piezas y no 9, porque `BuildGlob` compara
contra `Pieza` **con** extensión (`.png`) y sólo los términos con `*` matchean; los tres términos
sin comodín no resolvían. Se agregó `*` a cada término. Nada más cambió en la selección.

Prueba de cierre — las tres aserciones:

```
$ git status --porcelain -- Assets      # sólo los 9 .meta, cero PNG
 M Assets/Sprites/Heroes/HeroeRojo.png.meta
 M Assets/Sprites/Heroes/rojo_alegre.png.meta
 M Assets/Sprites/Heroes/rojo_determinacion.png.meta
 M Assets/Sprites/Heroes/rojo_enojo.png.meta
 M Assets/Sprites/Heroes/rojo_idle.png.meta
 M Assets/Sprites/Heroes/rojo_sorpresa.png.meta
 M Assets/Sprites/NpcAvatar.png.meta
 M Assets/Sprites/PlayerAvatar.png.meta
 M "Assets/Sprites/V3_images/sombra 2.png.meta"

$ git diff --stat -- Assets
 9 files changed, 9 insertions(+), 229 deletions(-)
```

El diff es exactamente el bloque: se borran las entradas `sprites:` y se agrega `sprites: []`.
`guid`, `spriteMode`, `spriteBorder`, `spritePivot`, `spritePixelsToUnits`, `maxTextureSize` y
`internalIDToNameTable` no se mueven — ninguna referencia puede quedar colgada, porque el
`fileID` vivo (`21300000`) es el mismo antes y después.

```
$ sed -n '/spriteSheet:/,/mipmapLimitGroupName/p' <cada uno de los 9 .meta> | head -3
  spriteSheet:
    serializedVersion: 2
    sprites: []        # <- cero entradas en los 9
```

**Corrección al criterio de T-17**: `grep -c spriteSheet` **no** puede dar `0` en Unity 6. El
serializer escribe siempre la clave en Single, con `sprites: []`. Prueba de que no es una
regresión de este slice: `Assets/Sprites/BALLPASS/BALLPASS9.png.meta` —que nunca tuvo bloque—
también da `grep -c spriteSheet` = 1. El criterio verificable (y el que se usa) es **cero
entradas `sprites[]`**, que es lo que mide `SheetSpriteCount()` y lo que el re-parse del
migrador exige para no emitir `RECT-CLEAR-FAILED`.

Contadores del auditor, **antes → después**:

| Regla | Antes de S1b | Después de S1b |
|---|---|---|
| `FORMAT-JPG` | 3 | 3 |
| `MODE-TARGET` | 1 | 1 |
| `RECT-STALE` | **0** (S1 ya mide el sprite vivo) | **0** |
| **Total** | **4** | **4** |

El total **no se movió**, y eso es lo correcto: S1 ya había bajado 14 → 4 midiendo el sprite
vivo. S1b no "arregla" una violación contada; evita que vuelva. La prueba de que la regla sigue
viva es el fixture:

```
CASE  fixture
fixture mode2: 1 rect
fixture mode1 con 1 sprite(s) serializado(s): 0 rect vivo
PlayerAvatar spriteMode 1: 0 rect, 0 sprite(s) serializado(s)
  OK
CASE  rect-stale
real: 0 RECT-STALE, 4 violaciones
  OK
RESULTADO   PASS  (12 casos, 0 hallazgos)
```

El `spriteMode: 2` sigue midiendo **1**; el `spriteMode: 1` con bloque serializado sigue midiendo
**0** (ahora contra `mode1-residual-1344x768.png.meta`, porque el bloque real ya no existe); el
árbol real afirma 0 entradas. Los otros dos gates, sin cambio:
`check_placeholders.py` PASS `33 GENERATED, 4 bloqueadas` · `check_placeholder_wiring.py --plan`
PASS `20 cableado, 13 sin_slot, 4 bloqueado` · `audit_art_manifest.py` exit 1 con **4**.

### S1b.3 Riesgos que quedan

- `rect-resync` (el op de las piezas que **sí** quedan en Multiple) usa `TextureImporter.spritesheet`,
  que en Unity 6 compila con `CS0618` y el propio warning dice que el acceso "has been removed".
  `sheet-clear` usa el data provider (camino correcto); el de `rect-resync` puede ser un no-op
  silencioso y merece su propio chequeo cuando toque una pieza mode 2.
- El re-parse es la única prueba de que el bloque murió. Si una versión futura de Unity
  serializara el bloque desde otro estado, `SheetSpriteCount` lo ve igual (parsea el YAML).

## §S2 — la procedencia de las REPLACE, congelada (T-18…T-23)

Cierra PRV-1…PRV-4 sin tocar Unity ni un byte de arte. La pregunta que S2 vuelve contestable es
una sola: **"¿esta pieza se reemplazó?"**, y la contesta para cada una de las REPLACE que hoy
declara §9 (**85**; la 86 llega en S3 y este slice no la hardcodea).

### S2.0 Dos archivos, no uno (la decisión que sostiene todo lo demás)

`placeholders-v1.csv` sigue siendo **derivado**: `generate_placeholders.ledger_text()` lo
reescribe entero desde disco en cada corrida. Un sha de REPLACE guardado ahí se **auto-sana**, y
`REPLACE-SHA` no podría observar nunca un reemplazo: el gate diría "todo baseline" para siempre.
Por eso el baseline vive en `Docs/art/provenance-v1.csv`, un archivo aparte que **nadie
reescribe**:

| Archivo | Quién escribe | Qué dice | Mutabilidad |
|---|---|---|---|
| `placeholders-v1.csv` | `generate_placeholders.py` (derivado de §9) | cohorte NEW: estado, px, guid, sha | regenerable |
| `deliveries-v1.csv` | humano | quién entregó, cuándo | manual |
| `provenance-v1.csv` | `audit_art_manifest.py --capture-provenance` | qué bytes son el baseline de cada REPLACE | **one-shot** |

Esquema: `pieza,origen,guid,sha256,px_actual,px_entrega,rol,motivo` (86 líneas = encabezado +
85 filas). `px_actual` es el header **medido** el día de la captura (un hecho, no el eco de §9);
`px_entrega` es el valor de §9, que es el que manda (CTR-1).

### S2.1 La captura es una sola vez (T-18)

```
$ python tools/art/audit_art_manifest.py --capture-provenance
CAPTURE      Docs/art/provenance-v1.csv
FILAS        85 | 83 guid distintos, 2 guid compartidos por el mismo origen
SIN PANTALLA 4 REPLACE sin pantalla: game_board_balance_base_01.png, game_board_shadow_long_01.png, ui_common_divider_01.png, ui_pass_state_lock_01.png
RESULTADO    PASS  (baseline congelado; el gate no vuelve a correr esto)

$ python tools/art/audit_art_manifest.py --capture-provenance
CAPTURE-IMMUTABLE  Docs/art/provenance-v1.csv ya existe: la procedencia se congela una sola vez
             un gate que puede re-basar su baseline no mide nada; para una pieza ausente usar --add PIEZA (append-only)

$ python tools/art/audit_art_manifest.py --capture-provenance --add ui_common_divider_01
ADD-REFUSED  ui_common_divider_01.png ya tiene fila en provenance-v1.csv: append-only no reescribe
```

`--add PIEZA` es la única excepción (append-only) y existe para la fila 86 de S3. La captura
también **se niega a escribir** si lo medido no es exactamente el conjunto declarado de huérfanas
(`CAPTURE-ORPHAN`): el baseline se congela sobre una decisión revisada, no sobre lo que salga.

### S2.2 Ningún conteo NEW se movió (T-21)

Medido sobre el árbol de HEAD (`git stash` de `tools/art`, misma máquina, mismos assets) y sobre
el árbol de este slice. **Idéntico**:

| Línea | Antes (HEAD) | Después (S2) |
|---|---|---|
| `FILAS` | `122 (REPLACE 85 / NEW 37 / addresses 11)` | igual |
| `LEDGER NEW` | `37 filas / 33 GENERATED, 0 DELIVERED, 0 EXCLUDED` | igual |
| `COHORT` | `122 = 85 REPLACE + 37 NEW` | igual |
| `COHORT-SUM` | `37 = 33 + 0 + 0 + 2 + 2` | igual |
| `NEW-LEDGER` | `37 filas … 33 guid congelados` | igual |
| `PX-ENTREGA-PENDING` | `6/6` | igual |
| `REFERENCIAS` | `172 referencias en 28 archivos` | igual |
| `VIOLACIONES` | `4 en 2 reglas` (3 `FORMAT-JPG` + 1 `MODE-TARGET`) | igual |
| `check_placeholders.py` | PASS `33 GENERATED, 4 bloqueadas; 37 filas` | igual |
| `check_placeholder_wiring.py` | PASS `20 cableado, 13 sin_slot, 4 bloqueado` | igual |
| `selftest_art_gates.py` | PASS 12 casos | PASS **16** casos |

Lo único que aparece es lo que S2 agrega:

```
PROCEDENCIA 85 filas REPLACE | 0/85 reemplazadas | 4 sin pantalla
  REPLACE-LEDGER  85/85 filas con pieza+origen+guid+sha256+px (83 guid distintos)
  REPLACE-SHA  0/85 reemplazadas (85 BASELINE, 0 DELIVERED, 0 UNKNOWN)
  REPLACE-GUID  2 filas comparten guid por declarar el mismo origen (un archivo, dos roles): no es colision
  ORPHAN      4 REPLACE sin pantalla: game_board_balance_base_01.png, game_board_shadow_long_01.png, ui_common_divider_01.png, ui_pass_state_lock_01.png
```

**Hallazgo declarado, no escondido**: dos pares de filas comparten GUID porque **declaran el mismo
origen** — `ui_hud_btn_powerup_slot_{normal,active}_01` y `ui_menu_tab_powerups_{normal,active}_01`
apuntan ambos a `PowerUpsNormal.png` / `PowerUpsClicked.png` (§9 lo dice por escrito: "un archivo,
dos roles"). No es una colisión: la atribución es inequívoca. Sí es colisión —y falla
`REPLACE-GUID`— que **dos orígenes distintos** reclamen el mismo guid, o que un guid de REPLACE
aparezca en el cohorte NEW. El gate cuenta 83 guid distintos en 85 filas y lo imprime.

### S2.3 Los tres estados de `REPLACE-SHA` (T-19 / T-22b)

`BASELINE` (bytes == ledger), `DELIVERED` (bytes ≠ ledger **y** fila con ese sha en
`deliveries-v1.csv`) y `UNKNOWN` (bytes ≠ ledger sin declaración). Solo el tercero falla.

```
# (a) sha vacío en una fila
[REPLACE-LEDGER] ui_common_btn_primary_normal_01.png: 'sha256' vacio (cobertura 84/85)
[REPLACE-SHA]    ui_common_btn_primary_normal_01.png: UNKNOWN: ledger (vacio) != disco 489d7ac9… y deliveries-v1.csv no declara ese sha

# (b) bytes cambiados sin declaración
[REPLACE-SHA] ui_common_btn_primary_normal_01.png: UNKNOWN: ledger 0000…0000 != disco 489d7ac9… y deliveries-v1.csv no declara ese sha
  INFO REPLACE-SHA  0/85 reemplazadas (84 BASELINE, 0 DELIVERED, 1 UNKNOWN)

# (c) la misma pieza, declarada
  INFO REPLACE-SHA  1/85 reemplazadas (84 BASELINE, 1 DELIVERED, 0 UNKNOWN)
  → 4 violaciones (el baseline no se movió)
```

Los negativos corren sobre copias en `Temp/art-selftest/`; `Docs/` y `Assets/` quedan
byte-idénticos (`git status --porcelain` vacío al cerrar cada caso).

### S2.4 El GUID sobrevive al swap (PRV-3)

El swap reemplaza **bytes**; el `.meta` viaja intacto y con él el guid, que es lo que las 172
referencias de 28 archivos nombran. La prueba es falsable en las dos direcciones:

```
CASE  replace-guid
barrido real: 172 referencias en 28 archivos
tras simular el swap: 172 referencias en 28 archivos
guid nuevo: 1 REPLACE-GUID -> ledger 0123456789abcdef0123456789abcdef != .meta 8febf562d93d29f489aa842db7c78244 (un reimport que cambia el guid no es un swap: quema las referencias)
```

Simular el swap = declarar otro baseline sha + su fila en `deliveries-v1.csv`; el barrido no se
mueve un dígito. Un `.meta` con guid nuevo falla nombrando los dos valores y **no** se acepta como
"reimport".

### S2.5 Las 4 huérfanas (PRV-4)

El barrido que decide "0 consumidores" es **más ancho** que el de referencias: una entry de
Addressables declara `m_GUID:` sin `fileID`, y `m_PressedSprite` de un Button también lleva el
GUID. Contar solo `fileID: N, guid: G` habría declarado huérfanas **6** piezas que sí se dibujan
(4 addresses del pase + 2 slots de power-up). Con el barrido por mención de GUID, `_Recovery/`
excluido, la medición da **exactamente** el conjunto de PRV-4. Cada una con `rol=sin_pantalla` y
su motivo en el CSV:

| Pieza | Por qué no tiene consumidor hoy |
|---|---|
| `game_board_balance_base_01` | la barra se arma con `balance_bar_left/right_01`; ningún prefab dibuja la base |
| `game_board_shadow_long_01` | la sombra viva es `game_board_line_shadow_01` |
| `ui_pass_state_lock_01` | el panel de pase no existe en `Game.unity` |
| `ui_common_divider_01` | `Separador.png` entra al manifiesto sin slot |

```
[ORPHAN-UNDECLARED] ui_common_divider_01.png: REPLACE con 0 consumidores y rol != sin_pantalla
[ORPHAN-STALE]      ui_common_btn_primary_normal_01.png: declarada sin pantalla y hoy tiene 2 consumidor(es): Assets/Scenes/Game.unity, Assets/Scenes/MainMenu.unity
```

Ninguna de las 4 cuenta como cobertura: el auditor imprime `4 REPLACE sin pantalla` como línea
propia, y el cohorte NEW no las toca.

### S2.6 Riesgos y deudas que este slice deja abiertas

- **La captura es un hecho, no una verdad eterna**: si un origen cambia de `.meta` (no de bytes),
  `REPLACE-GUID` lo va a acusar; si cambia de bytes sin entrega, `REPLACE-SHA`. Eso es lo buscado.
  Lo que **no** puede pasar es que alguien re-base: la única salida es borrar el CSV, y eso es un
  diff visible con review.
- **`--add` sobre una pieza que §9 no declara REPLACE** sale `ADD-REFUSED`: no hay forma de meter
  en el baseline una pieza que el contrato no nombra.
- **`px_actual` del baseline es histórico**: no es un gate (para eso está `px_entrega`). Si el arte
  de §4 se entrega, el baseline no se mueve: se mueve el header.
- **S3 hereda el denominador**: `REPLACE-LEDGER`, `REPLACE-SHA` y `ORPHAN-*` derivan `R` de §9 y de
  la cantidad de filas del CSV. Cuando `ball_state_frozen_01` entra como REPLACE, la fila 86 se
  appendea con `--capture-provenance --add ball_state_frozen_01` y nada más de este slice cambia.

## §S3 — `ball_state_frozen_01` resuelve su slot (T-24…T-29)

Cierra CTR-3 y CTR-4 sin tocar Unity y sin escribir un byte de arte. La afirmación que este
slice vuelve medible es una sola: **`ball_state_frozen_01` estaba mal clasificada**. No era una
pieza nueva bloqueada por diseño: era el reemplazo de `Assets/Sprites/iceBall.png` en un slot
que existe, está cableado y se instancia dos veces.

La cadena, leída del YAML y no de la prosa:

```
Assets/Prefabs/Ballzone/FreezerMono.prefab:48
  freezeSprite: {fileID: 21300000, guid: 558ffd990351d9b4896d1582e751ee57, type: 3}
Assets/Sprites/iceBall.png.meta:2
  guid: 558ffd990351d9b4896d1582e751ee57     # 1024x1024, spriteMode 1, ppu 100, maxTextureSize 2048
Assets/Scenes/Game.unity                      # 2 instancias del prefab, sin guid directo
```

Ese guid aparece **sólo** en `FreezerMono.prefab` y en su propio `.meta` — el barrido GUID→YAML
de PRV-3 lo confirma y ningún otro consumidor lo reclama (CTR-3, segundo escenario).

### S3.1 La fila de §9 (T-24)

```
- ball_state_frozen_01.png,NEW,-,TBD,416x416,1,-,0/0/0/0,2048,0.5/0.5,100,-
+ ball_state_frozen_01.png,REPLACE,Assets/Sprites/iceBall.png,1024x1024,1024x1024,1,1,0/0/0/0,2048,0.5/0.5,100,-
```

`px_actual` = `px_entrega` = **1024×1024** porque es el header medido del archivo y no hay
canvas propuesto que respetar (`mode_actual` = `mode_target` = 1, el `.meta` no necesita nada).
Los 416×416 de la fila vieja eran un canvas inventado para un overlay que nunca se dibujó.

Efecto en los conteos del propio contrato (§2 del manifiesto): `D — Bolas` pasa de 12/3 a
**13/2** y el total de **85/37** a **86/36**. La aritmética vieja queda marcada como previa en
la nota de reclasificación, no borrada.

### S3.2 Los lectores que había que mover con la fila (T-25, T-27)

Ninguno de los tres gates tenía un literal con el número: los tres derivan de §9 y del ledger.
Lo que sí había eran **literales de comportamiento**:

| Archivo | Antes | Después | Por qué |
|---|---|---|---|
| `generate_placeholders.BLOCKED` | 4 entradas, una con `ball_state_frozen_01` | **3** entradas | una pieza que ya no es NEW no puede seguir teniendo motivo de bloqueo: sería una decisión zombi |
| motivos de las 2 `BLOCKED_PX` | `… 2 BLOCKED_BY_DESIGN = 37` | `… 1 BLOCKED_BY_DESIGN = 36` | el `motivo` promete el conteo post-desbloqueo; con 37 prometía un número que ya no existe |
| motivo de `spotlight_fill` | `EXCLUDED deja 37 = 33 + 2 + 1 + 1` | `EXCLUDED deja 36 = 33 + 2 + 0 + 1` | ídem, y la `BLOCKED_BY_DESIGN` restante queda en 0 |
| `check_placeholder_wiring.EXPECTED` | `{20, 13, 4}` literal | **derivado** del ledger + §9 | CTR-4: los buckets no pueden ser una constante que haya que editar cuando una pieza se entrega |
| `check_placeholder_wiring.EXPECTED_ROWS 37` | literal | **derivado** (cohorte = filas del ledger) | el mapa tiene 37 filas por coincidencia, no por contrato: 36 NEW + 1 REPLACE |

El cohorte del mapa ahora se **cruza contra dos artefactos**: el conjunto de piezas NEW sale del
ledger y cada fila extra tiene que ser una `REPLACE` de §9. Una `BLOCKED_*` sólo puede figurar
`bloqueado` (`LEDGER_STATE_TO_MAP`), así que una pieza bloqueada cableada es un hallazgo, no una
interpretación. El estado de las 36 piezas NEW no se compara contra una tabla de números: se
compara contra `pieza → estado` del ledger, que es lo único que un cambio de estado tiene que
tocar.

### S3.3 El mapa y la rama `MonoBehaviour` (T-26, T-27)

```
- ball_state_frozen_01.png,image,-,-,-,-,bloqueado,-,-,
+ ball_state_frozen_01.png,serialized_sprite_field,Assets/Prefabs/Ballzone/FreezerMono.prefab,FreezerMono,MonoBehaviour,freezeSprite,cableado,-,-,
```

`component_docs()` gana la rama `MonoBehaviour`: no tiene claves fijas, así que se reconoce por la
clave que **la fila nombra** (`freezeSprite`) teniendo valor serializado. Sin ese parámetro la
rama no existe a propósito — cualquier documento 114 de un nodo arbitrario sería un falso
positivo. `MAP_FIELDS["serialized_sprite_field"]` acepta `freezeSprite` y `BASE_KEYS` también (es
la clave que un diff puede tocar), y `MonoBehaviour` entra a la allowlist de `componente`.

El guid contra el que se resuelve el campo **no está en el ledger**: la pieza no es NEW. Sale de
`provenance-v1.csv` (PRV-1). Ese es el acoplamiento que este slice declara: sin la fila 86, el
campo no se puede verificar y el gate lo dice en vez de saltearlo.

```
$ python tools/art/check_placeholder_wiring.py --plan   # ANTES de la fila 86 (estado transitorio)
FALTA ball_state_frozen_01.png: no hay guid congelado (ledger ni provenance) contra el cual resolver FreezerMono.freezeSprite
RESULTADO   FAIL  (1 hallazgos)

$ python tools/art/check_placeholder_wiring.py --plan   # árbol final
MAPA        36 NEW: 20 cableado, 13 sin_slot, 3 bloqueado
REPLACE     1 cableado: ball_state_frozen_01.png
FILAS       37 en el mapa (36 del cohorte NEW + 1 REPLACE con slot)
GUID        119 piezas con guid congelado | 21 campos resueltos contra su pieza
PENDIENTE   1 capturas sin declarar (slice de capturas)
RESULTADO   PASS  (0 hallazgos)
```

`PENDIENTE 1` es deliberado y no es un hallazgo: la captura de `ball_state_frozen_01` la produce el
rig de S4b (`--require-captures` es el gate de ese slice). Declararla vacía es honesto; inventar
un PNG de captura no.

La fila REPLACE **no** suma a los tres buckets. Si sumara, el mapa diría `21/13/3 = 37` y el
cohorte NEW quedaría inflado por una pieza que no es del cohorte: es la misma doble cuenta que
CTR-3 prohíbe. Por eso `hosts_from_map()` filtra por cohorte — una REPLACE no tiene render
derivado contra el cual medir `SURVIVE`.

### S3.4 Ledger y fila 86 (T-28)

```
$ python tools/art/generate_placeholders.py --freeze-guid
escrito Docs/art/placeholders-v1.csv: 33 GUID congelados desde los .meta
$ wc -l Docs/art/placeholders-v1.csv
37        # encabezado + 36 filas (antes 38)

$ python tools/art/audit_art_manifest.py --capture-provenance --add ball_state_frozen_01
ADD          ball_state_frozen_01.png -> provenance-v1.csv (558ffd99, fb5f40d3)
FILAS        86 | 84 guid distintos, 2 guid compartidos por el mismo origen
$ wc -l Docs/art/provenance-v1.csv
87        # encabezado + 86 filas
```

`--freeze-guid` es seguro para este slice y se verificó antes de correrlo: `do_freeze_guid()`
**sólo** escribe el ledger (`ledger_path.write_text`), lee los `.meta` y nunca los abre en
escritura; el único escritor de `.meta` del proyecto sigue siendo `ArtMetaMigrator` (C#). Prueba:

```
$ git status --porcelain -- Assets
                                        # vacío: ni iceBall.png ni su .meta se tocaron
$ git status --porcelain -- "*.png"
                                        # vacío: los 33 PNG siguen byte-idénticos
```

La fila 86 es **append-only**: no re-basa las 85 anteriores ni puede reescribirlas
(`ADD-REFUSED`). `px_actual` y `px_entrega` son `1024x1024`, medidos del disco y de §9.

### S3.5 El cierre, medido (T-29)

```
$ python tools/art/check_placeholders.py          # exit 0
COHORT      122 = 86 REPLACE + 36 NEW
COHORT-SUM  36 = 33 + 0 + 0 + 2 + 1
RESULTADO   PASS  (33 GENERATED, 3 bloqueadas; 0 DELIVERED, 0 EXCLUDED; 36 filas, 0 hallazgos)

$ python tools/art/audit_art_manifest.py          # exit 1 — el baseline declarado, sin una más
PROCEDENCIA 86 filas REPLACE | 0/86 reemplazadas | 4 sin pantalla
VIOLACIONES 4 en 2 reglas        # FORMAT-JPG 3 + MODE-TARGET 1
  COHORT      122 = 86 REPLACE + 36 NEW
  COHORT-SUM  36 = 33 + 0 + 0 + 2 + 1
  REPLACE-LEDGER  86/86 filas con pieza+origen+guid+sha256+px (84 guid distintos)
  REPLACE-SHA  0/86 reemplazadas (86 BASELINE, 0 DELIVERED, 0 UNKNOWN)
  ORPHAN      4 REPLACE sin pantalla: game_board_balance_base_01.png, game_board_shadow_long_01.png, ui_common_divider_01.png, ui_pass_state_lock_01.png
  total 173 referencias en 29 archivos

$ python tools/art/selftest_art_gates.py          # exit 0
RESULTADO   PASS  (17 casos, 0 hallazgos)        # 16 de S1/S2 + wiring-mono
```
Tres números se movieron **porque la población creció**, y se declaran en vez de ajustarse en
silencio:

| Medición | S2 (85 REPLACE) | S3 (86 REPLACE) | Delta |
|---|---|---|---|
| referencias por GUID | 172 en 28 archivos | **173 en 29 archivos** | +1 referencia, +1 archivo: `FreezerMono.prefab:48` es el consumidor de la pieza 86 |
| `REPLACE-LEDGER` | 85/85 (83 guid distintos) | **86/86 (84 guid distintos)** | +1 pieza, +1 guid |
| `REPLACE-SHA` | 0/85 | **0/86** | denominador derivado, cero reemplazos: `iceBall.png` está en baseline |

`PRV-3` fija "172 referencias en 28 archivos" para las **85** REPLACE que medía S2. La medición de
hoy sobre las **86** es 173/29 y ninguna referencia se perdió: el diferencial es exactamente el
consumidor que se sumó. Ese literal de PRV-3 queda con su población explícita (85) y este slice
declara la nueva, en vez de editar el número como si siempre hubiera sido 173.

El caso `replace-sha` del selftest asertaba el denominador con un `85` literal; pasa a derivarlo
de `len(provenance)`. Es el mismo defecto que CTR-3 corrige en la prosa, encontrado en el test.

### S3.6 Los negativos de CTR-4 (caso nuevo `wiring-mono`)

```
$ python tools/art/selftest_art_gates.py --only wiring-mono
CASE  wiring-mono
freezeSprite declarado: 1 campo(s) resuelto(s), 0 hallazgo(s)
slot vaciado: 1 hallazgo(s) -> FALTA ball_state_frozen_01.png: FreezerMono.freezeSprite = sin sprite, el ledger congela 558ffd990351d9b4896d1582e751ee57
mapa real: 36 = 20 + 13 + 3 | REPLACE cableadas: ['ball_state_frozen_01.png']
RESULTADO   PASS  (1 casos, 0 hallazgos)
```

El sandbox escribe una copia del prefab **bajo `Temp/`** y la nombra desde un mapa también en
`Temp/`: el positivo mide el mismo campo que el árbol real, y el negativo (`freezeSprite:
{fileID: 0}`, el escenario "slot vaciado" de CTR-4) tiene que fallar nombrando la pieza. El mapa
real se vuelve a resolver ahí mismo para atar `36 = 20 + 13 + 3` y "la única REPLACE cableada es
la frozen" a los artefactos, no a este documento.

### S3.7 El contrato archivado: la sustitución, fila por fila

Los requirements de `sdd/placeholders-arte-v2` (obs **#4045**) están **cerrados**: se citan, no se
reescriben. Pero cinco de ellos —`APH-1`, `APH-10`, `APH-11`, `APW-1` y `APW-2`— contienen
literales numéricos que la reclasificación invalida, y dejar el texto archivado diciendo `37`
cuando el repo mide `36` es exactamente el modo de fallo que un replay futuro no puede detectar.
Esta tabla es la receta: **literal viejo → literal nuevo, requirement por requirement**, para que
un replay (o una sincronización de specs) la aplique sin re-derivar nada.

| Req. | Literal archivado (viejo) | Sustitución (nuevo) |
|---|---|---|
| `APH-1` | "derivar el cohorte de §9 (`estado=NEW`, **37**)" | `estado=NEW`, **36** |
| `APH-1` | "El ledger MUST tener **37** filas: 33 `GENERATED` … `BLOCKED_BY_DESIGN` (`ui_tutorial_spotlight_fill_01`, **`ball_state_frozen_01`**)" | **36** filas … `BLOCKED_BY_DESIGN` (**`ui_tutorial_spotlight_fill_01`**) |
| `APH-1` | Escenario *cohorte íntegro*: "GIVEN §9 con **37** NEW … THEN 33 PNG y **37** filas con los **4** motivos" | **36** NEW … **36** filas con los **3** motivos |
| `APH-10` | "un PNG cuyo nombre no esté entre las **37** filas MUST fallar por `NEW-ORPHAN`" | **36** |
| `APH-10` | "El ledger MUST tener **37** filas sin duplicados contra las **37** NEW de §9" | **36** y **36** |
| `APH-10` | Escenario *huérfano y bloqueada*: "un PNG con nombre fuera de las **37**" | fuera de las **36** |
| `APH-11` | "Cada una de las **4** piezas bloqueadas" | **3** |
| `APH-11` | Tabla, fila `ball_state_frozen_01` → `BLOCKED_BY_DESIGN`, desbloqueo por exclusión o marcador | **fila eliminada**: la pieza es REPLACE de `iceBall.png`, no está bloqueada y su ruta de desbloqueo ya no existe |
| `APH-11` | "el total MUST seguir siendo **37** y la resta `37 − BLOCKED_PX − BLOCKED_BY_DESIGN − EXCLUDED = GENERATED`" | **36** y `36 − … = GENERATED` |
| `APH-11` | "el ledger MUST quedar exactamente como APH-1 lo fija (**33 / 2 / 2**)" | **33 / 2 / 1** |
| `APH-11` | Escenario *§9 v3 desbloquea*: "THEN **35 `GENERATED` + 2 `BLOCKED_BY_DESIGN` = 37**" | **34 `GENERATED` + 2 bloqueadas** (1 `BLOCKED_PX` + 1 `BLOCKED_BY_DESIGN`) **= 36** |
| `APH-11` | Escenario *motivo sin decisión*: "GIVEN `motivo = "TBD"` en **`ball_state_frozen_01`**" | **`ui_tutorial_spotlight_fill_01`** — es la única `BLOCKED_BY_DESIGN` que queda; el escenario prueba la regla, no la pieza |
| `APH-11` | Escenario *exclusión permanente*: "el total sigue en **37**" | **36** |
| `APW-1` | Título: "20 cableables, 13 `sin_slot`, **4** bloqueadas" | **3** bloqueadas |
| `APW-1` | "`BLOCKED_*` **4** — sin PNG: `bubble_tail_left`, `caption_pill` (`BLOCKED_PX`), `spotlight_fill`, **`ball_state_frozen`** (`BLOCKED_BY_DESIGN`)" | **3** — sin `ball_state_frozen` |
| `APW-1` | "Aritmética: **20 + 13 + 4 = 37**; **33 GENERATED = 20 + 13**" | **20 + 13 + 3 = 36**; la segunda identidad no cambia |
| `APW-2` | "MUST tener **37** filas (**33 `GENERATED` + 4 `BLOCKED_*`**)" | **37** filas (**36** del cohorte NEW: 33 `GENERATED` + **3 `BLOCKED_*`**, **+ 1 `REPLACE` cableada**) |

**La trampa de esta tabla**: `APW-2` es el único requirement donde `37` **no** se convierte en
`36`. El mapa sigue teniendo 37 filas; lo que cambia es su composición (36 NEW + 1 REPLACE). Un
replay que aplique "37 → 36" a ciegas rompe `APW-2` y el gate de filas del mapa. Por eso la
sustitución está escrita fila por fila y no como una regla global.

Nada de esto se aplicó al artefacto archivado: obs **#4045** sigue byte por byte como se cerró.
Lo que se versiona acá es la **diferencia auditable** entre lo archivado y lo medido hoy; el
archivo de specs sólo debería moverse si alguien reabre `placeholders-arte-v2`, y entonces tiene
esta tabla esperándolo.

### S3.8 Lo que este slice no toca

- **Cero bytes de arte**: los 33 PNG quedan idénticos (`git status -- "*.png"` vacío). `iceBall.png`
  se lee para medir header y sha, nunca se escribe.
- **Cero `.meta`**: `--freeze-guid` no es un escritor de `.meta` (ver S3.4). Si lo hubiera sido
  sobre `iceBall.png.meta` el slice se detenía: el guid de una REPLACE es el contrato (PRV-3).
- **Cero C#**: la rama `MonoBehaviour` es del checker Python. Unity no se abrió en este slice.
- **Contratos de S1 y S2 intactos**: `--ack-sha` ledger-only, `DELIVERED`/`EXCLUDED`,
  `PX_ENTREGA_PENDING 6/6`, `SURVIVE` canónico, `TBD` exit 1, `sheet-clear`, `RECT-STALE` sobre el
  rect vivo, `REPLACE-SHA` en sus tres estados y `ORPHAN-*` siguen verdes en el selftest (17/17).
- **Baseline del auditor**: 4, sin una quinta. Cualquier hallazgo nuevo habría sido un defecto
  introducido por este slice; no lo hay.
