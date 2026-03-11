using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEngine;

namespace LaundryNDishes.Core // Ajuste para o namespace correto do seu projeto
{
    public static class ScriptMethodAnalyzer
    {
        // Centralizamos a lista de métodos do Unity aqui. Fica mais fácil dar manutenção depois.
        private static readonly string[] UnityLifecycleMethods = { 
            // Inicialização e Ciclo de Vida
            "Awake", "Start", "OnEnable", "OnDisable", "OnDestroy", "Reset",

            // Updates
            "Update", "FixedUpdate", "LateUpdate",

            // Física 3D
            "OnCollisionEnter", "OnCollisionStay", "OnCollisionExit",
            "OnTriggerEnter", "OnTriggerStay", "OnTriggerExit",

            // Física 2D
            "OnCollisionEnter2D", "OnCollisionStay2D", "OnCollisionExit2D",
            "OnTriggerEnter2D", "OnTriggerStay2D", "OnTriggerExit2D",

            // Eventos de Mouse (Built-in)
            "OnMouseDown", "OnMouseDrag", "OnMouseEnter", "OnMouseExit", 
            "OnMouseOver", "OnMouseUp", "OnMouseUpAsButton",

            // Renderização e GUI
            "OnGUI", "OnDrawGizmos", "OnDrawGizmosSelected", 
            "OnPreCull", "OnPreRender", "OnPostRender", 
            "OnRenderImage", "OnRenderObject", "OnWillRenderObject",

            // Aplicação e Editor
            "OnApplicationFocus", "OnApplicationPause", "OnApplicationQuit", "OnValidate",

            // Animação e Partículas
            "OnAnimatorIK", "OnAnimatorMove",
            "OnParticleCollision", "OnParticleSystemStopped", "OnParticleTrigger", "OnParticleUpdateJobScheduled",

            // Transforms e UI
            "OnTransformChildrenChanged", "OnTransformParentChanged",
            "OnBeforeTransformParentChanged", "OnRectTransformDimensionsChange", "OnCanvasGroupChanged",

            // Áudio
            "OnAudioFilterRead"
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
                .Where(m => !m.IsSpecialName && !m.IsAbstract && !monoBehaviourMethods.Contains(m.Name));

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
                var methodInfo = scriptClass.GetMethod(lifecycleMethod, flags);
                if (methodInfo != null && !methodInfo.IsAbstract)
                {
                    behaviorMethods.Add(lifecycleMethod);
                }
            }

            return (unitMethods, behaviorMethods);
        }
        
        public static bool HasReimplementedType(string code, string targetTypeName)
        {
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetRoot();

            return root.DescendantNodes()
                .OfType<BaseTypeDeclarationSyntax>()
                .Any(t =>
                    t.Identifier.ValueText == targetTypeName
                );
        }
        
        public static bool HasMethodImplementation(string code, string methodName)
        {
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetRoot();

            return root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Any(m => m.Identifier.ValueText == methodName);
        }
    }
}