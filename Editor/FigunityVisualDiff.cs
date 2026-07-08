using System;
using System.IO;
using UnityEngine;

namespace Figunity.Editor
{
    public static class FigunityVisualDiff
    {
        public static float Compare(string expectedPngPath, string actualPngPath)
        {
            if (string.IsNullOrWhiteSpace(expectedPngPath) || string.IsNullOrWhiteSpace(actualPngPath))
            {
                throw new ArgumentException("Both PNG paths are required.");
            }

            var expected = Load(expectedPngPath);
            var actual = Load(actualPngPath);
            var width = Mathf.Min(expected.width, actual.width);
            var height = Mathf.Min(expected.height, actual.height);
            if (width <= 0 || height <= 0)
            {
                return 1f;
            }

            var error = 0f;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var a = expected.GetPixel(x, y);
                    var b = actual.GetPixel(x, y);
                    error += Mathf.Abs(a.r - b.r);
                    error += Mathf.Abs(a.g - b.g);
                    error += Mathf.Abs(a.b - b.b);
                    error += Mathf.Abs(a.a - b.a);
                }
            }

            UnityEngine.Object.DestroyImmediate(expected);
            UnityEngine.Object.DestroyImmediate(actual);
            return error / (width * height * 4f);
        }

        private static Texture2D Load(string path)
        {
            var fullPath = path.StartsWith("Assets/", StringComparison.Ordinal)
                ? FigunitySettings.ProjectAbsolutePath(path)
                : path;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(File.ReadAllBytes(fullPath)))
            {
                throw new InvalidOperationException("Could not decode PNG: " + path);
            }

            return texture;
        }
    }
}
