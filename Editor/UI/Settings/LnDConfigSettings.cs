using UnityEditor;
using UnityEngine;
using LaundryNDishes.Core;
using UnityEditorInternal;

namespace LaundryNDishes.UI
{
    class LnDConfigSettings : SettingsProvider
    {
        private static string _connectionTestResult = "Waiting for test...";
        private static bool _isTestingConnection = false;
        
        // Controla o estado aberto/fechado da caixa de configurações compartilháveis (Inicia aberto por padrão)
        private static bool _globalSettingsFoldout = true; 

        public LnDConfigSettings(string path, SettingsScope scopes) : base(path, scopes) { }

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            var provider = new LnDConfigSettings("Project/Laundry & Dishes", SettingsScope.Project);
            provider.keywords = new System.Collections.Generic.HashSet<string>(new[] { "LLM", "AI", "Test" });
            return provider;
        }

        public override void OnGUI(string searchContext)
        {
            var config = LnDConfig.instance;
            
            // --- TELEMETRIA E INFO DO SISTEMA ---
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
            // PASSO IMPORTANTE: Iniciamos a detecção de mudanças e o Undo ANTES do novo botão
            // para que a alternância do escopo também possa ser desfeita pelo Ctrl+Z.
            EditorGUI.BeginChangeCheck(); 
            Undo.RecordObject(config, "Modify Laundry & Dishes Settings");

            // --- CONFIGURAÇÃO DE ESCOPO E ARMAZENAMENTO ---
            EditorGUILayout.LabelField("Storage Scope Configuration", EditorStyles.boldLabel);
            
            // Checkbox: por padrão vem falso (ou seja, usa o comportamento Global compartilhado via EditorPrefs)
            config.UseProjectSettingsOnly = EditorGUILayout.ToggleLeft(
                new GUIContent("Isolate Settings to this Project Only", 
                "Se marcado, ignora o EditorPrefs global da máquina e salva tudo (inclusive chaves) estritamente no arquivo local ProjectSettings. asset."), 
                config.UseProjectSettingsOnly
            );

            // Exibe um HelpBox contextual informando ao desenvolvedor o que está acontecendo
            if (config.UseProjectSettingsOnly)
            {
                EditorGUILayout.HelpBox("Modo Isolado Ativo: As configurações contidas na caixa abaixo pertencem única e exclusivamente a este projeto.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("Modo Global Ativo: As configurações contidas na caixa abaixo são salvas no EditorPrefs e serão compartilhadas com qualquer outro projeto aberto nesta máquina.", MessageType.Warning);
            }

            EditorGUILayout.Space(10);

            // --- GRUPO COLAPSÁVEL DE CONFIGURAÇÕES (AFETADAS PELO ESCOPO GLOBAL/LOCAL) ---
            string foldoutTitle = $"Core Configuration Data (Current Scope: {(config.UseProjectSettingsOnly ? "Project Isolated" : "Machine Global")})";
            _globalSettingsFoldout = EditorGUILayout.Foldout(_globalSettingsFoldout, foldoutTitle, true);
            
            if (_globalSettingsFoldout)
            {
                // Inicia o container visual "box" que envelopa os campos afetados pelo escopo
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.Space(5);

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
                EditorGUILayout.Space(15);
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

                // --- CONFIGURAÇÕES DE GERAÇÃO ---
                EditorGUILayout.Space(15);
                EditorGUILayout.LabelField("Generation Settings", EditorStyles.boldLabel);
                config.Temperature = EditorGUILayout.Slider("Temperature", config.Temperature, 0f, 2f);
                config.MaxTokens = EditorGUILayout.IntField("Max Tokens", config.MaxTokens);
                config.MaxAttempts = EditorGUILayout.IntField("Max Attempts (Per Test)", config.MaxAttempts);
                config.MaxCorrections = EditorGUILayout.IntField("Max Corrections (Per Attempt)", config.MaxCorrections);
                config.ShowAllLLmComm = EditorGUILayout.Toggle("Show LLM Communications", config.ShowAllLLmComm);
                config.DefaultTearDown = EditorGUILayout.Toggle("Force Default TearDown", config.DefaultTearDown);

                EditorGUILayout.Space(5);
                EditorGUILayout.EndVertical(); // Fecha o container visual "box"
            }

            // ==========================================================================
            // CONFIGURAÇÕES ESTREITAMENTE LOCAIS (Sempre fora da caixa e nunca vão para o EditorPrefs)
            // ==========================================================================
            EditorGUILayout.Space(25);
            EditorGUILayout.LabelField("Assembly Configuration", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Arraste aqui os arquivos de Assembly Definition (.asmdef) do seu projeto.", MessageType.Info);

            config.MainProjectAssembly = EditorGUILayout.ObjectField("Project Assembly (Runtime)", config.MainProjectAssembly, typeof(AssemblyDefinitionAsset), false) as AssemblyDefinitionAsset;
            config.PlayModeTestAssembly = EditorGUILayout.ObjectField("Play Mode Tests Assembly", config.PlayModeTestAssembly, typeof(AssemblyDefinitionAsset), false) as AssemblyDefinitionAsset;
            config.EditorTestAssembly = EditorGUILayout.ObjectField("Editor Tests Assembly", config.EditorTestAssembly, typeof(AssemblyDefinitionAsset), false) as AssemblyDefinitionAsset;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField(new GUIContent("Custom Templates Folder (Optional)", "Deixe em branco para usar os templates padrão do plugin."));
            config.CustomTemplatesFolder = EditorGUILayout.TextField(" ", config.CustomTemplatesFolder);

            // Se mudou algo em qualquer parte da UI, salvamos as alterações
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
                var config = LnDConfig.instance;
                var llmService = config.GetCurrentService();

                var prompt = new Prompt();
                prompt.Messages.Add(new ChatMessage { role = "user", content = "This is a test do you hear me?" });

                var requestData = new LLMRequestData
                {
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