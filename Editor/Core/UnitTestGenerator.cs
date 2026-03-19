using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using LaundryNDishes.UnityCore;
using LaundryNDishes.Data;
using LaundryNDishes.DomainAdapter;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEditor.Compilation;
using UnityEditor.TestTools.TestRunner.Api;
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
        public int TestPassed { get; private set; } = 0;
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
        public async Task Generate(MonoScript targetScript, string extra, TestType testType, string csvPath = null)
        {
            if (DoesTestExist(targetScript, extra))
            {
                Log("Teste ja existe exclua ele para gerar um novo");
                return;
            }
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
                    string intention = await GetIntentionAsync(testType, classSource, extra);

                    // ETAPA 2: Gerar e validar o código (na pasta Temp).
                    string folder = testType == TestType.Unitieditor
                        ? _config.EditorTestScriptsFolder
                        : _config.PlayTestDestinationFolder;

                    var (rawCompiledCode, correctionsMade) =
                        await GenerateAndCompileCodeAsync(testType, targetScript, extra, intention, folder);
                    corrections += correctionsMade;
                    // ETAPA 3: Salvar o arquivo final. (Ele renomeia a classe para bater com o nome do arquivo).
                    CurrentStep = GeneratingStep.SavingFile;
                    string finalPath = SaveFinalTestFile(rawCompiledCode, targetScript, extra, folder,
                        out string finalCode);
                    GeneratedTestCode = finalCode;

                    // ETAPA 4: Atualiza Banco de Dados (Passamos 'true' pois o arquivo compilou e salvou).
                    UpdateTestDatabase(targetScript, extra, testType,0, GeneratedTestCode, finalPath);
                    stopwatch.Stop();
                    generated = true;
                    
                    if (!string.IsNullOrEmpty(csvPath))
                    {
                        // SUTClass, SUTMethod, TestType, TestFile, NumberOfCorrections, TimeToGenerate
                        string csvLine =
                            $"{targetScript.name},{extra},{testType},{finalPath},{corrections},{attempts},{stopwatch.ElapsedMilliseconds},SUCESS\n";
                        File.AppendAllText(csvPath, csvLine); // Append garante que adiciona no final
                    }
                    break;
                }
                catch (Exception ex)
                {
                    corrections += _config.MaxCorrections;
                    Log($"Erro na geração: {ex.Message}");
                }
            }
            CurrentStep = GeneratingStep.Finished;
            UpdateTestDatabase(targetScript, extra, testType,0, null, null);
            if (!string.IsNullOrEmpty(csvPath)&&!generated)
            {
                Log($"Nao gerou com todas as {_config.MaxAttempts} tentativas");
                stopwatch.Stop();
                // SUTClass, SUTMethod, TestType, TestFile, NumberOfCorrections, TimeToGenerate
                string csvLine = $"{targetScript.name},{extra},{testType},-,{corrections},{attempts},{stopwatch.ElapsedMilliseconds},FAILURE\n";
                File.AppendAllText(csvPath, csvLine); // Append garante que adiciona no final
            }
        }

        /// <summary>
        /// Método usado pela UI do Editor (Gera, Salva e RODA o teste físico).
        /// </summary>
        public async Task GenerateAndTest(MonoScript targetScript, string extra, TestType testType)
        {
            if (DoesTestExist(targetScript, extra))
            {
                Log("Teste ja existe exclua ele para gerar um novo");
                return;
            }
            try
            {
                string classSource = File.ReadAllText(AssetDatabase.GetAssetPath(targetScript));

                // ETAPA 1: Obter a intenção.
                string intention = await GetIntentionAsync(testType, classSource, extra);

                // ETAPA 2: Gerar e validar.
                string folder = testType == TestType.Unitieditor 
                    ? _config.EditorTestScriptsFolder 
                    : _config.PlayTestDestinationFolder;

                var (rawCompiledCode,ties) = await GenerateAndCompileCodeAsync(testType, targetScript, extra, intention, folder);

                // ETAPA 3: Salvar o arquivo final e importar para a Unity reconhecer a nova classe.
                CurrentStep = GeneratingStep.SavingFile;
                string finalPath = SaveFinalTestFile(rawCompiledCode, targetScript, extra, folder, out string finalCode);
                GeneratedTestCode = finalCode;
                
                // Força a compilação do projeto para que o TestRunner enxergue a nova classe salva.
                AssetDatabase.Refresh();

                // ETAPA 4: Executar o teste gerado a partir do arquivo salvo.
                TestPassed = await ExecuteTestAsync(GeneratedTestCode, finalPath,testType);

                // ETAPA 5: Atualizar o Banco de Dados.
                UpdateTestDatabase(targetScript, extra,testType, TestPassed, GeneratedTestCode, finalPath);

                Log(TestPassed>0
                    ? "5. Teste passou! Processo finalizado com sucesso."
                    : "5. O teste gerado falhou ou não pôde ser executado.");
            }
            catch (Exception ex)
            {
                Log($"ERRO FATAL: {ex.Message}");
                UpdateTestDatabase(targetScript, extra,testType, 0, null, null);
            }
            finally
            {
                CurrentStep = GeneratingStep.Finished;
            }
        }
        
        /// <summary>
        /// Checa se já existe um teste gerado válido para a SUT e Método especificados.
        /// </summary>
        private bool DoesTestExist(MonoScript targetScript, string method)
        {
            var db = TestDatabase.Instance;
            if (db == null || db.AllTests == null) return false;

            // Procura um teste onde a SUT bate, o Método bate, e o arquivo físico (GeneratedTestScript) ainda existe
            bool exists = db.AllTests.Any(t => 
                t.TargetScript == targetScript && 
                t.SutMethod == method && 
                t.GeneratedTestScript != null);

            return exists;
        }

        #region Helper Methods - As Etapas da Geração
        
        public string GetReducedClassSource(SyntaxTree tree, string methodName)
        {
            try
            {
                var root = tree.GetRoot();
                string cleanMethodName = methodName.Split('(')[0].Trim();

                var targetMethod = root.DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .FirstOrDefault(m => m.Identifier.Text == cleanMethodName);

                if (targetMethod == null)
                    return tree.GetText().ToString();

                var walker = new MethodInvocationWalker();
                walker.Visit(targetMethod);

                var reducer = new SUTContextReducer(cleanMethodName, walker.CalledMethods);
                var reducedRoot = reducer.Visit(root);

                return reducedRoot.ToFullString();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Roslyn] Falha ao reduzir código. Usando original. Erro: {ex.Message}");
                return tree.GetText().ToString();
            }
        }
        
        
        /// <summary>
        /// Busca arquivos .cs no projeto (ignorando pastas de teste e ThirdParty) 
        /// que possuam uma chamada para o método da SUT.
        /// </summary>
        private string[] FindRelatedMethodsContext(SyntaxTree sutTree, string sutClassName, string methodName)
        {
            string cleanMethodName = methodName.Split('(')[0].Trim();

            var sutRoot = sutTree.GetRoot();

            var targetMethod = sutRoot.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.Text == cleanMethodName);

            if (targetMethod == null) return new string[0];

            // 1. Pegar todos os métodos chamados dentro do SUT
            var walker = new MethodInvocationWalker();
            walker.Visit(targetMethod);
            var allCalledMethods = walker.CalledMethods;

            // 2. Descobrir métodos da própria SUT
            var internalMethods = new HashSet<string>(
                sutRoot.DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .Select(m => m.Identifier.Text)
            );

            // 3. Chamadas externas
            var externalCalledMethods = allCalledMethods.Except(internalMethods).ToHashSet();
            if (externalCalledMethods.Count == 0) return new string[0];

            var relatedContexts = new List<string>();

            // 4. Buscar scripts do projeto
            string[] allScripts = AssetDatabase.FindAssets("t:MonoScript")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path =>
                    path.EndsWith(".cs") &&
                    !path.Contains("/ThirdParty/") &&
                    !path.Contains("/Plugins/") &&
                    System.IO.Path.GetFileNameWithoutExtension(path) != sutClassName
                )
                .ToArray();

            foreach (string path in allScripts)
            {
                string content = System.IO.File.ReadAllText(path, System.Text.Encoding.UTF8);

                // Filtro rápido melhorado
                bool mightContain = externalCalledMethods.Any(m => content.Contains(m + "("));
                if (!mightContain) continue;

                SyntaxTree extTree = CSharpSyntaxTree.ParseText(content);
                var extRoot = extTree.GetRoot();

                // 5. Verificar métodos declarados
                var declaredMethodsToKeep = extRoot.DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .Where(m =>
                        externalCalledMethods.Contains(m.Identifier.Text) &&
                        m.Body != null // evita interface/abstract
                    )
                    .Select(m => m.Identifier.Text)
                    .ToHashSet();

                if (declaredMethodsToKeep.Count == 0)
                    continue;

                string callerClass = System.IO.Path.GetFileNameWithoutExtension(path);

                // 6. Reduz classe externa
                var reducer = new SUTContextReducer(string.Empty, declaredMethodsToKeep);
                var reducedExtRoot = reducer.Visit(extRoot);

                string reducedContent = reducedExtRoot.ToFullString();

                relatedContexts.Add($"// External Dependency: {callerClass}\n{reducedContent}");

                // limite para não explodir contexto
                if (relatedContexts.Count >= 3)
                    break;
            }

            return relatedContexts.ToArray();
        }
        
        public async Task<string> GetIntentionAsync(TestType testType, string classSource, string extra)
        {
            CurrentStep = GeneratingStep.GettingIntention;
            Log("1. Gerando intenção do método...");
            Prompt intentionPrompt = _promptBuilder.BuildIntentionPrompt(testType, classSource, null, extra);
            var intentionRequest = new LLMRequestData { GeneratedPrompt = intentionPrompt, Config = _config };
            var intentionResponse = await _llmService.GetResponseAsync(intentionRequest,_config.ShowAllLLmComm);

            if (!intentionResponse.Success)
                throw new Exception("Falha ao obter a intenção: " + intentionResponse.ErrorMessage);

            Log($"Intenção recebida: {intentionResponse.Content}");
            return intentionResponse.Content;
        }

        /// <summary>
        /// ETAPA 2: Retorna APENAS o código como string. A compilação acontece isolada na Temp.
        /// </summary>
        public async Task<(string Code, int Corrections)> GenerateAndCompileCodeAsync(TestType testType, 
            MonoScript targetScript, string method, string initialIntention, string destinationFolder)        {
            string lastGeneratedCode = "";
            string structuredErrors = "";
            string rawClassSource = File.ReadAllText(AssetDatabase.GetAssetPath(targetScript));
            SyntaxTree sutTree = CSharpSyntaxTree.ParseText(rawClassSource);
            string reducedClassSource = GetReducedClassSource(sutTree, method);

            // 2. Busca referências simples pelo projeto (quem chama o método)
            string[] relatedMethods = FindRelatedMethodsContext(sutTree, targetScript.name, method);

            if (relatedMethods == null || relatedMethods.All(string.IsNullOrWhiteSpace))
            {
                relatedMethods = null;
            }

            for (int i = 0; i < _config.MaxCorrections; i++)
            {
                CurrentStep = (i == 0) ? GeneratingStep.GeneratingCode : GeneratingStep.CorrectingCode;
                Log($"2.{i + 1}. {(i == 0 ? "Gerando" : "Corrigindo")} código de teste (tentativa {i + 1})...");

                Prompt testPrompt = (i == 0)
                    // Passamos o 'reducedClassSource' no lugar do código original e o 'relatedMethods' no lugar do null
                    ? _promptBuilder.BuildGeneratorPrompt(testType, initialIntention, reducedClassSource, relatedMethods, method)
                    : _promptBuilder.BuildCorrectionPrompt(lastGeneratedCode, structuredErrors, relatedMethods);

                var testRequest = new LLMRequestData { GeneratedPrompt = testPrompt, Config = _config };
                var testResponse = await _llmService.GetResponseAsync(testRequest,_config.ShowAllLLmComm);
                if (!testResponse.Success) throw new Exception("Falha ao gerar o código: " + testResponse.ErrorMessage);

                lastGeneratedCode = CodeParser.ExtractTestCode(testResponse.Content);
                if (string.IsNullOrEmpty(lastGeneratedCode)) continue;
                
                var checker = new CompilationChecker();
                string tempFileNameBase = $"{targetScript.name}_{method}";
                
                // O validador usa a pasta Temp internamente, o 'destinationFolder' só serve pro nome do arquivo (se ele usar).
                await checker.Run(lastGeneratedCode, tempFileNameBase, destinationFolder,_config,testType==TestType.Unitieditor);

                bool hasReimplementedSUT = ScriptMethodAnalyzer.HasReimplementedType(lastGeneratedCode,targetScript.name);
                    
                bool hasReimplementedMethod = ScriptMethodAnalyzer.HasMethodImplementation(lastGeneratedCode,method);

                if (hasReimplementedSUT||hasReimplementedMethod)
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

        public async Task<int> ExecuteTestAsync(string code, string filePath, TestType testType)
        {
            if (SkipTestExecution)
            {
                Log("3. Pulando execução do teste (Modo CLI/Lote ativado).");
                return 0;
            }

            CurrentStep = GeneratingStep.RunningTests;
            Log("3. Executando os testes...");

            var executor = new TestExecutor();
            string className = CodeParser.ExtractClassName(code);
            
            // Agora o filePath é o arquivo final dentro da pasta Assets, então o Unity já sabe qual é o assembly.
            string assemblyName = CompilationPipeline.GetAssemblyNameFromScriptPath(filePath);
            TestMode mode = TestMode.PlayMode;
            if (testType == TestType.Unitieditor)
            {
                mode = TestMode.EditMode;
            }
            await executor.Run(assemblyName, className,mode);

            Log($"Resultado do teste: {(executor.TestResult != null && executor.TestResult.Value.Passed ? "Passou" : "Falhou")}");
            return executor.TestResult != null ? executor.TestResult.Value.PassCount:0;
        }

        private void UpdateTestDatabase(MonoScript targetScript, string extra, TestType testType, int testPassed, string generatedCode, string finalPath)
        {
            CurrentStep = GeneratingStep.UpdatingDatabase;
            Log("4. Atualizando o banco de dados...");

            var db = TestDatabase.Instance;
            if (db == null) return;

            if (string.IsNullOrEmpty(generatedCode) || string.IsNullOrEmpty(finalPath))
            {
                Log("AVISO: Geração falhou. Banco não será atualizado com o script.");
                return;
            }

            // Procura o teste não mais pelo MonoScript gerado, mas pela dupla: Script Alvo + Método Alvo
            var testEntry = db.AllTests.FirstOrDefault(t => t.TargetScript == targetScript && t.SutMethod == extra);

            if (testEntry == null)
            {
                testEntry = new GeneratedTestData(targetScript, extra,testType);
                db.AllTests.Add(testEntry);
            }

            // Atualiza os dados usando apenas o caminho da string!
            testEntry.GeneratedTestFilePath = finalPath; 
            testEntry.passedTestCount = testPassed;

            Log($"Atualizando entrada no banco para o arquivo '{finalPath}'.");
            EditorApplication.delayCall += () =>
            {
                if (db != null)
                {
                    EditorUtility.SetDirty(db);
                    AssetDatabase.SaveAssets();
                }
            };
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