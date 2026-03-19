using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace LaundryNDishes.DomainAdapter
{
    public class TestExecutor
    {
        public enum State { Idle, Running, Finished }
        public State CurrentState { get; private set; } = State.Idle;
        
        public bool IsDone => CurrentState == State.Finished;
        
        // --- MODIFICADO: Agora expõe os contadores exatos ---
        public (bool Passed, int PassCount, int FailCount)? TestResult { get; private set; }

        public async Task Run(string assemblyName, string className, TestMode mode)
        {
            if (CurrentState != State.Idle)
            {
                Debug.LogWarning("TestExecutor já está em execução.");
                return;
            }

            CurrentState = State.Running;
            try
            {
                var filter = new Filter
                {
                    testMode = mode, // Usa o modo passado por parâmetro
                    assemblyNames = new[] { assemblyName },
                    testNames = new[] { className }
                };
                
                var testCallbacks = new TestResultCallback();
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
            // Alterado para capturar a tupla de resultados
            public readonly TaskCompletionSource<(bool, int, int)> CompletionSource = new TaskCompletionSource<(bool, int, int)>();
            
            public void RunFinished(ITestResultAdaptor result) 
            {
                bool passed = result.PassCount > 0 && result.FailCount == 0 && result.InconclusiveCount == 0;
                CompletionSource.TrySetResult((passed, result.PassCount, result.FailCount));
            }
            
            public void RunStarted(ITestAdaptor testsToRun) {}
            public void TestStarted(ITestAdaptor test) {}
            public void TestFinished(ITestResultAdaptor result) {}
        }
    }
}