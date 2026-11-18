# Release a Play Store — runbook

Estado real del pipeline (verificado en `Android.yml`, no en el comentario del YAML):
**el workflow NO sube nada a Play Store.** `target=aab` compila un AAB firmado y lo adjunta a un
GitHub Release junto con el tag. La subida a Play Console es manual.

---

## Icono

El ícono del launcher y el de la ficha salen del **mismo artwork commiteado**. Si divergen, Play
rechaza la app por identidad de marca. Reglas duras:

| Asset | Tamaño / modo | Dónde termina |
|---|---|---|
| `Assets/Art/AppIcon/app_icon_background.png` | 432×432 RGB opaco `#22B14C` | Adaptive, capa **0** (6 slots) |
| `Assets/Art/AppIcon/app_icon_foreground.png` | 432×432 RGBA (solo los círculos) | Adaptive, capa **1** (6 slots) |
| `Assets/Art/AppIcon/app_icon_legacy.png` | 192×192 RGB | Legacy (6 slots) |
| `Assets/Art/AppIcon/app_icon_round.png` | 192×192 RGB | Round (6 slots) |
| `Docs/release/assets/store-icon-512.png` | 512×512 RGBA | Ficha de Play (default **y todas las locales**) |

**Generador** (fuente: `Assets/Sprites/matchballance_icon.png`; nunca se redibuja, se recorta y se
reescala en un paso):

```bash
python tools/icons/generate_icons.py --check      # exit 1 si un PNG no coincide en píxeles
python tools/icons/generate_icons.py              # regenera los 5 PNG
python tools/icons/generate_icons.py --manifest   # sha256 de los 5
```

**Guard de build** (`Assets/Editor/AppIconBuildGuard.cs`): en cada build Android re-aplica los 18
slots desde los assets versionados **antes** de compilar. Aborta con `BuildFailedException` si falta
un slot, una textura no es legible o mide distinto. También está el menú
`Tools > App Icons > Apply to Android Player Settings` para reasignar y persistir sin compilar.

```text
[AppIcon] Build bloqueado: no se pudieron aplicar los íconos de launcher Android.
  · Slot: Adaptive 432x432, capa 1 (foreground)
    Archivo: Assets/Art/AppIcon/app_icon_foreground.png
    Problema: no existe o no importa como Texture2D (Texture Type debe ser Default, no Sprite)
  Regenerar:  python tools/icons/generate_icons.py
```

**Verificador del AAB** (lo mismo que corre el CI, también sirve a mano):

```bash
python tools/icons/verify_aab_icons.py --aab release/MatchBall-X.Y.Z.aab
```

Exit `0` = PASS con inventario · `1` = falta un archivo/densidad, el fondo no es `#22B14C`, las capas
están invertidas o el dhash contra `store-icon-512` supera 8/64 · `2` = no pudo leer el AAB. El
mensaje de fallo nombra **densidad + archivo** (`falta base/res/mipmap-hdpi/ic_launcher_foreground.png`).

En CI es el step `Verify launcher icons in AAB` del job `build-aab`, entre `Normalize AAB` y
`upload-artifact`: valida **el mismo archivo** que se adjunta al Release. Si falla, no hay artifact
y `publish` se saltea solo.

---

## Antes de disparar

| Check | Cómo |
|---|---|
| Estás en `main` | El job `validate` aborta si `target` es `aab`/`all` y el ref no es `refs/heads/main`. |
| El build de itch pasó | Si `dev` (APK + WebGL) falló, el release no va a compilar mejor. |
| **versionCode** | Se calcula como `major*1e6 + minor*1e3 + patch`. `v0.2.11` → `2011`. Play rechaza un AAB con versionCode `<=` al ya publicado. |
| Keystore | Debe ser el mismo `ANDROID_KEYSTORE_*` de siempre. Con Play App Signing, la clave de subida se reusa para siempre. |
| Ficha e ícono | Ver la sección «Resubida tras rechazo por ícono»: la ficha se actualiza **antes** del build. |

Último tag conocido: `v0.3.0`.

---

## Paso 1 — Construir el AAB firmado

```bash
gh workflow run Android.yml --ref main -f target=aab -f bump=patch
```

`bump` acepta `patch` | `minor` | `major`. El tag se crea con formato `vX.Y.Z`.

Qué produce:

| Artefacto | Dónde |
|---|---|
| AAB firmado | GitHub Release `MatchBall X.Y.Z`, archivo `MatchBall-X.Y.Z.aab` |
| Tag | `vX.Y.Z` sobre el commit que disparó el run |
| Build crudo | Artifact del run, retención 1 día |

Si el tag ya existe pero no hay un draft release apuntando a ese mismo commit, el job **aborta a propósito** para no pisar un release anterior.

---

## Paso 2 — Descargar el AAB

```bash
gh release download vX.Y.Z --pattern "*.aab" --dir ./release
```

Verificá que el archivo exista y pese lo esperado (un AAB de este proyecto ronda las decenas de MB).

---

## Paso 3 — Subir a Play Console (manual)

1. Play Console → **MatchBallance** (`com.DarkVoid.MatchBallance`).
2. Elegí el track: **Internal testing** para validar rápido, **Production** para publicar.
3. *Create new release* → subir el `.aab`.
4. Release notes.
5. Revisar el **versionCode** que muestra Play: debe coincidir con el calculado.
6. Rollout. En Internal testing, "Review and rollout" es inmediato.

---

## Resubida tras rechazo por ícono

El orden importa: **ficha → internal → producción → apelación**. Invertir los primeros dos reabre el
mismo rechazo (el revisor compara lo que ve en la ficha con lo que ve instalado).

| # | Acción | Dónde | Criterio de salida |
|---|---|---|---|
| 1 | Leer el **versionCode máximo publicado** (Production + Internal + Closed) | Console → Releases | menor que el que va a emitir el build (`v0.3.1` → `3001`) |
| 2 | **Ficha primero**: ícono `store-icon-512.png` y título `MatchBallance` en la ficha **predeterminada y en todas las locales** (`es-419` incluida). El ícono de ficha es por locale: una por una | Presencia en la tienda → Ficha principal → selector de idioma | todas las locales con el mismo archivo y el mismo título |
| 3 | Disparar el build | `gh workflow run Android.yml --ref main -f target=aab -f bump=patch` | run verde, incluido `Verify launcher icons in AAB` |
| 4 | Descargar el AAB del Release | `gh release download vX.Y.Z --pattern "*.aab" --dir ./release` | archivo presente |
| 5 | Subir a **Internal testing** (nunca directo a producción) | Console → Testing → Internal | release aceptada |
| 6 | Instalar desde el link interno en un device/emulador **API ≥ 30** | device | ícono del launcher == `store-icon-512.png`, label `MatchBallance` |
| 7 | Capturas: (a) launcher con ícono y label, (b) preview de la ficha; **mismo device y mismo encuadre**, guardadas fuera del repo | — | dos imágenes comparables lado a lado |
| 8 | Promover el **mismo** AAB a Production, rollout 100% | Console → Production | versión en revisión |
| 9 | Apelación, **solo después de 8**, con las dos capturas y el link al Release | Console → App appeal | enviada |

Verificación del paso 6: el launcher cachea íconos viejos. Probá en un device/emulador donde
MatchBallance **nunca** se instaló; si reusás uno, desinstalá antes y volvé a instalar. Una captura
de un ícono cacheado no prueba nada y el revisor lo nota.

**Borrador de apelación** (en inglés: la revisión es global; 3 párrafos, sin capturas embebidas —
se adjuntan):

> The launcher icon now comes from a single artwork committed to our repository and applied by the
> build: green #22B14C with two filled circles (#ED1C24, #3F48CC), no text. The store listing icon
> and the on-device icon are the same composition, as the attached screenshots show side by side.
>
> Build vX.Y.Z (versionCode N) is live on the production track; the listing was updated in every
> locale before the upload.
>
> Root cause of the previous mismatch: the submitted build was compiled outside our CI pipeline with
> an icon that was never in the repository. That path is now blocked by a build-time check that fails
> the build if the icons do not match the committed artwork, and by a CI step that inspects the
> published AAB.

---

## Gate humano (checklist, no lo ejecuta el workflow)

Nada de esta lista es automático. Marcar a mano y archivar la evidencia:

- [ ] versionCode máximo publicado leído (Production + Internal + Closed) y **menor** que el del build.
- [ ] Ficha actualizada: `store-icon-512.png` + título `MatchBallance` en la ficha predeterminada y en **todas** las locales (`es-419` incluida).
- [ ] `gh workflow run Android.yml --ref main -f target=aab -f bump=patch` → run verde con el step `Verify launcher icons in AAB` (el AAB validado es el que se publica).
- [ ] AAB del Release subido a **Internal testing**; versionCode en Console == el del run.
- [ ] Instalado en device/emulador API ≥ 30 **sin instalación previa**: ícono del launcher == `store-icon-512.png`, label `MatchBallance`.
- [ ] Dos capturas (launcher + ficha) con el mismo device y encuadre, fuera del repo.
- [ ] AAB promovido a **Production** con rollout 100%.
- [ ] Apelación enviada con las dos capturas y el link al Release.

Si falla el paso 6, **no** se apela: se corrige el artwork o la asignación, se sube un versionCode
nuevo y se repite desde el paso 2. Un versionCode ya consumido no se reutiliza.

---

## Rollback

- **No se puede bajar el versionCode.** Revertir una release significa publicar una versión nueva con un número mayor.
- Si el AAB es rechazado, el problema casi siempre es versionCode repetido o keystore distinto.
- Un rollout al 100% se puede *halt* desde Play Console, pero no se puede "des-publicar" una versión ya instalada.

---

## Estados y qué NO hace este workflow

| Acción | ¿Automática? |
|---|---|
| Compilar AAB firmado | ✅ |
| Crear tag + GitHub Release | ✅ |
| Subir a Play Console | ❌ manual |
| Promover de internal a production | ❌ manual |
| Actualizar la ficha de la tienda | ❌ manual |

Si en algún momento se quiere automatizar la subida, el cambio es agregar un step con
`r0adkll/upload-google-play` y un service account JSON como secret. No está implementado.
