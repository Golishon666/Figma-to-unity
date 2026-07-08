using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Figunity.Editor
{
    public static class FigunityPrefabWriter
    {
        public const string PayloadPath = "Assets/FIGUNITY/Imports/payload.json";
        public const string PrefabFolder = "Assets/FIGUNITY/Prefabs";
        private const string ImportedContentName = "ImportedContent";

        public static FigunityDocument ReadPayload()
        {
            return ReadPayload(FigunitySettings.LoadOrCreate());
        }

        public static FigunityDocument ReadPayload(FigunitySettingsAsset settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            var payloadPath = settings.importFolder.TrimEnd('/', '\\') + "/payload.json";
            var fullPath = Path.GetFullPath(FigunitySettings.ProjectAbsolutePath(payloadPath));
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("FIGUNITY payload is missing. Run Export From Figma first.", payloadPath);
            }

            var document = FigunityDocument.Decode(File.ReadAllText(fullPath));
            if (document == null || document.frames == null || document.frames.Count == 0)
            {
                throw new InvalidOperationException("FIGUNITY payload is empty or invalid: " + payloadPath);
            }

            return document;
        }

        public static void RebuildPrefabsFromPayload()
        {
            RebuildPrefabsFromPayload(FigunitySettings.LoadOrCreate());
        }

        public static void RebuildPrefabsFromPayload(FigunitySettingsAsset settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            var document = ReadPayload(settings);
            FigunityTextureImporter.ConfigureElementTextures(settings.importFolder);
            FigunitySettings.EnsureUnityFolder(settings.prefabFolder);
            if (settings.createRepeatedItemPrefabs)
            {
                FigunitySettings.EnsureUnityFolder(settings.repeatedPrefabFolder);
            }

            var built = 0;
            var buildResults = new List<FigunityBuildResult>();
            for (var i = 0; i < document.frames.Count; i++)
            {
                var screen = document.frames[i];
                if (screen == null || screen.tree == null)
                {
                    continue;
                }

                buildResults.Add(BuildPreviewPrefab(screen, settings));
                built++;
            }

            if (settings.createRepeatedItemPrefabs)
            {
                BuildRepeatedItemPrefabs(document, settings);
            }

            FigunityDiagnosticsWriter.Write(document, settings, buildResults);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("FIGUNITY rebuilt " + built + " prefab(s) from " + settings.importFolder + "/payload.json.");
        }

        private static FigunityBuildResult BuildPreviewPrefab(FigunityScreen screen, FigunitySettingsAsset settings)
        {
            var prefabName = "PF_" + ToPrefabToken(string.IsNullOrWhiteSpace(screen.slug) ? screen.frameName : screen.slug);
            var prefabPath = settings.prefabFolder.TrimEnd('/', '\\') + "/" + prefabName + ".prefab";
            var loadedExistingPrefab = File.Exists(FigunitySettings.ProjectAbsolutePath(prefabPath));
            var root = loadedExistingPrefab
                ? PrefabUtility.LoadPrefabContents(prefabPath)
                : new GameObject(prefabName, typeof(RectTransform), typeof(CanvasGroup));

            try
            {
                root.name = prefabName;
                var rect = root.GetComponent<RectTransform>() ?? root.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(Mathf.Max(1f, screen.width), Mathf.Max(1f, screen.height));
                if (root.GetComponent<CanvasGroup>() == null)
                {
                    root.AddComponent<CanvasGroup>();
                }

                var importedRoot = settings.preserveManualPrefabChildren
                    ? EnsureImportedContent(root.transform, rect.sizeDelta)
                    : rect;
                ClearImportedContent(importedRoot);

                var result = new FigunityUgUiComposer().ComposeScreen(screen, importedRoot, new FigunityFrameOptions
                {
                    settings = settings,
                    rootName = ToPrefabToken(screen.slug),
                    active = true,
                    useCrop = false,
                    includeRootBackground = true,
                    addRootMask = false
                });

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log("FIGUNITY saved " + prefabPath + " (texts=" + result.report.texts + ", graphics=" + result.report.graphics + ", sliders=" + result.report.sliders + ", masks=" + result.report.masks + ").");
                return result;
            }
            finally
            {
                if (loadedExistingPrefab)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        private static RectTransform EnsureImportedContent(Transform root, Vector2 size)
        {
            var child = root.Find(ImportedContentName);
            if (child != null)
            {
                var rect = child.GetComponent<RectTransform>() ?? child.gameObject.AddComponent<RectTransform>();
                ConfigureFullRect(rect, size);
                return rect;
            }

            var go = new GameObject(ImportedContentName, typeof(RectTransform));
            var created = go.GetComponent<RectTransform>();
            created.SetParent(root, false);
            created.SetAsFirstSibling();
            ConfigureFullRect(created, size);
            return created;
        }

        private static void ConfigureFullRect(RectTransform rect, Vector2 size)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }

        private static void ClearImportedContent(RectTransform importedRoot)
        {
            for (var i = importedRoot.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(importedRoot.GetChild(i).gameObject);
            }
        }

        private static void BuildRepeatedItemPrefabs(FigunityDocument document, FigunitySettingsAsset settings)
        {
            var groups = new Dictionary<string, List<FigunityNode>>();
            for (var i = 0; i < document.frames.Count; i++)
            {
                CollectRepeats(document.frames[i]?.tree, groups);
            }

            foreach (var group in groups)
            {
                if (group.Value.Count < 2)
                {
                    continue;
                }

                var node = group.Value[0];
                var screen = new FigunityScreen
                {
                    key = group.Key,
                    slug = group.Key,
                    frameName = node.name,
                    width = Mathf.Max(1f, node.bounds.width),
                    height = Mathf.Max(1f, node.bounds.height),
                    tree = node
                };

                var prefabName = "PF_Repeated_" + ToPrefabToken(group.Key);
                var prefabPath = settings.repeatedPrefabFolder.TrimEnd('/', '\\') + "/" + prefabName + ".prefab";
                var root = new GameObject(prefabName, typeof(RectTransform), typeof(CanvasGroup));
                try
                {
                    var rect = root.GetComponent<RectTransform>();
                    ConfigureFullRect(rect, new Vector2(screen.width, screen.height));
                    new FigunityUgUiComposer().ComposeScreen(screen, root.transform, new FigunityFrameOptions
                    {
                        settings = settings,
                        rootName = ToPrefabToken(group.Key),
                        active = true,
                        includeRootBackground = true
                    });
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        private static void CollectRepeats(FigunityNode node, Dictionary<string, List<FigunityNode>> groups)
        {
            if (node == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(node.repeatKey) && node.bounds.width >= 8f && node.bounds.height >= 8f)
            {
                if (!groups.TryGetValue(node.repeatKey, out var list))
                {
                    list = new List<FigunityNode>();
                    groups[node.repeatKey] = list;
                }

                list.Add(node);
            }

            if (node.children != null)
            {
                for (var i = 0; i < node.children.Count; i++)
                {
                    CollectRepeats(node.children[i], groups);
                }
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
    }
}
