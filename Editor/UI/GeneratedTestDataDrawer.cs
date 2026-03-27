using UnityEditor;
using UnityEngine;
using System.IO;
using LaundryNDishes.Data;
using LaundryNDishes.DomainAdapter; 

namespace LaundryNDishes.UI
{
    [CustomPropertyDrawer(typeof(GeneratedTestData))]
    public class GeneratedTestDataDrawer : PropertyDrawer
    {
        // Constantes visuais
        private const float BUTTON_WIDTH = 60f;
        private const float INDENT_SPACE = 15f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // Altura de uma linha padrão
            float lineH = EditorGUIUtility.singleLineHeight;
            float yPos = position.y;

            Rect foldoutRect = new Rect(position.x, yPos, position.width, lineH);
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label);
            yPos += lineH + 2;

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;

                var targetScriptProp = property.FindPropertyRelative("TargetScript");
                var sutMethodProp = property.FindPropertyRelative("SutMethod");
                var generatedTestScriptProp = property.FindPropertyRelative("GeneratedTestScript");
                var typeProp = property.FindPropertyRelative("type");
                var testsListProp = property.FindPropertyRelative("IndividualTests");

                // --- 1. CAMPOS BÁSICOS ---
                Rect propRect = new Rect(position.x, yPos, position.width, lineH);
                EditorGUI.PropertyField(propRect, targetScriptProp); yPos += lineH + 2;
                
                propRect.y = yPos; EditorGUI.PropertyField(propRect, sutMethodProp); yPos += lineH + 2;
                propRect.y = yPos; EditorGUI.PropertyField(propRect, generatedTestScriptProp); yPos += lineH + 2;

                EditorGUI.BeginDisabledGroup(true);
                propRect.y = yPos; EditorGUI.PropertyField(propRect, typeProp); yPos += lineH + 2;
                EditorGUI.EndDisabledGroup();

                // --- 2. ALERTA DE DESATUALIZAÇÃO ---
                if (IsSutNewer(targetScriptProp, generatedTestScriptProp))
                {
                    Rect helpRect = new Rect(position.x + INDENT_SPACE, yPos, position.width - INDENT_SPACE, 38);
                    EditorGUI.HelpBox(helpRect, "SUT foi modificado. O teste pode estar desatualizado!", MessageType.Warning);
                    yPos += 40;
                }

                // --- 3. BOTÕES PRINCIPAIS (CLASSE TODA) ---
                Rect buttonsRect = new Rect(position.x + INDENT_SPACE, yPos, position.width - INDENT_SPACE, lineH);
                DrawMainButtons(buttonsRect, generatedTestScriptProp, typeProp, property.serializedObject);
                yPos += lineH + 5;

                // --- 4. LISTA DE MÉTODOS DE TESTE ---
                if (testsListProp.arraySize > 0)
                {
                    Rect headerRect = new Rect(position.x + INDENT_SPACE, yPos, position.width - INDENT_SPACE, lineH);
                    EditorGUI.LabelField(headerRect, "Individual Test Results", EditorStyles.boldLabel);
                    yPos += lineH + 2;

                    for (int i = 0; i < testsListProp.arraySize; i++)
                    {
                        var testItem = testsListProp.GetArrayElementAtIndex(i);
                        var methodName = testItem.FindPropertyRelative("MethodName").stringValue;
                        var fullName = testItem.FindPropertyRelative("FullName").stringValue;
                        var status = (SingleTestStatus)testItem.FindPropertyRelative("Status").enumValueIndex;

                        Rect itemRect = new Rect(position.x + INDENT_SPACE * 2, yPos, position.width - INDENT_SPACE * 2, lineH);
                        DrawIndividualTestRow(itemRect, methodName, fullName, status, generatedTestScriptProp, typeProp, property.serializedObject);
                        yPos += lineH + 2;
                    }
                }
                else
                {
                    Rect emptyRect = new Rect(position.x + INDENT_SPACE, yPos, position.width - INDENT_SPACE, lineH);
                    EditorGUI.LabelField(emptyRect, "Execute a classe toda para descobrir os testes internos.", EditorStyles.miniLabel);
                    yPos += lineH + 2;
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        private void DrawMainButtons(Rect rect, SerializedProperty testScriptProp, SerializedProperty typeProp, SerializedObject obj)
        {
            float halfWidth = rect.width / 2f;
            Rect btn1 = new Rect(rect.x, rect.y, halfWidth - 2, rect.height);
            Rect btn2 = new Rect(rect.x + halfWidth + 2, rect.y, halfWidth - 2, rect.height);

            if (GUI.Button(btn1, "Update Type"))
            {
                // Lógica de update type (mesma que você já tinha)
            }

            if (GUI.Button(btn2, "Run All in Class"))
            {
                RunTest(testScriptProp, typeProp, obj, null); // null executa a classe toda
            }
        }

        private void DrawIndividualTestRow(Rect rect, string methodName, string fullName, SingleTestStatus status, SerializedProperty scriptProp, SerializedProperty typeProp, SerializedObject obj)
        {
            // Status Icon/Color
            Color oldColor = GUI.color;
            GUI.color = status switch {
                SingleTestStatus.Passed => Color.green,
                SingleTestStatus.Failed => Color.red,
                SingleTestStatus.Inconclusive => Color.yellow,
                _ => Color.gray
            };

            Rect statusRect = new Rect(rect.x, rect.y, 15, rect.height);
            GUI.Box(statusRect, GUIContent.none); // Um quadradinho colorido
            GUI.color = oldColor;

            // Nome do teste
            Rect labelRect = new Rect(rect.x + 20, rect.y, rect.width - 20 - BUTTON_WIDTH - 5, rect.height);
            EditorGUI.LabelField(labelRect, methodName);

            // Botão de rodar só este
            Rect runRect = new Rect(rect.x + rect.width - BUTTON_WIDTH, rect.y, BUTTON_WIDTH, rect.height);
            if (GUI.Button(runRect, "Run"))
            {
                RunTest(scriptProp, typeProp, obj, new[] { fullName }); // Passa o FullName exato
            }
        }

        private void RunTest(SerializedProperty scriptProp, SerializedProperty typeProp, SerializedObject obj, string[] specificTests)
        {
            var script = scriptProp.objectReferenceValue as MonoScript;
            if (script == null) return;

            string path = AssetDatabase.GetAssetPath(script);
            string assemblyFile = UnityEditor.Compilation.CompilationPipeline.GetAssemblyNameFromScriptPath(path);
            string assemblyName = assemblyFile.Replace(".dll", "");
            string className = script.GetClass()?.FullName ?? script.name;
            
            var mode = (typeProp.enumNames[typeProp.enumValueIndex] == "Unitieditor") 
                        ? UnityEditor.TestTools.TestRunner.Api.TestMode.EditMode 
                        : UnityEditor.TestTools.TestRunner.Api.TestMode.PlayMode;

            RunTestFireAndForget(assemblyName, className, mode, specificTests);
        }

        private async void RunTestFireAndForget(string assemblyName, string className, UnityEditor.TestTools.TestRunner.Api.TestMode mode, string[] specificTests)
        {
            var executor = new TestExecutor();
            await executor.Run(assemblyName, className, mode, specificTests);
            // O GlobalTestListener já vai interceptar o resultado e salvar no TestDatabase automaticamente!
        }

        private bool IsSutNewer(SerializedProperty targetProp, SerializedProperty testProp)
        {
            // ... (Mantenha sua lógica do IsSutNewer exatamente como estava) ...
            return false;
        }

        // A Matemática da altura precisa ser cirúrgica quando usamos Rects diretos
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded) return EditorGUIUtility.singleLineHeight;

            float h = EditorGUIUtility.singleLineHeight; // Foldout
            h += (EditorGUIUtility.singleLineHeight + 2) * 4; // 4 Propriedades básicas
            
            var targetScriptProp = property.FindPropertyRelative("TargetScript");
            var generatedTestScriptProp = property.FindPropertyRelative("GeneratedTestScript");
            if (IsSutNewer(targetScriptProp, generatedTestScriptProp)) h += 40; // HelpBox
            
            h += EditorGUIUtility.singleLineHeight + 5; // Botões principais

            var testsListProp = property.FindPropertyRelative("IndividualTests");
            if (testsListProp.arraySize > 0)
            {
                h += EditorGUIUtility.singleLineHeight + 2; // Cabeçalho "Individual Test Results"
                h += (EditorGUIUtility.singleLineHeight + 2) * testsListProp.arraySize; // Cada linha de teste
            }
            else
            {
                h += EditorGUIUtility.singleLineHeight + 2; // Texto "Execute a classe toda..."
            }

            return h + 5; // Margem inferior
        }
    }
}