using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Figunity.Editor
{
    [Serializable]
    public sealed class FigunityDocument
    {
        public string source;
        public string expectedFileName;
        public string currentFileName;
        public string currentFileKey;
        public string port;
        public string exportedAt;
        public List<FigunityScreen> frames = new List<FigunityScreen>();

        public static FigunityDocument Decode(string json)
        {
            return JsonConvert.DeserializeObject<FigunityDocument>(json);
        }

        public FigunityScreen Locate(string keyNameOrSlug)
        {
            if (frames == null || string.IsNullOrWhiteSpace(keyNameOrSlug))
            {
                return null;
            }

            for (var i = 0; i < frames.Count; i++)
            {
                var screen = frames[i];
                if (screen == null)
                {
                    continue;
                }

                if (string.Equals(screen.key, keyNameOrSlug, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(screen.slug, keyNameOrSlug, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(screen.frameName, keyNameOrSlug, StringComparison.OrdinalIgnoreCase))
                {
                    return screen;
                }
            }

            return null;
        }
    }

    [Serializable]
    public sealed class FigunityScreen
    {
        public string key;
        public string slug;
        public string frameId;
        public string frameName;
        public float width;
        public float height;
        public FigunityBounds rootBounds;
        public int nodeCount;
        public int textCount;
        public int visualCount;
        public FigunityNode tree;
        public List<FigunityRasterExport> exports = new List<FigunityRasterExport>();
        public string screenshotPath;
    }

    [Serializable]
    public sealed class FigunityNode
    {
        public string id;
        public string parentId;
        public string name;
        public string type;
        public int depth;
        public int siblingIndex;
        public string path;
        public string renderMode;
        public string controlHint;
        public string overrideHint;
        public string decisionReason;
        public bool clipsContent;
        public bool isMask;
        public string maskType;
        public float opacity = 1f;
        public string blendMode;
        public FigunityBounds bounds;
        public FigunityConstraints constraints;
        public FigunityAutoLayout autoLayout;
        public List<FigunityPaint> fills = new List<FigunityPaint>();
        public List<FigunityPaint> strokes = new List<FigunityPaint>();
        public float strokeWeight;
        public float cornerRadius;
        public bool isInstance;
        public string componentKey;
        public string componentName;
        public string repeatKey;
        public FigunityText text;
        public List<FigunityNode> children = new List<FigunityNode>();
        public string assetPath;

        public bool CarriesText =>
            string.Equals(type, "TEXT", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(renderMode, "text", StringComparison.OrdinalIgnoreCase);
    }

    [Serializable]
    public sealed class FigunityRasterExport
    {
        public string id;
        public string name;
        public string mode;
        public int index;
        public int scale;
        public int byteLength;
        public string error;
        public string assetPath;
    }

    [Serializable]
    public struct FigunityBounds
    {
        public float x;
        public float y;
        public float width;
        public float height;

        public FigunityBounds(float x, float y, float width, float height)
        {
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
        }

        public float Right => x + width;
        public float Bottom => y + height;
        public Vector2 Size => new Vector2(width, height);
        public Vector2 Midpoint => new Vector2(x + width * 0.5f, y + height * 0.5f);

        public bool HoldsCenterOf(FigunityBounds other)
        {
            var point = other.Midpoint;
            return point.x >= x && point.x <= Right && point.y >= y && point.y <= Bottom;
        }
    }

    [Serializable]
    public sealed class FigunityPaint
    {
        public string type;
        public float opacity = 1f;
        public FigunityColor color;
        public bool hasImage;
        public string scaleMode;
    }

    [Serializable]
    public sealed class FigunityConstraints
    {
        public string horizontal;
        public string vertical;
    }

    [Serializable]
    public sealed class FigunityAutoLayout
    {
        public string layoutMode;
        public string primaryAxisSizingMode;
        public string counterAxisSizingMode;
        public string primaryAxisAlignItems;
        public string counterAxisAlignItems;
        public string layoutWrap;
        public float itemSpacing;
        public float counterAxisSpacing;
        public float paddingLeft;
        public float paddingRight;
        public float paddingTop;
        public float paddingBottom;

        public bool Enabled =>
            string.Equals(layoutMode, "HORIZONTAL", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(layoutMode, "VERTICAL", StringComparison.OrdinalIgnoreCase);
    }

    [Serializable]
    public struct FigunityColor
    {
        public float r;
        public float g;
        public float b;
        public float a;
    }

    [Serializable]
    public sealed class FigunityText
    {
        public string characters;
        public FigunityFont fontName;
        public float fontSize = 16f;
        public FigunityLineHeight lineHeight;
        public FigunityLetterSpacing letterSpacing;
        public string textAlignHorizontal;
        public string textAlignVertical;
        public string textAutoResize;
    }

    [Serializable]
    public sealed class FigunityFont
    {
        public string family;
        public string style;
    }

    [Serializable]
    public sealed class FigunityLineHeight
    {
        public string unit;
        public float value;
    }

    [Serializable]
    public sealed class FigunityLetterSpacing
    {
        public string unit;
        public float value;
    }
}
