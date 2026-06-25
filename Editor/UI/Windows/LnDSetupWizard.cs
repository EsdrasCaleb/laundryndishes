using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using LaundryNDishes.Core;
using UnityEditorInternal;


namespace LaundryNDishes.UI
{
    public class LnDSetupWizard : EditorWindow
    {
        private enum SetupStep { WelcomeTerms, FoldersAssemblies, DatabaseConfig, ModelConfig, Complete }
        private SetupStep currentStep = SetupStep.WelcomeTerms;

        // UI Control flags
        private string[] foundDbGuids;
        private bool isDownloadingDependencies = false;
        private LnDDownloader lndDownloader;

        [MenuItem("Window/Laundry & Dishes/Setup Wizard")]
        public static void ShowWindow()
        {
            var window = GetWindow<LnDSetupWizard>(true, "Laundry & Dishes - Setup Wizard", true);
            window.minSize = new Vector2(500, 100);
            window.maxSize = new Vector2(700, 920);
            window.Show();
        }

        private void OnEnable()
        {
            foundDbGuids = AssetDatabase.FindAssets("t:TestDatabase");
            
            // Regra 2: Se já foi configurado/selecionado antes, pula direto para a segunda aba (FoldersAssemblies)
            if (LnDConfig.instance.TelemetryEnabled)
            {
                currentStep = SetupStep.FoldersAssemblies;
            }
            else
            {
                currentStep = SetupStep.WelcomeTerms;
            }

            lndDownloader = new LnDDownloader();
            lndDownloader.OnProgressUpdated += Repaint;
        }

        private void OnDisable()
        {
            if (lndDownloader != null)
            {
                lndDownloader.OnProgressUpdated -= Repaint;
            }
        }

        private void OnGUI()
        {
            DrawHeaderTabs();
            EditorGUILayout.Space(15);

            // Renderiza o conteúdo com base na aba atual
            switch (currentStep)
            {
                case SetupStep.WelcomeTerms: DrawWelcomeTermsStep(); break;
                case SetupStep.FoldersAssemblies: DrawFoldersAssembliesStep(); break;
                case SetupStep.DatabaseConfig: DrawDatabaseStep(); break;
                case SetupStep.ModelConfig: DrawModelConfigStep(); break;
                case SetupStep.Complete: DrawCompleteStep(); break;
            }

            EditorGUILayout.Space(20);
            DrawFooter();
        }

        #region Step Rendering

        // Regra 1: Substituído barra de progresso rígida por um sistema de Abas Navegáveis
        private void DrawHeaderTabs()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Laundry & Dishes — Setup Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            string[] tabNames = { "1. Telemetry", "2. Architecture", "3. Database", "4. Model Config", "5. Complete" };
            
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < tabNames.Length; i++)
            {
                bool isCurrent = ((int)currentStep == i);
                
                // Regra 2: Bloqueia as outras abas se a escolha de telemetria ainda não foi confirmada
                bool isTabEnabled = LnDConfig.instance.BoostrapWizardShown || i == 0;

                EditorGUI.BeginDisabledGroup(!isTabEnabled);
                
                Color originalColor = GUI.backgroundColor;
                if (isCurrent) 
                {
                    // Destaca visualmente a aba ativa
                    GUI.backgroundColor = new Color(0.35f, 0.55f, 0.8f); 
                }

                if (GUILayout.Button(tabNames[i], GUILayout.Height(28)))
                {
                    currentStep = (SetupStep)i;
                    if (currentStep == SetupStep.DatabaseConfig) foundDbGuids = AssetDatabase.FindAssets("t:TestDatabase");
                }
                
                GUI.backgroundColor = originalColor;
                EditorGUI.EndDisabledGroup();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawWelcomeTermsStep()
        {
            var config = LnDConfig.instance;

            EditorGUILayout.LabelField("Telemetry & Research Data", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "Laundry & Dishes is being evaluated as part of an academic research study.\n\n" +
                "Anonymous telemetry helps us understand how the plugin is used in real-world scenarios.\n\n" +
                "Data collected:\n" +
                "• Unity version | Plugin version\n" +
                "• Feature usage (Test Generation / CLI)\n" +
                "• Test Execution outcome\n" +
                "• Anonymous identifiers for the User\n\n" +
                "Source code, prompts, personal data or file paths are NEVER collected.",
                MessageType.Info
            );

            EditorGUILayout.Space(10);

            // Regra 2: Mudança para Checkbox para deixar claro se está ativo ou não
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.Space(5);
            string[] togleOptions = new string[] { "Yes", "No" };
            using (var check = new EditorGUI.ChangeCheckScope())
            {
                // Fazemos o cast do enum atual para int (já que seus valores são 0 e 1)
                int currentEnumIndex = config.TelemetryEnabled?0:1;

                int newIndex = EditorGUILayout.Popup("Send Anonymous Data?", currentEnumIndex, togleOptions);
        
                // 2. Se o usuário mudou a seleção no menu, aplicamos e salvamos imediatamente
                if (check.changed)
                {
                    config.TelemetryEnabled = newIndex==0;
                    config.BoostrapWizardShown = true;
                    config.Save(); // Garante que a mudança persiste no projeto
                }
            }
            
            EditorGUILayout.Space(5);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);
            

            // Fluxo Condicional de Setup Rápido (Apenas se aceitar telemetria)
            if (config.BoostrapWizardShown)
            {
                EditorGUILayout.Space(15);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("⚡ Quick Setup (Recommended)", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Set up your entire testing environment automatically:\n" +
                                           "• Disables Assembly Definitions (Zero Setup Mode)\n" +
                                           "• Creates a default Test Database at your root folder\n" +
                                           "• Prefills cloud configurations for Codestral (Mistral AI)", EditorStyles.wordWrappedLabel);
                
                EditorGUILayout.Space(10);
                if (GUILayout.Button("Execute Quick Setup", GUILayout.Height(35)))
                {
                    ExecuteQuickSetup();
                }
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawFoldersAssembliesStep()
        {
            var config = LnDConfig.instance;

            EditorGUILayout.LabelField("Test Architecture Setup", EditorStyles.boldLabel);
            config.UseAssemblyDef = EditorGUILayout.Toggle("Use Assembly Definitions (.asmdef)", config.UseAssemblyDef);
            EditorGUILayout.Space(10);

            if (config.UseAssemblyDef)
            {
                EditorGUILayout.HelpBox("Advanced Mode: Link your project's specific tracking assemblies.", MessageType.Info);
                config.MainProjectAssembly = EditorGUILayout.ObjectField("Project Assembly (Runtime)", config.MainProjectAssembly, typeof(AssemblyDefinitionAsset), false) as AssemblyDefinitionAsset;
                config.PlayModeTestAssembly = EditorGUILayout.ObjectField("Play Mode Tests Assembly", config.PlayModeTestAssembly, typeof(AssemblyDefinitionAsset), false) as AssemblyDefinitionAsset;
                config.EditorTestAssembly = EditorGUILayout.ObjectField("Editor Tests Assembly", config.EditorTestAssembly, typeof(AssemblyDefinitionAsset), false) as AssemblyDefinitionAsset;

                EditorGUILayout.Space(10);
                
                // Regra 3: Desabilita o botão se ambas as assemblies de teste já estiverem preenchidas
                bool assembliesFilled = (config.PlayModeTestAssembly != null && config.EditorTestAssembly != null);
                
                EditorGUI.BeginDisabledGroup(assembliesFilled);
                if (GUILayout.Button("Auto-Create Missing Test Assemblies", GUILayout.Height(25)))
                {
                    AutoCreateMissingAssemblies(config);
                }
                EditorGUI.EndDisabledGroup();
            }
            else
            {
                EditorGUILayout.HelpBox("Zero Setup Mode: Tests will be generated inside the global folder below.", MessageType.Info);
                
                // Regra 4: Substituído o seletor genérico por um seletor de arquivos de sistema (File/Folder Browser)
                EditorGUILayout.BeginHorizontal();
                string currentPath = config.TestFolderAsset != null ? AssetDatabase.GetAssetPath(config.TestFolderAsset) : "No Folder Selected";
                EditorGUILayout.TextField("Root Test Folder Path", currentPath);
                
                if (GUILayout.Button("Browse...", GUILayout.Width(70)))
                {
                    string selectedPath = EditorUtility.OpenFolderPanel("Select Root Test Folder", "Assets", "");
                    if (!string.IsNullOrEmpty(selectedPath))
                    {
                        // Converte o caminho absoluto em caminho relativo interno do projeto Unity
                        if (selectedPath.StartsWith(Application.dataPath))
                        {
                            string relativePath = "Assets" + selectedPath.Substring(Application.dataPath.Length);
                            var folderAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(relativePath);
                            if (folderAsset != null)
                            {
                                config.TestFolderAsset = folderAsset;
                                config.Save();
                            }
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("Invalid Folder", "Please select a folder inside your Unity Project's Assets directory.", "OK");
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawDatabaseStep()
        {
            var config = LnDConfig.instance;
            EditorGUILayout.LabelField("Test Database Configuration", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("The plugin requires an active runtime database asset to track metadata.");
            EditorGUILayout.Space(10);

            // Regra 5: Transforma o seletor em um Object Field do tipo específico 'TestDatabase'
            var currentDb = config.ActiveDatabase;
            var selectedDb = EditorGUILayout.ObjectField("Active Database Target", currentDb, typeof(TestDatabase), false) as TestDatabase;
            
            if (selectedDb != currentDb)
            {
                config.SetActiveDatabase(selectedDb);
                config.Save();
            }

            EditorGUILayout.Space(15);

            // Regra 5: O botão de criação só aparece dinamicamente se NÃO houver um asset válido selecionado
            if (config.ActiveDatabase == null)
            {
                EditorGUILayout.HelpBox("No active Test Database linked. Create a new asset to resolve tracking initialization.", MessageType.Warning);
                if (GUILayout.Button("✨ Create & Activate New Test Database", GUILayout.Height(35)))
                {
                    CreateAndAssignNewDatabase(config);
                }
            }
            else
            {
                EditorGUILayout.HelpBox($"Ready! Active tracking assigned to asset: '{config.ActiveDatabase.name}'", MessageType.Info);
            }
        }

        private void DrawModelConfigStep()
        {
            var config = LnDConfig.instance;
            EditorGUILayout.LabelField("AI Model / LLM Provider Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            
            // Regra 6: Modificado de GUILayout.Toolbar para um seletor Dropdown / Popup
            string[] providerOptions = new string[] { "Remote (OpenAI API)", "Local Llama.cpp" };
            EditorGUI.BeginDisabledGroup(lndDownloader.IsDownloadingAny);
            using (var check = new EditorGUI.ChangeCheckScope())
            {
                // Fazemos o cast do enum atual para int (já que seus valores são 0 e 1)
                int currentEnumIndex = (int)config.ProviderType;

                int newIndex = EditorGUILayout.Popup("LLM Provider Engine", currentEnumIndex, providerOptions);
        
                // 2. Se o usuário mudou a seleção no menu, aplicamos e salvamos imediatamente
                if (check.changed)
                {
                    config.ProviderType = (LLMProviderType)newIndex;
                    config.Save(); // Garante que a mudança persiste no projeto
                }
            }
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.Space(10);

            if (LnDConfig.instance.ProviderType  == LLMProviderType.OpenAIRestServer)
            {
                config.ProviderType = LLMProviderType.OpenAIRestServer;
                EditorGUILayout.HelpBox("Powered by Codestral: Mistral AI's flagship model tailored explicitly for code tasks.", MessageType.Info);
                
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("🔑 Authentication Setup Steps:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("1. Access your account on the Mistral Platform.\n" +
                                           "2. Navigate to: Studio ➔ Vibe CLI.\n" +
                                           "3. Copy the token and paste it into the field below.", EditorStyles.wordWrappedLabel);
                EditorGUILayout.Space(5);
                if (GUILayout.Button("🔗 Go to Mistral Console", GUILayout.Width(200))) Help.BrowseURL("https://console.mistral.ai/codestral/cli");
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.Space(10);
                config.LlmApiKey = EditorGUILayout.TextField("API Key (Mistral Key)", config.LlmApiKey);
                config.LlmServerUrl = EditorGUILayout.TextField("Endpoint URL", "https://api.mistral.ai/v1/chat/completions");
                config.LlmModel = EditorGUILayout.TextField("Model Identifier", "codestral-latest");
            }
            else
            {
                config.ProviderType = LLMProviderType.LlamaCppDirect;
                EditorGUILayout.HelpBox("Runs locally on your host environment. Zero API token expenses, works offline completely.", MessageType.Info);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Automated Model Downloads", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Fetches the specific Llama.cpp executable architecture along with Qwen 3.5 4B model.");
                EditorGUILayout.Space(10);
                
                if (!lndDownloader.IsDownloadingAny)
                {
                    bool hasLlama = lndDownloader.HasValidLlamaCpp();
                    bool hasModel = lndDownloader.HasValidModel();

                    if (!hasLlama || !hasModel)
                    {
                        if (GUILayout.Button("📥 Initialize Downloads via LnDDownloader", GUILayout.Height(35)))
                        {
                            ExecuteLocalSetupDownloads();
                        }
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("🎉 Perfect! Valid Llama.cpp binaries and GGUF model paths detected locally.", MessageType.Info);
                    }

                    if (hasLlama || hasModel)
                    {
                        EditorGUILayout.Space(5);
                        EditorGUILayout.BeginHorizontal();
                        if (hasLlama && GUILayout.Button("🗑️ Delete Llama.cpp Cache"))
                        {
                            if (EditorUtility.DisplayDialog("Delete Binaries", "Are you sure?", "Yes", "No")) lndDownloader.DeleteLlamaCpp();
                        }
                        if (hasModel && GUILayout.Button("🗑️ Delete GGUF Model Cache"))
                        {
                            if (EditorUtility.DisplayDialog("Delete Model", "Are you sure?", "Yes", "No")) lndDownloader.DeleteModel();
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                }
                else
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField($"Status: {lndDownloader.StatusMessage}", EditorStyles.miniBoldLabel);
                    EditorGUILayout.Space(4);

                    Rect rLlama = EditorGUILayout.GetControlRect(false, 16);
                    EditorGUI.ProgressBar(rLlama, lndDownloader.LlamaProgress, $"Llama.cpp Backend Core: {(lndDownloader.LlamaProgress * 100f):F0}%");
                    EditorGUILayout.Space(5);

                    Rect rModel = EditorGUILayout.GetControlRect(false, 16);
                    EditorGUI.ProgressBar(rModel, lndDownloader.ModelProgress, $"Super Coder Model (GGUF): {(lndDownloader.ModelProgress * 100f):F0}%");
                    
                    EditorGUILayout.Space(8);
                    GUI.backgroundColor = new Color(0.9f, 0.35f, 0.35f);
                    if (GUILayout.Button("🛑 Cancel Download Session", GUILayout.Height(26))) lndDownloader.Cancel();
                    GUI.backgroundColor = Color.white;

                    EditorGUILayout.EndVertical();
                }
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(10);
                EditorGUI.BeginDisabledGroup(lndDownloader.IsDownloadingAny);
                config.LlamaCppPath = EditorGUILayout.TextField("Llama.cpp Binary Path", config.LlamaCppPath);
                config.GgufModelFile = EditorGUILayout.TextField("Model File Path (.gguf)", config.GgufModelFile);
                EditorGUI.EndDisabledGroup();
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Hyperparameters", EditorStyles.boldLabel);
            config.Temperature = EditorGUILayout.Slider("Temperature", config.Temperature, 0f, 1.5f);
            config.MaxTokens = EditorGUILayout.IntField("Max Generation Tokens", config.MaxTokens);
        }

        private async void ExecuteLocalSetupDownloads()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Confirm Local Environment Download",
                "You are about to download the Llama.cpp compilation toolchain and the Qwen model.\n\n" +
                "⚠️ Combined size is approximately ~2.5 GB.\n\nDo you wish to start?",
                "Yes", "Cancel"
            );

            if (!proceed) return;

            var config = LnDConfig.instance;
            lndDownloader.StartSession();

            try
            {
                Task<string> llamaTask = lndDownloader.DownloadLlamaCppAsync();
                Task<string> modelTask = lndDownloader.DownloadModelAsync();

                string finalLlamaPath = await llamaTask;
                string finalModelPath = await modelTask;

                config.LlamaCppPath = finalLlamaPath;
                config.GgufModelFile = finalModelPath;
                config.Save();
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning("[LnD Downloader] Configuration aborted by user.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[LnD Downloader Error] Failed: {e.Message}");
                EditorUtility.DisplayDialog("Download Failed", e.Message, "Ok");
            }
        }

        private void DrawCompleteStep()
        {
            EditorGUILayout.LabelField("🎉 Complete Checkout & Summary", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Everything is ready! Review your environment configurations below.", MessageType.Info);
            
            EditorGUILayout.Space(15);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Environment Summary Overview:", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField($"• Provider Engine: {LnDConfig.instance.ProviderType}");
            EditorGUILayout.LabelField($"• Model Target: {LnDConfig.instance.LlmModel}");
            EditorGUILayout.LabelField($"• Isolation Strategy: {(LnDConfig.instance.UseAssemblyDef ? "Assembly Definition" : "Zero Setup (Global Workspace)")}");
            EditorGUILayout.LabelField($"• Tracking Database: {(LnDConfig.instance.ActiveDatabase != null ? LnDConfig.instance.ActiveDatabase.name : "Unassigned")}");
            EditorGUILayout.EndVertical();
        }

        #endregion

        #region Navigation Footer

        private void DrawFooter()
        {
            LnDConfig config = LnDConfig.instance;
            EditorGUILayout.BeginHorizontal();

            // Botão Back (Oculto se for na aba inicial)
            EditorGUI.BeginDisabledGroup(currentStep == SetupStep.WelcomeTerms);
            if (GUILayout.Button("Back", GUILayout.Width(100), GUILayout.Height(30)))
            {
                currentStep--;
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.FlexibleSpace();

            if (currentStep == SetupStep.Complete)
            {
                if (GUILayout.Button("Finish Setup", GUILayout.Width(130), GUILayout.Height(30)))
                {
                    config.Save();
                    Close();
                }
            }
            else
            {
                // Impede avançar se estiver baixando dependências locais
                bool canAdvance = true;
                if (currentStep == SetupStep.ModelConfig && lndDownloader.IsDownloadingAny) canAdvance = false;
                if (!config && currentStep == SetupStep.WelcomeTerms) canAdvance = false;

                EditorGUI.BeginDisabledGroup(!canAdvance);
                if (GUILayout.Button("Next", GUILayout.Width(130), GUILayout.Height(30)))
                {
                    currentStep++;
                    if (currentStep == SetupStep.DatabaseConfig) foundDbGuids = AssetDatabase.FindAssets("t:TestDatabase");
                }
                EditorGUI.EndDisabledGroup();
            }

            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Telemetry Custom Flows & Utilities

        private void TelemetryEnableFlow() => Debug.Log("[LnD Telemetry] Opt-in confirmed.");
        private void TelemetryDisableFlow() => Debug.Log("[LnD Telemetry] Opt-out confirmed.");

        private void ExecuteQuickSetup()
        {
            var config = LnDConfig.instance;
            
            config.UseAssemblyDef = false;
            
            string rootPath = "Assets";
            if (config.TestFolderAsset != null)
            {
                rootPath = AssetDatabase.GetAssetPath(config.TestFolderAsset);
            }
            else
            {
                string standardFolder = "Assets/LnDTests";
                if (!AssetDatabase.IsValidFolder(standardFolder)) AssetDatabase.CreateFolder("Assets", "LnDTests");
                rootPath = standardFolder;
            }

            string dbPath = Path.Combine(rootPath, "TestDatabase.asset").Replace("\\", "/");
            if (AssetDatabase.LoadAssetAtPath<TestDatabase>(dbPath) == null)
            {
                var newDb = ScriptableObject.CreateInstance<TestDatabase>();
                AssetDatabase.CreateAsset(newDb, dbPath);
                AssetDatabase.SaveAssets();
                config.SetActiveDatabase(newDb);
            }
            else
            {
                config.SetActiveDatabase(AssetDatabase.LoadAssetAtPath<TestDatabase>(dbPath));
            }

            config.ProviderType = LLMProviderType.OpenAIRestServer;
            config.LlmServerUrl = "https://api.mistral.ai/v1/chat/completions";
            config.LlmModel = "codestral-latest";
            config.Save();

            Help.BrowseURL("https://console.mistral.ai/");

            currentStep = SetupStep.ModelConfig;
        }

        private void CreateAndAssignNewDatabase(LnDConfig config)
        {
            string path = EditorUtility.SaveFilePanelInProject("Create New Test DataBase", "TestDatabase", "asset", "Select a path", "Assets/");
            if (string.IsNullOrEmpty(path)) return;

            var newDb = ScriptableObject.CreateInstance<TestDatabase>();
            AssetDatabase.CreateAsset(newDb, path);
            AssetDatabase.SaveAssets();
            
            // Seta o DB na instância da config e salva.
            config.SetActiveDatabase(newDb);
            config.Save();
            foundDbGuids = AssetDatabase.FindAssets("t:TestDatabase");
            Repaint();
        }
        
        private void AutoCreateMissingAssemblies(LnDConfig config)
        {
            string playPath = config.PlayTestDestinationFolder;
            string editPath = config.EditorTestScriptsFolder;

            // Get the name of the main project assembly if it is assigned
            string mainAsmName = "";
            if (config.MainProjectAssembly != null)
            {
                mainAsmName = config.MainProjectAssembly.name; 
            }

            if (!string.IsNullOrEmpty(playPath) && !Directory.Exists(playPath))
            {
                Directory.CreateDirectory(playPath);
                CreateNewTestAsmdef(playPath, false, mainAsmName);
            }
            if (!string.IsNullOrEmpty(editPath) && !Directory.Exists(editPath))
            {
                Directory.CreateDirectory(editPath);
                CreateNewTestAsmdef(editPath, true, mainAsmName);
            }
            AssetDatabase.Refresh();
        }

        private void CreateNewTestAsmdef(string folderPath, bool isEditor, string mainAsmName)
        {
            string baseName = Path.GetFileName(folderPath);
            string finalName = baseName.Equals("Tests", StringComparison.OrdinalIgnoreCase) ? "LnD.Tests" : $"{baseName}.Tests";
            if (isEditor) finalName += ".Editor";

            string includePlatforms = isEditor ? "\"Editor\"" : "";
    
            // Construct references dynamically
            string referencesJson = "";
            if (!string.IsNullOrEmpty(mainAsmName))
            {
                // If a main assembly exists, reference it so tests can compile against your production code
                referencesJson = $",\n    \"references\": [\n        \"{mainAsmName}\"\n    ]";
            }

            string asmdefContent = $@"{{
    ""name"": ""{finalName}"",
    ""optionalUnityReferences"": [
        ""TestAssemblies""
    ],
    ""includePlatforms"": [
        {includePlatforms}
    ]{referencesJson}
}}";

            File.WriteAllText(Path.Combine(folderPath, $"{finalName}.asmdef"), asmdefContent);
        }

        #endregion
    }
}