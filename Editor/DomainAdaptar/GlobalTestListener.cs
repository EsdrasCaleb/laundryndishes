using LaundryNDishes.Data;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using LaundryNDishes.UnityCore;

namespace LaundryNDishes.DomainAdapter
{
    // Usamos uma classe estática separada apenas para fazer a inicialização na hora certa
    [InitializeOnLoad]
    public static class GlobalTestListenerInitializer
    {
        private static TestRunnerApi _api;

        static GlobalTestListenerInitializer()
        {
            // O delayCall joga a execução para o próximo ciclo de atualização da Unity,
            // fugindo da restrição do construtor estático.
            EditorApplication.delayCall += Initialize;
        }

        private static void Initialize()
        {
            if (_api == null)
            {
                _api = ScriptableObject.CreateInstance<TestRunnerApi>();
                _api.RegisterCallbacks(new GlobalTestListener());
            }
        }
    }

    // O nosso listener agora é um objeto C# puro, sem herdar de ScriptableObject
    public class GlobalTestListener : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { }

        public void RunFinished(ITestResultAdaptor result)
        {
            var db = TestDatabase.Instance;
            if (db == null) return;

            bool dbNeedsSave = false;

            ProcessResultTree(result, db, ref dbNeedsSave);

            if (dbNeedsSave)
            {
                EditorUtility.SetDirty(db);
                AssetDatabase.SaveAssets();
                Debug.Log("[LnD] TestDatabase atualizado com os resultados da última execução de testes!");
            }
        }

        private void ProcessResultTree(ITestResultAdaptor result, TestDatabase db, ref bool dbNeedsSave)
        {
            if (!result.HasChildren)
            {
                string parentClassName = result.Test.Parent?.FullName;
                string methodFullName = result.Test.FullName;
                string methodName = result.Test.Name;

                foreach (var testData in db.AllTests)
                {
                    if (testData.GeneratedTestScript != null)
                    {
                        var scriptClass = testData.GeneratedTestScript.GetClass();

                        if (scriptClass != null && scriptClass.FullName == parentClassName)
                        {
                            var testRecord = testData.IndividualTests.Find(t => t.FullName == methodFullName);

                            if (testRecord == null)
                            {
                                testRecord = new IndividualTestResult { FullName = methodFullName, MethodName = methodName };
                                testData.IndividualTests.Add(testRecord);
                            }

                            testRecord.Status = result.TestStatus switch
                            {
                                UnityEditor.TestTools.TestRunner.Api.TestStatus.Passed => SingleTestStatus.Passed,
                                UnityEditor.TestTools.TestRunner.Api.TestStatus.Failed => SingleTestStatus.Failed,
                                UnityEditor.TestTools.TestRunner.Api.TestStatus.Inconclusive => SingleTestStatus.Inconclusive,
                                UnityEditor.TestTools.TestRunner.Api.TestStatus.Skipped => SingleTestStatus.Skipped,
                                _ => SingleTestStatus.Unknown
                            };

                            dbNeedsSave = true;
                        }
                    }
                }
            }

            if (result.Children != null)
            {
                foreach (var child in result.Children)
                {
                    ProcessResultTree(child, db, ref dbNeedsSave);
                }
            }
        }

        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
    }
}
