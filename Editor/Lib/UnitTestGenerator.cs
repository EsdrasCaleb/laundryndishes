using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEditor.TestTools.TestRunner.Api;

namespace Packages.LaundryNDishes
{
    public class UnitTestGenerator
    {
        public enum GeneratingStep
        {
            Initiated = 0,
            Intention = 1,
            Generation = 2,
            Correcting = 3,
            Testing = 4,
            Finished = 5
        }
        private readonly string classSource;
        private readonly string targetMethod;
        private string methodDescription;
        private string unityTestCode;
        private readonly LLMRequestor llmRequestor;
        public GeneratingStep generatingSteps = GeneratingStep.Initiated;
        public bool testPassed = false;

        public UnitTestGenerator(string filePath, string targetMethod)
        {
            classSource = File.ReadAllText(filePath);
            this.targetMethod = targetMethod;
            llmRequestor = new LLMRequestor();
        }

        public void Generate()
        {
            generatingSteps = GeneratingStep.Intention;
            Debug.Log("Generating intention...");
            llmRequestor.MakeRequest(BuildIntention(), GetIntention);
        }

        private void GetIntention(string rawResponse)
        {
            Debug.Log($"Intention received: {rawResponse}");
            methodDescription = rawResponse;
            generatingSteps = GeneratingStep.Generation;

            Debug.Log("Generating test...");
            llmRequestor.MakeRequest(BuildTest(), TestCode);
        }

        private void TestCode(string rawResponse)
        {
            Debug.Log($"Test code received: {rawResponse}");
            unityTestCode = ExtractTestCode(rawResponse);

            // Verifica se o código compila antes de salvar o arquivo
            CheckCompilationErrors(unityTestCode,CorrectOrSaveCode);
        }

        private void CorrectOrSaveCode(string compilationErrors)
        {
            if (!string.IsNullOrEmpty(compilationErrors))
            {
                Debug.LogWarning("Test has compilation errors, requesting fix...");
                generatingSteps = GeneratingStep.Finished;
                string fixPrompt = BuildTestFixPrompt(compilationErrors);
                llmRequestor.MakeRequest(fixPrompt, TestCode);
                return;
            }

            // Salva o arquivo apenas se não houver erros de compilação
            string filePath = SaveTestFile(unityTestCode);

            // Simula a execução do teste (substituir pelo mecanismo de execução do Unity Test Framework)
            RunTest(filePath);

        }
        
        private void CheckCompilationErrors(string code, Action<string> onErrorsChecked)
        {
            // Caminho temporário para o arquivo de teste
            string tempPath = Path.Combine(Application.dataPath, "TempTestScript.cs");
            generatingSteps = GeneratingStep.Correcting;
            try
            {
                // Salva o código temporariamente
                File.WriteAllText(tempPath, code);

                // Força a atualização do AssetDatabase, o que aciona a recompilação
                AssetDatabase.Refresh();

                // Variável para garantir que a exclusão do arquivo ocorra depois da compilação
                bool isCompilationComplete = false;

                // Aguarda a compilação ser concluída
                EditorApplication.delayCall += () =>
                {
                    if (isCompilationComplete)
                    {
                        string[] compilerErrors = CompilationChecker.GetCompilerErrors();
                        if (compilerErrors.Length > 0)
                        {
                            // Chama o callback com os erros de compilação
                            string errors = string.Join("\n", compilerErrors);
                            onErrorsChecked?.Invoke(errors);
                        }
                        else
                        {
                            // Caso não haja erros de compilação
                            onErrorsChecked?.Invoke("No compilation errors.");
                        }

                        // Agora que a compilação foi verificada, pode excluir o arquivo
                        DeleteTempFile(tempPath);
                    }
                };

                // Marca que a compilação foi concluída (a verificação de compilação pode ocorrer)
                isCompilationComplete = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error while checking compilation: {e.Message}");
                onErrorsChecked?.Invoke(e.Message); // Chama o callback com o erro
            }
        }

        private void DeleteTempFile(string tempPath)
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                    AssetDatabase.Refresh();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error while deleting temp file: {e.Message}");
            }
        }
        
        private string BuildTestFixPrompt(string compilationErrors)
        {
            return $"The following Unity test code has compilation errors:\n{unityTestCode}\nErrors:\n{compilationErrors}\nPlease fix the code.";
        }


        private string BuildIntention()
        {
            return $"Analyze the following Unity C# code:\n```{classSource}```\nDescribe the purpose and functionality of the method named '{targetMethod}' in one sentence.";
        }

        private string BuildTest()
        {
            return $"Based on the following Unity C# code:\n```{classSource}```\nGenerate a Unity test using the 'Unity Test Framework' for the method '{targetMethod}'. The test should ensure that the method {methodDescription}. Give just the code as the answer";
        }
        

        private string ExtractTestCode(string rawResponse)
        {
            if (string.IsNullOrWhiteSpace(rawResponse))
            {
                Debug.LogWarning("Empty or null response received.");
                return string.Empty;
            }

            // Look for code wrapped in triple backticks
            int startIndex = rawResponse.IndexOf("```", StringComparison.Ordinal);
            if (startIndex != -1)
            {
                startIndex += 3; // Move past the opening backticks
                int endIndex = rawResponse.IndexOf("```", startIndex, StringComparison.Ordinal);
                if (endIndex != -1)
                {
                    return rawResponse.Substring(startIndex, endIndex - startIndex).Trim();
                }
            }

            // If no triple backticks found, assume the entire response is code
            return rawResponse.Trim();
        }

        private string SaveTestFile(string code)
        {
            string targetMethodFormatted = targetMethod
                .Replace(" ", "_")  
                .Replace(",", "_")   
                .Replace("(", "_")   
                .Replace(")", "_"); 

            string path = "Assets/Tests/LnDUnity/" + targetMethodFormatted + "Test.cs";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, code);
            Debug.Log($"Test saved at: {path}");
            return path;
        }

        private void RunTest(string filePath)
        {
            string fileContent = File.ReadAllText(filePath);
            generatingSteps = GeneratingStep.Testing;
            // Use regex to find the class name
            string className = CodeParser.ExtractClassName(fileContent);
            
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.Execute(new ExecutionSettings(new Filter()
            {
                testNames = new [] {className+".*"}
            }));
            api.RegisterCallbacks(new CheckTestResult(this));
        }
        
        private class CheckTestResult : ICallbacks
        {
            private UnitTestGenerator generator;
            public CheckTestResult(UnitTestGenerator current)
            {
                generator=current;
            }
            public void RunStarted(ITestAdaptor testsToRun)
            {
       
            }

            public void RunFinished(ITestResultAdaptor result)
            {
       
            }

            public void TestStarted(ITestAdaptor test)
            {
       
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (!result.HasChildren && result.ResultState != "Passed")
                {
                    Debug.Log(string.Format("Test {0} {1}", result.Test.Name, result.ResultState));
                    generator.testPassed = false;
                }
                else
                {
                    Debug.Log("Test passed!");
                    generator.testPassed = true;
                }
                generator.generatingSteps = GeneratingStep.Finished;
            }
        }

    }
}
