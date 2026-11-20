# S2a — migración de meta del cohorte `game_board_*` (T-09 / PR 10)

**Change**: `organizar-pantallas-para-arte` · slice **S2a** · 13 piezas · `spriteMode 2 → 1` con remapeo de referencias en el mismo paso.
**Sin commitear a propósito**: queda para revisión. Rollback de 16 paths (§6).

---

## 1. Dry-run de la sesión (antes de escribir)

```
seleccion: pieces=game_board_* | piezas=13 | refs=21 | filas=13
piezas seleccionadas: game_board_balance_bar_left_01.png, game_board_balance_bar_right_01.png,
  game_board_balance_base_01.png, game_board_line_01.png, game_board_line_shadow_01.png,
  game_board_shadow_long_01.png, game_board_shadow_mid_01.png, game_board_shadow_short_01.png,
  game_board_arrow_swap_01.png, game_board_arrow_swap_alt_01.png, game_board_arrow_shadow_01.png,
  game_board_arrow_shadow_02.png, game_board_backdrop_01.png
TOTAL 13 piezas con operaciones planificadas | 21 ocurrencias de fileID a remapear
```
13 / 21, cero piezas fuera del glob. `git status --short -- Assets` vacío antes del apply.

## 2. Apply

`Apply(null, "game_board_*", false)` → **exit 0**
`13 .meta reimportados, 9 archivos YAML reescritos, 21 referencias remapeadas a 21300000`
Reporte: `Temp/art-contract/reports/art-meta-migrator-20261120-184946.md`

## 3. Diff (14 archivos: 13 `.meta` + `Game.unity`)

| Archivo | numstat |
|---|---|
| `Assets/Scenes/Game.unity` | **21 / 21** — las 21 son `m_Sprite: {fileID: <viejo> …}` → `{fileID: 21300000 …}`, mismo GUID. 0 ruido estructural. |
| los 13 `.meta` | **28 / 2** cada uno |

Los 2 borrados por `.meta` son siempre `spriteMode: 2 → 1` y `spriteID: "" → 5e97eb03…`.
Los 26 agregados extra son bloques de plataforma `iOS` y `WindowsStoreApps` (`overridden: 0`) que Unity serializa al reimportar: inertes, pero inflan el diff. **El PNG no cambia en ninguno de los 13** (git status solo ve `.meta`).

## 4. Verificación dura

### 4.1 Geometría medida en Unity (no en el texto del `.meta`)

| pieza | mode | textura | sprite.rect | borde |
|---|---|---|---|---|
| balance_bar_left | Single | 9×134 | 0,0 9×134 | 0/0/0/0 |
| balance_bar_right | Single | 223×109 | 0,0 223×109 | 0/0/0/0 |
| balance_base | Single | 305×118 | 0,0 305×118 | 0/0/0/0 |
| line | Single | 305×10 | 0,0 305×10 | 0/0/0/0 |
| line_shadow | Single | 352×54 | 0,0 352×54 | 0/0/0/0 |
| shadow_long | Single | 352×164 | 0,0 352×164 | 0/0/0/0 |
| shadow_mid | Single | 276×170 | **0,0 276×170** | 0/0/0/0 |
| shadow_short | Single | 266×152 | 0,0 266×152 | 0/0/0/0 |
| arrow_swap | Single | 29×142 | 0,0 29×142 | 0/0/0/0 |
| arrow_swap_alt | Single | 29×142 | 0,0 29×142 | 0/0/0/0 |
| arrow_shadow_01 | Single | 59×172 | 0,0 59×172 | 0/0/0/0 |
| arrow_shadow_02 | Single | 59×172 | 0,0 59×172 | 0/0/0/0 |
| backdrop | Single | 921×2048 (downsample de 1220×2712) | 0,0 921×2048 | 0/0/0/0 |

`border` medido == columna `border` de §9 (0/0/0/0 en las 13). `maxTextureSize` 2048 == §9 en las 13.

### 4.2 Referencias (0 colgadas)

| | antes | después |
|---|---|---|
| refs a sub-sprite hasheada | **21** | **0** |
| refs a `21300000` | 0 | **21** |
| refs a `2800000` (Texture2D en los 2 `.mat`) | 4 | **4** |
| total en el cohorte | 25 | 25 |

`BalanceLine.mat` y `BalanceLineSombra.mat` **no aparecen en el diff** y siguen resolviendo `_BaseMap` / `_MainTex` al `Texture2D` (`LineaBalanza` 305×10, `LineaBalanzaSombra` 352×54). Es la trampa del cohorte y quedó intacta.

### 4.3 Atómico, pieza por pieza

| pieza | flip | refs viejas | refs 21300000 | colgadas |
|---|---|---|---|---|
| balance_bar_left | 2→1 | 4 | 4 | 0 |
| balance_bar_right | 2→1 | 4 | 4 | 0 |
| balance_base | 2→1 | 0 | 0 | 0 |
| line | 2→1 | 0 (solo 2800000) | 0 | 0 |
| line_shadow | 2→1 | 0 (solo 2800000) | 0 | 0 |
| shadow_long | 2→1 | 0 | 0 | 0 |
| shadow_mid | 2→1 | 4 | 4 | 0 |
| shadow_short | 2→1 | 4 | 4 | 0 |
| arrow_swap | 2→1 | 1 | 1 | 0 |
| arrow_swap_alt | 2→1 | 1 | 1 | 0 |
| arrow_shadow_01 | 2→1 | 1 | 1 | 0 |
| arrow_shadow_02 | 2→1 | 1 | 1 | 0 |
| backdrop | 2→1 | 1 | 1 | 0 |

Ningún flip huérfano, ninguna ref colgada. Las 4 piezas sin refs de sub-sprite no tenían consumidores que remapear (2 por `2800000`, 2 sin refs).

## 5. Gate sintético (`ArtSyntheticBatch`, `pieces=game_board_*`)

**La entrada por lote (`Run`) se NIEGA con exit 3**: el guard exige `git status --porcelain -- Assets` sin cambios ajenos y las 13 `.meta` son `[TARGET]`, `Game.unity` es `[AJENO]`. Es correcto por diseño: el harness no puede probar restauración con el árbol sucio (**F-PR10-4** extendido). El gate por lote queda para **después del commit**.

Veredictos por pieza obtenidos con el pipeline del propio harness (`ProcessTarget`: genera patrón, cambia bytes, render, compara, restaura con SHA-256):

| pieza | veredicto | restore | geometría |
|---|---|---|---|
| balance_bar_left | **Exact** | True/True | 9×134 · rect 0,0 9×134 |
| balance_bar_right | **Exact** | True/True | 223×109 · rect full |
| balance_base | **Exact** | True/True | 305×118 · rect full |
| line | **Exact** | True/True | 305×10 · rect full |
| line_shadow | **Exact** | True/True | 352×54 · rect full |
| shadow_long | **Exact** | True/True | 352×164 · rect full |
| shadow_mid | **Exact** | True/True | 276×170 · rect full |
| shadow_short | **Exact** | True/True | 266×152 · rect full |
| arrow_swap | **Exact** | True/True | 29×142 · rect full |
| arrow_swap_alt | **Exact** | True/True | 29×142 · rect full |
| arrow_shadow_01 | **Exact** | True/True | 59×172 · rect full |
| arrow_shadow_02 | **Exact** | True/True | 59×172 · rect full |
| backdrop | **Skipped** | — | `el origen no es PNG (ASC-5)` |

12/12 **Exact**; 1 salteada por **ASC-5 (JPG)**, no por `spriteMode 2`. Antes de S2a las 13 se salteaban por `spriteMode 2`: el cohorte gateable pasa de 53 a **65** piezas.
SHA-256 de los 13 PNG y de los 13 `.meta` antes y después: **26/26 idénticos** (prueba propia, más fuerte que el porcelain).

## 6. Auditor de manifiesto

| regla | antes | después |
|---|---|---|
| FORMAT-JPG | 5 | 5 |
| MODE-TARGET | 25 | **12** |
| MODE-ACTUAL | 0 | **13** |
| RECT-STALE | 10 | **10** |
| **total** | **40** | **40** |

- **MODE-TARGET 25 → 12**: exacto (−13).
- **RECT-STALE no bajó a 9**: `game_board_shadow_mid_01` conserva el rect congelado `105,2 159×165` **en el texto del `.meta`**. Unity lo descarta al pasar a Single (medido: `sprite.rect = 0,0 276×170`), pero el migrador no reescribe el rect cuando hay flip (regla declarada: "se descarta con el flip"). Violación preexistente, sin regresión.
- **MODE-ACTUAL 0 → 13**: la columna `mode_actual` de §9 es una medición histórica congelada (2); ahora que la realidad es 1, la regla dispara en las 13. Por eso el total no baja: −13 +13.
- `UiContractAudit.Run()` → **160 hallazgos, idéntico** a PR 8 (ASC-3 23 · UCC-5 61 · ULN-1 76 · UCC-1/2/3 0). Ningún canvas tocado.
- Addressables: **0 archivos cambiados**; las 13 piezas no tienen dirección.

## 7. Captura

`Game-board_900x1900.png` — 900×1900, IHDR `bitDepth 8 colorType 2`, 911.414 bytes.
Escena `Game` abierta (no guardada: SHA-256 del archivo idéntico antes y después, `numstat` sigue 21/21).
Se ven el tablero (3 líneas de balanza + 4 flechas de swap + sombras), el HUD y los avatares. Cero magenta, cero huecos.
Nota: `Matchballance_0` (backdrop) está **inactivo en la escena** con alpha 0,133 — estado previo, no lo tocó esta migración.

## 8. Rollback

16 paths; los 2 `.mat` quedan en la lista aunque no cambiaron (no-op, no falla).

```
git checkout -- \
  "Assets/Sprites/V3_images/balanza 1.png.meta" \
  "Assets/Sprites/V3_images/balanza 2.png.meta" \
  "Assets/Sprites/V3_images/balanza 3.png.meta" \
  "Assets/Sprites/V3_images/LineaBalanza.png.meta" \
  "Assets/Sprites/V3_images/LineaBalanzaSombra.png.meta" \
  "Assets/Sprites/V3_images/sombra 1.png.meta" \
  "Assets/Sprites/V3_images/sombra 2.png.meta" \
  "Assets/Sprites/V3_images/sombra 3.png.meta" \
  "Assets/Sprites/V3_images/flechita 11.png.meta" \
  "Assets/Sprites/V3_images/flechita 2.png.meta" \
  "Assets/Sprites/V3_images/sombra flechita 1.png.meta" \
  "Assets/Sprites/V3_images/sombra flechita 2.png.meta" \
  "Assets/Sprites/Matchballance.jpg.meta" \
  "Assets/Scenes/Game.unity" \
  "Assets/Prefabs/BalanceLine.mat" \
  "Assets/Prefabs/BalanceLineSombra.mat"
```
