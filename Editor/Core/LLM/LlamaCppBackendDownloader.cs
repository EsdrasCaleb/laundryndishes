using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace LaundryNDishes.Core
{
    public class LlamaCppBackendDownloader
    {
        private const string BASE_URL = "https://github.com/EsdrasCaleb/LnDArtifacts/releases/download/v1/";

        private CancellationTokenSource _cts;

        public bool IsDownloading { get; private set; }
        public float Progress { get; private set; }
        public string StatusMessage { get; private set; } = "Idle";

        public event Action OnProgressUpdated;

        #region Resolução de Caminhos

        /// <summary>
        /// Resolve o caminho absoluto para "../../ThirdParty/LlamaCore" a partir da pasta DESTE script.
        /// </summary>
        private static string GetAbsoluteTargetFolder([CallerFilePath] string currentScriptPath = "")
        {
            string currentDir = Path.GetDirectoryName(currentScriptPath);
            string targetDir = Path.GetFullPath(Path.Combine(currentDir, "../../ThirdParty/LlamaCore/X64"));
            return targetDir.Replace("\\", "/");
        }

        /// <summary>
        /// Converte um caminho absoluto do Windows/Mac/Linux para o formato relativo da Unity (ex: "Assets/...")
        /// </summary>
        private static string ToUnityRelativePath(string absolutePath)
        {
            string projectDir = Path.GetDirectoryName(Application.dataPath).Replace("\\", "/");
            string normalized = absolutePath.Replace("\\", "/");

            if (normalized.StartsWith(projectDir, StringComparison.OrdinalIgnoreCase))
            {
                return normalized.Substring(projectDir.Length + 1);
            }
            return normalized;
        }

        #endregion

        #region Mapeamento de URLs via Enum

        public string GetDownloadUrl(LlamaCppHardwareBackend backend)
        {
            bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            string prefix = isWindows ? "win" : "linux";

            string fileName = backend switch
            {
                LlamaCppHardwareBackend.CPU => $"{prefix}noavx.zip",
                LlamaCppHardwareBackend.CPU_AVX => $"{prefix}avx.zip",
                LlamaCppHardwareBackend.CPU_AVX2 => $"{prefix}avx2.zip",
                LlamaCppHardwareBackend.CPU_AVX512 => $"{prefix}avx512.zip",
                LlamaCppHardwareBackend.Vulkan => $"{prefix}Vulkan.zip",
                LlamaCppHardwareBackend.CUDA11 => $"{prefix}Cuda11.zip",
                LlamaCppHardwareBackend.CUDA12 => $"{prefix}Cuda12.zip",
                _ => $"{prefix}avx2.zip"
            };

            return BASE_URL + fileName;
        }

        #endregion

        #region Controle de Sessão

        public void StartSession()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
        }

        public void Cancel()
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                StatusMessage = "Cancelling backend installation...";
                _cts.Cancel();
                NotifyChange();
            }
        }

        #endregion

        #region Download e Descompactação

        public async Task InstallBackendAsync(LlamaCppHardwareBackend backend)
        {
            if (IsDownloading) return;

            IsDownloading = true;
            Progress = 0f;
            NotifyChange();

            string zipUrl = GetDownloadUrl(backend);
            string tempZipPath = Path.Combine(Application.temporaryCachePath, $"backend_{backend}.zip");
            CancellationToken token = _cts?.Token ?? CancellationToken.None;

            try
            {
                // 1. Download do Arquivo ZIP
                var progressReporter = new Progress<float>(val =>
                {
                    Progress = val * 0.7f; // Download representa 70% do processo
                    StatusMessage = $"Downloading {backend} backend... {(val * 100f):F0}%";
                    NotifyChange();
                });

                await DownloadFileAsync(zipUrl, tempZipPath, progressReporter, token);

                // 2. Extração dos Arquivos com verificação inteligente de .meta
                StatusMessage = "Extracting native libraries...";
                Progress = 0.8f;
                NotifyChange();

                string absoluteTargetFolder = GetAbsoluteTargetFolder();
                if (!Directory.Exists(absoluteTargetFolder))
                {
                    Directory.CreateDirectory(absoluteTargetFolder);
                }

                // Lista para guardar apenas os arquivos que NÃO tinham .meta (arquivos novos)
                List<string> newlyExtractedFiles = new List<string>();

                using (ZipArchive archive = ZipFile.OpenRead(tempZipPath))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        token.ThrowIfCancellationRequested();

                        if (string.IsNullOrEmpty(entry.Name)) continue;

                        string destFilePath = Path.Combine(absoluteTargetFolder, entry.Name);
                        string metaFilePath = destFilePath + ".meta";

                        // VERIFICAÇÃO INTELIGENTE:
                        // Se o arquivo E o meta já existem, apenas sobrescrevemos o binário.
                        // Se o meta NÃO existe, registramos na lista para configurar o PluginImporter depois.
                        bool metaAlreadyExists = File.Exists(destFilePath) && File.Exists(metaFilePath);

                        entry.ExtractToFile(destFilePath, overwrite: true);

                        if (!metaAlreadyExists)
                        {
                            newlyExtractedFiles.Add(destFilePath);
                        }
                    }
                }

                Progress = 0.9f;
                StatusMessage = "Configuring Unity meta files...";
                NotifyChange();

                // 3. Só chamamos a Unity para criar e configurar os .meta se houver arquivos NOVOS!
                if (newlyExtractedFiles.Count > 0)
                {
                    AssetDatabase.Refresh(); // Força a criação dos .meta iniciais
                    ConfigureNewPluginsMeta(newlyExtractedFiles);
                }
                else
                {
                    // Se apenas sobrescrevemos arquivos existentes, um simples Refresh já basta
                    AssetDatabase.Refresh();
                    Debug.Log("<color=yellow>[LnD Backend] Arquivos DLL/.SO atualizados. Mantendo arquivos .meta existentes!</color>");
                }

                Progress = 1f;
                StatusMessage = "Backend installed successfully!";
                
                LnDConfig.instance.ActiveHardwareBackend = backend;
                LnDConfig.instance.Save();
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Installation cancelled.";
                throw;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                Debug.LogError($"[LnD Backend] Failed to install backend: {ex}");
                throw;
            }
            finally
            {
                if (File.Exists(tempZipPath)) File.Delete(tempZipPath);
                IsDownloading = false;
                NotifyChange();
            }
        }

        private async Task DownloadFileAsync(string url, string destPath, IProgress<float> progress, CancellationToken token)
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(10);

            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var canReportProgress = totalBytes != -1 && progress != null;

            using var stream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[16384];
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token)) != 0)
            {
                token.ThrowIfCancellationRequested();
                await fileStream.WriteAsync(buffer, 0, bytesRead, token);
                totalRead += bytesRead;

                if (canReportProgress)
                    progress.Report((float)totalRead / totalBytes);
            }
        }

        #endregion

        #region Configuração Apenas de Novos .meta

        /// <summary>
        /// Configura os arquivos .meta APENAS dos arquivos que foram extraídos pela primeira vez.
        /// </summary>
        private void ConfigureNewPluginsMeta(List<string> newFiles)
        {
            string[] targetExtensions = { ".dll", ".so" };

            foreach (string filePath in newFiles)
            {
                string ext = Path.GetExtension(filePath).ToLower();
                if (Array.IndexOf(targetExtensions, ext) == -1) continue;

                // Converte o caminho do Windows (C:/...) para o formato da Unity (Assets/ThirdParty/LlamaCore/...)
                string relativePath = ToUnityRelativePath(filePath);

                PluginImporter importer = AssetImporter.GetAtPath(relativePath) as PluginImporter;
                if (importer != null)
                {
                    bool isWindowsDll = ext == ".dll";
                    bool isLinuxSo = ext == ".so";

                    importer.SetCompatibleWithAnyPlatform(false);
                    importer.SetCompatibleWithEditor(true);

                    importer.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, isWindowsDll);
                    if (isWindowsDll)
                    {
                        importer.SetPlatformData(BuildTarget.StandaloneWindows64, "CPU", "x86_64");
                        importer.SetEditorData("OS", "Windows");
                        importer.SetEditorData("CPU", "x86_64");
                    }

                    importer.SetCompatibleWithPlatform(BuildTarget.StandaloneLinux64, isLinuxSo);
                    if (isLinuxSo)
                    {
                        importer.SetPlatformData(BuildTarget.StandaloneLinux64, "CPU", "x86_64");
                        importer.SetEditorData("OS", "Linux");
                        importer.SetEditorData("CPU", "x86_64");
                    }

                    importer.SaveAndReimport();
                    Debug.Log($"<color=cyan>[LnD Backend] Novo arquivo detectado e .meta configurado: {Path.GetFileName(filePath)}</color>");
                }
            }
        }

        #endregion

        private void NotifyChange() => OnProgressUpdated?.Invoke();
    }
}