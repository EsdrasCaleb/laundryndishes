using UnityEditor;
using UnityEngine;

public class CustomEditorWindow : EditorWindow
{
	private string textFieldValue = "Tests";
	private bool toggleValue = false;

	[MenuItem("Window/Minha Aba Personalizada")]
	public static void ShowWindow()
	{
		// Abre a janela e dá um título a ela
		GetWindow<CustomEditorWindow>("Generate Tests");
	}

	private void OnGUI()
	{
		// Adiciona campos interativos no editor
		GUILayout.Label("Configurações Personalizadas", EditorStyles.boldLabel);

		// Campo de texto
		textFieldValue = EditorGUILayout.TextField("Campo de Texto", textFieldValue);

		// Toggle
		toggleValue = EditorGUILayout.Toggle("Ativar Opção", toggleValue);

		// Botão
		if (GUILayout.Button("Clique Aqui"))
		{
			Debug.Log($"Campo de texto: {textFieldValue}, Toggle: {toggleValue}");
		}
	}
}
