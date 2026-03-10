using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using LaundryNDishes.UnityCore;
using LaundryNDishes.Data;
using LaundryNDishes.TestRunner;
using UnityEditor.Compilation;
using Debug = UnityEngine.Debug;

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
            SavingFile,
            RunningTests,
            UpdatingDatabase,
            Finished
        }

        public event Action<string> OnProgressLog;

        public GeneratingStep CurrentStep { get; private set; } = GeneratingStep.Idle;
        public bool? TestPassed { get; private set; } = null;
        public string GeneratedTestCode { get; private set; }

        public bool SkipTestExecution { get; set; } = false;

        private readonly MonoScript _targetScript;
        private readonly ILLMService _llmService;
        private readonly LnDConfig _config;
        private readonly PromptBuilder _promptBuilder;

        public UnitTestGenerator(ILLMService llmService, LnDConfig config)
        {
            _llmService = llmService;
            _config = config;
            _promptBuilder = new PromptBuilder();
        }

        public UnitTestGenerator() { }

        private void Log(string message)
        {
            Debug.Log(message);
            OnProgressLog?.Invoke(message);
        }

        /// <summary>
        /// O método principal, usado pela CLI (apenas gera e salva, não roda os testes).
        /// </summary>
        public async Task Generate(MonoScript targetScript, string extra, PromptType promptType, string csvPath = null)
        {
            var stopwatch = Stopwatch.StartNew();
            int attempts = 0; 
            int corrections = 0;
            bool generated = false;
            for (int i = 0; i < _config.MaxAttempts; i++)
            {
                attempts = i;
                try
                {
                    string classSource = File.ReadAllText(AssetDatabase.GetAssetPath(targetScript));

                    // ETAPA 1: Obter a intenção do método.
                    string intention = await GetIntentionAsync(promptType, classSource, extra);

                    // ETAPA 2: Gerar e validar o código (na pasta Temp).
                    string folder = promptType == PromptType.Unitieditor
                        ? _config.EditorTestScriptsFolder
                        : _config.PlayTestDestinationFolder;

                    var (rawCompiledCode, correctionsMade) =
                        await GenerateAndCompileCodeAsync(promptType, targetScript, extra, intention, folder);
                    corrections += correctionsMade;
                    // ETAPA 3: Salvar o arquivo final. (Ele renomeia a classe para bater com o nome do arquivo).
                    CurrentStep = GeneratingStep.SavingFile;
                    string finalPath = SaveFinalTestFile(rawCompiledCode, targetScript, extra, folder,
                        out string finalCode);
                    GeneratedTestCode = finalCode;

                    // ETAPA 4: Atualiza Banco de Dados (Passamos 'true' pois o arquivo compilou e salvou).
                    UpdateTestDatabase(targetScript, extra, true, GeneratedTestCode, finalPath);
                    stopwatch.Stop();
                    generated = true;
                    
                    if (!string.IsNullOrEmpty(csvPath))
                    {
                        // SUTClass, SUTMethod, TestType, TestFile, NumberOfCorrections, TimeToGenerate
                        string csvLine =
                            $"{targetScript.name},{extra},{promptType},{finalPath},{corrections},{attempts},{stopwatch.ElapsedMilliseconds},SUCESS\n";
                        File.AppendAllText(csvPath, csvLine); // Append garante que adiciona no final
                    }
                    break;
                }
                catch (Exception ex)
                {
                    Log($"Erro na geração: {ex.Message}");
                }
            }
            CurrentStep = GeneratingStep.Finished;
            UpdateTestDatabase(targetScript, extra, false, null, null);
            if (!string.IsNullOrEmpty(csvPath)&&!generated)
            {
                Log($"Nao gerou com todas as {_config.MaxAttempts} tentativas");
                stopwatch.Stop();
                // SUTClass, SUTMethod, TestType, TestFile, NumberOfCorrections, TimeToGenerate
                string csvLine = $"{targetScript.name},{extra},{promptType},-,{corrections},{attempts},{stopwatch.ElapsedMilliseconds},FAILURE\n";
                File.AppendAllText(csvPath, csvLine); // Append garante que adiciona no final
            }
        }

        /// <summary>
        /// Método usado pela UI do Editor (Gera, Salva e RODA o teste físico).
        /// </summary>
        public async Task GenerateAndTest(MonoScript targetScript, string extra, PromptType promptType)
        {
            try
            {
                string classSource = File.ReadAllText(AssetDatabase.GetAssetPath(targetScript));

                // ETAPA 1: Obter a intenção.
                string intention = await GetIntentionAsync(promptType, classSource, extra);

                // ETAPA 2: Gerar e validar.
                string folder = promptType == PromptType.Unitieditor 
                    ? _config.EditorTestScriptsFolder 
                    : _config.PlayTestDestinationFolder;

                var (rawCompiledCode,ties) = await GenerateAndCompileCodeAsync(promptType, targetScript, extra, intention, folder);

                // ETAPA 3: Salvar o arquivo final e importar para a Unity reconhecer a nova classe.
                CurrentStep = GeneratingStep.SavingFile;
                string finalPath = SaveFinalTestFile(rawCompiledCode, targetScript, extra, folder, out string finalCode);
                GeneratedTestCode = finalCode;
                
                // Força a compilação do projeto para que o TestRunner enxergue a nova classe salva.
                AssetDatabase.Refresh();

                // ETAPA 4: Executar o teste gerado a partir do arquivo salvo.
                TestPassed = await ExecuteTestAsync(GeneratedTestCode, finalPath);

                // ETAPA 5: Atualizar o Banco de Dados.
                UpdateTestDatabase(targetScript, extra, TestPassed, GeneratedTestCode, finalPath);

                Log(TestPassed.HasValue && TestPassed.Value
                    ? "5. Teste passou! Processo finalizado com sucesso."
                    : "5. O teste gerado falhou ou não pôde ser executado.");
            }
            catch (Exception ex)
            {
                Log($"ERRO FATAL: {ex.Message}");
                TestPassed = false;
                UpdateTestDatabase(targetScript, extra, false, null, null);
            }
            finally
            {
                CurrentStep = GeneratingStep.Finished;
            }
        }

        #region Helper Methods - As Etapas da Geração

        public async Task<string> GetIntentionAsync(PromptType promptType, string classSource, string extra)
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
        /// ETAPA 2: Retorna APENAS o código como string. A compilação acontece isolada na Temp.
        /// </summary>
        public async Task<(string Code, int Corrections)> GenerateAndCompileCodeAsync(PromptType promptType, MonoScript targetScript, string extra, string initialIntention, string destinationFolder)        {
            string lastGeneratedCode = "";
            string structuredErrors = "";
            string classSource = File.ReadAllText(AssetDatabase.GetAssetPath(targetScript));

            for (int i = 0; i < _config.MaxCorrections; i++)
            {
                CurrentStep = (i == 0) ? GeneratingStep.GeneratingCode : GeneratingStep.CorrectingCode;
                Log($"2.{i + 1}. {(i == 0 ? "Gerando" : "Corrigindo")} código de teste (tentativa {i + 1})...");

                Prompt testPrompt = (i == 0)
                    ? _promptBuilder.BuildGeneratorPrompt(promptType, initialIntention, classSource, null, extra)
                    : _promptBuilder.BuildCorrectionPrompt(lastGeneratedCode, structuredErrors);

                var testRequest = new LLMRequestData { GeneratedPrompt = testPrompt, Config = _config };
                var testResponse = await _llmService.GetResponseAsync(testRequest);
                if (!testResponse.Success) throw new Exception("Falha ao gerar o código: " + testResponse.ErrorMessage);

                lastGeneratedCode = CodeParser.ExtractTestCode(testResponse.Content);
                if (string.IsNullOrEmpty(lastGeneratedCode)) continue;
                
                var checker = new CompilationChecker();
                string tempFileNameBase = $"{targetScript.name}_{extra}";
                
                // O validador usa a pasta Temp internamente, o 'destinationFolder' só serve pro nome do arquivo (se ele usar).
                await checker.Run(lastGeneratedCode, tempFileNameBase, destinationFolder,_config,promptType==PromptType.Unitieditor);

                bool hasReimplementedSUT = Regex.IsMatch(
                    lastGeneratedCode, 
                    $@"\b(class|struct|interface|enum)\s+{targetScript.name}\b"
                );

                if (hasReimplementedSUT)
                {
                    Log($"[Validação] A IA tentou reimplementar a classe SUT '{targetScript.name}'. Rejeitando...");
                    // Força a mensagem de erro para que o LLM entenda o que não deve fazer no CorrectionPrompt
                    structuredErrors = $"ERRO LÓGICO: Você declarou a classe '{targetScript.name}' dentro do arquivo de teste. Não reimplemente ou moke a classe original. Apenas crie a classe de testes.";
                    continue; // Pula a compilação e vai direto para a próxima tentativa (Correção)
                }
                
                if (!checker.HasErrors)
                {
                    Log("Código validado com sucesso na pasta temporária!");
                    return (lastGeneratedCode, i); // Retorna o código limpo, sem caminhos.
                }

                structuredErrors = string.Join("\n", checker.CompilationErrors.Select(e => e.ToString()));
                Log($"Erros de compilação encontrados. A IA tentará corrigir...");
            }

            throw new Exception($"Não foi possível gerar um código de teste que compilasse após {_config.MaxCorrections} tentativas.");
        }

        public async Task<bool?> ExecuteTestAsync(string code, string filePath)
        {
            if (SkipTestExecution)
            {
                Log("3. Pulando execução do teste (Modo CLI/Lote ativado).");
                return true;
            }

            CurrentStep = GeneratingStep.RunningTests;
            Log("3. Executando os testes...");

            var executor = new TestExecutor();
            string className = CodeParser.ExtractClassName(code);
            
            // Agora o filePath é o arquivo final dentro da pasta Assets, então o Unity já sabe qual é o assembly.
            string assemblyName = CompilationPipeline.GetAssemblyNameFromScriptPath(filePath);

            await executor.Run(assemblyName, className);

            Log($"Resultado do teste: {(executor.TestPassed.HasValue && executor.TestPassed.Value ? "Passou" : "Falhou")}");
            return executor.TestPassed;
        }

        private void UpdateTestDatabase(MonoScript targetScript, string extra, bool? testPassed, string generatedCode, string finalPath)
        {
            CurrentStep = GeneratingStep.UpdatingDatabase;
            Log("4. Atualizando o banco de dados...");

            var db = TestDatabase.Instance;
            if (db == null)
            {
                Log("ERRO: Não foi possível encontrar o TestDatabase para salvar o resultado.");
                return;
            }

            if (string.IsNullOrEmpty(generatedCode) || string.IsNullOrEmpty(finalPath))
            {
                Log("AVISO: Geração falhou. Banco não será atualizado com o script.");
                return;
            }

            var generatedTestMonoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(finalPath);
            if (generatedTestMonoScript == null)
            {
                Log($"ERRO: Falha ao carregar o MonoScript de '{finalPath}'. A atualização foi abortada.");
                return;
            }

            var testEntry = db.AllTests.FirstOrDefault(t => t.GeneratedTestScript == generatedTestMonoScript);

            if (testEntry == null)
            {
                testEntry = new GeneratedTestData(targetScript, extra);
                testEntry.GeneratedTestScript = generatedTestMonoScript;
                db.AllTests.Add(testEntry);
            }

            testEntry.passedInLastExecution = testPassed;
            testEntry.LastEditTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            testEntry.TargetScript = targetScript; 
            testEntry.SutMethod = extra;

            Log($"Atualizando entrada no banco de dados para '{generatedTestMonoScript.name}'. Resultado: {testEntry.passedInLastExecution}");
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Salva o arquivo no destino final e garante que o nome da classe seja igual ao nome do arquivo.
        /// Retorna o caminho final e o código atualizado via parâmetro 'out'.
        /// </summary>
        private string SaveFinalTestFile(string rawCode, MonoScript targetScript, string extra, string destinationFolder, out string updatedCode)
        {
            if (string.IsNullOrEmpty(destinationFolder))
                throw new Exception("ERRO: A pasta de destino não foi configurada nos Project Settings!");

            // 1. Limpa o nome do método/descrição
            string sanitizedExtra = extra.Split('(')[0].Trim(); 
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            {
                sanitizedExtra = sanitizedExtra.Replace(c, '_');
            }

            // 2. Cria o nome base desejado
            string baseFileName = $"{targetScript.name}_{sanitizedExtra}_Test";
            string desiredPath = Path.Combine(destinationFolder, $"{baseFileName}.cs").Replace("\\", "/");

            // 3. Usa a Unity para gerar um caminho único (ex: Adiciona " 1" se já existir)
            string uniquePath = AssetDatabase.GenerateUniqueAssetPath(desiredPath);
            
            // Extrai apenas o nome do arquivo resultante, sem extensão e sem caminhos, 
            // ex: "Player_Jump_Test 1"
            string finalClassNameWithSpaces = Path.GetFileNameWithoutExtension(uniquePath);
            
            // Nomes de classe em C# não podem ter espaços. O Unity coloca um espaço quando gera unique.
            string finalValidClassName = finalClassNameWithSpaces.Replace(" ", ""); 

            // Como tiramos o espaço do nome da classe, precisamos garantir que o nome do arquivo acompanhe
            // para não quebrar a regra da Unity.
            uniquePath = Path.Combine(destinationFolder, $"{finalValidClassName}.cs").Replace("\\", "/");

            // 4. Substitui o nome da classe no código original gerado pela IA pelo nome final que decidimos
            string originalClassName = CodeParser.ExtractClassName(rawCode);
            if (!string.IsNullOrEmpty(originalClassName) && originalClassName != finalValidClassName)
            {
                // Substituição segura usando Regex para pegar a declaração da classe
                string pattern = $@"\bclass\s+{originalClassName}\b";
                updatedCode = Regex.Replace(rawCode, pattern, $"class {finalValidClassName}");
                Log($"Nome da classe atualizado internamente de '{originalClassName}' para '{finalValidClassName}'");
            }
            else
            {
                updatedCode = rawCode;
            }

            // 5. Salva fisicamente e importa
            File.WriteAllText(uniquePath, updatedCode);
            
            Log($"Arquivo de teste final salvo em: {uniquePath}");
            return uniquePath;
        }
        #endregion
    }
}