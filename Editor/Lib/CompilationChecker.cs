using UnityEditor;
using UnityEditor.Compilation;
using System.Collections.Generic;
using UnityEngine;

namespace Packages.LaundryNDishes
{
    [InitializeOnLoad]
    public static class CompilationChecker
    {
        private static List<string> compilerErrors = new List<string>();

        static CompilationChecker()
        {
            CompilationPipeline.assemblyCompilationFinished += OnCompilationFinished;
        }

        private static void OnCompilationFinished(string assemblyPath, CompilerMessage[] messages)
        {
            compilerErrors.Clear();

            foreach (var message in messages)
            {
                if (message.type == CompilerMessageType.Error)
                {
                    compilerErrors.Add(message.message);
                }
            }
        }

        public static string[] GetCompilerErrors()
        {
            return compilerErrors.ToArray();
        }
    }
}