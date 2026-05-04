using System;

namespace Domain.Entities
{
    [Serializable]
    public class DialogAnswer
    {
        public bool isCorrect;
        public string optionKey;
        public string optionText;
        public string nextSequence;
    }
}