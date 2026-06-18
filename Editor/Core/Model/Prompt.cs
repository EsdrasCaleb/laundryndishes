using System;
using System.Collections.Generic;

namespace LaundryNDishes.Core
{
    [Serializable]
    public class Prompt
    {
        public List<ChatMessage> Messages = new List<ChatMessage>();
    }
}
