using System;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace LaundryNDishes.Core
{
    public enum LLMProviderType { OpenAIRestServer, LlamaCppDirect/*, UnitySentis*/ }

    [FilePath("ProjectSettings/LaundryNDishesSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public class LnDConfig : ScriptableSingleton<LnDConfig>
    {
        // Prefixo GLOBAL (as configurações serão compartilhadas por todos os projetos do seu computador)
        private const string GlobalPrefPrefix = "LnD_Global_";

        [SerializeField] private bool isInitialized = false;

        // --- NOVA PROPRIEDADE DE ISOLAÇÃO ---
        [SerializeField] private bool useProjectSettingsOnly = false;

        // Campo para salvar a API Key no arquivo .asset CASO o EditorPrefs esteja desativado
        [SerializeField] private string llmApiKey = string.Empty;

        [SerializeField] private LLMProviderType providerType = LLMProviderType.OpenAIRestServer;
        [SerializeField] private string llmServerUrl = "";
        [SerializeField] private string llmModel = "";
        [SerializeField] private string llamaCppPath = "";
        [SerializeField] private string ggufModelFile = "";
        [SerializeField] private string onnxModelPath = "";
        [SerializeField] private string tokenizerPath = "";
        [SerializeField] private float temperature = 0.7f;
        [SerializeField] private int maxTokens = 2048;
        
        // Estes campos abaixo SEMPRE ficam restritos apenas ao ProjectSettings (nunca vão para o EditorPrefs)
        [SerializeField] private bool useAssemblyDef = true;
        [SerializeField] private DefaultAsset testFolderAsset;
        [SerializeField] private AssemblyDefinitionAsset mainProjectAssembly;
        [SerializeField] private AssemblyDefinitionAsset playModeTestAssembly;
        [SerializeField] private AssemblyDefinitionAsset editorTestAssembly;
        [SerializeField] private string customTemplatesFolder = string.Empty;
        
        [SerializeField] private TestDatabase activeDatabase;
        [SerializeField] private int maxCorrections = 5;
        [SerializeField] private int maxAttempts = 5;
        [SerializeField] private bool showAllLLmComm = true;
        [SerializeField] private bool defaultTearDown = true;

        // --- Propriedades Públicas ---
        public bool UseProjectSettingsOnly { get => useProjectSettingsOnly; set => useProjectSettingsOnly = value; }
        public LLMProviderType ProviderType { get => providerType; set => providerType = value; }
        public string LlmServerUrl { get => llmServerUrl; set => llmServerUrl = value; }
        public string LlmModel { get => llmModel; set => llmModel = value; }
        public string LlamaCppPath { get => llamaCppPath; set => llamaCppPath = value; }
        public string GgufModelFile { get => ggufModelFile; set => ggufModelFile = value; }
        public string OnnxModelPath { get => onnxModelPath; set => onnxModelPath = value; }
        public string TokenizerPath { get => tokenizerPath; set => tokenizerPath = value; }
        public float Temperature { get => temperature; set => temperature = value; }
        public int MaxTokens { get => maxTokens; set => maxTokens = value; }
        public int MaxCorrections { get => maxCorrections; set => maxCorrections = value; }
        public int MaxAttempts { get => maxAttempts; set => maxAttempts = value; }
        public bool ShowAllLLmComm { get => showAllLLmComm; set => showAllLLmComm = value; }
        public bool DefaultTearDown { get => defaultTearDown; set => defaultTearDown = value; }
        public string CustomTemplatesFolder { get => customTemplatesFolder; set => customTemplatesFolder = value; }
        public TestDatabase ActiveDatabase { get => activeDatabase; private set => activeDatabase = value; }
        public string InstallationId
        {
            get
            {
                string id = EditorPrefs.GetString(GlobalPrefPrefix + "InstallationId", "");

                if (string.IsNullOrEmpty(id))
                {
                    id = Guid.NewGuid().ToString("N");
                    EditorPrefs.SetString(GlobalPrefPrefix + "InstallationId", id);
                }

                return id;
            }
        }
        public bool TelemetryEnabled
        {
            get => EditorPrefs.GetBool(GlobalPrefPrefix + "TelemetryEnabled", true); // default ON
            set => EditorPrefs.SetBool(GlobalPrefPrefix + "TelemetryEnabled", value);
        }
        public bool BoostrapWizardShown
        {
            get => EditorPrefs.GetBool(GlobalPrefPrefix + "BoostrapWizardShown", false);
            set => EditorPrefs.SetBool(GlobalPrefPrefix + "BoostrapWizardShown", value);
        }

        public bool UseAssemblyDef { get => useAssemblyDef; set => useAssemblyDef = value; }
        public DefaultAsset TestFolderAsset
        {
            get => testFolderAsset;
            set => testFolderAsset = value; 
        }
        public AssemblyDefinitionAsset MainProjectAssembly { get => mainProjectAssembly; set => mainProjectAssembly = value; }
        public AssemblyDefinitionAsset PlayModeTestAssembly { get => playModeTestAssembly; set => playModeTestAssembly = value; }
        public AssemblyDefinitionAsset EditorTestAssembly { get => editorTestAssembly; set => editorTestAssembly = value; }
        
        public string LlmApiKey { get => llmApiKey; set => llmApiKey = value; }

        public string PlayTestDestinationFolder
        {
            get
            {
                if (useAssemblyDef)
                {
                    return PlayModeTestAssembly != null ? Path.GetDirectoryName(AssetDatabase.GetAssetPath(PlayModeTestAssembly)).Replace("\\", "/") : string.Empty;
                }
               
                // No modo Zero Setup, usa a própria pasta raiz selecionada pelo usuário
                return AssetDatabase.GetAssetPath(TestFolderAsset);
                
            }
        }

        public string EditorTestScriptsFolder
        {
            get
            {
                if (useAssemblyDef)
                {
                    return EditorTestAssembly != null ? Path.GetDirectoryName(AssetDatabase.GetAssetPath(EditorTestAssembly)).Replace("\\", "/") : string.Empty;
                }
                
                string rootPath = AssetDatabase.GetAssetPath(TestFolderAsset);
                return Path.Combine(rootPath, "Editor").Replace("\\", "/");
                
            }
        }

        // --- Ciclo de Vida Condicional ---
        private void OnEnable()
        {
            if (!isInitialized)
            {
                // Se for um projeto novo (arquivo .asset acabou de ser criado), ele nasce com 'useProjectSettingsOnly = false'.
                // Então ele vai herdar automaticamente a configuração global que você já configurou em outros projetos.
                if (!useProjectSettingsOnly)
                {
                    LoadFromEditorPrefs();
                }
                LoadFromEnvironmentVariables();
                isInitialized = true;
                Save(); 
            }
        }

        /// <summary>
        /// Carrega os dados globais do EditorPrefs para este projeto.
        /// </summary>
        private void LoadFromEditorPrefs()
        {
            llmApiKey = EditorPrefs.GetString(GlobalPrefPrefix + "LlmApiKey", llmApiKey);
            providerType = (LLMProviderType)EditorPrefs.GetInt(GlobalPrefPrefix + "ProviderType", (int)providerType);
            llmServerUrl = EditorPrefs.GetString(GlobalPrefPrefix + "LlmServerUrl", llmServerUrl);
            llmModel = EditorPrefs.GetString(GlobalPrefPrefix + "LlmModel", llmModel);
            llamaCppPath = EditorPrefs.GetString(GlobalPrefPrefix + "LlamaCppPath", llamaCppPath);
            ggufModelFile = EditorPrefs.GetString(GlobalPrefPrefix + "GgufModelFile", ggufModelFile);
            onnxModelPath = EditorPrefs.GetString(GlobalPrefPrefix + "OnnxModelPath", onnxModelPath);
            tokenizerPath = EditorPrefs.GetString(GlobalPrefPrefix + "TokenizerPath", tokenizerPath);
            temperature = EditorPrefs.GetFloat(GlobalPrefPrefix + "Temperature", temperature);
            maxTokens = EditorPrefs.GetInt(GlobalPrefPrefix + "MaxTokens", maxTokens);
            maxCorrections = EditorPrefs.GetInt(GlobalPrefPrefix + "MaxCorrections", maxCorrections);
            maxAttempts = EditorPrefs.GetInt(GlobalPrefPrefix + "MaxAttempts", maxAttempts);
            showAllLLmComm = EditorPrefs.GetBool(GlobalPrefPrefix + "ShowAllLLmComm", showAllLLmComm);
            defaultTearDown = EditorPrefs.GetBool(GlobalPrefPrefix + "DefaultTearDown", defaultTearDown);
        }
        
        /// <summary>
        /// Verifica a flag LnD_use_env e injeta as variáveis do sistema se disponíveis.
        /// </summary>
        private void LoadFromEnvironmentVariables()
        {
            // Verifica o "master switch" na variável de ambiente do Sistema Operacional
            string useEnvStr = Environment.GetEnvironmentVariable("LnD_use_env");
            
            // Se não estiver definida ou for "false", ignora o carregamento por ambiente
            if (string.IsNullOrEmpty(useEnvStr) || useEnvStr.ToLower() == "false" || useEnvStr == "0")
            {
                return;
            }
            
            
            llmApiKey = Environment.GetEnvironmentVariable("LlmApiKey") ?? llmApiKey;
            llmServerUrl = Environment.GetEnvironmentVariable("LlmServerUrl") ?? llmServerUrl;
            llmModel = Environment.GetEnvironmentVariable("LlmModel") ?? llmModel;
            llamaCppPath = Environment.GetEnvironmentVariable("LlamaCppPath") ?? llamaCppPath;
            ggufModelFile = Environment.GetEnvironmentVariable("GgufModelFile") ?? ggufModelFile;
            onnxModelPath = Environment.GetEnvironmentVariable("OnnxModelPath") ?? onnxModelPath;
            tokenizerPath = Environment.GetEnvironmentVariable("TokenizerPath") ?? tokenizerPath;

            // Tratamento para o Enum (ProviderType)
            string envProvider = Environment.GetEnvironmentVariable("ProviderType");
            if (!string.IsNullOrEmpty(envProvider) && Enum.TryParse(envProvider, true, out LLMProviderType parsedProvider))
            {
                providerType = parsedProvider;
            }

            // Tratamento para floats e inteiros
            string envTemp = Environment.GetEnvironmentVariable("Temperature");
            if (!string.IsNullOrEmpty(envTemp) && float.TryParse(envTemp, out float parsedTemp))
                temperature = parsedTemp;

            string envMaxTokens = Environment.GetEnvironmentVariable("MaxTokens");
            if (!string.IsNullOrEmpty(envMaxTokens) && int.TryParse(envMaxTokens, out int parsedTokens))
                maxTokens = parsedTokens;

            string envMaxCorr = Environment.GetEnvironmentVariable("MaxCorrections");
            if (!string.IsNullOrEmpty(envMaxCorr) && int.TryParse(envMaxCorr, out int parsedCorr))
                maxCorrections = parsedCorr;

            string envMaxAtt = Environment.GetEnvironmentVariable("MaxAttempts");
            if (!string.IsNullOrEmpty(envMaxAtt) && int.TryParse(envMaxAtt, out int parsedAtt))
                maxAttempts = parsedAtt;

            // Tratamento para booleanos
            string envShowComm = Environment.GetEnvironmentVariable("ShowAllLLmComm");
            if (!string.IsNullOrEmpty(envShowComm) && bool.TryParse(envShowComm, out bool parsedShowComm))
                showAllLLmComm = parsedShowComm;

            string envTearDown = Environment.GetEnvironmentVariable("DefaultTearDown");
            if (!string.IsNullOrEmpty(envTearDown) && bool.TryParse(envTearDown, out bool parsedTearDown))
                defaultTearDown = parsedTearDown;
        }

        /// <summary>
        /// Salva as alterações. Sincroniza com o EditorPrefs apenas se a isolação estiver desativada.
        /// </summary>
        public void Save()
        {
            if (!useProjectSettingsOnly)
            {
                // Sincroniza e atualiza o perfil GLOBAL no EditorPrefs da máquina
                EditorPrefs.SetString(GlobalPrefPrefix + "LlmApiKey", llmApiKey);
                EditorPrefs.SetInt(GlobalPrefPrefix + "ProviderType", (int)providerType);
                EditorPrefs.SetString(GlobalPrefPrefix + "LlmServerUrl", llmServerUrl);
                EditorPrefs.SetString(GlobalPrefPrefix + "LlmModel", llmModel);
                EditorPrefs.SetString(GlobalPrefPrefix + "LlamaCppPath", llamaCppPath);
                EditorPrefs.SetString(GlobalPrefPrefix + "GgufModelFile", ggufModelFile);
                EditorPrefs.SetString(GlobalPrefPrefix + "OnnxModelPath", onnxModelPath);
                EditorPrefs.SetString(GlobalPrefPrefix + "TokenizerPath", tokenizerPath);
                EditorPrefs.SetFloat(GlobalPrefPrefix + "Temperature", temperature);
                EditorPrefs.SetInt(GlobalPrefPrefix + "MaxTokens", maxTokens);
                EditorPrefs.SetInt(GlobalPrefPrefix + "MaxCorrections", maxCorrections);
                EditorPrefs.SetInt(GlobalPrefPrefix + "MaxAttempts", maxAttempts);
                EditorPrefs.SetBool(GlobalPrefPrefix + "ShowAllLLmComm", showAllLLmComm);
                EditorPrefs.SetBool(GlobalPrefPrefix + "DefaultTearDown", defaultTearDown);
                
                // Nota: se mudar de 'true' para 'false', o estado atual deste projeto vira o novo padrão global.
            }

            // Sempre salva localmente no arquivo ProjectSettings/LaundryNDishesSettings.asset
            EditorUtility.SetDirty(this);
            Save(true); 
        }

        public ILLMService GetCurrentService()
        {
            switch (instance.ProviderType)
            {
                case LLMProviderType.OpenAIRestServer: return new OpenAIRestService();
                case LLMProviderType.LlamaCppDirect: return new LlamaCppDirectService();
                default: throw new ArgumentOutOfRangeException(nameof(instance.ProviderType));
            }
        }

        public void SetActiveDatabase(TestDatabase database)
        {
            activeDatabase = database;
        }
    }
}