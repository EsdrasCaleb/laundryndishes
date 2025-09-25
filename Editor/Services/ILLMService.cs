using System.Threading.Tasks;
using LaundryNDishes.Data;

namespace LaundryNDishes
{
// Um objeto para passar os parâmetros da requisição de forma estruturada.
    public class LLMRequestData
    {
        public Prompt GeneratedPrompt; 
        public Data.LnDConfig Config; // Passa toda a configuração necessária.
    }

// Um objeto para receber a resposta de forma estruturada.
    public class LLMResponse
    {
        public bool Success;
        public string Content;
        public string ErrorMessage;
    }

// O contrato que todos os nossos "provedores" de LLM devem seguir.
    public interface ILLMService
    {
        Task<LLMResponse> GetResponseAsync(LLMRequestData requestData);
    }
}