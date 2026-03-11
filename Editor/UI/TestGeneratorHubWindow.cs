using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LaundryNDishes.Core;
using LaundryNDishes.UnityCore;
using LaundryNDishes.Services;
using LaundryNDishes.Data;

namespace LaundryNDishes.UI
{
    public class TestGeneratorHubWindow : EditorWindow
    {
        // Expandimos a classe para guardar o tipo de teste e se já existe no banco
        private class SelectableMethod
        {
            public string Name;
            public bool IsSelected;
            public PromptType Type;
            public bool HasExistingTest;

            public SelectableMethod(string name, PromptType type, bool hasExistingTest)
            {
                Name = name;
                Type = type;
                HasExistingTest = hasExistingTest;
                IsSelected = false; // Começa desmarcado
            }
        }

        private MonoScript _targetScript;
        private List<SelectableMethod> _allMethods = new List<SelectableMethod>();
        private Vector2 _scrollPosition;
        private string _statusMessage;
        private MessageType _statusMessageType = MessageType.Info;
        private bool _isGenerating = false;

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
            PopulateMethodList();
        }

        private void OnEnable()
        {
            if (_targetScript != null)
            {
                PopulateMethodList();
            }
        }

        private void OnGUI()
        {
            if (_targetScript == null)
            {
                EditorGUILayout.HelpBox("Nenhum script selecionado...", MessageType.Warning);
                return;
            }

            if (!string.IsNullOrEmpty(_statusMessage))
            {
                EditorGUILayout.HelpBox(_statusMessage, _statusMessageType);
                EditorGUILayout.Space();
            }

            EditorGUI.BeginDisabledGroup(_isGenerating);
            {
                EditorGUILayout.LabelField("Target Script:", _targetScript.name, EditorStyles.boldLabel);
                EditorGUILayout.Space();

                // Única aba: Mostra todos os métodos
                DrawMethodsTab();
            }
            EditorGUI.EndDisabledGroup();
        }

        private void DrawMethodsTab()
        {
            EditorGUILayout.LabelField("Select methods to generate tests:", EditorStyles.boldLabel);
            
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.ExpandHeight(true));
            
            if (_allMethods.Count == 0)
            {
                EditorGUILayout.LabelField("No supported methods found in this script.");
            }
            else
            {
                foreach (var method in _allMethods)
                {
                    EditorGUI.BeginDisabledGroup(method.HasExistingTest);
                    
                    // Mostra um aviso se já existir
                    string displayName = method.HasExistingTest ? $"{method.Name} (Test already exists)" : method.Name;
                    
                    method.IsSelected = EditorGUILayout.ToggleLeft(displayName, method.IsSelected);
                    
                    EditorGUI.EndDisabledGroup();
                }
            }
            
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();

            if (GUILayout.Button("Generate Tests for Selected Methods", GUILayout.Height(30)))
            {
                var methodsToGenerate = _allMethods.Where(m => m.IsSelected && !m.HasExistingTest).ToList();
                RunSequentialGeneration(methodsToGenerate);
            }
        }

        private async void RunSequentialGeneration(List<SelectableMethod> methodsToGenerate)
        {
            if (!methodsToGenerate.Any())
            {
                _statusMessage = "Nenhum método válido selecionado.";
                _statusMessageType = MessageType.Warning;
                Repaint();
                return;
            }

            _isGenerating = true;
            var progressWindow = GetWindow<GenerationProgressWindow>("Test Generation Log");
            progressWindow.Show();

            try
            {
                var config = LnDConfig.Instance;
                var llmService = config.GetCurrentService();
                var generator = new UnitTestGenerator(llmService, config);

                _statusMessage = $"Iniciando geração em lote de {methodsToGenerate.Count} teste(s)...";
                _statusMessageType = MessageType.Info;
                Repaint();

                for (int i = 0; i < methodsToGenerate.Count; i++)
                {
                    var methodInfo = methodsToGenerate[i];

                    _statusMessage = $"({i + 1}/{methodsToGenerate.Count}) Gerando teste para: {methodInfo.Name} [{methodInfo.Type}]...";
                    Repaint();

                    progressWindow.StartMonitoring(generator);
                    
                    // Passa o PromptType correto para cada método individualmente
                    await generator.Generate(_targetScript, methodInfo.Name, methodInfo.Type);
                }

                _statusMessage = $"Processo finalizado! {methodsToGenerate.Count} teste(s) processados.";
                progressWindow.ShowFinishedMessage(_statusMessage);
                
                // Repopula a lista ao final para desabilitar os métodos que acabaram de ganhar testes
                PopulateMethodList(); 
                EditorApplication.delayCall += () =>
                {
                    Debug.Log("[LnD] Forçando atualização do AssetDatabase para compilar novos testes...");
                    AssetDatabase.Refresh();
                };
            }
            catch (System.Exception ex)
            {
                _statusMessage = $"ERRO: {ex.Message}";
                _statusMessageType = MessageType.Error;
                Debug.LogError(ex);
            }
            finally
            {
                _isGenerating = false;
                Repaint();
            }
        }

        private void PopulateMethodList()
        {
            _allMethods.Clear();

            if (_targetScript == null) return;
            var scriptClass = _targetScript.GetClass();
            if (scriptClass == null) return;

            var config = LnDConfig.Instance;
            var activeDb = config.ActiveDatabase;

            // Usa o analisador existente
            var (unitMethods, behaviorMethods) = ScriptMethodAnalyzer.CategorizeMethods(scriptClass);

            bool isMonoBehaviour = scriptClass.IsSubclassOf(typeof(MonoBehaviour));

            if (!isMonoBehaviour)
            {
                // Se não é MonoBehaviour, filtra métodos abstratos e junta tudo como Unit Test
                var allEligibleMethods = unitMethods.Concat(behaviorMethods)
                    .Where(methodName => 
                    {
                        var methodInfo = scriptClass.GetMethod(methodName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static);
                        return methodInfo != null && !methodInfo.IsAbstract;
                    })
                    .ToList();

                foreach (var method in allEligibleMethods)
                {
                    bool exists = activeDb != null && activeDb.HasTestForMethod(_targetScript, method);
                    _allMethods.Add(new SelectableMethod(method, PromptType.Uniti, exists));
                }
            }
            else
            {
                // Se for MonoBehaviour, mantém a separação
                foreach (var method in unitMethods)
                {
                    bool exists = activeDb != null && activeDb.HasTestForMethod(_targetScript, method);
                    _allMethods.Add(new SelectableMethod(method, PromptType.Uniti, exists));
                }

                foreach (var method in behaviorMethods)
                {
                    bool exists = activeDb != null && activeDb.HasTestForMethod(_targetScript, method);
                    _allMethods.Add(new SelectableMethod(method, PromptType.Behavior, exists));
                }
            }

            // Ordena alfabeticamente para ficar mais organizado na UI
            _allMethods = _allMethods.OrderBy(m => m.Name).ToList();
        }
    }
}