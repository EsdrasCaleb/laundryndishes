using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
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
        /// <summary>
        /// Determina se uma classe é uma classe de teste válida baseada no seu conteúdo.
        /// </summary>
        public static bool IsTargetTestClass(ClassDeclarationSyntax classDecl)
        {
            // Classes de teste não podem ser abstratas (Unity/NUnit não as executa diretamente)
            if (classDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword)))
            {
                return false;
            }

            // Critério 1: A classe possui explicitamente o atributo [TestFixture]
            bool hasTestFixture = classDecl.AttributeLists
                .SelectMany(al => al.Attributes)
                .Any(a => {
                    string name = a.Name.ToString();
                    return name == "TestFixture" || name.EndsWith(".TestFixture");
                });
    
            if (hasTestFixture) return true;

            // Critério 2: A classe contém pelo menos UM método de teste (Usa o método auxiliar abaixo)
            bool containsTestMethods = classDecl.Members
                .OfType<MethodDeclarationSyntax>()
                .Any(IsTestMethod);

            return containsTestMethods;
        }

        /// <summary>
        /// Método auxiliar sugerido: Avalia se um método possui atributos de teste do NUnit ou Unity.
        /// </summary>
        public static bool IsTestMethod(MethodDeclarationSyntax methodDecl)
        {
            return methodDecl.AttributeLists
                .SelectMany(al => al.Attributes)
                .Any(a => {
                    string attrName = a.Name.ToString();
            
                    // Atende tanto a sintaxe curta [Test] quanto completas [NUnit.Framework.Test]
                    return attrName == "Test" || 
                           attrName.EndsWith(".Test") || 
                           attrName == "UnityTest" || 
                           attrName.EndsWith(".UnityTest");
                });
        }
        
        /// <summary>
        /// [Sobrecarga Reflection] Avalia se um método compilado possui atributos de teste do NUnit ou Unity.
        /// </summary>
        public static bool IsTestMethod(MethodInfo methodInfo)
        {
            if (methodInfo == null) return false;

            return Attribute.IsDefined(methodInfo, typeof(NUnit.Framework.TestAttribute)) || 
                   Attribute.IsDefined(methodInfo, typeof(UnityEngine.TestTools.UnityTestAttribute));
        }

        /// <summary>
        /// Extrai via Reflection todos os métodos de teste declarados diretamente no tipo (evita heranças e propriedades).
        /// </summary>
        public static IEnumerable<MethodInfo> GetTestMethods(Type scriptType)
        {
            if (scriptType == null) return Enumerable.Empty<MethodInfo>();

            return scriptType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Where(m => !m.IsSpecialName && m.DeclaringType == scriptType && IsTestMethod(m));
        }
    }
    
   
}