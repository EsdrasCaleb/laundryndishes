using System;
using UnityEditor;
using UnityEngine; // Necessário para MonoScript

namespace LaundryNDishes.Data
{

    [Serializable] // Essencial para que a Unity possa salvar essa classe dentro de um ScriptableObject
    public class GeneratedTestData
    {
        [Tooltip("O script que foi alvo da geração de testes.")]
        public MonoScript TargetScript;

        [Tooltip("O método (System Under Test) para o qual o teste foi gerado.")]
        public string SutMethod;

        [HideInInspector]
        public string GeneratedTestFilePath;
        
        [Tooltip("O arquivo de script de teste que foi gerado. A referência não quebra se o arquivo for renomeado.")]
        public MonoScript GeneratedTestScript;

        public TestType type;
        
        public int numberOfTests;
        public int passedTestCount;

        public GeneratedTestData(MonoScript script, string sutMethod, TestType type)
            : this(script, sutMethod,type, null, false)
        {
        }

        public GeneratedTestData(MonoScript script, string sutMethod, TestType testType, MonoScript generatedTestScript, bool passedInLastExecution)
        {
            TargetScript = script;
            SutMethod = sutMethod;
            type = testType;
            GeneratedTestScript = generatedTestScript;
        }
    }
}
