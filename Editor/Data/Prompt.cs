using System.Collections.Generic;

namespace LaundryNDishes.Data
{
    [System.Serializable]
    public class ChatMessage
    {
        public string role;
        public string content;
    }

    // A classe principal que nosso PromptBuilder irá retornar.
    public class Prompt
    {
        public List<ChatMessage> Messages = new List<ChatMessage>();
    }
}