# S1c — evidencia de conversión de render mode (escenas)

**Change**: `organizar-pantallas-para-arte` · slice **S1c** · batches **PR 5** (`Game`) y **PR 6** (los 4 canvas de escena restantes)
**Cambio aplicado**: `ScreenSpaceOverlay` → `ScreenSpaceCamera`, con la cámara y el plano declarados para cada escena.
**30 PNG** (5 escenas × 3 aspectos × before/after). Cada uno verificado contra su cabecera IHDR (ancho/alto exactos), no contra la intención.

---

## 1. Qué se convirtió

| Escena | Canvas | Antes | Después | Líneas de diff |
|---|---|---|---|---|
| `Game` (PR 5) | `Canvas_UI` (order 50) | Overlay | SSCam · Main Camera · plano 100 | 1 |
| `Game` (PR 5) | `TimeLineToStart/Canva/Canvas` (order 20) | Overlay | SSCam · Main Camera · plano 100 | 1 |
| `Lobby` (PR 6) | `Canvas` raíz (order 1) | Overlay · cam Main Camera · plano 100 | SSCam · **sin cambios de cámara** | 1 |
| `Matching` (PR 6) | `Canvas` raíz (order 1) | Overlay · cam Main Camera · plano 100 | SSCam · **sin cambios de cámara** | 1 |
| `PvPPowerUps` (PR 6) | `Canvas` raíz (order 1) | Overlay · cam Main Camera · plano 100 | SSCam · **sin cambios de cámara** | 1 |
| `MainMenu` (PR 6) | `CanvasDialog` (order 0, inactivo) | Overlay · **cam NULL** | SSCam · Main Camera (#180168347) · plano 100 | 2 |

- En **Lobby / Matching / PvPPowerUps** el `m_Camera` y el `m_PlaneDistance: 100` **ya estaban serializados** con la Main Camera de la escena; en Overlay Unity los ignora. El único campo que faltaba era `m_RenderMode`. La decisión de diseño (Main Camera de la escena + plano 100) coincide con lo que el asset ya decía.
- En **MainMenu/CanvasDialog** la cámara estaba en `{fileID: 0}`. Se copió la de `MainMenu/Canvas` (que ya era SSCam con Main Camera), como se decidió.
- **No** se tocaron: sort orders, anchors, sizes, `m_ReferenceResolution`, la `Camera.rect` serializada, ni un solo prefab.

---

## 2. Método de captura (Editor, modo edición)

1. **Resolución del Game view**: `UnityEditor.GameView.SetCustomResolution` (reflexión) + `EditorWindow.maximized = true`. Las 3 resoluciones son `900×1900`, `900×1600`, `900×2000` y cada PNG se verificó contra su cabecera IHDR.
2. **Letterbox emulado**: `LetterBoxer` solo corre en `Awake`/`Update` (play mode) y **no está en ninguna de estas escenas**. Se replicó su fórmula (`x=9`, `y=19`, `referenceMode=DesignedAspectRatio`) sobre `Camera.rect`:
   - 9:16 (900×1600) → pillarbox `rect = (0.078947, 0, 0.842105, 1)` → `pixelRect = 757.89×1600`
   - 9:19 (900×1900) → `rect = (0, 0, 1, 1)`
   - 9:20 (900×2000) → letterbox `rect = (0, 0.025, 1, 0.95)` → `pixelRect = 900×1900`
3. **Matte**: cámara temporal `S1cMatteCam_TEMP` (depth −100, cullingMask 0, clear negro) = réplica de `LetterBoxer.AddLetterBoxingCamera`. Se crea con `HideFlags.HideAndDontSave` (no ensucia la escena ni la guarda el auto-save) y se destruye antes de cada save.
4. **Captura en dos tiempos, por imagen**: (a) `SetCustomResolution` + `Camera.rect` + matte + `Canvas.ForceUpdateCanvases()` + repaint; (b) en una llamada aparte, `ForceUpdateCanvases()` + `RepaintAllViews()` + `ScreenCapture.CaptureScreenshot`. Con una sola llamada el `CanvasScaler` entrega el `scaleFactor` del frame anterior y la captura miente: se ve en `Screen.width` (queda en el valor viejo) mientras `Canvas.pixelRect` ya cambió. Dos tiempos = geometría asentada.
5. **Geometría reportada por imagen** (`canvas.pixelRect`, `renderingDisplaySize`, `Canvas.scaleFactor`, `RectTransform.rect` y `Camera.rect`) al momento de disparar la captura.

### Caveats por escena (declarados, no cosméticos)

- **Matching**: la instancia del prefab `transition` tiene un `Image` negro **opaco a pantalla completa**, activo, order 90. Con eso activo el screenshot es negro entero y **el par before/after da 0 píxeles distintos en los 3 aspectos** (medido antes de desactivarlo). Las 6 imágenes de `Matching` se tomaron con ese root **desactivado en memoria** durante toda la sesión de captura (before y after), y se reactivó al recargar la escena desde disco. El render mode fue la única variable del A/B.
- **MainMenu**: `CanvasDialog` está **inactivo** en la escena. Las 6 imágenes se tomaron con el root **activado en memoria** (también en el "before"), porque un canvas inactivo da un par idéntico y no prueba nada. El estado final del archivo sigue con el dialog inactivo: se recargó desde disco después de capturar.
- **Lobby / PvPPowerUps**: `PowerUpsSelectorPanel` es un canvas **anidado** (instancia de prefab hija del canvas raíz): su `Canvas.renderMode` reportado sigue al canvas raíz automáticamente. Su `m_RenderMode` serializado en el prefab sigue en 0 y **no se tocó** (es T-07b). El canvas `transition` de estas escenas es un root aparte y sigue en Overlay.

---

## 3. Geometría medida (prueba dura del cambio)

Unidades del canvas (`RectTransform.rect`) y `Canvas.scaleFactor` medidos con el aspect ya asentado:

| Aspecto | Overlay (before) | SSCam (after) |
|---|---|---|
| 900×1900 | 900×1900 u · sf 1 | 900×1900 u · sf 1 |
| 900×1600 | **900×1600 u** · sf 1 | **900×1900 u** · sf 0.8421052 (`pixelRect` 757.89×1600) |
| 900×2000 | **900×2000 u** · sf 1 | **900×1900 u** · sf 1 (`pixelRect` 900×1900) |

Idéntico en las 4 escenas de PR 6 y en las 2 de `Game` (PR 5): el espacio de unidades deja de depender del device (UCC-4) y queda clavado en 900×1900 u.

**Barras de matte medidas en los PNG after** (píxeles exactos, no redondeados):

| Aspecto | Barra izquierda | Contenido | Barra derecha | Barra superior | Contenido | Barra inferior |
|---|---|---|---|---|---|---|
| 900×1600 (las 4) | **71 px** | **758 px** | **71 px** | — | — | — |
| 900×2000 (las 4) | — | — | — | **50 px** | **1900 px** | **50 px** |

Promedio de las barras = **0.0/255** (negro puro): son matte, no contenido.

---

## 4. Píxeles antes/después

`differing_px` = algún canal distinto · `strong` = delta ≥ 24 (cambio visible, no ruido de AA).

| Par | Píxeles distintos | % | Máx. delta | strong (≥24) | Lectura |
|---|---|---|---|---|---|
| `Game_900x1900` (PR 5) | 501 / 1.710.000 | 0,0293 % | 6 | 0 | avatar del rival, imperceptible |
| `Game_900x1600` (PR 5) | 0 / 1.440.000 | 0 % | 0 | 0 | byte-idénticos |
| `Game_900x2000` (PR 5) | 501 / 1.800.000 | 0,0278 % | 6 | 0 | misma caja, corrida 50 px |
| `Lobby_900x1900` | 875.351 / 1.710.000 | 51,19 % | **7** | **0** | solo diferencias de rasterizado (SSCam vs Overlay) |
| `Lobby_900x1600` | 1.157.539 / 1.440.000 | 80,38 % | 255 | 537.408 (37,32 %) | UI recomprimida al área 9:19 |
| `Lobby_900x2000` | 1.228.967 / 1.800.000 | 68,28 % | 255 | 160.785 (8,93 %) | UI recompuesta + barras |
| `Matching_900x1900` | 1.414.362 / 1.710.000 | 82,71 % | **6** | **0** | solo rasterizado |
| `Matching_900x1600` | 1.251.442 / 1.440.000 | 86,91 % | 254 | 306.054 (21,25 %) | BG + título + loader dentro del pillarbox |
| `Matching_900x2000` | 1.789.866 / 1.800.000 | 99,44 % | 249 | 314.546 (17,47 %) | ídem, con letterbox |
| `PvPPowerUps_900x1900` | 872.374 / 1.710.000 | 51,02 % | **6** | **0** | solo rasterizado |
| `PvPPowerUps_900x1600` | 1.154.012 / 1.440.000 | 80,14 % | 255 | 534.401 (37,11 %) | UI recomprimida |
| `PvPPowerUps_900x2000` | 1.229.303 / 1.800.000 | 68,29 % | 251 | 160.797 (8,93 %) | UI recompuesta + barras |
| `MainMenu_900x1900` | 1.710.000 / 1.710.000 | **100 %** | 255 | 1.698.887 (99,35 %) | **el dialog desaparece** (ver §5) |
| `MainMenu_900x1600` | 1.438.402 / 1.440.000 | 99,89 % | 255 | 1.428.607 (99,21 %) | ídem + pillarbox |
| `MainMenu_900x2000` | 1.800.000 / 1.800.000 | **100 %** | 255 | 1.788.210 (99,34 %) | ídem + letterbox |

**Lectura**:

- **En el aspecto de diseño (9:19) la conversión es visualmente un no-op**: `Lobby`, `Matching` y `PvPPowerUps` cambian medio frame con delta máximo **6-7/255** (resampleo), y `Game` cambia 501 px. Nada se mueve, nada se corta. Esto es lo que exige UCC-3.
- **En los aspectos desviados la UI se va adentro del letterbox**: en 9:16 aparece pillarbox de 71 px por lado y en 9:20 letterbox de 50 px arriba y abajo, con el contenido en 758×1600 y 900×1900 px respectivamente. El contenido se **recompone** (no se estira ni se recorta): títulos, tarjetas Player/NPC, anillo de "LOOKING FOR RIVAL" y la barra inferior quedan completos en las 3 capturas.
- **`MainMenu` es el único caso roto**, y no por geometría: el dialog se va **detrás** del menú. Ver §5.

---

## 5. Hallazgos (leer antes de dar por bueno el PR)

### F-S1c-4 — `MainMenu/CanvasDialog` queda OCULTO detrás de `MainMenu/Canvas`

- **Qué pasa**: `CanvasDialog` (order 0, plano 100, Main Camera) y `Canvas` (order 0, plano 100, Main Camera) quedan **empatados** en todo lo que Unity usa para ordenar canvas en SSCam. Como Overlay siempre iba arriba de cualquier SSCam, antes el empate no existía; ahora el menú gana y el dialog no se ve.
- **Prueba**: los 3 pares de `MainMenu` cambian ~100 % de los píxeles. Con el dialog activo, el "after" muestra el menú, no el dialog.
- **Prueba de que es solo el sort order**: se repitió la captura con `CanvasDialog.m_SortingOrder = 1` **en memoria** (`C:/Users/luis_/AppData/Local/Temp/opencode/MainMenu_diag_order1.png`, 900×1600) y el dialog **reaparece**, correctamente adentro del pillarbox. No se aplicó: el alcance prohíbe tocar sort orders.
- **Arreglo de 1 línea para el batch siguiente**: `CanvasDialog.m_SortingOrder` ≥ 1 (o bajar `Canvas`). Es el mismo patrón que F-S1c-1 en `Game`.

### F-S1c-5 — `transition` está activo con un negro opaco a pantalla completa

En `Matching` la instancia del prefab `transition` (order 90) tiene un `Image` negro opaco **activo**: la pantalla de Matching se ve **negra entera** en la escena, hoy, con o sin este cambio. No lo arregla S1c (es T-07b) pero conviene saberlo: cualquier captura de esa escena sin desactivarlo miente. En `Lobby`/`PvPPowerUps` ese root está inactivo; en `MainMenu` la instancia está inactiva también.

### F-S1c-6 — canvas anidados que "cambian solos"

`PowerUpsSelectorPanel` (Lobby, PvPPowerUps) es un canvas **anidado**: su `renderMode`/`worldCamera` reportados siguen al canvas raíz. Al convertir el root, el panel pasa a SSCam **sin tocar su prefab** (`m_RenderMode` serializado en el `.prefab` sigue 0). No es un efecto colateral del batch: es cómo funciona un canvas hijo, y es exactamente lo que T-07b va a formalizar.

---

## 6. Proveniencia (declarado)

- Las capturas se tomaron sobre la **escena en memoria** del Editor. El A/B es válido porque before y after se tomaron del mismo contenido de escena con el render mode como única variable (más los caveats de §2).
- **Al guardar, Unity normaliza**. Medido en este batch: `Lobby` +64/−1 líneas (overrides de anchors de la instancia `PowerUpsSelectorPanel` + un float `-293.81198→-293.81195`), `Matching` +2/−1 (`m_characterHorizontalScale` de TMP y un `m_Version` reubicado), `PvPPowerUps` +13/−1. **Ninguna de esas líneas es del cambio.** En los tres casos el archivo se revirtió a `HEAD` y se reescribió la línea mínima; el resultado entregado es 1 línea por escena.
- Copias de las versiones normalizadas (no versionadas), por si alguien reclama esa normalización: `C:/Users/luis_/AppData/Local/Temp/opencode/{Lobby,Matching,PvPPowerUps}.unity.S1c-normalized-backup`.
- `MainMenu` **no se guardó** desde el Editor: se recargó desde disco para descartar el estado artificial de captura (dialog activo, sort order de diagnóstico) y los 2 cambios se aplicaron por script. Se validó **recargando la escena en Unity desde el archivo entregado**: el Editor reporta `CanvasDialog` en `ScreenSpaceCamera`, con la Main Camera de la escena y plano 100.
- Verificación final del árbol: `git diff` de `Assets/Scenes` = 7 líneas (`m_RenderMode` ×5 escenas, `m_Camera` ×1), cero `m_SizeDelta`, `m_Anchor*`, `m_Pivot`, `m_AnchoredPosition`, `m_LocalScale`, `m_SortingOrder`, `m_ReferenceResolution` ni `m_NormalizedViewPortRect`.

---

## 7. Fix de sort order (commit extra del PR 6)

Dos líneas de `m_SortingOrder`, nada más. Cierra **F-S1c-4** (MainMenu) y resuelve **F-S1c-1** (Game) con evidencia, no con gusto.

| Escena | Canvas | Antes | Después | Líneas |
|---|---|---|---|---|
| `MainMenu` | `CanvasDialog` | 0 (empatado con `Canvas`) | **1** | 1 |
| `Game` | `Canvas_UI` | 50 (empatado con `Canvas_UI_In_Game`) | **51** | 1 |

### F-S1c-4 — MainMenu: el dialog vuelve a verse

- `Canvas` se queda en **0** (no se toca); `CanvasDialog` pasa a **1**. 1 es suficiente porque el único canvas con el que competía es `Canvas` (0); `PowerUpsSelectorPanel` está en 100 pero **inactivo** y es de T-07b.
- **Evidencia** (`MainMenu-dialog_<aspecto>_after.png`, dialog **abierto**): 900×1900 / 900×1600 / 900×2000, verificadas por cabecera IHDR. En las tres el dialog se dibuja **arriba** del menú: en 9:19 ocupa el ancho completo; en 9:16 queda dentro del pillarbox (71 px por lado); en 9:20 dentro del letterbox (50 px arriba/abajo). Antes de este commit, las tres capturas mostraban el menú sin dialog (§4).

### F-S1c-1 — Game: la capa modal va arriba del HUD

Evidencia dura de qué contiene cada canvas (leída de la escena, no de los nombres):

| Canvas | Hijos relevantes | Rol |
|---|---|---|
| `Canvas_UI` (50 → **51**) | `GameOverPanel`, `WinPanel`, `MessagesUI` | capa **modal / resultado** |
| `Canvas_UI_In_Game` (50) | `UI/Player (1)`, `UI/Player (2)` (Profile + Points), `MultiplierPopupTemplate` ×2 | **HUD en vivo** |
| `Canvas_BG` (−1) | `bg` | fondo |
| `TimeLineToStart/Canva/Canvas` (20) | `Image` | transición de inicio |

Los paneles de resultado viven en `Canvas_UI` y el HUD en `Canvas_UI_In_Game`; con ambos en 50 el empate dejaba el orden a merced del traversal. Se sube **solo `Canvas_UI` a 51**: la capa modal queda arriba del HUD y debajo de `Canvas_BG`/`Canvas` no cambia nada (siguen −1 y 20). El canvas `Canvas` del prefab `transition` (90, inactivo) no se toca (T-07b).

- **Prueba de la semántica del orden** (diagnóstico en memoria, nunca guardado): con `MessagesUI/Panel` estirado a pantalla completa y opaco, `Canvas_UI = 51` → HUD **tapado** por completo; `Canvas_UI = 49` → el HUD **se dibuja encima** del panel. Guardadas en `C:/Users/luis_/AppData/Local/Temp/opencode/Game_order51_modal_over_hud.png` y `..._order49_hud_over_modal.png`.
- **Capturas entregadas**: `Game-hud_900x1900_after.png` (HUD solo) y `Game-result_900x1900_after.png` (`WinPanel` activado **en memoria**; no se puede llegar al resultado sin jugar un partido). Ambas 900×1900 verificadas por IHDR. Hoy los paneles no se solapan con el HUD en 9:19, así que el cambio es **latente en píxeles** (A/B 51 vs 49 sobre el resultado real: 63 px distintos, bbox 20×4): lo que garantiza es el orden, no un delta visible.

### Normalización de Unity en este batch (medida, revertida)

| Escena | numstat tras el save del Editor | Se revirtió a HEAD y se reescribieron |
|---|---|---|
| `MainMenu` | **5 / 17** (`powerUpsSelectionPanel` stripped, `m_TextStyleHashCode`, `m_AnchoredPosition` float, bloque stripped de 11 líneas) | 3 líneas: `m_RenderMode`, `m_Camera` (PR 6) + `m_SortingOrder` (este batch) |
| `Game` | **1360 / 1342** (reordenamiento masivo de componentes, el mismo de PR 5) | 3 líneas: `m_RenderMode` ×2 (PR 5) + `m_SortingOrder` (este batch) |

`git diff -U0 Assets/Scenes | grep m_SortingOrder` → **exactamente 2 cambios** (50→51 y 0→1). Los 5 archivos CRLF, 0 LF sueltos. Consola del Editor: **0 errores, 0 warnings**. Verificado recargando ambas escenas **desde el archivo entregado** (MainMenu: `CanvasDialog` order 1, SSCam, Main Camera, inactivo; Game: `Canvas_UI` 51, HUD 50).
