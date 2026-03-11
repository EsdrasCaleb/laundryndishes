using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using LaundryNDishes.Data;

namespace LaundryNDishes.UnityCore
{
    [CreateAssetMenu(fileName = "TestDatabase", menuName = "Laundry & Dishes/Test Database")]
    public class TestDatabase : ScriptableObject
    {
        public List<GeneratedTestData> AllTests = new List<GeneratedTestData>();

        private static TestDatabase _instance;

        public static TestDatabase Instance
        {
            get
            {
                if (_instance != null) return _instance;

                string[] guids = AssetDatabase.FindAssets("t:TestDatabase");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    _instance = AssetDatabase.LoadAssetAtPath<TestDatabase>(path);
                }
                else
                {
                    _instance = CreateInstance<TestDatabase>();
                    string path = "Assets/Editor/Data"; 
                    if (!Directory.Exists(path))
                    {
                        Directory.CreateDirectory(path);
                    }
                    AssetDatabase.CreateAsset(_instance, $"{path}/TestDatabase.asset");
                    AssetDatabase.SaveAssets();
                }

                return _instance;
            }
        }
        
        // A Unity chama isso automaticamente SEMPRE que termina de compilar os códigos do projeto.
        [UnityEditor.Callbacks.DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            var db = Instance;
            if (db != null)
            {
                db.ResolvePendingScripts();
            }
        }

        /// <summary>
        /// Varre o banco de dados procurando testes que têm o caminho do arquivo, 
        /// mas ainda não têm o MonoScript associado (porque acabaram de ser gerados).
        /// </summary>
        public void ResolvePendingScripts()
        {
            bool needsSave = false;

            for (int i = AllTests.Count - 1; i >= 0; i--)
            {
                var test = AllTests[i];

                // Se falta o MonoScript, mas temos o caminho salvo...
                if (test.GeneratedTestScript == null && !string.IsNullOrEmpty(test.GeneratedTestFilePath))
                {
                    // 1. Verifica se o arquivo físico realmente existe no disco
                    if (!System.IO.File.Exists(test.GeneratedTestFilePath))
                    {
                        Debug.LogWarning($"[TestDatabase] O arquivo de teste '{test.GeneratedTestFilePath}' não foi encontrado. Removendo entrada órfã.");
                        AllTests.RemoveAt(i);
                        needsSave = true;
                        continue; // Pula o resto e vai para a próxima iteração
                    }

                    // 2. Agora é 100% seguro tentar carregar o arquivo (ele existe e a Unity já compilou!)
                    var scriptAsset = AssetDatabase.LoadAssetAtPath<MonoScript>(test.GeneratedTestFilePath);
                    
                    if (scriptAsset != null)
                    {
                        test.GeneratedTestScript = scriptAsset;
                        needsSave = true;
                    }
                }
            }

            // Salva o banco de dados apenas se alguma referência nova foi conectada
            if (needsSave)
            {
                EditorUtility.SetDirty(this);
                AssetDatabase.SaveAssets();
                Debug.Log("[TestDatabase] Referências de scripts recém-gerados foram conectadas com sucesso!");
            }
        }

        public bool HasTestForMethod(MonoScript targetScript, string method)
        {
            if (targetScript == null || string.IsNullOrEmpty(method)) return false;
            
            // Verifica se existe algum teste na lista para este script e este método
            return AllTests.Exists(t => t.TargetScript == targetScript && t.SutMethod == method);
        }
    }
}