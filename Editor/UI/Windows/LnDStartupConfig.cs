using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using LaundryNDishes.Core; // Referenciando sua nova pasta Web

namespace LaundryNDishes.UI
{
    public class LnDStartupConfig : EditorWindow
    {
        private float _modelProgress = 0f;
        private float _llamaProgress = 0f;
        private bool _isDownloading = false;
        private string _statusText = "Pronto para configurar o ambiente.";

        [MenuItem("Tools/Laundry & Dishes/Startup Config")]
        public static void ShowWindow()
        {
            var window = GetWindow<LnDStartupConfig>("LnD Setup");
            window.minSize = new Vector2(400, 220);
            window.Show();
        }

        private void OnGUI()
        {
            GUILayout.Label("Configuração de Hardware e IA", EditorStyles.boldLabel);
            GUILayout.Space(10);
            
            // Exibe qual placa de vídeo foi detectada para dar feedback visual
            EditorGUILayout.HelpBox($"Hardware Detectado: {SystemInfo.graphicsDeviceName}\nA versão mais otimizada será baixada automaticamente.", MessageType.Info);
            
            GUILayout.Space(10);

            EditorGUI.BeginDisabledGroup(_isDownloading);
            if (GUILayout.Button("Configurar Ambiente (Aprox. 600MB)", GUILayout.Height(40)))
            {
                _ = StartDownloadProcess();
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.Space(20);

            if (_isDownloading || _modelProgress > 0 || _llamaProgress > 0)
            {
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(), _llamaProgress, "Motor de Inferência (Llama.cpp)");
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(), _modelProgress, "Pesos da IA (Qwen 0.5B)");
                
                GUILayout.Space(10);
                GUILayout.Label(_statusText, EditorStyles.centeredGreyMiniLabel);
            }
        }

        private async Task StartDownloadProcess()
        {
            _isDownloading = true;
            
            try
            {
                _statusText = "Avaliando hardware e baixando Llama.cpp...";
                var llamaProgress = new Progress<float>(p => { _llamaProgress = p; Repaint(); });
                await LnDDownloader.DownloadLlamaCppAsync(llamaProgress);

                _statusText = "Baixando inteligência base (Qwen)...";
                var modelProgress = new Progress<float>(p => { _modelProgress = p; Repaint(); });
                await LnDDownloader.DownloadModelAsync(modelProgress);

                _statusText = "Tudo pronto! Seu ambiente foi otimizado.";
                Debug.Log($"[LnD] Arquivos gravados em: {LnDDownloader.GetGlobalInstallDirectory()}");
            }
            catch (Exception ex)
            {
                _statusText = "Erro na configuração. Veja o Console.";
                Debug.LogError($"[LnD] Falha ao configurar: {ex.Message}");
            }
            finally
            {
                _isDownloading = false;
                Repaint();
            }
        }
    }
}