using System;
using System.Threading;
using UnityEditor;
using UnityEngine;
using LaundryNDishes.Core;

namespace LaundryNDishes.UI
{
    class LnDConfigSettings : SettingsProvider
    {
        private static string _connectionTestResult = "Waiting for test...";
        private static bool _isTestingConnection = false;
        
        // Controls the open/closed state of the shareable settings box
        private static bool _globalSettingsFoldout = true; 

        public LnDConfigSettings(string path, SettingsScope scopes) : base(path, scopes) { }

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            var provider = new LnDConfigSettings("Project/Laundry & Dishes", SettingsScope.Project);
            provider.keywords = new System.Collections.Generic.HashSet<string>(new[] { "LLM", "AI", "Test", "Wizard" });
            return provider;
        }

        public override void OnGUI(string searchContext)
        {
            var config = LnDConfig.instance;
            
            EditorGUILayout.Space(5);
            
            // --- SETUP WIZARD BUTTON ---
            GUI.backgroundColor = new Color(0.2f, 0.6f, 1f); // Azul sutil
            if (GUILayout.Button("Open Setup Wizard", GUILayout.Height(30)))
            {
                LnDSetupWizard.ShowWindow();
            }
            GUI.backgroundColor = Color.white; // Reseta a cor
            
            EditorGUILayout.Space(15);

            // --- TELEMETRY & SYSTEM INFO ---
            EditorGUILayout.LabelField("Telemetry & System Info", EditorStyles.boldLabel);
            bool previous = config.TelemetryEnabled;

            bool current = EditorGUILayout.Toggle(
                "Send anonymised telemetry usage data",
                previous
            );

            if (previous != current)
            {
                config.TelemetryEnabled = current;
                OnTelemetryChanged(previous, current);
            }
            GUI.enabled = false;
            EditorGUILayout.TextField("Installation ID", LnDConfig.instance.InstallationId);
            GUI.enabled = true;
            EditorGUILayout.Space(10);
            
            // --------------------------------------------------------------------------
            // Start change detection and Undo tracking
            EditorGUI.BeginChangeCheck(); 
            Undo.RecordObject(config, "Modify Laundry & Dishes Settings");

            // --- STORAGE SCOPE CONFIGURATION ---
            EditorGUILayout.LabelField("Storage Scope Configuration", EditorStyles.boldLabel);
            
            config.UseProjectSettingsOnly = EditorGUILayout.ToggleLeft(
                new GUIContent("Isolate Settings to this Project Only", 
                "If checked, ignores the global machine EditorPrefs and saves everything (including API keys) strictly to the local ProjectSettings.asset file."), 
                config.UseProjectSettingsOnly
            );

            if (config.UseProjectSettingsOnly)
            {
                EditorGUILayout.HelpBox("Isolated Mode Active: The settings contained in the box below belong solely and exclusively to this project.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("Global Mode Active: The settings contained in the box below are saved in EditorPrefs and will be shared with any other project opened on this machine.", MessageType.Warning);
            }

            EditorGUILayout.Space(10);

            // --- FOLDABLE GROUP FOR CORE CONFIG (AFFECTED BY SCOPE) ---
            string foldoutTitle = $"Core Configuration Data (Current Scope: {(config.UseProjectSettingsOnly ? "Project Isolated" : "Machine Global")})";
            _globalSettingsFoldout = EditorGUILayout.Foldout(_globalSettingsFoldout, foldoutTitle, true);
            
            if (_globalSettingsFoldout)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.Space(5);

                // --- ACTIVE DATABASE SELECTION ---
                EditorGUILayout.LabelField("Database Settings", EditorStyles.boldLabel);
                var newDatabase = EditorGUILayout.ObjectField("Active Test Database", config.ActiveDatabase, typeof(TestDatabase), false) as TestDatabase;
                if (newDatabase != config.ActiveDatabase)
                {
                    config.SetActiveDatabase(newDatabase);
                }

                if (config.ActiveDatabase == null)
                {
                    EditorGUILayout.HelpBox("No test database selected. Please assign one above or generate a new one via the Setup Wizard.", MessageType.Warning);
                }

                // --- LLM SETTINGS ---
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
                    case LLMProviderType.LlamaCppDirect:
                        EditorGUILayout.HelpBox("Direct Llama.cpp execution runs locally on your machine.", MessageType.Info);
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

                // --- GENERATION SETTINGS ---
                EditorGUILayout.Space(15);
                EditorGUILayout.LabelField("Generation Settings", EditorStyles.boldLabel);
                config.Temperature = EditorGUILayout.Slider("Temperature", config.Temperature, 0f, 2f);
                config.MaxTokens = EditorGUILayout.IntField("Max Tokens", config.MaxTokens);
                config.MaxAttempts = EditorGUILayout.IntField("Max Attempts (Per Test)", config.MaxAttempts);
                config.MaxCorrections = EditorGUILayout.IntField("Max Corrections (Per Attempt)", config.MaxCorrections);
                config.ShowAllLLmComm = EditorGUILayout.Toggle("Show LLM Communications", config.ShowAllLLmComm);
                config.DefaultTearDown = EditorGUILayout.Toggle("Force Default TearDown", config.DefaultTearDown);

                EditorGUILayout.Space(5);
                EditorGUILayout.EndVertical(); 
            }

            // ==========================================================================
            // STRICTLY LOCAL CONFIGURATIONS
            // ==========================================================================
            EditorGUILayout.Space(25);
            EditorGUILayout.LabelField("Test & Assembly Configuration", EditorStyles.boldLabel);
            
            EditorGUILayout.HelpBox("Drag or Select the Assembly Definition (.asmdef) files from your project here.\n", MessageType.Info);

            config.MainProjectAssembly = EditorGUILayout.ObjectField("Project Assembly (Runtime)", config.MainProjectAssembly, typeof(UnityEditorInternal.AssemblyDefinitionAsset), false) as UnityEditorInternal.AssemblyDefinitionAsset;
            config.PlayModeTestAssembly = EditorGUILayout.ObjectField("Play Mode Tests Assembly", config.PlayModeTestAssembly, typeof(UnityEditorInternal.AssemblyDefinitionAsset), false) as UnityEditorInternal.AssemblyDefinitionAsset;
            config.EditorTestAssembly = EditorGUILayout.ObjectField("Editor Tests Assembly", config.EditorTestAssembly, typeof(UnityEditorInternal.AssemblyDefinitionAsset), false) as UnityEditorInternal.AssemblyDefinitionAsset;
           

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField(new GUIContent("Custom Templates Folder (Optional)", "Leave blank to use the plugin's default templates."));
       
            config.CustomTemplatesFolder = EditorGUILayout.ObjectField(
                "Template Folder", 
                config.CustomTemplatesFolder, 
                typeof(DefaultAsset), 
                false
            ) as DefaultAsset;

            if (EditorGUI.EndChangeCheck())
            {
                config.Save();
            }
        }

        private static void OnTelemetryChanged(bool previous, bool current)
        {
            if (current && !previous)
            {
                TelemetryEnableFlow();
            }
            else if (!current && previous)
            {
                TelemetryDisableFlow();
            }
        }
        
        private static void TelemetryEnableFlow()
        {
            var pending = TelemetryQueue.GetPending();
            var old = TelemetryQueue.GetReenableData();

            if (old.Count > 0)
            {
                bool includeOld = EditorUtility.DisplayDialog(
                    "Telemetry Enabled",
                    "Do you want to send previously stored anonymous telemetry data?",
                    "Send old data",
                    "Only future data"
                );

                if (includeOld)
                {
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        TelemetrySender.SendBatch(old);
                        TelemetryQueue.MarkAsSent(old);
                    });
                }
            }
        }
        
        private static void TelemetryDisableFlow()
        {
            bool confirmDelete = EditorUtility.DisplayDialog(
                "Telemetry Disabled",
                "Do you want to request deletion of your telemetry data on the server?",
                "Delete the data that was sent",
                "You can keep it"
            );

            if (!confirmDelete)
                return;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                bool success = TelemetrySender.RequestDelete(LnDConfig.instance.InstallationId);

                if (success)
                {
                    TelemetryQueue.MarkAllAsDoNotSend();

                    TelemetrySender.Send(new TelemetryEvent
                    {
                        installationId = LnDConfig.instance.InstallationId,
                        action = "TelemetryDeleted",
                        unityVersion = Application.unityVersion,
                        pluginVersion = "1.0.0",
                        timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    });
                }
            });
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
                prompt.Messages.Add(new ChatMessage { role = "user", content = "This is a test. Do you hear me?" });

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