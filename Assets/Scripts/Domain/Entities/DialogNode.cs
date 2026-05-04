using System;
using System.Collections.Generic;

namespace Domain.Entities
{
    [Serializable]
    public class DialogNode
    {
        public string text;
        public string emotion;

        public string sequence;
        public bool isQuestion;

        public string nextSequence;

        public List<DialogAnswer> possibleAnswers;
    }
}