using System;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering;

namespace LaundryNDishes.Core
{
    public enum LLMProviderType { OpenAIRestServer, LlamaCppDirect }

    // --- NOVO ENUM DE HARDWARE ---
    public enum LlamaCppHardwareBackend 
    { 
        CPU, 
        CPU_AVX, 
        CPU_AVX2, 
        CPU_AVX512, 
        Vulkan, 
        CUDA11, 
        CUDA12,
        MACOS,
        ARM
    }

    [FilePath("ProjectSettings/LaundryNDishesSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public class LnDConfig : ScriptableSingleton<LnDConfig>
    {
        // Prefixo GLOBAL (as configurações serão compartilhadas por todos os projetos do seu computador)
        private const string GlobalPrefPrefix = "LnD_Global_";
        
        [SerializeField] private bool isInitialized = false;

        // --- PROPRIEDADE DE ISOLAÇÃO ---
        [SerializeField] private bool useProjectSettingsOnly = false;

        // Campo para salvar a API Key no arquivo .asset CASO o EditorPrefs esteja desativado
        [SerializeField] private string llmApiKey = string.Empty;

        [SerializeField] private LLMProviderType providerType = LLMProviderType.OpenAIRestServer;
        [SerializeField] private string llmServerUrl = "";
        [SerializeField] private string llmModel = "";
        [SerializeField] private string ggufModelFile = "";
        [SerializeField] private string onnxModelPath = "";
        [SerializeField] private string tokenizerPath = "";
        [SerializeField] private float temperature = 0.7f;
        [SerializeField] private int maxTokens = 2048;
        
        // Estes campos abaixo SEMPRE ficam restritos apenas ao ProjectSettings (nunca vão para o EditorPrefs)
        [SerializeField] private AssemblyDefinitionAsset mainProjectAssembly;
        [SerializeField] private AssemblyDefinitionAsset playModeTestAssembly;
        [SerializeField] private AssemblyDefinitionAsset editorTestAssembly;
        [SerializeField] private DefaultAsset customTemplatesFolder;
        
        [SerializeField] private TestDatabase activeDatabase;
        [SerializeField] private int maxCorrections = 5;
        [SerializeField] private int maxAttempts = 5;
        [SerializeField] private bool showAllLLmComm = true;
        [SerializeField] private bool defaultTearDown = true;
        [SerializeField] private LlamaCppHardwareBackend activeHardwareBackend = LlamaCppHardwareBackend.CPU_AVX2;
        [SerializeField] private LlamaCppHardwareBackend detectedHardware;

        // --- Propriedades Públicas ---
        public bool UseProjectSettingsOnly { get => useProjectSettingsOnly; set => useProjectSettingsOnly = value; }
        public LLMProviderType ProviderType { get => providerType; set => providerType = value; }
        public string LlmServerUrl { get => llmServerUrl; set => llmServerUrl = value; }
        public string LlmModel { get => llmModel; set => llmModel = value; }
        public string GgufModelFile { get => ggufModelFile; set => ggufModelFile = value; }
        public string OnnxModelPath { get => onnxModelPath; set => onnxModelPath = value; }
        public string TokenizerPath { get => tokenizerPath; set => tokenizerPath = value; }
        public float Temperature { get => temperature; set => temperature = value; }
        public int MaxTokens { get => maxTokens; set => maxTokens = value; }
        public int MaxCorrections { get => maxCorrections; set => maxCorrections = value; }
        public int MaxAttempts { get => maxAttempts; set => maxAttempts = value; }
        public bool ShowAllLLmComm { get => showAllLLmComm; set => showAllLLmComm = value; }
        public bool DefaultTearDown { get => defaultTearDown; set => defaultTearDown = value; }
        public DefaultAsset CustomTemplatesFolder { get => customTemplatesFolder; set => customTemplatesFolder = value; }
        public TestDatabase ActiveDatabase { get => activeDatabase; private set => activeDatabase = value; }
        /// <summary>
        /// Define qual backend nativo do Llama.cpp está carregado na máquina atual. O padrão é CPU_AVX2.
        /// </summary>
        /// <summary>
        /// Define qual backend nativo do Llama.cpp está instalado neste projeto específico.
        /// </summary>
        public LlamaCppHardwareBackend ActiveHardwareBackend
        {
            get => activeHardwareBackend;
            set
            {
                if (activeHardwareBackend != value)
                {
                    activeHardwareBackend = value;
                    Save(); // Salva no arquivo .asset imediatamente ao mudar
                }
            }
        }
        
        public LlamaCppHardwareBackend DetectedHardware => detectedHardware;

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
            get => EditorPrefs.GetBool(GlobalPrefPrefix + "TelemetryEnabled", true);
            set => EditorPrefs.SetBool(GlobalPrefPrefix + "TelemetryEnabled", value);
        }
        public bool BoostrapWizardShown
        {
            get => EditorPrefs.GetBool(GlobalPrefPrefix + "BoostrapWizardShown", false);
            set => EditorPrefs.SetBool(GlobalPrefPrefix + "BoostrapWizardShown", value);
        }
       
        public AssemblyDefinitionAsset MainProjectAssembly { get => mainProjectAssembly; set => mainProjectAssembly = value; }
        public AssemblyDefinitionAsset PlayModeTestAssembly { get => playModeTestAssembly; set => playModeTestAssembly = value; }
        public AssemblyDefinitionAsset EditorTestAssembly { get => editorTestAssembly; set => editorTestAssembly = value; }
        
        public string LlmApiKey { get => llmApiKey; set => llmApiKey = value; }

        public string PlayTestDestinationFolder
        {
            get
            {
                return PlayModeTestAssembly != null ? Path.GetDirectoryName(AssetDatabase.GetAssetPath(PlayModeTestAssembly)).Replace("\\", "/") : string.Empty;
            }
        }

        public string EditorTestScriptsFolder
        {
            get
            {
                return EditorTestAssembly != null ? Path.GetDirectoryName(AssetDatabase.GetAssetPath(EditorTestAssembly)).Replace("\\", "/") : string.Empty;
            }
        }

        // --- Ciclo de Vida Condicional ---
        private void OnEnable()
        {
            if (!isInitialized)
            {
                UpdateBestBackend();
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
            string useEnvStr = Environment.GetEnvironmentVariable("LnD_use_env");
            
            if (string.IsNullOrEmpty(useEnvStr) || useEnvStr.ToLower() == "false" || useEnvStr == "0")
            {
                return;
            }
            
            llmApiKey = Environment.GetEnvironmentVariable("LlmApiKey") ?? llmApiKey;
            llmServerUrl = Environment.GetEnvironmentVariable("LlmServerUrl") ?? llmServerUrl;
            llmModel = Environment.GetEnvironmentVariable("LlmModel") ?? llmModel;
            ggufModelFile = Environment.GetEnvironmentVariable("GgufModelFile") ?? ggufModelFile;
            onnxModelPath = Environment.GetEnvironmentVariable("OnnxModelPath") ?? onnxModelPath;
            tokenizerPath = Environment.GetEnvironmentVariable("TokenizerPath") ?? tokenizerPath;

            string envProvider = Environment.GetEnvironmentVariable("ProviderType");
            if (!string.IsNullOrEmpty(envProvider) && Enum.TryParse(envProvider, true, out LLMProviderType parsedProvider))
            {
                providerType = parsedProvider;
            }

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
                EditorPrefs.SetString(GlobalPrefPrefix + "LlmApiKey", llmApiKey);
                EditorPrefs.SetInt(GlobalPrefPrefix + "ProviderType", (int)providerType);
                EditorPrefs.SetString(GlobalPrefPrefix + "LlmServerUrl", llmServerUrl);
                EditorPrefs.SetString(GlobalPrefPrefix + "LlmModel", llmModel);
                EditorPrefs.SetString(GlobalPrefPrefix + "GgufModelFile", ggufModelFile);
                EditorPrefs.SetString(GlobalPrefPrefix + "OnnxModelPath", onnxModelPath);
                EditorPrefs.SetString(GlobalPrefPrefix + "TokenizerPath", tokenizerPath);
                EditorPrefs.SetFloat(GlobalPrefPrefix + "Temperature", temperature);
                EditorPrefs.SetInt(GlobalPrefPrefix + "MaxTokens", maxTokens);
                EditorPrefs.SetInt(GlobalPrefPrefix + "MaxCorrections", maxCorrections);
                EditorPrefs.SetInt(GlobalPrefPrefix + "MaxAttempts", maxAttempts);
                EditorPrefs.SetBool(GlobalPrefPrefix + "ShowAllLLmComm", showAllLLmComm);
                EditorPrefs.SetBool(GlobalPrefPrefix + "DefaultTearDown", defaultTearDown);
            }

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
        
        
        
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        private const uint PF_AVX_INSTRUCTIONS_AVAILABLE = 19;
        private const uint PF_AVX2_INSTRUCTIONS_AVAILABLE = 26;
        private const uint PF_AVX512F_INSTRUCTIONS_AVAILABLE = 41;

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool IsProcessorFeaturePresent(uint ProcessorFeature);
#endif
        public void UpdateBestBackend()
        {
            LlamaCppHardwareBackend previousDetected = detectedHardware;
            DetectBestBackend();
            if (previousDetected != detectedHardware)
            {
                Save();
            }
        }
        
        private void DetectBestBackend()
        {
#if UNITY_EDITOR_OSX
            Debug.Log("<color=green>[LnD Hardware] Plataforma macOS detectada. Ignorando checagens x86/Windows/Linux -> Selecionando Metal</color>");
            // NOTA: Se o seu Enum usar outro nome para o Mac (ex: 'Metal' ou 'Mac'), altere aqui.
            DetectedHardware = LlamaCppHardwareBackend.MACOS; 
            return;
#endif
            string gpuVendor = SystemInfo.graphicsDeviceVendor.ToLower();
            string gpuName = SystemInfo.graphicsDeviceName.ToLower();
            int vramMB = SystemInfo.graphicsMemorySize;

            Debug.Log($"<color=cyan>[LnD Hardware] Analisando sistema: GPU '{SystemInfo.graphicsDeviceName}' ({SystemInfo.graphicsDeviceVendor}) com {vramMB}MB VRAM. CPU: '{SystemInfo.processorType}'</color>");
            
            const int MinVramThresholdMB = 2000;

            // 1. PRIORIDADE MÁXIMA: Placas NVIDIA
            if (gpuVendor.Contains("nvidia") || gpuName.Contains("geforce") || gpuName.Contains("rtx") || gpuName.Contains("gtx") || gpuName.Contains("quadro"))
            {
                if (vramMB >= MinVramThresholdMB || vramMB == 0)
                {
                    bool isOlderNvidia = gpuName.Contains("gtx 6") ||
                                         gpuName.Contains("gtx 7") ||
                                         gpuName.Contains("gtx 8") ||
                                         gpuName.Contains("quadro k") ||
                                         gpuName.Contains("tesla k") ||
                                         gpuName.Contains("titan z") ||
                                         gpuName.Contains("titan black");

                    if (isOlderNvidia)
                    {
                        Debug.Log("<color=green>[LnD Hardware] GPU NVIDIA antiga detectada -> Selecionando CUDA11</color>");
                        detectedHardware = LlamaCppHardwareBackend.CUDA11;
                    }
                    else
                    {
                        Debug.Log("<color=green>[LnD Hardware] GPU NVIDIA detectada com VRAM suficiente -> Selecionando CUDA12</color>");
                        detectedHardware = LlamaCppHardwareBackend.CUDA12;
                    }
                    return; 
                }
            }

            // 2. SEGUNDA PRIORIDADE: AMD Radeon / Intel Arc (Vulkan)
            // Checa se a GPU é AMD/Intel E se a Unity está ativamente rodando com suporte a Vulkan
            if (gpuVendor.Contains("amd") || gpuVendor.Contains("ati") || gpuName.Contains("radeon") || gpuName.Contains("rx ") || gpuName.Contains("arc "))
            {
                if (vramMB >= MinVramThresholdMB || vramMB == 0)
                {
                    Debug.Log("<color=green>[LnD Hardware] GPU AMD/Intel dedicada detectada -> Selecionando Vulkan de forma independente</color>");
                    detectedHardware = LlamaCppHardwareBackend.Vulkan;
                    return; 
                }
            }
            
            var arch = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture;
            bool isArmArchitecture = arch == System.Runtime.InteropServices.Architecture.Arm || 
                                     arch == System.Runtime.InteropServices.Architecture.Arm64;

            if (isArmArchitecture)
            {
                Debug.Log("<color=yellow>[LnD Hardware] Arquitetura ARM detectada (Sem suporte a AVX). Pulando checagens x86 e selecionando CPU básico.</color>");
                detectedHardware = LlamaCppHardwareBackend.CPU;
                return;
            }

#if UNITY_EDITOR_WIN
            try
            {
                if (IsProcessorFeaturePresent(PF_AVX512F_INSTRUCTIONS_AVAILABLE))
                {
                    Debug.Log("<color=green>[LnD Hardware] Windows confirmou via Hardware: AVX-512 Suportado</color>");
                    detectedHardware = LlamaCppHardwareBackend.CPU_AVX512;
                    return;
                }
                if (IsProcessorFeaturePresent(PF_AVX2_INSTRUCTIONS_AVAILABLE))
                {
                    Debug.Log("<color=green>[LnD Hardware] Windows confirmou via Hardware: AVX2 Suportado</color>");
                    detectedHardware = LlamaCppHardwareBackend.CPU_AVX2;
                    return;
                }
                if (IsProcessorFeaturePresent(PF_AVX_INSTRUCTIONS_AVAILABLE))
                {
                    Debug.Log("<color=green>[LnD Hardware] Windows confirmou via Hardware: AVX Antigo Suportado</color>");
                    detectedHardware = LlamaCppHardwareBackend.CPU_AVX;
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LnD Hardware] Erro na API do Windows, usando string de segurança: {ex.Message}");
            }
#endif


            // --- BLOCO EXCLUSIVO DO LINUX ---
#if UNITY_EDITOR_LINUX
            if (System.IO.File.Exists("/proc/cpuinfo"))
            {
                try
                {
                    string cpuInfo = System.IO.File.ReadAllText("/proc/cpuinfo").ToLower();
                    
                    if (cpuInfo.Contains("avx512"))
                    {
                        Debug.Log("<color=green>[LnD Hardware] Kernel Linux confirmou via cpuinfo: AVX-512 Suportado</color>");
                        detectedHardware = LlamaCppHardwareBackend.CPU_AVX512;
                        return;
                    }
                    if (cpuInfo.Contains("avx2"))
                    {
                        Debug.Log("<color=green>[LnD Hardware] Kernel Linux confirmou via cpuinfo: AVX2 Suportado</color>");
                        detectedHardware = LlamaCppHardwareBackend.CPU_AVX2;
                        return;
                    }
                    if (cpuInfo.Contains("avx"))
                    {
                        Debug.Log("<color=green>[LnD Hardware] Kernel Linux confirmou via cpuinfo: AVX Antigo Suportado</color>");
                        detectedHardware = LlamaCppHardwareBackend.CPU_AVX;
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LnD Hardware] Erro ao ler /proc/cpuinfo no Linux: {ex.Message}");
                }
            }
#endif


            // --- FALLBACK GENÉRICO SEGURO (Se rodar em outra plataforma ou se as APIs falharem) ---
            // Removi a heurística perigosa baseada em string que causava o falso positivo no seu Ryzen 3700X.
            // Se o código cair aqui, ele assume o perfil mais seguro compatível com a marca.
            string cpuName = SystemInfo.processorType.ToLower();
            
            if (cpuName.Contains("ryzen") || cpuName.Contains("threadripper") || (cpuName.Contains("intel") && cpuName.Contains("core")))
            {
                // Praticamente todo Ryzen e todo Intel Core moderno (Haswell+) suportam AVX2.
                Debug.Log("<color=yellow>[LnD Hardware] Fallback de segurança de String acionado -> Selecionando AVX2 por compatibilidade padrão</color>");
                detectedHardware = LlamaCppHardwareBackend.CPU_AVX2;
                return;
            }

            // Segurança máxima absoluta para hardware muito antigo
            Debug.Log("<color=red>[LnD Hardware] CPU não identificada -> Selecionando CPU_AVX básico</color>");
            detectedHardware = LlamaCppHardwareBackend.CPU_AVX;
        }

    }
}