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

        public bool HasValidModel()
        {
            string path = GetExpectedModelPath();
            return File.Exists(path) && new FileInfo(path).Length > 0;
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

        #endregion
    }
}