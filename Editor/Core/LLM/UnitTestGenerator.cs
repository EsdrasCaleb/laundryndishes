using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
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
        /// O método principal, usado pela CLI ou lote (apenas gera e salva, não roda os testes).
        /// </summary>
        public async Task Generate(MonoScript targetScript, string extra, TestType testType, string csvPath = null, CancellationToken cancellationToken = default)
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
                // Verifica cancelamento no início de cada tentativa do loop principal
                cancellationToken.ThrowIfCancellationRequested();
                attempts = i;
                
                try
                {
                    string classSource = File.ReadAllText(AssetDatabase.GetAssetPath(targetScript));
        
                    // ETAPA 1: Obter a intenção do método (passando o token).
                    string intention = await GetIntentionAsync(testType, classSource, extra, cancellationToken);

                    // ETAPA 2: Gerar e validar o código (passando o token).
                    string folder = testType == TestType.Unitieditor
                        ? _config.EditorTestScriptsFolder
                        : _config.PlayTestDestinationFolder;

                    var (rawCompiledCode, correctionsMade) =
                        await GenerateAndCompileCodeAsync(testType, targetScript, extra, intention, folder, cancellationToken);
                        
                    corrections += correctionsMade;
                    
                    // ETAPA 3: Salvar o arquivo final.
                    cancellationToken.ThrowIfCancellationRequested();
                    CurrentStep = GeneratingStep.SavingFile;
                    string finalPath = SaveFinalTestFile(rawCompiledCode, targetScript, extra, folder, out string finalCode, testType);
                    GeneratedTestCode = finalCode;

                    // ETAPA 4: Atualiza Banco de Dados.
                    cancellationToken.ThrowIfCancellationRequested();
                    UpdateTestDatabase(targetScript, extra, testType, 0, GeneratedTestCode, finalPath);
                    stopwatch.Stop();
                    generated = true;
                    
                    if (!string.IsNullOrEmpty(csvPath))
                    {
                        string csvLine = $"{targetScript.name},{extra},{testType},{finalPath},{corrections},{attempts},{stopwatch.ElapsedMilliseconds},SUCESS\n";
                        File.AppendAllText(csvPath, csvLine);
                    }
                    break;
                }
                catch (Exception ex)
                {
                    // CRÍTICO: Se o erro for um pedido de cancelamento, não consome como erro comum; relança para a Janela Hub tratar.
                    if (ex is OperationCanceledException || cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    
                    corrections += _config.MaxCorrections;
                    Log($"Erro na geração: {ex.Message}");
                }
            }
            
            CurrentStep = GeneratingStep.Finished;
            UpdateTestDatabase(targetScript, extra, testType, 0, null, null);
            
            if (!string.IsNullOrEmpty(csvPath) && !generated)
            {
                Log($"Nao gerou com todas as {_config.MaxAttempts} tentativas");
                stopwatch.Stop();
                string csvLine = $"{targetScript.name},{extra},{testType},-,{corrections},{attempts},{stopwatch.ElapsedMilliseconds},FAILURE\n";
                File.AppendAllText(csvPath, csvLine);
            }
        }

        /// <summary>
        /// Método usado pela UI do Editor (Gera, Salva e RODA o teste físico).
        /// </summary>
        public async Task GenerateAndTest(MonoScript targetScript, string extra, TestType testType, CancellationToken cancellationToken = default)
        {
            if (DoesTestExist(targetScript, extra))
            {
                Log("Teste ja existe exclua ele para gerar um novo");
                return;
            }
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                string classSource = File.ReadAllText(AssetDatabase.GetAssetPath(targetScript));

                // ETAPA 1: Obter a intenção.
                string intention = await GetIntentionAsync(testType, classSource, extra, cancellationToken);

                // ETAPA 2: Gerar e validar.
                string folder = testType == TestType.Unitieditor 
                    ? _config.EditorTestScriptsFolder 
                    : _config.PlayTestDestinationFolder;

                var (rawCompiledCode, ties) = await GenerateAndCompileCodeAsync(testType, targetScript, extra, intention, folder, cancellationToken);

                // ETAPA 3: Salvar o arquivo final.
                cancellationToken.ThrowIfCancellationRequested();
                CurrentStep = GeneratingStep.SavingFile;
                string finalPath = SaveFinalTestFile(rawCompiledCode, targetScript, extra, folder, out string finalCode, testType);
                GeneratedTestCode = finalCode;
                
                AssetDatabase.Refresh();

                // ETAPA 4: Executar o teste gerado.
                cancellationToken.ThrowIfCancellationRequested();
                TestPassed = await ExecuteTestAsync(GeneratedTestCode, finalPath, testType, cancellationToken);

                // ETAPA 5: Atualizar o Banco de Dados.
                cancellationToken.ThrowIfCancellationRequested();
                UpdateTestDatabase(targetScript, extra, testType, TestPassed, GeneratedTestCode, finalPath);

                Log(TestPassed > 0
                    ? "5. Teste passou! Processo finalizado com sucesso."
                    : "5. O teste gerado falhou ou não pôde ser executado.");
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException || cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                Log($"ERRO FATAL: {ex.Message}");
                UpdateTestDatabase(targetScript, extra, testType, 0, null, null);
            }
            finally
            {
                CurrentStep = GeneratingStep.Finished;
            }
        }
        
        private bool DoesTestExist(MonoScript targetScript, string method)
        {
            var db = TestDatabase.Instance;
            if (db == null || db.AllTests == null) return false;

            return db.AllTests.Any(t => 
                t.TargetScript == targetScript && 
                t.SutMethod == method && 
                t.GeneratedTestScript != null);
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
            catch (Exception ex)
            {
                Debug.LogWarning($"[Roslyn] Falha ao reduzir código. Usando original. Erro: {ex.Message}");
                return tree.GetText().ToString();
            }
        }
        
        private string[] FindRelatedMethodsContext(SyntaxTree sutTree, string sutClassName, string methodName)
        {
            string cleanMethodName = methodName.Split('(')[0].Trim();
            var sutRoot = sutTree.GetRoot();

            var targetMethod = sutRoot.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.Text == cleanMethodName);

            if (targetMethod == null) return new string[0];

            var walker = new MethodInvocationWalker();
            walker.Visit(targetMethod);
            var allCalledMethods = walker.CalledMethods;

            var internalMethods = new HashSet<string>(
                sutRoot.DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .Select(m => m.Identifier.Text)
            );

            var externalCalledMethods = allCalledMethods.Except(internalMethods).ToHashSet();
            if (externalCalledMethods.Count == 0) return new string[0];

            var relatedContexts = new List<string>();

            string[] allScripts = AssetDatabase.FindAssets("t:MonoScript")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path =>
                    path.EndsWith(".cs") &&
                    !path.Contains("/ThirdParty/") &&
                    !path.Contains("/Plugins/") &&
                    Path.GetFileNameWithoutExtension(path) != sutClassName
                )
                .ToArray();

            foreach (string path in allScripts)
            {
                string content = File.ReadAllText(path, System.Text.Encoding.UTF8);

                bool mightContain = externalCalledMethods.Any(m => content.Contains(m + "("));
                if (!mightContain) continue;

                SyntaxTree extTree = CSharpSyntaxTree.ParseText(content);
                var extRoot = extTree.GetRoot();

                var declaredMethodsToKeep = extRoot.DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .Where(m =>
                        externalCalledMethods.Contains(m.Identifier.Text) &&
                        m.Body != null
                    )
                    .Select(m => m.Identifier.Text)
                    .ToHashSet();

                if (declaredMethodsToKeep.Count == 0)
                    continue;

                string callerClass = Path.GetFileNameWithoutExtension(path);

                var reducer = new SUTContextReducer(string.Empty, declaredMethodsToKeep);
                var reducedExtRoot = reducer.Visit(extRoot);

                string reducedContent = reducedExtRoot.ToFullString();
                relatedContexts.Add($"// External Dependency: {callerClass}\n{reducedContent}");

                if (relatedContexts.Count >= 3)
                    break;
            }

            return relatedContexts.ToArray();
        }
        
        public async Task<string> GetIntentionAsync(TestType testType, string classSource, string extra, CancellationToken cancellationToken = default)
        {
            CurrentStep = GeneratingStep.GettingIntention;
            Log("1. Gerando intenção do método...");
            
            Prompt intentionPrompt = _promptBuilder.BuildIntentionPrompt(testType, classSource, null, extra);
            var intentionRequest = new LLMRequestData { GeneratedPrompt = intentionPrompt, Config = _config };
            
            cancellationToken.ThrowIfCancellationRequested();
            var intentionResponse = await _llmService.GetResponseAsync(intentionRequest, _config.ShowAllLLmComm);
            cancellationToken.ThrowIfCancellationRequested();

            if (!intentionResponse.Success)
                throw new Exception("Falha ao obter a intenção: " + intentionResponse.ErrorMessage);

            Log($"Intenção recebida: {intentionResponse.Content}");
            return intentionResponse.Content;
        }

        public async Task<(string Code, int Corrections)> GenerateAndCompileCodeAsync(TestType testType, 
            MonoScript targetScript, string method, string initialIntention, string destinationFolder, CancellationToken cancellationToken = default)
        {
            string lastGeneratedCode = "";
            string structuredErrors = "";
            string rawClassSource = File.ReadAllText(AssetDatabase.GetAssetPath(targetScript));
            SyntaxTree sutTree = CSharpSyntaxTree.ParseText(rawClassSource);
            string reducedClassSource = GetReducedClassSource(sutTree, method);

            string[] relatedMethods = FindRelatedMethodsContext(sutTree, targetScript.name, method);
            if (relatedMethods == null || relatedMethods.All(string.IsNullOrWhiteSpace))
            {
                relatedMethods = null;
            }

            for (int i = 0; i < _config.MaxCorrections; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                CurrentStep = (i == 0) ? GeneratingStep.GeneratingCode : GeneratingStep.CorrectingCode;
                Log($"2.{i + 1}. {(i == 0 ? "Gerando" : "Corrigindo")} código de teste (tentativa {i + 1})...");

                Prompt testPrompt = (i == 0)
                    ? _promptBuilder.BuildGeneratorPrompt(testType, initialIntention, reducedClassSource, relatedMethods, method)
                    : _promptBuilder.BuildCorrectionPrompt(lastGeneratedCode, structuredErrors, relatedMethods);

                var testRequest = new LLMRequestData { GeneratedPrompt = testPrompt, Config = _config };
                
                cancellationToken.ThrowIfCancellationRequested();
                var testResponse = await _llmService.GetResponseAsync(testRequest, _config.ShowAllLLmComm);
                cancellationToken.ThrowIfCancellationRequested();
                
                if (!testResponse.Success) throw new Exception("Falha ao gerar o código: " + testResponse.ErrorMessage);

                lastGeneratedCode = CodeParser.ExtractTestCode(testResponse.Content);
                if (string.IsNullOrEmpty(lastGeneratedCode)) continue;
                
                var checker = new CompilationChecker();
                string tempFileNameBase = $"{targetScript.name}_{method}";
                
                cancellationToken.ThrowIfCancellationRequested();
                await checker.Run(lastGeneratedCode, tempFileNameBase, destinationFolder, _config, testType == TestType.Unitieditor);
                cancellationToken.ThrowIfCancellationRequested();

                bool hasReimplementedSUT = ScriptMethodAnalyzer.HasReimplementedType(lastGeneratedCode, targetScript.name);
                bool hasReimplementedMethod = ScriptMethodAnalyzer.HasMethodImplementation(lastGeneratedCode, method);

                if (hasReimplementedSUT || hasReimplementedMethod)
                {
                    Log($"[Validação] A IA tentou reimplementar a classe SUT '{targetScript.name}'. Rejeitando...");
                    structuredErrors = $"ERRO LÓGICO: Você declarou a classe '{targetScript.name}' dentro do arquivo de teste. Não reimplemente ou moke a classe original. Apenas crie a classe de testes.";
                    continue;
                }
                
                if (!checker.HasErrors)
                {
                    Log("Código validado com sucesso na pasta temporária!");
                    return (lastGeneratedCode, i);
                }

                structuredErrors = string.Join("\n", checker.CompilationErrors.Select(e => e.ToString()));
                Log($"Erros de compilação encontrados. A IA tentará corrigir...");
            }

            throw new Exception($"Não foi possível gerar um código de teste que compilasse após {_config.MaxCorrections} tentativas.");
        }

        public async Task<int> ExecuteTestAsync(string code, string filePath, TestType testType, CancellationToken cancellationToken = default)
        {
            if (SkipTestExecution)
            {
                Log("3. Pulando execução do teste (Modo CLI/Lote ativado).");
                return 0;
            }

            cancellationToken.ThrowIfCancellationRequested();
            CurrentStep = GeneratingStep.RunningTests;
            Log("3. Executando os testes...");

            var executor = new TestExecutor();
            string className = CodeParser.ExtractClassName(code);
            
            string assemblyName = CompilationPipeline.GetAssemblyNameFromScriptPath(filePath);
            TestMode mode = testType == TestType.Unitieditor ? TestMode.EditMode : TestMode.PlayMode;
            
            cancellationToken.ThrowIfCancellationRequested();
            await executor.Run(assemblyName, className, mode);
            
            Log($"Resultado do teste: {(executor.TestResult != null && executor.TestResult.Value.Passed ? "Passou" : "Falhou")}");
            return executor.TestResult != null ? executor.TestResult.Value.PassCount : 0;
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

            var testEntry = db.AllTests.FirstOrDefault(t => t.TargetScript == targetScript && t.SutMethod == extra);

            if (testEntry == null)
            {
                testEntry = new GeneratedTestData(targetScript, extra, testType);
                db.AllTests.Add(testEntry);
            }

            testEntry.GeneratedTestFilePath = finalPath; 

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

        private string SaveFinalTestFile(string rawCode, MonoScript targetScript, string extra, 
            string destinationFolder, out string updatedCode, TestType type)
        {
            if (string.IsNullOrEmpty(destinationFolder))
                throw new Exception("ERRO: A pasta de destino não foi configurada nos Project Settings!");

            string sanitizedExtra = extra.Split('(')[0].Trim(); 
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                sanitizedExtra = sanitizedExtra.Replace(c, '_');
            }

            string baseFileName = $"{targetScript.name}_{sanitizedExtra}_Test";
            string desiredPath = Path.Combine(destinationFolder, $"{baseFileName}.cs").Replace("\\", "/");

            string uniquePath = AssetDatabase.GenerateUniqueAssetPath(desiredPath);
            string finalClassNameWithSpaces = Path.GetFileNameWithoutExtension(uniquePath);
            string finalValidClassName = finalClassNameWithSpaces.Replace(" ", ""); 

            uniquePath = Path.Combine(destinationFolder, $"{finalValidClassName}.cs").Replace("\\", "/");

            string isolatedCode = this.IsolateGeneratedCode(rawCode, finalValidClassName);

            if (type != TestType.Unitieditor && LnDConfig.instance.DefaultTearDown)
            {
                 isolatedCode = InjectDefaultTearDownWithRoslyn(isolatedCode);
            }

            updatedCode = isolatedCode;
            File.WriteAllText(uniquePath, updatedCode);
            
            Log($"Arquivo de teste final isolado e salvo em: {uniquePath}");
            return uniquePath;
        }

        private string IsolateGeneratedCode(string rawCode, string finalValidClassName)
        {
            string isolationNamespace = $"LnDTests.{finalValidClassName}";
            var namespaceRegex = new Regex(@"\bnamespace\s+([\w\.]+)");
            var match = namespaceRegex.Match(rawCode);

            if (match.Success)
            {
                string existingNamespace = match.Groups[1].Value;
                int indexAfterNamespaceName = match.Index + match.Length;

                char terminator = ' ';
                int terminatorIndex = -1;
                for (int i = indexAfterNamespaceName; i < rawCode.Length; i++)
                {
                    if (rawCode[i] == ';' || rawCode[i] == '{')
                    {
                        terminator = rawCode[i];
                        terminatorIndex = i;
                        break;
                    }
                }

                if (terminator == ';')
                {
                    return rawCode.Replace($"namespace {existingNamespace}", $"namespace {existingNamespace}.{isolationNamespace}");
                }

                if (terminator == '{')
                {
                    string beforeBrace = rawCode.Substring(0, terminatorIndex + 1);
                    string afterBrace = rawCode.Substring(terminatorIndex + 1);

                    return $"{beforeBrace}\nnamespace {isolationNamespace}\n{{\n{afterBrace}\n}}";
                }
            }

            return $"namespace {isolationNamespace}\n{{\n{rawCode}\n}}";
        }

        private string InjectDefaultTearDownWithRoslyn(string sourceCode)
        {
            SyntaxTree tree = CSharpSyntaxTree.ParseText(sourceCode);
            CompilationUnitSyntax root = (CompilationUnitSyntax)tree.GetRoot();

            var allClasses = root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToList();
            var testClasses = allClasses.Where(ScriptMethodAnalyzer.IsTargetTestClass).ToList();

            if (testClasses.Count == 0) return sourceCode;

            string methodsToInjectText = @"
                [UnityEngine.TestTools.UnitySetUp]
                public System.Collections.IEnumerator LnDDefaultReloadScene()
                {
                    var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                    yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(scene.name, UnityEngine.SceneManagement.LoadSceneMode.Single);
                }

                [UnityEngine.TestTools.UnityTearDown]
                public System.Collections.IEnumerator LnDDefaultClearScene()
                {
                    var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
                    foreach (var go in roots)
                    {
                        if (go != null)
                        {
                            UnityEngine.Object.DestroyImmediate(go);
                        }
                    }
                    yield return null; 
                }
        ";

            var elementsToInject = CSharpSyntaxTree.ParseText(methodsToInjectText)
                .GetCompilationUnitRoot()
                .Members;

            root = root.TrackNodes(testClasses);

            foreach (var originalClass in testClasses)
            {
                var currentClass = root.GetCurrentNode(originalClass);
                if (currentClass != null)
                {
                    var updatedClass = currentClass.WithMembers(currentClass.Members.InsertRange(0, elementsToInject));
                    root = root.ReplaceNode(currentClass, updatedClass);
                }
            }

            return root.ToFullString();
        }
        #endregion
    }
}