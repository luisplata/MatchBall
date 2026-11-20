# Placeholders v1 — mapa pieza → slot

Una fila por pieza del cohorte NEW (§9 de `manifest-refresh-visual-v1.md`), 37 en total:
**20 `cableado` + 13 `sin_slot` + 4 `bloqueado`**. Es la **única** fuente del cableado: una
pieza que no está acá no se cablea, y todo `nodo` tiene que resolver en su `asset`.

El bloque ```csv de abajo es el que lee `tools/art/check_placeholder_wiring.py`: la prosa de
este archivo existe para personas y no se parsea.

## Columnas

| Columna | Significado |
|---|---|
| `pieza` | nombre verbatim del PNG, igual que el ledger |
| `slot_tipo` | `image` · `image+alfa_host` · `image+tinte_design` · `serialized_sprite_field` · `selectable_state` |
| `asset` | prefab/escena/PNG que contiene el slot (`-` si la pieza no se cablea) |
| `nodo` | path del GameObject dentro del asset (`-` si no hay nodo propio) |
| `componente` | componente dueño del campo (`Image`, `Button`, `TutorialStepDots`) |
| `campo` | clave que se toca, exactamente como la serializa Unity |
| `estado` | `cableado` · `sin_slot` · `bloqueado` (una fila bloqueada puede nombrar su nodo: existe y hoy esta vacio, `m_Sprite: {fileID: 0}`, pero no se cablea) |
| `tint_original` | `m_Color` vivo **antes** de normalizar, `r/g/b/a`; `-` si no se toca el tinte |
| `transition_original` | `m_Transition` vivo antes de la excepción; `-` si no se toca |
| `captura` | PNG de `Docs/art/captures/placeholders-v1/`; el mismo nombre de la pieza |

## Excepciones declaradas, por fila (nunca por asset)

- `selectable_state` **es** la excepción `spriteswap` (APW-4b): permite `m_Transition 1→2` y
  `m_SpriteState.m_PressedSprite` / `m_DisabledSprite`. `m_ColorBlock` no se toca y queda como
  vía de reversión. Costo: esos Selectables pierden el feedback de tinte al presionar.
- `image+alfa_host`: el host tiene alfa < 1 por diseño (`SkipButton` 0.9, `ContinueButton` 0.95,
  `RetryButton` 0.95, `DangerStrip` 0.12). Se cablea el sprite y **no** se toca el alfa.
- `image+tinte_design`: las dos piezas `flat4` son relleno blanco de 4×4 que se estira a pantalla
  completa; su `m_Color` oscuro **es** el efecto (`Dim` = velo negro, `zone_veil` = velo del
  countdown). Se cablea el sprite y **no** se normaliza el tinte: normalizarlo convertiría un
  velo negro en un flash blanco. Es la única desviación declarada de APW-3, con la fila escrita.

## Las dos variantes del sprite maestro: alcance recortado (declarado)

`ui_common_btn_primary_pressed_01` y `ui_common_btn_primary_disabled_01` son las variantes de
estado del botón maestro `Button_UI` (124×25, border 7/6/5/7 — el mismo contrato que su REPLACE
`ui_common_btn_primary_normal_01`). El maestro lo consumen 15 `Selectable` de `MainMenu.unity` (más
3 en otras escenas): no existe un nodo que sea "el slot", así que la fila nombra **un consumidor**.

En 2a se había escrito `nodo=-` con la idea de contar referencias `m_SpriteState` por GUID en vez de
resolver un path. El slice 2b encontró el costo real de eso: el gate de diff (APW-4) sólo admite
nodos **nombrados en el mapa**, y el mapa congela 20 filas `cableado`; con `nodo=-` esas dos piezas
quedaban declaradas como cableadas sin poder aparecer en ninguna captura (APW-5 pediría una captura
de un host que no las muestra). Se corrige **la celda**, no el código:

| Fila | Nodo declarado | Por qué ese |
|---|---|---|
| `ui_common_btn_primary_pressed_01` | `Canvas/Configuration/Contenedor/SubContainer/Button_Audio` | `Selectable` con `m_Transition: 1` y `m_Sprite` = `Button_UI`: es el caso exacto de la variante `pressed` |
| `ui_common_btn_primary_disabled_01` | `Canvas/Configuration/Contenedor/SubContainer/Button_Name` | ídem para `m_DisabledSprite` |

**Alcance y costo, medidos**: el costo que APW-4b declara ("19 botones pierden el tinte") **no se
materializa** en este slice: quedan 7 `Selectable` con `m_Transition: 2` (skip, continue, retry,
settings, play, audio, name), no 19. Los otros 13 consumidores de `Button_UI` siguen en `ColorTint`
porque tocar un nodo fuera del mapa lo rechaza el gate. Es una **reducción** del blast radius, no una
ampliación: si el review quiere las variantes en los 15 consumidores, se agregan filas al mapa (no se
toca código) y el gate las empieza a admitir.

## Piezas que §9 promete y la topología no sostiene

`ui_tutorial_hand_tap_01..03` son tres poses de **un** nodo (`TutorialOverlay/CalloutLeft/Hand`
y `.../CalloutRight/Hand` ya son `hand_point_left/right`) y `ui_tutorial_icon_star_01` no tiene
nodo badge: las cuatro van `sin_slot`. Crear el nodo es diseño de UI, no cableado (APW-1).

`game_board_zone_veil_01` se cablea en el velo del
countdown de `Game.unity` (`TimeLineToStart/Canva/Canvas/Image/Image`, negro a 0.298): es el
único Image de pantalla completa con `m_Color` oscuro del tablero. Si el dueño de la UI
pretendía otro velo, se corrige esa celda y el checker vuelve a pasar sin tocar código.

```csv
pieza,slot_tipo,asset,nodo,componente,campo,estado,tint_original,transition_original,captura
ui_tutorial_dim_01.png,image+tinte_design,Assets/Prefabs/UI/TutorialOverlay.prefab,TutorialOverlay/Dim,Image,m_Sprite,cableado,0/0/0/0.72,-,ui_tutorial_dim_01.png
ui_tutorial_bubble_body_01.png,image,Assets/Prefabs/UI/TutorialOverlay.prefab,TutorialOverlay/Panel/Bubble,Image,m_Sprite,cableado,0.07/0.08/0.12/0.92,-,ui_tutorial_bubble_body_01.png
ui_tutorial_bubble_tail_left_01.png,image,Assets/Prefabs/UI/TutorialOverlay.prefab,TutorialOverlay/Panel/Bubble/Tail,Image,m_Sprite,bloqueado,0.07/0.08/0.12/0.92,-,
ui_tutorial_caption_pill_01.png,image,Assets/Prefabs/UI/TutorialOverlay.prefab,TutorialOverlay/CalloutLeft/CaptionPill,Image,m_Sprite,bloqueado,0.07/0.08/0.12/0.9,-,
ui_tutorial_spotlight_ring_01.png,image,-,-,-,-,sin_slot,-,-,
ui_tutorial_spotlight_fill_01.png,image,-,-,-,-,bloqueado,-,-,
ui_tutorial_hand_tap_01.png,image,-,-,-,-,sin_slot,-,-,
ui_tutorial_hand_tap_02.png,image,-,-,-,-,sin_slot,-,-,
ui_tutorial_hand_tap_03.png,image,-,-,-,-,sin_slot,-,-,
ui_tutorial_hand_point_left_01.png,image,Assets/Prefabs/UI/TutorialOverlay.prefab,TutorialOverlay/CalloutLeft/Hand,Image,m_Sprite,cableado,1/1/1/0.85,-,ui_tutorial_hand_point_left_01.png
ui_tutorial_hand_point_right_01.png,image,Assets/Prefabs/UI/TutorialOverlay.prefab,TutorialOverlay/CalloutRight/Hand,Image,m_Sprite,cableado,1/1/1/0.85,-,ui_tutorial_hand_point_right_01.png
ui_tutorial_icon_star_01.png,image,-,-,-,-,sin_slot,-,-,
ui_tutorial_step_dot_normal_01.png,serialized_sprite_field,Assets/Prefabs/UI/TutorialOverlay.prefab,TutorialOverlay/Panel/Dots,TutorialStepDots,normalSprite,cableado,-,-,ui_tutorial_step_dot_normal_01.png
ui_tutorial_step_dot_active_01.png,serialized_sprite_field,Assets/Prefabs/UI/TutorialOverlay.prefab,TutorialOverlay/Panel/Dots,TutorialStepDots,activeSprite,cableado,-,-,ui_tutorial_step_dot_active_01.png
ui_tutorial_step_dot_done_01.png,serialized_sprite_field,Assets/Prefabs/UI/TutorialOverlay.prefab,TutorialOverlay/Panel/Dots,TutorialStepDots,doneSprite,cableado,-,-,ui_tutorial_step_dot_done_01.png
ui_tutorial_btn_skip_normal_01.png,image+alfa_host,Assets/Prefabs/UI/TutorialOverlay.prefab,TutorialOverlay/SkipButton,Image,m_Sprite,cableado,0.18/0.2/0.28/0.9,-,ui_tutorial_btn_skip_normal_01.png
ui_tutorial_btn_skip_pressed_01.png,selectable_state,Assets/Prefabs/UI/TutorialOverlay.prefab,TutorialOverlay/SkipButton,Button,m_SpriteState.m_PressedSprite,cableado,0.18/0.2/0.28/0.9,1,ui_tutorial_btn_skip_pressed_01.png
ui_tutorial_btn_continue_normal_01.png,image+alfa_host,Assets/Prefabs/UI/TutorialOverlay.prefab,TutorialOverlay/Panel/CloseGroup/ContinueButton,Image,m_Sprite,cableado,0.16/0.55/0.35/0.95,-,ui_tutorial_btn_continue_normal_01.png
ui_tutorial_btn_continue_pressed_01.png,selectable_state,Assets/Prefabs/UI/TutorialOverlay.prefab,TutorialOverlay/Panel/CloseGroup/ContinueButton,Button,m_SpriteState.m_PressedSprite,cableado,0.16/0.55/0.35/0.95,1,ui_tutorial_btn_continue_pressed_01.png
ui_tutorial_btn_retry_normal_01.png,image+alfa_host,Assets/Prefabs/UI/TutorialOverlay.prefab,TutorialOverlay/RetryPanel/RetryButton,Image,m_Sprite,cableado,0.16/0.55/0.35/0.95,-,ui_tutorial_btn_retry_normal_01.png
ui_tutorial_btn_retry_pressed_01.png,selectable_state,Assets/Prefabs/UI/TutorialOverlay.prefab,TutorialOverlay/RetryPanel/RetryButton,Button,m_SpriteState.m_PressedSprite,cableado,0.16/0.55/0.35/0.95,1,ui_tutorial_btn_retry_pressed_01.png
ui_tutorial_panel_retry_01.png,image,Assets/Prefabs/UI/TutorialOverlay.prefab,TutorialOverlay/RetryPanel,Image,m_Sprite,cableado,0.08/0.09/0.13/0.98,-,ui_tutorial_panel_retry_01.png
ui_tutorial_icon_danger_01.png,image+alfa_host,Assets/Prefabs/UI/TutorialOverlay.prefab,TutorialOverlay/Panel/DangerStrip,Image,m_Sprite,cableado,1/0.7/0.2/0.12,-,ui_tutorial_icon_danger_01.png
ui_hud_btn_settings_pressed_01.png,selectable_state,Assets/Scenes/MainMenu.unity,Canvas/Profile/Settings,Button,m_SpriteState.m_PressedSprite,cableado,-,0,ui_hud_btn_settings_pressed_01.png
ui_hud_avatar_frame_player_01.png,image,-,-,-,-,sin_slot,-,-,
ui_hud_avatar_frame_ai_01.png,image,-,-,-,-,sin_slot,-,-,
ui_hud_score_pill_player_01.png,image,-,-,-,-,sin_slot,-,-,
ui_hud_score_pill_ai_01.png,image,-,-,-,-,sin_slot,-,-,
ui_hud_panel_player_01.png,image,-,-,-,-,sin_slot,-,-,
game_board_zone_veil_01.png,image+tinte_design,Assets/Scenes/Game.unity,TimeLineToStart/Canva/Canvas/Image/Image,Image,m_Sprite,cableado,0/0/0/0.29803923,-,game_board_zone_veil_01.png
ball_state_selected_ring_01.png,image,-,-,-,-,sin_slot,-,-,
ball_state_frozen_01.png,image,-,-,-,-,bloqueado,-,-,
ball_state_matched_01.png,image,-,-,-,-,sin_slot,-,-,
ui_menu_bg_panel_01.png,image,-,-,-,-,sin_slot,-,-,
ui_common_btn_primary_pressed_01.png,selectable_state,Assets/Scenes/MainMenu.unity,Canvas/Configuration/Contenedor/SubContainer/Button_Audio,Button,m_SpriteState.m_PressedSprite,cableado,1/1/1/1,1,ui_common_btn_primary_pressed_01.png
ui_common_btn_primary_disabled_01.png,selectable_state,Assets/Scenes/MainMenu.unity,Canvas/Configuration/Contenedor/SubContainer/Button_Name,Button,m_SpriteState.m_DisabledSprite,cableado,1/1/1/1,1,ui_common_btn_primary_disabled_01.png
ui_menu_btn_play_pressed_01.png,selectable_state,Assets/Scenes/MainMenu.unity,Canvas/Button_Play,Button,m_SpriteState.m_PressedSprite,cableado,-,0,ui_menu_btn_play_pressed_01.png
```

## Capturas (`Docs/art/captures/placeholders-v1/`)

24 PNG: **20 recortes** con el nombre de su pieza + **4 panorámicas** de host a 900×1900 (la
resolución de referencia del proyecto: `CanvasScaler` de todas las escenas es
`ScaleWithScreenSize 900×1900`, match 0).

Método: cámara del propio canvas (`Canvas.worldCamera`, `ScreenSpaceCamera`) → `RenderTexture`
900×1900 → `ReadPixels` → recorte a la `RectTransform` del nodo. Nada de `ScreenCapture`: la
resolución tiene que ser la del contrato, no la del Game view.

Tres decisiones del rig, declaradas porque afectan lo que se ve:

1. **`TutorialOverlay` es código, no un estado guardado.** En el prefab `Dim`, `Panel`,
   `CalloutLeft/Right`, `SkipButton`, `RetryPanel` y `DangerStrip` están inactivos y los prende
   `TutorialOverlay.cs` paso a paso. Cada recorte activa **sólo** la cadena de su nodo, y las tres
   `step_dot_*` se capturan con `TutorialStepDots.SetCurrent(1)` (dot 0 `done`, dot 1 `active`,
   dot 2 `normal`) — el estado real del componente, no una asignación a mano.
2. **`pressed`/`disabled` se fuerzan con `Selectable.DoStateTransition`** (vía reflexión: es
   `protected`), que es exactamente lo que hace el input. La captura de `pressed` muestra el sprite
   de `m_PressedSprite` y **ningún** tinte de color: es la prueba visual del costo de APW-4b.
3. **`MainMenu` sólo se ve bien en play mode**, y su panel `Configuration` no tiene *opener*
   cableado (`onClick` con 0 listeners en el engranaje, `RectTransform` fuera de pantalla): el rig
   corre en play mode y deja el panel en la posición que tendría abierto. No se guarda nada — el
   play mode no escribe la escena.

**Firma visual, en las capturas**: el marco `#FF00FF` y la etiqueta se ven en `bubble`, `skip`,
`panel_retry`, `btn_primary_*`, `play` y `settings`. En `continue`/`retry` el tinte del host
(`0.16/0.55/0.35`) **mata el magenta** (255×0.55 en G, 255×0.16 en R): el marco queda verde y la
firma no pasa el umbral de APA-2. Es la consecuencia medida de no normalizar hosts con luminancia
> 0.25 (APW-3); la pieza sigue siendo inconfundible por la placa `SIN ARTE`. En `dim` y `zone_veil`
> la firma no existe por diseño (`flat4`, `except: sin_firma`), y en `danger` (alfa 0.12) y en los
> dots (alfa 0.25/0.55) el alfa del host baja el magenta bajo el umbral sin cambiarlo de tono.

## Reversión

- **Antes de que llegue arte**: `git revert` del commit de cableado. `m_Sprite`, `m_Color` y
  `m_Transition` vuelven en un paso; el mapa no cambia porque sigue declarando lo mismo.
- **Después de que llegue arte**: no se revierte con `git revert` (pisaría los valores del arte).
  El mapa es la fuente: `tint_original` y `transition_original` restauran mecánicamente, y el
  checker vuelve a probar que el diff cae en la allowlist.
