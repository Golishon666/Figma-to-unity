using UnityEditor;

namespace Figunity.Editor
{
    public static class FigunityMenu
    {
        [MenuItem("Tools/FIGUNITY/Export From Figma via figma-console-mcp")]
        public static void ExportFromFigma()
        {
            FigunityMcpExporter.Export();
        }

        [MenuItem("Tools/FIGUNITY/Rebuild Prefabs From Payload")]
        public static void RebuildPrefabs()
        {
            FigunityPrefabWriter.RebuildPrefabsFromPayload();
        }

        [MenuItem("Tools/FIGUNITY/Export And Rebuild")]
        public static void ExportAndRebuild()
        {
            FigunityMcpExporter.Export();
            FigunityPrefabWriter.RebuildPrefabsFromPayload();
        }
    }
}
