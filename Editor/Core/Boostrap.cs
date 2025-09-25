using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using LaundryNDishes.Data;
using System.IO;
using System.Linq;

namespace LaundryNDishes.Core
{
    [InitializeOnLoad]
    public static class Bootstrap
    {
        static Bootstrap()
        {
            EditorApplication.delayCall += RunStartupChecks;
        }

        private static void RunStartupChecks()
        {
            // Primeiro, checa a configuração do banco de dados.
            CheckDatabaseConfiguration();
            
            // Em seguida, garante que o ambiente de teste está configurado.
            EnsureTestAssemblyExists();
        }

        private static void CheckDatabaseConfiguration()
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
        
        /// <summary>
        /// Garante que a pasta de destino dos testes exista e contenha um Assembly Definition de teste.
        /// </summary>
        public static void EnsureTestAssemblyExists()
        {
            var config = LnDConfig.Instance;
            string testFolderPath = config.PlayTestDestinationFolder;

            // --- CASO 1: A pasta de testes não existe. ---
            if (!Directory.Exists(testFolderPath))
            {
                Debug.Log($"[Laundry & Dishes] Pasta de testes '{testFolderPath}' não encontrada. Criando...");
                Directory.CreateDirectory(testFolderPath);
                CreateNewTestAsmdef(testFolderPath);
                AssetDatabase.Refresh();
                return;
            }

            // A pasta existe, vamos verificar o .asmdef.
            string[] asmdefGuids = AssetDatabase.FindAssets("t:AssemblyDefinitionAsset", new[] { testFolderPath });

            // --- CASO 2: A pasta existe, mas não tem .asmdef. ---
            if (asmdefGuids.Length == 0)
            {
                Debug.Log($"[Laundry & Dishes] A pasta de testes '{testFolderPath}' não tem um Assembly Definition. Criando...");
                CreateNewTestAsmdef(testFolderPath);
                AssetDatabase.Refresh();
                return;
            }

            // --- CASO 3: A pasta existe e tem um .asmdef. Precisamos verificar se é de teste. ---
            string asmdefPath = AssetDatabase.GUIDToAssetPath(asmdefGuids[0]);
            var asmdefData = JsonUtility.FromJson<AsmdefData>(File.ReadAllText(asmdefPath));

            // A verificação mais confiável para um assembly de teste é a presença do "defineConstraint" UNITY_INCLUDE_TESTS.
            bool isTestAssembly = (asmdefData.defineConstraints != null && 
                                  asmdefData.defineConstraints.Contains("UNITY_INCLUDE_TESTS"))||(
                                  asmdefData.optionalUnityReferences != null && 
                                  asmdefData.optionalUnityReferences.Contains("TestAssemblies"));

            if (!isTestAssembly)
            {
                // Apenas emitimos o aviso, como você sugeriu.
                Debug.LogWarning(
                    $"[Laundry & Dishes] O Assembly Definition em '{asmdefPath}' não está configurado como um assembly de teste.\n" +
                    $"Para corrigir, selecione o arquivo e marque a opção 'Test Assemblies' no Inspector, ou apague o arquivo para que o plugin possa criar um novo automaticamente."
                );
            }
        }

        /// <summary>
        /// Cria um novo arquivo .asmdef configurado para testes na pasta especificada.
        /// </summary>
        private static void CreateNewTestAsmdef(string folderPath)
        {
            string baseName = Path.GetFileName(folderPath);
            string desiredAssemblyName = baseName.Equals("Tests", System.StringComparison.OrdinalIgnoreCase) 
                ? baseName 
                : $"{baseName}.Tests";

            // --- 2. Lógica para garantir um nome de assembly único no projeto ---
            var allAssemblyNames = GetAllAssemblyNames();
            string finalAssemblyName = desiredAssemblyName;
            int counter = 1;
            while (allAssemblyNames.Contains(finalAssemblyName))
            {
                finalAssemblyName = $"{desiredAssemblyName}_{counter++}";
            }

            if (finalAssemblyName != desiredAssemblyName)
            {
                Debug.LogWarning($"[Laundry & Dishes] O nome de assembly '{desiredAssemblyName}' já existia. Usando '{finalAssemblyName}' para evitar conflitos.");
            }
            
            // Template JSON para um .asmdef de teste perfeito.
            string asmdefContent = $@"
{{
    ""name"": ""{finalAssemblyName}"",
    ""rootNamespace"": """",
    ""references"": [],
    ""includePlatforms"": [],
    ""excludePlatforms"": [],
    ""allowUnsafeCode"": false,
    ""overrideReferences"": true,
    ""precompiledReferences"": [],
    ""autoReferenced"": false,
    ""defineConstraints"": [],
    ""versionDefines"": [],
    ""noEngineReferences"": false,
    ""optionalUnityReferences"": [
        ""TestAssemblies""
    ]
}}
";
            string filePath = Path.Combine(folderPath, $"{finalAssemblyName}.asmdef");
            File.WriteAllText(filePath, asmdefContent);
        }
        
        private static HashSet<string> GetAllAssemblyNames()
        {
            var assemblyNames = new HashSet<string>();
            string[] asmdefGuids = AssetDatabase.FindAssets("t:AssemblyDefinitionAsset");

            foreach (var guid in asmdefGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string json = File.ReadAllText(path);
                var asmdefData = JsonUtility.FromJson<AsmdefData>(json);
                if (asmdefData != null && !string.IsNullOrEmpty(asmdefData.name))
                {
                    assemblyNames.Add(asmdefData.name);
                }
            }
            return assemblyNames;
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