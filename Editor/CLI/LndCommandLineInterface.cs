using UnityEditor;
using UnityEngine;
using System;
using System.Linq;
using LaundryNDishes.Core;
using LaundryNDishes.Data;

namespace LaundryNDishes.CLI
{
    public static class LndCommandLineInterface
    {
        // Exemplo de comando: 
        // Unity.exe -batchmode -nographics -executeMethod LaundryNDishes.CLI.LndCommandLineInterface.RunBenchmark -myArg "valor"

        public static void GenerateIntention()
        {
            Debug.Log("[LnD CLI] Iniciando Benchmark de Geração...");

            // 1. Capturar argumentos customizados
            string targetScriptPath = GetArgValue("-targetScript");
            string promptTypeStr = GetArgValue("-promptType");
            string resultUrl = GetArgValue("-outputPath");

            if (string.IsNullOrEmpty(targetScriptPath))
            {
                Debug.LogError("Caminho do script alvo não fornecido! Use -targetScript <path>");
                EditorApplication.Exit(1); // Sai com erro
                return;
            }

            // 2. Lógica de execução (exemplo rápido)
            // Aqui você chamaria seu UnitTestGenerator ou carregaria o TestDatabase
            
            Debug.Log($"[LnD CLI] Executando para: {targetScriptPath} com tipo {promptTypeStr}");

            // IMPORTANTE: Como sua geração é ASYNC, você precisaria de um 
            // pequeno helper para rodar tarefas async dentro de métodos estáticos da Unity
            // ou usar o EditorApplication.update para monitorar o progresso.
            
            // EditorApplication.Exit(0); // Sucesso
        }

        // Helper para ler argumentos do terminal
        private static string GetArgValue(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == name && i + 1 < args.Length)
                {
                    return args[i + 1];
                }
            }
            return null;
        }
        public static void GenerateTest()
        {
            
        }
        
    }
}