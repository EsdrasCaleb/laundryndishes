using UnityEditor;
using UnityEngine;

namespace LaundryNDishes.UI
{
    public static class ContextMenuActions
    {
        private const string MENU_PATH = "Assets/LnD Generate Tests...";

        [MenuItem(MENU_PATH, false, 1000)]
        private static void OpenTestGeneratorHub()
        {
            var selectedScript = Selection.activeObject as MonoScript;
            // Chama o método estático para abrir nossa nova janela com o contexto do script.
            TestGeneratorHubWindow.OpenWindow(selectedScript);
        }

        [MenuItem(MENU_PATH, true)]
        private static bool OpenTestGeneratorHubValidation()
        {
            // O menu só aparece se o objeto selecionado for um arquivo de script.
            return Selection.activeObject is MonoScript;
        }
    }
}