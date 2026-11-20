# S2b — migración de meta del cohorte `s2b` (avatares + retratos/banner de héroe)

**Change**: `organizar-pantallas-para-arte` · slice **S2b** · 9 piezas · `spriteMode 2 → 1` + remapeo de referencias en el mismo paso, pieza por pieza.
**Sin commitear a propósito**: queda para revisión. Rollback de 14 paths (§8).

---

## 1. Baseline del cohorte (medido de los archivos, antes de escribir)

`git status --short -- Assets` **vacío**. 9 piezas, todas en `spriteMode: 2`, **18 refs** de sub-sprite hasheadas.

| # | pieza | origen | lienzo | rect congelado en el `.meta` | guid | fileID viejo | refs |
|---|---|---|---|---|---|---|---|
| 1 | `ui_menu_avatar_player_01` | `Sprites/PlayerAvatar.png` | 1344×768 | **271,71 746×631** | `80793527…77f6` | 1873116096488874472 | 2 |
| 2 | `ui_menu_avatar_npc_01` | `Sprites/NpcAvatar.png` | 1024×1024 | **63,155 905×675** | `996c4027…f329` | −5044352846202842811 | 2 |
| 3 | `ui_menu_panel_profile_01` | `Sprites/Heroes/BGProfile.png` | 1920×1080 | 0,0 1920×1080 (full) | `72ed9a9c…8855` | −463948951673354684 | 5 |
| 4 | `hero_rojo_portrait_idle_01` | `rojo_idle.png` | 1024×1024 | **175,0 716×1024** | `c385d689…b4ee` | 4501473769023296242 | 1 |
| 5 | `hero_rojo_portrait_happy_01` | `rojo_alegre.png` | 1024×1024 | **40,0 723×1024** | `7c968f1d…fb45` | 1618486498139659625 | 2 |
| 6 | `hero_rojo_portrait_angry_01` | `rojo_enojo.png` | 1024×1024 | **88,0 925×1024** | `3617c5df…5fa1` | 2570779558932556528 | 1 |
| 7 | `hero_rojo_portrait_determined_01` | `rojo_determinacion.png` | 1024×1024 | **24,0 924×1009** | `7eff919c…ecb4` | 5945098726244164508 | 1 |
| 8 | `hero_rojo_portrait_surprised_01` | `rojo_sorpresa.png` | 1024×1024 | **177,0 606×1024** | `c275f44f…f1be` | −6446150744945478788 | 1 |
| 9 | `hero_rojo_banner_01` | `HeroeRojo.png` | 1344×768 | **321,249 10×28** + **326,0 696×768** (2 sub-sprites) | `aa48d609…5604` | 6343446867228487132 / −6369504855698502937 | 3 |

`hero_dialog_bg_01` (**JPG**) **no está en el cohorte**: el seam lo excluye a propósito (`s2c`). Confirmado en el dry-run.

## 2. Dry-run de la sesión (antes de escribir)

`DryRun("s2b", null)` → **exit 0**

```
seleccion: cohort=s2b | piezas=9 | refs=18 | filas=10
TOTAL 9 piezas con operaciones planificadas | 18 ocurrencias de fileID a remapear
1 piezas ya conformes (sin operaciones)
```

- `filas=10`: la fila 10 del cohorte es `ui_menu_picture_profile_01` (48×48), **ya en `spriteMode 1`** → 0 operaciones, 0 escrituras.
- 9 piezas / **18 refs** == lo previsto. 3 archivos de escena + 2 de prefab/asset serán tocados.
- **Ojo con la firma**: `DryRun(null, "s2b")` NO sirve — el segundo parámetro es `pieces` (glob) y tiene precedencia; "s2b" como glob no matchea nada → **exit 2** `pieces no matcheo ninguna pieza REPLACE`. El cohorte va en el **primer** argumento: `DryRun("s2b", null)` / `Apply("s2b", null, false)`. (Medido en esta sesión; ver F-S2b-1.)

## 3. Apply

`Apply("s2b", null, false)` → **exit 0**
`9 .meta reimportados, 15 archivos YAML reescritos, 18 referencias remapeadas a 21300000`
Reporte: `Temp/art-contract/reports/art-meta-migrator-20261120-191525.md`

## 4. Diff (14 archivos: 9 `.meta` + 5 consumidores)

| Archivo | numstat | Contenido |
|---|---|---|
| `Assets/Sprites/PlayerAvatar.png.meta` | 2 / 2 | `spriteMode: 2 → 1` + `spriteID: "" → 5e97eb03…` (nivel sheet) |
| `Assets/Sprites/NpcAvatar.png.meta` | 2 / 2 | idem |
| `Assets/Sprites/Heroes/BGProfile.png.meta` | 2 / 2 | idem |
| `Assets/Sprites/Heroes/rojo_{idle,alegre,enojo,determinacion,sorpresa}.png.meta` | 2 / 2 c/u | idem |
| `Assets/Sprites/Heroes/HeroeRojo.png.meta` | 2 / 2 | idem |
| `Assets/Scenes/Lobby.unity` | 4 / 4 | 4 `m_Sprite` / `sprite` remapeados |
| `Assets/Scenes/PvPPowerUps.unity` | 4 / 4 | idem |
| `Assets/Scenes/MainMenu.unity` | 7 / 7 | 5 retratos (`spritesImages`) + `defaultProfilePicture` (banner) + 1 `m_Sprite` (alegre) |
| `Assets/Prefabs/Heroes/Ui/Cards/HeroPrefab.prefab` | 2 / 2 | `Avatar/Art` (banner) + `bg` (BGProfile) |
| `Assets/Prefabs/Heroes/List/HeroList.asset` | 1 / 1 | `art:` (banner) |

**36 insertions / 36 deletions**: cada línea agregada es un `{fileID: 21300000, guid: <mismo>}` y cada borrada su `{fileID: <viejo>, guid: <mismo>}`. **0 ruido estructural.** Los 9 PNG **no cambiaron** (git status solo ve `.meta`): SHA-256 verificado, ver §5.3.

## 5. Verificación dura

### 5.1 Geometría medida en Unity (no en el texto del `.meta`)

| pieza | mode | textura | `sprite.rect` | `sprite.bounds` (u) | borde | maxTex |
|---|---|---|---|---|---|---|
| `ui_menu_avatar_player_01` | **Single (1)** | 1344×768 | **0,0 1344×768** | 13.44 × 7.68 | 0/0/0/0 | 2048 |
| `ui_menu_avatar_npc_01` | **Single (1)** | 1024×1024 | **0,0 1024×1024** | 10.24 × 10.24 | 0/0/0/0 | 2048 |
| `ui_menu_panel_profile_01` | **Single (1)** | 1920×1080 | **0,0 1920×1080** | 19.20 × 10.80 | 0/0/0/0 | 2048 |
| `hero_rojo_portrait_idle_01` | **Single (1)** | 1024×1024 | **0,0 1024×1024** | 10.24 × 10.24 | 0/0/0/0 | 2048 |
| `hero_rojo_portrait_happy_01` | **Single (1)** | 1024×1024 | **0,0 1024×1024** | 10.24 × 10.24 | 0/0/0/0 | 2048 |
| `hero_rojo_portrait_angry_01` | **Single (1)** | 1024×1024 | **0,0 1024×1024** | 10.24 × 10.24 | 0/0/0/0 | 2048 |
| `hero_rojo_portrait_determined_01` | **Single (1)** | 1024×1024 | **0,0 1024×1024** | 10.24 × 10.24 | 0/0/0/0 | 2048 |
| `hero_rojo_portrait_surprised_01` | **Single (1)** | 1024×1024 | **0,0 1024×1024** | 10.24 × 10.24 | 0/0/0/0 | 2048 |
| `hero_rojo_banner_01` | **Single (1)** | 1344×768 | **0,0 1344×768** | 13.44 × 7.68 | 0/0/0/0 | 2048 |

**Los 2 avatares y los 5 retratos ahora usan el lienzo completo.** El caso más duro, `rojo_enojo` (925×1024 de 1024 de ancho), mide **1024×1024**.

> **No confundir `sprite.textureRect` con el crop.** En `Single` + `Mesh Type: Tight`, `textureRect` devuelve el **bbox del alfa** (p. ej. `270.04,70.05 747.84×632.87` en PlayerAvatar), no el rectángulo de recorte del importador: `sprite.rect` y `bounds` son el lienzo completo y el mesh cubre todo. Control medido con sprites que **nunca** se migraron (`Assets/Sprites/BALLPASS/*.png`, `Single` de origen): muestran el mismo patrón (`rect` full, `textureRect` < full, `bounds` full). La prueba definitiva del render es el gate de §6 (comparación 1:1 del patrón sintético).

### 5.2 Referencias (0 colgadas) — conteo global sobre 281 assets de texto

| | antes | después |
|---|---|---|
| refs a los 10 fileIDs viejos del cohorte | **18** | **0** |
| refs a `21300000` en total | 148 | **166** (+18) |
| refs a `21300000` con los 9 GUIDs del cohorte | 0 | **18** |
| refs a `2800000` (Texture2D) | **141** | **141** (sin tocar) |

Atómico por pieza (flip y remapeo en el mismo estado):

| pieza | flip | refs viejas | refs `21300000` | colgadas |
|---|---|---|---|---|
| avatar_player | 2→1 | 2 | 2 | 0 |
| avatar_npc | 2→1 | 2 | 2 | 0 |
| panel_profile | 2→1 | 5 | 5 | 0 |
| portrait_idle | 2→1 | 1 | 1 | 0 |
| portrait_happy | 2→1 | 2 | 2 | 0 |
| portrait_angry | 2→1 | 1 | 1 | 0 |
| portrait_determined | 2→1 | 1 | 1 | 0 |
| portrait_surprised | 2→1 | 1 | 1 | 0 |
| banner | 2→1 | 3 (2 sub-sprites) | 3 | 0 |

Cero flip huérfano, cero ref colgada.

**Consecuencia declarada en el banner**: `HeroeRojo` tenía 2 sub-sprites; la ref de `HeroList.asset` apuntaba al **astilla de 10×28** (`HeroeRojo_0`) y las otras 2 a `HeroeRojo_1` (696×768). Las 3 se pliegan a `21300000` (lienzo completo 1344×768). El asset `HeroList.asset` está **muerto** (§6 del manifiesto: nada lo consume), pero lo que declare es ahora el sprite entero, no la astilla. Es el resultado que exige el contrato (ASC-2), no una regresión silenciosa.

### 5.3 Integridad de bytes

- SHA-256 antes/después de los **9 PNG**: los 9 **idénticos**.
- SHA-256 antes/después de los **9 `.meta`**: los 9 cambian (es el flip esperado).
- El gate de §6 restaura y compara SHA-256 de PNG+`.meta` en las 10 piezas procesadas → `restoreFirst=True` y `restore=True` en todas.

### 5.4 Addressables y piezas ajenas

- **0 archivos** de `Assets/AddressableAssetsData` en el diff; ninguno de los 9 GUIDs aparece en esa carpeta.
- `ui_pass_reward_picture_01` (**rol dual, otro cohorte**) **no se tocó**: no está en el diff y su `.meta` sigue limpio.
- `ui_menu_picture_profile_01` (48×48): seleccionada por el cohorte, **ya conforme**, 0 escrituras.
- `ball_shadow_01`, `ui_login_bg_01`, `hero_dialog_bg_01` → `s2c`, intactas (siguen en `spriteMode 2`).

## 6. Gate sintético (`ArtSyntheticBatch`)

**La entrada por lote se NIEGA con exit 3** (F-PR10-4 extendido): las 9 `.meta` son `[TARGET]` y los 5 consumidores `[AJENO]`. Es correcto: el harness no puede probar restauración con `Assets/` sucio. Gate por lote: **pendiente post-commit**.

Veredictos por pieza con el pipeline del propio harness (`ProcessTarget`: patrón al px de entrega, swap, import, render, comparación 1:1, restauración con SHA-256):

| pieza | veredicto | rect medido | restoreFirst / restore |
|---|---|---|---|
| `ui_menu_avatar_player_01` | **Exact** | 1344×768 de 1344×768 | True / True |
| `ui_menu_avatar_npc_01` | **Exact** | 1024×1024 de 1024×1024 | True / True |
| `ui_menu_panel_profile_01` | **Exact** | 1920×1080 de 1920×1080 | True / True |
| `ui_menu_picture_profile_01` | **Exact** | 48×48 de 48×48 | True / True |
| `hero_rojo_portrait_idle_01` | **Exact** | 1024×1024 de 1024×1024 | True / True |
| `hero_rojo_portrait_happy_01` | **Exact** | 1024×1024 de 1024×1024 | True / True |
| `hero_rojo_portrait_angry_01` | **Exact** | 1024×1024 de 1024×1024 | True / True |
| `hero_rojo_portrait_determined_01` | **Exact** | 1024×1024 de 1024×1024 | True / True |
| `hero_rojo_portrait_surprised_01` | **Exact** | 1024×1024 de 1024×1024 | True / True |
| `hero_rojo_banner_01` | **Exact** | 1344×768 de 1344×768 | True / True |
| `hero_dialog_bg_01` | Skipped | — | `el origen no es PNG (ASC-5)` |

**9/9 Exact sobre las piezas migradas** (+1 Exact de control `ui_menu_picture_profile_01`), **0 salteadas por `spriteMode`**, 0 defectos, 0 hard failures. La única salteada es el **JPG de `s2c`** que el cohorte `avatars` del harness arrastra por prefijo (§F-S2b-2).
Evidencia: `Temp/art-contract/render/20261120-191733/`.

## 7. Auditor de manifiesto

| regla | antes | después | previsto |
|---|---|---|---|
| FORMAT-JPG | 5 | **5** | 5 |
| MODE-TARGET | **12** | **3** | 3 (−9) |
| RECT-STALE | 10 | **10** | 10 (sin cambio) |
| **total** | **27** | **18** | 18 (−9) |

- **MODE-TARGET 12 → 3**: exacto. Las 3 que quedan son `ball_shadow_01`, `ui_login_bg_01`, `hero_dialog_bg_01` → **todas de `s2c`**.
- **RECT-STALE 10 → 10**: el migrador no reescribe el rect cuando hay flip; **Unity lo descarta** (medido: `sprite.rect` full en las 9) pero el **texto del `.meta`** conserva el rect congelado y la regla lo sigue contando. **Sin regresión** (F-S2a-1).
- `MODE-ACTUAL-DRIFT`: pasa de 13 a **22** filas en el bloque INFO (no es gate).
- `RESULTADO FAIL` / `exit 1`: quedan las 18 reales (5 JPG + 3 mode de `s2c` + 10 rects muertos).

## 8. Captura

| archivo | contenido | IHDR |
|---|---|---|
| `MainMenu_900x1900.png` | MainMenu tal cual está en la escena (menú sin tarjetas de héroe: las instancia el `HeroController` en runtime) | **900×1900**, 8 bit, colorType 2, 823.990 B |
| `MainMenu-heroes_900x1900.png` | **evidencia dirigida**: los 6 sprites del cohorte renderizados con `Image` real sobre el canvas del MainMenu, en memoria | **900×1900**, 8 bit, colorType 2, 1.333.779 B |

Método: escena `MainMenu` abierta y **recargada desde disco al terminar** — SHA-256 del `.unity` **idéntico antes y después** (`yvuusQb7k7H38QI4…`), `dirty=False`, `S2B_TEMP` destruido. `Canvas.pixelRect = 900×1900`, `scaleFactor = 1`, `Screen = 900×1900`.

Qué se ve en `MainMenu-heroes_900x1900.png` (lo importante: **caras enteras**):

- `ui_menu_avatar_player_01` y `ui_menu_avatar_npc_01`: los 2 vehículos completos, **sin el recorte de 746×631 / 905×675**.
- `hero_rojo_portrait_angry_01` (el peor rect: 925×1024 de 1024) y `hero_rojo_portrait_determined_01` (el único con crop **en los dos ejes**): rostro y torso completos, sin franjas cortadas.
- `hero_rojo_banner_01`: personaje entero (1344×768), no la astilla 10×28.
- `ui_menu_panel_profile_01`: el fondo oscuro completo (1920×1080).
- Cero magenta, cero huecos, cero transparencias rotas.

## 9. Consola

0 `error CS`, 0 excepciones de import, 0 sprites faltantes. El único `LogError` de la sesión es el **dry-run mal invocado** a propósito para documentar F-S2b-1 (`pieces=s2b` no matchea) — 0 escrituras. La única `warning` es el rechazo del harness por árbol sucio (§6).

## 10. Rollback (14 paths)

```
git checkout -- \
  "Assets/Sprites/PlayerAvatar.png.meta" \
  "Assets/Sprites/NpcAvatar.png.meta" \
  "Assets/Sprites/Heroes/BGProfile.png.meta" \
  "Assets/Sprites/Heroes/rojo_idle.png.meta" \
  "Assets/Sprites/Heroes/rojo_alegre.png.meta" \
  "Assets/Sprites/Heroes/rojo_enojo.png.meta" \
  "Assets/Sprites/Heroes/rojo_determinacion.png.meta" \
  "Assets/Sprites/Heroes/rojo_sorpresa.png.meta" \
  "Assets/Sprites/Heroes/HeroeRojo.png.meta" \
  "Assets/Scenes/Lobby.unity" \
  "Assets/Scenes/PvPPowerUps.unity" \
  "Assets/Scenes/MainMenu.unity" \
  "Assets/Prefabs/Heroes/Ui/Cards/HeroPrefab.prefab" \
  "Assets/Prefabs/Heroes/List/HeroList.asset"
```

Los 9 PNG no están en la lista porque **no cambiaron**. `Docs/art/verify/S2b/` es nuevo (borrar la carpeta si se revierte).

## 11. Hallazgos de S2b

- **F-S2b-1**: la firma es `(cohort, pieces, …)`. Pasar el cohorte en el slot de `pieces` (`DryRun(null, "s2b")`) falla con exit 2 porque `pieces` tiene precedencia y se interpreta como glob. Los runbooks tienen que escribir `Apply("s2b", null, false)`.
- **F-S2b-2**: el cohorte `avatars` del **harness** no es idéntico al `s2b` del **migrador**: arrastra `hero_dialog_bg_01` (JPG de `s2c`) y suma `ui_menu_picture_profile_01` (ya conforme). Es la misma tensión que F-PR10-3/D27, ahora medida: 11 targets / 10 Exact / 1 Skipped ASC-5.
- **F-S2b-3**: `Sprite.textureRect` en `Single` + `Tight` es el **bbox del alfa**, no el crop del importador. Un verify que mida `textureRect` va a creer que el flip no funcionó. Medir `sprite.rect` + `sprite.bounds` (y el gate).
- **F-S2b-4 (heredado, sigue)**: el gate por lote es post-commit (F-PR10-4). Este batch queda con 9/9 por pieza y el lote pendiente.

## 12. Decisiones de S2b

- **D34**: se gateó con `ProcessTarget` por pieza (mismo pipeline del harness) y **no** se forzó el guard ni se stasheó nada. Revertir/stashear el working tree para pasar el guard habría destruido la evidencia del propio batch.
- **D35**: la captura de caras usa `Image` reales sobre el canvas del MainMenu **en memoria** (los heroes se instancian en runtime, así que en edición el menú no los muestra). Se recargó la escena desde disco y se verificó SHA del `.unity`.
- **D36**: la evidencia dirigida recorta a 380 px de ancho cada pieza (no 1:1) para que entren 6 en 900×1900. La prueba 1:1 la da el gate (§6), no el PNG.
