using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NLP_With_Dispatch_Bot
{
    public class ConversationFlowQuestion
    {

        public string Question { get; set; }
        public List<string> Choices { get; set; }
        public bool SaveAnswer { get; set; }

        public ConversationFlowQuestion() { }


        public ConversationFlowQuestion(string question, List<string> choices)
        {
            Question = question;
            Choices = choices;
        }
    }
}
