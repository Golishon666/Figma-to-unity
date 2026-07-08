using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Figunity.Editor
{
    public sealed class FigunitySettingsAsset : ScriptableObject
    {
        public string figmaWsPort = "9225";
        public string expectedFileName = string.Empty;
        public string frameConfigPath = "Assets/FIGUNITY/figunity.frames.json";
        public string importFolder = "Assets/FIGUNITY/Imports";
        public string prefabFolder = "Assets/FIGUNITY/Prefabs";
        public string repeatedPrefabFolder = "Assets/FIGUNITY/Prefabs/Repeated";
        public string diagnosticsPath = "Assets/FIGUNITY/Imports/diagnostics.md";
        public string testScenePath = "Assets/FIGUNITY/TestScene/FigunityTestMenus.unity";
        public int rasterScale = 2;
        public bool applyAutoLayoutGroups = true;
        public bool createMasks = true;
        public bool createRoundedRectGraphics = true;
        public bool createRepeatedItemPrefabs = true;
        public bool writeDiagnostics = true;
        public bool preserveManualPrefabChildren = true;
        public List<FigunityFontMapEntry> fontMappings = new List<FigunityFontMapEntry>();
    }

    [Serializable]
    public sealed class FigunityFontMapEntry
    {
        public string figmaFamily;
        public string figmaStyle;
        public TMP_FontAsset fontAsset;
    }
}
