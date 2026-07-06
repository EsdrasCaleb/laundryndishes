using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LaundryNDishes.Core;

namespace LaundryNDishes.UI
{
    public class TestGeneratorHubWindow : EditorWindow
    {
        private class SelectableMethod
        {
            public string Name;
            public bool IsSelected;
            public TestType Type;
            public bool HasExistingTest;

            public SelectableMethod(string name, TestType type, bool hasExistingTest)
            {
                Name = name;
                Type = type;
                HasExistingTest = hasExistingTest;
                IsSelected = false;
            }
        }

        // Classe de dados para isolar o estado de cada script em sua própria aba
        private class ScriptTabData
        {
            public MonoScript TargetScript;
            public List<SelectableMethod> Methods = new List<SelectableMethod>();
            public Vector2 ScrollPosition;
        }

        // Gerenciamento de Abas
        private List<ScriptTabData> _tabs = new List<ScriptTabData>();
        private int _currentTabIdx = 0;

        // Estados Globais de Geração e Progresso integrado
        private string _statusMessage;
        private MessageType _statusMessageType = MessageType.Info;
        private bool _isGenerating = false;
        private UnitTestGenerator _activeGenerator;
        private CancellationTokenSource _cts;
        private readonly List<string> _generationLogs = new List<string>();
        private Vector2 _logScrollPosition;

        public static void OpenWindow(MonoScript script)
        {
            var window = GetWindow<TestGeneratorHubWindow>("Test Generator Hub");
            window.SetTargetScript(script);
            window.Show();
        }

        /// <summary>
        /// Configura a janela para focar em apenas um script específico (Legado/Atalho)
        /// </summary>
        public void SetTargetScript(MonoScript script)
        {
            _tabs.Clear();
            _currentTabIdx = 0;
            if (script != null)
            {
                AddTabForScript(script);
            }
            UpdateTitle();
        }

        /// <summary>
        /// PREPARAÇÃO FUTURA: Passa um GameObject/Prefab e a janela monta abas para todos os scripts nele contidos
        /// </summary>
        public void SetTargetGameObject(GameObject go)
        {
            _tabs.Clear();
            _currentTabIdx = 0;
            if (go != null)
            {
                var components = go.GetComponents<MonoBehaviour>();
                foreach (var comp in components)
                {
                    if (comp == null) continue;
                    var script = MonoScript.FromMonoBehaviour(comp);
                    if (script != null && !_tabs.Any(t => t.TargetScript == script))
                    {
                        AddTabForScript(script);
                    }
                }
            }
            UpdateTitle();
        }

        private void AddTabForScript(MonoScript script)
        {
            var tab = new ScriptTabData { TargetScript = script };
            PopulateMethodListForTab(tab);
            _tabs.Add(tab);
        }

        private void UpdateTitle()
        {
            if (_tabs.Count == 1)
                titleContent = new GUIContent($"Tests for {_tabs[0].TargetScript.name}");
            else if (_tabs.Count > 1)
                titleContent = new GUIContent("Multi-Script Test Hub");
            else
                titleContent = new GUIContent("Test Generator Hub");
        }

        private void OnEnable()
        {
            // Recarrega as listas das abas ativas caso a Unity recompile da memória
            foreach (var tab in _tabs)
            {
                if (tab.TargetScript != null) PopulateMethodListForTab(tab);
            }
        }

        private void Update()
        {
            // Mantém a interface redesenhando fluidamente frame-a-frame enquanto gera o código
            if (_isGenerating)
            {
                Repaint();
            }
        }

        private void OnGUI()
        {
            if (_tabs.Count == 0)
            {
                EditorGUILayout.HelpBox("Nenhum script ou objeto válido selecionado...", MessageType.Warning);
                return;
            }

            // 1. ÁREA DE PROGRESSO INTEGRADA (Fixada no Topo)
            DrawIntegratedProgressArea();

            // Mensagens de status gerais abaixo do progresso
            if (!string.IsNullOrEmpty(_statusMessage))
            {
                EditorGUILayout.HelpBox(_statusMessage, _statusMessageType);
                EditorGUILayout.Space(5);
            }

            // 2. DESENHO DAS ABAS (Se houver mais de um script detectado no Objeto)
            if (_tabs.Count > 1)
            {
                string[] tabNames = _tabs.Select(t => t.TargetScript.name).ToArray();
                _currentTabIdx = GUILayout.Toolbar(_currentTabIdx, tabNames);
                EditorGUILayout.Space(5);
            }

            // Garante que o índice da aba está em escopo seguro
            if (_currentTabIdx >= _tabs.Count) _currentTabIdx = 0;
            var currentTab = _tabs[_currentTabIdx];

            // Bloqueia interações com a lista de métodos do script ativo durante a geração
            EditorGUI.BeginDisabledGroup(_isGenerating);
            {
                EditorGUILayout.LabelField("Target Script:", currentTab.TargetScript.name, EditorStyles.boldLabel);
                EditorGUILayout.Space(5);

                DrawMethodsTab(currentTab);
            }
            EditorGUI.EndDisabledGroup();
        }

        private void DrawIntegratedProgressArea()
        {
            if (!_isGenerating) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("AI TEST GENERATION IN PROGRESS", EditorStyles.boldLabel);

            float stepProgress = 0f;
            string stepName = "Initializing...";

            if (_activeGenerator != null)
            {
                stepProgress = (float)_activeGenerator.CurrentStep / (float)UnitTestGenerator.GeneratingStep.Finished;
                stepName = _activeGenerator.CurrentStep.ToString();
            }

            EditorGUILayout.LabelField($"Current Step: {stepName}");
            EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(), stepProgress, $"Progress: {Mathf.RoundToInt(stepProgress * 100)}%");
            
            EditorGUILayout.Space(5);

            // BOTÃO DE ABORTAR NO MEIO DA ÁREA
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            Color originalBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.85f, 0.3f, 0.3f); // Vermelho sutil de alerta
            
            if (GUILayout.Button("ABORT GENERATION", GUILayout.Width(220), GUILayout.Height(26)))
            {
                AbortGenerationProcess();
            }
            
            GUI.backgroundColor = originalBg;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // Histórico rápido de logs na própria janela principal
            if (_generationLogs.Count > 0)
            {
                EditorGUILayout.Space(5);
                _logScrollPosition = EditorGUILayout.BeginScrollView(_logScrollPosition, GUILayout.Height(60), GUILayout.ExpandWidth(true));
                foreach (var log in _generationLogs)
                {
                    EditorGUILayout.LabelField($"<color=grey>{log}</color>", new GUIStyle(EditorStyles.miniLabel) { richText = true });
                }
                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);
        }

        private void DrawMethodsTab(ScriptTabData tab)
        {
            EditorGUILayout.LabelField("Select methods to generate tests:", EditorStyles.boldLabel);
            
            tab.ScrollPosition = EditorGUILayout.BeginScrollView(tab.ScrollPosition, GUILayout.ExpandHeight(true));
            
            if (tab.Methods.Count == 0)
            {
                EditorGUILayout.LabelField("No supported methods found in this script.");
            }
            else
            {
                foreach (var method in tab.Methods)
                {
                    EditorGUI.BeginDisabledGroup(method.HasExistingTest);
                    
                    string displayName = method.HasExistingTest ? $"{method.Name} (Test already exists)" : method.Name;
                    method.IsSelected = EditorGUILayout.ToggleLeft(displayName, method.IsSelected);
                    
                    EditorGUI.EndDisabledGroup();
                }
            }
            
            EditorGUILayout.EndScrollView();
            EditorGUILayout.Space(5);

            if (GUILayout.Button("Generate Tests for Selected Methods", GUILayout.Height(30)))
            {
                var methodsToGenerate = tab.Methods.Where(m => m.IsSelected && !m.HasExistingTest).ToList();
                RunSequentialGeneration(methodsToGenerate, tab);
            }
        }

        private async void RunSequentialGeneration(List<SelectableMethod> methodsToGenerate, ScriptTabData currentTab)
        {
            if (!methodsToGenerate.Any())
            {
                _statusMessage = "Nenhum método válido selecionado.";
                _statusMessageType = MessageType.Warning;
                Repaint();
                return;
            }

            _isGenerating = true;
            _generationLogs.Clear();
            _statusMessage = $"Iniciando geração em lote de {methodsToGenerate.Count} teste(s)...";
            _statusMessageType = MessageType.Info;
            
            // Inicializa Token de cancelamento assíncrono
            _cts = new CancellationTokenSource();
            Repaint();

            try
            {
                var config = LnDConfig.instance;
                var llmService = config.GetCurrentService();
                var generator = new UnitTestGenerator(llmService, config);

                _activeGenerator = generator;
                _activeGenerator.OnProgressLog += HandleIncomingLog;

                for (int i = 0; i < methodsToGenerate.Count; i++)
                {
                    // Checa interrupção entre a transição de métodos da fila
                    if (_cts.Token.IsCancellationRequested) break;

                    var methodInfo = methodsToGenerate[i];
                    _statusMessage = $"({i + 1}/{methodsToGenerate.Count}) Gerando teste para: {methodInfo.Name} [{methodInfo.Type}]...";
                    Repaint();
                    
                    await generator.Generate(currentTab.TargetScript, methodInfo.Name, methodInfo.Type, null, _cts.Token);
                }

                if (!_cts.Token.IsCancellationRequested)
                {
                    _statusMessage = $"Processo finalizado! {methodsToGenerate.Count} teste(s) processados.";
                    _statusMessageType = MessageType.Info;
                }
            }
            catch (Exception ex)
            {
                _statusMessage = $"ERRO: {ex.Message}";
                _statusMessageType = MessageType.Error;
                Debug.LogError(ex);
            }
            finally
            {
                CleanUpGenerationState();
                
                // Atualiza a aba para trancar os métodos recém-gerados
                PopulateMethodListForTab(currentTab); 
                
                EditorApplication.delayCall += () =>
                {
                    Debug.Log("[LnD] Forçando atualização do AssetDatabase para compilar novos testes...");
                    AssetDatabase.Refresh();
                };
            }
        }

        private void HandleIncomingLog(string message)
        {
            _generationLogs.Add(message);
            if (_generationLogs.Count > 25) _generationLogs.RemoveAt(0); // Impede estouro de memória visual
            _logScrollPosition.y = float.MaxValue; // Força auto-scroll do mini-log do topo
        }

        private void AbortGenerationProcess()
        {
            if (_cts != null)
            {
                _cts.Cancel();
            }
            _statusMessage = "Geração de testes cancelada pelo usuário.";
            _statusMessageType = MessageType.Warning;
            CleanUpGenerationState();
        }

        private void CleanUpGenerationState()
        {
            if (_activeGenerator != null)
            {
                _activeGenerator.OnProgressLog -= HandleIncomingLog;
                _activeGenerator = null;
            }
            _isGenerating = false;
            if (_cts != null)
            {
                _cts.Dispose();
                _cts = null;
            }
            Repaint();
        }

        private void PopulateMethodListForTab(ScriptTabData tab)
        {
            tab.Methods.Clear();
            if (tab.TargetScript == null) return;
            
            var scriptClass = tab.TargetScript.GetClass();
            if (scriptClass == null) return;

            var config = LnDConfig.instance;
            var activeDb = config.ActiveDatabase;

            var (unitMethods, behaviorMethods) = ScriptMethodAnalyzer.CategorizeMethods(scriptClass);
            bool isMonoBehaviour = scriptClass.IsSubclassOf(typeof(MonoBehaviour));

            if (!isMonoBehaviour)
            {
                var allEligibleMethods = unitMethods.Concat(behaviorMethods)
                    .Where(methodName => 
                    {
                        var methodInfo = scriptClass.GetMethod(methodName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static);
                        return methodInfo != null && !methodInfo.IsAbstract;
                    })
                    .ToList();

                foreach (var method in allEligibleMethods)
                {
                    bool exists = activeDb != null && activeDb.HasTestForMethod(tab.TargetScript, method);
                    tab.Methods.Add(new SelectableMethod(method, TestType.Uniti, exists));
                }
            }
            else
            {
                foreach (var method in unitMethods)
                {
                    bool exists = activeDb != null && activeDb.HasTestForMethod(tab.TargetScript, method);
                    tab.Methods.Add(new SelectableMethod(method, TestType.Uniti, exists));
                }

                foreach (var method in behaviorMethods)
                {
                    bool exists = activeDb != null && activeDb.HasTestForMethod(tab.TargetScript, method);
                    tab.Methods.Add(new SelectableMethod(method, TestType.Behavior, exists));
                }
            }

            tab.Methods = tab.Methods.OrderBy(m => m.Name).ToList();
        }

        private void OnDestroy()
        {
            CleanUpGenerationState();
        }
    }
}