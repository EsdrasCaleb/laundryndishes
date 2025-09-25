using UnityEditor;
using UnityEngine;
using System.Reflection;
using System.Linq;
using LaundryNDishes.Core;
using LaundryNDishes.Data;
using LaundryNDishes.Services;

namespace LaundryNDishes.UI
{
    public static class ContextMenuActions
    {
        // O caminho "Assets/..." adiciona a opção ao menu de contexto de qualquer asset.
        // O último parâmetro 'false' indica que é uma ação, e o '1000' define a prioridade (ordem no menu).
        [MenuItem("Assets/Laundry & Dishes/Generate Unit Test...", false, 1000)]
        private static void GenerateTestForScript()
        {
            var selectedScript = Selection.activeObject as MonoScript;
            if (selectedScript == null) return;

            var scriptClass = selectedScript.GetClass();
            if (scriptClass == null)
            {
                Debug.LogWarning("Não foi possível encontrar uma classe no arquivo de script selecionado.");
                return;
            }

            // Usamos Reflection para encontrar todos os métodos públicos e não-estáticos declarados na classe.
            var methods = scriptClass.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName) // Filtra getters, setters, etc.
                .ToList();

            if (methods.Count == 0)
            {
                EditorUtility.DisplayDialog("Nenhum Método Encontrado", "Nenhum método público e de instância foi encontrado nesta classe para gerar um teste.", "OK");
                return;
            }

            // Cria um menu de seleção dinâmico com os métodos encontrados.
            var menu = new GenericMenu();
            foreach (var method in methods)
            {
                // Para cada método, adiciona um item ao menu.
                // O segundo parâmetro é a função que será chamada quando o item for clicado.
                menu.AddItem(new GUIContent(method.Name), false, OnMethodSelected, new object[] { selectedScript, method.Name });
            }
            
            // Exibe o menu de seleção para o usuário.
            menu.ShowAsContext();
        }

        /// <summary>
        /// Este é o "validation method". Ele decide se o item de menu deve estar habilitado ou não.
        /// Retorna true apenas se o objeto selecionado for um MonoScript (um arquivo .cs).
        /// </summary>
        [MenuItem("Assets/Laundry & Dishes/Generate Unit Test...", true)]
        private static bool GenerateTestForScriptValidation()
        {
            return Selection.activeObject is MonoScript;
        }

        /// <summary>
        /// Esta função é o callback que é executado após o usuário escolher um método no menu.
        /// </summary>
        private static async void OnMethodSelected(object userData)
        {
            var data = (object[])userData;
            var script = (MonoScript)data[0];
            var methodName = (string)data[1];

            Debug.Log($"Iniciando geração de teste para: {script.name}.{methodName}");

            // 1. Abre (ou foca) a nossa janela de progresso.
            var progressWindow = EditorWindow.GetWindow<GenerationProgressWindow>("Test Generation");
            progressWindow.Show();
            
            // 2. Prepara as dependências para o gerador.
            var config = LnDConfig.Instance;
            var llmService = LLMServiceFactory.GetCurrentService();
            
            // 3. Cria a instância do gerador.
            var generator = new UnitTestGenerator(script, methodName, llmService, config);
            
            // 4. Passa a referência do gerador para a janela para que ela possa mostrar o progresso.
            progressWindow.StartMonitoring(generator);

            // 5. Inicia o processo de geração (não bloqueia o editor).
            await generator.Generate();
        }
    }
}