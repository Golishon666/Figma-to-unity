using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Figunity.Editor
{
    public sealed class FigunityImporterWindow : EditorWindow
    {
        private const string TemporaryFrameConfigName = "figunity.window.frames.json";
        private static readonly string[] Tabs = { "Import", "Settings" };

        private readonly HashSet<string> selectedNodeIds = new HashSet<string>();
        private FigunitySettingsAsset settings;
        private SerializedObject serializedSettings;
        private FigunityFigmaPanelList panelList;
        private Vector2 panelScroll;
        private Vector2 settingsScroll;
        private int selectedTab;
        private string status = "Refresh panels from Figma to begin.";

        [MenuItem("Tools/FIGUNITY/Importer Panel")]
        public static void Open()
        {
            var window = GetWindow<FigunityImporterWindow>("FIGUNITY Importer");
            window.minSize = new Vector2(560f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            LoadSettings();
        }

        private void OnGUI()
        {
            if (settings == null || serializedSettings == null)
            {
                LoadSettings();
            }

            selectedTab = GUILayout.Toolbar(selectedTab, Tabs, GUILayout.Height(28f));
            EditorGUILayout.Space(8f);

            if (selectedTab == 0)
            {
                DrawImportTab();
            }
            else
            {
                DrawSettingsTab();
            }
        }

        private void LoadSettings()
        {
            settings = FigunitySettings.LoadOrCreate();
            serializedSettings = new SerializedObject(settings);
        }

        private void DrawImportTab()
        {
            DrawToolbar();
            EditorGUILayout.Space(8f);
            DrawStatus();
            EditorGUILayout.Space(8f);
            DrawPanelList();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Refresh Panels", EditorStyles.toolbarButton, GUILayout.Width(110f)))
                {
                    RefreshPanels();
                }

                GUILayout.Space(8f);
                if (GUILayout.Button("Select All", EditorStyles.toolbarButton, GUILayout.Width(80f)))
                {
                    SelectAllPanels();
                }

                if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                {
                    selectedNodeIds.Clear();
                }

                GUILayout.FlexibleSpace();

                var selectedCount = SelectedPanels().Count;
                EditorGUILayout.LabelField(selectedCount + " selected", GUILayout.Width(84f));

                using (new EditorGUI.DisabledScope(selectedCount == 0))
                {
                    if (GUILayout.Button("Import Selected", EditorStyles.toolbarButton, GUILayout.Width(120f)))
                    {
                        ImportPanels(SelectedPanels());
                    }
                }

                using (new EditorGUI.DisabledScope(panelList == null || panelList.panels == null || panelList.panels.Count == 0))
                {
                    if (GUILayout.Button("Import All", EditorStyles.toolbarButton, GUILayout.Width(88f)))
                    {
                        ImportPanels(panelList.panels);
                    }
                }
            }
        }

        private void DrawStatus()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var fileName = panelList != null && !string.IsNullOrWhiteSpace(panelList.currentFileName)
                    ? panelList.currentFileName
                    : "not loaded";
                var count = panelList?.panels?.Count ?? 0;
                EditorGUILayout.LabelField("Figma file", fileName);
                EditorGUILayout.LabelField("Panels", count.ToString());
                EditorGUILayout.LabelField("Status", status);
            }
        }

        private void DrawPanelList()
        {
            if (panelList == null || panelList.panels == null || panelList.panels.Count == 0)
            {
                EditorGUILayout.HelpBox("No panels loaded. Click Refresh Panels while Figma Desktop Bridge is connected.", MessageType.Info);
                return;
            }

            panelScroll = EditorGUILayout.BeginScrollView(panelScroll);
            for (var i = 0; i < panelList.panels.Count; i++)
            {
                DrawPanelRow(panelList.panels[i], i);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawPanelRow(FigunityFigmaPanelItem panel, int index)
        {
            var selected = selectedNodeIds.Contains(panel.nodeId);
            var row = GUILayoutUtility.GetRect(1f, 46f, GUILayout.ExpandWidth(true));
            var background = selected
                ? new Color(0.18f, 0.43f, 0.82f, 0.42f)
                : (index % 2 == 0 ? new Color(0.18f, 0.18f, 0.18f, 0.18f) : new Color(0f, 0f, 0f, 0.08f));
            EditorGUI.DrawRect(row, background);

            var toggleRect = new Rect(row.x + 8f, row.y + 13f, 18f, 18f);
            var nextSelected = GUI.Toggle(toggleRect, selected, GUIContent.none);
            if (nextSelected != selected)
            {
                SetSelected(panel, nextSelected);
            }

            var titleRect = new Rect(row.x + 34f, row.y + 5f, row.width - 42f, 18f);
            var detailRect = new Rect(row.x + 34f, row.y + 24f, row.width - 42f, 16f);
            var titleStyle = selected ? EditorStyles.whiteBoldLabel : EditorStyles.boldLabel;
            var detailStyle = selected ? EditorStyles.whiteMiniLabel : EditorStyles.miniLabel;
            GUI.Label(titleRect, panel.DisplayName, titleStyle);
            GUI.Label(detailRect, panel.Detail + " | " + panel.nodeId, detailStyle);

            var current = Event.current;
            if (current.type == EventType.MouseDown && current.button == 0 && row.Contains(current.mousePosition) && !toggleRect.Contains(current.mousePosition))
            {
                SetSelected(panel, !selectedNodeIds.Contains(panel.nodeId));
                current.Use();
                Repaint();
            }
        }

        private void DrawSettingsTab()
        {
            serializedSettings.Update();
            settingsScroll = EditorGUILayout.BeginScrollView(settingsScroll);

            EditorGUILayout.LabelField("Figma MCP", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("figmaWsPort"));
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("expectedFileName"));
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("rasterScale"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Paths", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("frameConfigPath"));
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("importFolder"));
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("prefabFolder"));
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("repeatedPrefabFolder"));
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("diagnosticsPath"));
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("testScenePath"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Import Rules", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("applyAutoLayoutGroups"));
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("createMasks"));
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("createRoundedRectGraphics"));
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("createRepeatedItemPrefabs"));
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("writeDiagnostics"));
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("preserveManualPrefabChildren"));

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serializedSettings.FindProperty("fontMappings"), true);

            EditorGUILayout.EndScrollView();

            if (serializedSettings.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }
        }

        private void RefreshPanels()
        {
            try
            {
                EditorUtility.DisplayProgressBar("FIGUNITY", "Reading Figma panels...", 0.35f);
                panelList = FigunityFigmaPanelBrowser.ListPanels(settings);
                PruneSelection();
                status = "Loaded " + (panelList.panels?.Count ?? 0) + " panel(s).";
            }
            catch (Exception exception)
            {
                status = "Refresh failed: " + exception.Message;
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("FIGUNITY", status, "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Repaint();
            }
        }

        private void ImportPanels(IReadOnlyList<FigunityFigmaPanelItem> panels)
        {
            if (panels == null || panels.Count == 0)
            {
                EditorUtility.DisplayDialog("FIGUNITY", "No panels selected.", "OK");
                return;
            }

            try
            {
                EditorUtility.DisplayProgressBar("FIGUNITY", "Preparing frame config...", 0.1f);
                var configPath = WriteTemporaryFrameConfig(panels);

                EditorUtility.DisplayProgressBar("FIGUNITY", "Exporting selected panels from Figma...", 0.35f);
                FigunityMcpExporter.Export(settings, configPath);

                EditorUtility.DisplayProgressBar("FIGUNITY", "Rebuilding prefabs...", 0.75f);
                FigunityPrefabWriter.RebuildPrefabsFromPayload(settings);

                status = "Imported " + panels.Count + " panel(s).";
                Debug.Log("FIGUNITY imported " + panels.Count + " panel(s) from Importer Panel.");
            }
            catch (Exception exception)
            {
                status = "Import failed: " + exception.Message;
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("FIGUNITY", status, "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Repaint();
            }
        }

        private string WriteTemporaryFrameConfig(IReadOnlyList<FigunityFigmaPanelItem> panels)
        {
            FigunitySettings.EnsureUnityFolder(settings.importFolder);

            var config = new FrameConfig
            {
                expectedFileName = settings.expectedFileName,
                frames = new List<FrameConfigEntry>()
            };

            var usedSlugs = new HashSet<string>();
            for (var i = 0; i < panels.Count; i++)
            {
                var panel = panels[i];
                var slug = UniqueSlug(SafeToken(string.IsNullOrWhiteSpace(panel.slug) ? panel.name : panel.slug), usedSlugs);
                config.frames.Add(new FrameConfigEntry
                {
                    key = slug,
                    slug = slug,
                    name = panel.name,
                    nodeId = panel.nodeId
                });
            }

            var unityPath = settings.importFolder.TrimEnd('/', '\\') + "/" + TemporaryFrameConfigName;
            var absolutePath = FigunitySettings.ProjectAbsolutePath(unityPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
            File.WriteAllText(absolutePath, JsonUtility.ToJson(config, true));
            AssetDatabase.ImportAsset(unityPath, ImportAssetOptions.ForceSynchronousImport);
            return unityPath;
        }

        private void SelectAllPanels()
        {
            selectedNodeIds.Clear();
            if (panelList?.panels == null)
            {
                return;
            }

            for (var i = 0; i < panelList.panels.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(panelList.panels[i].nodeId))
                {
                    selectedNodeIds.Add(panelList.panels[i].nodeId);
                }
            }
        }

        private List<FigunityFigmaPanelItem> SelectedPanels()
        {
            if (panelList?.panels == null)
            {
                return new List<FigunityFigmaPanelItem>();
            }

            return panelList.panels
                .Where(panel => panel != null && selectedNodeIds.Contains(panel.nodeId))
                .ToList();
        }

        private void SetSelected(FigunityFigmaPanelItem panel, bool selected)
        {
            if (panel == null || string.IsNullOrWhiteSpace(panel.nodeId))
            {
                return;
            }

            if (selected)
            {
                selectedNodeIds.Add(panel.nodeId);
            }
            else
            {
                selectedNodeIds.Remove(panel.nodeId);
            }
        }

        private void PruneSelection()
        {
            if (panelList?.panels == null)
            {
                selectedNodeIds.Clear();
                return;
            }

            var currentIds = new HashSet<string>(panelList.panels.Select(panel => panel.nodeId));
            selectedNodeIds.RemoveWhere(nodeId => !currentIds.Contains(nodeId));
        }

        private static string UniqueSlug(string baseSlug, HashSet<string> usedSlugs)
        {
            baseSlug = string.IsNullOrWhiteSpace(baseSlug) ? "panel" : baseSlug;
            var slug = baseSlug;
            var index = 2;
            while (!usedSlugs.Add(slug))
            {
                slug = baseSlug + "-" + index.ToString("00");
                index++;
            }

            return slug;
        }

        private static string SafeToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "panel";
            }

            var result = string.Empty;
            var lastWasDash = false;
            for (var i = 0; i < value.Length; i++)
            {
                var ch = char.ToLowerInvariant(value[i]);
                if (char.IsLetterOrDigit(ch))
                {
                    result += ch;
                    lastWasDash = false;
                }
                else if (!lastWasDash && result.Length > 0)
                {
                    result += "-";
                    lastWasDash = true;
                }
            }

            result = result.Trim('-');
            if (result.Length > 54)
            {
                result = result.Substring(0, 54).Trim('-');
            }

            return string.IsNullOrWhiteSpace(result) ? "panel" : result;
        }

        [Serializable]
        private sealed class FrameConfig
        {
            public string expectedFileName;
            public List<FrameConfigEntry> frames;
        }

        [Serializable]
        private sealed class FrameConfigEntry
        {
            public string key;
            public string slug;
            public string name;
            public string nodeId;
        }
    }
}
