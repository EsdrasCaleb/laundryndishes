using System.IO;
using UnityEngine;

namespace Packages.LaundryNDishes
{
    public class UnitTestGenerator
    {
        private readonly string classSource;
        private readonly string targetMethod;
        private string methodDescription;
        private string unityTestCode;
        private readonly LLMRequestor llmRequestor;
        public int generatingSteps = 0;
        public bool testPassed = false;

        public UnitTestGenerator(string filePath, string targetMethod)
        {
            classSource = File.ReadAllText(filePath);
            this.targetMethod = targetMethod;
            llmRequestor = new LLMRequestor();
        }

        public void Generate()
        {
            generatingSteps = 1;
            Debug.Log("Generating intention...");
            llmRequestor.MakeRequest(BuildIntention(), GetIntention);
        }

        private void GetIntention(string rawResponse)
        {
            Debug.Log($"Intention received: {rawResponse}");
            methodDescription = ExtractIntention(rawResponse);
            generatingSteps = 2;

            Debug.Log("Generating test...");
            llmRequestor.MakeRequest(BuildTest(), TestCode);
        }

        private void TestCode(string rawResponse)
        {
            Debug.Log($"Test code received: {rawResponse}");
            unityTestCode = ExtractTestCode(rawResponse);
            SaveTestFile(unityTestCode);

            // Simulate running the test (replace with proper Unity Test execution)
            if (RunTest())
            {
                Debug.Log("Test passed!");
                testPassed = true;
            }
            else
            {
                Debug.LogWarning("Test failed, requesting fix...");
                generatingSteps = 3;
                llmRequestor.MakeRequest(BuildTest(), TestCode);
            }
        }

        private string BuildIntention()
        {
            return $"Analyze the following Unity C# code:\n```{classSource}```\nDescribe the purpose and functionality of the method named '{targetMethod}' in detail.";
        }

        private string BuildTest()
        {
            return $"Based on the following Unity C# code:\n```{classSource}```\nGenerate a Unity test using the 'Unity Test Framework' for the method '{targetMethod}'. The test should ensure that the method {methodDescription}.";
        }

        private string ExtractIntention(string rawResponse)
        {
            // TODO: Implement proper extraction from LLM response
            return rawResponse;  // Placeholder: assumes raw response is the intention
        }

        private string ExtractTestCode(string rawResponse)
        {
            // TODO: Implement code extraction (e.g., regex or JSON parsing)
            return rawResponse;  // Placeholder: assumes raw response is the code
        }

        private void SaveTestFile(string code)
        {
            string path = "Assets/Tests/GeneratedTests/" + targetMethod + "Test.cs";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, code);
            Debug.Log($"Test saved at: {path}");
        }

        private bool RunTest()
        {
            // TODO: Implement actual test execution
            Debug.Log("Running test...");
            return Random.value > 0.5f;  // Simulated result
        }
    }
}
