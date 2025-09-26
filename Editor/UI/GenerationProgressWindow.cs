using LaundryNDishes.Core;
using UnityEditor;
using UnityEngine;

namespace LaundryNDishes.UI
{
    public class GenerationProgressWindow : EditorWindow
    {
        private UnitTestGenerator _generator;

        // Guarda o estado atual para evitar redesenhos desnecessários.
        private UnitTestGenerator.GeneratingStep _lastDisplayedStep = UnitTestGenerator.GeneratingStep.Idle;

        /// <summary>
        /// O método principal que inicia o monitoramento de um processo de geração.
        /// </summary>
        public void StartMonitoring(UnitTestGenerator generator)
        {
            _generator = generator;
            _lastDisplayedStep = UnitTestGenerator.GeneratingStep.Idle; // Reseta o estado
        }

        // A Unity chama este método para desenhar a UI da janela.
        void OnGUI()
        {
            if (_generator == null)
            {
                EditorGUILayout.LabelField("Aguardando o início da geração de um teste...");
                return;
            }

            // Título da janela
            EditorGUILayout.LabelField("Geração de Teste em Progresso", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Exibe o estado atual do gerador.
            EditorGUILayout.LabelField("Estado Atual:", _generator.CurrentStep.ToString());

            // Barra de progresso para dar um feedback visual.
            float progress = (float)_generator.CurrentStep / (float)UnitTestGenerator.GeneratingStep.Finished;
            EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(), progress, "Progresso...");

            EditorGUILayout.Space();

            // Mensagem final
            if (_generator.CurrentStep == UnitTestGenerator.GeneratingStep.Finished)
            {
                if (_generator.TestPassed.HasValue && _generator.TestPassed.Value)
                {
                    EditorGUILayout.HelpBox("Processo finalizado com sucesso! O teste passou.", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox("Processo finalizado, mas o teste falhou ou encontrou um erro. Verifique o Console.", MessageType.Warning);
                }
            }
        }

        /// <summary>
        /// A Unity chama este método em intervalos regulares.
        /// Usamos para forçar a janela a se redesenhar e mostrar o progresso atualizado.
        /// </summary>
        void Update()
        {
            if (_generator != null)
            {
                // Se o estado mudou, força um redesenho da UI.
                if (_generator.CurrentStep != _lastDisplayedStep)
                {
                    _lastDisplayedStep = _generator.CurrentStep;
                    Repaint();
                }
            }
        }
        
        // Adicione este método à sua classe GenerationProgressWindow
        public void ShowFinishedMessage(string message)
        {
            _generator = null; // Para de monitorar o último gerador
            // Desenha uma mensagem final
            EditorGUILayout.HelpBox(message, MessageType.Info);
            Repaint();
        }
    }
}