using UnityEditor;
using UnityEngine;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using LaundryNDishes.Core;
using LaundryNDishes.Data;
using LaundryNDishes.Services;

namespace LaundryNDishes.UI
{
    public static class ContextMenuActions
    {
        private const string UNIT_TEST_MENU_PATH = "Assets/Laundry & Dishes/Generate Unit Test for Method";
        private const string BEHAVIOR_TEST_MENU_PATH = "Assets/Laundry & Dishes/Generate Behavioral Test";

        #region Unit Test Generation

        // O método agora é 'async void' para poder usar 'await'.
        [MenuItem(UNIT_TEST_MENU_PATH, false, 1000)]
        private static async void GenerateAllUnitTestsForScript()
        {
            var selectedScript = Selection.activeObject as MonoScript;
            var methodsToTest = GetPublicMethodsForTesting(selectedScript)
                .Select(m => m.Name) // Pegamos apenas os nomes
                .ToList();
            
            await RunSequentialGeneration(selectedScript, methodsToTest, PromptType.Uniti);
        }

        [MenuItem(UNIT_TEST_MENU_PATH, true)]
        private static bool GenerateUnitTestForScriptValidation()
        {
            var selectedScript = Selection.activeObject as MonoScript;
            return selectedScript != null && GetPublicMethodsForTesting(selectedScript).Any();
        }

        #endregion

        #region Behavioral Test Generation

        // O método agora é 'async void' para poder usar 'await'.
        [MenuItem(BEHAVIOR_TEST_MENU_PATH, false, 1001)]
        private static async void GenerateAllBehavioralTestsForScript()
        {
            var selectedScript = Selection.activeObject as MonoScript;
            var methodsToTest = GetImplementedLifecycleMethods(selectedScript);
            
            await RunSequentialGeneration(selectedScript, methodsToTest, PromptType.Behavior);
        }

        [MenuItem(BEHAVIOR_TEST_MENU_PATH, true)]
        private static bool GenerateBehavioralTestValidation()
        {
            var selectedScript = Selection.activeObject as MonoScript;
            if (selectedScript == null) return false;

            var scriptClass = selectedScript.GetClass();
            if (scriptClass == null || !typeof(MonoBehaviour).IsAssignableFrom(scriptClass)) return false;

            bool hasLifecycleMethods = GetImplementedLifecycleMethods(selectedScript).Any();
            
            return hasLifecycleMethods;
        }

        #endregion

        // <summary>
        /// O novo orquestrador que executa a geração para uma lista de métodos em sequência.
        /// </summary>
        private static async Task RunSequentialGeneration(MonoScript script, List<string> methodNames, PromptType promptType)
        {
            if (!methodNames.Any())
            {
                Debug.LogWarning("Nenhum método válido encontrado para a geração em lote.");
                return;
            }
            
            var progressWindow = EditorWindow.GetWindow<GenerationProgressWindow>("Test Generation");
            progressWindow.Show();
            
            var config = LnDConfig.Instance;
            var llmService = LLMServiceFactory.GetCurrentService();

            Debug.Log($"Iniciando geração em lote de {methodNames.Count} teste(s) do tipo '{promptType}' para o script '{script.name}'.");
            var generator = new UnitTestGenerator(script, llmService, config);
            // Loop que executa a geração para cada método, um de cada vez.
            foreach (var methodName in methodNames)
            {
                Debug.Log($"--- Gerando teste para o método: {methodName} ---");
                
                // A janela de progresso agora monitora o gerador atual.
                progressWindow.StartMonitoring(generator);

                // O 'await' aqui é a chave: ele pausa o loop até que a geração de um teste termine
                // antes de começar a próxima.
                await generator.Generate(promptType,methodName);
                
                Debug.Log($"--- Geração para {methodName} finalizada. ---");
            }

            Debug.Log("Geração em lote finalizada com sucesso!");
            // Podemos adicionar uma mensagem final na janela de progresso.
            progressWindow.ShowFinishedMessage($"Processo finalizado! {methodNames.Count} teste(s) foram processados.");
        }
        
        #region Helper Methods
        
        /// <summary>
        /// Pega todos os métodos públicos de instância de um script, adequados para testes unitários.
        /// </summary>
        private static List<MethodInfo> GetPublicMethodsForTesting(MonoScript script)
        {
            if (script == null) return new List<MethodInfo>();
            
            var scriptClass = script.GetClass();
            if (scriptClass == null) return new List<MethodInfo>();

            return scriptClass.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName) // Filtra getters/setters/construtores etc.
                .ToList();
        }
        
        /// <summary>
        /// Verifica quais métodos de ciclo de vida (Update, FixedUpdate, LateUpdate) estão implementados em um script.
        /// </summary>
        private static List<string> GetImplementedLifecycleMethods(MonoScript script)
        {
            var implementedMethods = new List<string>();
            var scriptClass = script.GetClass();
            if (scriptClass == null) return implementedMethods;
            
            // Precisamos verificar métodos privados/protegidos também, por isso 'NonPublic'.
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            if (scriptClass.GetMethod("Update", flags) != null) implementedMethods.Add("Update");
            if (scriptClass.GetMethod("FixedUpdate", flags) != null) implementedMethods.Add("FixedUpdate");
            if (scriptClass.GetMethod("LateUpdate", flags) != null) implementedMethods.Add("LateUpdate");

            return implementedMethods;
        }

        #endregion
    }
}