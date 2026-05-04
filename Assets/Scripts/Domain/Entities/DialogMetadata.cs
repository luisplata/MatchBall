using System;
using System.Collections.Generic;

namespace Domain.Entities
{
    [Serializable]
    public class DialogMetadata
    {
        public List<DialogNode> nodes;

        public int version;
        public string startSequence;
        public List<string> responseContract;
        public string completedSequence;
    }
}