using UnityEditor;

namespace LaundryNDishes.Data
{
    // O Enum permanece o mesmo.
    public enum LLMProviderType { OpenAIRestServer, LlamaCppDirect }

    // A classe agora tem métodos estáticos para carregar e salvar.
    public class LnDConfig
    {
        // Propriedades da configuração...
        public LLMProviderType ProviderType { get; set; }
        public string LlmServerUrl { get; set; }
        public string LlmApiKey { get; set; }
        public string LlmModel { get; set; }
        public string LlamaCppPath { get; set; }
        public string GgufModelFile { get; set; }
        public float Temperature { get; set; }
        public int MaxTokens { get; set; }
        public string TestDestinationFolder { get; set; }
        public string TestableScriptsFolder { get; set; }

        // Chaves para o EditorPrefs (evita "magic strings").
        private const string KeyPrefix = "LnD.";
        private const string ProviderTypeKey = KeyPrefix + "ProviderType";
        private const string ServerUrlKey = KeyPrefix + "LlmServerUrl";
        private const string ApiKeyKey = KeyPrefix + "LlmApiKey";
        // ... defina outras chaves aqui para consistência

        // Método estático que carrega a configuração a partir do EditorPrefs.
        public static LnDConfig Load()
        {
            var config = new LnDConfig();
            config.ProviderType = (LLMProviderType)EditorPrefs.GetInt(ProviderTypeKey, (int)LLMProviderType.OpenAIRestServer);
            config.LlmServerUrl = EditorPrefs.GetString(ServerUrlKey, "http://localhost:11434/v1/chat/completions");
            config.LlmApiKey = EditorPrefs.GetString(ApiKeyKey, "ollama");
            config.LlmModel = EditorPrefs.GetString(KeyPrefix + "LlmModel", "gemma:2b");
            config.LlamaCppPath = EditorPrefs.GetString(KeyPrefix + "LlamaCppPath", "C:/path/to/llama.cpp/main.exe");
            config.GgufModelFile = EditorPrefs.GetString(KeyPrefix + "GgufModelFile", "C:/path/to/models/model.gguf");
            config.Temperature = EditorPrefs.GetFloat(KeyPrefix + "Temperature", 0.7f);
            config.MaxTokens = EditorPrefs.GetInt(KeyPrefix + "MaxTokens", 2048);
            config.TestDestinationFolder = EditorPrefs.GetString(KeyPrefix + "TestDestinationFolder", "Assets/Tests/Generated");
            config.TestableScriptsFolder = EditorPrefs.GetString(KeyPrefix + "TestableScriptsFolder", "Assets/Scripts");
            return config;
        }

        // Método que salva a instância atual da configuração no EditorPrefs.
        public void Save()
        {
            EditorPrefs.SetInt(ProviderTypeKey, (int)ProviderType);
            EditorPrefs.SetString(ServerUrlKey, LlmServerUrl);
            EditorPrefs.SetString(ApiKeyKey, LlmApiKey);
            EditorPrefs.SetString(KeyPrefix + "LlmModel", LlmModel);
            EditorPrefs.SetString(KeyPrefix + "LlamaCppPath", LlamaCppPath);
            EditorPrefs.SetString(KeyPrefix + "GgufModelFile", GgufModelFile);
            EditorPrefs.SetFloat(KeyPrefix + "Temperature", Temperature);
            EditorPrefs.SetInt(KeyPrefix + "MaxTokens", MaxTokens);
            EditorPrefs.SetString(KeyPrefix + "TestDestinationFolder", TestDestinationFolder);
            EditorPrefs.SetString(KeyPrefix + "TestableScriptsFolder", TestableScriptsFolder);
            // UnityEngine.Debug.Log("Laundry & Dishes settings saved."); // Opcional
        }
    }
}