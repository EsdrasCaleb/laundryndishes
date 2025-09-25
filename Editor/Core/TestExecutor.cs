using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace LaundryNDishes.Core
{
    public class TestExecutor
    {
        public enum State { Idle, Running, Finished }
        public State CurrentState { get; private set; } = State.Idle;
        
        public bool IsDone => CurrentState == State.Finished;
        
        // --- Resultado ---
        public bool? TestPassed { get; private set; }

        public async Task Run(string assemblyName, string className)
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
                    testMode = TestMode.PlayMode,
                    assemblyNames = new[] { assemblyName },
                    testNames = new[] { className }
                };
                
                var testCallbacks = new TestResultCallback();
                var api = ScriptableObject.CreateInstance<TestRunnerApi>();
                api.RegisterCallbacks(testCallbacks);
                api.Execute(new ExecutionSettings(filter));

                TestPassed = await testCallbacks.CompletionSource.Task;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                TestPassed = false;
            }
            finally
            {
                CurrentState = State.Finished;
            }
        }
        
        private class TestResultCallback : ICallbacks
        {
            public readonly TaskCompletionSource<bool> CompletionSource = new TaskCompletionSource<bool>();
            public void RunFinished(ITestResultAdaptor result) => CompletionSource.TrySetResult(result.PassCount > 0 && result.FailCount == 0 && result.InconclusiveCount == 0);
            public void RunStarted(ITestAdaptor testsToRun) {}
            public void TestStarted(ITestAdaptor test) {}
            public void TestFinished(ITestResultAdaptor result) {}
        }
    }
}