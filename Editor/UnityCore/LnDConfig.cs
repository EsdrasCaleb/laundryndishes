using System;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace LaundryNDishes.UnityCore
{
    public enum LLMProviderType { OpenAIRestServer, LlamaCppDirect, UnitySentis }

    // =========================================================================
    // 1. SINGLETON DE CONFIGURAÇÕES DO USUÁRIO (Fica fora do Git)
    // =========================================================================
    [FilePath("UserSettings/LaundryNDishesUserSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public class LnDUserConfig : ScriptableSingleton<LnDUserConfig>
    {
        [SerializeField] private string llmApiKey = "ollama";
        public string LlmApiKey { get => llmApiKey; set => llmApiKey = value; }

        public void Save() => Save(true);
    }

    // =========================================================================
    // 2. SINGLETON DE CONFIGURAÇÕES DO PROJETO (Vai para o Git)
    // =========================================================================
    [FilePath("ProjectSettings/LaundryNDishesSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public class LnDConfig : ScriptableSingleton<LnDConfig>
    {
        [SerializeField] private LLMProviderType providerType = LLMProviderType.OpenAIRestServer;
        [SerializeField] private string llmServerUrl = "http://localhost:11434/v1/chat/completions";
        [SerializeField] private string llmModel = "gemma:2b";
        [SerializeField] private string llamaCppPath = "C:/path/to/llama.cpp/main.exe";
        [SerializeField] private string ggufModelFile = "C:/path/to/models/model.gguf";
        [SerializeField] private string onnxModelPath = "Assets/Models/my_llm.onnx";
        [SerializeField] private string tokenizerPath = "Assets/Models/tokenizer.json";
        [SerializeField] private float temperature = 0.7f;
        [SerializeField] private int maxTokens = 2048;
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

        // MÁGICA AQUI: Redireciona o acesso da API Key para o Singleton de Usuário de forma transparente
        public string LlmApiKey 
        { 
            get => LnDUserConfig.instance.LlmApiKey; 
            set => LnDUserConfig.instance.LlmApiKey = value; 
        }

        public string PlayTestDestinationFolder => PlayModeTestAssembly != null ? Path.GetDirectoryName(AssetDatabase.GetAssetPath(PlayModeTestAssembly)) : string.Empty;
        public string EditorTestScriptsFolder => EditorTestAssembly != null ? Path.GetDirectoryName(AssetDatabase.GetAssetPath(EditorTestAssembly)) : string.Empty;

        public ILLMService GetCurrentService()
        {
            switch (instance.ProviderType)
            {
                case LLMProviderType.OpenAIRestServer: return new Services.OpenAIRestService();
                case LLMProviderType.LlamaCppDirect: return new LlamaCppDirectService();
                default: throw new ArgumentOutOfRangeException(nameof(instance.ProviderType));
            }
        }

        public void SetActiveDatabase(TestDatabase database)
        {
            activeDatabase = database;
        }

        /// <summary>
        /// Salva as alterações tanto no arquivo do projeto quanto no arquivo do usuário.
        /// </summary>
        public void Save()
        {
            // Salva os dados gerais em ProjectSettings/LaundryNDishesSettings.asset
            Save(true);
            
            // Salva a API Key de forma isolada em UserSettings/LaundryNDishesUserSettings.asset
            LnDUserConfig.instance.Save();
        }
    }
}