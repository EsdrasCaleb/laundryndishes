using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace LaundryNDishes.Core
{
    public class LnDDownloader
    {
        private const string MODEL_URL = "https://huggingface.co/jica98/qwen3.5-4B-super-coder/resolve/main/qwen3.5-4B-super-coder.Q4_0.gguf?download=true";
        private const string LLAMA_VERSION_FOLDER = "llama_cpp_b8816";

        // Controle de Cancelamento Global da Instância
        private CancellationTokenSource _cts;

        public bool IsDownloadingLlama { get; private set; }
        public bool IsDownloadingModel { get; private set; }
        public bool IsDownloadingAny => IsDownloadingLlama || IsDownloadingModel;

        public float LlamaProgress { get; private set; }
        public float ModelProgress { get; private set; }
        public string StatusMessage { get; private set; } = "Idle";

        public event Action OnProgressUpdated;

        public string GetGlobalInstallDirectory()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string targetFolder = Path.Combine(appData, "LaundryNDishes", "LLM_Core");
            if (!Directory.Exists(targetFolder)) Directory.CreateDirectory(targetFolder);
            return targetFolder;
        }

        public string GetExpectedModelPath() => Path.Combine(GetGlobalInstallDirectory(), "active_local_model.gguf").Replace("\\", "/");
        public string GetExpectedLlamaFolder() => Path.Combine(GetGlobalInstallDirectory(), LLAMA_VERSION_FOLDER).Replace("\\", "/");

        public bool HasValidModel()
        {
            string path = GetExpectedModelPath();
            return File.Exists(path) && new FileInfo(path).Length > 0;
        }

        public bool HasValidLlamaCpp()
        {
            string folder = GetExpectedLlamaFolder();
            if (!Directory.Exists(folder)) return false;

            string binaryExtension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "";
            string[] possibleNames = { $"llama-cli{binaryExtension}", $"llama-server{binaryExtension}", $"main{binaryExtension}" };

            foreach (var name in possibleNames)
            {
                if (Directory.GetFiles(folder, name, SearchOption.AllDirectories).Length > 0) return true;
            }
            return false;
        }

        #region Inicialização e Cancelamento

        /// <summary>
        /// Prepara o token de cancelamento para uma nova sessão de downloads.
        /// </summary>
        public void StartSession()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
        }

        /// <summary>
        /// Cancela imediatamente qualquer download ativo nesta instância.
        /// </summary>
        public void Cancel()
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                StatusMessage = "Cancelling downloads and cleaning up...";
                _cts.Cancel();
                NotifyChange();
            }
        }

        #endregion

        #region Funções de Deleção / Limpeza

        public void DeleteModel()
        {
            try
            {
                string path = GetExpectedModelPath();
                if (File.Exists(path)) File.Delete(path);
                LnDConfig.instance.GgufModelFile = "";
                LnDConfig.instance.Save();
                NotifyChange();
            }
            catch (Exception e) { Debug.LogError($"[LnD Downloader] Delete model failed: {e.Message}"); }
        }

        public void DeleteLlamaCpp()
        {
            try
            {
                string folder = GetExpectedLlamaFolder();
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
                LnDConfig.instance.LlamaCppPath = "";
                LnDConfig.instance.Save();
                NotifyChange();
            }
            catch (Exception e) { Debug.LogError($"[LnD Downloader] Delete Llama folder failed: {e.Message}"); }
        }

        #endregion

        #region Processos Assíncronos

        public async Task<string> DownloadModelAsync()
        {
            if (IsDownloadingModel) return GetExpectedModelPath();
            
            IsDownloadingModel = true;
            ModelProgress = 0f;
            NotifyChange();

            string destPath = GetExpectedModelPath();
            CancellationToken token = _cts?.Token ?? CancellationToken.None;

            try
            {
                var progressReporter = new Progress<float>(val =>
                {
                    ModelProgress = val;
                    StatusMessage = val < 1f ? $"Downloading model weights... {(val * 100f):F1}%" : "Finalizing model setup...";
                    NotifyChange();
                });

                await DownloadFileAsync(MODEL_URL, destPath, progressReporter, token);
                
                ModelProgress = 1f;
                return destPath;
            }
            catch (OperationCanceledException)
            {
                ModelProgress = 0f;
                StatusMessage = "Download cancelled.";
                if (File.Exists(destPath)) File.Delete(destPath); // Evita deixar arquivo corrompido pela metade
                throw;
            }
            finally
            {
                IsDownloadingModel = false;
                NotifyChange();
            }
        }

        public async Task<string> DownloadLlamaCppAsync()
        {
            if (IsDownloadingLlama) return LnDConfig.instance.LlamaCppPath;

            IsDownloadingLlama = true;
            LlamaProgress = 0f;
            NotifyChange();

            string installDir = GetGlobalInstallDirectory();
            string llamaFolder = GetExpectedLlamaFolder();
            string zipPath = Path.Combine(installDir, "llama_temp.zip");
            string downloadUrl = GetLlamaUrlForCurrentOS();
            CancellationToken token = _cts?.Token ?? CancellationToken.None;

            try
            {
                var progressReporter = new Progress<float>(val =>
                {
                    LlamaProgress = val;
                    StatusMessage = val < 1f ? $"Downloading Llama.cpp... {(val * 100f):F0}%" : "Extracting framework...";
                    NotifyChange();
                });

                if (!Directory.Exists(llamaFolder))
                {
                    await DownloadFileAsync(downloadUrl, zipPath, progressReporter, token);
                    
                    // Se foi cancelado logo após baixar o zip mas antes de extrair
                    token.ThrowIfCancellationRequested();

                    Directory.CreateDirectory(llamaFolder);
                    ZipFile.ExtractToDirectory(zipPath, llamaFolder);
                    if (File.Exists(zipPath)) File.Delete(zipPath);
                }

                LlamaProgress = 1f;
                NotifyChange();

                string binaryExtension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "";
                string[] possibleNames = { $"llama-cli{binaryExtension}", $"llama-server{binaryExtension}", $"main{binaryExtension}" };

                foreach (var name in possibleNames)
                {
                    string[] files = Directory.GetFiles(llamaFolder, name, SearchOption.AllDirectories);
                    if (files.Length > 0) return files[0].Replace("\\", "/");
                }

                return llamaFolder.Replace("\\", "/");
            }
            catch (OperationCanceledException)
            {
                LlamaProgress = 0f;
                StatusMessage = "Download cancelled.";
                if (File.Exists(zipPath)) File.Delete(zipPath);
                if (Directory.Exists(llamaFolder)) Directory.Delete(llamaFolder, true);
                throw;
            }
            finally
            {
                IsDownloadingLlama = false;
                NotifyChange();
            }
        }

        private async Task DownloadFileAsync(string url, string destPath, IProgress<float> progress, CancellationToken token)
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(15);

            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var canReportProgress = totalBytes != -1 && progress != null;

            using var stream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[16384];
            long totalRead = 0;
            int bytesRead;

            // Passamos o token para o ReadAsync para interromper o laço imediatamente ao cancelar
            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token)) != 0)
            {
                token.ThrowIfCancellationRequested();
                await fileStream.WriteAsync(buffer, 0, bytesRead, token);
                totalRead += bytesRead;

                if (canReportProgress)
                {
                    progress.Report((float)totalRead / totalBytes);
                }
            }
        }

        private void NotifyChange() => OnProgressUpdated?.Invoke();

        private static string GetLlamaUrlForCurrentOS()
        {
            string gpuName = SystemInfo.graphicsDeviceName.ToLower();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (gpuName.Contains("nvidia")) return "https://github.com/ggml-org/llama.cpp/releases/download/b8816/llama-b8816-bin-win-cuda-cu12.2.0-x64.zip";
                if (gpuName.Contains("amd") || gpuName.Contains("radeon")) return "https://github.com/ggml-org/llama.cpp/releases/download/b8816/llama-b8816-bin-win-vulkan-x64.zip";
                return "https://github.com/ggml-org/llama.cpp/releases/download/b8816/llama-b8816-bin-win-avx2-x64.zip";
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? 
                    "https://github.com/ggml-org/llama.cpp/releases/download/b8816/llama-b8816-bin-macos-arm64.tar.gz" : 
                    "https://github.com/ggml-org/llama.cpp/releases/download/b8816/llama-b8816-bin-macos-x64.tar.gz";
            }
            return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ?  "https://github.com/ggml-org/llama.cpp/releases/download/b8816/llama-b8816-bin-ubuntu-arm64.tar.gz"
            : "https://github.com/ggml-org/llama.cpp/releases/download/b8816/llama-b8816-bin-ubuntu-x64.tar.gz";
        }

        #endregion
    }
}