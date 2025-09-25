using System;
using LaundryNDishes.Data;

namespace LaundryNDishes.Services
{
    public static class LLMServiceFactory
    {
        // Criamos instâncias estáticas para que não precisem ser recriadas toda vez.
        // São leves e sem estado, então é seguro reutilizá-las.
        private static readonly OpenAIRestService OpenAiRestInstance = new OpenAIRestService();
        private static readonly LlamaCppDirectService LlamaDirectInstance = new LlamaCppDirectService();
        

        /// <summary>
        /// Carrega a configuração atual do EditorPrefs e retorna a instância de serviço apropriada.
        /// </summary>
        /// <returns>Uma implementação de ILLMService.</returns>
        public static ILLMService GetCurrentService()
        {
            switch (LnDConfig.Instance.ProviderType)
            {
                case LLMProviderType.OpenAIRestServer: return OpenAiRestInstance;
                case LLMProviderType.LlamaCppDirect: return LlamaDirectInstance;
                default: throw new ArgumentOutOfRangeException(nameof(LnDConfig.Instance.ProviderType));
            }
        }
    }
}