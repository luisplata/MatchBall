# Release a Play Store — runbook

Estado real del pipeline (verificado en `Android.yml`, no en el comentario del YAML):
**el workflow NO sube nada a Play Store.** `target=aab` compila un AAB firmado y lo adjunta a un
GitHub Release junto con el tag. La subida a Play Console es manual.

---

## Antes de disparar

| Check | Cómo |
|---|---|
| Estás en `main` | El job `validate` aborta si `target` es `aab`/`all` y el ref no es `refs/heads/main`. |
| El build de itch pasó | Si `dev` (APK + WebGL) falló, el release no va a compilar mejor. |
| **versionCode** | Se calcula como `major*1e6 + minor*1e3 + patch`. `v0.2.11` → `2011`. Play rechaza un AAB con versionCode `<=` al ya publicado. |
| Keystore | Debe ser el mismo `ANDROID_KEYSTORE_*` de siempre. Con Play App Signing, la clave de subida se reusa para siempre. |

Último tag conocido: `v0.2.10`.

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
