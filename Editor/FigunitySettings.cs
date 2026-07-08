using System.IO;
using UnityEditor;
using UnityEngine;

namespace Figunity.Editor
{
    public static class FigunitySettings
    {
        public const string AssetPath = "Assets/FIGUNITY/FigunitySettings.asset";

        public static FigunitySettingsAsset LoadOrCreate()
        {
            EnsureFolder("Assets", "FIGUNITY");
            var settings = AssetDatabase.LoadAssetAtPath<FigunitySettingsAsset>(AssetPath);
            if (settings != null)
            {
                return settings;
            }

            settings = ScriptableObject.CreateInstance<FigunitySettingsAsset>();
            AssetDatabase.CreateAsset(settings, AssetPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        public static string ProjectAbsolutePath(string unityPath)
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(root, unityPath));
        }

        public static void EnsureUnityFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var normalized = path.Replace("\\", "/").Trim('/');
            var parts = normalized.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static void EnsureFolder(string parent, string child)
        {
            var path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
