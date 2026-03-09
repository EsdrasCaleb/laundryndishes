using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace LaundryNDishes.Core // Ajuste para o namespace correto do seu projeto
{
    public static class ScriptMethodAnalyzer
    {
        // Centralizamos a lista de métodos do Unity aqui. Fica mais fácil dar manutenção depois.
        private static readonly string[] UnityLifecycleMethods = { 
            "Awake", "Start", "OnEnable", "OnDisable", "Update", 
            "FixedUpdate", "LateUpdate", "OnCollisionEnter", "OnTriggerEnter",
            "OnDestroy", "OnGUI" 
        };

        /// <summary>
        /// Analisa um tipo e separa seus métodos em "Unitários" (lógica pura) e "Comportamentais" (ciclo de vida Unity).
        /// </summary>
        public static (List<string> UnitMethods, List<string> BehaviorMethods) CategorizeMethods(Type scriptClass)
        {
            var unitMethods = new List<string>();
            var behaviorMethods = new List<string>();

            if (scriptClass == null) 
                return (unitMethods, behaviorMethods);

            // 1. Extrai métodos unitários
            // Pegamos métodos base do MonoBehaviour (como Invoke, StartCoroutine) para ignorar
            var monoBehaviourMethods = new HashSet<string>(typeof(MonoBehaviour).GetMethods().Select(m => m.Name));
            
            var publicMethods = scriptClass.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName && !monoBehaviourMethods.Contains(m.Name));

            foreach (var method in publicMethods)
            {
                // Uma melhoria: Evita que um método "Start" público acabe na lista de testes unitários
                if (UnityLifecycleMethods.Contains(method.Name)) continue; 
                
                unitMethods.Add(method.Name);
            }

            // 2. Extrai métodos comportamentais (mesmo que sejam privados, o Unity os chama por "magia")
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            
            foreach (var lifecycleMethod in UnityLifecycleMethods)
            {
                if (scriptClass.GetMethod(lifecycleMethod, flags) != null)
                {
                    behaviorMethods.Add(lifecycleMethod);
                }
            }

            return (unitMethods, behaviorMethods);
        }
    }
}