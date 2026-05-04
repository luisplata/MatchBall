# DTOs recomendados (ejemplos C#)

Estos ejemplos están pensados para usarse con `UnityEngine.JsonUtility`. Los nombres de campos deben coincidir exactamente con los keys del JSON (camelCase).

Ejemplo: DialogData y dependencias

```csharp
[Serializable]
public class DialogResponse {
    public bool success;
    public DialogData data;
}

[Serializable]
public class DialogData {
    public int id;
    public string dialogId;
    public string heroId;
    public string title;
    public DialogMetadata metadata;
    public string created_at;
    public string updated_at;
    public List<QuestionData> questions;
}

[Serializable]
public class DialogMetadata {
    public List<DialogNode> nodes;
    public int version;
    public string startSequence;
    public List<string> responseContract;
    public string completedSequence;
}

[Serializable]
public class DialogNode {
    public string text;
    public string emotion;
    public string sequence;
    public bool isQuestion;
    public string nextSequence;
    public List<DialogAnswer> possibleAnswers;
}

[Serializable]
public class DialogAnswer {
    public bool isCorrect;
    public string optionKey;
    public string optionText;
    public string nextSequence;
}

[Serializable]
public class QuestionData {
    public int id;
    public string questionId;
    public int dialogId;
    public string question;
    public int order_index;
    public string node_sequence; // <<-- importante: agregar
    public DialogNode node;      // <<-- importante: agregar
}
```

Notas:
- `JsonUtility` ignora campos que no existen en la clase; por eso es importante que los campos que necesitamos estén presentes y nombrados exactamente.
- Para arrays top-level usar `JsonHelper.FromJson<T>` (utilitario existente en el proyecto).

Mapping/Mapper
- Si hay una capa Mapper (p.ej. `Infra.WebAdapter.Mapper`), aprovecharla para convertir DTO -> Domain (p. ej. `DialogNode` -> `DialogNodeDomain`). Mantener esa separación.

