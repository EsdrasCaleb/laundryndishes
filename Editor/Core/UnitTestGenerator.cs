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
        
        public event Action<string> OnProgressLog;

        public GeneratingStep CurrentStep { get; private set; } = GeneratingStep.Idle;
        public bool? TestPassed { get; private set; } = null;
        public string GeneratedTestCode { get; private set; }

        private readonly MonoScript _targetScript;
        private string _extra;
        private readonly ILLMService _llmService;
        private readonly LnDConfig _config;
        private readonly PromptBuilder _promptBuilder;

        public UnitTestGenerator(ILLMService llmService, LnDConfig config)
        {
            _llmService = llmService;
            _config = config;
            _promptBuilder = new PromptBuilder();
        }
        
        private void Log(string message)
        {
            // Continua logando no console para depuração.
            Debug.Log(message);
            
            // Dispara o evento para qualquer "ouvinte" (nossa janela).
            // O '?.' é uma checagem de segurança para garantir que há pelo menos um ouvinte.
            OnProgressLog?.Invoke(message);
        }

        /// <summary>
        /// O método principal, agora atuando como um "orquestrador" limpo e legível.
        /// </summary>
        public async Task Generate(MonoScript targetScript, string extra, PromptType promptType)
        {
            string tempTestPath = null;
            try
            {
                string classSource = File.ReadAllText(AssetDatabase.GetAssetPath(targetScript));

                // ETAPA 1: Obter a intenção do método.
                string intention = await GetIntentionAsync(promptType, classSource, extra);

                // ETAPA 2: Gerar e compilar o código em um loop de tentativas.
                var generationResult = await GenerateAndCompileCodeAsync(promptType, targetScript, extra, intention);
                GeneratedTestCode = generationResult.CompiledCode;
                tempTestPath = generationResult.FilePath;
                
                // ETAPA 3: Executar o teste gerado.
                TestPassed = await ExecuteTestAsync(GeneratedTestCode, tempTestPath);

                // ETAPA 4: Atualizar o Banco de Dados.
                UpdateTestDatabase(targetScript, extra, TestPassed, GeneratedTestCode);
                
                Log(TestPassed.HasValue && TestPassed.Value
                    ? "5. Teste passou! Processo finalizado com sucesso."
                    : "5. O teste gerado falhou ou não pôde ser executado.");
            }
            catch (Exception ex)
            {
                Log($"ERRO FATAL: {ex.Message}");
                TestPassed = false;
                UpdateTestDatabase(targetScript, extra, false, null);
            }
            finally
            {
                // ETAPA FINAL: Limpeza do arquivo temporário.
                CleanupTemporaryFile(tempTestPath);
            }
        }

        #region Helper Methods - As Etapas da Geração

        /// <summary>
        /// ETAPA 1: Pede ao LLM para descrever a intenção do método/cenário.
        /// </summary>
        private async Task<string> GetIntentionAsync(PromptType promptType, string classSource, string extra)
        {
            CurrentStep = GeneratingStep.GettingIntention;
            Log("1. Gerando intenção do método...");
            Prompt intentionPrompt = _promptBuilder.BuildIntentionPrompt(promptType, classSource, null, extra);
            var intentionRequest = new LLMRequestData { GeneratedPrompt = intentionPrompt, Config = _config };
            var intentionResponse = await _llmService.GetResponseAsync(intentionRequest);

            if (!intentionResponse.Success)
                throw new Exception("Falha ao obter a intenção: " + intentionResponse.ErrorMessage);

            Log($"Intenção recebida: {intentionResponse.Content}");
            return intentionResponse.Content;
        }

        /// <summary>
        /// ETAPA 2: Entra em um loop para gerar o código e corrigi-lo até que compile.
        /// </summary>
        private async Task<(string CompiledCode, string FilePath)> GenerateAndCompileCodeAsync(PromptType promptType, MonoScript targetScript, string extra, string initialIntention)
        {
            string lastGeneratedCode = "";
            string structuredErrors = "";
            string classSource = File.ReadAllText(AssetDatabase.GetAssetPath(targetScript));

            for (int i = 0; i < 5; i++)
            {
                CurrentStep = (i == 0) ? GeneratingStep.GeneratingCode : GeneratingStep.CorrectingCode;
                Log($"2.{i + 1}. {(i == 0 ? "Gerando" : "Corrigindo")} código de teste (tentativa {i + 1})...");

                Prompt testPrompt = (i == 0)
                    ? _promptBuilder.BuildGeneratorPrompt(promptType, initialIntention, classSource, null, extra)
                    : _promptBuilder.BuildCorrectionPrompt(promptType, lastGeneratedCode, structuredErrors);

                var testRequest = new LLMRequestData { GeneratedPrompt = testPrompt, Config = _config };
                var testResponse = await _llmService.GetResponseAsync(testRequest);
                if (!testResponse.Success) throw new Exception("Falha ao gerar o código: " + testResponse.ErrorMessage);
                
                lastGeneratedCode = CodeParser.ExtractTestCode(testResponse.Content);
                if (string.IsNullOrEmpty(lastGeneratedCode)) continue;
                
                var checker = new CompilationChecker();
                // Passa um nome base para o arquivo temporário
                string tempFileNameBase = $"{targetScript.name}_{extra}";
                await checker.Run(lastGeneratedCode, tempFileNameBase, _config);

                if (!checker.HasErrors)
                {
                    Log("Código compilou com sucesso!");
                    return (lastGeneratedCode, checker.TempFilePath);
                }
                
                structuredErrors = string.Join("\n", checker.CompilationErrors.Select(e => e.ToString()));
                Log($"Erros de compilação encontrados.");
            }

            throw new Exception("Não foi possível gerar um código de teste que compilasse após 5 tentativas.");
        }

        /// <summary>
        /// ETAPA 3: Executa o teste compilado usando o Test Runner da Unity.
        /// </summary>
        private async Task<bool?> ExecuteTestAsync(string code, string filePath)
        {
            CurrentStep = GeneratingStep.RunningTests;
            Log("3. Executando os testes...");
            
            var executor = new TestExecutor();
            string className = CodeParser.ExtractClassName(code);
            string assemblyName = CompilationPipeline.GetAssemblyNameFromScriptPath(filePath);
            
            await executor.Run(assemblyName, className);
            
            Log($"Resultado do teste: {(executor.TestPassed.HasValue && executor.TestPassed.Value ? "Passou" : "Falhou")}");
            return executor.TestPassed;
        }

        /// <summary>
        /// ETAPA FINAL: Deleta o arquivo de teste temporário.
        /// </summary>
        private void CleanupTemporaryFile(string filePath)
        {
            CurrentStep = GeneratingStep.Finished;
            /*
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                AssetDatabase.DeleteAsset(filePath);
                Log("Arquivo de teste temporário deletado.");
            }
            */
        }

        /// <summary>
        /// ETAPA 4: Atualiza a entrada no banco de dados para o teste que acabou de ser gerado e executado.
        /// </summary>
        private void UpdateTestDatabase(MonoScript targetScript, string extra, bool? testPassed, string generatedCode)
        {
            CurrentStep = GeneratingStep.UpdatingDatabase;
            Log("4. Atualizando o banco de dados...");

            var db = TestDatabase.Instance;
            if (db == null)
            {
                Log("ERRO: Não foi possível encontrar o TestDatabase para salvar o resultado.");
                return;
            }

            if (string.IsNullOrEmpty(generatedCode))
            {
                Log("AVISO: Nenhum código foi gerado, a atualização do banco de dados foi pulada.");
                return;
            }

            // 1. Salva o código gerado em um arquivo permanente e obtém seu caminho.
            string finalPath = SaveFinalTestFile(generatedCode, targetScript, extra);
            if (string.IsNullOrEmpty(finalPath)) return; // Se o salvamento falhou, aborta.

            // 2. Carrega o MonoScript a partir do arquivo que acabamos de salvar.
            var generatedTestMonoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(finalPath);
            if (generatedTestMonoScript == null)
            {
                Log($"ERRO: Falha ao carregar o MonoScript de '{finalPath}'. A atualização foi abortada.");
                return;
            }

            // 3. Procura a entrada no DB usando a referência do arquivo gerado como chave.
            var testEntry = db.AllTests.FirstOrDefault(t => t.GeneratedTestScript == generatedTestMonoScript);

            // Se a entrada não existir, cria uma nova.
            if (testEntry == null)
            {
                testEntry = new GeneratedTestData(targetScript, extra);
                testEntry.GeneratedTestScript = generatedTestMonoScript;
                db.AllTests.Add(testEntry);
            }

            // 4. Atualiza o estado da entrada (seja ela nova ou existente).
            testEntry.passedInLastExecution = testPassed.HasValue && testPassed.Value;
            testEntry.LastEditTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            testEntry.TargetScript = targetScript; // Garante que a referência ao alvo está correta
            testEntry.SutMethod = extra;

            // 5. Salva as alterações no ScriptableObject.
            Log($"Atualizando entrada no banco de dados para '{generatedTestMonoScript.name}'. Resultado: {(testEntry.passedInLastExecution ? "Passou" : "Falhou")}");
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
        }


        /// <summary>
        /// Salva o código do teste em um arquivo permanente, com um nome padronizado e único.
        /// </summary>
        private string SaveFinalTestFile(string code, MonoScript targetScript, string extra)
        {
            string destinationFolder = _config.PlayTestDestinationFolder;

            if (string.IsNullOrEmpty(destinationFolder))
            {
                Log("ERRO: A pasta de destino para testes de Play Mode não foi configurada nos Project Settings!");
                return null;
            }

            // --- LÓGICA DE NOMENCLATURA PADRONIZADA ---

            // 1. Limpa o nome do método/descrição para ser seguro para um nome de arquivo.
            string sanitizedExtra = extra.Split('(')[0].Trim(); // Pega apenas o nome do método antes de parênteses
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            {
                sanitizedExtra = sanitizedExtra.Replace(c, '_');
            }

            // 2. Cria um nome base padronizado.
            // Ex: "Player_Jump_Test"
            string baseFileName = $"{targetScript.name}_{sanitizedExtra}_Test";
    
            // 3. Monta o caminho desejado.
            string desiredPath = Path.Combine(destinationFolder, $"{baseFileName}.cs").Replace("\\", "/");

            // 4. USA A FUNÇÃO DA UNITY PARA GARANTIR UNICIDADE.
            // Se "Player_Jump_Test.cs" já existir, esta função retornará "Player_Jump_Test 1.cs".
            string uniquePath = AssetDatabase.GenerateUniqueAssetPath(desiredPath);

            // --- FIM DA LÓGICA DE NOMENCLATURA ---

            File.WriteAllText(uniquePath, code);
            AssetDatabase.ImportAsset(uniquePath);

            Log($"Arquivo de teste final salvo em: {uniquePath}");
            return uniquePath;
        }
        

        #endregion
        
    }
}