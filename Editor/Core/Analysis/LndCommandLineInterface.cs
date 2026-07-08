using UnityEditor;
using UnityEngine;
using System;
using System.Threading.Tasks;
using System.IO;
using System.Text;
using UnityEditor.Compilation;


namespace LaundryNDishes.Core
{
    public static class LndCommandLineInterface
    {

        public static void GenerateTest()
        {
            try
            {
                Debug.Log("[LnD CLI] Iniciando processo via linha de comando...");

                // 1. Lemos os argumentos que você passou no terminal
                string targetScriptPath = GetArgValue("-script");
                string extra = GetArgValue("-method");
                string promptTypeStr = GetArgValue("-type");

                // Validação básica
                if (string.IsNullOrEmpty(targetScriptPath) || string.IsNullOrEmpty(extra))
                {
                    Debug.LogError("[LnD CLI] ERRO: Faltam argumentos! Certifique-se de usar: -script <caminho> -method <nome_do_metodo>");
                    EditorApplication.Exit(1); // Exit 1 indica erro para o terminal
                    return;
                }

                // 2. Carrega o script da Unity usando o caminho passado (ex: Assets/Scripts/Player.cs)
                MonoScript targetScript = AssetDatabase.LoadAssetAtPath<MonoScript>(targetScriptPath);
                if (targetScript == null)
                {
                    Debug.LogError($"[LnD CLI] ERRO: Não foi possível encontrar o script em: '{targetScriptPath}'");
                    EditorApplication.Exit(1);
                    return;
                }

                // 3. Define o PromptType (Padrão: Uniti)
                TestType testType = TestType.Uniti;
                if (!string.IsNullOrEmpty(promptTypeStr) && Enum.TryParse(promptTypeStr, true, out TestType parsedType))
                {
                    testType = parsedType;
                }

                // 4. Inicializa o gerador
                var config = LnDConfig.instance;
                var llmService = config.GetCurrentService();
                var generator = new UnitTestGenerator(llmService, config);

                // ... (código anterior igual até a linha do generator.Generate)

                Debug.Log($"[LnD CLI] Gerando teste para o script: {targetScript.name} | Método: {extra} | Tipo: {testType}");

                // INICIA a tarefa, mas NÃO usa GetAwaiter().GetResult()
                var genTask = generator.Generate(targetScript, extra, testType);

                // Inscreve um "checador" no loop do Unity Editor
                EditorApplication.update += () =>
                {
                    // Fica checando a cada frame se a tarefa já terminou
                    if (genTask.IsCompleted)
                    {
                        if (genTask.IsFaulted)
                        {
                            Debug.LogError($"[LnD CLI] ERRO FATAL: {genTask.Exception?.GetBaseException().Message}");
                            EditorApplication.Exit(1); // Sai com erro
                        }
                        else
                        {
                            Debug.Log("[LnD CLI] Processo finalizado com sucesso!");
                            EditorApplication.Exit(0); // Sai com sucesso
                        }
                    }
                };
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LnD CLI] ERRO FATAL NA EXECUÇÃO: {ex.Message}\n{ex.StackTrace}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Entry point da CLI para gerar testes para uma pasta inteira.
        /// Comando: -executeMethod LaundryNDishes.CLI.LndCommandLineInterface.GenerateTestsFolder -folder "Assets/Scripts/MinhaPasta"
        /// </summary>
        public static void GenerateTestsFolder()
        {
            try
            {
                Debug.Log("[LnD CLI] Iniciando geração em lote por pasta...");

                string folderPath = GetArgValue("-folder");
                string csvPath = GetArgValue("-csv");


                if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
                {
                    Debug.LogError("[LnD CLI] ERRO: Pasta inválida ou não fornecida. Use: -folder <caminho_da_pasta>");
                    EditorApplication.Exit(1);
                    return;
                }

                // Iniciamos o processo principal em background
                var batchTask = RunBatchGenerationAsync(folderPath, csvPath);

                // O mesmo truque do update para manter a Unity ativa
                EditorApplication.update += () =>
                {
                    if (batchTask.IsCompleted)
                    {
                        if (batchTask.IsFaulted)
                        {
                            Debug.LogError($"[LnD CLI] ERRO FATAL NO LOTE: {batchTask.Exception?.GetBaseException().Message}");
                            EditorApplication.Exit(1);
                        }
                        else
                        {
                            Debug.Log("[LnD CLI] Geração em lote finalizada com sucesso!");
                            EditorApplication.Exit(0);
                        }
                    }
                };
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LnD CLI] ERRO FATAL: {ex.Message}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Lógica assíncrona que varre a pasta e aciona a IA.
        /// </summary>
        private static async Task RunBatchGenerationAsync(string folderPath, string csvPath = null)
        {
            var config = LnDConfig.instance;
            var llmService = config.GetCurrentService();
            var generator = new UnitTestGenerator(llmService, config);

            if (!string.IsNullOrEmpty(csvPath) && !File.Exists(csvPath))
            {
                File.WriteAllText(csvPath, "SUTClass,SUTMethod,TestType,TestFile,NumberOfCorrections,Attempts,TimeToGenerate(ms),Status\n");
            }
            // BLOQUEIA A RECOMPILAÇÃO: Impede a Unity de travar durante o loop ao criar novos arquivos
            EditorApplication.LockReloadAssemblies();

            try
            {
                // Busca todos os scripts dentro da pasta informada
                string[] guids = AssetDatabase.FindAssets("t:MonoScript", new[] { folderPath });

                int totalScripts = guids.Length;
                int currentScriptIndex = 0;

                foreach (string guid in guids)
                {
                    currentScriptIndex++;

                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);
                    if (script == null) continue;

                    Type scriptType = script.GetClass();
                    if (scriptType == null) continue; // Pode ser uma interface ou enum
                    bool isTestScript = false;

                    
                    // MODO PRO: Filtragem por nome de Assembly (.asmdef)
                    string scriptAssemblyFile = CompilationPipeline.GetAssemblyNameFromScriptPath(assetPath);
                    bool isPlayModeTest = config.PlayModeTestAssembly != null && scriptAssemblyFile == config.PlayModeTestAssembly.name + ".dll";
                    bool isEditModeTest = config.EditorTestAssembly != null && scriptAssemblyFile == config.EditorTestAssembly.name + ".dll";

                    if (isPlayModeTest || isEditModeTest)
                    {
                        isTestScript = true;
                        Debug.Log($"[LnD CLI] Pulando {script.name} - É um script de teste (Verificação por Assembly).");
                    }

                    // Bônus de Segurança antigo (pelo caminho das pastas dos asmdefs)
                    if (!isTestScript)
                    {
                        if (config.PlayModeTestAssembly != null)
                        {
                            string playModePath = AssetDatabase.GetAssetPath(config.PlayModeTestAssembly);
                            if (!string.IsNullOrEmpty(playModePath))
                            {
                                string playModeDir = Path.GetDirectoryName(playModePath).Replace("\\", "/");
                                if (assetPath.StartsWith(playModeDir)) isTestScript = true;
                            }
                        }

                        if (config.EditorTestAssembly != null && !isTestScript)
                        {
                            string editorPath = AssetDatabase.GetAssetPath(config.EditorTestAssembly);
                            if (!string.IsNullOrEmpty(editorPath))
                            {
                                string editorDir = Path.GetDirectoryName(editorPath).Replace("\\", "/");
                                if (assetPath.StartsWith(editorDir)) isTestScript = true;
                            }
                        }
                        
                        if (isTestScript)
                        {
                            Debug.Log($"[LnD CLI] Pulando {script.name} - Script está dentro do diretório do Assembly de testes.");
                        }
                    }
                    
                   

                    // Se a checagem (seja por Asmdef ou Pasta) acusou que é um teste, pula o script
                    if (isTestScript) continue;

                    bool isMonoBehaviour = typeof(MonoBehaviour).IsAssignableFrom(scriptType);

                    // Usa a classe utilitária que criamos antes para extrair os métodos!
                    var (unitMethods, behaviorMethods) = ScriptMethodAnalyzer.CategorizeMethods(scriptType);

                    float progressPercentage = ((float)currentScriptIndex / totalScripts) * 100f;
                    int filledBars = (int)(progressPercentage / 5f); // 20 barrinhas no total (100 / 5)
                    string progressBar = new string('#', filledBars).PadRight(20, '-');

                    Debug.Log($"\n[LnD CLI] PROGRESSO: [{progressBar}] {progressPercentage:F1}% ({currentScriptIndex}/{totalScripts})");
                    Debug.Log($"[LnD CLI] Analisando Script: {script.name} | É MonoBehaviour? {isMonoBehaviour}");

                    if (isMonoBehaviour)
                    {
                        // 1. Gera os testes do ciclo de vida da Unity (Behavior)
                        foreach (var method in behaviorMethods)
                        {
                            Debug.Log($"   -> [Behavior] Gerando teste para: {method}");
                            await generator.Generate(script, method, TestType.Behavior, csvPath);
                        }

                        // 2. Gera os testes de lógica pura do MonoBehaviour (Uniti)
                        foreach (var method in unitMethods)
                        {
                            Debug.Log($"   -> [Uniti] Gerando teste para: {method}");
                            await generator.Generate(script, method, TestType.Uniti, csvPath);
                        }
                    }
                    else
                    {
                        // 3. É um C# puro: Gera os testes de EditMode (Unitieditor)
                        foreach (var method in unitMethods)
                        {
                            Debug.Log($"   -> [Unitieditor] Gerando teste para: {method}");
                            await generator.Generate(script, method, TestType.Unitieditor, csvPath);
                        }
                    }
                }
            }
            finally
            {
                // LIBERA A RECOMPILAÇÃO: Agora que tudo acabou, a Unity pode processar os novos .cs
                EditorApplication.UnlockReloadAssemblies();
                AssetDatabase.Refresh();
                Debug.Log("[LnD CLI] Todos os testes gerados. Recompilando banco de assets...");
            }
        }

        // Helper para ler argumentos do terminal
        private static string GetArgValue(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == name && i + 1 < args.Length)
                {
                    return args[i + 1];
                }
            }
            return null;
        }


        //Call to get generated Test List
        public static void ExportTestReport()
        {
            try
            {
                string csvPath = GetArgValue("-csv");
                if (string.IsNullOrEmpty(csvPath))
                {
                    Debug.LogError("[LnD CLI] ERRO: Caminho do CSV não fornecido.");
                    EditorApplication.Exit(1);
                    return;
                }

                var db = TestDatabase.Instance;

                var sb = new StringBuilder();

                // MODIFICAÇÃO: Adicionada a coluna "Status" no cabeçalho do CSV
                sb.AppendLine("File,Path,Type,ClassName,TestName,Status");

                foreach (var t in db.AllTests)
                {
                    if (t.GeneratedTestScript == null) continue;

                    string fileName = t.GeneratedTestScript.name;
                    string assetPath = AssetDatabase.GetAssetPath(t.GeneratedTestScript);

                    // MODIFICAÇÃO: Deixa o nome do tipo legível igual ao Drawer
                    string testType = t.type.ToString() switch
                    {
                        "Uniti" => "Unitary (PlayMode)",
                        "Unitieditor" => "Unitary (EditMode)",
                        "Behavior" => "Unitary In LifeCicle (PlayMode)",
                        "Integration" => "Integration (PlayMode)",
                        "Scriptable" => "Unitary In Scriptable (PlayMode)",
                        "Prefab" => "Unitary In Prefab (PlayMode)",
                        "Scene" => "Unitary In Scene (PlayMode)",
                        _ => t.type.ToString()
                    };

                    Type scriptType = t.GeneratedTestScript.GetClass();
                    if (scriptType == null) continue;

                    string className = scriptType.FullName ?? scriptType.Name;

                    var methods = scriptType.GetMethods(System.Reflection.BindingFlags.Public |
                                                        System.Reflection.BindingFlags.Instance |
                                                        System.Reflection.BindingFlags.Static);

                    foreach (var method in methods)
                    {
                        if (method.DeclaringType != scriptType) continue;
                        if (method.IsSpecialName) continue;

                        // MODIFICAÇÃO: Filtra para exportar APENAS métodos que são testes reais
                        bool isTest = System.Attribute.IsDefined(method, typeof(NUnit.Framework.TestAttribute)) ||
                                    System.Attribute.IsDefined(method, typeof(UnityEngine.TestTools.UnityTestAttribute));
                        if (!isTest) continue;

                        string testName = method.Name;

                        // =================================================================================
                        // ADIÇÃO: BUSCA E MAPEA O STATUS DO TESTE INDIVIDUAL
                        // =================================================================================
                        var existingTest = t.IndividualTests.Find(it => it.MethodName == testName);
                        string statusText = "PENDING"; // Fallback se o teste nunca foi executado

                        if (existingTest != null)
                        {
                            statusText = existingTest.Status switch
                            {
                                SingleTestStatus.Passed => "PASSED",
                                SingleTestStatus.Failed => "FAILED",
                                SingleTestStatus.Inconclusive => "INCONCLUSIVE",
                                SingleTestStatus.Skipped => "SKIPPED",
                                _ => "PENDING"
                            };
                        }
                        // =================================================================================

                        // MODIFICAÇÃO: Inclui o statusText no final da linha do CSV
                        sb.AppendLine($"{fileName},{assetPath},{testType},{className},{testName},{statusText}");
                    }
                }

                string directory = Path.GetDirectoryName(csvPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(csvPath, sb.ToString(), Encoding.UTF8);
                Debug.Log($"[LnD CLI] Manifesto de testes com telemetria de Status gerado em: {csvPath}");

                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LnD CLI] ERRO FATAL AO GERAR MANIFESTO: {ex.Message}");
                EditorApplication.Exit(1);
            }
        }

        // <summary>
        /// Instala o backend Llama.cpp especificado via argumento no terminal.
        /// 
        /// COMO USAR NO TERMINAL:
        /// Passo 1: Passe a flag '-backend' seguida por um dos valores abaixo (case-insensitive):
        /// 
        /// Backends Possíveis:
        ///   CPU        -> Para CPUs muito antigas (pré-2011) sem instruções avançadas.
        ///   CPU_AVX    -> Para CPUs antigas (2011-2013, ex: Intel Sandy Bridge).
        ///   CPU_AVX2   -> Padrão recomendado para 99% das CPUs modernas (AMD Ryzen / Intel moderno).
        ///   CPU_AVX512 -> Apenas para AMD Zen 4+ (Ryzen 7000+) ou Intel Xeon/Ice Lake.
        ///   Vulkan     -> Para GPUs AMD Radeon (Dedicas/APUs fortes) ou Intel Arc.
        ///   CUDA11     -> Para placas NVIDIA mais antigas (Série GTX 600 a GTX 800).
        ///   CUDA12     -> Para placas NVIDIA modernas (GTX 900, Série 10, RTX 20/30/40).
        /// 
        /// EXEMPLO DE COMANDO (Linux/macOS):
        /// ./Unity -batchmode -nographics -projectPath "/caminho/do/projeto" -executeMethod LaundryNDishes.Core.LndCommandLineInterface.InstallBackendCLI -backend Vulkan -quit
        /// 
        /// EXEMPLO DE COMANDO (Windows):
        /// Unity.exe -batchmode -nographics -projectPath "C:/Projeto" -executeMethod LaundryNDishes.Core.LndCommandLineInterface.InstallBackendCLI -backend CUDA12 -quit
        /// </summary>
        public static void InstallBackendCLI()
        {
            Debug.Log("[LnD CLI] Iniciando rotina de instalação de backend via linha de comando...");
            LlamaCppHardwareBackend targetBackend;
            try
            {
                // 1. Lê o argumento '-backend' passado pelo usuário na linha de comando
                string backendArg = GetArgValue("-backend");

                if (string.IsNullOrEmpty(backendArg))
                {
                    LnDConfig.instance.UpdateBestBackend();
                    targetBackend = LnDConfig.instance.DetectedHardware;
                }
                else
                {

                    // 2. Converte a string do terminal para o Enum LlamaCppHardwareBackend ignorando maiúsculas/minúsculas
                    if (!Enum.TryParse(backendArg, true, out targetBackend))
                    {
                        Debug.LogError(
                            $"[LnD CLI] ERRO: O valor '{backendArg}' não é um backend válido! Valores aceitos: CPU, CPU_AVX, CPU_AVX2, CPU_AVX512, Vulkan, CUDA11, CUDA12.");
                        if (Application.isBatchMode) EditorApplication.Exit(1);
                        return;
                    }
                }

                Debug.Log($"[LnD CLI] Backend selecionado via argumento: {targetBackend}");

                // 3. Acessa o Config do projeto
                LnDConfig config = LnDConfig.instance;

                // 4. Verifica se já está instalado para economizar tempo/banda no CI/CD
                if (config.ActiveHardwareBackend == targetBackend)
                {
                    Debug.Log($"[LnD CLI] O backend '{targetBackend}' já está configurado e atualizado neste projeto. Nenhuma ação necessária.");
                    if (Application.isBatchMode) EditorApplication.Exit(0);
                    return;
                }

                Debug.Log($"[LnD CLI] Iniciando o download e bootstrap do backend '{targetBackend}'...");

                // 5. Executa o download de forma assíncrona com bloqueio síncrono da thread para o modo -batchmode não fechar
                LlamaCppBackendDownloader downloader = new LlamaCppBackendDownloader();
                downloader.StartSession();

                Task downloadTask = Task.Run(async () => 
                {
                    await downloader.InstallBackendAsync(targetBackend);
                });

                // Trava a execução do terminal até o download ser finalizado
                downloadTask.Wait();

                if (downloader.Progress >= 1.0f)
                {
                    // 6. Atualiza o projeto com o novo backend e salva o .asset
                    config.ActiveHardwareBackend = targetBackend;
                    config.Save();
                    
                    Debug.Log($"[LnD CLI] SUCESSO: Backend '{targetBackend}' foi baixado, instalado e configurado como ativo no projeto!");
                    if (Application.isBatchMode) EditorApplication.Exit(0);
                }
                else
                {
                    Debug.LogError($"[LnD CLI] ERRO: Falha ao instalar o backend. Status final: {downloader.StatusMessage}");
                    if (Application.isBatchMode) EditorApplication.Exit(1);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LnD CLI] Exceção crítica na execução do comando CLI: {ex.Message}\n{ex.StackTrace}");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

    }
}
