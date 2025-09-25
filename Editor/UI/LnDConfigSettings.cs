using UnityEditor;
using UnityEngine;
using LaundryNDishes.Data;
using LaundryNDishes.Services; // Namespace da Factory

namespace LaundryNDishes.UI
{
    class LnDConfigSettings : SettingsProvider
    {
        private LnDConfig _config;
        private static string _connectionTestResult = "Waiting for test...";
        private static bool _isTestingConnection = false;

        public LnDConfigSettings(string path, SettingsScope scopes) : base(path, scopes) {}

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            var provider = new LnDConfigSettings("Project/Laundry & Dishes", SettingsScope.Project) {
                keywords = new System.Collections.Generic.HashSet<string>(new[] { "LLM", "AI", "Test" })
            };
            return provider;
        }

        public override void OnActivate(string searchContext, UnityEngine.UIElements.VisualElement rootElement)
        {
            _config = LnDConfig.Load(); // Carrega a config ao abrir a janela.
        }

        public override void OnGUI(string searchContext)
        {
            if (_config == null) _config = LnDConfig.Load();

            EditorGUI.BeginChangeCheck();

            _config.ProviderType = (LLMProviderType)EditorGUILayout.EnumPopup("Connection Type", _config.ProviderType);
            EditorGUILayout.Space(10);

            if (_config.ProviderType == LLMProviderType.OpenAIRestServer)
            {
                DrawServerSettings();
            }
            else // LlamaCppDirect
            {
                DrawDirectSettings();
            }

            DrawGenerationSettings();
            DrawPathSettings();

            if (EditorGUI.EndChangeCheck())
            {
                _config.Save(); // A UI apenas diz para a config se salvar.
            }
        }

        private void DrawServerSettings()
        {
            EditorGUILayout.LabelField("OpenAI REST Server Settings", EditorStyles.boldLabel);
            _config.LlmServerUrl = EditorGUILayout.TextField("Server URL", _config.LlmServerUrl);
            _config.LlmModel = EditorGUILayout.TextField("Model Name", _config.LlmModel);
            _config.LlmApiKey = EditorGUILayout.PasswordField("API Key", _config.LlmApiKey);

            EditorGUILayout.Space(5);
            GUI.enabled = !_isTestingConnection;
            if (GUILayout.Button("Test Connection"))
            {
                TestConnection();
            }
            GUI.enabled = true;
            EditorGUILayout.HelpBox(_connectionTestResult, MessageType.None);
        }
        
        private void DrawDirectSettings()
        {
            EditorGUILayout.LabelField("Direct Llama.cpp Settings", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Direct connection is not yet implemented.", MessageType.Info);
            GUI.enabled = false;
            _config.LlamaCppPath = EditorGUILayout.TextField("Llama.cpp Executable Path", _config.LlamaCppPath);
            _config.GgufModelFile = EditorGUILayout.TextField("GGUF Model File Path", _config.GgufModelFile);
            GUI.enabled = true;
        }

        private void DrawGenerationSettings()
        {
            EditorGUILayout.Space(20);
            EditorGUILayout.LabelField("Generation Settings", EditorStyles.boldLabel);
            _config.Temperature = EditorGUILayout.Slider("Temperature", _config.Temperature, 0f, 2f);
            _config.MaxTokens = EditorGUILayout.IntField("Max Tokens", _config.MaxTokens);
        }

        private void DrawPathSettings()
        {
            EditorGUILayout.Space(20);
            EditorGUILayout.LabelField("Project Path Settings", EditorStyles.boldLabel);
            _config.TestDestinationFolder = EditorGUILayout.TextField("Test Destination Folder", _config.TestDestinationFolder);
            _config.TestableScriptsFolder = EditorGUILayout.TextField("Testable Scripts Folder", _config.TestableScriptsFolder);
        }
        
        private async void TestConnection()
        {
            _isTestingConnection = true;
            _connectionTestResult = "Testing...";
            try
            {
                // A Factory agora sabe qual serviço usar, não precisamos dizer a ela!
                ILLMService llmService = LLMServiceFactory.GetCurrentService();
                var requestData = new LLMRequestData {
                    Prompt = "Say 'hello' in one word.",
                    Config = _config // Passa a config atual (com os dados da UI) para o request.
                };
                LLMResponse response = await llmService.GetResponseAsync(requestData);
                _connectionTestResult = response.Success ? $"OK - Success! Response: '{response.Content.Trim()}'" : $"Failed. Error: {response.ErrorMessage}";
            }
            catch (System.Exception ex) { _connectionTestResult = $"Failed. Exception: {ex.Message}"; }
            finally { _isTestingConnection = false; }
        }
    }
}