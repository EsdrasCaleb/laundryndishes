using UnityEditor;
using UnityEngine;
using System;
using System.Threading.Tasks;
using LaundryNDishes.Core;
using LaundryNDishes.UnityCore;
using System.IO;
using LaundryNDishes.Data;
using UnityEditor.Compilation;
using UnityEditor.TestTools.TestRunner.Api;


namespace LaundryNDishes.CLI
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
                var config = LnDConfig.Instance;
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
            var config = LnDConfig.Instance;
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

                    string scriptAssemblyFile = CompilationPipeline.GetAssemblyNameFromScriptPath(assetPath);
                    bool isPlayModeTest = config.PlayModeTestAssembly != null && scriptAssemblyFile == config.PlayModeTestAssembly.name + ".dll";
                    bool isEditModeTest = config.EditorTestAssembly != null && scriptAssemblyFile == config.EditorTestAssembly.name + ".dll";

                    if (isPlayModeTest || isEditModeTest)
                    {
                        Debug.Log($"[LnD CLI] Pulando {script.name} - É um script de teste (Verificação por Assembly).");
                        continue;
                    }
    
                    // 2. Filtro Bônus de Segurança (Pelo caminho físico das pastas de teste do Config)
                    bool isInTestFolder = false;

                    if (config.PlayModeTestAssembly != null)
                    {
                        string playModePath = AssetDatabase.GetAssetPath(config.PlayModeTestAssembly);
                        if (!string.IsNullOrEmpty(playModePath))
                        {
                            string playModeDir = Path.GetDirectoryName(playModePath).Replace("\\", "/");
                            if (assetPath.StartsWith(playModeDir)) isInTestFolder = true;
                        }
                    }

                    if (config.EditorTestAssembly != null && !isInTestFolder)
                    {
                        string editorPath = AssetDatabase.GetAssetPath(config.EditorTestAssembly);
                        if (!string.IsNullOrEmpty(editorPath))
                        {
                            string editorDir = Path.GetDirectoryName(editorPath).Replace("\\", "/");
                            if (assetPath.StartsWith(editorDir)) isInTestFolder = true;
                        }
                    }

                    if (isInTestFolder)
                    {
                        Debug.Log($"[LnD CLI] Pulando {script.name} - Script está dentro do diretório de testes configurado.");
                        continue;
                    }
                    
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
        
        /// <summary>
        /// Comando da CLI: -executeMethod LaundryNDishes.CLI.LndCommandLineInterface.ExportTestReport -csv "Caminho/relatorio.csv"
        /// </summary>
        public static void ExportTestReport()
        {
            try
            {
                Debug.Log("[LnD CLI] Iniciando execução de testes para o relatório...");
                string csvPath = GetArgValue("-csv");

                if (string.IsNullOrEmpty(csvPath))
                {
                    Debug.LogError("[LnD CLI] ERRO: Caminho do CSV não fornecido. Use o argumento -csv <caminho_do_arquivo>");
                    EditorApplication.Exit(1);
                    return;
                }

                var reportTask = RunAllTestsAndGenerateReportAsync(csvPath);

                EditorApplication.update += () =>
                {
                    if (reportTask.IsCompleted)
                    {
                        if (reportTask.IsFaulted)
                        {
                            Debug.LogError($"[LnD CLI] ERRO FATAL AO GERAR RELATÓRIO: {reportTask.Exception?.GetBaseException().Message}");
                            EditorApplication.Exit(1);
                        }
                        else
                        {
                            Debug.Log("[LnD CLI] Relatório finalizado e salvo com sucesso!");
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

        private static async Task RunAllTestsAndGenerateReportAsync(string csvPath)
        {
            var db = TestDatabase.Instance;
            if (db == null || db.AllTests.Count == 0)
            {
                Debug.LogWarning("[LnD CLI] O banco de dados de testes está vazio.");
                System.IO.File.WriteAllText(csvPath, "File,Path,Type,PassedCount,FailedCount,Status\n");
                return;
            }

            var sb = new System.Text.StringBuilder();
            // Cabeçalho do CSV
            sb.AppendLine("File,Path,Type,PassedCount,FailedCount,Status");

            bool dbNeedsSave = false;

            for (int i = 0; i < db.AllTests.Count; i++)
            {
                var testData = db.AllTests[i];

                if (testData.GeneratedTestScript == null) continue;

                // 1. Extrai os caminhos e dados da Unity
                string assetPath = AssetDatabase.GetAssetPath(testData.GeneratedTestScript);
                string assemblyFile = CompilationPipeline.GetAssemblyNameFromScriptPath(assetPath);
                
                // O TestRunnerApi requer o nome do Assembly SEM a extensão ".dll"
                string assemblyName = assemblyFile.Replace(".dll", "");
                
                // O nome da classe com o namespace para que o TestRunner a encontre com precisão
                string className = testData.GeneratedTestScript.GetClass()?.FullName ?? testData.GeneratedTestScript.name;

                // 2. Define se roda no EditMode ou PlayMode baseado na enum que você guardou
                // (Se a sua enum se chamar diferente, ajuste aqui. Assumi "Unitieditor" como EditMode)
                TestMode mode = (testData.type.ToString() == "Unitieditor") ? TestMode.EditMode : TestMode.PlayMode;

                Debug.Log($"[LnD CLI] ({i + 1}/{db.AllTests.Count}) Rodando {className} [{mode}]...");

                // 3. Executa
                var executor = new LaundryNDishes.DomainAdapter.TestExecutor();
                await executor.Run(assemblyName, className, mode);

                // 4. Salva o resultado no banco e no CSV
                if (executor.TestResult.HasValue)
                {
                    var (passed, passCount, failCount) = executor.TestResult.Value;
                    
                    testData.passedInLastExecution = passed;
                    dbNeedsSave = true;

                    string status = passed ? "PASSED" : "FAILED";
                    sb.AppendLine($"{testData.GeneratedTestScript.name},{assetPath},{testData.type},{passCount},{failCount},{status}");
                }
                else
                {
                    sb.AppendLine($"{testData.GeneratedTestScript.name},{assetPath},{testData.type},0,0,ERROR");
                }
            }

            // Salva as alterações feitas na flag 'passedInLastExecution' do banco
            if (dbNeedsSave)
            {
                EditorUtility.SetDirty(db);
                AssetDatabase.SaveAssets();
            }

            System.IO.File.WriteAllText(csvPath, sb.ToString());
            Debug.Log($"[LnD CLI] Relatório CSV salvo fisicamente em: {csvPath}");
        }
    }
}
