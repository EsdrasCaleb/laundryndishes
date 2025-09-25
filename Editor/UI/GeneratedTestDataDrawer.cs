using UnityEditor;
using UnityEngine;
using System;
using LaundryNDishes.Data;

namespace LaundryNDishes.UI // Use um namespace de UI
{
// Este atributo diz à Unity para usar esta classe para desenhar qualquer campo do tipo GeneratedTestData.
    [CustomPropertyDrawer(typeof(GeneratedTestData))]
    public class GeneratedTestDataDrawer : PropertyDrawer
    {
        // Este método redesenha a UI padrão para um campo [Serializable]
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // Abre um "foldout" (a setinha para expandir/recolher)
            property.isExpanded =
                EditorGUI.Foldout(new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight),
                    property.isExpanded, label);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;

                // Pega as propriedades filhas pelo nome da variável na classe
                var targetScriptProp = property.FindPropertyRelative("TargetScript");
                var sutMethodProp = property.FindPropertyRelative("SutMethod");
                var generatedTestScriptProp = property.FindPropertyRelative("GeneratedTestScript");
                var timestampProp = property.FindPropertyRelative("LastEditTimestamp");

                // Desenha os campos de MonoScript e string normalmente
                EditorGUILayout.PropertyField(targetScriptProp);
                EditorGUILayout.PropertyField(sutMethodProp);
                EditorGUILayout.PropertyField(generatedTestScriptProp);

                // --- A MÁGICA ACONTECE AQUI ---
                // Pega o valor do timestamp (long)
                long timestamp = timestampProp.longValue;
                // Converte para DateTime para exibição
                DateTime dateTime = DateTimeOffset.FromUnixTimeSeconds(timestamp).LocalDateTime;

                // Exibe a data legível, mas o campo não é editável diretamente.
                // O dado real (o long) permanece intacto.
                EditorGUILayout.LabelField("Last Edit", dateTime.ToString("dd/MM/yyyy HH:mm:ss"));

                // Botão para atualizar o timestamp para o momento atual
                if (GUILayout.Button("Update Timestamp"))
                {
                    timestampProp.longValue = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        // Precisamos ajustar a altura do nosso drawer para comportar os campos extras quando expandido.
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property.isExpanded)
            {
                // Altura do foldout + 4 campos + 1 botão + espaçamento
                return EditorGUIUtility.singleLineHeight * 7 + 5;
            }

            return EditorGUIUtility.singleLineHeight;
        }
    }
}