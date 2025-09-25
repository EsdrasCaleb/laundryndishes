using UnityEditor;
using UnityEngine;
using LaundryNDishes.Data;
using LaundryNDishes.Services;
using LaundryNDishes.Core;

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
            // Pega a instância única da configuração. A mágica do singleton acontece aqui.
            var config = LnDConfig.Instance;

            EditorGUI.BeginChangeCheck(); // Inicia a detecção de mudanças na UI.

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
            EditorGUILayout.LabelField("LLM Settings (Local to this Machine)", EditorStyles.boldLabel);
            config.ProviderType = (LLMProviderType)EditorGUILayout.EnumPopup("Connection Type", config.ProviderType);

            // UI Condicional
            switch (config.ProviderType)
            {
                case LLMProviderType.OpenAIRestServer:
                    config.LlmServerUrl = EditorGUILayout.TextField("Server URL", config.LlmServerUrl);
                    config.LlmModel = EditorGUILayout.TextField("Model Name", config.LlmModel);
                    config.LlmApiKey = EditorGUILayout.PasswordField("API Key", config.LlmApiKey);
                    // Adicione aqui os outros campos que desejar para este modo
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
            
            // Botão de teste para o modo Servidor
            if (config.ProviderType == LLMProviderType.OpenAIRestServer)
            {
                GUI.enabled = !_isTestingConnection;
                if (GUILayout.Button("Test Connection")) { TestConnection(); }
                GUI.enabled = true;
                EditorGUILayout.HelpBox(_connectionTestResult, MessageType.None);
            }

            // --- CONFIGURAÇÕES GERAIS ---
            EditorGUILayout.Space(20);
            EditorGUILayout.LabelField("Generation Settings", EditorStyles.boldLabel);
            config.Temperature = EditorGUILayout.Slider("Temperature", config.Temperature, 0f, 2f);
            config.MaxTokens = EditorGUILayout.IntField("Max Tokens", config.MaxTokens);
            
            EditorGUILayout.Space(20);
            EditorGUILayout.LabelField("Project Path Settings", EditorStyles.boldLabel);
            config.PlayTestDestinationFolder = EditorGUILayout.TextField("Playmode Test Destination Folder", config.PlayTestDestinationFolder);
            //config.EditorTestScriptsFolder = EditorGUILayout.TextField("Editor Test Destination Folder", config.EditorTestScriptsFolder);
            EditorGUILayout.LabelField(new GUIContent("Custom Templates Folder (Optional)", "Deixe em branco para usar os templates padrão do plugin."));
            config.CustomTemplatesFolder = EditorGUILayout.TextField(" ", config.CustomTemplatesFolder);
            // Se qualquer valor na UI mudou, o EndChangeCheck será true.
            if (EditorGUI.EndChangeCheck())
            {
                // A UI simplesmente pede para a instância da config se salvar.
                config.Save();
            }
        }
        
        private async void TestConnection()
        {
            _isTestingConnection = true;
            _connectionTestResult = "Testing...";
            try
            {
                var config = LnDConfig.Instance;
                var llmService = LLMServiceFactory.GetCurrentService();
                
                var prompt = new Prompt();
                prompt.Messages.Add(new ChatMessage { role = "user", content = "Say 'hello' in one word." });
                
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