namespace LaundryNDishes
{
    using System.Threading.Tasks;
    using UnityEngine;

    public class LlamaCppDirectService : ILLMService
    {
        public Task<LLMResponse> GetResponseAsync(LLMRequestData requestData)
        {
            // TODO: Implementar a chamada direta à biblioteca do llama.cpp via C++ Interop (P/Invoke).
            // Esta é uma tarefa complexa que envolve criar "bindings" para a biblioteca C++.
            Debug.LogWarning("Chamada direta ao Llama.cpp ainda não implementada!");

            var mockResponse = new LLMResponse
            {
                Success = false,
                ErrorMessage = "Direct Llama.cpp call is not yet implemented."
            };

            return Task.FromResult(mockResponse); // Retorna uma tarefa já completada.
        }
    }
}