using UnityEditor;
using UnityEngine;

public class CustomEditorWindow : EditorWindow
{
	private string textFieldValue = "Tests";
	private bool toggleValue = false;

	[MenuItem("Window/Laundry & Dishes")]
	public static void ShowWindow()
	{
		// Abre a janela e d� um t�tulo a ela
		GetWindow<CustomEditorWindow>("Generate Automated Tests");
	}

	private void OnGUI()
	{
		// Adiciona campos interativos no editor
		GUILayout.Label("Configura��es Personalizadas", EditorStyles.boldLabel);

		// Campo de texto
		textFieldValue = EditorGUILayout.TextField("Campo de Texto", textFieldValue);

		// Toggle
		toggleValue = EditorGUILayout.Toggle("Ativar Op��o", toggleValue);

		// Bot�o
		if (GUILayout.Button("Clique Aqui"))
		{
			Debug.Log($"Campo de texto: {textFieldValue}, Toggle: {toggleValue}");
		}
	}
}
