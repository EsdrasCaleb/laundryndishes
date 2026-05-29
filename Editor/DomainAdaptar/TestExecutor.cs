using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using LaundryNDishes.Data; // Garante o acesso aos enums e dados
using LaundryNDishes.UnityCore;

namespace LaundryNDishes.DomainAdapter
{
    public class TestExecutor
    {
        public enum State { Idle, Running, Finished }
        public State CurrentState { get; private set; } = State.Idle;

        public bool IsDone => CurrentState == State.Finished;
        public (bool Passed, int PassCount, int FailCount)? TestResult { get; private set; }

        public async Task Run(string assemblyName, string className, TestMode mode, string[] specificTestNames = null)
        {
            if (CurrentState != State.Idle)
            {
                Debug.LogWarning("[TestExecutor] Já está em execução.");
                return;
            }

            CurrentState = State.Running;
            try
            {
                string[] namesToRun = specificTestNames != null && specificTestNames.Length > 0
                    ? specificTestNames
                    : new[] { className };

                var filter = new Filter
                {
                    testMode = mode,
                    assemblyNames = new[] { assemblyName },
                    testNames = namesToRun
                };

                // Passamos o className para o callback saber qual registro atualizar no banco
                var testCallbacks = new TestResultCallback(className);
                var api = ScriptableObject.CreateInstance<TestRunnerApi>();
                api.RegisterCallbacks(testCallbacks);
                api.Execute(new ExecutionSettings(filter));

                TestResult = await testCallbacks.CompletionSource.Task;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                TestResult = (false, 0, 0);
            }
            finally
            {
                CurrentState = State.Finished;
            }
        }

        private class TestResultCallback : ICallbacks
        {
            public readonly TaskCompletionSource<(bool, int, int)> CompletionSource = new TaskCompletionSource<(bool, int, int)>();
            private readonly string _targetClassName;

            public TestResultCallback(string targetClassName)
            {
                _targetClassName = targetClassName;
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                try
                {
                    // Acessa a instância do banco de dados para salvar os resultados
                    var db = TestDatabase.Instance;
                    if (db != null)
                    {
                        var testData = db.AllTests.Find(t => t.GeneratedTestScript != null &&
                            (t.GeneratedTestScript.GetClass()?.FullName ?? t.GeneratedTestScript.name) == _targetClassName);

                        if (testData != null)
                        {
                            // Limpa os resultados individuais anteriores daquela classe antes de reescrever
                            testData.IndividualTests.Clear();

                            // Extrai recursivamente a árvore de resultados que a Unity gerou
                            ExtractIndividualTests(result, testData);
                            testData.WasExecuted = true;

                            // Força a Unity a salvar o ScriptableObject no disco rigidamente
                            EditorUtility.SetDirty(db);
                            AssetDatabase.SaveAssets();
                            Debug.Log($"[TestExecutor] Banco de dados atualizado com sucesso para: {_targetClassName}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[TestExecutor] Erro ao salvar telemetria no banco: {ex.Message}");
                }

                bool passed = result.PassCount > 0 && result.FailCount == 0 && result.InconclusiveCount == 0;
                CompletionSource.TrySetResult((passed, result.PassCount, result.FailCount));
            }

            private void ExtractIndividualTests(ITestResultAdaptor node, GeneratedTestData testData)
            {
                if (!node.HasChildren)
                {
                    // Ignora nós de setup/estruturais vazios se houverem
                    if (string.IsNullOrEmpty(node.Test.Name)) return;

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
                return unityStatus switch
                {
                    TestStatus.Passed => SingleTestStatus.Passed,
                    TestStatus.Failed => SingleTestStatus.Failed,
                    TestStatus.Inconclusive => SingleTestStatus.Inconclusive,
                    TestStatus.Skipped => SingleTestStatus.Skipped,
                    _ => SingleTestStatus.Unknown
                };
            }

            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
        }
    }
}
