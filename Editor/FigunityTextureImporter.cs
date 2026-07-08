using System;
using UnityEditor;
using UnityEngine;

namespace Figunity.Editor
{
    public static class FigunityTextureImporter
    {
        public static int ConfigureElementTextures(string importFolder)
        {
            var folder = NormalizeFolder(importFolder).TrimEnd('/') + "/ElementAssets";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                return 0;
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var changed = 0;
            var textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            for (var i = 0; i < textureGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(textureGuids[i]);
                if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null || !ApplyUiSettings(importer))
                {
                    continue;
                }

                importer.SaveAndReimport();
                changed++;
            }

            if (changed > 0)
            {
                Debug.Log("FIGUNITY configured " + changed + " UI texture importer(s).");
            }

            return changed;
        }

        private static string NormalizeFolder(string importFolder)
        {
            return string.IsNullOrWhiteSpace(importFolder)
                ? FigunityMcpExporter.ImportFolder
                : importFolder.Replace('\\', '/');
        }

        private static bool ApplyUiSettings(TextureImporter importer)
        {
            var changed = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                changed = true;
            }

            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                changed = true;
            }

            if (!importer.sRGBTexture)
            {
                importer.sRGBTexture = true;
                changed = true;
            }

            if (importer.filterMode != FilterMode.Bilinear)
            {
                importer.filterMode = FilterMode.Bilinear;
                changed = true;
            }

            if (importer.wrapMode != TextureWrapMode.Clamp)
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                changed = true;
            }

            if (importer.npotScale != TextureImporterNPOTScale.None)
            {
                importer.npotScale = TextureImporterNPOTScale.None;
                changed = true;
            }

            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                changed = true;
            }

            if (importer.maxTextureSize < 4096)
            {
                importer.maxTextureSize = 4096;
                changed = true;
            }

            var defaultSettings = importer.GetDefaultPlatformTextureSettings();
            if (defaultSettings.maxTextureSize < 4096)
            {
                defaultSettings.maxTextureSize = 4096;
                changed = true;
            }

            if (defaultSettings.textureCompression != TextureImporterCompression.Uncompressed)
            {
                defaultSettings.textureCompression = TextureImporterCompression.Uncompressed;
                changed = true;
            }

            if (changed)
            {
                importer.SetPlatformTextureSettings(defaultSettings);
            }

            ConfigurePlatform(importer, "Standalone", ref changed);
            ConfigurePlatform(importer, "Android", ref changed);
            ConfigurePlatform(importer, "WebGL", ref changed);
            ConfigurePlatform(importer, "iOS", ref changed);

            return changed;
        }

        private static void ConfigurePlatform(TextureImporter importer, string buildTarget, ref bool changed)
        {
            var settings = importer.GetPlatformTextureSettings(buildTarget);
            var platformChanged = false;
            if (!settings.overridden)
            {
                settings.overridden = true;
                platformChanged = true;
            }

            if (settings.maxTextureSize < 4096)
            {
                settings.maxTextureSize = 4096;
                platformChanged = true;
            }

            if (settings.textureCompression != TextureImporterCompression.Uncompressed)
            {
                settings.textureCompression = TextureImporterCompression.Uncompressed;
                platformChanged = true;
            }

            if (settings.crunchedCompression)
            {
                settings.crunchedCompression = false;
                platformChanged = true;
            }

            if (!platformChanged)
            {
                return;
            }

            importer.SetPlatformTextureSettings(settings);
            changed = true;
        }
    }
}
