using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Figunity.Editor
{
    public static class FigunityTestSceneBuilder
    {
        public static void BuildScene(FigunitySettingsAsset settings)
        {
            if (settings == null) settings = FigunitySettings.LoadOrCreate();
            var sceneFolder = Path.GetDirectoryName(settings.testScenePath)?.Replace("\\", "/");
            if (!string.IsNullOrWhiteSpace(sceneFolder))
            {
                FigunitySettings.EnsureUnityFolder(sceneFolder);
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = Path.GetFileNameWithoutExtension(settings.testScenePath);

            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var content = new GameObject("FIGUNITY Test Imports", typeof(RectTransform));
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.SetParent(canvasGo.transform, false);
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(0f, 1f);
            contentRect.pivot = new Vector2(0f, 1f);
            contentRect.anchoredPosition = new Vector2(48f, -48f);
            contentRect.sizeDelta = new Vector2(1824f, 980f);

            var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { settings.prefabFolder });
            var x = 0f;
            var y = 0f;
            var rowHeight = 0f;
            for (var i = 0; i < prefabs.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(prefabs[i]);
                if (path.Contains("/Repeated/"))
                {
                    continue;
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    continue;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.name = prefab.name;
                instance.transform.SetParent(contentRect, false);
                var rect = instance.GetComponent<RectTransform>();
                if (rect != null)
                {
                    if (x + rect.sizeDelta.x > contentRect.sizeDelta.x && x > 0f)
                    {
                        x = 0f;
                        y += rowHeight + 32f;
                        rowHeight = 0f;
                    }

                    rect.anchorMin = new Vector2(0f, 1f);
                    rect.anchorMax = new Vector2(0f, 1f);
                    rect.pivot = new Vector2(0f, 1f);
                    rect.anchoredPosition = new Vector2(x, -y);
                    x += rect.sizeDelta.x + 32f;
                    rowHeight = Mathf.Max(rowHeight, rect.sizeDelta.y);
                }
            }

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            EditorSceneManager.SaveScene(scene, settings.testScenePath);
            AssetDatabase.Refresh();
            Debug.Log("FIGUNITY test scene saved: " + settings.testScenePath);
        }
    }
}
