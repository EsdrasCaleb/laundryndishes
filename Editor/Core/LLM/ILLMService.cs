using System.Threading.Tasks;
using LaundryNDishes.Core;

namespace LaundryNDishes.Core
{
// O cotrato que todos os nossos "provedores" de LLM devem seguir.
    public interface ILLMService
    {
        Task<LLMResponse> GetResponseAsync(LLMRequestData requestData, bool debug=false);
    }
}