using LLama;
using LLama.Common;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using LLama.Native;

namespace LaundryNDishes.Core
{
    public class LlamaCppDirectService : ILLMService
    {
        private static bool _hasLoadedNativeLibrary = false;
        private static string _initialPathUsed = "";
        
        public async Task<LLMResponse> GetResponseAsync(LLMRequestData requestData, bool debug = false)
        {
            try
            {
                // Joga a carga pesada para uma thread em background (salva o Editor de travar)
                return await Task.Run(async () =>
                {
                    var config = LnDConfig.instance;
                    if (!string.IsNullOrEmpty(config.LlamaCppPath))
                    {
                        if (_hasLoadedNativeLibrary && config.LlamaCppPath != _initialPathUsed)
                        {
                            Debug.LogWarning("LnD: Você alterou o caminho do Llama.cpp, mas a IA já foi carregada na memória nesta sessão. Por favor, reinicie a Unity para o novo caminho (GPU) fazer efeito.");
                        }
                        else
                        {
                            string targetDirectory = null;

                            // Descobre se o usuário passou o caminho de uma PASTA ou de um ARQUIVO
                            if (Directory.Exists(config.LlamaCppPath))
                            {
                                targetDirectory = config.LlamaCppPath;
                            }
                            else if (File.Exists(config.LlamaCppPath))
                            {
                                targetDirectory = Path.GetDirectoryName(config.LlamaCppPath);
                            }

                            // Se o diretório for válido, injeta no PATH do sistema
                            if (!string.IsNullOrEmpty(targetDirectory))
                            {
                                string currentPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process);
                                
                                // Só injeta se já não estiver lá
                                if (currentPath != null && !currentPath.Contains(targetDirectory))
                                {
                                    // Path.PathSeparator usa ';' no Windows e ':' no Mac/Linux automaticamente
                                    string newPath = targetDirectory + Path.PathSeparator + currentPath;
                                    Environment.SetEnvironmentVariable("PATH", newPath, EnvironmentVariableTarget.Process);
                                }
                            }
                            else
                            {
                                Debug.LogWarning("O caminho customizado do llama.cpp não foi encontrado. Tentando usar o nativo embutido na Unity...");
                            }
        
                            _hasLoadedNativeLibrary = true;
                            _initialPathUsed = config.LlamaCppPath;
                        }
                    }
                    var parameters = new ModelParams(config.GgufModelFile)
                    {
                        ContextSize = (uint)config.MaxTokens,
                        GpuLayerCount = 20 // Ajuste conforme VRAM
                    };

                    // O LLamaSharp aqui vai magicamente encontrar a DLL nativa que a Unity carregou
                    using var weights = LLamaWeights.LoadFromFile(parameters);
                    using var context = weights.CreateContext(parameters);
                    var executor = new InteractiveExecutor(context);

                    var chatHistory = new ChatHistory();
                    var message = new ChatHistory.Message(AuthorRole.User, requestData.GeneratedPrompt.ToString());

                    var session = new ChatSession(executor, chatHistory);
                    var inferenceParams = new InferenceParams()
                    {
                        MaxTokens = config.MaxTokens,
                        Temperature = 0.7f
                    };

                    var sb = new StringBuilder();
                    var responseStream = session.ChatAsync(message, inferenceParams);
                    
                    await foreach (var token in responseStream)
                    {
                        sb.Append(token);
                    }

                    return new LLMResponse { Success = true, Content = sb.ToString() };
                });
            }
            catch (Exception ex)
            {
                Debug.LogError($"Erro na inferência local: {ex.Message}");
                return new LLMResponse { Success = false, ErrorMessage = ex.Message };
            }
        }
    }
}