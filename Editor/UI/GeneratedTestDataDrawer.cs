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
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            property.isExpanded = EditorGUI.Foldout(
                new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight),
                property.isExpanded, label);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;

                // Puxa as propriedades
                var targetScriptProp = property.FindPropertyRelative("TargetScript");
                var sutMethodProp = property.FindPropertyRelative("SutMethod");
                var generatedTestScriptProp = property.FindPropertyRelative("GeneratedTestScript");
                var typeProp = property.FindPropertyRelative("type");
                var numberOfTestsProp = property.FindPropertyRelative("numberOfTests");
                var passedTestCountProp = property.FindPropertyRelative("passedTestCount");

                // Desenha os campos básicos
                EditorGUILayout.PropertyField(targetScriptProp);
                EditorGUILayout.PropertyField(sutMethodProp);
                EditorGUILayout.PropertyField(generatedTestScriptProp);
                // --- BLOQUEIA A EDIÇÃO DO TIPO ---
                EditorGUI.BeginDisabledGroup(true); // Tudo abaixo disso fica cinza (read-only)
                EditorGUILayout.PropertyField(typeProp);
                EditorGUI.EndDisabledGroup();       // Volta ao normal para os próximos botões

                // Mostra o resultado da última execução
                EditorGUILayout.LabelField("Last Result", $"{passedTestCountProp.intValue} passed / {numberOfTestsProp.intValue} total");

                // --- ALERTA DE DESATUALIZAÇÃO ---
                if (IsSutNewer(targetScriptProp, generatedTestScriptProp))
                {
                    EditorGUILayout.HelpBox("Atenção: O script original (SUT) foi modificado mais recentemente do que este teste. O teste pode estar desatualizado!", MessageType.Warning);
                }

                // --- BOTÕES ---
                EditorGUILayout.BeginHorizontal();

                // 1. Atualizar Tipo (Refaz o elo)
                if (GUILayout.Button("Update Type"))
                {
                    var script = generatedTestScriptProp.objectReferenceValue as MonoScript;
                    if (script != null)
                    {
                        string path = AssetDatabase.GetAssetPath(script);
                        string assemblyName = UnityEditor.Compilation.CompilationPipeline.GetAssemblyNameFromScriptPath(path);
                        
                        if (!string.IsNullOrEmpty(assemblyName) && assemblyName.Contains("PlayMode"))
                        {
                            typeProp.enumValueIndex = (int)TestType.Behavior; 
                        }
                        else
                        {
                            typeProp.enumValueIndex = (int)TestType.Unitieditor; 
                        }
                        Debug.Log($"[LnD] Tipo atualizado baseado no assembly: {assemblyName}");
                    }
                }

                // 2. Rodar Teste
                if (GUILayout.Button("Run Test"))
                {
                    var script = generatedTestScriptProp.objectReferenceValue as MonoScript;
                    if (script != null)
                    {
                        string path = AssetDatabase.GetAssetPath(script);
                        string assemblyFile = UnityEditor.Compilation.CompilationPipeline.GetAssemblyNameFromScriptPath(path);
                        string assemblyName = assemblyFile.Replace(".dll", "");
                        string className = script.GetClass()?.FullName ?? script.name;
                        
                        var mode = (typeProp.enumNames[typeProp.enumValueIndex] == "Unitieditor") 
                                    ? UnityEditor.TestTools.TestRunner.Api.TestMode.EditMode 
                                    : UnityEditor.TestTools.TestRunner.Api.TestMode.PlayMode;

                        RunTestFireAndForget(assemblyName, className, mode, numberOfTestsProp, passedTestCountProp, property.serializedObject);
                    }
                }

                EditorGUILayout.EndHorizontal();
                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        // Helper para checar as datas dos arquivos
        private bool IsSutNewer(SerializedProperty targetProp, SerializedProperty testProp)
        {
            var targetScript = targetProp.objectReferenceValue as MonoScript;
            var testScript = testProp.objectReferenceValue as MonoScript;

            if (targetScript != null && testScript != null)
            {
                string targetPath = AssetDatabase.GetAssetPath(targetScript);
                string testPath = AssetDatabase.GetAssetPath(testScript);

                if (File.Exists(targetPath) && File.Exists(testPath))
                {
                    return File.GetLastWriteTime(targetPath) > File.GetLastWriteTime(testPath);
                }
            }
            return false;
        }

        // Método auxiliar para rodar o teste
        private async void RunTestFireAndForget(string assemblyName, string className, UnityEditor.TestTools.TestRunner.Api.TestMode mode, SerializedProperty numTests, SerializedProperty passedTests, SerializedObject serializedObj)
        {
            var executor = new TestExecutor();
            await executor.Run(assemblyName, className, mode);

            if (executor.TestResult.HasValue)
            {
                var (_, passCount, failCount) = executor.TestResult.Value;
                
                serializedObj.Update();
                numTests.intValue = passCount + failCount;
                passedTests.intValue = passCount;
                serializedObj.ApplyModifiedProperties();
                
                Debug.Log($"[LnD] Teste individual finalizado! Passaram: {passCount}, Falharam: {failCount}");
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property.isExpanded)
            {
                float height = EditorGUIUtility.singleLineHeight * 7 + 10;
                
                // Se o alerta estiver visível, precisamos aumentar a altura da caixa do inspetor
                var targetScriptProp = property.FindPropertyRelative("TargetScript");
                var generatedTestScriptProp = property.FindPropertyRelative("GeneratedTestScript");
                
                if (IsSutNewer(targetScriptProp, generatedTestScriptProp))
                {
                    height += 40; // Altura aproximada do HelpBox com duas linhas
                }
                
                return height; 
            }
            return EditorGUIUtility.singleLineHeight;
        }
    }
}