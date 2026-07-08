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
                    EditorGUILayout.LabelField("Figma MCP", EditorStyles.boldLabel);
                    FigunityMcpExporter.FigmaPort = EditorGUILayout.TextField("FIGMA_WS_PORT", FigunityMcpExporter.FigmaPort);
                    FigunityMcpExporter.ExpectedFileName = EditorGUILayout.TextField("Expected File Name", FigunityMcpExporter.ExpectedFileName);

                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Paths", EditorStyles.boldLabel);
                    EditorGUILayout.SelectableLabel("Frame config: " + FigunityMcpExporter.FrameConfigPath);
                    EditorGUILayout.SelectableLabel("Import folder: " + FigunityMcpExporter.ImportFolder);
                    EditorGUILayout.SelectableLabel("Prefab folder: " + FigunityPrefabWriter.PrefabFolder);
                }
            };
        }
    }
}
