# Triaje de imágenes huérfanas — v1

**Cambio**: `refresh-visual-cliente` (RT-11) · **Repo**: MatchBall · **Base**: `e941c14`
**Barrido**: 2026-11-11 · **Ámbito**: `Assets/Sprites/**` (vendor `kenney_ui-pack` excluido)
**Naturaleza**: documento INTERNO. **No se manda al cliente y no se ejecuta en este cambio** — es
triaje, no borrado. Cualquier `delete` de esta tabla es una decisión para un cambio propio.

---

## 1. Cómo se armó la tabla

Barrido GUID→archivo sobre todo el proyecto. Cuenta como referencia cualquier `guid:` / `m_GUID:`
(incluye Addressables) presente en `.prefab`, `.unity`, `.asset`, `.mat`, `.controller`,
`.overrideController`, `.anim`, `.json`, `.txt` y `.mask`. Script completo, reproducible tal cual:

```python
import os, re
from collections import defaultdict

ROOT = "D:/Unity/MatchBall"
VENDOR = "kenney_ui-pack"
TEXT_EXT = (".prefab", ".unity", ".asset", ".mat", ".controller",
            ".overrideController", ".anim", ".json", ".txt", ".mask")

# 1) guid -> asset path, leyendo cada *.meta
path_of = {}
for dirpath, _, files in os.walk(os.path.join(ROOT, "Assets")):
    for fn in files:
        if not fn.endswith(".meta"):
            continue
        head = open(os.path.join(dirpath, fn), errors="ignore").read(400)
        m = re.search(r"^guid:\s*([0-9a-f]{32})", head, re.M)
        if m:
            path_of[m.group(1)] = os.path.join(dirpath, fn[:-5])

# 2) guid referenciado en cualquier archivo de texto del proyecto
refs = defaultdict(set)
for dirpath, _, files in os.walk(os.path.join(ROOT, "Assets")):
    for fn in files:
        if os.path.splitext(fn)[1] not in TEXT_EXT:
            continue
        p = os.path.join(dirpath, fn)
        with open(p, errors="ignore") as f:
            for g in re.findall(r"(?:m_)?[Gg][Uu][Ii][Dd]:\s*([0-9a-fA-F]{32})", f.read()):
                refs[g].add(p)

# 3) huerfano = imagen de Assets/Sprites sin ninguna referencia (vendor excluido)
for dirpath, _, files in os.walk(os.path.join(ROOT, "Assets", "Sprites")):
    for fn in files:
        if os.path.splitext(fn)[1].lower() not in (".png", ".jpg", ".jpeg"):
            continue
        p = os.path.join(dirpath, fn)
        rel = os.path.relpath(p, ROOT).replace("\\", "/")
        if VENDOR in rel:
            continue
        guid = next((k for k, v in path_of.items() if v == p), None)
        if not guid or not refs[guid]:
            print(rel)
```

**Paridad con el baseline del spec (RT-11)**: 140 imágenes en `Assets/Sprites` sin vendor → **89
referenciadas + 51 huérfanas**. El barrido repetido devuelve el mismo conjunto de 51: si aparece una
imagen nueva en esta tabla, hay una referencia rota que arreglar antes de tocar nombres.

---

## 2. Resultado

| Acción | Filas |
|---|---|
| `delete` | 35 |
| `keep` | 11 |
| `promote` | 5 |
| **Total** | **51** |

`promote` = el archivo ya es la fuente de una pieza del manifiesto (o su candidato declarado); no se
borra, se queda con nombre nuevo y el `.meta` viaja con él. `keep` = arte único que nadie reemplazó y
que nadie puede borrar sin que el cliente lo confirme.

---

## 3. Las 51 filas

| # | archivo | px | grupo | acción | promueve | evidencia / nota |
|---|---|---|---|---|---|---|
| 1 | `BALLPASS/028abb7f-d037-4bf8-99b7-006cb15a94b6.png` | 1024×1024 | UUID sin nombre | **keep** | — | 0 referencias por GUID · identidad desconocida: si el cliente la reconoce, renombrar y promover en una v2 |
| 2 | `BALLPASS/5affbe36-3b0b-4031-95e9-aa8532fede6b.png` | 1024×1024 | UUID sin nombre | **keep** | — | 0 referencias por GUID · identidad desconocida: si el cliente la reconoce, renombrar y promover en una v2 |
| 3 | `BALLPASS/BALLPASS.png` | 6835×2664 | BallPass numerado | **delete** | — | 0 referencias por GUID · copia de la serie; los 7 con address están vivos y NO se tocan |
| 4 | `BALLPASS/BALLPASS10.png` | 2722×1105 | BallPass numerado | **delete** | — | 0 referencias por GUID · copia de la serie; los 7 con address están vivos y NO se tocan |
| 5 | `BALLPASS/BALLPASS12.png` | 2750×1116 | BallPass numerado | **delete** | — | 0 referencias por GUID · copia de la serie; los 7 con address están vivos y NO se tocan |
| 6 | `BALLPASS/BALLPASS13.png` | 1553×1553 | BallPass numerado | **delete** | — | 0 referencias por GUID · copia de la serie; los 7 con address están vivos y NO se tocan |
| 7 | `BALLPASS/BALLPASS14.png` | 2750×1116 | BallPass numerado | **delete** | — | 0 referencias por GUID · copia de la serie; los 7 con address están vivos y NO se tocan |
| 8 | `BALLPASS/BALLPASS15.png` | 1552×1553 | BallPass numerado | **delete** | — | 0 referencias por GUID · copia de la serie; los 7 con address están vivos y NO se tocan |
| 9 | `BALLPASS/BALLPASS16.png` | 2750×1116 | BallPass numerado | **delete** | — | 0 referencias por GUID · copia de la serie; los 7 con address están vivos y NO se tocan |
| 10 | `BALLPASS/BALLPASS17.png` | 1541×1541 | BallPass numerado | **delete** | — | 0 referencias por GUID · copia de la serie; los 7 con address están vivos y NO se tocan |
| 11 | `BALLPASS/BALLPASS18.png` | 2750×1117 | BallPass numerado | **delete** | — | 0 referencias por GUID · copia de la serie; los 7 con address están vivos y NO se tocan |
| 12 | `BALLPASS/BALLPASS19.png` | 1561×1561 | BallPass numerado | **delete** | — | 0 referencias por GUID · copia de la serie; los 7 con address están vivos y NO se tocan |
| 13 | `BALLPASS/BALLPASS20.png` | 2750×1116 | BallPass numerado | **delete** | — | 0 referencias por GUID · copia de la serie; los 7 con address están vivos y NO se tocan |
| 14 | `BALLPASS/BALLPASS21.png` | 1585×1586 | BallPass numerado | **delete** | — | 0 referencias por GUID · copia de la serie; los 7 con address están vivos y NO se tocan |
| 15 | `BALLPASS/BALLPASS22.png` | 1575×1574 | BallPass numerado | **delete** | — | 0 referencias por GUID · copia de la serie; los 7 con address están vivos y NO se tocan |
| 16 | `BALLPASS/BALLPASS23.png` | 2750×1116 | BallPass numerado | **delete** | — | 0 referencias por GUID · copia de la serie; los 7 con address están vivos y NO se tocan |
| 17 | `BALLPASS/BALLPASS6.png` | 1553×1553 | BallPass numerado | **delete** | — | 0 referencias por GUID · copia de la serie; los 7 con address están vivos y NO se tocan |
| 18 | `BALLPASS/BALLPASS8.png` | 1560×1560 | BallPass numerado | **delete** | — | 0 referencias por GUID · copia de la serie; los 7 con address están vivos y NO se tocan |
| 19 | `BALLPASS/BALLPASS9.png` | 2750×1117 | BallPass numerado | **delete** | — | 0 referencias por GUID · copia de la serie; los 7 con address están vivos y NO se tocan |
| 20 | `BALLPASS/BOLAS GIF/BOLAS ESPECIALES8.png` | 195×188 | Bolas especiales | **delete** | — | 0 referencias por GUID · la serie que se usa es ESPECIALES/1/2/3/7; la 8 no está cableada |
| 21 | `BALLPASS/c0c0ad28-877e-4616-be3f-d76e4a13144d.png` | 1024×1024 | UUID sin nombre | **keep** | — | 0 referencias por GUID · identidad desconocida: si el cliente la reconoce, renombrar y promover en una v2 |
| 22 | `BALLPASS/c4920390-cf40-4699-9c55-aa46b54741ca.png` | 1024×1024 | UUID sin nombre | **keep** | — | 0 referencias por GUID · identidad desconocida: si el cliente la reconoce, renombrar y promover en una v2 |
| 23 | `BALLPASS/d68c9eef-519a-4869-b766-f82d3d3dcccc.png` | 1024×1024 | UUID sin nombre | **keep** | — | 0 referencias por GUID · identidad desconocida: si el cliente la reconoce, renombrar y promover en una v2 |
| 24 | `BALLPASS/fa0cd7d5-e2ef-45c9-9695-ba824c66b958.png` | 1024×1024 | UUID sin nombre | **keep** | — | 0 referencias por GUID · identidad desconocida: si el cliente la reconoce, renombrar y promover en una v2 |
| 25 | `BG_Card_Player.png` | 600×400 | UI sobrante | **delete** | — | 0 referencias por GUID · fondo de card sin consumidor; la card viva es Card.png → ui_common_card_01 |
| 26 | `CheckLock.png` | 23×26 | UI sobrante | **promote** | `ui_pass_state_lock_01` | 0 referencias por GUID · estado bloqueado del pase: ya entra al manifiesto |
| 27 | `Group 45.png` | 52×92 | UI sobrante | **delete** | — | 0 referencias por GUID · sin consumidor |
| 28 | `Icon_Coins.png` | 72×40 | UI sobrante | **delete** | — | 0 referencias por GUID · el ícono de moneda del HUD es Currenci.png → ui_hud_icon_coin_01 |
| 29 | `Instructions.png` | 360×36 | UI sobrante | **delete** | — | 0 referencias por GUID · sin consumidor |
| 30 | `LabelBallPass.png` | 88×67 | UI sobrante | **delete** | — | 0 referencias por GUID · sin consumidor |
| 31 | `Line 3.png` | 4×41 | UI sobrante | **delete** | — | 0 referencias por GUID · sin consumidor |
| 32 | `LineToExperience.png` | 28×114 | UI sobrante | **delete** | — | 0 referencias por GUID · sin consumidor |
| 33 | `Numero.png` | 53×46 | UI sobrante | **delete** | — | 0 referencias por GUID · sin consumidor |
| 34 | `Numero2.png` | 48×26 | UI sobrante | **delete** | — | 0 referencias por GUID · sin consumidor |
| 35 | `Rectangle 27.png` | 43×186 | UI sobrante | **delete** | — | 0 referencias por GUID · sin consumidor |
| 36 | `Separador.png` | 43×455 | UI sobrante | **promote** | `ui_common_divider_01` | 0 referencias por GUID · divisor vertical: ya entra al manifiesto |
| 37 | `V3_images/balanza 1.png` | 305×118 | Tablero sobrante | **promote** | `game_board_balance_base_01` | 0 referencias por GUID · base de la balanza: ya entra al manifiesto |
| 38 | `V3_images/linea larga 1.png` | 10×2071 | Tablero sobrante | **delete** | — | 0 referencias por GUID · la línea viva es LineaBalanza.png → game_board_line_01 |
| 39 | `V3_images/linea mas corta 2.png` | 9×1371 | Tablero sobrante | **delete** | — | 0 referencias por GUID · la sombra viva de la línea es LineaBalanzaSombra.png → game_board_line_shadow_01 |
| 40 | `V3_images/sombra 1.png` | 352×164 | Tablero sobrante | **promote** | `game_board_shadow_long_01` | 0 referencias por GUID · sombra larga: ya entra al manifiesto |
| 41 | `V3_images/sombra larga 1.png` | 106×2111 | Tablero sobrante | **delete** | — | 0 referencias por GUID · la usada es sombra 1.png → game_board_shadow_long_01 (promovida) |
| 42 | `WinScreen.png` | 3102×3087 | Resultados | **delete** | — | 0 referencias por GUID · pantalla de resultado vieja: las vivas son YouWin.png / GameOver.png |
| 43 | `balance_img.png` | 66×23 | Fondos | **delete** | — | 0 referencias por GUID · sin consumidor |
| 44 | `bg_login.png` | 291×580 | Fondos | **delete** | — | 0 referencias por GUID · el login usa BG_V2.jpg → ui_login_bg_01 |
| 45 | `gold.png` | 120×76 | Fondos | **delete** | — | 0 referencias por GUID · el address `gold` apunta a BALLPASS1.png, no a este archivo |
| 46 | `personajes y banner basicos png/PRUEBA.png` | 4266×4267 | Personajes / banners | **keep** | — | 0 referencias por GUID · hoja maestra: no se borra antes de que el cliente confirme (es la única evidencia de ese arte) |
| 47 | `personajes y banner basicos png/banner id.png` | 2759×1120 | Personajes / banners | **keep** | — | 0 referencias por GUID · candidato a banner de héroe; el manifiesto ya usa Heroes/HeroeRojo.png |
| 48 | `personajes y banner basicos png/c9d93736-4553-417e-8d84-fe28b3130b2b.png` | 1024×1024 | Personajes / banners | **keep** | — | 0 referencias por GUID · UUID: identidad desconocida, se guarda hasta que el cliente la reconozca |
| 49 | `personajes y banner basicos png/imagen ID basico F.png` | 666×667 | Personajes / banners | **keep** | — | 0 referencias por GUID · candidato a retrato de perfil; pendiente de confirmación del cliente |
| 50 | `personajes y banner basicos png/imagen ID bsico M.png` | 667×667 | Personajes / banners | **keep** | — | 0 referencias por GUID · candidato a retrato de perfil; pendiente de confirmación del cliente |
| 51 | `switchBalls.png` | 69×137 | Tablero sobrante | **promote** | `game_board_swap_hint_01` | 0 referencias por GUID · candidato: queda FUERA de las 121 (sería la pieza 122) hasta que se apruebe |

---

## 4. Keep explícito — NO forman parte de las 51

Estos archivos **sí están referenciados** (por eso no son huérfanos) y están protegidos por contrato.
Se listan acá para que nadie los toque al hacer el reemplazo de arte:

| Pieza | Address | Cómo se referencia |
|---|---|---|
| `BALLPASS/BALLPASS1.png` | `gold` | Addressables `BallPass` |
| `BALLPASS/BALLPASS5.png` | `profileAvatar_2` | Addressables `BallPass` |
| `BALLPASS/BALLPASS4.png` | `profileAvatar_1` | Addressables `BallPass` |
| `BALLPASS/BALLPASS0.png` | `profilePicture_1` | Addressables `BallPass` |
| `BALLPASS/BALLPASS2.png` | `profilePicture_2` | Addressables `BallPass` |
| `BALLPASS/BALLPASS3.png` | `profileBackground_1` | Addressables `BallPass` |
| `BALLPASS/BALLPASS7.png` | `profileBackground_2` | Addressables `BallPass` |
| `BALLPASS/BOLAS GIF/BOLAS ESPECIALES.png` | `POWERUP_ICE` | Addressables `Default Local Group` |
| `BALLPASS/BOLAS GIF/BOLAS ESPECIALES1.png` | `POWERUP_LEAD` | Addressables `Default Local Group` |
| `BALLPASS/BOLAS GIF/BOLAS ESPECIALES2.png` | `POWERUP_JOKER` | Addressables `Default Local Group` |
| `BALLPASS/BOLAS GIF/BOLAS ESPECIALES3.png` | `POWERUP_RAY` | Addressables `Default Local Group` |

Son las **11 addresses** del proyecto (7 en el grupo `BallPass` + 4 en `Default Local Group`). Todas
apuntan a piezas que el manifiesto reemplaza **in-place**: si el reemplazo respeta path + `.meta`, las 11
siguen resolviendo sin tocar un solo archivo de Addressables.

---

## 5. Regla de ejecución (RT-9 + RT-11)

1. Ninguna fila de esta tabla se ejecuta en `refresh-visual-cliente`.
2. El reemplazo de arte es **in-place**: mismos bytes de PNG reemplazados, mismo path, mismo `.meta`.
3. Un renombre mueve el `.meta` junto al `.png`, en un commit dedicado y separado del cambio de arte.
4. Nunca borrar-y-reimportar una imagen referenciada: eso quema GUIDs y deja las 11 addresses colgadas.
5. Antes y después de cualquier campaña de arte: repetir el barrido y comparar contra estas 51 filas.
