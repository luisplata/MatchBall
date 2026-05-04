using System;

namespace Domain.Entities
{
    [Serializable]
    public class QuestionData
    {
        public int id;
        public string questionId;
        public int dialogId;

        public string question;
        public int order_index;
        
        // Alineado con el contrato backend: referencia a la secuencia/nodo correspondiente
        public string node_sequence;
        public DialogNode node;
    }
}