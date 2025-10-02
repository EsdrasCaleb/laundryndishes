using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using LaundryNDishes.Core;
using LaundryNDishes.Data;
using LaundryNDishes.Services;

namespace LaundryNDishes.UI
{
    public class TestGeneratorHubWindow : EditorWindow
    {
        // Classe auxiliar para gerenciar a seleção dos métodos na UI
        private class SelectableMethod
        {
            public string Name;
            public bool IsSelected;
            public SelectableMethod(string name) { Name = name; IsSelected = false; }
        }

        private MonoScript _targetScript;
        private int _selectedTab = 0;
        private string[] _tabTitles = { "Unit Tests", "Behavioral Tests", "Integration Test" };

        private List<SelectableMethod> _unitTestMethods = new List<SelectableMethod>();
        private List<SelectableMethod> _behaviorMethods = new List<SelectableMethod>();
        private string _integrationTestDescription = "The user presses the jump button, the character plays the jump animation, and its Y position increases.";
        private Vector2 _scrollPosition;
        private string _statusMessage;
        private MessageType _statusMessageType = MessageType.Info;
        private bool _isGenerating = false;

        // Método estático para criar e mostrar a janela
        public static void OpenWindow(MonoScript script)
        {
            var window = GetWindow<TestGeneratorHubWindow>("Test Generator Hub");
            window.SetTargetScript(script);
            window.Show();
        }

        public void SetTargetScript(MonoScript script)
        {
            _targetScript = script;
            titleContent = new GUIContent($"Tests for {script.name}");
            PopulateMethodLists();
        }

        // Faz a análise do script uma vez quando a janela é aberta/focada
        private void OnEnable()
        {
            if (_targetScript != null)
            {
                PopulateMethodLists();
            }
        }

        // Desenha a UI da janela
        private void OnGUI()
        {
            if (_targetScript == null)
            {
                EditorGUILayout.HelpBox("Nenhum script selecionado...", MessageType.Warning);
                return;
            }
            // --- MOSTRA A MENSAGEM DE STATUS NO TOPO DA JANELA ---
            if (!string.IsNullOrEmpty(_statusMessage))
            {
                EditorGUILayout.HelpBox(_statusMessage, _statusMessageType);
                EditorGUILayout.Space();
            }

            // --- DESABILITA A UI ENQUANTO A GERAÇÃO ESTÁ ATIVA ---
            EditorGUI.BeginDisabledGroup(_isGenerating);
            {
                EditorGUILayout.LabelField("Target Script:", _targetScript.name, EditorStyles.boldLabel);
                EditorGUILayout.Space();

                _selectedTab = GUILayout.Toolbar(_selectedTab, _tabTitles);
                EditorGUILayout.Space();

                switch (_selectedTab)
                {
                    case 0: DrawUnitTestsTab(); break;
                    case 1: DrawBehavioralTestsTab(); break;
                    case 2: DrawIntegrationTestTab(); break;
                }
            }
            EditorGUI.EndDisabledGroup();
        }

        private void DrawUnitTestsTab()
        {
            EditorGUILayout.LabelField("Selecione os métodos para gerar testes unitários:", EditorStyles.boldLabel);
            DrawMethodsList(_unitTestMethods);
            if (GUILayout.Button("Generate Unit Tests for Selected Methods"))
            {
                var selectedMethods = _unitTestMethods.Where(m => m.IsSelected).Select(m => m.Name).ToList();
                RunSequentialGeneration(selectedMethods, PromptType.Uniti);
            }
        }

        private void DrawBehavioralTestsTab()
        {
            EditorGUILayout.LabelField("Selecione os métodos de ciclo de vida para gerar testes comportamentais:", EditorStyles.boldLabel);
            DrawMethodsList(_behaviorMethods);
            if (GUILayout.Button("Generate Behavioral Tests for Selected Methods"))
            {
                var selectedMethods = _behaviorMethods.Where(m => m.IsSelected).Select(m => m.Name).ToList();
                RunSequentialGeneration(selectedMethods, PromptType.Behavior);
            }
        }

        private void DrawIntegrationTestTab()
        {
            EditorGUILayout.LabelField("Descreva o cenário de integração para o teste (em inglês):", EditorStyles.boldLabel);
            _integrationTestDescription = EditorGUILayout.TextArea(_integrationTestDescription, GUILayout.Height(100));
            if (GUILayout.Button("Generate Integration Test"))
            {
                RunSequentialGeneration(new List<string> { _integrationTestDescription }, PromptType.Integration);
            }
        }

        private void DrawMethodsList(List<SelectableMethod> methods)
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.ExpandHeight(true));
            foreach (var method in methods)
            {
                method.IsSelected = EditorGUILayout.ToggleLeft(method.Name, method.IsSelected);
            }
            EditorGUILayout.EndScrollView();
        }

        private async void RunSequentialGeneration(List<string> methodNames, PromptType promptType)
        {
            if (!methodNames.Any() || (promptType == PromptType.Integration && string.IsNullOrWhiteSpace(methodNames.First())))
            {
                // ATUALIZA A MENSAGEM DE STATUS EM VEZ DE USAR Debug.LogWarning
                _statusMessage = "Nenhum método selecionado ou descrição fornecida.";
                _statusMessageType = MessageType.Warning;
                Repaint(); // Força o redesenho da janela para mostrar a mensagem
                return;
            }
            
            // Inicia o processo
            _isGenerating = true;
            var progressWindow = GetWindow<GenerationProgressWindow>("Test Generation Log");
            progressWindow.Show();
            
            try
            {
                var config = LnDConfig.Instance;
                var llmService = LLMServiceFactory.GetCurrentService();
                var generator = new UnitTestGenerator(llmService, config);

                _statusMessage = $"Iniciando geração em lote de {methodNames.Count} teste(s)...";
                _statusMessageType = MessageType.Info;
                Repaint();

                for (int i = 0; i < methodNames.Count; i++)
                {
                    string methodNameOrDescription = methodNames[i];
                    
                    // Atualiza o status para cada método
                    _statusMessage = $"({i + 1}/{methodNames.Count}) Gerando teste para: {methodNameOrDescription}...";
                    Repaint();
                    
                    progressWindow.StartMonitoring(generator);
                    await generator.Generate(_targetScript, methodNameOrDescription, promptType);
                }

                _statusMessage = $"Processo finalizado! {methodNames.Count} teste(s) foram processados com sucesso.";
                progressWindow.ShowFinishedMessage(_statusMessage);
            }
            catch (System.Exception ex)
            {
                _statusMessage = $"ERRO: {ex.Message}";
                _statusMessageType = MessageType.Error;
                Debug.LogError(ex); // Continua logando a exceção completa no console
            }
            finally
            {
                // GARANTE QUE A UI SEJA REABILITADA NO FINAL, MESMO SE OCORRER UM ERRO
                _isGenerating = false;
                _statusMessage += $"Terminado";
                _statusMessageType = MessageType.Info;
                Repaint();
            }
        }

        private void PopulateMethodLists()
        {
            _unitTestMethods.Clear();
            _behaviorMethods.Clear();

            if (_targetScript == null) return;
            var scriptClass = _targetScript.GetClass();
            if (scriptClass == null) return;

            // Popula testes unitários (métodos públicos que não são da Unity)
            var monoBehaviourMethods = new HashSet<string>(typeof(MonoBehaviour).GetMethods().Select(m => m.Name));
            _unitTestMethods = scriptClass.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName && !monoBehaviourMethods.Contains(m.Name))
                .Select(m => new SelectableMethod(m.Name))
                .ToList();

            // Popula testes comportamentais (métodos da Unity)
            var lifecycleMethods = new[] { "Start", "Awake", "OnEnable", "OnDisable", "Update", "FixedUpdate", "LateUpdate", "OnCollisionEnter", "OnTriggerEnter" /* adicione outros se quiser */ };
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            _behaviorMethods = lifecycleMethods
                .Where(m => scriptClass.GetMethod(m, flags) != null)
                .Select(m => new SelectableMethod(m))
                .ToList();
        }
    }
}