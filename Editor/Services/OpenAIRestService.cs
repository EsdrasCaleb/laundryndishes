using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using LaundryNDishes.Data;
using UnityEngine;

namespace LaundryNDishes.Services
{
    // Modelos de dados para serialização/deserialização segura do JSON.
    [Serializable]
    internal class ChatRequest { public string model; public List<ChatMessage> messages; public float temperature; public int max_tokens; }
    
    [Serializable]
    internal class ChatResponse { public List<Choice> choices; }
    [Serializable]
    internal class Choice { public ChatMessage message; }
    
    // A classe agora implementa nossa interface.
    public class OpenAIRestService : ILLMService
    {
        // Reutilizamos a mesma instância de HttpClient para performance.
        private static readonly HttpClient HttpClient = new HttpClient();

        // O único método público, que segue o contrato da interface.
        public async Task<LLMResponse> GetResponseAsync(LLMRequestData requestData)
        {
            try
            {
                var config = requestData.Config;
                
                // 1. Construir o objeto da requisição de forma segura.
                var requestBody = new ChatRequest
                {
                    model = config.LlmModel,
                    messages = requestData.GeneratedPrompt.Messages, 
                    temperature = config.Temperature,
                    max_tokens = config.MaxTokens
                };

                // 2. Serializar para JSON usando a ferramenta da Unity.
                string jsonRequestBody = JsonUtility.ToJson(requestBody);
      
                var content = new StringContent(jsonRequestBody, Encoding.UTF8, "application/json");

                // 3. Configurar e enviar a requisição usando um HttpRequestMessage para mais controle.
                using (var requestMessage = new HttpRequestMessage(HttpMethod.Post, config.LlmServerUrl))
                {
                    // A chave da API é adicionada por requisição, o que é mais seguro.
                    requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.LlmApiKey);
                    requestMessage.Content = content;

                    HttpResponseMessage response = await HttpClient.SendAsync(requestMessage);
                    string responseData = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        // 4. Deserializar a resposta e extrair o conteúdo.
                        var chatResponse = JsonUtility.FromJson<ChatResponse>(responseData);
                        return new LLMResponse
                        {
                            Success = true,
                            Content = chatResponse?.choices?[0]?.message?.content?.Trim()
                        };
                    }
                    else
                    {
                        Debug.LogError($"LLM request failed with status: {response.StatusCode}\nResponse: {responseData}");
                        return new LLMResponse { Success = false, ErrorMessage = $"API Error: {response.ReasonPhrase}" };
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("Error in OpenAIRestService: " + ex.Message);
                return new LLMResponse { Success = false, ErrorMessage = ex.Message };
            }
        }
    }
}