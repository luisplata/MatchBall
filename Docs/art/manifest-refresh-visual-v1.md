# Manifiesto de arte — v1

**Cambio**: `refresh-visual-cliente` · **Repo**: MatchBall (Unity 6000.3.10f1) · **Base**: `e941c14`
**Fecha del barrido**: 2026-11-11 · **Store**: engram `sdd/refresh-visual-cliente/art-manifest`

Este documento es la orden de entrega de arte. Es la lista CERRADA de piezas, nombres, pixeles y
estados: si una pieza no está acá, no entra en este cambio.

---

## 1. Convención de nombres (RT-7)

```
<sistema> _ <pantalla|elemento> _ <pieza> [ _ <estado> ] _ <variante de 2 dígitos>.png
```

| Campo | Valores |
|---|---|
| `sistema` | `ui` · `game` · `ball` · `hero` |
| `pantalla\|elemento` | `tutorial`, `hud`, `pass`, `menu`, `login`, `matching`, `result`, `store`, `common`, `board`, `normal`, `power`, `state`, `rojo` |
| `pieza` | el elemento: `bubble`, `hand`, `dot`, `btn`, `icon`, `card`, `shadow`, `backdrop`… |
| `estado` (opcional) | `normal` · `pressed` · `active` · `done` · `disabled` · `locked` · `claimed` |
| `variante` | SIEMPRE 2 dígitos: `_01`, `_02` |

Reglas duras:

- minúsculas ASCII, `snake_case`, sin espacios, sin acentos, sin UUID.
- todas las piezas terminan en `_01` (o `_02` cuando hay dos variantes de la misma pieza).
- el `estado` va ANTES de la variante (`ui_tutorial_btn_skip_pressed_01`), no después.
- la forma operativa del contrato es el regex de la auditoría: **100 % de las 121 piezas lo pasan**.

---

## 2. Conteo

| Grupo | Piezas | REPLACE | NEW |
|---|---|---|---|
| A — Tutorial / onboarding | 23 | 0 | 23 |
| B — HUD de partida | 12 | 6 | 6 |
| C — Tablero / gameplay (mundo, PPU 100) | 14 | 13 | 1 |
| D — Bolas | 15 | 12 | 3 |
| E — BallPass / pases | 14 | 14 | 0 |
| F — Menú / login / matching / resultados / store | 36 | 33 | 3 |
| G — Héroes | 7 | 7 | 0 |
| **Total** | **121** | **85** | **36** |

`REPLACE` = la pieza ya existe con path y `.meta` en el proyecto: se reemplazan **los bytes del PNG**,
mismo path, mismo `.meta`, mismo GUID. `NEW` = la pieza no existe: hay que dibujarla y **nace con GUID
nuevo** (no hay `.meta` que conservar). El grupo A no tiene ninguna REPLACE: las 5 filas que antes se
contaban como REPLACE sin archivo son NEW (ver la nota al pie de la §3).

---

## 3. Piezas

Columnas: **archivo** (`<nombre>.png`) · **px** actuales leídos del archivo · **estado** · **9-slice**
(bordes en px, orden izquierda/abajo/derecha/arriba) · **nota** (incluye el archivo de origen de cada REPLACE).

### A — Tutorial / onboarding (23)

| archivo | px | estado | 9-slice | nota / origen |
|---|---|---|---|---|
| `ui_tutorial_dim_01.png` | TBD | NEW | — | canvas de entrega 4×4, BLANCO plano para tintar (hoy la Image no tiene sprite: negro α 0.72). Nace con GUID nuevo: no hay archivo ni `.meta` que conservar |
| `ui_tutorial_bubble_body_01.png` | TBD | NEW | 48/56/48/56 | cuerpo de la burbuja = el panel 400×520 (compacto 320×240); marco 9-slice |
| `ui_tutorial_bubble_tail_left_01.png` | TBD | NEW | — | cola apuntando al tablero del jugador; el panel vive a su derecha. Sin versión _right |
| `ui_tutorial_caption_pill_01.png` | TBD | NEW | 24/16/24/16 | fondo del < IZQUIERDA / DERECHA > bajo la mano |
| `ui_tutorial_spotlight_ring_01.png` | TBD | NEW | — | px propuesto 512×512. Se entrega pero NO se cablea en este cambio (falta masking) |
| `ui_tutorial_spotlight_fill_01.png` | TBD | NEW | — | px propuesto 512×512, radial α≤0.12. NO se cablea |
| `ui_tutorial_hand_tap_01.png` | TBD | NEW | — | px propuesto 160×200, loop 1/3; pivote en la punta del dedo |
| `ui_tutorial_hand_tap_02.png` | TBD | NEW | — | px propuesto 160×200, loop 2/3 |
| `ui_tutorial_hand_tap_03.png` | TBD | NEW | — | px propuesto 160×200, loop 3/3 |
| `ui_tutorial_hand_point_left_01.png` | TBD | NEW | — | px propuesto 160×200, pose estática: la que se cablea hoy |
| `ui_tutorial_hand_point_right_01.png` | TBD | NEW | — | px propuesto 160×200. Espejo REAL: NUNCA scale.x = -1 (invertiría sombra y badge) |
| `ui_tutorial_icon_star_01.png` | TBD | NEW | — | px propuesto 64×64, badge de la mano |
| `ui_tutorial_step_dot_normal_01.png` | TBD | NEW | — | px propuesto 28×28, punto pendiente |
| `ui_tutorial_step_dot_active_01.png` | TBD | NEW | — | px propuesto 28×28, punto actual |
| `ui_tutorial_step_dot_done_01.png` | TBD | NEW | — | px propuesto 28×28, punto completado |
| `ui_tutorial_btn_skip_normal_01.png` | TBD | NEW | 12/12/12/12 | canvas de entrega 120×56 (hoy Image plano de color, sin sprite, dibujado 180×80). Nace con GUID nuevo |
| `ui_tutorial_btn_skip_pressed_01.png` | TBD | NEW | 12/12/12/12 | mismo canvas que normal |
| `ui_tutorial_btn_continue_normal_01.png` | TBD | NEW | 12/12/12/12 | canvas de entrega 280×84 (hoy Image plano verde (0.16, 0.55, 0.35), sin sprite). Nace con GUID nuevo |
| `ui_tutorial_btn_continue_pressed_01.png` | TBD | NEW | 12/12/12/12 | mismo canvas que normal |
| `ui_tutorial_btn_retry_normal_01.png` | TBD | NEW | 12/12/12/12 | canvas de entrega 220×64 (hoy Image plano, sin sprite). Nace con GUID nuevo |
| `ui_tutorial_btn_retry_pressed_01.png` | TBD | NEW | 12/12/12/12 | mismo canvas que normal |
| `ui_tutorial_panel_retry_01.png` | TBD | NEW | 32/32/32/32 | canvas de entrega 780×420 (hoy Image plano (0.08, 0.09, 0.13, 0.98), sin sprite). Nace con GUID nuevo |
| `ui_tutorial_icon_danger_01.png` | TBD | NEW | — | px propuesto 48×48, acompaña «Si sigues así…» |

### B — HUD de partida (12)

| archivo | px | estado | 9-slice | nota / origen |
|---|---|---|---|---|
| `ui_hud_counter_pill_01.png` | 46×22 | REPLACE | — | pill del contador · origen: `Assets/Sprites/Container_Counts.png` |
| `ui_hud_icon_coin_01.png` | 28×28 | REPLACE | — | Icon_Coins.png (72×40) queda huérfano y NO se usa para esta pieza · origen: `Assets/Sprites/Currenci.png` |
| `ui_hud_icon_cups_01.png` | 72×40 | REPLACE | — | origen: `Assets/Sprites/Icon_Cups.png` |
| `ui_hud_btn_settings_normal_01.png` | 28×28 | REPLACE | — | origen: `Assets/Sprites/Btn_settings.png` |
| `ui_hud_btn_settings_pressed_01.png` | TBD | NEW | — | mismo canvas 28×28 |
| `ui_hud_avatar_frame_player_01.png` | TBD | NEW | — | px propuesto 160×160. Hoy el Avatar usa BALLPASS0 sin marco |
| `ui_hud_avatar_frame_ai_01.png` | TBD | NEW | — | px propuesto 160×160. Hoy usa BALLPASS11 |
| `ui_hud_score_pill_player_01.png` | TBD | NEW | — | px propuesto 220×72, scoreboard Player/AI |
| `ui_hud_score_pill_ai_01.png` | TBD | NEW | — | px propuesto 220×72 |
| `ui_hud_btn_powerup_slot_normal_01.png` | 165×36 | REPLACE | — | origen: `Assets/Sprites/PowerUpsNormal.png` |
| `ui_hud_btn_powerup_slot_active_01.png` | 165×36 | REPLACE | — | origen: `Assets/Sprites/PowerUpsClicked.png` |
| `ui_hud_panel_player_01.png` | TBD | NEW | — | px propuesto 600×96 |

### C — Tablero / gameplay (mundo, PPU 100) (14)

| archivo | px | estado | 9-slice | nota / origen |
|---|---|---|---|---|
| `game_board_balance_bar_left_01.png` | 9×134 | REPLACE | — | PPU 100: no cambiar px (mueve el alineado de los 33 slots) · origen: `Assets/Sprites/V3_images/balanza 2.png` |
| `game_board_balance_bar_right_01.png` | 223×109 | REPLACE | — | PPU 100 · origen: `Assets/Sprites/V3_images/balanza 3.png` |
| `game_board_balance_base_01.png` | 305×118 | REPLACE | — | hoy huérfana: esta fila la PROMUEVE (ver triaje) · origen: `Assets/Sprites/V3_images/balanza 1.png` |
| `game_board_line_01.png` | 305×10 | REPLACE | — | PPU 100 · origen: `Assets/Sprites/V3_images/LineaBalanza.png` |
| `game_board_line_shadow_01.png` | 352×54 | REPLACE | — | PPU 100 · origen: `Assets/Sprites/V3_images/LineaBalanzaSombra.png` |
| `game_board_shadow_long_01.png` | 352×164 | REPLACE | — | hoy huérfana: esta fila la PROMUEVE (ver triaje) · origen: `Assets/Sprites/V3_images/sombra 1.png` |
| `game_board_shadow_mid_01.png` | 276×170 | REPLACE | — | PPU 100 · origen: `Assets/Sprites/V3_images/sombra 2.png` |
| `game_board_shadow_short_01.png` | 266×152 | REPLACE | — | PPU 100 · origen: `Assets/Sprites/V3_images/sombra 3.png` |
| `game_board_arrow_swap_01.png` | 29×142 | REPLACE | — | el Switch del tablero · origen: `Assets/Sprites/V3_images/flechita 11.png` |
| `game_board_arrow_swap_alt_01.png` | 29×142 | REPLACE | — | origen: `Assets/Sprites/V3_images/flechita 2.png` |
| `game_board_arrow_shadow_01.png` | 59×172 | REPLACE | — | PPU 100 · origen: `Assets/Sprites/V3_images/sombra flechita 1.png` |
| `game_board_arrow_shadow_02.png` | 59×172 | REPLACE | — | PPU 100 · origen: `Assets/Sprites/V3_images/sombra flechita 2.png` |
| `game_board_backdrop_01.png` | 1220×2712 | REPLACE | — | hoy JPG: entregar PNG al MISMO px. El import (maxTextureSize 2048) lo downsamplea · origen: `Assets/Sprites/Matchballance.jpg` |
| `game_board_zone_veil_01.png` | TBD | NEW | — | px propuesto 4×4; reemplaza el sprite built-in del velo verde α 0.176 |

### D — Bolas (15)

| archivo | px | estado | 9-slice | nota / origen |
|---|---|---|---|---|
| `ball_normal_red_01.png` | 416×416 | REPLACE | — | canvas de mundo: no cambiar px · origen: `Assets/Sprites/ball_red.png` |
| `ball_normal_blue_01.png` | 417×416 | REPLACE | — | canvas de mundo · origen: `Assets/Sprites/ball_blue.png` |
| `ball_normal_green_01.png` | 417×417 | REPLACE | — | canvas de mundo · origen: `Assets/Sprites/ball_green.png` |
| `ball_normal_orange_01.png` | 416×416 | REPLACE | — | canvas de mundo · origen: `Assets/Sprites/ball_orange.png` |
| `ball_normal_purple_01.png` | 416×416 | REPLACE | — | canvas de mundo · origen: `Assets/Sprites/ball_violet.png` |
| `ball_normal_yellow_01.png` | 417×417 | REPLACE | — | canvas de mundo · origen: `Assets/Sprites/bola amarilla.png` |
| `ball_shadow_01.png` | 169×169 | REPLACE | — | sombra de la bola · origen: `Assets/Sprites/sombra.png` |
| `ball_power_ice_01.png` | 192×197 | REPLACE | — | address `POWERUP_ICE`; reemplazo in-place · origen: `Assets/Sprites/BALLPASS/BOLAS GIF/BOLAS ESPECIALES.png` |
| `ball_power_lead_01.png` | 206×204 | REPLACE | — | address `POWERUP_LEAD` · origen: `Assets/Sprites/BALLPASS/BOLAS GIF/BOLAS ESPECIALES1.png` |
| `ball_power_joker_01.png` | 191×191 | REPLACE | — | address `POWERUP_JOKER` · origen: `Assets/Sprites/BALLPASS/BOLAS GIF/BOLAS ESPECIALES2.png` |
| `ball_power_ray_01.png` | 192×192 | REPLACE | — | address `POWERUP_RAY` · origen: `Assets/Sprites/BALLPASS/BOLAS GIF/BOLAS ESPECIALES3.png` |
| `ball_power_shell_01.png` | 192×192 | REPLACE | — | sin address · origen: `Assets/Sprites/BALLPASS/BOLAS GIF/BOLAS ESPECIALES7.png` |
| `ball_state_selected_ring_01.png` | TBD | NEW | — | px propuesto 480×480; hoy no hay estado visual de selección |
| `ball_state_frozen_01.png` | TBD | NEW | — | px propuesto 416×416, overlay de congelado |
| `ball_state_matched_01.png` | TBD | NEW | — | px propuesto 96×96, destello al juntar 3 |

### E — BallPass / pases (14)

| archivo | px | estado | 9-slice | nota / origen |
|---|---|---|---|---|
| `ui_pass_banner_01.png` | 4096×1596 | REPLACE | — | hoy 4096 px de ancho y el import lo baja a 2048: entregar 2048 · origen: `Assets/Sprites/ballpassBanner.png` |
| `ui_pass_icon_pass_01.png` | 129×73 | REPLACE | — | origen: `Assets/Sprites/Pase.png` |
| `ui_pass_separator_page_01.png` | 360×8 | REPLACE | — | origen: `Assets/Sprites/Separador_ballpass.png` |
| `ui_pass_separator_level_01.png` | 360×29 | REPLACE | — | origen: `Assets/Sprites/Separador_ballpass_levels.png` |
| `ui_pass_reward_gold_01.png` | 1550×1028 | REPLACE | — | address `gold` · origen: `Assets/Sprites/BALLPASS/BALLPASS1.png` |
| `ui_pass_reward_avatar_01.png` | 2156×2156 | REPLACE | — | address `profileAvatar_1` · **2156 px de lado > `maxTextureSize: 2048`**: el import lo downsamplea (~2048×2048). Entregar **2048×2048** (el mismo canvas que ve el juego hoy), no 2156 · origen: `Assets/Sprites/BALLPASS/BALLPASS4.png` |
| `ui_pass_reward_avatar_02.png` | 1080×2151 | REPLACE | — | address `profileAvatar_2` · canvas vertical atípico: confirmar con el cliente antes de dibujar · **2151 px de alto > 2048**: el import lo downsamplea (~1028×2048). Entregar **1028×2048** · origen: `Assets/Sprites/BALLPASS/BALLPASS5.png` |
| `ui_pass_reward_picture_01.png` | 1578×1578 | REPLACE | — | address `profilePicture_1` y ADEMÁS avatar del HUD (rol doble, ver §6) · origen: `Assets/Sprites/BALLPASS/BALLPASS0.png` |
| `ui_pass_reward_picture_02.png` | 1571×1571 | REPLACE | — | address `profilePicture_2` · origen: `Assets/Sprites/BALLPASS/BALLPASS2.png` |
| `ui_pass_reward_background_01.png` | 2759×1120 | REPLACE | — | address `profileBackground_1` · **2759 px de ancho > `maxTextureSize: 2048`**: el import lo downsamplea (~2048×831). Entregar **2048×831** · origen: `Assets/Sprites/BALLPASS/BALLPASS3.png` |
| `ui_pass_reward_background_02.png` | 2750×1116 | REPLACE | — | address `profileBackground_2` · **2750 px de ancho > 2048**: el import lo downsamplea (~2048×831). Entregar **2048×831** · origen: `Assets/Sprites/BALLPASS/BALLPASS7.png` |
| `ui_pass_state_check_01.png` | 23×26 | REPLACE | — | origen: `Assets/Sprites/Check.png` |
| `ui_pass_state_lock_01.png` | 23×26 | REPLACE | — | hoy huérfana: esta fila la PROMUEVE (ver triaje) · origen: `Assets/Sprites/CheckLock.png` |
| `ui_pass_btn_return_01.png` | 200×35 | REPLACE | — | origen: `Assets/Sprites/Btn_Return.png` |

### F — Menú / login / matching / resultados / store (36)

| archivo | px | estado | 9-slice | nota / origen |
|---|---|---|---|---|
| `ui_common_btn_primary_normal_01.png` | 124×25 | REPLACE | 7/6/5/7 | 9-slice ya declarado en el .meta: conservarlo · origen: `Assets/Sprites/Button_UI.png` |
| `ui_common_btn_primary_pressed_01.png` | TBD | NEW | 7/6/5/7 | mismo canvas y borde que normal |
| `ui_common_btn_primary_disabled_01.png` | TBD | NEW | 7/6/5/7 | mismo canvas y borde que normal |
| `ui_common_btn_large_01.png` | 360×67 | REPLACE | — | sin 9-slice hoy · origen: `Assets/Sprites/Button_BG.png` |
| `ui_common_panel_container_01.png` | 248×194 | REPLACE | 14/11/11/12 | 9-slice ya declarado en el .meta: conservarlo · origen: `Assets/Sprites/Contenedor_UI.png` |
| `ui_common_panel_subcontainer_01.png` | 312×197 | REPLACE | 12/11/8/7 | 9-slice ya declarado en el .meta: conservarlo · origen: `Assets/Sprites/SubContenedor_UI.png` |
| `ui_common_card_01.png` | 152×206 | REPLACE | — | compartida BallPass + Store (rol doble, ver §6) · origen: `Assets/Sprites/Card.png` |
| `ui_common_divider_01.png` | 43×455 | REPLACE | — | hoy huérfana: esta fila la PROMUEVE (ver triaje) · origen: `Assets/Sprites/Separador.png` |
| `ui_common_logo_game_01.png` | 340×63 | REPLACE | — | origen: `Assets/Sprites/GameTitle.png` |
| `ui_common_logo_title_01.png` | 1399×208 | REPLACE | — | origen: `Assets/Sprites/title.png` |
| `ui_common_loader_01.png` | 180×180 | REPLACE | — | frame de la animación de carga · origen: `Assets/Sprites/Loader anim.png` |
| `ui_menu_bg_01.png` | 1087×1928 | REPLACE | — | hoy JPG: entregar PNG al MISMO px · origen: `Assets/Sprites/BG.jpeg` |
| `ui_menu_btn_play_normal_01.png` | 140×48 | REPLACE | — | origen: `Assets/Sprites/Btn_Play.png` |
| `ui_menu_btn_play_pressed_01.png` | TBD | NEW | — | mismo canvas 140×48 |
| `ui_menu_btn_home_01.png` | 60×44 | REPLACE | — | origen: `Assets/Sprites/Home.png` |
| `ui_menu_btn_store_01.png` | 60×44 | REPLACE | — | origen: `Assets/Sprites/Store.png` |
| `ui_menu_btn_ballpass_01.png` | 60×44 | REPLACE | — | origen: `Assets/Sprites/Ballpass.png` |
| `ui_menu_btn_friends_01.png` | 48×48 | REPLACE | — | origen: `Assets/Sprites/Btn_Friends.png` |
| `ui_menu_btn_help_01.png` | 28×28 | REPLACE | — | origen: `Assets/Sprites/Btn_Help.png` |
| `ui_menu_btn_back_01.png` | 28×29 | REPLACE | — | origen: `Assets/Sprites/Btn_Back.png` |
| `ui_menu_btn_close_01.png` | 24×24 | REPLACE | — | origen: `Assets/Sprites/Close_Button.png` |
| `ui_menu_tab_rules_normal_01.png` | 165×36 | REPLACE | — | origen: `Assets/Sprites/RulesNormal.png` |
| `ui_menu_tab_rules_active_01.png` | 165×36 | REPLACE | — | origen: `Assets/Sprites/RulesClicked.png` |
| `ui_menu_tab_powerups_normal_01.png` | 165×36 | REPLACE | — | misma textura que carga el slot del HUD: un archivo, dos roles · origen: `Assets/Sprites/PowerUpsNormal.png` |
| `ui_menu_tab_powerups_active_01.png` | 165×36 | REPLACE | — | misma textura que carga el slot del HUD: un archivo, dos roles · origen: `Assets/Sprites/PowerUpsClicked.png` |
| `ui_menu_avatar_player_01.png` | 1344×768 | REPLACE | — | origen: `Assets/Sprites/PlayerAvatar.png` |
| `ui_menu_avatar_npc_01.png` | 1024×1024 | REPLACE | — | origen: `Assets/Sprites/NpcAvatar.png` |
| `ui_menu_panel_profile_01.png` | 1920×1080 | REPLACE | — | origen: `Assets/Sprites/Heroes/BGProfile.png` |
| `ui_menu_picture_profile_01.png` | 48×48 | REPLACE | — | origen: `Assets/Sprites/PictureProfile.png` |
| `ui_menu_rules_illustration_01.png` | 1024×1024 | REPLACE | — | origen: `Assets/Sprites/profileRules.png` |
| `ui_menu_icon_vs_01.png` | 31×18 | REPLACE | — | origen: `Assets/Sprites/VS.png` |
| `ui_login_bg_01.png` | 1024×1024 | REPLACE | — | hoy JPG: entregar PNG al MISMO px · origen: `Assets/Sprites/BG_V2.jpg` |
| `ui_matching_bg_01.png` | 2048×2048 | REPLACE | — | hoy JPG: entregar PNG al MISMO px · origen: `Assets/Sprites/BG_Search_Single.jpeg` |
| `ui_result_win_01.png` | 360×800 | REPLACE | — | origen: `Assets/Sprites/YouWin.png` |
| `ui_result_lose_01.png` | 360×800 | REPLACE | — | origen: `Assets/Sprites/GameOver.png` |
| `ui_store_placeholder_01.png` | 176×189 | REPLACE | — | vive FUERA de Assets/Sprites; referenciado por Prefabs/Store/StoreElement.prefab · origen: `Assets/StoreExample.png` |

### G — Héroes (7)

| archivo | px | estado | 9-slice | nota / origen |
|---|---|---|---|---|
| `hero_rojo_portrait_idle_01.png` | 1024×1024 | REPLACE | — | origen: `Assets/Sprites/Heroes/rojo_idle.png` |
| `hero_rojo_portrait_happy_01.png` | 1024×1024 | REPLACE | — | origen: `Assets/Sprites/Heroes/rojo_alegre.png` |
| `hero_rojo_portrait_angry_01.png` | 1024×1024 | REPLACE | — | origen: `Assets/Sprites/Heroes/rojo_enojo.png` |
| `hero_rojo_portrait_determined_01.png` | 1024×1024 | REPLACE | — | origen: `Assets/Sprites/Heroes/rojo_determinacion.png` |
| `hero_rojo_portrait_surprised_01.png` | 1024×1024 | REPLACE | — | origen: `Assets/Sprites/Heroes/rojo_sorpresa.png` |
| `hero_rojo_banner_01.png` | 1344×768 | REPLACE | — | origen: `Assets/Sprites/Heroes/HeroeRojo.png` |
| `hero_dialog_bg_01.png` | 1344×768 | REPLACE | — | hoy JPG: entregar PNG al MISMO px · origen: `Assets/Sprites/Heroes/conversacion_bg_1.jpg` |

**Nota de reclasificación (verify #3703, W-3 / RT-8).** Cinco filas del grupo A —
`ui_tutorial_dim_01`, `ui_tutorial_btn_skip_normal_01`, `ui_tutorial_btn_continue_normal_01`,
`ui_tutorial_btn_retry_normal_01` y `ui_tutorial_panel_retry_01` — se contaban como REPLACE con una
marca †. Hoy esas `Image` del prefab tienen `m_Sprite: {fileID: 0}` (color plano) y **no existe ni el
path ni el `.meta`** que RT-9 manda conservar. Operativamente son NEW, así que se reclasificaron como
tales: su columna **px es TBD** (el canvas de entrega va en la nota, como en todas las NEW) y
**nacerán con GUID nuevo**. Con eso el reparto del §2 queda en **85 REPLACE / 36 NEW**: las 85 REPLACE
restantes resolvieron todas a un PNG/JPEG en disco y su px sale del header real del archivo.

---

## 4. Spec de entrega (RT-10)

| Requisito | Valor |
|---|---|
| Formato | PNG-24 + canal alfa (RGBA8). **Nada de JPG**: hoy hay 5 JPG a reemplazar (`BG.jpeg`, `Matchballance.jpg`, `BG_V2.jpg`, `BG_Search_Single.jpeg`, `Heroes/conversacion_bg_1.jpg`) |
| Color | sRGB, sin perfil ICC embebido, sin alpha premultiplicado |
| PPU | **100** en todo el proyecto (verificado en los `.meta`) |
| Canvas de mundo | `game_*` y `ball_*` conservan **exactamente** el px actual: las bolas miden 416/417 px = 4.16 u; entregar 512 px cambia el tamaño de mundo y rompe el alineado con los 33 slots (0.6 u) y los `BoxCollider2D` |
| 9-slice | obligatorio declararlo por escrito en todo frame/botón/panel, con bordes en px |
| Tamaño máximo | **2048 px por lado** (el importador tiene `maxTextureSize: 2048` en todos los `.meta` verificados). Las **6 piezas REPLACE que hoy exceden el techo** —`game_board_backdrop_01`, `ui_pass_banner_01` y las 4 de BallPass— llevan en la §3 el tamaño exacto que el cliente debe entregar, porque el importador las downsamplea igual (verify #3703, W-4) |
| Un archivo por pieza | nada de maestros (`PRUEBA.png` 4266×4267 y `BALLPASS.png` 6835×2664 hoy violan esto) |
| Entrega | carpeta plana por sistema (`ui/`, `game/`, `ball/`, `hero/`), con `_01` en todas las piezas |

### 9-slice ya existente en el proyecto (verificado en los `.meta`)

| Sprite | `spriteBorder` (x/y/z/w) | Pieza del manifiesto |
|---|---|---|
| `Button_UI.png` | 7 / 6 / 5 / 7 | `ui_common_btn_primary_*` |
| `Contenedor_UI.png` | 14 / 11 / 11 / 12 | `ui_common_panel_container_01` |
| `SubContenedor_UI.png` | 12 / 11 / 8 / 7 | `ui_common_panel_subcontainer_01` |

Son **3**, no 4: el resto de los `.meta` del proyecto tiene `spriteBorder: {0,0,0,0}`. Los bordes
declarados para las piezas `ui_tutorial_*` de este documento son NUEVOS (contrato de entrega, todavía
no hay archivo que los tenga).

---

## 5. Regla de reemplazo (RT-9)

**Reemplazo in-place, siempre**: se pisan los BYTES del `.png`, se conserva el path, el nombre y el
`.meta`. Nunca borrar-y-reimportar, nunca `git mv` en el mismo commit que un cambio de arte. Un renombre
mueve el `.meta` junto al `.png`, en un commit dedicado, para que sobrevivan las **89 referencias por
GUID** y las **11 addresses** de Addressables.

---

## 6. Roles dobles (sin filas extra: RT-8 fija 121 exactas)

| Pieza | Roles | Decisión |
|---|---|---|
| `ui_pass_reward_picture_01` (`BALLPASS0.png`) | recompensa de BallPass + avatar del HUD | **una sola pieza**; el HUD la dibuja con `Image` + máscara circular. Si el cliente quiere arte distinto: `ui_hud_avatar_player_art_01` en una v2 |
| `ui_common_card_01` (`Card.png`) | card de BallPass + card del Store | **una sola pieza** compartida |
| `ui_hud_btn_powerup_slot_*` / `ui_menu_tab_powerups_*` | slot del HUD + tab del menú | una sola textura, dos consumidores |

---

## 7. Fuera de alcance

- **Tema carnaval** (`theme_*`, 18 piezas): es cotización, no alcance. Con ese bloque aprobado el
  manifiesto pasa a 139 piezas.
- Reskin de todas las pantallas: acá se NOMBRAN, pintarlas es otro cambio.
- `ui_tutorial_spotlight_*` se entrega pero no se cablea (falta masking en el proyecto).

---

## 8. Auditoría de este documento

- **px reales**: leídos del header de cada archivo (PNG IHDR bytes 16-24; JPEG SOF), no copiados de la
  propuesta. Las **85 REPLACE** con archivo de origen coinciden con el archivo que existe en el repo.
- **conteo**: 121 filas · **85 REPLACE** · **36 NEW** · 0 duplicados (verify #3703, W-3).
- **nombres**: `^[a-z0-9]+(_[a-z0-9]+)+_[0-9]{2}\.png$` → 121/121 pasan.
- **9-slice**: bordes de `Button_UI`, `Contenedor_UI`, `SubContenedor_UI` leídos de los `.meta`; son los
  únicos 3 no nulos del proyecto.
