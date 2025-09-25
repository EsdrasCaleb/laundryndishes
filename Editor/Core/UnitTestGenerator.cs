using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq; 
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEditor.Compilation;
using LaundryNDishes.Services; 
using LaundryNDishes.Data;  

namespace LaundryNDishes.Core // Colocando a lógica principal em um namespace Core
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
            Finished
        }

        public GeneratingStep CurrentStep { get; private set; } = GeneratingStep.Idle;
        public bool? TestPassed { get; private set; } = null;
        public string GeneratedTestCode { get; private set; }

        private readonly string _classSource;
        private readonly string _targetMethod;
        private readonly ILLMService _llmService;
        private readonly LnDConfig _config;

        // O construtor agora recebe a dependência do serviço LLM, em vez de criá-la.
        public UnitTestGenerator(string filePath, string targetMethod, ILLMService llmService, LnDConfig config)
        {
            _classSource = File.ReadAllText(filePath);
            _targetMethod = targetMethod;
            _llmService = llmService;
            _config = config;
        }

        // O método principal agora é assíncrono e retorna uma Task.
         public async Task Generate()
        {
            string tempTestPath = null;
            try
            {
                // ETAPA 1: Obter a intenção do método a ser testado.
                CurrentStep = GeneratingStep.GettingIntention;
                Debug.Log("1. Gerando intenção do método...");
                var intentionRequest = new LLMRequestData { Config = _config, Prompt = BuildIntentionPrompt() };
                var intentionResponse = await _llmService.GetResponseAsync(intentionRequest);
                if (!intentionResponse.Success) throw new Exception("Falha ao obter a intenção: " + intentionResponse.ErrorMessage);
                var methodDescription = intentionResponse.Content;
                Debug.Log($"Intenção recebida: {methodDescription}");

                // ETAPA 2: Gerar e corrigir o código do teste em um loop.
                string lastGeneratedCode = "";
                const int maxCorrectionAttempts = 3;
                for (int i = 0; i < maxCorrectionAttempts; i++)
                {
                    CurrentStep = (i == 0) ? GeneratingStep.GeneratingCode : GeneratingStep.CorrectingCode;
                    Debug.Log($"{(i == 0 ? "2." : "2." + (i + 1) + ".")} Gerando/Corrigindo código de teste (tentativa {i + 1})...");
                    var testRequest = new LLMRequestData { Config = _config, Prompt = BuildTestPrompt(methodDescription, lastGeneratedCode) };
                    var testResponse = await _llmService.GetResponseAsync(testRequest);
                    if (!testResponse.Success) throw new Exception("Falha ao gerar o código: " + testResponse.ErrorMessage);
                    
                    lastGeneratedCode = CodeParser.ExtractTestCode(testResponse.Content);

                    Debug.Log("3. Verificando erros de compilação...");
                    var compilationErrors = await CheckCompilationErrorsAsync(lastGeneratedCode);

                    if (string.IsNullOrEmpty(compilationErrors))
                    {
                        GeneratedTestCode = lastGeneratedCode;
                        Debug.Log("Código compilou com sucesso!");
                        break; // Sucesso! Sai do loop de correção.
                    }
                    
                    Debug.LogWarning($"Erros de compilação encontrados:\n{compilationErrors}");
                    methodDescription = $"O código anterior teve os seguintes erros: {compilationErrors}. Por favor, corrija-os e forneça apenas o código completo.";
                }

                if (string.IsNullOrEmpty(GeneratedTestCode))
                {
                    throw new Exception("Não foi possível gerar um código de teste que compilasse após várias tentativas.");
                }

                // ETAPA 3: Executar o teste compilado.
                CurrentStep = GeneratingStep.RunningTests;
                Debug.Log("4. Executando os testes...");
                (bool? testResult, string generatedPath) = await RunTestsAsync(GeneratedTestCode);
                tempTestPath = generatedPath; // Armazena o caminho para limpeza no 'finally'.
                TestPassed = testResult;

                // ETAPA 4: Salvar o arquivo final se o teste passou.
                if (TestPassed.HasValue && TestPassed.Value)
                {
                    Debug.Log("5. Teste passou! Salvando arquivo final...");
                    SaveFinalTestFile(GeneratedTestCode);
                }
                else
                {
                    Debug.LogWarning("5. O teste gerado falhou ou não pôde ser executado.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("Ocorreu um erro durante a geração: " + ex.Message);
                // Opcional: re-lançar a exceção se a UI precisar saber que algo deu errado.
                // throw; 
            }
            finally
            {
                // ETAPA 5: Limpeza garantida do arquivo temporário.
                CurrentStep = GeneratingStep.Finished;
                if (!string.IsNullOrEmpty(tempTestPath))
                {
                    AssetDatabase.DeleteAsset(tempTestPath);
                    Debug.Log("Arquivo de teste temporário deletado.");
                }
            }
        }

        // --- Métodos de Construção de Prompt ---

        private string BuildIntentionPrompt()
        {
            return $"Analise o seguinte código C# da Unity:\n```csharp\n{_classSource}\n```\nDescreva o propósito e a funcionalidade principal do método chamado '{_targetMethod}' em uma frase concisa e técnica.";
        }

        private string BuildTestPrompt(string description, string previousFaultyCode)
        {
            string prompt = $"Baseado no seguinte código C# da Unity:\n```csharp\n{_classSource}\n```\n";
            prompt += $"Gere um teste unitário completo usando o 'Unity Test Framework' (com NUnit) para o método '{_targetMethod}'. ";
            prompt += $"O teste deve garantir que o método {description}. ";

            if (!string.IsNullOrEmpty(previousFaultyCode))
            {
                prompt += $"A tentativa anterior de código foi:\n```csharp\n{previousFaultyCode}\n```\n";
            }
            
            prompt += "Forneça apenas o código C# completo, dentro de um bloco ```csharp ... ```, sem nenhuma explicação adicional.";
            return prompt;
        }

        // --- Métodos de Execução e Verificação (Agora Assíncronos) ---
        
        private async Task<(bool? testPassed, string tempFilePath)> RunTestsAsync(string testCode)
        {
            string tempFilePath = null;
            try
            {
                // 1. Salva o arquivo de teste temporário.
                tempFilePath = SaveTemporaryTestFile(testCode);
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

                // 2. Espera a compilação de forma robusta.
                bool compiledInTime = await WaitForCompilation();
                if (!compiledInTime) return (false, tempFilePath); // Retorna falha se houve timeout.

                string className = CodeParser.ExtractClassName(testCode);
                if (string.IsNullOrEmpty(className)) throw new Exception("Não foi possível extrair o nome da classe do teste.");

                string assemblyName = CompilationPipeline.GetAssemblyNameFromScriptPath(tempFilePath);
                if (string.IsNullOrEmpty(assemblyName)) throw new Exception("Não foi possível determinar o Assembly para o teste.");

                // 3. Cria o Filtro específico.
                var filter = new Filter()
                {
                    testMode = TestMode.EditMode,
                    assemblyNames = new[] { assemblyName },
                    testNames = new[] { className }
                };

                // 4. Executa a API com o filtro.
                var testCallbacks = new TestResultCallback();
                var api = ScriptableObject.CreateInstance<TestRunnerApi>();
                api.RegisterCallbacks(testCallbacks);
                api.Execute(new ExecutionSettings(filter));

                // 5. Espera o resultado e o retorna junto com o caminho do arquivo.
                bool result = await testCallbacks.CompletionSource.Task;
                return (result, tempFilePath);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                // Retorna falha e o caminho do arquivo para que ele possa ser limpo.
                return (false, tempFilePath);
            }
        }
        

        private class TestResultCallback : ICallbacks
        {
            // 3. AQUI ESTÁ O TRADUTOR: Criamos uma Task que podemos controlar.
            public readonly TaskCompletionSource<bool> CompletionSource = new TaskCompletionSource<bool>();
    
            // 4. Quando a Unity chama este método de callback (exatamente como no seu exemplo)...
            public void RunFinished(ITestResultAdaptor result)
            {
                bool success = result.PassCount > 0 && result.FailCount == 0;
        
                // ...nós manualmente completamos a nossa Task com o resultado.
                // Isso "desbloqueia" o 'await' no método RunTestsAsync.
                CompletionSource.TrySetResult(success);
            }

            // Outros métodos da interface que não precisamos usar para esta lógica.
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
        }

        // A implementação do ICallbacks usando TaskCompletionSource.
        private class TestResultCallback : ICallbacks
        {
            public readonly TaskCompletionSource<bool> CompletionSource = new TaskCompletionSource<bool>();
            
            public void RunFinished(ITestResultAdaptor result)
            {
                // Se ao menos um teste passou e nenhum falhou, consideramos sucesso.
                bool success = result.PassCount > 0 && result.FailCount == 0 && result.InconclusiveCount == 0;
                CompletionSource.TrySetResult(success);
            }

            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
        }
        
        // --- Métodos Auxiliares ---
        private void SaveFinalTestFile(string code)
        {
            string className = CodeParser.ExtractClassName(code) ?? _targetMethod.Replace("()", "");
            string fileName = $"{className}.cs"; // Sem o prefixo "Temp_"
            string path = Path.Combine(_config.TestDestinationFolder, fileName);
            
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, code);
            AssetDatabase.Refresh();
            Debug.Log($"Arquivo de teste final salvo em: {path}");
        }
        
        // Dentro da sua classe UnitTestGenerator

        /// <summary>
        /// Salva o código de teste em um arquivo temporário no local especificado pela configuração.
        /// </summary>
        /// <returns>O caminho relativo do arquivo salvo (ex: "Assets/Tests/Temp_MyTest.cs").</returns>
        private string SaveTemporaryTestFile(string code)
        {
            var config = LnDConfig.Load();
            string className = CodeParser.ExtractClassName(code) ?? _targetMethod.Replace("()", "");
    
            // Adiciona o prefixo Temp_ ao nome do arquivo.
            string fileName = $"Temp_{className}.cs";
    
            // Garante que o diretório de destino exista.
            Directory.CreateDirectory(config.TestDestinationFolder);

            string absolutePath = Path.Combine(config.TestDestinationFolder, fileName);
            File.WriteAllText(absolutePath, code);

            Debug.Log($"Arquivo de teste temporário salvo em: {absolutePath}");
    
            // Converte o caminho absoluto para um caminho relativo que a AssetDatabase entende.
            return GetProjectRelativePath(absolutePath);
        }

        // Helper para converter um caminho absoluto em um caminho relativo ao projeto.
        private string GetProjectRelativePath(string absolutePath)
        {
            return "Assets" + absolutePath.Replace("\\", "/").Substring(Application.dataPath.Length);
        }

        /// <summary>
        /// Espera a Unity terminar de compilar quaisquer scripts pendentes.
        /// </summary>
        /// <param name="timeoutInSeconds">Tempo máximo de espera em segundos.</param>
        /// <returns>Retorna true se a compilação terminou a tempo, false caso contrário.</returns>
        private async Task<bool> WaitForCompilation(int timeoutInSeconds = 15)
        {
            var startTime = Time.realtimeSinceStartup;
            Debug.Log("Aguardando compilação da Unity...");

            // Loop de espera ativa (polling)
            while (EditorApplication.isCompiling)
            {
                // Verifica se o tempo de espera foi excedido.
                if (Time.realtimeSinceStartup - startTime > timeoutInSeconds)
                {
                    Debug.LogError("Timeout! A compilação demorou mais de 15 segundos. Abortando.");
                    return false;
                }
                // Espera um curto período antes de verificar novamente.
                await Task.Delay(100); 
            }
    
            Debug.Log("Compilação finalizada.");
            return true;
        }
    }
    
}