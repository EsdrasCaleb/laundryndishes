using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using LaundryNDishes.Services;
using LaundryNDishes.Data;
using UnityEditor.Compilation;

namespace LaundryNDishes.Core
{
    public class UnitTestGenerator
    {
        public enum GeneratingStep
        {
            Idle,
            GettingIntention,
            GeneratingCode,
            CorrectingCode,
            RunningTests,
            UpdatingDatabase,
            Finished
        }

        public GeneratingStep CurrentStep { get; private set; } = GeneratingStep.Idle;
        public bool? TestPassed { get; private set; } = null;
        public string GeneratedTestCode { get; private set; }

        private readonly MonoScript _targetScript;
        private readonly string _classSource;
        private string _extra;
        private readonly ILLMService _llmService;
        private readonly LnDConfig _config;
        private readonly PromptBuilder _promptBuilder;

        public UnitTestGenerator(MonoScript targetScript,  ILLMService llmService, LnDConfig config)
        {
            _targetScript = targetScript;
            string filePath = AssetDatabase.GetAssetPath(targetScript);
            _classSource = File.ReadAllText(filePath);
            
            _llmService = llmService;
            _config = config;
            _promptBuilder = new PromptBuilder(); // Instancia o builder.
        }

        /// <summary>
        /// Orquestra o processo completo de geração, compilação e execução de testes.
        /// </summary>
        public async Task Generate(PromptType promptType, string extra)
        {
            string tempTestPath = null;
            _extra = extra;
            try
            {
                // ETAPA 1: Obter a intenção do método.
                CurrentStep = GeneratingStep.GettingIntention;
                Debug.Log("1. Gerando intenção do método...");
                Prompt intentionPrompt = _promptBuilder.BuildIntentionPrompt(promptType,_classSource, null, _extra);
                var intentionRequest = new LLMRequestData { GeneratedPrompt = intentionPrompt, Config = _config };
                var intentionResponse = await _llmService.GetResponseAsync(intentionRequest);
                if (!intentionResponse.Success) throw new Exception("Falha ao obter a intenção: " + intentionResponse.ErrorMessage);
                var methodDescription = intentionResponse.Content;
                Debug.Log($"Intenção recebida: {methodDescription}");

                // ETAPA 2: Loop de Geração e Correção de Código.
                string lastGeneratedCode = "";
                string structuredErrors = "";
                for (int i = 0; i < 5; i++)
                {
                    Prompt testPrompt;
                    if (i == 0)
                    {
                        CurrentStep = GeneratingStep.GeneratingCode;
                        Debug.Log(
                            $"{(i == 0 ? "2." : "2." + (i + 1) + ".")} Gerando código de teste (tentativa {i + 1})...");
                        testPrompt = _promptBuilder.BuildGeneratorPrompt(promptType, methodDescription,
                            _classSource, null, _extra);
                    }
                    else
                    {
                        Debug.Log(
                            $"{(i == 0 ? "2." : "2." + (i + 1) + ".")} Corrigindo código de teste (tentativa {i + 1})...");
                        CurrentStep = GeneratingStep.CorrectingCode;
                        testPrompt =
                            _promptBuilder.BuildCorrectionPrompt(promptType, lastGeneratedCode, structuredErrors);
                    }

                    var testRequest = new LLMRequestData { GeneratedPrompt = testPrompt, Config = _config };
                    var testResponse = await _llmService.GetResponseAsync(testRequest);
                    if (!testResponse.Success) throw new Exception("Falha ao gerar o código: " + testResponse.ErrorMessage);
                    lastGeneratedCode = CodeParser.ExtractTestCode(testResponse.Content);
                    
                    var checker = new CompilationChecker();
                    await checker.Run(lastGeneratedCode,"Behavior",_config);

                    if (!checker.HasErrors)
                    {
                        GeneratedTestCode = lastGeneratedCode;
                        tempTestPath = checker.TempFilePath;
                        Debug.Log("Código compilou com sucesso!");
                        break; // Sucesso, sai do loop.
                    }
                    
                    structuredErrors = string.Join("\n", checker.CompilationErrors.Select(e => e.ToString()));
                    Debug.LogWarning($"Erros de compilação encontrados:\n{structuredErrors}");
                }

                if (string.IsNullOrEmpty(GeneratedTestCode))
                {
                    throw new Exception("Não foi possível gerar um código de teste que compilasse.");
                }

                // ETAPA 3: Executar o teste.
                CurrentStep = GeneratingStep.RunningTests;
                Debug.Log("3. Executando os testes...");
                var executor = new TestExecutor();
                string className = CodeParser.ExtractClassName(GeneratedTestCode);
                string assemblyName = CompilationPipeline.GetAssemblyNameFromScriptPath(tempTestPath);
                await executor.Run(assemblyName, className);
                TestPassed = executor.TestPassed;

                // ETAPA 4: Atualizar o Banco de Dados.
                CurrentStep = GeneratingStep.UpdatingDatabase;
                Debug.Log("4. Atualizando o banco de dados...");
                CurrentStep = GeneratingStep.UpdatingDatabase;
                Debug.Log("4. Atualizando o banco de dados...");
                UpdateTestDatabase(); // O método agora usa os resultados armazenados na classe.


                if (TestPassed.HasValue && TestPassed.Value)
                {
                    Debug.Log("5. Teste passou! Processo finalizado com sucesso.");
                }
                else
                {
                    Debug.LogWarning("5. O teste gerado falhou ou não pôde ser executado.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Ocorreu um erro fatal durante a geração: {ex.Message}");
                TestPassed = false;
                UpdateTestDatabase(); // Tenta salvar o resultado de falha mesmo assim.
            }
            finally
            {
                // ETAPA FINAL: Limpeza do arquivo temporário.
                CurrentStep = GeneratingStep.Finished;
                if (!string.IsNullOrEmpty(tempTestPath) && File.Exists(tempTestPath))
                {
                    AssetDatabase.DeleteAsset(tempTestPath);
                    Debug.Log("Arquivo de teste temporário deletado.");
                }
            }
        }
        
        /// <summary>
        /// Salva o arquivo de teste gerado e atualiza a entrada correspondente no banco de dados.
        /// A busca pela entrada agora é feita usando o MonoScript do arquivo de teste gerado.
        /// </summary>
        private void UpdateTestDatabase()
        {
            var db = TestDatabase.Instance;
            if (db == null)
            {
                Debug.LogError("Não foi possível encontrar o TestDatabase para salvar o resultado.");
                return;
            }

            // Se por algum motivo não houver código gerado, não há o que fazer.
            if (string.IsNullOrEmpty(GeneratedTestCode))
            {
                Debug.LogWarning("Nenhum código foi gerado, a atualização do banco de dados foi pulada.");
                return;
            }

            // --- ETAPA 1: Salvar o arquivo de teste e obter sua referência (MonoScript) ---
            // Esta etapa agora acontece PRIMEIRO, pois precisamos da referência para a busca.
            string finalPath = SaveFinalTestFile(GeneratedTestCode);
            var generatedTestMonoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(finalPath);

            if (generatedTestMonoScript == null)
            {
                Debug.LogError($"Falha ao carregar o MonoScript do arquivo de teste salvo em '{finalPath}'. A atualização do banco de dados foi abortada.");
                return;
            }

            // --- ETAPA 2: Procurar a entrada no DB usando a referência do arquivo gerado ---
            var testEntry = db.AllTests.FirstOrDefault(t => t.GeneratedTestScript == generatedTestMonoScript);

            // Se a entrada não existir para este arquivo de teste específico, cria uma nova.
            if (testEntry == null)
            {
                // Usamos o construtor simples e preenchemos os campos.
                testEntry = new GeneratedTestData(_targetScript, _extra);
                testEntry.GeneratedTestScript = generatedTestMonoScript; // Define a "chave" da busca.
                db.AllTests.Add(testEntry);
            }

            // --- ETAPA 3: Atualizar o estado da entrada (seja ela nova ou existente) ---
            testEntry.passedInLastExecution = TestPassed.HasValue && TestPassed.Value;
            testEntry.LastEditTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            
            // Garante que o TargetScript e SutMethod estão corretos (útil se a entrada foi criada agora).
            testEntry.TargetScript = _targetScript;
            testEntry.SutMethod = _extra;

            // --- ETAPA 4: Salvar as alterações no banco de dados ---
            Debug.Log($"Atualizando entrada no banco de dados para o teste '{generatedTestMonoScript.name}'. Resultado: {(testEntry.passedInLastExecution ? "Passou" : "Falhou")}");
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Salva o código do teste em um arquivo, garantindo que a pasta de destino exista
        /// e que nenhum arquivo existente seja sobrescrito.
        /// </summary>
        /// <param name="code">O código-fonte do teste a ser salvo.</param>
        /// <returns>O caminho relativo do arquivo que foi efetivamente salvo.</returns>
        private string SaveFinalTestFile(string code)
        {
            // 1. Determina o nome e o caminho desejado para o arquivo.
            string className = CodeParser.ExtractClassName(code) ?? _extra.Replace("()", "");
            string fileName = $"{className}.cs";
    
            // Garante que a pasta de destino exista. Esta parte do seu código já estava correta.
            string destinationFolder = _config.PlayTestDestinationFolder;
            Directory.CreateDirectory(destinationFolder);
    
            string desiredPath = Path.Combine(destinationFolder, fileName).Replace("\\", "/");

            // 2. A MÁGICA: Pede à Unity para gerar um caminho único.
            // Se 'desiredPath' já existir, a Unity retornará "Assets/Tests/MyTest 1.cs", etc.
            // Se não existir, ela retornará o próprio 'desiredPath'.
            string uniquePath = AssetDatabase.GenerateUniqueAssetPath(desiredPath);

            // (Opcional) Informa o usuário se o arquivo foi renomeado para evitar surpresas.
            if (uniquePath != desiredPath)
            {
                Debug.LogWarning($"O arquivo '{Path.GetFileName(desiredPath)}' já existia. Salvando como '{Path.GetFileName(uniquePath)}' para evitar sobrescrita.");
            }

            // 3. Escreve o arquivo no caminho garantidamente único.
            File.WriteAllText(uniquePath, code);

            // 4. Importa o novo asset para que a Unity o reconheça.
            AssetDatabase.ImportAsset(uniquePath);

            Debug.Log($"Arquivo de teste final salvo em: {uniquePath}");
            return uniquePath;
        }
    }
}