# Spec: StartDialogByHero

Propósito
-------
Repositorio del SDD/Spec para validar y alinear el servicio `StartDialogByHero` (implementado en `Assets/Scripts/Infra/WebAdapter/HeroApi.cs`) con el contrato JSON que provee el backend.

Contenido
--------
- `proposal.md` : propuesta SDD y criterios de aceptación.
- `dtos.md` : ejemplos de DTOs C# recomendados para serialización/deserialización.
- `tests.md` : casos de prueba (unit + integration) y JSONs de ejemplo.

Cómo usar
--------
1. Revisá `proposal.md` para entender el scope y criterios de aceptación.
2. Implementá cambios en `Assets/Scripts/Domain/Entities/*` y `HeroApi.cs` según `dtos.md`.
3. Agregá tests descritos en `tests.md` en `Assets/Tests` y corré el pipeline de Unity/Editor.

Notas
----
No se guardó en engram (no tuve acceso). Este folder reemplaza ese tracking localmente para que puedas versionarlo en git.

