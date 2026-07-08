using System;
using System.Collections.Generic;
using System.IO;
using Figunity.Runtime;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Figunity.Editor
{
    public static class FigunityElementUpdater
    {
        private static readonly HashSet<string> GeneratedChildNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Viewport",
            "Content",
            "Track",
            "Fill Area",
            "Fill",
            "Handle Slide Area",
            "Handle",
            "Checkmark",
            "Text Area",
            "Placeholder",
            "Text",
            "Label"
        };

        private static readonly Type[] GeneratedComponentTypes =
        {
            typeof(FigunityImportedNode),
            typeof(FigunityRoundedRectGraphic),
            typeof(FigunityEllipseGraphic),
            typeof(FigunityToggleSwitch),
            typeof(TextMeshProUGUI),
            typeof(Image),
            typeof(RawImage),
            typeof(Button),
            typeof(Toggle),
            typeof(ToggleGroup),
            typeof(Slider),
            typeof(ScrollRect),
            typeof(TMP_InputField),
            typeof(TMP_Dropdown),
            typeof(LayoutElement),
            typeof(RectMask2D),
            typeof(Mask),
            typeof(HorizontalLayoutGroup),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter)
        };

        [MenuItem("Tools/FIGUNITY/Update Selected Element From Payload")]
        public static void UpdateSelectedElementFromPayloadMenu()
        {
            try
            {
                var result = UpdateSelectedElementFromPayload();
                Debug.Log("FIGUNITY updated selected element '" + result.targetName + "' from payload (updated=" + result.updatedObjects + ", created=" + result.createdObjects + ", removed=" + result.removedObjects + ").");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("FIGUNITY", exception.Message, "OK");
            }
        }

        [MenuItem("Tools/FIGUNITY/Update Selected Element From Payload", true)]
        public static bool CanUpdateSelectedElementFromPayload()
        {
            var selected = Selection.activeGameObject;
            return selected != null && selected.GetComponent<FigunityImportedNode>() != null;
        }

        public static FigunityElementUpdateResult UpdateSelectedElementFromPayload()
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                throw new InvalidOperationException("Select an imported FIGUNITY element first.");
            }

            return UpdateElementFromPayload(selected);
        }

        public static FigunityElementUpdateResult UpdateElementFromPayload(GameObject target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            var settings = FigunitySettings.LoadOrCreate();
            var document = FigunityPrefabWriter.ReadPayload(settings);
            FigunityTextureImporter.ConfigureElementTextures(settings.importFolder);

            if (PrefabUtility.IsPartOfPrefabAsset(target))
            {
                return UpdatePrefabAssetElement(target, document, settings);
            }

            var result = UpdateElement(target, document, settings, true);
            PersistSceneOrStageUpdate(target);
            AssetDatabase.SaveAssets();
            return result;
        }

        public static FigunityElementUpdateResult UpdateElement(GameObject target, FigunityDocument document, FigunitySettingsAsset settings)
        {
            return UpdateElement(target, document, settings, false);
        }

        private static FigunityElementUpdateResult UpdatePrefabAssetElement(GameObject selectedAssetObject, FigunityDocument document, FigunitySettingsAsset settings)
        {
            var metadata = selectedAssetObject.GetComponent<FigunityImportedNode>();
            if (metadata == null || string.IsNullOrWhiteSpace(metadata.FigmaId))
            {
                throw new InvalidOperationException("Selected prefab asset object has no FIGUNITY metadata.");
            }

            var prefabPath = AssetDatabase.GetAssetPath(selectedAssetObject);
            if (string.IsNullOrWhiteSpace(prefabPath) || !File.Exists(FigunitySettings.ProjectAbsolutePath(prefabPath)))
            {
                throw new InvalidOperationException("Selected object is not a prefab asset saved in this project.");
            }

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var target = FindImportedObject(root.transform, metadata.FigmaId);
                if (target == null)
                {
                    throw new InvalidOperationException("Could not find selected FIGUNITY element inside prefab asset: " + metadata.FigmaId);
                }

                var result = UpdateElement(target, document, settings, false);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                AssetDatabase.SaveAssets();
                return result;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static FigunityElementUpdateResult UpdateElement(GameObject target, FigunityDocument document, FigunitySettingsAsset settings, bool registerUndo)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (settings == null) settings = FigunitySettings.LoadOrCreate();

            var metadata = target.GetComponent<FigunityImportedNode>();
            if (metadata == null)
            {
                throw new InvalidOperationException("Selected object is not a FIGUNITY imported element.");
            }

            var match = FindNode(document, metadata);
            if (match == null)
            {
                throw new InvalidOperationException("Could not find Figma node in the latest payload: " + metadata.FigmaId);
            }

            var tempHost = new GameObject("FIGUNITY Update Temp", typeof(RectTransform));
            tempHost.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var parentBounds = match.parent != null
                    ? match.parent.bounds
                    : new FigunityBounds(0f, 0f, Mathf.Max(1f, match.screen.width), Mathf.Max(1f, match.screen.height));

                var composed = new FigunityUgUiComposer().ComposeSubtree(match.node, tempHost.transform, parentBounds, new FigunityFrameOptions
                {
                    settings = settings,
                    active = true,
                    includeRootBackground = true
                });

                if (composed.root == null)
                {
                    throw new InvalidOperationException("Selected Figma node is skipped by the current import rules and cannot be applied in place.");
                }

                if (registerUndo)
                {
                    Undo.RegisterFullObjectHierarchyUndo(target, "FIGUNITY Update Selected Element");
                }

                var context = new MergeContext(registerUndo);
                PrepareHierarchy(composed.root.gameObject, target, context);
                CopyPreparedHierarchy(composed.root.gameObject, target, context);
                MarkHierarchyDirty(target);

                return new FigunityElementUpdateResult
                {
                    targetName = target.name,
                    figmaId = match.node.id,
                    updatedObjects = context.updatedObjects,
                    createdObjects = context.createdObjects,
                    removedObjects = context.removedObjects
                };
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tempHost);
            }
        }

        private static void PrepareHierarchy(GameObject source, GameObject target, MergeContext context)
        {
            context.objectMap[source] = target;
            context.objectMap[source.transform] = target.transform;
            target.name = source.name;
            target.SetActive(source.activeSelf);
            context.updatedObjects++;

            EnsureComponentShells(source, target, context);

            var usedTargets = new HashSet<Transform>();
            for (var i = 0; i < source.transform.childCount; i++)
            {
                var sourceChild = source.transform.GetChild(i);
                var targetChild = FindMatchingChild(sourceChild, target.transform, usedTargets);
                if (targetChild == null)
                {
                    var childObject = new GameObject(sourceChild.name, typeof(RectTransform));
                    if (context.registerUndo)
                    {
                        Undo.RegisterCreatedObjectUndo(childObject, "FIGUNITY Create Imported Child");
                    }

                    targetChild = childObject.transform;
                    targetChild.SetParent(target.transform, false);
                    context.createdObjects++;
                }

                usedTargets.Add(targetChild);
                targetChild.SetSiblingIndex(i);
                PrepareHierarchy(sourceChild.gameObject, targetChild.gameObject, context);
            }

            for (var i = target.transform.childCount - 1; i >= 0; i--)
            {
                var child = target.transform.GetChild(i);
                if (usedTargets.Contains(child))
                {
                    continue;
                }

                if (!ShouldRemoveUnmatchedChild(child))
                {
                    continue;
                }

                context.removedObjects++;
                if (context.registerUndo)
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        private static void EnsureComponentShells(GameObject source, GameObject target, MergeContext context)
        {
            for (var i = 0; i < GeneratedComponentTypes.Length; i++)
            {
                var type = GeneratedComponentTypes[i];
                var sourceComponents = source.GetComponents(type);
                var targetComponents = target.GetComponents(type);

                while (targetComponents.Length > sourceComponents.Length)
                {
                    var last = targetComponents[targetComponents.Length - 1];
                    if (context.registerUndo)
                    {
                        Undo.DestroyObjectImmediate(last);
                    }
                    else
                    {
                        UnityEngine.Object.DestroyImmediate(last);
                    }

                    targetComponents = target.GetComponents(type);
                }

                while (targetComponents.Length < sourceComponents.Length)
                {
                    if (context.registerUndo)
                    {
                        Undo.AddComponent(target, type);
                    }
                    else
                    {
                        target.AddComponent(type);
                    }

                    targetComponents = target.GetComponents(type);
                }

                for (var c = 0; c < sourceComponents.Length; c++)
                {
                    context.objectMap[sourceComponents[c]] = targetComponents[c];
                }
            }
        }

        private static void CopyPreparedHierarchy(GameObject source, GameObject target, MergeContext context)
        {
            CopyRectTransform(source.GetComponent<RectTransform>(), target.GetComponent<RectTransform>());

            for (var i = 0; i < GeneratedComponentTypes.Length; i++)
            {
                var type = GeneratedComponentTypes[i];
                var sourceComponents = source.GetComponents(type);
                var targetComponents = target.GetComponents(type);
                var count = Mathf.Min(sourceComponents.Length, targetComponents.Length);
                for (var c = 0; c < count; c++)
                {
                    EditorUtility.CopySerialized(sourceComponents[c], targetComponents[c]);
                    RemapObjectReferences(targetComponents[c], context);
                    EditorUtility.SetDirty(targetComponents[c]);
                }
            }

            for (var i = 0; i < source.transform.childCount; i++)
            {
                var sourceChild = source.transform.GetChild(i);
                if (context.objectMap.TryGetValue(sourceChild.gameObject, out var mapped) && mapped is GameObject targetChild)
                {
                    CopyPreparedHierarchy(sourceChild.gameObject, targetChild, context);
                }
            }
        }

        private static void CopyRectTransform(RectTransform source, RectTransform target)
        {
            if (source == null || target == null)
            {
                return;
            }

            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.anchoredPosition = source.anchoredPosition;
            target.sizeDelta = source.sizeDelta;
            target.offsetMin = source.offsetMin;
            target.offsetMax = source.offsetMax;
            target.localRotation = source.localRotation;
            target.localScale = source.localScale;
            EditorUtility.SetDirty(target);
        }

        private static void RemapObjectReferences(Component targetComponent, MergeContext context)
        {
            if (targetComponent == null)
            {
                return;
            }

            var serialized = new SerializedObject(targetComponent);
            var property = serialized.GetIterator();
            var enterChildren = true;
            while (property.Next(enterChildren))
            {
                enterChildren = false;
                if (property.propertyType != SerializedPropertyType.ObjectReference)
                {
                    continue;
                }

                var reference = property.objectReferenceValue;
                if (reference == null)
                {
                    continue;
                }

                if (context.objectMap.TryGetValue(reference, out var mapped))
                {
                    property.objectReferenceValue = mapped;
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform FindMatchingChild(Transform sourceChild, Transform targetParent, HashSet<Transform> usedTargets)
        {
            var sourceMetadata = sourceChild.GetComponent<FigunityImportedNode>();
            if (sourceMetadata != null && !string.IsNullOrWhiteSpace(sourceMetadata.FigmaId))
            {
                for (var i = 0; i < targetParent.childCount; i++)
                {
                    var candidate = targetParent.GetChild(i);
                    if (usedTargets.Contains(candidate))
                    {
                        continue;
                    }

                    var candidateMetadata = candidate.GetComponent<FigunityImportedNode>();
                    if (candidateMetadata != null && string.Equals(candidateMetadata.FigmaId, sourceMetadata.FigmaId, StringComparison.Ordinal))
                    {
                        return candidate;
                    }
                }
            }

            for (var i = 0; i < targetParent.childCount; i++)
            {
                var candidate = targetParent.GetChild(i);
                if (usedTargets.Contains(candidate))
                {
                    continue;
                }

                if (string.Equals(candidate.name, sourceChild.name, StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static bool ShouldRemoveUnmatchedChild(Transform child)
        {
            return child.GetComponent<FigunityImportedNode>() != null ||
                   GeneratedChildNames.Contains(child.name);
        }

        private static GameObject FindImportedObject(Transform root, string figmaId)
        {
            if (root == null || string.IsNullOrWhiteSpace(figmaId))
            {
                return null;
            }

            var metadata = root.GetComponent<FigunityImportedNode>();
            if (metadata != null && string.Equals(metadata.FigmaId, figmaId, StringComparison.Ordinal))
            {
                return root.gameObject;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindImportedObject(root.GetChild(i), figmaId);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static NodeMatch FindNode(FigunityDocument document, FigunityImportedNode metadata)
        {
            if (document?.frames == null || metadata == null)
            {
                return null;
            }

            for (var i = 0; i < document.frames.Count; i++)
            {
                var screen = document.frames[i];
                var byId = FindNodeRecursive(screen, screen?.tree, null, metadata.FigmaId, null);
                if (byId != null)
                {
                    return byId;
                }
            }

            if (string.IsNullOrWhiteSpace(metadata.SourcePath))
            {
                return null;
            }

            for (var i = 0; i < document.frames.Count; i++)
            {
                var screen = document.frames[i];
                var byPath = FindNodeRecursive(screen, screen?.tree, null, null, metadata.SourcePath);
                if (byPath != null)
                {
                    return byPath;
                }
            }

            return null;
        }

        private static NodeMatch FindNodeRecursive(FigunityScreen screen, FigunityNode node, FigunityNode parent, string id, string sourcePath)
        {
            if (screen == null || node == null)
            {
                return null;
            }

            if ((!string.IsNullOrWhiteSpace(id) && string.Equals(node.id, id, StringComparison.Ordinal)) ||
                (!string.IsNullOrWhiteSpace(sourcePath) && string.Equals(node.path, sourcePath, StringComparison.Ordinal)))
            {
                return new NodeMatch(screen, parent, node);
            }

            if (node.children == null)
            {
                return null;
            }

            for (var i = 0; i < node.children.Count; i++)
            {
                var found = FindNodeRecursive(screen, node.children[i], node, id, sourcePath);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static void PersistSceneOrStageUpdate(GameObject target)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && target.transform.IsChildOf(stage.prefabContentsRoot.transform))
            {
                MarkHierarchyDirty(stage.prefabContentsRoot);
                EditorSceneManager.MarkSceneDirty(stage.scene);
                if (!string.IsNullOrWhiteSpace(stage.assetPath))
                {
                    PrefabUtility.SaveAsPrefabAsset(stage.prefabContentsRoot, stage.assetPath);
                }

                return;
            }

            MarkHierarchyDirty(target);
            if (target.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(target.scene);
            }
        }

        private static void MarkHierarchyDirty(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                EditorUtility.SetDirty(transforms[i].gameObject);
                PrefabUtility.RecordPrefabInstancePropertyModifications(transforms[i].gameObject);
                var components = transforms[i].GetComponents<Component>();
                for (var c = 0; c < components.Length; c++)
                {
                    if (components[c] == null)
                    {
                        continue;
                    }

                    EditorUtility.SetDirty(components[c]);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(components[c]);
                }
            }
        }

        private sealed class NodeMatch
        {
            public readonly FigunityScreen screen;
            public readonly FigunityNode parent;
            public readonly FigunityNode node;

            public NodeMatch(FigunityScreen screen, FigunityNode parent, FigunityNode node)
            {
                this.screen = screen;
                this.parent = parent;
                this.node = node;
            }
        }

        private sealed class MergeContext
        {
            public readonly bool registerUndo;
            public readonly Dictionary<UnityEngine.Object, UnityEngine.Object> objectMap = new Dictionary<UnityEngine.Object, UnityEngine.Object>();
            public int updatedObjects;
            public int createdObjects;
            public int removedObjects;

            public MergeContext(bool registerUndo)
            {
                this.registerUndo = registerUndo;
            }
        }
    }

    public struct FigunityElementUpdateResult
    {
        public string targetName;
        public string figmaId;
        public int updatedObjects;
        public int createdObjects;
        public int removedObjects;
    }
}
