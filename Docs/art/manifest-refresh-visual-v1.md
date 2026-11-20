# Manifiesto de arte — v1

**Cambio**: `refresh-visual-cliente` · **Repo**: MatchBall (Unity 6000.3.10f1) · **Base**: `e941c14`
**Fecha del barrido**: 2026-11-11 · **Store**: engram `sdd/refresh-visual-cliente/art-manifest`
**Revisión v2**: 2026-11-20 · **Cambio**: `organizar-pantallas-para-arte` · corrección de contrato:
Joker/Lead (§3), pieza 122 (§2, §3-F), 10 rects congelados (§3), `px_entrega` de las 6 > 2048 (§4),
encuadre de fondos y `HeroList.asset` (§6) y tabla de verdad §9.

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
- la forma operativa del contrato es el regex de la auditoría: **100 % de las 122 piezas lo pasan**.

---

## 2. Conteo

| Grupo | Piezas | REPLACE | NEW |
|---|---|---|---|
| A — Tutorial / onboarding | 23 | 0 | 23 |
| B — HUD de partida | 12 | 6 | 6 |
| C — Tablero / gameplay (mundo, PPU 100) | 14 | 13 | 1 |
| D — Bolas | 15 | 12 | 3 |
| E — BallPass / pases | 14 | 14 | 0 |
| F — Menú / login / matching / resultados / store | 37 | 33 | 4 |
| G — Héroes | 7 | 7 | 0 |
| **Total** | **122** | **85** | **37** |

La pieza 122 es `ui_menu_bg_panel_01.png` (grupo F, NEW): arte propio del panel `MainMenu/Contenedor`.
Antes de la v2 el manifiesto cerraba en 121 / 85 / 36; el panel se atendía recortando `ui_menu_bg_01` y el
recorte no sobrevive al cover-fit (ver §6).

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
| `ball_power_lead_01.png` | 191×191 | REPLACE | — | address `POWERUP_LEAD` (guid `8c04cf6a…` = `BOLAS ESPECIALES2.png`), consumida por `PowerUpLeadBall.prefab` (`id: POWERUP_LEAD`; `m_Sprite` en la línea 126) · origen: `Assets/Sprites/BALLPASS/BOLAS GIF/BOLAS ESPECIALES2.png` |
| `ball_power_joker_01.png` | 206×204 | REPLACE | — | address `POWERUP_JOKER` (guid `bc6b9068…` = `BOLAS ESPECIALES1.png`), consumida por `PowerUpJoker.prefab` (`id: POWERUP_JOKER`; `m_Sprite` en la línea 126) · origen: `Assets/Sprites/BALLPASS/BOLAS GIF/BOLAS ESPECIALES1.png` |
| `ball_power_ray_01.png` | 192×192 | REPLACE | — | address `POWERUP_RAY` · origen: `Assets/Sprites/BALLPASS/BOLAS GIF/BOLAS ESPECIALES3.png` |
| `ball_power_shell_01.png` | 192×192 | REPLACE | — | sin address · origen: `Assets/Sprites/BALLPASS/BOLAS GIF/BOLAS ESPECIALES7.png` |
| `ball_state_selected_ring_01.png` | TBD | NEW | — | px propuesto 480×480; hoy no hay estado visual de selección |
| `ball_state_frozen_01.png` | TBD | NEW | — | px propuesto 416×416, overlay de congelado |
| `ball_state_matched_01.png` | TBD | NEW | — | px propuesto 96×96, destello al juntar 3 |

**Corrección v2 — Joker/Lead estaban cruzados.** La v1 asignaba `ESPECIALES1` a Lead y `ESPECIALES2` a
Joker: el cliente habría entregado las dos bolas invertidas y el error entraba al repo con nombre
correcto. La verdad sale de Addressables + prefab, no de la prosa, y las tres fuentes coinciden:

| Fuente | `POWERUP_JOKER` | `POWERUP_LEAD` |
|---|---|---|
| `Assets/AddressableAssetsData/AssetGroups/Default Local Group.asset` | `m_GUID: bc6b9068705e85347aa44c3e8981073d` | `m_GUID: 8c04cf6a0074f1642a41b32264ad9d45` |
| `.meta` de ese GUID | `Assets/Sprites/BALLPASS/BOLAS GIF/BOLAS ESPECIALES1.png.meta` (206×204) | `…/BOLAS ESPECIALES2.png.meta` (191×191) |
| Prefab consumidor | `Assets/Prefabs/PowerUp/PowerUpJoker.prefab:126` `m_Sprite: {fileID: 21300000, guid: bc6b9068…}` | `Assets/Prefabs/PowerUp/PowerUpLeadBall.prefab:126` `m_Sprite: {fileID: 21300000, guid: 8c04cf6a…}` |

`StoreService.prefab` confirma el mismo par por otra vía independiente: `sprite3` = `8c04cf6a…` (Lead),
`sprite4` = `bc6b9068…` (Joker). El px de cada fila es el header del archivo correcto: al corregir el
origen, **el px cambia con él** (Lead 206×204 → 191×191, Joker 191×191 → 206×204).

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

### F — Menú / login / matching / resultados / store (37)

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
| `ui_menu_bg_panel_01.png` | TBD | NEW | — | canvas de entrega **1024×813** (aspecto 1,2596 = la caja `MainMenu/Contenedor` 683,86×542,92). **NO es un recorte de `ui_menu_bg_01`**: es arte de panel propio, dibujado con cover-fit adentro de la caja (ver §6) |
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
| `ui_login_bg_01.png` | 1024×1024 | REPLACE | — | migrado a PNG en S2c-format (mismo px, RGBA8) · origen: `Assets/Sprites/BG_V2.png` |
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
| `hero_dialog_bg_01.png` | 1344×768 | REPLACE | — | migrado a PNG en S2c-format (mismo px, RGBA8) · origen: `Assets/Sprites/Heroes/conversacion_bg_1.png` |

**Nota de reclasificación (verify #3703, W-3 / RT-8).** Cinco filas del grupo A —
`ui_tutorial_dim_01`, `ui_tutorial_btn_skip_normal_01`, `ui_tutorial_btn_continue_normal_01`,
`ui_tutorial_btn_retry_normal_01` y `ui_tutorial_panel_retry_01` — se contaban como REPLACE con una
marca †. Hoy esas `Image` del prefab tienen `m_Sprite: {fileID: 0}` (color plano) y **no existe ni el
path ni el `.meta`** que RT-9 manda conservar. Operativamente son NEW, así que se reclasificaron como
tales: su columna **px es TBD** (el canvas de entrega va en la nota, como en todas las NEW) y
**nacerán con GUID nuevo**. Con eso el reparto del §2 queda en **85 REPLACE / 37 NEW**: las 85 REPLACE
restantes resolvieron todas a un PNG/JPEG en disco y su px sale del header real del archivo.

### Los 10 rects congelados (9 assets)

Método: para cada uno de los **25 assets** que hoy importan con `spriteMode: 2` (25 piezas, 1:1) se
compara el `rect` del `.meta` contra el header del archivo. **16 assets ya tienen `rect` igual al
archivo**: en ellos el flip a Single es cosmético. Los otros **9 acumulan 10 rects** que no coinciden, y
se congelan acá para que el barrido de S2 tenga una lista cerrada en vez de un recorte implícito.

| # | Pieza | Asset de origen | header | `rect` en el `.meta` (x,y · w×h) | qué hace |
|---|---|---|---|---|---|
| 1 | `ui_menu_avatar_player_01` | `Assets/Sprites/PlayerAvatar.png` | 1344×768 | 271,71 · **746×631** | **corta el px declarado**: entrega el 55 % del ancho del lienzo |
| 2 | `ui_menu_avatar_npc_01` | `Assets/Sprites/NpcAvatar.png` | 1024×1024 | 63,155 · **905×675** | **corta el px declarado**: entrega el 66 % de la altura |
| 3 | `game_board_shadow_mid_01` | `Assets/Sprites/V3_images/sombra 2.png` | 276×170 | 105,2 · **159×165** | crop no detectado en el barrido de v1 (nuevo en la v2) |
| 4 | `hero_rojo_banner_01` (`HeroeRojo_0`) | `Assets/Sprites/Heroes/HeroeRojo.png` | 1344×768 | 321,249 · **10×28** | astilla de 280 px²: **sólo la consume `HeroList.asset`**, que está muerto (ver §6) |
| 5 | `hero_rojo_banner_01` (`HeroeRojo_1`) | `Assets/Sprites/Heroes/HeroeRojo.png` | 1344×768 | 326,0 · 696×768 | el sprite que consumen los 2 consumidores reales de la pieza |
| 6 | `hero_rojo_portrait_idle_01` | `Assets/Sprites/Heroes/rojo_idle.png` | 1024×1024 | 175,0 · 716×1024 | crop del lienzo |
| 7 | `hero_rojo_portrait_happy_01` | `Assets/Sprites/Heroes/rojo_alegre.png` | 1024×1024 | 40,0 · 723×1024 | crop del lienzo |
| 8 | `hero_rojo_portrait_angry_01` | `Assets/Sprites/Heroes/rojo_enojo.png` | 1024×1024 | 88,0 · 925×1024 | crop del lienzo |
| 9 | `hero_rojo_portrait_determined_01` | `Assets/Sprites/Heroes/rojo_determinacion.png` | 1024×1024 | 24,0 · 924×1009 | crop del lienzo **en los dos ejes** |
| 10 | `hero_rojo_portrait_surprised_01` | `Assets/Sprites/Heroes/rojo_sorpresa.png` | 1024×1024 | 177,0 · 606×1024 | crop del lienzo |

Los **dos primeros son los que rompen el contrato de entrega**: si el cliente entrega exactamente el px
declarado (1344×768 y 1024×1024), el proyecto importa 746×631 y 905×675 — arte recortado que parece culpa
del artista. El #4 es el reverso: declarar 1344×768 sobre un rect 10×28 hace que la UI dimensione el
lienzo entero mientras el sprite dibujado es una astilla de 10×28 px. Ninguno de los 10 se acepta en ese
estado: S2 los resuelve llevando el asset a `spriteMode: 1`, con lo cual el rect deja de existir.

---

## 4. Spec de entrega (RT-10)

| Requisito | Valor |
|---|---|
| Formato | PNG-24 + canal alfa (RGBA8). **Nada de JPG**: de los 5 JPG originales, 2 ya migraron (`BG_V2` y `Heroes/conversacion_bg_1`, slice S2c-format) y quedan 3 (`BG.jpeg`, `Matchballance.jpg`, `BG_Search_Single.jpeg`) |
| Color | sRGB, sin perfil ICC embebido, sin alpha premultiplicado |
| PPU | **100** en todo el proyecto (verificado en los `.meta`) |
| Canvas de mundo | `game_*` y `ball_*` conservan **exactamente** el px actual: las bolas miden 416/417 px = 4.16 u; entregar 512 px cambia el tamaño de mundo y rompe el alineado con los 33 slots (0.6 u) y los `BoxCollider2D` |
| 9-slice | obligatorio declararlo por escrito en todo frame/botón/panel, con bordes en px |
| Tamaño máximo | **2048 px por lado** (el importador tiene `maxTextureSize: 2048` en todos los `.meta` verificados). Las **6 piezas REPLACE que hoy exceden el techo** —`game_board_backdrop_01`, `ui_pass_banner_01` y las 4 de BallPass— llevan más abajo en esta misma §4 el `px_entrega` exacto que el cliente debe dibujar, porque el importador las downsamplea igual (verify #3703, W-4) |
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

### `px_entrega` de las 6 piezas que superan 2048

`maxTextureSize` es **2048** en los 85 `.meta` verificados. En 6 piezas REPLACE el header supera ese techo
y el importador downsamplea sin avisar: el juego nunca ve el px declarado. La resolución es **bajar el px
de entrega**, no subir el techo (4096 costaría ~13 MB RGBA en un backdrop de mundo, sin necesidad
demostrada). `px_entrega` es el canvas que el cliente debe dibujar: en las **79 REPLACE restantes**
`px_entrega = px_actual`; en las **37 NEW** es el canvas propuesto en el §3.

| Pieza | px del archivo | `px_entrega` | regla |
|---|---|---|---|
| `ui_pass_banner_01` | 4096×1596 | **2048×1596** | el ancho se parte a la mitad; el alto ya entra |
| `ui_pass_reward_avatar_01` | 2156×2156 | **2048×2048** | lado mayor al techo, cuadrado conservado |
| `ui_pass_reward_avatar_02` | 1080×2151 | **1028×2048** | 1080 × 2048 / 2151 = 1028,3 → 1028 |
| `ui_pass_reward_background_01` | 2759×1120 | **2048×831** | 1120 × 2048 / 2759 = 831,4 → 831 |
| `ui_pass_reward_background_02` | 2750×1116 | **2048×831** | 1116 × 2048 / 2750 = 831,2 → 831 |
| `game_board_backdrop_01` | 1220×2712 | **921×2048** | 1220 × 2048 / 2712 = 921,3 → 921 (lo que el juego ya muestra hoy) |

Regla del validador: `max(px_entrega) ≤ max_tex`, y **ninguna** dimensión importada por debajo de
`px_entrega`. Cinco de las seis conservan el aspecto (redondeo al px entero); `ui_pass_banner_01` es la
excepción declarada: se recorta el ancho, no se reescala el alto.

---

## 5. Regla de reemplazo (RT-9)

**Reemplazo in-place, siempre**: se pisan los BYTES del `.png`, se conserva el path, el nombre y el
`.meta`. Nunca borrar-y-reimportar, nunca `git mv` en el mismo commit que un cambio de arte. Un renombre
mueve el `.meta` junto al `.png`, en un commit dedicado, para que sobrevivan las **89 referencias por
GUID** y las **11 addresses** de Addressables.

---

## 6. Roles dobles (sin filas extra: RT-8 fija 122 exactas)

| Pieza | Roles | Decisión |
|---|---|---|
| `ui_pass_reward_picture_01` (`BALLPASS0.png`) | recompensa de BallPass + avatar del HUD | **una sola pieza**; el HUD la dibuja con `Image` + máscara circular. Si el cliente quiere arte distinto: `ui_hud_avatar_player_art_01` en una v2 |
| `ui_common_card_01` (`Card.png`) | card de BallPass + card del Store | **una sola pieza** compartida |
| `ui_hud_btn_powerup_slot_*` / `ui_menu_tab_powerups_*` | slot del HUD + tab del menú | una sola textura, dos consumidores |

### Fondos: cover-fit + pieza de panel aparte (decisión v2)

`ui_menu_bg_01` (`BG.jpeg`, 1087×1928) tiene **13 consumidores reales en 6 assets**. Medición por GUID
`ed2b35ab1ba9db746b58a772e9f3c4f8`: 14 referencias, de las cuales **una está en un GameObject inactivo**
(`Game.unity`, GO `1742248663`, `m_IsActive: 0`) → quedan 13 activas:

| Asset | Consumidores activos | Encuadre |
|---|---|---|
| `Assets/Scenes/MainMenu.unity` | 8 (`BG` ×3, `Store`, `Contenedor`, `GameOptions`, `Instruccions`, `RawImage`) | cover |
| `Assets/Scenes/Game.unity` | 1 (`bg`; la `Image` inactiva no cuenta) | cover |
| `Assets/Scenes/Lobby.unity` | 1 (`BG`) | cover |
| `Assets/Scenes/PvPPowerUps.unity` | 1 (`BG`) | cover |
| `Assets/Scenes/BallPass.unity` | 1 (`season`) | cover |
| `Assets/Prefabs/BallPass/ExampleToPass.prefab` | 1 | cover |
| **Total** | **13 en 6 assets** | |

Hoy los 6 encuadres se resuelven de tres maneras incompatibles: 3 fijos 1:1, 3 estirados a 3200–3383 u
de ancho (deforman) y 5 heredados por anclas 0..1. **Decisión cerrada**: los fondos pasan a **cover-fit**
(`preserveAspect` + envelope de `AspectRatioFitter`, sin distorsión) y el panel `MainMenu/Contenedor`
—caja de **683,86×542,92** en `MainMenu.unity` (GO `841790889`), hoy un `RawImage` con `m_UVRect` `y:
-0,15` sobre `BG.jpeg`— recibe **pieza propia**: `ui_menu_bg_panel_01.png` (pieza **122**, canvas
1024×813 = aspecto 1,2596 de la caja). Una ilustración vertical no puede ser a la vez fondo full-bleed de
5 pantallas y arte de una caja casi cuadrada; forzarla por crop es exactamente lo que hoy se ve mal.

### `HeroList.asset` es un asset muerto (decisión v2)

`Assets/Sprites/Heroes/HeroeRojo.png` (guid `aa48d609dc0280e47bcb8867e0f45604`) tiene **2 sub-sprites**:

| Sub-sprite | fileID | rect | consumidores |
|---|---|---|---|
| `HeroeRojo_0` | `6343446867228487132` | 321,249 · 10×28 | **sólo** `Assets/Prefabs/Heroes/List/HeroList.asset:16` |
| `HeroeRojo_1` | `-6369504855698502937` | 326,0 · 696×768 | `Assets/Prefabs/Heroes/Ui/Cards/HeroPrefab.prefab:1334` y `Assets/Scenes/MainMenu.unity:16898` |

`HeroList.asset` tiene GUID `e05bd6a693acdfa408a1fbc077448b68` y **no lo referencia nadie** en
`Assets/` (grep del GUID sobre `.unity/.prefab/.asset/.mat`: 0 resultados). Por lo tanto la astilla 10×28
**no se dibuja en ninguna pantalla**, y los 2 consumidores reales ya apuntan al lienzo completo
(`HeroeRojo_1`). Decisión: `HeroeRojo.png` pasa a `spriteMode: 1` remapeando **2 referencias** →
**cero cambio visual** y **cero piezas nuevas**. El riesgo R-b del design (que `HeroList` empiece a
mostrar el lienzo entero) queda descartado por evidencia, no por tolerancia.

---

## 7. Fuera de alcance

- **Tema carnaval** (`theme_*`, 18 piezas): es cotización, no alcance. Con ese bloque aprobado el
  manifiesto pasa a 140 piezas (122 + 18).
- Reskin de todas las pantallas: acá se NOMBRAN, pintarlas es otro cambio.
- `ui_tutorial_spotlight_*` se entrega pero no se cablea (falta masking en el proyecto).

---

## 8. Auditoría de este documento

- **px reales**: leídos del header de cada archivo (PNG IHDR bytes 16-24; JPEG SOF), no copiados de la
  propuesta. Las **85 REPLACE** con archivo de origen coinciden con el archivo que existe en el repo.
- **join de los 11 addresses**: `pieza → address → m_GUID → path → prefab consumidor` sin discrepancias.
  Las 4 de power-up salen de `Default Local Group.asset` y las 7 de BallPass de `BallPass.asset`; ninguna
  entry es sub-objeto (todas son GUID de asset).
- **Joker/Lead**: las 3 fuentes (Addressables, `.meta` del GUID y prefab consumidor) coinciden →
  `ball_power_lead_01` = `BOLAS ESPECIALES2` y `ball_power_joker_01` = `BOLAS ESPECIALES1` (§3).
- **conteo**: 122 filas · **85 REPLACE** · **37 NEW** · 0 duplicados.
- **nombres**: `^[a-z0-9]+(_[a-z0-9]+)+_[0-9]{2}\.png$` → 122/122 pasan.
- **rects**: de los 25 assets con `spriteMode: 2`, **16 ya tienen `rect` == header** y **9 acumulan 10
  rects** que no coinciden; los 10 están congelados en §3.
- **techo de textura**: `maxTextureSize: 2048` en los 85 `.meta`; las **6** piezas que lo superan tienen
  `px_entrega` declarado en §4.
- **9-slice**: bordes de `Button_UI`, `Contenedor_UI`, `SubContenedor_UI` leídos de los `.meta`; son los
  únicos 3 no nulos del proyecto.
- **tabla de verdad**: el bloque cercado de §9 tiene una fila por pieza (122) y es la única fuente que
  lee el validador; la prosa de §3/§4 es para personas.

---

## 9. Tabla de verdad (contrato v2)

Una fila por pieza. Es la **única fuente** que lee el validador: la prosa de §3/§4 existe para personas y
no se parsea. Cualquier discrepancia entre esta tabla y el `.meta` es un `fail`, no una discusión.

| Columna | Significado | Valores |
|---|---|---|
| `pieza` | nombre del archivo | `^[a-z0-9]+(_[a-z0-9]+)+_[0-9]{2}\.png$` |
| `estado` | REPLACE / NEW | REPLACE pisa bytes; NEW nace con GUID nuevo |
| `origen` | asset de origen hoy | `-` en las NEW |
| `px_actual` | header del archivo | `WxH` · `TBD` en las NEW (no hay archivo) |
| `px_entrega` | canvas que se dibuja | `WxH`; difiere de `px_actual` sólo en las 6 de §4 (en las NEW `px_actual` es `TBD`) |
| `mode_target` | `spriteImportMode` objetivo | `1` (Single) en todas — es lo que hace que el swap sea sólo bytes |
| `mode_actual` | `spriteImportMode` medido **el día del escaneo** (histórico) | `2` en las 25, `1` en las 60 REPLACE conformes, `-` en las NEW. **No es un gate** (ver nota) |
| `border` | `spriteBorder` l/b/r/t | `0/0/0/0` = sin borde. En REPLACE se lee del `.meta`; en NEW es contrato |
| `max_tex` | `maxTextureSize` | `2048` en las 122 |
| `pivot` | `spritePivot` | `0.5/0.5` (el pivote en la punta del dedo de las manos del tutorial es de RectTransform, no del sprite) |
| `ppu` | `spritePixelsToUnits` | `100` en las 122 |
| `address` | address de Addressables | `-` si la pieza no está direccionada (11 con address) |

> **Nota sobre `mode_actual` (histórico, no gate).** `mode_actual` es el **snapshot del escaneo**
> con el que se congeló esta tabla ("cómo estaba el arte el 2026-11-11"). No se actualiza cuando
> una pieza se migra y **no se puede usar como condición de nada**: ni el auditor
> (`tools/art/audit_art_manifest.py`) ni el harness sintético (`ArtSyntheticBatch`) deciden con
> ella. La columna operativa es **`mode_target`**, y ambos comparan el `spriteMode` **medido en el
> `.meta`** contra ese target. Usarla como estado vivo hace que cada pieza migrada genere una
> violación nueva (cancela el avance de S2) y que el gate no pueda correr justo después de migrar.

```csv
pieza,estado,origen,px_actual,px_entrega,mode_target,mode_actual,border,max_tex,pivot,ppu,address
ui_tutorial_dim_01.png,NEW,-,TBD,4x4,1,-,0/0/0/0,2048,0.5/0.5,100,-
ui_tutorial_bubble_body_01.png,NEW,-,TBD,400x520,1,-,48/56/48/56,2048,0.5/0.5,100,-
ui_tutorial_bubble_tail_left_01.png,NEW,-,TBD,TBD,1,-,0/0/0/0,2048,0.5/0.5,100,-
ui_tutorial_caption_pill_01.png,NEW,-,TBD,TBD,1,-,24/16/24/16,2048,0.5/0.5,100,-
ui_tutorial_spotlight_ring_01.png,NEW,-,TBD,512x512,1,-,0/0/0/0,2048,0.5/0.5,100,-
ui_tutorial_spotlight_fill_01.png,NEW,-,TBD,512x512,1,-,0/0/0/0,2048,0.5/0.5,100,-
ui_tutorial_hand_tap_01.png,NEW,-,TBD,160x200,1,-,0/0/0/0,2048,0.5/0.5,100,-
ui_tutorial_hand_tap_02.png,NEW,-,TBD,160x200,1,-,0/0/0/0,2048,0.5/0.5,100,-
ui_tutorial_hand_tap_03.png,NEW,-,TBD,160x200,1,-,0/0/0/0,2048,0.5/0.5,100,-
ui_tutorial_hand_point_left_01.png,NEW,-,TBD,160x200,1,-,0/0/0/0,2048,0.5/0.5,100,-
ui_tutorial_hand_point_right_01.png,NEW,-,TBD,160x200,1,-,0/0/0/0,2048,0.5/0.5,100,-
ui_tutorial_icon_star_01.png,NEW,-,TBD,64x64,1,-,0/0/0/0,2048,0.5/0.5,100,-
ui_tutorial_step_dot_normal_01.png,NEW,-,TBD,28x28,1,-,0/0/0/0,2048,0.5/0.5,100,-
ui_tutorial_step_dot_active_01.png,NEW,-,TBD,28x28,1,-,0/0/0/0,2048,0.5/0.5,100,-
ui_tutorial_step_dot_done_01.png,NEW,-,TBD,28x28,1,-,0/0/0/0,2048,0.5/0.5,100,-
ui_tutorial_btn_skip_normal_01.png,NEW,-,TBD,120x56,1,-,12/12/12/12,2048,0.5/0.5,100,-
ui_tutorial_btn_skip_pressed_01.png,NEW,-,TBD,120x56,1,-,12/12/12/12,2048,0.5/0.5,100,-
ui_tutorial_btn_continue_normal_01.png,NEW,-,TBD,280x84,1,-,12/12/12/12,2048,0.5/0.5,100,-
ui_tutorial_btn_continue_pressed_01.png,NEW,-,TBD,280x84,1,-,12/12/12/12,2048,0.5/0.5,100,-
ui_tutorial_btn_retry_normal_01.png,NEW,-,TBD,220x64,1,-,12/12/12/12,2048,0.5/0.5,100,-
ui_tutorial_btn_retry_pressed_01.png,NEW,-,TBD,220x64,1,-,12/12/12/12,2048,0.5/0.5,100,-
ui_tutorial_panel_retry_01.png,NEW,-,TBD,780x420,1,-,32/32/32/32,2048,0.5/0.5,100,-
ui_tutorial_icon_danger_01.png,NEW,-,TBD,48x48,1,-,0/0/0/0,2048,0.5/0.5,100,-
ui_hud_counter_pill_01.png,REPLACE,Assets/Sprites/Container_Counts.png,46x22,46x22,1,1,0/0/0/0,2048,0.5/0.5,100,-
ui_hud_icon_coin_01.png,REPLACE,Assets/Sprites/Currenci.png,28x28,28x28,1,1,0/0/0/0,2048,0.5/0.5,100,-
ui_hud_icon_cups_01.png,REPLACE,Assets/Sprites/Icon_Cups.png,72x40,72x40,1,1,0/0/0/0,2048,0.5/0.5,100,-
ui_hud_btn_settings_normal_01.png,REPLACE,Assets/Sprites/Btn_settings.png,28x28,28x28,1,1,0/0/0/0,2048,0.5/0.5,100,-
ui_hud_btn_settings_pressed_01.png,NEW,-,TBD,28x28,1,-,0/0/0/0,2048,0.5/0.5,100,-
ui_hud_avatar_frame_player_01.png,NEW,-,TBD,160x160,1,-,0/0/0/0,2048,0.5/0.5,100,-
ui_hud_avatar_frame_ai_01.png,NEW,-,TBD,160x160,1,-,0/0/0/0,2048,0.5/0.5,100,-
ui_hud_score_pill_player_01.png,NEW,-,TBD,220x72,1,-,0/0/0/0,2048,0.5/0.5,100,-
ui_hud_score_pill_ai_01.png,NEW,-,TBD,220x72,1,-,0/0/0/0,2048,0.5/0.5,100,-
ui_hud_btn_powerup_slot_normal_01.png,REPLACE,Assets/Sprites/PowerUpsNormal.png,165x36,165x36,1,1,0/0/0/0,2048,0.5/0.5,100,-
ui_hud_btn_powerup_slot_active_01.png,REPLACE,Assets/Sprites/PowerUpsClicked.png,165x36,165x36,1,1,0/0/0/0,2048,0.5/0.5,100,-
ui_hud_panel_player_01.png,NEW,-,TBD,600x96,1,-,0/0/0/0,2048,0.5/0.5,100,-
game_board_balance_bar_left_01.png,REPLACE,Assets/Sprites/V3_images/balanza 2.png,9x134,9x134,1,2,0/0/0/0,2048,0.5/0.5,100,-
game_board_balance_bar_right_01.png,REPLACE,Assets/Sprites/V3_images/balanza 3.png,223x109,223x109,1,2,0/0/0/0,2048,0.5/0.5,100,-
game_board_balance_base_01.png,REPLACE,Assets/Sprites/V3_images/balanza 1.png,305x118,305x118,1,2,0/0/0/0,2048,0.5/0.5,100,-
game_board_line_01.png,REPLACE,Assets/Sprites/V3_images/LineaBalanza.png,305x10,305x10,1,2,0/0/0/0,2048,0.5/0.5,100,-
game_board_line_shadow_01.png,REPLACE,Assets/Sprites/V3_images/LineaBalanzaSombra.png,352x54,352x54,1,2,0/0/0/0,2048,0.5/0.5,100,-
game_board_shadow_long_01.png,REPLACE,Assets/Sprites/V3_images/sombra 1.png,352x164,352x164,1,2,0/0/0/0,2048,0.5/0.5,100,-
game_board_shadow_mid_01.png,REPLACE,Assets/Sprites/V3_images/sombra 2.png,276x170,276x170,1,2,0/0/0/0,2048,0.5/0.5,100,-
game_board_shadow_short_01.png,REPLACE,Assets/Sprites/V3_images/sombra 3.png,266x152,266x152,1,2,0/0/0/0,2048,0.5/0.5,100,-
game_board_arrow_swap_01.png,REPLACE,Assets/Sprites/V3_images/flechita 11.png,29x142,29x142,1,2,0/0/0/0,2048,0.5/0.5,100,-
game_board_arrow_swap_alt_01.png,REPLACE,Assets/Sprites/V3_images/flechita 2.png,29x142,29x142,1,2,0/0/0/0,2048,0.5/0.5,100,-
game_board_arrow_shadow_01.png,REPLACE,Assets/Sprites/V3_images/sombra flechita 1.png,59x172,59x172,1,2,0/0/0/0,2048,0.5/0.5,100,-
game_board_arrow_shadow_02.png,REPLACE,Assets/Sprites/V3_images/sombra flechita 2.png,59x172,59x172,1,2,0/0/0/0,2048,0.5/0.5,100,-
game_board_backdrop_01.png,REPLACE,Assets/Sprites/Matchballance.jpg,1220x2712,921x2048,1,2,0/0/0/0,2048,0.5/0.5,100,-
game_board_zone_veil_01.png,NEW,-,TBD,4x4,1,-,0/0/0/0,2048,0.5/0.5,100,-
ball_normal_red_01.png,REPLACE,Assets/Sprites/ball_red.png,416x416,416x416,1,1,0/0/0/0,2048,0.5/0.5,100,-
ball_normal_blue_01.png,REPLACE,Assets/Sprites/ball_blue.png,417x416,417x416,1,1,0/0/0/0,2048,0.5/0.5,100,-
ball_normal_green_01.png,REPLACE,Assets/Sprites/ball_green.png,417x417,417x417,1,1,0/0/0/0,2048,0.5/0.5,100,-
ball_normal_orange_01.png,REPLACE,Assets/Sprites/ball_orange.png,416x416,416x416,1,1,0/0/0/0,2048,0.5/0.5,100,-
ball_normal_purple_01.png,REPLACE,Assets/Sprites/ball_violet.png,416x416,416x416,1,1,0/0/0/0,2048,0.5/0.5,100,-
ball_normal_yellow_01.png,REPLACE,Assets/Sprites/bola amarilla.png,417x417,417x417,1,1,0/0/0/0,2048,0.5/0.5,100,-
ball_shadow_01.png,REPLACE,Assets/Sprites/sombra.png,169x169,169x169,1,2,0/0/0/0,2048,0.5/0.5,100,-
ball_power_ice_01.png,REPLACE,Assets/Sprites/BALLPASS/BOLAS GIF/BOLAS ESPECIALES.png,192x197,192x197,1,1,0/0/0/0,2048,0.5/0.5,100,POWERUP_ICE
ball_power_lead_01.png,REPLACE,Assets/Sprites/BALLPASS/BOLAS GIF/BOLAS ESPECIALES2.png,191x191,191x191,1,1,0/0/0/0,2048,0.5/0.5,100,POWERUP_LEAD
ball_power_joker_01.png,REPLACE,Assets/Sprites/BALLPASS/BOLAS GIF/BOLAS ESPECIALES1.png,206x204,206x204,1,1,0/0/0/0,2048,0.5/0.5,100,POWERUP_JOKER
ball_power_ray_01.png,REPLACE,Assets/Sprites/BALLPASS/BOLAS GIF/BOLAS ESPECIALES3.png,192x192,192x192,1,1,0/0/0/0,2048,0.5/0.5,100,POWERUP_RAY
ball_power_shell_01.png,REPLACE,Assets/Sprites/BALLPASS/BOLAS GIF/BOLAS ESPECIALES7.png,192x192,192x192,1,1,0/0/0/0,2048,0.5/0.5,100,-
ball_state_selected_ring_01.png,NEW,-,TBD,480x480,1,-,0/0/0/0,2048,0.5/0.5,100,-
ball_state_frozen_01.png,NEW,-,TBD,416x416,1,-,0/0/0/0,2048,0.5/0.5,100,-
ball_state_matched_01.png,NEW,-,TBD,96x96,1,-,0/0/0/0,2048,0.5/0.5,100,-
ui_pass_banner_01.png,REPLACE,Assets/Sprites/ballpassBanner.png,4096x1596,2048x1596,1,1,0/0/0/0,2048,0.5/0.5,100,-
ui_pass_icon_pass_01.png,REPLACE,Assets/Sprites/Pase.png,129x73,129x73,1,1,0/0/0/0,2048,0.5/0.5,100,-
ui_pass_separator_page_01.png,REPLACE,Assets/Sprites/Separador_ballpass.png,360x8,360x8,1,1,0/0/0/0,2048,0.5/0.5,100,-
ui_pass_separator_level_01.png,REPLACE,Assets/Sprites/Separador_ballpass_levels.png,360x29,360x29,1,1,0/0/0/0,2048,0.5/0.5,100,-
ui_pass_reward_gold_01.png,REPLACE,Assets/Sprites/BALLPASS/BALLPASS1.png,1550x1028,1550x1028,1,1,0/0/0/0,2048,0.5/0.5,100,gold
ui_pass_reward_avatar_01.png,REPLACE,Assets/Sprites/BALLPASS/BALLPASS4.png,2156x2156,2048x2048,1,1,0/0/0/0,2048,0.5/0.5,100,profileAvatar_1
ui_pass_reward_avatar_02.png,REPLACE,Assets/Sprites/BALLPASS/BALLPASS5.png,1080x2151,1028x2048,1,1,0/0/0/0,2048,0.5/0.5,100,profileAvatar_2
ui_pass_reward_picture_01.png,REPLACE,Assets/Sprites/BALLPASS/BALLPASS0.png,1578x1578,1578x1578,1,1,0/0/0/0,2048,0.5/0.5,100,profilePicture_1
ui_pass_reward_picture_02.png,REPLACE,Assets/Sprites/BALLPASS/BALLPASS2.png,1571x1571,1571x1571,1,1,0/0/0/0,2048,0.5/0.5,100,profilePicture_2
ui_pass_reward_background_01.png,REPLACE,Assets/Sprites/BALLPASS/BALLPASS3.png,2759x1120,2048x831,1,1,0/0/0/0,2048,0.5/0.5,100,profileBackground_1
ui_pass_reward_background_02.png,REPLACE,Assets/Sprites/BALLPASS/BALLPASS7.png,2750x1116,2048x831,1,1,0/0/0/0,2048,0.5/0.5,100,profileBackground_2
ui_pass_state_check_01.png,REPLACE,Assets/Sprites/Check.png,23x26,23x26,1,1,0/0/0/0,2048,0.5/0.5,100,-
ui_pass_state_lock_01.png,REPLACE,Assets/Sprites/CheckLock.png,23x26,23x26,1,1,0/0/0/0,2048,0.5/0.5,100,-
ui_pass_btn_return_01.png,REPLACE,Assets/Sprites/Btn_Return.png,200x35,200x35,1,1,0/0/0/0,2048,0.5/0.5,100,-
ui_common_btn_primary_normal_01.png,REPLACE,Assets/Sprites/Button_UI.png,124x25,124x25,1,1,7/6/5/7,2048,0.5/0.5,100,-
ui_common_btn_primary_pressed_01.png,NEW,-,TBD,124x25,1,-,7/6/5/7,2048,0.5/0.5,100,-
ui_common_btn_primary_disabled_01.png,NEW,-,TBD,124x25,1,-,7/6/5/7,2048,0.5/0.5,100,-
ui_common_btn_large_01.png,REPLACE,Assets/Sprites/Button_BG.png,360x67,360x67,1,1,0/0/0/0,2048,0.5/0.5,100,-
ui_common_panel_container_01.png,REPLACE,Assets/Sprites/Contenedor_UI.png,248x194,248x194,1,1,14/11/11/12,2048,0.5/0.5,100,-
ui_common_panel_subcontainer_01.png,REPLACE,Assets/Sprites/SubContenedor_UI.png,312x197,312x197,1,1,12/11/8/7,2048,0.5/0.5,100,-
ui_common_card_01.png,REPLACE,Assets/Sprites/Card.png,152x206,152x206,1,1,0/0/0/0,2048,0.5/0.5,100,-
ui_common_divider_01.png,REPLACE,Assets/Sprites/Separador.png,43x455,43x455,1,1,0/0/0/0,2048,0.5/0.5,100,-
ui_common_logo_game_01.png,REPLACE,Assets/Sprites/GameTitle.png,340x63,340x63,1,1,0/0/0/0,2048,0.5/0.5,100,-
ui_common_logo_title_01.png,REPLACE,Assets/Sprites/title.png,1399x208,1399x208,1,1,0/0/0/0,2048,0.5/0.5,100,-
ui_common_loader_01.png,REPLACE,Assets/Sprites/Loader anim.png,180x180,180x180,1,1,0/0/0/0,2048,0.5/0.5,100,-
ui_menu_bg_01.png,REPLACE,Assets/Sprites/BG.jpeg,1087x1928,1087x1928,1,1,0/0/0/0,2048,0.5/0.5,100,-
ui_menu_bg_panel_01.png,NEW,-,TBD,1024x813,1,-,0/0/0/0,2048,0.5/0.5,100,-
ui_menu_btn_play_normal_01.png,REPLACE,Assets/Sprites/Btn_Play.png,140x48,140x48,1,1,0/0/0/0,2048,0.5/0.5,100,-
ui_menu_btn_play_pressed_01.png,NEW,-,TBD,140x48,1,-,0/0/0/0,2048,0.5/0.5,100,-
ui_menu_btn_home_01.png,REPLACE,Assets/Sprites/Home.png,60x44,60x44,1,1,0/0/0/0,2048,0.5/0.5,100,-
ui_menu_btn_store_01.png,REPLACE,Assets/Sprites/Store.png,60x44,60x44,1,1,0/0/0/0,2048,0.5/0.5,100,-
ui_menu_btn_ballpass_01.png,REPLACE,Assets/Sprites/Ballpass.png,60x44,60x44,1,1,0/0/0/0,2048,0.5/0.5,100,-
ui_menu_btn_friends_01.png,REPLACE,Assets/Sprites/Btn_Friends.png,48x48,48x48,1,1,0/0/0/0,2048,0.5/0.5,100,-
ui_menu_btn_help_01.png,REPLACE,Assets/Sprites/Btn_Help.png,28x28,28x28,1,1,0/0/0/0,2048,0.5/0.5,100,-
ui_menu_btn_back_01.png,REPLACE,Assets/Sprites/Btn_Back.png,28x29,28x29,1,1,0/0/0/0,2048,0.5/0.5,100,-
ui_menu_btn_close_01.png,REPLACE,Assets/Sprites/Close_Button.png,24x24,24x24,1,1,0/0/0/0,2048,0.5/0.5,100,-
ui_menu_tab_rules_normal_01.png,REPLACE,Assets/Sprites/RulesNormal.png,165x36,165x36,1,1,0/0/0/0,2048,0.5/0.5,100,-
ui_menu_tab_rules_active_01.png,REPLACE,Assets/Sprites/RulesClicked.png,165x36,165x36,1,1,0/0/0/0,2048,0.5/0.5,100,-
ui_menu_tab_powerups_normal_01.png,REPLACE,Assets/Sprites/PowerUpsNormal.png,165x36,165x36,1,1,0/0/0/0,2048,0.5/0.5,100,-
ui_menu_tab_powerups_active_01.png,REPLACE,Assets/Sprites/PowerUpsClicked.png,165x36,165x36,1,1,0/0/0/0,2048,0.5/0.5,100,-
ui_menu_avatar_player_01.png,REPLACE,Assets/Sprites/PlayerAvatar.png,1344x768,1344x768,1,2,0/0/0/0,2048,0.5/0.5,100,-
ui_menu_avatar_npc_01.png,REPLACE,Assets/Sprites/NpcAvatar.png,1024x1024,1024x1024,1,2,0/0/0/0,2048,0.5/0.5,100,-
ui_menu_panel_profile_01.png,REPLACE,Assets/Sprites/Heroes/BGProfile.png,1920x1080,1920x1080,1,2,0/0/0/0,2048,0.5/0.5,100,-
ui_menu_picture_profile_01.png,REPLACE,Assets/Sprites/PictureProfile.png,48x48,48x48,1,1,0/0/0/0,2048,0.5/0.5,100,-
ui_menu_rules_illustration_01.png,REPLACE,Assets/Sprites/profileRules.png,1024x1024,1024x1024,1,1,0/0/0/0,2048,0.5/0.5,100,-
ui_menu_icon_vs_01.png,REPLACE,Assets/Sprites/VS.png,31x18,31x18,1,1,0/0/0/0,2048,0.5/0.5,100,-
ui_login_bg_01.png,REPLACE,Assets/Sprites/BG_V2.png,1024x1024,1024x1024,1,2,0/0/0/0,2048,0.5/0.5,100,-
ui_matching_bg_01.png,REPLACE,Assets/Sprites/BG_Search_Single.jpeg,2048x2048,2048x2048,1,1,0/0/0/0,2048,0.5/0.5,100,-
ui_result_win_01.png,REPLACE,Assets/Sprites/YouWin.png,360x800,360x800,1,1,0/0/0/0,2048,0.5/0.5,100,-
ui_result_lose_01.png,REPLACE,Assets/Sprites/GameOver.png,360x800,360x800,1,1,0/0/0/0,2048,0.5/0.5,100,-
ui_store_placeholder_01.png,REPLACE,Assets/StoreExample.png,176x189,176x189,1,1,0/0/0/0,2048,0.5/0.5,100,-
hero_rojo_portrait_idle_01.png,REPLACE,Assets/Sprites/Heroes/rojo_idle.png,1024x1024,1024x1024,1,2,0/0/0/0,2048,0.5/0.5,100,-
hero_rojo_portrait_happy_01.png,REPLACE,Assets/Sprites/Heroes/rojo_alegre.png,1024x1024,1024x1024,1,2,0/0/0/0,2048,0.5/0.5,100,-
hero_rojo_portrait_angry_01.png,REPLACE,Assets/Sprites/Heroes/rojo_enojo.png,1024x1024,1024x1024,1,2,0/0/0/0,2048,0.5/0.5,100,-
hero_rojo_portrait_determined_01.png,REPLACE,Assets/Sprites/Heroes/rojo_determinacion.png,1024x1024,1024x1024,1,2,0/0/0/0,2048,0.5/0.5,100,-
hero_rojo_portrait_surprised_01.png,REPLACE,Assets/Sprites/Heroes/rojo_sorpresa.png,1024x1024,1024x1024,1,2,0/0/0/0,2048,0.5/0.5,100,-
hero_rojo_banner_01.png,REPLACE,Assets/Sprites/Heroes/HeroeRojo.png,1344x768,1344x768,1,2,0/0/0/0,2048,0.5/0.5,100,-
hero_dialog_bg_01.png,REPLACE,Assets/Sprites/Heroes/conversacion_bg_1.png,1344x768,1344x768,1,2,0/0/0/0,2048,0.5/0.5,100,-
```

**Pendientes declarados.** Dos NEW todavía no tienen canvas en el §3 y por eso quedan en `TBD`:
`ui_tutorial_bubble_tail_left_01` y `ui_tutorial_caption_pill_01` (el manifiesto describe su función pero
no fija px). No se inventa un número: se cierra cuando se dibuje la burbuja, porque la cola y la píldora
se dimensionan contra el cuerpo de 400×520.
