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

        [Tooltip("O arquivo de script de teste que foi gerado. A referência não quebra se o arquivo for renomeado.")]
        public MonoScript GeneratedTestScript;

        // Armazenamos como um Unix Timestamp (long) para robustez.
        // Usaremos um Property Drawer para mostrá-lo de forma legível.
        public long LastEditTimestamp;

        public GeneratedTestData(MonoScript script, string sutMethod)
        {
            TargetScript = script;
            SutMethod = sutMethod;
            GeneratedTestScript = null; // Preenchido após a geração
            LastEditTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }
}