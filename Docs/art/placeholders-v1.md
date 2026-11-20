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
| `captura` | PNG de `Docs/art/captures/placeholders-v1/`; vacío hasta el slice de capturas |

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

## Los dos `nodo=-` y por qué

`ui_common_btn_primary_pressed_01` y `ui_common_btn_primary_disabled_01` son las variantes de
estado del botón maestro `Button_UI` (124×25, border 7/6/5/7 — el mismo contrato que su REPLACE
`ui_common_btn_primary_normal_01`). No tienen un nodo propio: el slot es **el sprite maestro**,
y lo consumen los Selectables que lo referencian. El checker, para esas dos filas, cuenta las
referencias `m_SpriteState` al GUID de la pieza en vez de resolver un path.

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
ui_tutorial_dim_01.png,image+tinte_design,Assets/Prefabs/UI/TutorialOverlay.prefab,TutorialOverlay/Dim,Image,m_Sprite,cableado,0/0/0/0.72,-,
ui_tutorial_bubble_body_01.png,image,Assets/Prefabs/UI/TutorialOverlay.prefab,TutorialOverlay/Panel/Bubble,Image,m_Sprite,cableado,0.07/0.08/0.12/0.92,-,
ui_tutorial_bubble_tail_left_01.png,image,Assets/Prefabs/UI/TutorialOverlay.prefab,TutorialOverlay/Panel/Bubble/Tail,Image,m_Sprite,bloqueado,0.07/0.08/0.12/0.92,-,
ui_tutorial_caption_pill_01.png,image,Assets/Prefabs/UI/TutorialOverlay.prefab,TutorialOverlay/CalloutLeft/CaptionPill,Image,m_Sprite,bloqueado,0.07/0.08/0.12/0.9,-,
ui_tutorial_spotlight_ring_01.png,image,-,-,-,-,sin_slot,-,-,
ui_tutorial_spotlight_fill_01.png,image,-,-,-,-,bloqueado,-,-,
ui_tutorial_hand_tap_01.png,image,-,-,-,-,sin_slot,-,-,
ui_tutorial_hand_tap_02.png,image,-,-,-,-,sin_slot,-,-,
ui_tutorial_hand_tap_03.png,image,-,-,-,-,sin_slot,-,-,
ui_tutorial_hand_point_left_01.png,image,Assets/Prefabs/UI/TutorialOverlay.prefab,TutorialOverlay/CalloutLeft/Hand,Image,m_Sprite,cableado,1/1/1/0.85,-,
ui_tutorial_hand_point_right_01.png,image,Assets/Prefabs/UI/TutorialOverlay.prefab,TutorialOverlay/CalloutRight/Hand,Image,m_Sprite,cableado,1/1/1/0.85,-,
ui_tutorial_icon_star_01.png,image,-,-,-,-,sin_slot,-,-,
ui_tutorial_step_dot_normal_01.png,serialized_sprite_field,Assets/Prefabs/UI/TutorialOverlay.prefab,TutorialOverlay/Panel/Dots,TutorialStepDots,normalSprite,cableado,-,-,
ui_tutorial_step_dot_active_01.png,serialized_sprite_field,Assets/Prefabs/UI/TutorialOverlay.prefab,TutorialOverlay/Panel/Dots,TutorialStepDots,activeSprite,cableado,-,-,
ui_tutorial_step_dot_done_01.png,serialized_sprite_field,Assets/Prefabs/UI/TutorialOverlay.prefab,TutorialOverlay/Panel/Dots,TutorialStepDots,doneSprite,cableado,-,-,
ui_tutorial_btn_skip_normal_01.png,image+alfa_host,Assets/Prefabs/UI/TutorialOverlay.prefab,TutorialOverlay/SkipButton,Image,m_Sprite,cableado,0.18/0.2/0.28/0.9,-,
ui_tutorial_btn_skip_pressed_01.png,selectable_state,Assets/Prefabs/UI/TutorialOverlay.prefab,TutorialOverlay/SkipButton,Button,m_SpriteState.m_PressedSprite,cableado,0.18/0.2/0.28/0.9,1,
ui_tutorial_btn_continue_normal_01.png,image+alfa_host,Assets/Prefabs/UI/TutorialOverlay.prefab,TutorialOverlay/Panel/CloseGroup/ContinueButton,Image,m_Sprite,cableado,0.16/0.55/0.35/0.95,-,
ui_tutorial_btn_continue_pressed_01.png,selectable_state,Assets/Prefabs/UI/TutorialOverlay.prefab,TutorialOverlay/Panel/CloseGroup/ContinueButton,Button,m_SpriteState.m_PressedSprite,cableado,0.16/0.55/0.35/0.95,1,
ui_tutorial_btn_retry_normal_01.png,image+alfa_host,Assets/Prefabs/UI/TutorialOverlay.prefab,TutorialOverlay/RetryPanel/RetryButton,Image,m_Sprite,cableado,0.16/0.55/0.35/0.95,-,
ui_tutorial_btn_retry_pressed_01.png,selectable_state,Assets/Prefabs/UI/TutorialOverlay.prefab,TutorialOverlay/RetryPanel/RetryButton,Button,m_SpriteState.m_PressedSprite,cableado,0.16/0.55/0.35/0.95,1,
ui_tutorial_panel_retry_01.png,image,Assets/Prefabs/UI/TutorialOverlay.prefab,TutorialOverlay/RetryPanel,Image,m_Sprite,cableado,0.08/0.09/0.13/0.98,-,
ui_tutorial_icon_danger_01.png,image+alfa_host,Assets/Prefabs/UI/TutorialOverlay.prefab,TutorialOverlay/Panel/DangerStrip,Image,m_Sprite,cableado,1/0.7/0.2/0.12,-,
ui_hud_btn_settings_pressed_01.png,selectable_state,Assets/Scenes/MainMenu.unity,Canvas/Profile/Settings,Button,m_SpriteState.m_PressedSprite,cableado,-,0,
ui_hud_avatar_frame_player_01.png,image,-,-,-,-,sin_slot,-,-,
ui_hud_avatar_frame_ai_01.png,image,-,-,-,-,sin_slot,-,-,
ui_hud_score_pill_player_01.png,image,-,-,-,-,sin_slot,-,-,
ui_hud_score_pill_ai_01.png,image,-,-,-,-,sin_slot,-,-,
ui_hud_panel_player_01.png,image,-,-,-,-,sin_slot,-,-,
game_board_zone_veil_01.png,image+tinte_design,Assets/Scenes/Game.unity,TimeLineToStart/Canva/Canvas/Image/Image,Image,m_Sprite,cableado,0/0/0/0.29803923,-,
ball_state_selected_ring_01.png,image,-,-,-,-,sin_slot,-,-,
ball_state_frozen_01.png,image,-,-,-,-,bloqueado,-,-,
ball_state_matched_01.png,image,-,-,-,-,sin_slot,-,-,
ui_menu_bg_panel_01.png,image,-,-,-,-,sin_slot,-,-,
ui_common_btn_primary_pressed_01.png,selectable_state,Assets/Sprites/Button_UI.png,-,Button,m_SpriteState.m_PressedSprite,cableado,-,1,
ui_common_btn_primary_disabled_01.png,selectable_state,Assets/Sprites/Button_UI.png,-,Button,m_SpriteState.m_DisabledSprite,cableado,-,1,
ui_menu_btn_play_pressed_01.png,selectable_state,Assets/Scenes/MainMenu.unity,Canvas/Button_Play,Button,m_SpriteState.m_PressedSprite,cableado,-,0,
```

## Reversión

- **Antes de que llegue arte**: `git revert` del commit de cableado. `m_Sprite`, `m_Color` y
  `m_Transition` vuelven en un paso; el mapa no cambia porque sigue declarando lo mismo.
- **Después de que llegue arte**: no se revierte con `git revert` (pisaría los valores del arte).
  El mapa es la fuente: `tint_original` y `transition_original` restauran mecánicamente, y el
  checker vuelve a probar que el diff cae en la allowlist.
