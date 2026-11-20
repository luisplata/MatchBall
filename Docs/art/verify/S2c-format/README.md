# S2c-format — JPG→PNG de `BG_V2` y `conversacion_bg_1`

**Change**: `organizar-pantallas-para-arte` · MatchBall (Unity 6000.3.10f1)
**Slice**: `S2c-format` — **2 de los 5 JPG** del contrato (ASC-5). Los otros 3 (`BG.jpeg`, `Matchballance.jpg`, `BG_Search_Single.jpeg`) siguen fuera de alcance por pedido.
**Puerta de un solo sentido**: cambiar la extensión cambia el path, el path nuevo es un asset nuevo (GUID nuevo) y toda referencia al GUID viejo queda colgada si no se remapea en la misma operación. Por eso este slice se hace con un tool, no a mano.

---

## 1. Qué cambió

| pieza | origen (borrado) | destino (nuevo) | GUID viejo | GUID nuevo | px | refs |
|---|---|---|---|---|---|---|
| `ui_login_bg_01` | `Assets/Sprites/BG_V2.jpg` (58.881 B) | `Assets/Sprites/BG_V2.png` (339.860 B) | `31abb8cd428ae0f4ab6b1e32bfe0cb84` | `ec3f8a03e58cb668bc1b11bd32449019` | 1024×1024 | **2** (1 primaria + 1 backup) |
| `hero_dialog_bg_01` | `Assets/Sprites/Heroes/conversacion_bg_1.jpg` (318.772 B) | `Assets/Sprites/Heroes/conversacion_bg_1.png` (1.751.516 B) | `2c92cf635c583f1478aceffc42666a35` | `ecc9fb843686c5608e120ce6cf0ca280` | 1344×768 | **1** (1 primaria) |

Los dos PNG son **RGBA8** (IHDR `bitDepth 8`, `colorType 6`), al **mismo px** que el JPG: no hay resize ni recompresión con otro `maxTextureSize`.

Referencias remapeadas (3 líneas, 3 archivos, 0 ruido):

| archivo:línea | antes | después |
|---|---|---|
| `Assets/Scenes/Login.unity:1196` | `fileID: -2423211018224590080, guid: 31abb8cd…` | `fileID: 21300000, guid: ec3f8a03…` |
| `Assets/_Recovery/0.unity:1196` (backup, **versionado**) | idem | idem |
| `Assets/Scenes/MainMenu.unity:14012` | `fileID: 4551842825865518708, guid: 2c92cf63…` | `fileID: 21300000, guid: ecc9fb84…` |

> El pedido esperaba **1 ref por pieza**. Hay 1 primaria por pieza **más 1 duplicado en `Assets/_Recovery/0.unity`** (escena de backup de Unity, trackeada en git, copia de Login). Se remapeó también: si no, después del borrado quedaba una referencia colgada en un archivo versionado y el chequeo de "0 referencias al GUID viejo" era falso. Ver **F-FMT-2**.

---

## 2. Tool: `Assets/Editor/ArtContract/ArtFormatConverter.cs`

Editor-only, sin asmdef (misma frontera que el resto de `Assets/Editor/ArtContract/`). 1.785 líneas.

- **Selección explícita**: lista de paths separada por comas. `*` y `?` se rechazan con exit 2 (un glob arrastraría los otros 3 JPG). Solo `.jpg`/`.jpeg` bajo `Assets/`.
- **GUID nuevo determinista**: `MD5("DarkVoid.ArtContract.ArtFormatConverter:v1:" + guidViejo)`. El dry-run imprime el GUID exacto que va a quedar escrito y el tool es re-corrible en cualquier máquina.
- **Dry-run por defecto**: imprime origen, GUID viejo, refs por archivo, destino, GUID nuevo, y la lista exacta de líneas a reescribir (antes/después).
- **Apply**: `LoadImage(jpg)` → `RGBA32` explícito → `EncodeToPNG()` (mismas dimensiones) → `<name>.png`; copia el `.meta` viejo cambiando **solo** el `guid`; `ImportAsset(ForceUpdate)`; `spriteImportMode = Single` (mode_target de §9); remapeo `fileID viejo + GUID viejo → fileID nuevo + GUID nuevo` (sub-sprites → `21300000`, Texture2D → `2800000`, cualquier otro id **aborta** en vez de adivinar).
- **Nunca borra** el `.jpg` ni su `.meta`. El borrado es el paso 5, separado.
- **Idempotente**: con el `.png` ya en su lugar y el GUID esperado, el plan queda en 0 operaciones y `Apply` devuelve 0 con `nada que aplicar` (medido, ver §3(f)).
- **Guard de árbol sucio** (mismo que `ArtMetaMigrator`): se niega a aplicar con `git status --porcelain -- Assets` no vacío, **eximiendo los paths que el propio tool posee** (su fuente, los `.png`/`.meta` destino y los consumidores de los GUID viejo y nuevo). Probado en negativo: con un archivo ajeno untracked devuelve **exit 3** y no escribe nada.
- **`Verify`** corre en dos modos: con el `.jpg` presente compara contra el `.meta` viejo (baseline real); con el `.jpg` borrado (paso 5 hecho) compara contra **§9** (`mode_target`, `max_tex`, `pivot`, `ppu`, `border`) y `px_entrega`.

Superficie: `DryRun(pieces)` · `Apply(pieces, allowDirty)` · `Verify(pieces)` · `PlanText(pieces)` · menús `Tools > Art Contract > Format Converter >` · `BatchDryRun/BatchApply/BatchVerify` con `-artFormatPieces`.

---

## 3. Verificación

**(a) Dimensiones y formato** — medidas en el header del archivo, no en la intención:

| pieza | JPG (SOF) | PNG (IHDR) | bitDepth | colorType |
|---|---|---|---|---|
| `ui_login_bg_01` | 1024×1024 | **1024×1024** | 8 | **6 (RGBA)** |
| `hero_dialog_bg_01` | 1344×768 | **1344×768** | 8 | **6 (RGBA)** |

**(b) Import settings** — `maxTextureSize`, `spritePixelsToUnits`, `spritePivot`, `spriteMode`, `spriteBorder` idénticos al `.meta` viejo (y, tras el borrado, iguales a §9):

| setting | viejo | nuevo | §9 |
|---|---|---|---|
| `maxTextureSize` | 2048 | 2048 | 2048 |
| `spritePixelsToUnits` | 100 | 100 | 100 |
| `spritePivot` | `{0.5, 0.5}` | `{0.5, 0.5}` | 0.5/0.5 |
| `spriteMode` | **2** | **1** | 1 |
| `spriteBorder` | `{0,0,0,0}` | `{0,0,0,0}` | 0/0/0/0 |

Medido además en Unity sobre el asset importado: `Sprite` con `fileID 21300000` (el que usan las referencias), `rect` = lienzo completo (1024×1024 y 1344×768), `Texture2D` del mismo px, `pixelsPerUnit 100`, `border (0,0,0,0)`. Sub-assets de `BG_V2.png`: exactamente 2 (`Texture2D` 2800000 + `Sprite` 21300000).

**(c) Referencias**

| medición | antes | después |
|---|---|---|
| ocurrencias del GUID viejo de `BG_V2` (todo el repo, sin `.git`/`Library`/`Temp`) | 2 | **0** |
| ocurrencias del GUID viejo de `conversacion_bg_1` | 1 | **0** |
| ocurrencias del GUID nuevo de `BG_V2` | 0 | **2** (Login + `_Recovery`) |
| ocurrencias del GUID nuevo de `conversacion_bg_1` | 0 | **1** (MainMenu) |
| `21300000` en los 85 REPLACE (auditor) | 154 | 154 · más 3 refs nuevas resueltas |

Las dos referencias se resolvieron **cargando la escena y comparando objetos**: `Login/Canvas/Image` → `sprite == BG_V2` con `AssetDatabase.GetAssetPath == Assets/Sprites/BG_V2.png`, rect `900×910`; `MainMenu/CanvasDialog/Panel/bg` → `sprite == conversacion_bg_1` con path `Assets/Sprites/Heroes/conversacion_bg_1.png`. Los 2 consumidores resuelven; 0 sprites nulos nuevos (ver (g)).

**(d) Renders** — 900×1900, IHDR-verificados, sin magenta ni huecos:

| archivo | contenido | IHDR |
|---|---|---|
| `Login_ui_login_bg_900x1900.png` | Login **tal cual está en la escena**: el arte queda tapado por el canvas `order 1` (negro opaco + logo) | **900×1900**, 8 bit, colorType 6 |
| `Login-ui_login_bg-direct_900x1900.png` | Login con el canvas `order 1` **desactivado en memoria**: se ve el arte completo (splash rosa/violeta), el título y el botón Login | **900×1900**, 8 bit, colorType 6 |
| `MainMenu-hero_dialog_bg_900x1900.png` | `CanvasDialog` **activado en memoria**: el arte de fondo (personaje + calle cyberpunk) detrás del diálogo lorem ipsum | **900×1900**, 8 bit, colorType 6 |

Método: Game view en `900×1900` (`GameView.SetCustomResolution` por reflexión) y render de la cámara consumidora (`Main Camera`) a una `RenderTexture` 900×1900 + `ReadPixels` + `EncodeToPNG`.

- **`ScreenCapture.CaptureScreenshot` no sirve acá**: en modo edición devolvió un PNG negro de 22.496 B dos veces seguidas (el Game view no repinta el frame en el que se dispara — el mismo caveat de dos tiempos que documenta `Docs/art/verify/S1c/README.md` §2). El render por cámara sí compone el canvas `ScreenSpaceCamera`.
- **Caveat Login**: el arte de `ui_login_bg_01` vive en el canvas `order 0` y la escena tiene un canvas `order 1` con un `Image` negro **opaco a pantalla completa** encima. Tal cual está la escena, el arte no se ve. No es una regresión de este slice (composición pre-existente); se capturan las dos vistas para que la decisión de ocultar/mover ese canvas sea consciente.
- Las escenas se **recargaron desde disco** después de capturar: `MainMenu.unity` con SHA-256 idéntico antes/después y `dirty=False`; el diff de las dos escenas es de **1 línea** cada una (solo el `m_Sprite`).

**(e) Auditor** (`python tools/art/audit_art_manifest.py`, exit 1):

| regla | antes | después |
|---|---|---|
| `FORMAT-JPG` | **5** | **3** ✅ |
| `MODE-TARGET` | 3 | **1** (solo `ball_shadow_01`, que es S2c sin formato) |
| `RECT-STALE` | 10 | 10 (preexistente, F-S2a-1) |
| **total** | **18** | **14** |

`FILAS 122 (REPLACE 85 | NEW 37 | addresses 11)` sin cambios. `FORMAT-JPG` baja porque §9 ahora declara `origen` en `.png` (ASC-5: "el manifiesto MUST declarar `.png` como formato de origen"); `MODE-TARGET` baja de rebote porque el auditor mide el `.meta` del path declarado, que ahora es el PNG en `Single`.

**(f) Idempotencia y guard** — con el árbol ya convertido: plan `convertibles=0 | refs=0`, `2 piezas ya conformes`, `Apply` → **exit 0** y `nada que aplicar` (0 escrituras). Con un archivo ajeno `?? Assets/Sprites/__guard_probe.txt`: `Apply` → **exit 3**, 0 escrituras.

**(g) Sprites nulos** — `m_Sprite: {fileID: 0}` en las 2 escenas: **1 (Login) y 11 (MainMenu)**, idénticos a `HEAD` (`git show HEAD:… | grep -c`). 0 nulos nuevos.

**(h) Consola**: 0 `error CS`, 0 excepciones de import, 0 referencias faltantes. El único `LogError` de la sesión es el rechazo del guard provocado a propósito para documentar `F-FMT-4` (0 escrituras).

---

## 4. Diff

| archivo | numstat | contenido |
|---|---|---|
| `Assets/Scenes/Login.unity` | 1/1 | `m_Sprite` → GUID nuevo |
| `Assets/Scenes/MainMenu.unity` | 1/1 | idem |
| `Assets/_Recovery/0.unity` | 1/1 | idem (backup versionado) |
| `Assets/Sprites/BG_V2.jpg` + `.meta` | borrado | paso 5 |
| `Assets/Sprites/Heroes/conversacion_bg_1.jpg` + `.meta` | borrado | paso 5 |
| `Assets/Sprites/BG_V2.png` + `.meta` | nuevo | RGBA8 1024×1024, GUID nuevo, `spriteMode 1` |
| `Assets/Sprites/Heroes/conversacion_bg_1.png` + `.meta` | nuevo | RGBA8 1344×768, GUID nuevo, `spriteMode 1` |
| `Assets/Editor/ArtContract/ArtFormatConverter.cs` (+`.meta`) | nuevo | el tool |
| `Docs/art/manifest-refresh-visual-v1.md` | 5/5 | §9 `origen` → `.png` (2 filas), §3 (2 filas), §4 (formato) |
| `Docs/art/orphan-triage-v1.md` | 1/1 | la fila 44 decía que el login usa `BG_V2.jpg` |

`ProjectSettings/ProjectSettings.asset` (1/1) es **preexistente** y no se tocó en este slice.

Costo declarado (R-g del design): el repo crece **+1,71 MB** de PNG permanentes. El peso de build no cambia (Unity recomprime).

---

## 5. Hallazgos

- **F-FMT-1 (desvío declarado contra ASC-5 literal)**: ASC-5 dice "conservando GUID" y el design D3 proponía `AssetDatabase.MoveAsset`. Este slice hace lo contrario a propósito: **PNG nuevo + GUID nuevo + remapeo completo**, como pidió el alcance. El invariante observable se cumple (0 referencias colgadas, mismas dimensiones, mismos import settings, mismas refs contadas antes/después), pero **el GUID cambió**: cualquier referencia externa al repo (un `.unitypackage`, un build cacheado, un ticket que cite el GUID) queda apuntando a la nada. Ninguno de los dos assets es addressable (`address: -` en §9) y el barrido de GUIDs viejos da 0 en todo el repo, así que no hay daño medido. Si el verify prefiere la letra de ASC-5, el camino es re-hacerlo con `MoveAsset` (el trabajo de remapeo se descarta).
- **F-FMT-2**: `ui_login_bg_01` tiene **2** referencias y no 1: `Login.unity:1196` + `Assets/_Recovery/0.unity:1196`. `_Recovery` es la escena de backup de Unity y **está versionada** (el auditor la trata como "fuera de la cuenta, no versionado" — esa suposición del auditor es incorrecta). Se remapeó también.
- **F-FMT-3**: el `.meta` nuevo conserva `internalIDToNameTable` y el bloque `spriteSheet` con el **fileID viejo** del sub-sprite, aunque `spriteMode` es 1. Es texto inerte (el sprite real es `21300000`, medido) y Unity lo dejó así al reimportar — misma deuda que F-S2a-1 en los 9 `.meta` de S2b. No se editó el YAML a mano (design D8: se escribe por API).
- **F-FMT-4**: `ScreenCapture.CaptureScreenshot` en modo edición devuelve **negro** si se dispara en la misma llamada que el `Repaint` (2/2 intentos, 22.496 B idéntico). La captura honesta es render de cámara a `RenderTexture`. Extiende el caveat de dos tiempos de S1c a un caso donde el segundo tiempo tampoco alcanza.
- **F-FMT-5**: `git status --porcelain` con `Trim()` se come el espacio inicial de la 1ra línea (` M path` → `M path`) y el parseo posterior corta un char del path (`ssets/Scenes/Login.unity`) → el guard rechazaba un árbol propio. Mismo bug que F-S2a-3, en otro archivo. Corregido con `TrimEnd()`.
- **F-FMT-6 (composición de Login)**: el arte de `ui_login_bg_01` está tapado en la escena por un canvas `order 1` negro opaco. El asset migró bien; la pantalla no lo muestra. Decisión de UI, no de contrato.

---

## 6. Rollback

```bash
# 1) devolver los 2 JPG y sus metas, y los 3 consumidores
git checkout -- "Assets/Sprites/BG_V2.jpg" "Assets/Sprites/BG_V2.jpg.meta" \
                "Assets/Sprites/Heroes/conversacion_bg_1.jpg" "Assets/Sprites/Heroes/conversacion_bg_1.jpg.meta" \
                "Assets/Scenes/Login.unity" "Assets/Scenes/MainMenu.unity" "Assets/_Recovery/0.unity" \
                "Docs/art/manifest-refresh-visual-v1.md" "Docs/art/orphan-triage-v1.md"

# 2) borrar lo nuevo
rm -f "Assets/Sprites/BG_V2.png" "Assets/Sprites/BG_V2.png.meta" \
      "Assets/Sprites/Heroes/conversacion_bg_1.png" "Assets/Sprites/Heroes/conversacion_bg_1.png.meta" \
      "Assets/Editor/ArtContract/ArtFormatConverter.cs" "Assets/Editor/ArtContract/ArtFormatConverter.cs.meta"
rm -rf "Docs/art/verify/S2c-format"

# 3) refresh en Unity y verificar árbol limpio
git status --short -- Assets
```

---

## 7. Lo que NO se tocó

- Los otros 3 JPG: `Assets/Sprites/BG.jpeg`, `Assets/Sprites/Matchballance.jpg`, `Assets/Sprites/BG_Search_Single.jpeg` (y `ball_shadow_01`, que es la otra pieza de S2c).
- `ProjectSettings/ProjectSettings.asset`, `Docs/pitch/`, `tools/pitch/`.
- Addressables: ninguno de los dos assets tiene entry.
- Ningún `.meta` se editó a mano: el PNG se importó por API y Unity reserializó.
