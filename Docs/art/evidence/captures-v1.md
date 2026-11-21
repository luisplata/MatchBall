# Capturas v1 — rig `ArtCaptureRig` (DRL-3 / DRL-4)

Generado por `Assets/Editor/ArtContract/ArtCaptureRig.cs` (`BatchCapture`, entrypoint unico). **No editar a mano**: re-ejecutar el rig reescribe este archivo y los PNG que declara.

Metodo: camara del canvas (`Canvas.worldCamera`, ScreenSpaceCamera) -> `RenderTexture` 900x1900 (la resolucion de referencia de todos los `CanvasScaler`) -> `ReadPixels` -> recorte por la proyeccion del `RectTransform` del nodo. **Nada de `ScreenCapture`**: la resolucion es la del contrato, no la del Game view.

Forzado por fila, declarado (no es maquillaje):

- cadena de ancestros activa: un nodo inactivo no dibuja.
- `m_Color` = el que APW-3 exige para la fila (la misma regla que mide `check_placeholder_wiring.py`), para que la captura no dependa de un estado previo.
- `selectable_state`: `Selectable.DoStateTransition` (por reflexion) al estado de su `campo`; es exactamente lo que hace el input, y con `m_Transition: 2` no hay tinte de color (el costo de APW-4b se ve en la captura).
- `serialized_sprite_field` sobre `TutorialStepDots`: `SetCurrent(1)` (dot 0 `done`, dot 1 `active`, dot 2 `normal`) y el recorte va al dot que nombra el `campo`, no a la tira completa.
- panel con `PanelFloatingUiRules` (`Configuration` en `MainMenu`): se escribe el destino de su tween `Show()`, `localPosition = 0`. Sin play mode; la posicion abierta del panel **no** esta confirmada por el dueno de la UI.

Sin play mode y sin escribir `Assets/`: cada escena se abre desde disco y se recarga para descartar el estado forzado. Ningun `.meta` se reescribe (15 piezas en Single serializan un bloque de tamano lienzo: moverles un px las volveria `RECT-STALE`).

## Columnas

| Columna | Significado |
|---|---|
| `pieza` | nombre verbatim; la captura es `<pieza>` en Docs/art/captures/placeholders-v1 |
| `px` | tamano del recorte (el nodo, no el marco) |
| `marco` | tamano del marco del que se recorta (constante: 900x1900) |
| `bbox` | `x,y,w,h` del nodo proyectado al marco, en pixeles del PNG (origen arriba-izquierda): la region que un swap puede tocar (DRL-4) |
| `colores` | colores distintos del PNG: una captura uniforme no es una captura (DRL-3) |

El marco completo no se versiona: `CaptureSwapDrill` emite `before.png` y `after.png` del mismo estado en `Temp/art-captures/swap-drill` (ignorado) y `tools/art/check_capture_diff.py` compara `changed ⊆ bbox` y `changed >= 1`. No hay hashes de captura: dependen de Unity y del frame.

## Filas `cableado` sin captura (declaradas)

`ball_state_frozen_01.png` es la unica: su slot es un `Sprite` serializado en un `MonoBehaviour` (`FreezerMono.freezeSprite`) que el juego asigna a un `SpriteRenderer` de mundo en runtime. No tiene `RectTransform` ni Canvas, asi que este rig no lo proyecta, y no se inventa un bitmap. La lista cerrada vive en `tools/art/art_ledger.py` (`CAPTURE_EXCEPTIONS`) y los dos gates la imprimen por nombre.

- `ball_state_frozen_01.png` (cableado · Assets/Prefabs/Ballzone/FreezerMono.prefab · FreezerMono.freezeSprite)

```csv
pieza,px,marco,bbox,colores
ui_tutorial_dim_01.png,900x1900,900x1900,"0,0,900,1900",14338
ui_tutorial_bubble_body_01.png,401x521,900x1900,"461,906,401,521",6887
ui_tutorial_hand_point_left_01.png,160x200,900x1900,"370,850,160,200",844
ui_tutorial_hand_point_right_01.png,160x200,900x1900,"370,850,160,200",770
ui_tutorial_step_dot_normal_01.png,29x28,900x1900,"667,930,29,28",17
ui_tutorial_step_dot_active_01.png,28x28,900x1900,"627,930,28,28",12
ui_tutorial_step_dot_done_01.png,28x28,900x1900,"587,930,28,28",7
ui_tutorial_btn_skip_normal_01.png,122x57,900x1900,"484,1495,122,57",855
ui_tutorial_btn_skip_pressed_01.png,122x57,900x1900,"484,1495,122,57",852
ui_tutorial_btn_continue_normal_01.png,280x85,900x1900,"521,1258,280,85",915
ui_tutorial_btn_continue_pressed_01.png,280x85,900x1900,"521,1258,280,85",936
ui_tutorial_btn_retry_normal_01.png,280x84,900x1900,"310,1060,280,84",2051
ui_tutorial_btn_retry_pressed_01.png,280x84,900x1900,"310,1060,280,84",2156
ui_tutorial_panel_retry_01.png,781x420,900x1900,"59,740,781,420",3298
ui_tutorial_icon_danger_01.png,353x44,900x1900,"484,1206,353,44",1839
game_board_zone_veil_01.png,288x146,900x1900,"306,877,288,146",278
ui_hud_btn_settings_pressed_01.png,100x119,900x1900,"779,147,100,119",602
ui_common_btn_primary_pressed_01.png,238x58,900x1900,"176,823,238,58",1643
ui_common_btn_primary_disabled_01.png,548x58,900x1900,"176,952,548,58",2718
ui_menu_btn_play_pressed_01.png,310x112,900x1900,"214,1314,310,112",4040
```

