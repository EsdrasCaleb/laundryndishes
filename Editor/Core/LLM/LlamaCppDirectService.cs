using LLama;
using LLama.Common;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using LLama.Native;
using LLama.Sampling;

namespace LaundryNDishes.Core
{
    public class LlamaCppDirectService : ILLMService
    {
        private static bool _nativeLogConfigured = false;
        
        public async Task<LLMResponse> GetResponseAsync(LLMRequestData requestData, bool debug = false, CancellationToken cancellationToken = default)
        {
            var config = LnDConfig.instance;

            // 1. ATIVA LOGS NATIVOS DO C++ PARA VOCÊ VER A IA TRABALHANDO NOS BASTIDORES
            if (!_nativeLogConfigured && debug)
            {
                try 
                {
                    NativeLibraryConfig.All.WithLogCallback((level, message) => 
                    {
                        // Filtra apenas avisos importantes ou erros para não fludar o console
                        if (level == LLamaLogLevel.Error)
                            Debug.LogError($"[LLama C++ Native] {message}");
                        else if (message.Contains("load") || message.Contains("eval"))
                            Debug.Log($"<color=cyan>[LLama C++ Native] {message.Trim()}</color>");
                    });
                    _nativeLogConfigured = true;
                } 
                catch { /* Ignora se a versão não suportar callback */ }
            }

            try
            {
                if (debug) Debug.Log("<color=yellow>[LnD] Iniciando carregamento do modelo na memória RAM...</color>");

                // 2. EXECUTANDO EM BACKGROUND COM SUPORTE A CANCELAMENTO
                return await Task.Run<LLMResponse>(async () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var parameters = new ModelParams(config.GgufModelFile)
                    {
                        ContextSize = (uint)config.MaxTokens,
                        GpuLayerCount = 0 // CPU Pura
                    };

                    using var weights = LLamaWeights.LoadFromFile(parameters);
                    using var context = weights.CreateContext(parameters);
                    var executor = new InteractiveExecutor(context);

                    // ====================================================================
                    // TRADUÇÃO DO PROMPT (Sua Correção!)
                    // ====================================================================
                    var chatHistory = new ChatHistory();
                    var mensagens = requestData.GeneratedPrompt.Messages;

                    if (mensagens == null || mensagens.Count == 0)
                        throw new Exception("[LnD] O prompt gerado não possui mensagens.");

                    // Converte a string "system", "user" ou "assistant" para o Enum do LLamaSharp
                    AuthorRole ConverterRole(string role) => (role?.ToLower().Trim()) switch
                    {
                        "system" => AuthorRole.System,
                        "assistant" => AuthorRole.Assistant,
                        _ => AuthorRole.User
                    };

                    // 1. Adiciona TODAS as mensagens anteriores (ex: a sua mensagem de SYSTEM) no histórico
                    for (int i = 0; i < mensagens.Count - 1; i++)
                    {
                        chatHistory.AddMessage(ConverterRole(mensagens[i].role), mensagens[i].content);
                    }

                    // 2. Separa a ÚLTIMA mensagem (que será sempre a sua mensagem de USER) para ser o gatilho
                    var ultimaMsg = mensagens[mensagens.Count - 1];
                    var mensagemGatilho = new ChatHistory.Message(ConverterRole(ultimaMsg.role), ultimaMsg.content);

                    // Cria a sessão já com o SYSTEM gravado na memória do modelo
                    var session = new ChatSession(executor, chatHistory);
                    
                    var samplingParams = new DefaultSamplingPipeline { Temperature = config.Temperature };
                    var inferenceParams = new InferenceParams() { MaxTokens = config.MaxTokens, SamplingPipeline = samplingParams };

                    if (debug) Debug.Log("<color=yellow>[LnD] Lendo prompt de Sistema e Usuário na CPU...</color>");

                    var sb = new StringBuilder();
                    
                    // 3. Dispara a IA enviando a mensagem do USER
                    var responseStream = session.ChatAsync(mensagemGatilho, inferenceParams, cancellationToken);
                    
                    bool firstTokenReceived = false;
                    StringBuilder lineBuffer = new StringBuilder();

                    await foreach (var token in responseStream.WithCancellation(cancellationToken))
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (!firstTokenReceived)
                        {
                            firstTokenReceived = true;
                            //if (debug) Debug.Log("<color=green>⚡ [LnD] Primeiro token gerado! Escrevendo resposta...</color>");
                        }

                        sb.Append(token);

                        if (debug)
                        {
                            lineBuffer.Append(token);
                            if (token.Contains("\n"))
                            {
                                //Debug.Log($"<color=white>[LnD AI]: {lineBuffer.ToString().TrimEnd()}</color>");
                                lineBuffer.Clear();
                            }
                        }
                    }

                    if (debug && lineBuffer.Length > 0)
                    {
                        Debug.Log($"<color=white>[LnD AI]: {lineBuffer.ToString()}</color>");
                    }

                    return new LLMResponse { Success = true, Content = sb.ToString() };
                }, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Tratamento elegante para quando o usuário clica em Cancelar
                Debug.LogWarning("[LnD] A geração de testes foi cancelada pelo usuário. Memória liberada.");
                return new LLMResponse { Success = false, ErrorMessage = "Geração cancelada pelo usuário." };
            }
            catch (Exception ex)
            {
                if (ex.InnerException != null)
                {
                    Debug.LogError($"[LnD] Erro Nativo Detalhado: {ex.InnerException.Message}\n{ex.InnerException.StackTrace}");
                }
                Debug.LogError($"Erro na inferência local: {ex.Message}");
                return new LLMResponse { Success = false, ErrorMessage = ex.Message };
            }
        }
    }
}