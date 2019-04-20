using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NLP_With_Dispatch_Bot
{
    public class ConversationFlowDialog
    {

        public ConversationFlowDialog()
        {
            Questions = new List<ConversationFlowQuestion>();
            Answers = new List<string>();
        }

        public List<ConversationFlowQuestion> Questions { get; set; }
        public List<string> Answers { get; set; }
        public bool InfoInQnA { get; set; }
        public string BotAnswer { get; set; }

    }
}
