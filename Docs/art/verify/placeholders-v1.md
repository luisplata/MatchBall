# placeholders-arte-v2 — evidencia por slice

Change `placeholders-arte-v2` · contrato §9 de `manifest-refresh-visual-v1.md` (37 NEW) · base `a3b9cda`
Python 3.13.6 / Pillow 13.0.0 · Gamma (`m_ActiveColorSpace: 0`) · Unity 6000.3.10f1 (editor abierto:
los §1b.6 y §2b se corrieron en él).

Cada sección pega los comandos y su salida. Nada de acá se escribe a mano: el ledger es
derivado, y esta página sólo transcribe lo que el gate imprimió.

---

## §1a — generador determinista + gate (cero `Assets/`)

**Herramientas**: `tools/art/generate_placeholders.py` (parser de §9, familias, render, ledger,
`--manifest`, `--freeze-guid`, `--ack-sha`) y `tools/art/check_placeholders.py` (el gate:
IHDR, chunks, píxeles, ledger, GUID, firma, `FRAME-FILL`, `LEGIBLE`, `SURVIVE`). El gate
importa el parser y el render del generador: se revisa una sola vez y no puede divergir
(plan B declarado en `sdd/placeholders-arte-v2/tasks`).

### 1a.1 Dos corridas, los mismos bytes (APH-7 / G-01)

```
$ python tools/art/generate_placeholders.py --out Temp/art-tmp --twice
twice       33 piezas identicas en dos corridas
escritos 33 PNG en Temp/art-tmp y el ledger Temp/art-tmp/placeholders-v1.csv
```

### 1a.2 Otro `TZ`, otro `PYTHONHASHSEED`, el mismo agregado (APH-7)

```
$ TZ=America/Anchorage  PYTHONHASHSEED=1 python tools/art/generate_placeholders.py --out Temp/art-tza
$ TZ=America/Anchorage  PYTHONHASHSEED=1 ... --manifest > Temp/man-a.txt
$ TZ=Pacific/Kiritimati PYTHONHASHSEED=0 python tools/art/generate_placeholders.py --out Temp/art-tzb
$ TZ=Pacific/Kiritimati PYTHONHASHSEED=0 ... --manifest > Temp/man-b.txt
$ diff Temp/man-a.txt Temp/man-b.txt && diff -r Temp/art-tza Temp/art-tzb --exclude=placeholders-v1.csv
DETERMINISTA: 33/33 bytes y agregados iguales

$ grep AGREGADO Temp/man-a.txt
AGREGADO  eafea6fcfc8e54a5bf3e122dff6723c2d0219c5c630908efe613bbb6a967d6e5  33/33
```

Ningún chunk `tIME`, `pHYs`, `tEXt` ni `iTXt`: el `CHUNK` del gate recorre los bytes crudos y
falla nombrando pieza y chunk. La fuente es `ImageFont.load_default(size=...)` —la Aileron
embebida en Pillow— y no hay ninguna lectura de `C:\Windows\Fonts` ni de fontconfig.

### 1a.3 Las 37 filas, al píxel de §9 (APH-1 / APH-2)

33 PNG, `IHDR` leído de los bytes crudos (offsets 16-24, big-endian), 8 bit colorType 6;
`BLOCKED_*` sin archivo.

| pieza | px | border | familia |
|---|---|---|---|
| `ui_tutorial_dim_01.png` | 4x4 | 0/0/0/0 | flat4 |
| `ui_tutorial_bubble_body_01.png` | 400x520 | 48/56/48/56 | frame |
| `ui_tutorial_bubble_tail_left_01.png` | - | 0/0/0/0 | BLOCKED_PX |
| `ui_tutorial_caption_pill_01.png` | - | 24/16/24/16 | BLOCKED_PX |
| `ui_tutorial_spotlight_ring_01.png` | 512x512 | 0/0/0/0 | silhouette |
| `ui_tutorial_spotlight_fill_01.png` | 512x512 | 0/0/0/0 | BLOCKED_BY_DESIGN |
| `ui_tutorial_hand_tap_01.png` | 160x200 | 0/0/0/0 | silhouette |
| `ui_tutorial_hand_tap_02.png` | 160x200 | 0/0/0/0 | silhouette |
| `ui_tutorial_hand_tap_03.png` | 160x200 | 0/0/0/0 | silhouette |
| `ui_tutorial_hand_point_left_01.png` | 160x200 | 0/0/0/0 | silhouette |
| `ui_tutorial_hand_point_right_01.png` | 160x200 | 0/0/0/0 | silhouette |
| `ui_tutorial_icon_star_01.png` | 64x64 | 0/0/0/0 | silhouette |
| `ui_tutorial_step_dot_normal_01.png` | 28x28 | 0/0/0/0 | silhouette |
| `ui_tutorial_step_dot_active_01.png` | 28x28 | 0/0/0/0 | silhouette |
| `ui_tutorial_step_dot_done_01.png` | 28x28 | 0/0/0/0 | silhouette |
| `ui_tutorial_btn_skip_normal_01.png` | 120x56 | 12/12/12/12 | frame |
| `ui_tutorial_btn_skip_pressed_01.png` | 120x56 | 12/12/12/12 | frame |
| `ui_tutorial_btn_continue_normal_01.png` | 280x84 | 12/12/12/12 | frame |
| `ui_tutorial_btn_continue_pressed_01.png` | 280x84 | 12/12/12/12 | frame |
| `ui_tutorial_btn_retry_normal_01.png` | 220x64 | 12/12/12/12 | frame |
| `ui_tutorial_btn_retry_pressed_01.png` | 220x64 | 12/12/12/12 | frame |
| `ui_tutorial_panel_retry_01.png` | 780x420 | 32/32/32/32 | frame |
| `ui_tutorial_icon_danger_01.png` | 48x48 | 0/0/0/0 | silhouette |
| `ui_hud_btn_settings_pressed_01.png` | 28x28 | 0/0/0/0 | silhouette |
| `ui_hud_avatar_frame_player_01.png` | 160x160 | 0/0/0/0 | frame |
| `ui_hud_avatar_frame_ai_01.png` | 160x160 | 0/0/0/0 | frame |
| `ui_hud_score_pill_player_01.png` | 220x72 | 0/0/0/0 | frame |
| `ui_hud_score_pill_ai_01.png` | 220x72 | 0/0/0/0 | frame |
| `ui_hud_panel_player_01.png` | 600x96 | 0/0/0/0 | frame |
| `game_board_zone_veil_01.png` | 4x4 | 0/0/0/0 | flat4 |
| `ball_state_selected_ring_01.png` | 480x480 | 0/0/0/0 | silhouette |
| `ball_state_frozen_01.png` | 416x416 | 0/0/0/0 | BLOCKED_BY_DESIGN |
| `ball_state_matched_01.png` | 96x96 | 0/0/0/0 | silhouette |
| `ui_common_btn_primary_pressed_01.png` | 124x25 | 7/6/5/7 | frame |
| `ui_common_btn_primary_disabled_01.png` | 124x25 | 7/6/5/7 | frame |
| `ui_menu_bg_panel_01.png` | 1024x813 | 0/0/0/0 | frame |
| `ui_menu_btn_play_pressed_01.png` | 140x48 | 0/0/0/0 | frame |

Aritmética cerrada: **37 = 33 GENERATED + 2 `BLOCKED_PX` + 2 `BLOCKED_BY_DESIGN`**. El
`classify()` aborta con exit 2 si esa resta no cierra, si una NEW no tiene familia ni bloqueo,
o si un `border` no entra en el lienzo (`l+r >= w` o `t+b >= h`: aborta, no clampea).

### 1a.4 El gate sobre los 33 (T-06)

```
$ python tools/art/check_placeholders.py --out Temp/art-tmp
RESULTADO   PASS  (33 GENERATED, 4 bloqueadas, 0 hallazgos)
```

Qué quedó verde, regla por regla: `IHDR` 33/33 == `px_entrega`; `colorType 6` 8-bit 33/33;
`CHUNK` sin `tIME`/`pHYs`/`tEXt`/`iTXt`; `FRAME` (esquinas `#FF00FF` opaco y cero alfa < 1 en
las 17 `frame`); `ALFA` (4 esquinas en 0 y cero alfa parcial en las 14 `silhouette`); `FLAT4`
(2 piezas de un solo color `#FFFFFF`); `FIRMA` (tramo `#FF00FF` ≥ 6 px en las 31 con
`min(w,h) ≥ 16`; `flat4` exenta por diseño); `FRAME-FILL` (relleno `#E8EAF0` = 232/234/240, los
tres canales ≥ 232); `LEGIBLE` (texto `#FFFFFF` sobre placa `#1A1D26` = **16.8:1** pre-tinte ≥ 4.5).

### 1a.5 Un píxel, un canal, con coordenada (APH-8)

```
$ cp -r Temp/art-tmp Temp/art-tamper && (bajar G de (17,20) de 29 a 28 en ui_hud_score_pill_player_01.png)
$ python tools/art/generate_placeholders.py --out Temp/art-tamper --check
ui_hud_score_pill_player_01.png          PIXEL: (17,20) canal G 29->28
RESULTADO   FAIL  (33 GENERATED, 4 bloqueadas, 1 hallazgos)
$ echo $?
1
```

Una sola pieza en el reporte, con coordenada y canal. El orden es el de la spec: firma e IHDR
de los bytes crudos primero, decodificación después. Si los bytes difieren y los píxeles no,
el hallazgo se rotula `SHA` y **no** inventa coordenadas.

### 1a.6 `SURVIVE`: qué discrimina y qué no (APH-6b — **conflicto de spec declarado**)

La fórmula que se implementó es la del cuadro de APH-6b y del design D4:
`L_tinte(relleno) ≥ 0.004` **y** `L_tinte(relleno) − L_tinte(marca más oscura) ≥ 0.002`, con el
`m_Color` multiplicando el valor sRGB (Gamma, no lineal).

```
$ python ... --check --hosts Temp/hosts-dark.csv     # 0.07/0.08/0.12 sobre bubble_body y dim
RESULTADO   PASS  ... 0 hallazgos
$ python tools/art/generate_placeholders.py --check --hosts Temp/hosts-ok.csv   # (1,0.7,0.2) en icon_danger
RESULTADO   PASS  ... 0 hallazgos
```

Medido: relleno `#E8EAF0` con host `0.07` → `L_tinte = 0.00665` ≥ 0.004 ✓ (design D4 publica
0.00497 con su variante de redondeo; mismo veredicto). Con el relleno oscuro que la spec usa
en su escenario, `#1A1D26` con `0.07` → `L_tinte = 0.00067 < 0.004` → **falla por `SURVIVE`**,
que es exactamente lo que el escenario discrimina (la spec publica 0.00062: misma conclusión,
redondeo del producto Gamma).

> **Residuo de APH-6b, para `sdd-verify`.** La prosa dice "mientras un host conserve `m_Color`
> con luminancia < 0.25, su pieza MUST fallar `SURVIVE`", pero el cuadro del mismo requirement
> marca ✓ para `0.07` (0.00497 / 0.00435) y el design D4 lo confirma. Con un relleno claro
> —que APH-6 obliga— un host de 0.07 **no** puede fallar el piso 0.004: la frase y el cuadro
> son incompatibles. Se implementó el cuadro (es el número medido y la decisión de design) y
> se deja la frase sin cumplir a propósito. Para cerrarla hay que elegir una: (a) borrar la
> frase, o (b) subir el piso a `> 0.0067`. No se toca código para ninguna de las dos.

`SURVIVE` sólo se evalúa con host declarado (`--hosts`) o cuando el mapa del slice 2 declara el
host vivo; sin host no hay con qué medir y el gate lo omite, en vez de inventar un verde.

### 1a.7 Ledger y manifest (APH-9 / T-07)

```
$ head -2 Temp/art-tmp/placeholders-v1.csv
pieza,estado,px,border,familia,path,guid,sha256,motivo
ui_tutorial_dim_01.png,GENERATED,4x4,0/0/0/0,flat4,Assets/Sprites/ui_tutorial_dim_01.png,,84595179…

$ python tools/art/generate_placeholders.py --out Temp/art-tmp --manifest | tail -1
AGREGADO  eafea6fcfc8e54a5bf3e122dff6723c2d0219c5c630908efe613bbb6a967d6e5  33/33
```

`motivo` está vacío en las 33 `GENERATED` y no vacío en las 4 `BLOCKED_*`, sin `TBD` ni
genéricos: cada uno nombra el artefacto (§9 v3 o la decisión de design), el rol que decide y el
efecto en los conteos (APH-11). El `classify()` rechaza el generador si un motivo cae en un
genérico.

`--ack-sha` es el único reconocimiento: con un sha de ledger distinto del render, el generador
**no escribe nada** y sale 1 nombrando pieza, sha viejo y sha nuevo; con `--ack-sha <pieza>`
reescribe sólo esa celda.

```
$ python tools/art/generate_placeholders.py --out Temp/art-tmp
ui_tutorial_bubble_body_01.png SHA: ledger 0000…, render f456e8… (reconocer con --ack-sha ui_tutorial_bubble_body_01.png)
FALLA  nada escrito: 1 hallazgos
$ python tools/art/generate_placeholders.py --out Temp/art-tmp --ack-sha ui_tutorial_bubble_body_01.png
escritos 33 PNG en Temp/art-tmp y el ledger Temp/art-tmp/placeholders-v1.csv
```

`--freeze-guid` sale 2 mientras no existan los `.meta` (los escribe Unity en el slice 1b):

```
$ python tools/art/generate_placeholders.py --out Temp/art-tmp --freeze-guid
ERROR: falta Temp/art-tmp/ui_tutorial_dim_01.png.meta: los .meta los escribe Unity (ArtMetaMigrator, cohorte new)
$ echo $?
2
```

### 1a.8 Límites declarados de esta capa

- **Etiqueta en piezas de 124×25** (`ui_common_btn_primary_pressed/disabled`): con marco de
  2 px y borde 7/6/5/7 el interior deja 11 px de alto, así que entra una sola línea
  (`SIN ARTE`); el `WxH` y el nombre no. La placa cruza la banda cian en esas dos.
- **Piezas gemelas con prefijo común largo comparten bytes**: el truncado de D2 saca el último
  carácter del nombre, y `…_pressed` / `…_disabled` de `124×25` y `…_normal` / `…_pressed` de
  `280×84` difieren después del punto de corte. Sus filas del ledger, sus GUID y sus `.meta`
  sí difieren. En 2b la captura de cada una sale de su nodo, no del otro.
- `except: sin_firma` de las dos `flat4` se registra como `familia=flat4` en el ledger (el
  encabezado congelado por T-07 no tiene columna `except`).

---

## §1b — los 33 bytes, el ledger y las reglas `NEW-*` del auditor

**Entregado**: 33 PNG en `Assets/Sprites/` + sus 33 `.meta` + `Docs/art/placeholders-v1.csv` (37
filas, 33 GUID congelados) + `Docs/art/deliveries-v1.csv` (sólo encabezado) + cohorte `new` y guard
v2 en `ArtMetaMigrator.cs` + 6 reglas `NEW-*` en `audit_art_manifest.py`.

El paso de Unity ya corrió: los 33 `.meta` los escribió `TextureImporter` vía `ArtMetaMigrator`
(cohorte `new`) en el editor 6000.3.10f1, y `--freeze-guid` congeló los 33 GUID en el ledger
(§1b.6). Ningún `.meta` se editó a mano: D1 sigue con un solo escritor.

### 1b.1 Generación sobre `Assets/Sprites/`

```
$ python tools/art/generate_placeholders.py
escritos 33 PNG en Assets/Sprites y el ledger Docs/art/placeholders-v1.csv
$ ls Assets/Sprites/ui_tutorial_*.png | wc -l   # + ui_hud_*, ball_state_*, game_board_zone_veil_01
33
```

Ninguna `BLOCKED_*` tiene archivo: `ui_tutorial_bubble_tail_left_01.png`,
`ui_tutorial_caption_pill_01.png`, `ui_tutorial_spotlight_fill_01.png` y
`ball_state_frozen_01.png` no existen en disco (APH-1).

### 1b.2 Gate del generador

```
$ python tools/art/generate_placeholders.py --check
INFO  37 filas sin guid congelado: los .meta los escribe Unity (cohorte new) y --freeze-guid los congela
RESULTADO   PASS  (33 GENERATED, 4 bloqueadas, 0 hallazgos)
$ echo $?
0
```

### 1b.3 `--manifest` contra el ledger (APH-9)

```
manifest lines 33 | ledger GENERATED 33
iguales: 33 / 33        diferencias: []
AGREGADO  eafea6fcfc8e54a5bf3e122dff6723c2d0219c5c630908efe613bbb6a967d6e5  33/33
```

### 1b.4 Tamper: un canal, un píxel (APH-8)

```
$ (bajar G de (17,20) de 29 a 28 en Assets/Sprites/ui_hud_score_pill_player_01.png)
$ python tools/art/generate_placeholders.py --check
ui_hud_score_pill_player_01.png          PIXEL: (17,20) canal G 29->28
RESULTADO   FAIL  (33 GENERATED, 4 bloqueadas, 1 hallazgos)
$ echo $?
1
```

Restaurado: `PASS`. El reporte nombra pieza, coordenada, canal y ambos valores; ninguna otra
pieza aparece.

### 1b.5 Auditor: 6 reglas `NEW-*`, sin violaciones nuevas (T-15, G-02)

```
$ python tools/art/audit_art_manifest.py --no-refs
FILAS       122  (REPLACE 85 | NEW 37 | addresses 11)
LEDGER NEW  37 filas | 33 GENERATED registradas en el auditor
VIOLACIONES 14 en 3 reglas          # FORMAT-JPG 3 · MODE-TARGET 1 · RECT-STALE 10
...
  NEW-LEDGER  37 filas: 33 GENERATED, 4 bloqueadas, 0 guid congelados
  NEW-PENDING  33 piezas sin guid congelado: los .meta los escribe Unity ...
```

Antes del cambio el auditor daba **14 violaciones en las mismas 3 reglas** (`FORMAT-JPG` 3 ·
`MODE-TARGET` 1 · `RECT-STALE` 10): el cambio agrega **0**. Las 33 `GENERATED` entran al dict
`pieces` con `path` del ledger, así que heredan `MODE-TARGET`/`BORDER-META`/`MAX-TEX`/`PIVOT`/
`PPU` sin duplicar una línea de regla (D5), y `NEW-META` sólo agrega los dos campos que las
heredadas no miran: `textureType 8` y `alphaIsTransparency 1`.

Cada regla se probó rompiendo el corpus a propósito y restaurándolo:

| Regla | Cómo se forzó | Resultado |
|---|---|---|
| `NEW-BLOCKED` | copiar un PNG con el nombre de `ui_tutorial_spotlight_fill_01` | +1 violación, pieza nombrada |
| `NEW-ORPHAN` | dejar `ui_tutorial_stray_badge_01.png` suelto | +1 violación, path nombrado |
| `NEW-SHA` | bajar 1 canal de 1 píxel de `ui_hud_score_pill_ai_01` | +1 violación con sha del ledger y de disco |
| `NEW-LEDGER` | duplicar la fila de `ui_tutorial_dim_01` | +2 violaciones (duplicado y 38 filas) |
| `NEW-GUID` | congelar un guid sin `.meta` | +1 violación (ledger con guid, `.meta` ausente) |

Y el caso de APA-1, arte real: el mismo píxel torcido **con** una fila
`ui_tutorial_dim_01.png,<sha>,arte-test,2026-11-20` en `deliveries-v1.csv` pasa a
`NEW-DELIVERY` (INFO) y el auditor vuelve a 14: el sha distinto queda reconocido, no silenciado.

### 1b.6 El paso Unity: los 33 `.meta` (hecho, editor 6000.3.10f1)

Se corrió el orden de D1 **dentro del editor abierto**, no en batchmode: `ArtMetaMigrator.Apply("new",
null, false)` desde el mismo código del `-executeMethod`/R6 (la sesión batchmode habría abierto una
segunda instancia del proyecto). Es el único escritor: **ningún `.meta` se editó a mano**, y los 33
son salida de `TextureImporter.SaveAndReimport()`.

```
$ ArtMetaMigrator.Apply("new", null, false)
guard v2 ok: lo sucio en Assets/ es del cohorte (33 piezas del ledger)
apply: 33 .meta reimportados, 0 archivos YAML reescritos, 0 referencias remapeadas a 21300000
apply ok                                                                # exit 0
```

**Dos defectos del writer, encontrados por el auditor y corregidos en el writer** (commit
`b4603ec`), no en los `.meta`:

1. **RECT-STALE.** El primer import de un PNG lo deja en `Multiple` (default del proyecto) y recorta
   el `rect` del sub-sprite al alfa; el flip a `Single` **no reescribe** ese `rect`, así que 14 de los
   33 `.meta` quedaban con `rect ≠ lienzo` y el auditor los marcaba (14 → 28 violaciones). El writer
   ahora resincroniza los rects al lienzo con `ISpriteEditorDataProvider` **antes** de pasar a
   `Single` (`TextureImporter.spritesheet` está retirado en Unity 6: sólo tira `CS0618`).
2. **`ParseSheet` no podía ver el rect.** Comparaba `line.TrimEnd() == "rect:"`, que nunca matchea la
   línea indentada del `.meta`: la detección de rects stale estaba muerta, y por eso `rect-resync`
   del cohorte REPLACE tampoco podía dispararse. Se compara por `Trim()`.

Corregido eso, el auditor vuelve **exactamente al baseline** (14 en las mismas 3 reglas, 0 de las 6
`NEW-*`), que es lo que T-17 pide:

```
$ python tools/art/generate_placeholders.py --freeze-guid
escrito Docs/art/placeholders-v1.csv: 33 GUID congelados desde los .meta      # 33/33, únicos
$ python tools/art/generate_placeholders.py --check
INFO  4 filas sin guid congelado: ...                    # las 4 BLOCKED_*, sin .meta por diseño
RESULTADO   PASS  (33 GENERATED, 4 bloqueadas, 0 hallazgos)                   # exit 0
$ python tools/art/generate_placeholders.py --manifest | tail -1
AGREGADO  eafea6fcfc8e54a5bf3e122dff6723c2d0219c5c630908efe613bbb6a967d6e5  33/33
$ python tools/art/audit_art_manifest.py
LEDGER NEW  37 filas | 33 GENERATED registradas en el auditor
VIOLACIONES 14 en 3 reglas                     # baseline pre-cambio, NEW-* 0
```

**Contrato medido en los 33** (`NEW-META` en 0): `textureType: 8`; `spriteMode: 1`; `spriteBorder` =
`border` de §9 al píxel (p. ej. `bubble_body` `{48,56,48,56}`, `btn_primary` `{7,6,5,7}`);
`spritePixelsToUnits: 100`; `maxTextureSize: 2048`; `spritePivot` por §9; `alphaIsTransparency: 1`;
`rect` = lienzo completo; GUID de 32 hex, **33 distintos**, ausentes del índice pre-cambio.

**Idempotencia**: una segunda (y tercera) corrida del cohorte `new` deja los 33 `.meta`
**byte-idénticos** (sha256 del archivo, 33/33) y el ledger sin cambios — no hay round-trip de modo
cuando el `rect` ya es el lienzo.

**Excepción declarada de presupuesto de review**: los 33 `.meta` (~2.2 k líneas) son salida de
`TextureImporter`, van en el commit y se revisan por muestreo + `NEW-META`/`NEW-GUID`, nunca línea a
línea (tasks, *Presupuesto*).

---

## §2a — el mapa pieza→slot y el checker, cero YAML

**Entregado**: `Docs/art/placeholders-v1.md` (37 filas: 20 `cableado` + 13 `sin_slot` +
4 `bloqueado`) y `tools/art/check_placeholder_wiring.py`. Ningún archivo de `Assets/` cambió.

### 2a.1 `--plan`: las 20 filas resuelven

```
$ python tools/art/check_placeholder_wiring.py --plan
PLAN  ui_tutorial_dim_01.png                   image+tinte_design         TutorialOverlay/Dim
PLAN  ui_tutorial_bubble_body_01.png           image                      TutorialOverlay/Panel/Bubble
PLAN  ui_tutorial_hand_point_left_01.png       image                      TutorialOverlay/CalloutLeft/Hand
PLAN  ui_tutorial_hand_point_right_01.png      image                      TutorialOverlay/CalloutRight/Hand
PLAN  ui_tutorial_step_dot_normal_01.png       serialized_sprite_field    TutorialOverlay/Panel/Dots
... (20 líneas, una por fila cableada)
MAPA        37 filas: 20 cableado, 13 sin_slot, 4 bloqueado
GUID        0 piezas con guid congelado | 0 YAML escaneados en busca de filtraciones
PENDIENTE   20 capturas sin declarar (slice de capturas)
RESULTADO   PASS  (0 hallazgos)
```

Cada `nodo` se resolvió contra el YAML real (walk de `m_Father` sobre
`Transform`/`RectTransform`), no contra lo que decía el design: los 20 paths existen.

### 2a.2 Gate de diff (APW-4 / APW-4b / APW-6)

`git diff -U0` se clasifica por **clave + nodo**: cada línea cambiada se atribuye a su
documento (`--- !u!114 &id`) y de ahí a su GameObject, y se compara contra la allowlist de la
fila que nombra ese nodo.

```
$ (mover m_AnchoredPosition de TutorialOverlay/SkipButton y volver)
DIFF: Assets/Prefabs/UI/TutorialOverlay.prefab:486 del 'm_AnchoredPosition' no esta permitida en la fila ui_tutorial_btn_skip_pressed_01.png
RESULTADO   FAIL  (2 hallazgos)

$ (mover m_AnchoredPosition de un nodo que no está en el mapa)
DIFF: Assets/Prefabs/UI/TutorialOverlay.prefab:106 (m_AnchoredPosition) toca un nodo fuera del mapa
RESULTADO   FAIL  (2 hallazgos)

$ (cambiar m_Color de Bubble a 1/1/1/0.92, que es justo lo que hará el cableado)
RESULTADO   PASS  (0 hallazgos)
```

Un `.cs` en el diff falla por sí solo (APW-6), y cualquier asset que no esté en el mapa también.

### 2a.3 Lo que el mapa declara y el repo no sostiene

| Hallazgo | Fila | Por qué |
|---|---|---|
| `hand_tap_01..03` y `icon_star` sin nodo | `sin_slot` | Las manos son 3 poses de **un** nodo (`CalloutLeft/Hand`, `CalloutRight/Hand` ya son `hand_point_*`) y no hay nodo badge para la estrella. Crear nodo es diseño de UI (APW-1). |
| `zone_veil` → `TimeLineToStart/Canva/Canvas/Image/Image` | `cableado` | No existe ningún nodo llamado `Veil`/`Zone` en `Game.unity`; las `BallsZone`/`FreezeZone` son zonas de juego, no Images. El único Image de pantalla completa con `m_Color` negro (0/0/0/0.298) es el velo del countdown. **Si el dueño de la UI apuntaba a otro velo, se corrige esa celda** y el checker pasa sin tocar código. |
| `btn_primary_pressed/disabled` con `nodo=-` | `cableado` | Son variantes de estado del sprite maestro `Button_UI` (124×25, border 7/6/5/7, el mismo contrato que su REPLACE). El slot es el sprite, no un nodo: el checker cuenta referencias `m_SpriteState` al GUID en vez de resolver un path. |
| `dim` y `zone_veil` con `image+tinte_design` | `cableado` | APW-3 manda normalizar todo host con luminancia < 0.25, pero estas dos son relleno blanco 4×4 estirado a pantalla completa: su `m_Color` oscuro **es** el efecto (velo negro). Normalizarlo lo volvería un flash blanco. Es la única desviación declarada de APW-3 y está escrita **en la fila**. |

---

## §2b — cableado y capturas

**Entregado**: 30 celdas escritas en 3 YAML (`TutorialOverlay.prefab` 21, `MainMenu.unity` 8,
`Game.unity` 1), el mapa con las 20 filas en `cableado` + su `captura`, y 24 PNG en
`Docs/art/captures/placeholders-v1/` (20 recortes + 4 panorámicas). Cero `.cs`, cero nodos nuevos.

### 2b.1 El diff, contra la allowlist

```
$ python tools/art/check_placeholder_wiring.py --require-captures
MAPA        37 filas: 20 cableado, 13 sin_slot, 4 bloqueado
GUID        33 piezas con guid congelado | 278 YAML escaneados en busca de filtraciones
RESULTADO   PASS  (0 hallazgos)                                            # exit 0
```

El gate de diff (APW-4/APW-4b/APW-6) corre por defecto contra `HEAD` y ya pasó en el commit del
cableado (`f68b843`): 30 líneas cambiadas, todas `m_Sprite` (18), `m_Color` (4), `m_Transition` (7)
y `m_SpriteState` (5) — ninguna clave fuera de la allowlist de su fila, ningún `.cs`, ningún asset
fuera del mapa. Re-aplicar el procedimiento es no-op: la segunda pasada imprime `ya` en las 30
celdas y `git diff` queda vacío (APW-6).

### 2b.2 Las 20 referencias resuelven

Las 33 GUID del ledger cargan como `Sprite` (20/20 de las cableadas), y el prefab instanciado
devuelve los nombres esperados:

```
guids ok=20 bad=0
SPRITE Dim -> ui_tutorial_dim_01 rect=4x4
SPRITE Bubble -> ui_tutorial_bubble_body_01 rect=400x520
SPRITE ContinueButton -> ui_tutorial_btn_continue_normal_01 rect=280x84
SPRITE DangerStrip -> ui_tutorial_icon_danger_01 rect=48x48
SPRITE RetryPanel -> ui_tutorial_panel_retry_01 rect=780x420
SPRITE RetryButton -> ui_tutorial_btn_retry_normal_01 rect=220x64
SPRITE Hand -> ui_tutorial_hand_point_left_01 rect=160x200
SPRITE Hand -> ui_tutorial_hand_point_right_01 rect=160x200
SPRITE SkipButton -> ui_tutorial_btn_skip_normal_01 rect=120x56
```

`m_Color` normalizado a `(1,1,1)` sólo donde APW-3 lo manda (bubble 0.07, skip 0.18, panel_retry
0.08) preservando el alfa; `dim` y `zone_veil` conservan el suyo porque son `image+tinte_design`.

### 2b.3 Las 24 capturas

| Tipo | N | Archivos |
|---|---|---|
| recorte por pieza | 20 | `<pieza>.png`, en la `RectTransform` del nodo, con el marco y la banda |
| panorámica de host 900×1900 | 4 | `tutorial-overlay.png`, `game.png`, `main-menu.png`, `main-menu-configuration.png` |

24 sha256 distintos (T-26). El método está en el mapa (§*Capturas*): cámara del canvas →
`RenderTexture` 900×1900 → recorte. `MainMenu` se capturó en **play mode** porque en edit time sus
paneles están fuera de pantalla y el `Configuration` no tiene *opener* cableado; el rig lo deja en
la posición que tendría abierto y **no guarda la escena**.

Firma (`APA-2`) visible en `bubble`, `skip`, `panel_retry`, `btn_primary_*`, `play` y `settings`
(marco magenta + placa `SIN ARTE` + 4 esquinas + banda cian). En `continue`/`retry` el tinte verde
del host mata el magenta y el marco queda verde; en `dim`/`zone_veil` no hay firma por diseño
(`flat4`), y en `danger`/dots el alfa del host la atenúa. Todo eso está declarado en el mapa.

### 2b.4 El gate del placeholder y el auditor, después del cableado

```
$ python tools/art/generate_placeholders.py --check
RESULTADO   PASS  (33 GENERATED, 4 bloqueadas, 0 hallazgos)                # exit 0
$ python tools/art/audit_art_manifest.py
LEDGER NEW  37 filas | 33 GENERATED registradas en el auditor
VIOLACIONES 14 en 3 reglas            # baseline pre-cambio: las mismas 3 y 0 NEW-*
```

`SURVIVE` sigue midiéndose contra el `m_Color` **normalizado** del host (APW-3): las 3 filas
normalizadas pasan a host `(1,1,1)`, y la tabla de APH-6b se sigue cumpliendo con el host medido
`(0.07,0.08,0.12,0.92)` del §1a.6.

### 2b.5 Desviación declarada: las dos variantes del sprite maestro

En 2a esas dos filas decían `nodo=-` y el checker iba a contar referencias por GUID. En 2b se
midió el costo real: con `nodo=-` el gate de diff no admite ningún nodo, así que las dos piezas
quedaban `cableado` sin host que las mostrara — imposible de capturar (APW-5). Se corrigió **la
celda del mapa**, no el código: `btn_primary_pressed` → `Button_Audio` y `btn_primary_disabled` →
`Button_Name`, ambos `Selectable` con `m_Sprite` = `Button_UI` y `m_Transition: 1`. El alcance
queda en **7 `Selectable`** con `SpriteSwap`, no en los 19 que estimaba APW-4b: es una reducción,
y está escrita en el mapa.




