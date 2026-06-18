using System;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace LaundryNDishes.Core
{
    public enum LLMProviderType { OpenAIRestServer, LlamaCppDirect, UnitySentis }

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
        [SerializeField] private string llmServerUrl = "http://localhost:11434/v1/chat/completions";
        [SerializeField] private string llmModel = "gemma:2b";
        [SerializeField] private string llamaCppPath = "C:/path/to/llama.cpp/main.exe";
        [SerializeField] private string ggufModelFile = "C:/path/to/models/model.gguf";
        [SerializeField] private string onnxModelPath = "Assets/Models/my_llm.onnx";
        [SerializeField] private string tokenizerPath = "Assets/Models/tokenizer.json";
        [SerializeField] private float temperature = 0.7f;
        [SerializeField] private int maxTokens = 2048;
        
        // Estes campos abaixo SEMPRE ficam restritos apenas ao ProjectSettings (nunca vão para o EditorPrefs)
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

        public AssemblyDefinitionAsset MainProjectAssembly { get => mainProjectAssembly; set => mainProjectAssembly = value; }
        public AssemblyDefinitionAsset PlayModeTestAssembly { get => playModeTestAssembly; set => playModeTestAssembly = value; }
        public AssemblyDefinitionAsset EditorTestAssembly { get => editorTestAssembly; set => editorTestAssembly = value; }

        // --- Lógica Condicional para a API Key ---
        public string LlmApiKey
        {
            get
            {
                // Variável de ambiente continua com prioridade máxima se existir
                string envKey = Environment.GetEnvironmentVariable("UNITY_LLM_API_KEY");
                if (!string.IsNullOrEmpty(envKey)) return envKey;

                if (useProjectSettingsOnly)
                {
                    return llmApiKey; // Lê do arquivo .asset local deste projeto
                }
                else
                {
                    return EditorPrefs.GetString(GlobalPrefPrefix + "LlmApiKey", ""); // Lê do EditorPrefs Global da máquina
                }
            }
            set 
            {
                if (useProjectSettingsOnly)
                {
                    if (llmApiKey != value) llmApiKey = value;
                }
                else
                {
                    if (EditorPrefs.GetString(GlobalPrefPrefix + "LlmApiKey", "") != value)
                    {
                        EditorPrefs.SetString(GlobalPrefPrefix + "LlmApiKey", value);
                    }
                }
            }
        }

        public string PlayTestDestinationFolder => PlayModeTestAssembly != null ? Path.GetDirectoryName(AssetDatabase.GetAssetPath(PlayModeTestAssembly)) : string.Empty;
        public string EditorTestScriptsFolder => EditorTestAssembly != null ? Path.GetDirectoryName(AssetDatabase.GetAssetPath(EditorTestAssembly)) : string.Empty;

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
                isInitialized = true;
                Save(); 
            }
        }

        /// <summary>
        /// Carrega os dados globais do EditorPrefs para este projeto.
        /// </summary>
        private void LoadFromEditorPrefs()
        {
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
            
            // Conforme solicitado: customTemplatesFolder, assemblies e useProjectSettingsOnly NÃO são tocados aqui.
        }

        /// <summary>
        /// Salva as alterações. Sincroniza com o EditorPrefs apenas se a isolação estiver desativada.
        /// </summary>
        public void Save()
        {
            if (!useProjectSettingsOnly)
            {
                // Sincroniza e atualiza o perfil GLOBAL no EditorPrefs da máquina
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