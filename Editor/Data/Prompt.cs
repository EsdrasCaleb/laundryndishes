using System.Collections.Generic;

namespace LaundryNDishes.Data
{
    // A classe principal que nosso PromptBuilder irá retornar.
    public class Prompt
    {
        public List<ChatMessage> Messages = new List<ChatMessage>();
    }
}