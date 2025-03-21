using UnityEditor;
using UnityEngine;
using System;
using System.Threading.Tasks;

namespace Packages.LaundryNDishes
{
    public class LnDConfigSettings : SettingsProvider
    {
        private static LnDConfig _lnDConfig;
        private string connectionTestResult = "";
        private LLMRequestor llmRequestor = new LLMRequestor();

        public LnDConfigSettings(string path, SettingsScope scopes) : base(path, scopes) {}

        // The GUI rendering for the settings window
        public override void OnGUI(string searchContext)
        {
            // Try to load the configuration file when the settings window is opened
            if (_lnDConfig == null)
            {
                LoadConfig();
            }

            if (_lnDConfig == null)
            {
                EditorGUILayout.LabelField("Configuration not found", EditorStyles.boldLabel);
                return; // Exit if config is not loaded
            }

            EditorGUILayout.LabelField("LLM Configuration", EditorStyles.boldLabel);

            // Allow editing the server and API key fields
            _lnDConfig.llmServer = EditorGUILayout.TextField("Server URL", _lnDConfig.llmServer);
            _lnDConfig.llmApiKey = EditorGUILayout.TextField("API Key", _lnDConfig.llmApiKey);
            _lnDConfig.llmModel = EditorGUILayout.TextField("Model", _lnDConfig.llmModel);

            // Use a slider for temperature, but ensure it's only 2 decimal places
            _lnDConfig.llTemperature = Mathf.Round(EditorGUILayout.Slider("Temperature", (float)_lnDConfig.llTemperature, 0f, 1f) * 100f) / 100f;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Test Connection"))
            {
                TestConnection();
            }
            EditorGUILayout.LabelField(connectionTestResult, GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();

            // Save button to persist changes
            if (GUILayout.Button("Save"))
            {
                SaveConfig();
            }
        }

        private void TestConnection()
        {
            connectionTestResult = "Testing...";
            llmRequestor.MakeRequest("Test", response =>
            {
                if (response!= String.Empty)
                {
                    connectionTestResult = "OK";
                }
                else
                {
                    connectionTestResult = "Failed";
                }
            });
        }

        // Load the config from the Project Settings folder
        private void LoadConfig()
        {
            _lnDConfig = AssetDatabase.LoadAssetAtPath<LnDConfig>("Assets/Settings/LnDConfig.asset");

            // If the config doesn't exist, create a new one
            if (_lnDConfig == null)
            {
                _lnDConfig = ScriptableObject.CreateInstance<LnDConfig>();
                AssetDatabase.CreateAsset(_lnDConfig, "Assets/Settings/LnDConfig.asset");
                AssetDatabase.SaveAssets();
            }
        }

        // Save the config back to the file
        private void SaveConfig()
        {
            // Ensure the _lnDConfig asset is marked as dirty and saved
            EditorUtility.SetDirty(_lnDConfig);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Success", "Configuration saved!", "OK");
        }

        // Register the settings provider to appear in the Project Settings window
        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            // The path is what will appear in the Project Settings sidebar
            return new LnDConfigSettings("Project/Laundry Dishes Configuration", SettingsScope.Project);
        }
    }
}
