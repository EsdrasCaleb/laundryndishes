using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;

namespace LaundryNDishes
{
    public class CacheUpdater
    {
        private static bool isUpdatingCache = false;
        private static float progress = 0f;
        private static string cachePath = Path.Combine(Application.dataPath, "ScriptCache.json");
        private static List<ScriptInfo> scriptCache = new List<ScriptInfo>();

        public static bool IsUpdatingCache => isUpdatingCache;
        public static float Progress => progress;
        public static List<ScriptInfo> ScriptCache => scriptCache;

        public static void LoadCache()
        {
            if (File.Exists(cachePath))
            {
                string json = File.ReadAllText(cachePath);
                scriptCache = JsonUtility.FromJson<ScriptCacheWrapper>(json).Scripts;
            }
        }
        
        private static string CleanMethodName(string input)
        {
            // Remove "public" e espaços extras
            string cleaned = Regex.Replace(input, @"\bpublic\b\s*", "");

            // Remove o último caractere se for '('
            if (cleaned.EndsWith("("))
            {
                cleaned = cleaned.Substring(0, cleaned.Length - 1);
            }

            return cleaned;
        }

        public static void UpdateCache()
        {
            isUpdatingCache = true;
            progress = 0f;
            string[] guids = AssetDatabase.FindAssets("t:MonoScript");
            List<string> fullPaths = new List<string>();

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith("Assets/"))
                {
                    string fullPath = Path.Combine(Application.dataPath, "..", path);
                    fullPaths.Add(fullPath);
                }
            }
            
            
            // Start the cache update process on a separate thread
            Thread updateThread = new Thread(() =>
            {
                scriptCache.Clear();

                for (int i = 0; i < fullPaths.Count; i++)
                {
          
                    string fullPath =  fullPaths[i];
                    string fileName = Path.GetFileNameWithoutExtension(fullPaths[i]);
                    string lastEdit = File.GetLastWriteTime(fullPath).ToString();

                    // Improved method extraction
                    string scriptContent = File.ReadAllText(fullPath);
                    var methodMatches = Regex.Matches(scriptContent, 
                        @"public\s+\w+\s+(\w+)\s*\(");
                    foreach (Match match in methodMatches)
                    {
                        scriptCache.Add(new ScriptInfo
                        {
                            FileName = fileName,
                            MethodName = CleanMethodName(match.Groups[0].Value),
                            LastEdit = lastEdit,
                            GeneratedTest = "None"
                        });
                    }
                    

                    // Update progress
                    progress = (float)(i + 1) / fullPaths.Count;
                }

                string json = JsonUtility.ToJson(new ScriptCacheWrapper { Scripts = scriptCache }, true);
                File.WriteAllText(cachePath, json);

                // Ensure UI update happens on the main thread
                EditorApplication.delayCall += () =>
                {
                    isUpdatingCache = false;
                };
            });

            updateThread.Start();
        }

        [System.Serializable]
        private class ScriptCacheWrapper
        {
            public List<ScriptInfo> Scripts;
        }

        [System.Serializable]
        public class ScriptInfo
        {
            public string FileName;
            public string MethodName;
            public string LastEdit;
            public string GeneratedTest = "None";
        }
    }
}
