using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            LnDConfig config = LnDConfig.instance;

            
            // Regra 2: Se já foi configurado/selecionado antes, pula direto para a segunda aba (FoldersAssemblies)
            if (config.TelemetryEnabled && config.BoostrapWizardShown)
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
                string guid = AssetDatabase.FindAssets("t:TestDatabase").FirstOrDefault();
                
                if (!string.IsNullOrEmpty(guid))
                {
                    AssociateAvaibleDatabase(config);
                }
            }
        }
        
        
        private void AssociateAvaibleDatabase(LnDConfig config)
        {
            string guid = AssetDatabase.FindAssets("t:TestDatabase").FirstOrDefault();
                            
            if (!string.IsNullOrEmpty(guid))
            {
                // 2. Converte o GUID no caminho do arquivo (ex: "Assets/...')
                string path = AssetDatabase.GUIDToAssetPath(guid);
                
                // 3. Carrega o asset original usando o tipo correto
                var testDb = AssetDatabase.LoadAssetAtPath<LaundryNDishes.Core.TestDatabase>(path);
                
                // 4. Passa o objeto correto para o método
                config.SetActiveDatabase(testDb);
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


            AutoCreateMissingAssemblies(config);
            

            string guid = AssetDatabase.FindAssets("t:TestDatabase").FirstOrDefault();
                
            if (string.IsNullOrEmpty(guid))
            {
                CreateAndAssignNewDatabase(config);
            }
            else
            {
                AssociateAvaibleDatabase(config);
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
            Repaint();
            AssociateAvaibleDatabase(config);
        }
        
        private void AutoCreateMissingAssemblies(LnDConfig config)
        {
            // 1. Garante ou define os caminhos das pastas de teste padrão
            string playPath = string.IsNullOrEmpty(config.PlayTestDestinationFolder) ? "Assets/LnDTests" : config.PlayTestDestinationFolder;
            string editPath = string.IsNullOrEmpty(config.EditorTestScriptsFolder) ? playPath+"/Editor" : config.EditorTestScriptsFolder;

            if (!Directory.Exists(playPath)) Directory.CreateDirectory(playPath);
            if (!Directory.Exists(editPath)) Directory.CreateDirectory(editPath);

            // 2. Resolve o Assembly principal do Jogo
            string mainAsmName = "";
            if (config.MainProjectAssembly != null)
            {
                mainAsmName = config.MainProjectAssembly.name; 
            }
            else
            {
                string rawProjectName = PlayerSettings.productName;

                // // Limpa o nome removendo espaços e caracteres especiais para garantir um nome de assembly válido
                string baseAsmName = System.Text.RegularExpressions.Regex.Replace(rawProjectName, @"[^a-zA-Z0-9_]", "");
                if (string.IsNullOrEmpty(baseAsmName))
                {
                    baseAsmName = "GameMain";
                }
                CreateUniqueGameAssembly(config,baseAsmName);
            }

            // 3. Cria os asmdefs de teste apontando para o assembly do jogo
            CreateNewTestAsmdef(playPath, false, mainAsmName);
            CreateNewTestAsmdef(editPath, true, mainAsmName);

            AssetDatabase.Refresh();
        }
        
        private string CreateUniqueGameAssembly(LnDConfig config, string baseAsmName="GameMain")
        {
            // 1. Resgata os nomes dos assemblies de teste direto das configurações salvos no Unity
            string playAsmName = config.PlayModeTestAssembly != null ? config.PlayModeTestAssembly.name : "";
            string editAsmName = config.EditorTestAssembly != null ? config.EditorTestAssembly.name : "";

            // Coleta TODOS os assemblies existentes e registrados no ecossistema da Unity
            var allAsmGuids = AssetDatabase.FindAssets("t:AssemblyDefinitionAsset");
            HashSet<string> existingAsmNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> referencesList = new List<string>();

            foreach (var guid in allAsmGuids)
            {
                string asmPath = AssetDatabase.GUIDToAssetPath(guid);
                string asmName = Path.GetFileNameWithoutExtension(asmPath);
                
                if (!string.IsNullOrEmpty(asmName))
                {
                    // Guarda o nome interno do Assembly para a validação de duplicatas logo abaixo
                    existingAsmNames.Add(asmName);
                    
                    // CHECAGEM DINÂMICA: Ignora os assemblies de teste configurados para evitar referência circular
                    if ((!string.IsNullOrEmpty(playAsmName) && asmName.Equals(playAsmName, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrEmpty(editAsmName) && asmName.Equals(editAsmName, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue; 
                    }

                    // Se for um assembly de terceiros/plugins válido, adiciona como referência do jogo
                    referencesList.Add($"\"{asmName}\"");
                }
            }

            // 2. VALIDAÇÃO DE DUPLICIDADE (O que você pontuou):
            // O loop começa testando o próprio 'baseAsmName' enviado. Se ele já existir nas pastas (File.Exists) 
            // OU se o nome interno dele já estiver registrado na Unity (existingAsmNames.Contains), ele aplica o N+1.
            string uniqueName = baseAsmName;
            string rootAsmPath = $"Assets/{uniqueName}.asmdef";
            int counter = 1;

            while (File.Exists(rootAsmPath) || existingAsmNames.Contains(uniqueName))
            {
                uniqueName = $"{baseAsmName}{counter}";
                rootAsmPath = $"Assets/{uniqueName}.asmdef";
                counter++;
            }

            // 3. Monta o JSON final do Assembly principal
            string gameReferencesJson = referencesList.Count > 0 ? string.Join(",\n        ", referencesList) : "";
            
            string rootAsmContent = $@"{{
            ""name"": ""{uniqueName}"",
            ""references"": [
                {gameReferencesJson}
            ],
            ""includePlatforms"": [],
            ""excludePlatforms"": []
        }}";

            // 4. Cria o arquivo físico na raiz do projeto
            File.WriteAllText(rootAsmPath, rootAsmContent);

            return uniqueName;
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