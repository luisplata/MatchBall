# Tests y JSON de ejemplo

Objetivo
--------
Definir casos de prueba (unit + integration) y proveer el JSON maestro para reproducir y validar parsing.

Casos unitarios (parsing)
-------------------------
1. Caso nominal (JSON exacto del backend) -> `DialogResponse` parsea y `DialogData` tiene los valores esperados (id, dialogId, metadata.startSequence, nodo pregunta con possibleAnswers). 
2. Wrapper absent: backend retorna directamente `data` (sin `success` wrapper). Verificar que el helper detecta y parsea.
3. Array top-level inesperado (no esperado para este endpoint) -> debe fallar con `null` o devolver lista vacía según contrato (definir). Para `StartDialogByHero` esperamos un objeto, así que caso malformed -> `null`.
4. Campos faltantes (p.ej. `possibleAnswers` ausente) -> parsing no debe lanzar excepción y las listas deben quedar vacías/null manejadas.
5. Malformed JSON -> `StartDialogByHero` retorna `null` y registra error.

Tests de integración (HeroApi)
----------------------------
1. Mock HTTP 200 con JSON ejemplo -> `StartDialogByHero` devuelve `DialogData` esperado.
2. Mock HTTP 200 con `{"success":false,...}` -> `StartDialogByHero` devuelve `null` y registra warning.
3. Mock HTTP 500 -> transporte manejado por retry; al final `null`.

Ejemplo JSON (caso nominal — pegar tal cual en un test)
-----------------------------------------------------
```json
{"success":true,"data":{"id":11,"dialogId":"dialog-nova-intro-v1","heroId":"hero-nova","title":"Nova Intro Conversation","metadata":{"nodes":[{"text":"Nova te saluda y te explica que esta conversacion define tu progreso con el heroe.","emotion":"neutral","sequence":"seq-1","isQuestion":false,"nextSequence":"seq-2","possibleAnswers":[]},{"text":"Nova te da contexto y te prepara para una pregunta de decision.","emotion":"confianza","sequence":"seq-2","isQuestion":false,"nextSequence":"question-1","possibleAnswers":[]},{"text":"Si ves a un aliado en riesgo, tu prioridad es protegerlo antes que buscar puntos?","emotion":"sorpresa","sequence":"question-1","isQuestion":true,"nextSequence":null,"possibleAnswers":[{"isCorrect":true,"optionKey":"a","optionText":"Si, priorizo al aliado.","nextSequence":"seq-3"},{"isCorrect":false,"optionKey":"b","optionText":"No, primero busco ventaja individual.","nextSequence":"seq-4"}]},{"text":"Buena decision: el trabajo en equipo acelera tu progreso con Nova.","emotion":"confianza","sequence":"seq-3","isQuestion":false,"nextSequence":"seq-5","possibleAnswers":[]},{"text":"Nova te corrige: sin equipo no hay consistencia, pero podes recuperarte.","emotion":"enojo","sequence":"seq-4","isQuestion":false,"nextSequence":"seq-5","possibleAnswers":[]},{"text":"Ahora consolidamos lo aprendido para cerrar la conversacion.","emotion":"neutral","sequence":"seq-5","isQuestion":false,"nextSequence":"seq-6","possibleAnswers":[]},{"text":"Conversacion completada: Nova registra tu resultado final.","emotion":"determinacion","sequence":"seq-6","isQuestion":false,"nextSequence":null,"possibleAnswers":[]}],"version":1,"startSequence":"seq-1","responseContract":["currentSequence","nextSequence","completed","pointsAwarded"],"completedSequence":"seq-6"},"created_at":"2026-05-03T23:05:16.000Z","updated_at":"2026-05-03T23:05:16.000Z","questions":[{"id":11,"questionId":"question-nova-1","dialogId":11,"question":"Si ves a un aliado en riesgo, tu prioridad es protegerlo antes que buscar puntos?","order_index":1,"node_sequence":"question-1","node":{"sequence":"question-1","emotion":"sorpresa","text":"Si ves a un aliado en riesgo, tu prioridad es protegerlo antes que buscar puntos?","possibleAnswers":[{"optionKey":"a","optionText":"Si, priorizo al aliado.","nextSequence":"seq-3"},{"optionKey":"b","optionText":"No, primero busco ventaja individual.","nextSequence":"seq-4"}]}}]}}
```

Ejemplos de asserts (unit tests)
--------------------------------
- assert.NotNull(response);
- assert.AreEqual(11, response.data.id);
- assert.AreEqual("dialog-nova-intro-v1", response.data.dialogId);
- assert.AreEqual("seq-1", response.data.metadata.startSequence);
- var questionNode = response.data.metadata.nodes.Find(n => n.sequence == "question-1");
- assert.IsTrue(questionNode.isQuestion);
- assert.AreEqual("a", questionNode.possibleAnswers[0].optionKey);

Dónde guardar tests
-------------------
- Unit: `Assets/Tests/Unit/Parser/DialogParsingTests.cs`
- Integration: `Assets/Tests/Integration/HeroApi/StartDialogByHeroTests.cs`

