using System;
using System.Collections.Generic;

namespace LaundryNDishes.Data
{
    [Serializable]
    public class Prompt
    {
        public List<ChatMessage> Messages = new List<ChatMessage>();
    }
}
