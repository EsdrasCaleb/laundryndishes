using UnityEditor;
using UnityEngine;
using LaundryNDishes.Data;
using LaundryNDishes.UnityCore;
using LaundryNDishes.Core;
using UnityEditorInternal;

namespace LaundryNDishes.UI
{
    class LnDConfigSettings : SettingsProvider
    {
        private static string _connectionTestResult = "Waiting for test...";
        private static bool _isTestingConnection = false;

        public LnDConfigSettings(string path, SettingsScope scopes) : base(path, scopes) {}

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            var provider = new LnDConfigSettings("Project/Laundry & Dishes", SettingsScope.Project);
            provider.keywords = new System.Collections.Generic.HashSet<string>(new[] { "LLM", "AI", "Test" });
            return provider;
        }

        public override void OnGUI(string searchContext)
        {
            // MUDANÇA AQUI: Pegando a instância estendida do ScriptableSingleton da Unity
            var config = LnDConfig.instance;
            
            // --- INSERÇÃO DA TELEMETRIA ANÔNIMA ---
            EditorGUILayout.LabelField("Telemetry & System Info", EditorStyles.boldLabel);
            
            string unityId = CloudProjectSettings.userId;
            string orgId = CloudProjectSettings.organizationId;
            string devId = SystemInfo.deviceUniqueIdentifier;

            var seedBuilder = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(unityId)) seedBuilder.Append(unityId);
            if (!string.IsNullOrEmpty(orgId)) seedBuilder.Append(orgId);
            
            if (!string.IsNullOrEmpty(devId) && devId != "n/a" && !devId.Contains("00000000"))
            {
                seedBuilder.Append(devId);
            }
            else if (seedBuilder.Length == 0) 
            {
                seedBuilder.Append(System.Environment.MachineName).Append(System.Environment.UserName);
            }

            string shortHash;
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(seedBuilder.ToString()));
                shortHash = System.BitConverter.ToString(hashBytes).Replace("-", "").Substring(0, 8).ToUpper();
            }

            GUI.enabled = false; 
            EditorGUILayout.TextField("Anonymized Developer ID", shortHash);
            GUI.enabled = true;  
            EditorGUILayout.Space(10);
            // --------------------------------------------------------------------------

            EditorGUI.BeginChangeCheck(); // Inicia a detecção de mudanças na UI.
            
            // Registra o estado atual para habilitar o sistema de Undo da Unity e marcar o arquivo como modificado
            Undo.RecordObject(config, "Modify Laundry & Dishes Settings");
            Undo.RecordObject(LnDUserConfig.instance, "Modify Laundry & Dishes User Settings");

            // --- SELEÇÃO DO BANCO DE DADOS ATIVO ---
            EditorGUILayout.LabelField("Database Settings", EditorStyles.boldLabel);
            var newDatabase = EditorGUILayout.ObjectField("Active Test Database", config.ActiveDatabase, typeof(TestDatabase), false) as TestDatabase;
            if (newDatabase != config.ActiveDatabase)
            {
                config.SetActiveDatabase(newDatabase);
            }
            
            if (config.ActiveDatabase == null)
            {
                 EditorGUILayout.HelpBox("Nenhum banco de dados selecionado.", MessageType.Warning);
                 if (GUILayout.Button("Create New Database"))
                 {
                     Bootstrap.CreateNewDatabase();
                 }
            }

            // --- CONFIGURAÇÕES DO LLM ---
            EditorGUILayout.Space(20);
            EditorGUILayout.LabelField("LLM Settings", EditorStyles.boldLabel);
            config.ProviderType = (LLMProviderType)EditorGUILayout.EnumPopup("Connection Type", config.ProviderType);

            switch (config.ProviderType)
            {
                case LLMProviderType.OpenAIRestServer:
                    config.LlmServerUrl = EditorGUILayout.TextField("Server URL", config.LlmServerUrl);
                    config.LlmModel = EditorGUILayout.TextField("Model Name", config.LlmModel);
                    config.LlmApiKey = EditorGUILayout.PasswordField("API Key", config.LlmApiKey);
                    break;
                
                case LLMProviderType.UnitySentis:
                    config.OnnxModelPath = EditorGUILayout.TextField("ONNX Model Path", config.OnnxModelPath);
                    config.TokenizerPath = EditorGUILayout.TextField("Tokenizer Path", config.TokenizerPath);
                    break;

                case LLMProviderType.LlamaCppDirect:
                    EditorGUILayout.HelpBox("Direct Llama.cpp connection is not yet implemented.", MessageType.Info);
                    GUI.enabled = false;
                    config.LlamaCppPath = EditorGUILayout.TextField("Llama.cpp Executable Path", config.LlamaCppPath);
                    config.GgufModelFile = EditorGUILayout.TextField("GGUF Model File Path", config.GgufModelFile);
                    GUI.enabled = true;
                    break;
            }
            
            if (config.ProviderType == LLMProviderType.OpenAIRestServer)
            {
                GUI.enabled = !_isTestingConnection;
                if (GUILayout.Button("Test Connection")) { TestConnection(); }
                GUI.enabled = true;
                EditorGUILayout.HelpBox(_connectionTestResult, MessageType.None);
            }
            
            EditorGUILayout.Space(20);
            EditorGUILayout.LabelField("Generation Settings", EditorStyles.boldLabel);
            config.Temperature = EditorGUILayout.Slider("Temperature", config.Temperature, 0f, 2f);
            config.MaxTokens = EditorGUILayout.IntField("Max Tokens", config.MaxTokens);
            config.MaxAttempts = EditorGUILayout.IntField("Max Attempts (Per Test)", config.MaxAttempts);
            config.MaxCorrections = EditorGUILayout.IntField("Max Corrections (Per Attempt)", config.MaxCorrections);
            config.ShowAllLLmComm = EditorGUILayout.Toggle("Show LLM Communications", config.ShowAllLLmComm);
            config.DefaultTearDown = EditorGUILayout.Toggle("Force Default TearDown", config.DefaultTearDown); // Corrigido a atribuição que estava errada no seu código original (salvava em ShowAllLLmComm de novo)
            
            EditorGUILayout.Space(20);
            EditorGUILayout.LabelField("Assembly Configuration", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Arraste aqui os arquivos de Assembly Definition (.asmdef) do seu projeto.", MessageType.Info);

            config.MainProjectAssembly = EditorGUILayout.ObjectField("Project Assembly (Runtime)", config.MainProjectAssembly, typeof(AssemblyDefinitionAsset), false) as AssemblyDefinitionAsset;
            config.PlayModeTestAssembly = EditorGUILayout.ObjectField("Play Mode Tests Assembly", config.PlayModeTestAssembly, typeof(AssemblyDefinitionAsset), false) as AssemblyDefinitionAsset;
            config.EditorTestAssembly = EditorGUILayout.ObjectField("Editor Tests Assembly", config.EditorTestAssembly, typeof(AssemblyDefinitionAsset), false) as AssemblyDefinitionAsset;
            
            EditorGUILayout.LabelField(new GUIContent("Custom Templates Folder (Optional)", "Deixe em branco para usar os templates padrão do plugin."));
            config.CustomTemplatesFolder = EditorGUILayout.TextField(" ", config.CustomTemplatesFolder);

            // Se mudou algo na UI, persistimos a alteração no arquivo físico do ProjectSettings
            if (EditorGUI.EndChangeCheck())
            {
                config.Save();
            }
        }
        
        private async void TestConnection()
        {
            _isTestingConnection = true;
            _connectionTestResult = "Testing...";
            try
            {
                // MUDANÇA AQUI: Atualizado para usar o padrão .instance
                var config = LnDConfig.instance;
                var llmService = config.GetCurrentService();
                
                var prompt = new Prompt();
                prompt.Messages.Add(new ChatMessage { role = "user", content = "This is a test do you hear me?" });
                
                var requestData = new LLMRequestData {
                    GeneratedPrompt = prompt,
                    Config = config
                };
                
                LLMResponse response = await llmService.GetResponseAsync(requestData);
                _connectionTestResult = response.Success ? $"OK - Success! Response: '{response.Content}'" : $"Failed. Error: {response.ErrorMessage}";
            }
            catch (System.Exception ex) { _connectionTestResult = $"Failed. Exception: {ex.Message}"; }
            finally { _isTestingConnection = false; }
        }
    }
}