using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;

namespace LaundryNDishes
{
    public class UnityTestTab : EditorWindow
    {
        private Vector2 scrollPosition;

        [MenuItem("Window/Laundry & Dishes/Unity Tests")]
        public static void ShowWindow()
        {
            var window = GetWindow<UnityTestTab>("LnD Unity Tests");
            window.minSize = new Vector2(600, 400);
            window.LoadCache();
        }

        private void OnEnable()
        {
            CacheUpdater.LoadCache();
        }

        private void LoadCache()
        {
            if (CacheUpdater.ScriptCache.Count == 0)
            {
                UpdateCache();
            }
        }

        private void UpdateCache()
        {
            CacheUpdater.UpdateCache();
        }

        private void OnGUI()
        {
            if (GUILayout.Button("Update Cache") && !CacheUpdater.IsUpdatingCache)
            {
                UpdateCache();
            }

            if (CacheUpdater.IsUpdatingCache)
            {
                // Display a loading spinner or progress bar
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Updating Cache...");
                // Use EditorGUI.ProgressBar here
                EditorGUI.ProgressBar(new Rect(10, 50, position.width - 20, 20), CacheUpdater.Progress, "Progress");
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                scrollPosition = GUILayout.BeginScrollView(scrollPosition);
                GUILayout.BeginHorizontal();
                GUILayout.Label("File Name", GUILayout.Width(150));
                GUILayout.Label("Method", GUILayout.Width(200));
                GUILayout.Label("Last Edit", GUILayout.Width(150));
                GUILayout.Label("Generated Test", GUILayout.Width(100));
                GUILayout.EndHorizontal();

                foreach (var script in CacheUpdater.ScriptCache)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button(script.FileName, GUILayout.Width(150)))
                    {
                        string path = AssetDatabase.FindAssets(script.FileName + " t:MonoScript")
                            .Select(AssetDatabase.GUIDToAssetPath)
                            .FirstOrDefault();
                        if (!string.IsNullOrEmpty(path))
                        {
                            UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(
                                Path.Combine(Application.dataPath, "..", path), 1);
                        }
                    }

                    GUILayout.Label(script.MethodName, GUILayout.Width(200));
                    GUILayout.Label(script.LastEdit, GUILayout.Width(150));
                    GUILayout.Label(script.GeneratedTest, GUILayout.Width(100));
                    GUILayout.EndHorizontal();
                }

                GUILayout.EndScrollView();
            }
        }
    }
}
