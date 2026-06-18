using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace LaundryNDishes.Core
{
    public static class LnDDownloader
    {
        // Links diretos (Raw) para download
        private const string MODEL_URL = "https://huggingface.co/unsloth/Qwen2.5-Coder-0.5B-Instruct-GGUF/resolve/main/Qwen2.5-Coder-0.5B-Instruct-Q8_0.gguf";
        
        // Define as chaves do EditorPrefs
        public const string PREFS_LLAMA_PATH = "LnD_LlamaCppPath";
        public const string PREFS_MODEL_PATH = "LnD_ModelPath";

        // Pasta global no AppData do usuário (ex: C:/Users/Nome/AppData/Roaming/LaundryNDishes)
        public static string GetGlobalInstallDirectory()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string targetFolder = Path.Combine(appData, "LaundryNDishes", "LLM_Core");
            if (!Directory.Exists(targetFolder)) Directory.CreateDirectory(targetFolder);
            return targetFolder;
        }

        public static async Task DownloadModelAsync(IProgress<float> progress)
        {
            string destPath = Path.Combine(GetGlobalInstallDirectory(), "Qwen2.5-Coder-0.5B.gguf");
            
            // Pula se já existe
            if (File.Exists(destPath)) 
            {
                EditorPrefs.SetString(PREFS_MODEL_PATH, destPath);
                progress?.Report(1f);
                return;
            }

            await DownloadFileAsync(MODEL_URL, destPath, progress);
            EditorPrefs.SetString(PREFS_MODEL_PATH, destPath);
        }

        public static async Task DownloadLlamaCppAsync(IProgress<float> progress)
        {
            string installDir = GetGlobalInstallDirectory();
            string llamaFolder = Path.Combine(installDir, "llama_cpp_b8816");
            string zipPath = Path.Combine(installDir, "llama_temp.zip");

            // Verifica qual o OS para baixar o binário correto do GitHub
            string downloadUrl = GetLlamaUrlForCurrentOS();

            if (!Directory.Exists(llamaFolder))
            {
                await DownloadFileAsync(downloadUrl, zipPath, progress);
                
                // Descompacta o zip
                Directory.CreateDirectory(llamaFolder);
                ZipFile.ExtractToDirectory(zipPath, llamaFolder);
                File.Delete(zipPath); // Limpa o zip temporário
            }

            EditorPrefs.SetString(PREFS_LLAMA_PATH, llamaFolder);
        }

        private static string GetLlamaUrlForCurrentOS()
        {
            string gpuName = SystemInfo.graphicsDeviceName.ToLower();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Checa se é NVIDIA para baixar o CUDA 12
                if (gpuName.Contains("nvidia"))
                    return "https://github.com/ggml-org/llama.cpp/releases/download/b8816/llama-b8816-bin-win-cuda-cu12.2.0-x64.zip";
                
                // Checa se é AMD para baixar o Vulkan
                if (gpuName.Contains("amd") || gpuName.Contains("radeon"))
                    return "https://github.com/ggml-org/llama.cpp/releases/download/b8816/llama-b8816-bin-win-vulkan-x64.zip";
                
                // Fallback seguro: Processador puro (CPU)
                return "https://github.com/ggml-org/llama.cpp/releases/download/b8816/llama-b8816-bin-win-avx2-x64.zip";
            }
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? 
                    "https://github.com/ggml-org/llama.cpp/releases/download/b8816/llama-b8816-bin-macos-arm64.zip" : 
                    "https://github.com/ggml-org/llama.cpp/releases/download/b8816/llama-b8816-bin-macos-x64.zip";
            }
            
            // Fallback Linux (também dá para ramificar CUDA aqui se quiser no futuro)
            return "https://github.com/ggml-org/llama.cpp/releases/download/b8816/llama-b8816-bin-ubuntu-x64.zip";
        }

        // Método utilitário para baixar o arquivo com reporte de progresso
        private static async Task DownloadFileAsync(string url, string destPath, IProgress<float> progress)
        {
            using var client = new HttpClient();
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var canReportProgress = totalBytes != -1 && progress != null;

            using var stream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[8192];
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) != 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead);
                totalRead += bytesRead;

                if (canReportProgress)
                {
                    progress.Report((float)totalRead / totalBytes);
                }
            }
        }
    }
}