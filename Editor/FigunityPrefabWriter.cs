using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Figunity.Editor
{
    public static class FigunityPrefabWriter
    {
        public const string PayloadPath = "Assets/FIGUNITY/Imports/payload.json";
        public const string PrefabFolder = "Assets/FIGUNITY/Prefabs";

        public static FigunityDocument ReadPayload()
        {
            var fullPath = Path.GetFullPath(PayloadPath);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("FIGUNITY payload is missing. Run Export From Figma first.", PayloadPath);
            }

            var document = FigunityDocument.Decode(File.ReadAllText(fullPath));
            if (document == null || document.frames == null || document.frames.Count == 0)
            {
                throw new InvalidOperationException("FIGUNITY payload is empty or invalid: " + PayloadPath);
            }

            return document;
        }

        public static void RebuildPrefabsFromPayload()
        {
            var document = ReadPayload();
            EnsureFolder("Assets", "FIGUNITY");
            EnsureFolder("Assets/FIGUNITY", "Prefabs");

            var built = 0;
            for (var i = 0; i < document.frames.Count; i++)
            {
                var screen = document.frames[i];
                if (screen == null || screen.tree == null)
                {
                    continue;
                }

                BuildPreviewPrefab(screen);
                built++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("FIGUNITY rebuilt " + built + " prefab(s) from " + PayloadPath + ".");
        }

        private static void BuildPreviewPrefab(FigunityScreen screen)
        {
            var prefabName = "PF_" + ToPrefabToken(string.IsNullOrWhiteSpace(screen.slug) ? screen.frameName : screen.slug);
            var prefabPath = PrefabFolder + "/" + prefabName + ".prefab";
            var root = new GameObject(prefabName, typeof(RectTransform), typeof(CanvasGroup));

            try
            {
                var rect = root.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(Mathf.Max(1f, screen.width), Mathf.Max(1f, screen.height));

                var result = new FigunityUgUiComposer().ComposeScreen(screen, root.transform, new FigunityFrameOptions
                {
                    rootName = ToPrefabToken(screen.slug),
                    active = true,
                    useCrop = false,
                    includeRootBackground = true,
                    addRootMask = false
                });

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log("FIGUNITY saved " + prefabPath + " (texts=" + result.report.texts + ", graphics=" + result.report.graphics + ", sliders=" + result.report.sliders + ").");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static string ToPrefabToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Frame";
            }

            var result = string.Empty;
            var upperNext = true;
            for (var i = 0; i < value.Length; i++)
            {
                var ch = value[i];
                if (!char.IsLetterOrDigit(ch))
                {
                    upperNext = true;
                    continue;
                }

                result += upperNext ? char.ToUpperInvariant(ch) : ch;
                upperNext = false;
            }

            return string.IsNullOrWhiteSpace(result) ? "Frame" : result;
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
