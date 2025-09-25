using System;
using UnityEditor;
using UnityEngine;

namespace LaundryNDishes.Data
{
    public enum LLMProviderType { OpenAIRestServer, LlamaCppDirect, UnitySentis }

    /// <summary>
    /// Uma classe de instância única (Singleton) que armazena todas as configurações do plugin.
    /// Ela carrega os dados do EditorPrefs na primeira vez que é acessada e opera em memória.
    /// O método Save() persiste as alterações de volta para o EditorPrefs.
    /// </summary>
    public class LnDConfig
    {
        // --- Padrão Singleton ---

        private static LnDConfig _instance;
        public static LnDConfig Instance
        {
            get
            {
                // Se a instância ainda não foi criada, cria uma nova.
                // O construtor privado se encarregará de carregar os dados.
                if (_instance == null)
                {
                    _instance = new LnDConfig();
                }
                return _instance;
            }
        }
        
        // O construtor é privado para garantir que ninguém mais possa criar instâncias desta classe.
        private LnDConfig()
        {
            Load();
        }

        public LLMProviderType ProviderType { get; set; }
        public string LlmServerUrl { get; set; }
        public string LlmApiKey { get; set; }
        public string LlmModel { get; set; }
        public string LlamaCppPath { get; set; }
        public string GgufModelFile { get; set; }
        public string OnnxModelPath { get; set; }
        public string TokenizerPath { get; set; }
        public float Temperature { get; set; }
        public int MaxTokens { get; set; }
        public string PlayTestDestinationFolder { get; set; }
        public string EditorTestScriptsFolder { get; set; }
        public string CustomTemplatesFolder { get; set; }
        public TestDatabase ActiveDatabase { get; private set; }
        
        // --- Chaves de Persistência ---

        private const string KeyPrefix = "LaundryNDishes.";
        public const string ActiveDatabasePathKey = KeyPrefix + "ActiveDatabasePath";
        // Adicione outras chaves aqui se precisar de acesso externo...

        // --- Lógica de Carga e Salvamento ---

        /// <summary>
        /// Carrega todos os valores do EditorPrefs para as propriedades desta instância.
        /// </summary>
        public void Load()
        {
            ProviderType = (LLMProviderType)EditorPrefs.GetInt(KeyPrefix + "ProviderType", (int)LLMProviderType.OpenAIRestServer);
            LlmServerUrl = EditorPrefs.GetString(KeyPrefix + "LlmServerUrl", "http://localhost:11434/v1/chat/completions");
            LlmApiKey = EditorPrefs.GetString(KeyPrefix + "LlmApiKey", "ollama");
            LlmModel = EditorPrefs.GetString(KeyPrefix + "LlmModel", "gemma:2b");
            LlamaCppPath = EditorPrefs.GetString(KeyPrefix + "LlamaCppPath", "C:/path/to/llama.cpp/main.exe");
            GgufModelFile = EditorPrefs.GetString(KeyPrefix + "GgufModelFile", "C:/path/to/models/model.gguf");
            OnnxModelPath = EditorPrefs.GetString(KeyPrefix + "OnnxModelPath", "Assets/Models/my_llm.onnx");
            TokenizerPath = EditorPrefs.GetString(KeyPrefix + "TokenizerPath", "Assets/Models/tokenizer.json");
            Temperature = EditorPrefs.GetFloat(KeyPrefix + "Temperature", 0.7f);
            MaxTokens = EditorPrefs.GetInt(KeyPrefix + "MaxTokens", 2048);
            PlayTestDestinationFolder = EditorPrefs.GetString(KeyPrefix + "TestDestinationFolder", "Assets/Tests/Generated");
            EditorTestScriptsFolder = EditorPrefs.GetString(KeyPrefix + "TestableScriptsFolder", "Assets/Scripts");
            CustomTemplatesFolder = EditorPrefs.GetString(KeyPrefix + "TemplateFolder", string.Empty);
            
            string dbPath = EditorPrefs.GetString(ActiveDatabasePathKey, string.Empty);
            if (!string.IsNullOrEmpty(dbPath))
            {
                ActiveDatabase = AssetDatabase.LoadAssetAtPath<TestDatabase>(dbPath);
            }
        }

        /// <summary>
        /// Salva todos os valores atuais desta instância de volta para o EditorPrefs.
        /// </summary>
        public void Save()
        {
            EditorPrefs.SetInt(KeyPrefix + "ProviderType", (int)ProviderType);
            EditorPrefs.SetString(KeyPrefix + "LlmServerUrl", LlmServerUrl);
            EditorPrefs.SetString(KeyPrefix + "LlmApiKey", LlmApiKey);
            EditorPrefs.SetString(KeyPrefix + "LlmModel", LlmModel);
            EditorPrefs.SetString(KeyPrefix + "LlamaCppPath", LlamaCppPath);
            EditorPrefs.SetString(KeyPrefix + "GgufModelFile", GgufModelFile);
            EditorPrefs.SetString(KeyPrefix + "OnnxModelPath", OnnxModelPath);
            EditorPrefs.SetString(KeyPrefix + "TokenizerPath", TokenizerPath);
            EditorPrefs.SetFloat(KeyPrefix + "Temperature", Temperature);
            EditorPrefs.SetInt(KeyPrefix + "MaxTokens", MaxTokens);
            EditorPrefs.SetString(KeyPrefix + "TestDestinationFolder", PlayTestDestinationFolder);
            EditorPrefs.SetString(KeyPrefix + "TestableScriptsFolder", EditorTestScriptsFolder);
            EditorPrefs.SetString(KeyPrefix + "CustomTemplatesFolder", CustomTemplatesFolder);

            string path = (ActiveDatabase != null) ? AssetDatabase.GetAssetPath(ActiveDatabase) : string.Empty;
            EditorPrefs.SetString(ActiveDatabasePathKey, path);
            
            Debug.Log("Laundry & Dishes settings saved.");
        }

        /// <summary>
        /// Define o banco de dados ativo na instância em memória.
        /// A alteração só será persistida quando Save() for chamado.
        /// </summary>
        public void SetActiveDatabase(TestDatabase database)
        {
            ActiveDatabase = database;
        }
    }
}