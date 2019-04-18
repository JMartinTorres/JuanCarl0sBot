using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NLP_With_Dispatch_Bot
{
    public class ConversationFlowQuestion
    {
        public ConversationFlowQuestion(List<string> questions) => Items = questions;

        public List<string> Items { get; set; }
    }
}
