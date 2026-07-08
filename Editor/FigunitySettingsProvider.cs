using UnityEditor;

namespace Figunity.Editor
{
    public static class FigunitySettingsProvider
    {
        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            return new SettingsProvider("Project/FIGUNITY", SettingsScope.Project)
            {
                label = "FIGUNITY",
                guiHandler = _ =>
                {
                    var settings = FigunitySettings.LoadOrCreate();
                    var serialized = new SerializedObject(settings);
                    serialized.Update();

                    EditorGUILayout.LabelField("Figma MCP", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(serialized.FindProperty("figmaWsPort"));
                    EditorGUILayout.PropertyField(serialized.FindProperty("expectedFileName"));
                    EditorGUILayout.PropertyField(serialized.FindProperty("rasterScale"));

                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Paths", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(serialized.FindProperty("frameConfigPath"));
                    EditorGUILayout.PropertyField(serialized.FindProperty("importFolder"));
                    EditorGUILayout.PropertyField(serialized.FindProperty("prefabFolder"));
                    EditorGUILayout.PropertyField(serialized.FindProperty("repeatedPrefabFolder"));
                    EditorGUILayout.PropertyField(serialized.FindProperty("diagnosticsPath"));
                    EditorGUILayout.PropertyField(serialized.FindProperty("testScenePath"));

                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Import Rules", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(serialized.FindProperty("applyAutoLayoutGroups"));
                    EditorGUILayout.PropertyField(serialized.FindProperty("createMasks"));
                    EditorGUILayout.PropertyField(serialized.FindProperty("createRoundedRectGraphics"));
                    EditorGUILayout.PropertyField(serialized.FindProperty("createRepeatedItemPrefabs"));
                    EditorGUILayout.PropertyField(serialized.FindProperty("writeDiagnostics"));
                    EditorGUILayout.PropertyField(serialized.FindProperty("preserveManualPrefabChildren"));

                    EditorGUILayout.Space();
                    EditorGUILayout.PropertyField(serialized.FindProperty("fontMappings"), true);

                    serialized.ApplyModifiedProperties();
                }
            };
        }
    }
}
