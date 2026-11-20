# placeholders-arte-v2 — evidencia por slice

Change `placeholders-arte-v2` · contrato §9 de `manifest-refresh-visual-v1.md` (37 NEW) · base `a3b9cda`
Python 3.13.6 / Pillow 13.0.0 · Gamma (`m_ActiveColorSpace: 0`) · sin Unity en esta máquina.

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
