using System;
using System.Collections.Generic;
using UnityEngine;

namespace Figunity.Editor
{
    public static class FigunityFigmaPanelBrowser
    {
        private const int TimeoutMilliseconds = 240000;

        public static FigunityFigmaPanelList ListPanels(FigunitySettingsAsset settings)
        {
            var stdout = FigunityMcpExporter.RunNodeWorker(
                settings,
                "figunity-list-frames.mjs",
                null,
                TimeoutMilliseconds,
                "FIGUNITY panel list");

            var result = JsonUtility.FromJson<FigunityFigmaPanelList>(stdout);
            if (result == null)
            {
                throw new InvalidOperationException("FIGUNITY panel list returned empty JSON.");
            }

            if (result.panels == null)
            {
                result.panels = new List<FigunityFigmaPanelItem>();
            }

            return result;
        }
    }

    [Serializable]
    public sealed class FigunityFigmaPanelList
    {
        public string currentFileName;
        public string currentFileKey;
        public string currentPageName;
        public List<FigunityFigmaPanelItem> panels = new List<FigunityFigmaPanelItem>();
    }

    [Serializable]
    public sealed class FigunityFigmaPanelItem
    {
        public string nodeId;
        public string name;
        public string pageName;
        public string path;
        public string type;
        public string key;
        public string slug;
        public float x;
        public float y;
        public float width;
        public float height;

        public string DisplayName => string.IsNullOrWhiteSpace(name) ? nodeId : name;
        public string Detail => pageName + " | " + type + " | " + Mathf.RoundToInt(width) + "x" + Mathf.RoundToInt(height);
    }
}
