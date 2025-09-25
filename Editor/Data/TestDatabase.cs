using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LaundryNDishes.Data
{
// Podemos usar CreateAssetMenu aqui para facilitar a criação inicial, se quisermos.
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

                // Tenta encontrar o asset no projeto
                string[] guids = AssetDatabase.FindAssets("t:TestDatabase");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    _instance = AssetDatabase.LoadAssetAtPath<TestDatabase>(path);
                }
                else
                {
                    // Se não encontrar, cria um novo
                    _instance = CreateInstance<TestDatabase>();

                    string path = "Assets/Editor/Data"; // Caminho padrão
                    if (!Directory.Exists(path))
                    {
                        Directory.CreateDirectory(path);
                    }

                    AssetDatabase.CreateAsset(_instance, $"{path}/TestDatabase.asset");
                    AssetDatabase.SaveAssets();
                    Debug.Log("TestDatabase created at " + path);
                }

                return _instance;
            }
        }
    }
}