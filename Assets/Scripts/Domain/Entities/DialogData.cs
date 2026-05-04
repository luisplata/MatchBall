using System;
using System.Collections.Generic;

namespace Domain.Entities
{
    [Serializable]
    public class DialogData
    {
        public int id;
        public string dialogId;
        public string heroId;
        public string title;

        public DialogMetadata metadata;

        public string created_at;
        public string updated_at;

        public List<QuestionData> questions;
    }
}