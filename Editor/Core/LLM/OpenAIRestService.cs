using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LaundryNDishes.Core;
using UnityEngine;

namespace LaundryNDishes.Core
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
        public async Task<LLMResponse> GetResponseAsync(LLMRequestData requestData, bool debug = false, CancellationToken cancellationToken = default)
        {
            var config = requestData.Config;
            try
            {
                // 0. Aborta imediatamente se o usuário já clicou em cancelar antes de começar
                cancellationToken.ThrowIfCancellationRequested();

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
                
                if (debug)
                {
                    Debug.Log($"Request: \n{jsonRequestBody}");
                }

                // 3. Configurar e enviar a requisição usando um HttpRequestMessage para mais controle.
                using (var requestMessage = new HttpRequestMessage(HttpMethod.Post, config.LlmServerUrl))
                {
                    requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.LlmApiKey);
                    requestMessage.Content = content;

                    // AQUI ESTÁ A MÁGICA: Passamos o token para o HttpClient abater a conexão de rede se cancelado!
                    HttpResponseMessage response = await HttpClient.SendAsync(requestMessage, cancellationToken);
                    
                    // Verifica novamente após a volta da rede
                    cancellationToken.ThrowIfCancellationRequested();

                    string responseData = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        if (debug)
                        {
                            Debug.Log($"Response: \n{responseData}");
                        }
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
                        Debug.LogError($"LLM request failed with status: {response.StatusCode}\nResponse: {responseData} " +
                                       $"\nRequest: {jsonRequestBody} \n Server:{config.LlmServerUrl}" +
                                       $"\nKey: {config.LlmApiKey}");
                        return new LLMResponse { Success = false, ErrorMessage = $"API Error: {response.ReasonPhrase}" };
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Tratamento limpo sem gerar erro vermelho no console da Unity
                Debug.LogWarning("[LnD] A requisição REST para o LLM foi cancelada pelo usuário. Conexão interrompida.");
                return new LLMResponse { Success = false, ErrorMessage = "Geração cancelada pelo usuário." };
            }
            catch (Exception ex)
            {
                Debug.LogError("Error in OpenAIRestService: " + ex.Message + " Server:" + config.LlmServerUrl);
                return new LLMResponse { Success = false, ErrorMessage = ex.Message };
            }
        }
    }
}
