using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LaundryNDishes.UI
{
    public static class ContextMenuActions
    {
        // Caminho para o clique direito na aba Project (Assets)
        private const string ASSETS_MENU_PATH = "Assets/LnD/Generate Automated Tests";
        // Caminho para o clique direito na aba Hierarchy (GameObjects/Prefabs)
        private const string GAMEOBJECT_MENU_PATH = "GameObject/LnD/Generate Automated Tests";

        [MenuItem(ASSETS_MENU_PATH, false, 1000)]
        [MenuItem(GAMEOBJECT_MENU_PATH, false, 10)]
        private static void OpenTestGeneratorHub()
        {
            // Extrai todos os scripts únicos da seleção atual
            HashSet<MonoScript> extractedScripts = ExtractScriptsFromSelection();
            
            if (extractedScripts.Count > 0)
            {
                // Envia a lista completa de scripts encontrados para a Janela Hub
                TestGeneratorHubWindow.OpenWindow(extractedScripts.ToList());
            }
            else
            {
                Debug.LogWarning("[LnD] Nenhum script C# válido foi encontrado na seleção atual.");
            }
        }

        [MenuItem(ASSETS_MENU_PATH, true)]
        [MenuItem(GAMEOBJECT_MENU_PATH, true)]
        private static bool OpenTestGeneratorHubValidation()
        {
            // O menu só fica ativo se houver pelo menos um script válido ou GameObject com scripts na seleção
            return HasValidScriptInSelection();
        }

        /// <summary>
        /// Varre todos os objetos selecionados e extrai os MonoScripts correspondentes.
        /// </summary>
        private static HashSet<MonoScript> ExtractScriptsFromSelection()
        {
            var scripts = new HashSet<MonoScript>();

            if (Selection.objects == null) return scripts;

            foreach (var obj in Selection.objects)
            {
                // Cenário 1: O objeto selecionado já é diretamente um ficheiro de Script (.cs)
                if (obj is MonoScript script)
                {
                    scripts.Add(script);
                }
                // Cenário 2: O objeto é um GameObject da cena ou um Prefab do projeto
                else if (obj is GameObject go)
                {
                    // Procura todos os MonoBehaviours no objeto e nos seus filhos (útil para prefabs complexos)
                    var monoBehaviours = go.GetComponentsInChildren<MonoBehaviour>(true);
                    foreach (var mb in monoBehaviours)
                    {
                        if (mb == null) continue;
                        
                        // Extrai o MonoScript correspondente ao componente do Unity
                        var monoScript = MonoScript.FromMonoBehaviour(mb);
                        if (monoScript != null)
                        {
                            scripts.Add(monoScript);
                        }
                    }
                }
            }

            return scripts;
        }

        /// <summary>
        /// Validação rápida para determinar se o menu deve ou não ser exibido.
        /// </summary>
        private static bool HasValidScriptInSelection()
        {
            if (Selection.objects == null || Selection.objects.Length == 0) return false;

            foreach (var obj in Selection.objects)
            {
                if (obj is MonoScript) return true;
                
                if (obj is GameObject go)
                {
                    var mb = go.GetComponentInChildren<MonoBehaviour>(true);
                    if (mb != null && MonoScript.FromMonoBehaviour(mb) != null)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}