# S1c-T07b — Page 9:19 en los 3 prefabs Overlay + `transition` legible en `Matching`

**Change**: `organizar-pantallas-para-arte` · slice **S1c / T-07b** · PR 7
**Qué se entregó**: root `Page` con `AspectRatioFitter` (`FitInParent`, 0,47368) en `MessageService.prefab`, `transition.prefab` y `PowerUpsSelectorPanel.prefab`; y el `CanvasGroup` del prefab `transition` con su estado de reposo real (`m_Alpha: 0`).
**Alcance respetado**: 0 escenas tocadas, 0 `.cs`, 0 `CanvasScaler`, 0 sort orders, 0 `.meta`. Todo el batch vive en 3 archivos bajo `Assets/Prefabs/`.
**Evidencia**: 8 PNG en esta carpeta — 3 de `Matching` (9:19/9:16/9:20, escena viva, sin trucos), 4 de `MessageService` en `Login` (before/after en 9:19 y 9:16) y 1 diagnóstico del velo. Los 8 verificados por cabecera IHDR (ancho/alto exactos).

---

## 1. La regla y por qué así

El design (§4, decisión D4) deja los 3 canvas de prefab en **`ScreenSpaceOverlay`** y les agrega un root `Page` con `AspectRatioFitter`: un prefab no puede serializar la cámara de la escena, así que no se convierte a `ScreenSpaceCamera`; en cambio, su contenido se ancla al área 9:19 con un componente stock. La excepción queda declarada en la spec (UCC-3): *el rect del canvas excede, el contenido visible no*.

Estructura resultante (idéntica en los 3, cambia solo dónde cuelga el `Page`):

| Prefab | Raíz | Canvas | Hijos que ahora cuelgan de `Page` |
|---|---|---|---|
| `MessageService.prefab` | `MessageService` (Transform) | `Canvas` · Overlay · order 100 · Scaler 900×1900 | `Panel`, `Top`, `Botton` |
| `transition.prefab` | `transition` (Transform) | `Canvas` · Overlay · order 90 · Scaler 900×1900 | `Image` |
| `PowerUpsSelectorPanel.prefab` | el propio `Canvas` · Overlay · order 100 | — (es la raíz) | `Filter`, `ConfirmButton`, `CancelButton` |

`Page` serializa `anchorMin (0,0)` / `anchorMax (1,1)` / `pivot (0,5; 0,5)` / `sizeDelta (0,0)` y el fitter `m_AspectMode: 3` (`FitInParent`) con `m_AspectRatio: 0.47368422` (= 900/1900). El fitter es `[ExecuteAlways]`: en edición ya deja el rect asentado, y por eso las capturas de abajo son evidencia de la geometría real.

### Geometría medida (unidades del canvas, aspect asentado)

| Aspecto | Canvas Overlay | `Page` (medido) | ratio |
|---|---|---|---|
| 900×1900 (9:19) | 900×1900 | **900×1900** | 0,47368 |
| 900×1600 (9:16) | 900×1600 | **757,89×1600** | 0,47368 |
| 900×2000 (9:20) | 900×2000 | **900×1900** | 0,47368 |

En 9:19 el `Page` es exactamente el canvas ⇒ **no hay doble escalado** (probado abajo: A/B byte-idéntico). En los aspectos desviados el contenido queda adentro del área 9:19, que es el mismo recorte que hace el `LetterBoxer` sobre la escena (71 px por lado en 9:16; 50 px arriba/abajo en 9:20).

### Lo que el `Page` NO hace (declarado)

Un `AspectRatioFitter` **no reflowa** hijos de tamaño fijo: solo define la caja. `MessageService/Panel` es una tarjeta de 900×311,5 con anclas centradas, así que a 9:16 sigue midiendo 900 y sobresale 71 px por lado del `Page`. Es correcto para lo que pidió el design (caja 9:19, cero reflow) y el reflow de esos hijos es territorio de T-14..T-21, no de este batch. Consecuencia: el par A/B de `MessageService` es idéntico en 9:19 **y** en 9:16 (ver §4).

---

## 2. `transition` en `Matching` — causa, y por qué el fix es una línea de prefab

### Lo que decía el reporte y lo que se midió

`verify` (WARNING 3) describía el síntoma como *"opaco, tapa todo y bloquea input"*. La parte visual es cierta; la de input **no**: el `CanvasGroup` del prefab serializa `m_Interactable: 0` y `m_BlocksRaycasts: 0`, así que el velo nunca bloqueó raycasts (el `Image` tiene `m_RaycastTarget: 1`, pero el grupo apaga el bloqueo).

### Causa (medida, no inferida)

| Hecho | Medición |
|---|---|
| El velo existe y es opaco en el asset | `Image` `{r:0,g:0,b:0,a:1}`, stretch full-rect, `CanvasGroup` `m_Alpha: 1` |
| En `Matching` la instancia está **activa** | no hay override `m_IsActive` en `Matching.unity`; en `Lobby`, `PvPPowerUps` y `Game` sí (`m_IsActive: 0` sobre `6463951386534501998`). `Matching` y `Lobby` comparten el mismo id de instancia (`1380429308`) y las **mismas 12 modificaciones menos esa** ⇒ es una divergencia de autoría, no un diseño |
| En runtime el velo YA se apaga | `TransitionService.Awake()` (`Assets/Scripts/Spaguetti/Services/Transitions/TransitionService.cs:23`) hace `canvasGroupSplashScreen.alpha = 0` + `EnableComponents(false)` |

Conclusión: **el negro es un estado de edición**, no un bug de juego. El valor `m_Alpha: 1` del asset describe un estado que el código nunca muestra (el primer frame de play ya corre `Awake`). Se ve negro solo en el Editor: en `Matching`, y también en `Login`/`DevGame`, donde el velo tapa la escena mientras no hay play.

### Fix aplicado

`transition.prefab`: `m_Alpha: 1` → **`m_Alpha: 0`** (1 línea). El estado de reposo serializado pasa a coincidir con el invariante que el código ya impone.

Por qué **no** alguna de las alternativas:

- **No** se desactivó la instancia en `Matching`: eso alinearía la autoría con las otras 4 escenas, pero `TransitionService` es un singleton `DontDestroyOnLoad` que se auto-registra en `Awake`; desactivar el root deja la escena sin servicio cuando se juega `Matching` sola (hoy `Lobby`/`PvPPowerUps` ya tienen esa limitación). El fix elegido **no desactiva nada**: pide fade y funciona igual que antes.
- **No** se tocó C#: el runtime ya era correcto. Cambiarlo habría agregado superficie sin arreglar la causa (el asset mentía).
- **No** se tocó ninguna escena (ni override, ni sort order): el batch cierra con 0 cambios en `Assets/Scenes`.

### Efecto colateral (bienvenido, declarado)

`Login` y `DevGame`, que también tienen la instancia activa, dejan de verse negros en el Editor. En play mode no cambia ni un frame: `Awake` ya hacía `alpha = 0`.

### El velo adentro del `Page`

El `Image` ahora estira 757,89 en 9:16 en vez de 900. Durante un fade, las bandas laterales quedan a cargo del matte del `LetterBoxer`, que es **negro puro**. Se midió con el velo forzado a `alpha: 1` **en memoria** (diagnóstico, nunca guardado) a 900×1600 y está entregado: `transition_curtain-on_900x1600_diag.png`. Banda izquierda (x 0..70) y derecha (x 829..899): **media 0,0/255 y máximo 0**; el centro también negro. O sea: no hay costura visible, y el velo sigue siendo visualmente full-screen.

---

## 3. Sort ladder — re-auditoría y decisión (sin cambios)

| Escena | Canvas | order serializado | order efectivo | estado | ¿puede tapar el dialog? |
|---|---|---|---|---|---|
| `MainMenu` | `Canvas` | 0 | 0 | activo | — |
| `MainMenu` | `CanvasDialog` (modal del menú) | **1** | 1 | inactivo (lo enciende `DialogController`) | — |
| `MainMenu` | `PowerUpsSelectorPanel` | **100** | 100 | **inactivo** | sí, si se activara |
| `Lobby` / `PvPPowerUps` | `PowerUpsSelectorPanel` | 100 | **heredado del root (1)** | canvas **anidado** (`m_OverrideSorting: 0`) | no: hereda |
| `Lobby` / `PvPPowerUps` / `Game` / `dev` | `transition` | 90 | 90 | inactivo (salvo `Login`/`DevGame`, que lo necesitan para registrarse) | — |
| `Login` / `DevGame` | `MessageService` | 100 | 100 | activo | — |

**Decisión: no se cambia ningún order.** Razones medidas:

1. **Donde el selector está cableado, su 100 no cuenta.** `PvPPowerUpsScreen` → `PowerUpsSelectionPanelView` es el único consumidor del panel, y en `PvPPowerUps`/`Lobby` la instancia es un canvas **anidado** con `m_OverrideSorting: 0`: su `sortingOrder` reportado es el del canvas raíz (1). El 100 serializado solo actúa si el prefab es canvas raíz.
2. **En `MainMenu`, donde sí es raíz, está inactivo y sin código vivo.** El único rastro es `powerUpsSelectionPanel: {fileID: 7700000201}` en `MainMenu.unity:11314`, un campo que `GameOptionMediator` (el script dueño del bloque) **ya no declara**: es una línea huérfana, no un cableado. Nada lo abre, así que no puede tapar el dialog.
3. **El dialogo no convive con el selector.** `CanvasDialog` existe solo en `MainMenu`; en `PvPPowerUps`, donde el selector sí se usa, no hay dialog. La capa modal queda arriba en todos los escenarios alcanzables.
4. **Los dos 100 no se cruzan nunca.** `MessageService` (100) y `PowerUpsSelectorPanel` (100) no coexisten en ninguna escena (inventario de instancias: `Login`/`DevGame` tienen `MessageService`, `Lobby`/`PvPPowerUps`/`MainMenu` tienen el selector). El empate es teórico.

Si alguien cableara el selector en `MainMenu`, el orden correcto **sigue siendo 100**: es un selector a pantalla completa y va arriba del dialog (1), como va arriba del canvas raíz en `PvPPowerUps`. Bajarlo de 1 lo dejaría detrás del menú (0) y sería invisible. Queda escrito acá para no re-litigarlo.

---

## 4. Píxeles

### `Matching`, después del fix (sin trucos de sesión)

| Archivo | Resolución (IHDR) |
|---|---|
| `Matching_900x1900_after.png` | 900×1900 |
| `Matching_900x1600_after.png` | 900×1600 |
| `Matching_900x2000_after.png` | 900×2000 |

Contra la evidencia de PR 6 —que se tomó con el root **desactivado en memoria**—:

| Aspecto | Píxeles distintos | strong (≥24) | Máx. delta | Lectura |
|---|---|---|---|---|
| 900×1900 | **0** | 0 | 0 | byte-idéntico: el fix reproduce la evidencia sin desactivar nada |
| 900×1600 | 9.234 (0,64 %) | 0 | **4** | ruido de rasterizado, bbox x 71..827 = exactamente el área del pillarbox |
| 900×2000 | **0** | 0 | 0 | byte-idéntico |

### `MessageService` en su escena host (`Login`), antes/después del `Page`

| Aspecto | Píxeles distintos | strong | Lectura |
|---|---|---|---|
| 900×1900 | **0** | 0 | el `Page` es 900×1900 ⇒ mismo layout, **no hay doble escalado** |
| 900×1600 | **0** | 0 | el `Page` baja a 757,89, pero `Panel` es de 900 fijo y desborda igual (ver §1) |

Medición de geometría en el momento de la captura: `Panel` `sizeDelta (900; 311,5)` con anclas centradas, `Page` 757,89×1600 ⇒ el desborde de 71 px por lado es del hijo, no del `Page`.

**Caveat de captura (declarado)**: en `Login` el velo `transition` está activo y tapa la escena en edición; se desactivó **en memoria** para las 4 fotos (before y after), igual que el protocolo de PR 6, y la escena se recargó desde disco al terminar. El A/B tiene una sola variable: el `Page` del prefab. En `Matching` no hubo ningún truco: el velo está activo y en alpha 0.

Ninguna captura se guardó desde el Editor: `git status --porcelain -- Assets/Scenes` sale vacío.

---

## 5. Protocolo de entrega y normalización

1. **Autoría en Unity** (`PrefabUtility.LoadPrefabContents` → `Page` + `AspectRatioFitter` + reparent de los hijos → `SaveAsPrefabAsset`), con el Editor abierto: 3 prefabs, 0 errores de compilación.
2. **Medición del save**: Unity normaliza. Medido: `transition` +53/−2 (limpio), `MessageService` +60/−6 (mete `m_characterHorizontalScale` de TMP y `successColor`/`errorColor` de un script ajeno al batch) y `PowerUpsSelectorPanel` **+846/−795** (reordena el archivo entero). El de PUSP es inrevisable.
3. **Entrega**: revert a `HEAD` + reconstrucción mínima de los 5 hunks por prefab (m_Children del canvas, `m_Father` de cada hijo movido, y los 3 bloques nuevos: `GameObject Page`, su `RectTransform` y el `MonoBehaviour` del `AspectRatioFitter`), conservando **los fileIDs que generó Unity** para los objetos nuevos. CRLF conservado, 0 LF sueltos.
4. **Verificación releyendo el archivo entregado**: `AssetDatabase.Refresh(ForceUpdate)` + instanciar los 3 prefabs y medir `Page` en 9:19/9:16/9:20 (§1); abrir las 7 escenas con instancias (`Login`, `Lobby`, `Matching`, `PvPPowerUps`, `MainMenu`, `Game`, `DevGame`) y contar canvas con `Page` (2, 2, 1, 2, 1, 1, 2 — coincide con el inventario de instancias). Consola: **0 errores, 0 warnings**.

### Diff entregado

| Archivo | numstat | Contenido |
|---|---|---|
| `Assets/Prefabs/MessageService.prefab` | 57 / 6 | 3 hunks de reparent + 51 líneas del `Page` |
| `Assets/Prefabs/transition.prefab` | 54 / 3 | 2 hunks de reparent + 51 líneas del `Page` + `m_Alpha` 1→0 |
| `Assets/Prefabs/PowerUp/PowerUpsSelectorPanel.prefab` | 57 / 6 | 4 hunks de reparent + 51 líneas del `Page` |

`Assets/Scenes` y `Assets/Scripts`: **0 líneas**. `ProjectSettings/ProjectSettings.asset`: sucio desde antes, no tocado.

### Overrides de instancia (la puerta de una sola dirección)

Las 5 escenas con instancias de estos prefabs declaran **solo overrides de propiedades** (posición, `m_IsActive`, `m_Name`, anclas, `m_Enabled`); ninguna modifica `m_Children`, `m_Father` ni `m_AddedGameObjects` (verificado leyendo los 11 bloques `PrefabInstance`). Los fileIDs originales de todos los objetos se conservaron (comparación de conjuntos de IDs entre `HEAD` y lo entregado: 0 removidos, solo +3 por prefab). Los overrides de `PvPPowerUps` (14) y `Lobby` (1) siguen apuntando a los mismos targets y Unity no reporta instancias rotas.

---

## 6. Nombre y caveats, para el que venga

- **F-T07b-1**: `MessageService/Panel` mide 900×311,5 fijo. A 9:16 desborda el `Page`; el reflow es T-14..T-21.
- **F-T07b-2**: el `Page` del `PowerUpsSelectorPanel` se calcula contra la caja que lo hospeda, no contra la pantalla: anidado en `PvPPowerUps` bajo `Canvas/Filter` (790×1580) da **748,42×1580**; en `Lobby` bajo `Canvas/Filter` (900×1880) da **890,53×1880**. Es la consecuencia de F-S1c-6 (canvas anidado) y no se puede evitar desde el prefab: la caja la fija el host.
- **F-T07b-3**: el `CanvasGroup` de `transition` queda en `m_Alpha: 0`. Cualquiera que vea el prefab "vacío" en el Editor ahora sabe por qué: ese es su estado de reposo; `ShowSplashScreen` lo lleva a 1 en 1,5 s.
- **Sin V4 (device)**: sigue pendiente del design §9; este batch es Editor + emulación de `Camera.rect`, igual que PR 5/6.
