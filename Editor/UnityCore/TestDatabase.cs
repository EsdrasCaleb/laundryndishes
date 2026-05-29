using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using LaundryNDishes.Data;
using UnityEditor.TestTools.TestRunner.Api;

namespace LaundryNDishes.UnityCore
{
    [CreateAssetMenu(fileName = "TestDatabase", menuName = "Laundry & Dishes/Test Database")]
    public class TestDatabase : ScriptableObject, ICallbacks
    {
        public List<GeneratedTestData> AllTests = new List<GeneratedTestData>();
        
        [HideInInspector] public string CurrentTargetClassName = "";
        private Action _onTestExecutionFinished;

        private static TestDatabase _instance;

        public static TestDatabase Instance
        {
            get
            {
                if (_instance != null) return _instance;

                string[] guids = AssetDatabase.FindAssets("t:TestDatabase");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    _instance = AssetDatabase.LoadAssetAtPath<TestDatabase>(path);
                }
                else
                {
                    _instance = CreateInstance<TestDatabase>();
                    string path = "Assets/Editor/Data";
                    if (!Directory.Exists(path))
                    {
                        Directory.CreateDirectory(path);
                    }
                    AssetDatabase.CreateAsset(_instance, $"{path}/TestDatabase.asset");
                    AssetDatabase.SaveAssets();
                }

                return _instance;
            }
        }

        // A Unity chama isso automaticamente SEMPRE que termina de compilar os códigos do projeto.
        [UnityEditor.Callbacks.DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            var db = Instance;
            if (db != null)
            {
                db.ResolvePendingScripts();
            }
        }

        /// <summary>
        /// Varre o banco de dados procurando testes que têm o caminho do arquivo,
        /// mas ainda não têm o MonoScript associado (porque acabaram de ser gerados).
        /// </summary>
        public void ResolvePendingScripts()
        {
            bool needsSave = false;

            for (int i = AllTests.Count - 1; i >= 0; i--)
            {
                var test = AllTests[i];

                // Se falta o MonoScript, mas temos o caminho salvo...
                if (test.GeneratedTestScript == null && !string.IsNullOrEmpty(test.GeneratedTestFilePath))
                {
                    // 1. Verifica se o arquivo físico realmente existe no disco
                    if (!System.IO.File.Exists(test.GeneratedTestFilePath))
                    {
                        Debug.LogWarning($"[TestDatabase] O arquivo de teste '{test.GeneratedTestFilePath}' não foi encontrado. Removendo entrada órfã.");
                        AllTests.RemoveAt(i);
                        needsSave = true;
                        continue; // Pula o resto e vai para a próxima iteração
                    }

                    // 2. Agora é 100% seguro tentar carregar o arquivo (ele existe e a Unity já compilou!)
                    var scriptAsset = AssetDatabase.LoadAssetAtPath<MonoScript>(test.GeneratedTestFilePath);

                    if (scriptAsset != null)
                    {
                        test.GeneratedTestScript = scriptAsset;
                        needsSave = true;
                    }
                }
            }

            // Salva o banco de dados apenas se alguma referência nova foi conectada
            if (needsSave)
            {
                EditorUtility.SetDirty(this);
                AssetDatabase.SaveAssets();
                Debug.Log("[TestDatabase] Referências de scripts recém-gerados foram conectadas com sucesso!");
            }
        }

        public bool HasTestForMethod(MonoScript targetScript, string method)
        {
            if (targetScript == null || string.IsNullOrEmpty(method)) return false;

            // Verifica se existe algum teste na lista para este script e este método
            return AllTests.Exists(t => t.TargetScript == targetScript && t.SutMethod == method);
        }
        
        // =================================================================================
        // EXECUÇÃO DE TESTES
        // =================================================================================

        /// <summary>
        /// Executa um teste específico. Recebe um callback opcional para executar ao terminar.
        /// </summary>
        public void RunTest(GeneratedTestData testData, Action onFinishedCallback = null)
        {
            // Guarda o callback para ser chamado no RunFinished
            _onTestExecutionFinished = onFinishedCallback;
            CurrentTargetClassName = testData.GeneratedTestScript.GetClass()?.FullName ?? testData.GeneratedTestScript.name;
            TestMode mode = (testData.type.ToString() == "Unitieditor") ? TestMode.EditMode : TestMode.PlayMode;

            Debug.Log($"[TestDatabase] Executando teste: {CurrentTargetClassName} | Mode: {mode}");

            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            
            api.RegisterCallbacks(this); 
            api.Execute(new ExecutionSettings(new Filter { testNames = new[] { CurrentTargetClassName }, testMode = mode }));
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            Debug.Log($"[TestDatabase] Execution Finished Start");
            try
            {
                var testData = AllTests.Find(t => t.GeneratedTestScript != null && 
                               (t.GeneratedTestScript.GetClass()?.FullName ?? t.GeneratedTestScript.name) == CurrentTargetClassName);

                if (testData != null)
                {
                    testData.IndividualTests.Clear();
                    ExtractIndividualTests(result, testData);
                    testData.WasExecuted = true;
                    
                    EditorUtility.SetDirty(this);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"[TestDatabase] Testes extraídos e salvos com sucesso para: {CurrentTargetClassName}");
                }
            }
            finally
            {
                var api = ScriptableObject.CreateInstance<TestRunnerApi>();
                api.UnregisterCallbacks(this);
                CurrentTargetClassName = "";
                Debug.Log($"[TestDatabase] Execution Finished callback: {_onTestExecutionFinished}");
                // Chama o callback (se ele foi passado na hora do RunTest)
                _onTestExecutionFinished?.Invoke();
                
                // Limpa o callback da memória
                _onTestExecutionFinished = null;
            }
        }

        private void ExtractIndividualTests(ITestResultAdaptor node, GeneratedTestData testData)
        {
            if (!node.HasChildren)
            {
                testData.IndividualTests.Add(new IndividualTestResult
                {
                    MethodName = node.Test.Name,
                    FullName = node.Test.FullName,
                    Status = MapStatus(node.TestStatus)
                });
            }
            else
            {
                foreach (var child in node.Children)
                {
                    ExtractIndividualTests(child, testData);
                }
            }
        }

        private SingleTestStatus MapStatus(TestStatus unityStatus)
        {
            switch (unityStatus)
            {
                case TestStatus.Passed: return SingleTestStatus.Passed;
                case TestStatus.Failed: return SingleTestStatus.Failed;
                case TestStatus.Inconclusive: return SingleTestStatus.Inconclusive;
                case TestStatus.Skipped: return SingleTestStatus.Skipped;
                default: return SingleTestStatus.Unknown;
            }
        }

        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
    }
}
