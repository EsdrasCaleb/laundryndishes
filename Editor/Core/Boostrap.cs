using UnityEditor;
using UnityEngine;
using LaundryNDishes.Data;
using System.IO;

namespace LaundryNDishes.Core
{
    [InitializeOnLoad]
    public static class Bootstrap
    {
        static Bootstrap()
        {
            EditorApplication.delayCall += CheckConfiguration;
        }

        private static void CheckConfiguration()
        {
            // Acessa a instância única. Se a config não existir, ela é criada aqui.
            var config = LnDConfig.Instance;

            // CASO 1: Ideal. A config já aponta para um banco de dados válido.
            if (config.ActiveDatabase != null) return;

            string[] databaseGUIDs = AssetDatabase.FindAssets("t:TestDatabase");

            // CASO 4: Primeira Execução.
            if (databaseGUIDs.Length == 0)
            {
                if (EditorUtility.DisplayDialog("Configuração Inicial", "Nenhuma base de dados de testes foi encontrada. Deseja criar uma agora?", "Sim", "Não"))
                {
                    CreateNewDatabase();
                }
                return;
            }
            
            if (databaseGUIDs.Length == 1)
            {
                if (EditorUtility.DisplayDialog("Configuração - Laundry & Dishes", "Encontramos uma base de dados de testes, mas ela não está configurada como ativa. Deseja usá-la?", "Sim, usar esta", "Não"))
                {
                    string path = AssetDatabase.GUIDToAssetPath(databaseGUIDs[0]);
                    var db = AssetDatabase.LoadAssetAtPath<TestDatabase>(path);
                    LnDConfig.Instance.SetActiveDatabase(db);
                    Debug.Log($"Laundry & Dishes: Base de dados '{db.name}' configurada como ativa.");
                }
            }
            else // Mais de um banco de dados encontrado
            {
                if (EditorUtility.DisplayDialog("Configuração - Laundry & Dishes", $"Encontramos {databaseGUIDs.Length} bases de dados de testes, mas nenhuma está ativa. Deseja ir para a tela de configurações para escolher uma?", "Sim", "Não"))
                {
                    SettingsService.OpenProjectSettings("Project/Laundry & Dishes");
                }
            }
        }

        public static void CreateNewDatabase()
        {
            string path = EditorUtility.SaveFilePanelInProject("Criar Nova Base de Dados", "TestDatabase", "asset", "Selecione um local.", "Assets/");
            if (string.IsNullOrEmpty(path)) return;

            var newDb = ScriptableObject.CreateInstance<TestDatabase>();
            AssetDatabase.CreateAsset(newDb, path);
            AssetDatabase.SaveAssets();
            
            // Seta o DB na instância da config e salva.
            var config = LnDConfig.Instance;
            config.SetActiveDatabase(newDb);
            config.Save();
            
            Debug.Log($"Base de dados criada e configurada em: {path}");
        }
    }
}