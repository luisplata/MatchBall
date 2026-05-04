# Proposal SDD — StartDialogByHero

Executive summary
-----------------
Validar y asegurar que `StartDialogByHero` (en `Assets/Scripts/Infra/WebAdapter/HeroApi.cs`) parsea correctamente el JSON que devuelve el backend y expone un `DialogData` consistente con el contrato. Actualmente hay discrepancias en DTOs (`questions`, `node_sequence`, `node`) y limitaciones por usar `JsonUtility`.

Scope
-----
- Ajustes a DTOs en `Assets/Scripts/Domain/Entities` (DialogData, DialogMetadata, DialogNode, DialogAnswer, QuestionData).
- Refactor mínimo en `StartDialogByHero` para parsing tolerante (wrapper `success/data`, arrays inesperados).
- Tests unitarios de parsing y tests de integración con mock HTTP.

Acceptance criteria (con ejemplos concretos)
-----------------------------------------
1. Dado el JSON de ejemplo (ver `tests.md`), `StartDialogByHero("hero-nova", "playerX")` retorna `DialogData` no nulo con `id == 11` y `dialogId == "dialog-nova-intro-v1"`.
2. `DialogData.metadata.startSequence == "seq-1"` y `metadata.responseContract` contiene `"currentSequence"`.
3. El nodo con `sequence == "question-1"` tiene `isQuestion == true` y `possibleAnswers[0].isCorrect == true` y `optionKey == "a"`.
4. El método tolera la envoltura `{"success":true,"data":{...}}` y sigue devolviendo `DialogData` válido.
5. Ante body vacío o malformado, `StartDialogByHero` retorna `null` y registra warnings/errors, sin lanzar excepciones no controladas.

Success metrics
---------------
- 100% de tests de parsing pasan.
- Integración mock: `StartDialogByHero` pasa escenario nominal y casos de error.
- No regressions en endpoints que usan `ApiResponse<T>`.

HTTP response contract (alto nivel)
---------------------------------
- Root object: { success: bool, message?: string, data: object }
- data: {
  id: number,
  dialogId: string,
  heroId: string,
  title: string,
  metadata: { nodes: [Node], version?: number, startSequence?: string, responseContract?: [string], completedSequence?: string },
  created_at?: string, updated_at?: string,
  questions?: [Question]
}

Node contract
- text: string
- emotion?: string
- sequence: string
- isQuestion: bool
- nextSequence?: string|null
- possibleAnswers?: [ { isCorrect?: bool, optionKey?: string, optionText?: string, nextSequence?: string|null } ]

Question contract
- id: number
- questionId: string
- dialogId: number
- question: string
- order_index: number
- node_sequence: string
- node: Node

Decision points
---------------
- Mantener `JsonUtility` + `JsonHelper` para arrays: menos dependencias, suficiente si los DTOs se alinean exactamente.
- Si aparecen casos más complejos (nombres dinámicos, nullables complejos), introducir `Newtonsoft.Json`.

Propuesta de next steps (prioridad)
1. Actualizar DTOs:
   - `QuestionData` agregar `node_sequence` y `node`.
   - Confirmar `DialogNode` y `DialogAnswer` campos exactos.
2. Añadir tests unitarios de parsing (JSON ejemplo + edge cases).
3. Ajustar `StartDialogByHero` para validar wrapper `success` y devolver `data`.
4. Code review y QA manual en Unity.

