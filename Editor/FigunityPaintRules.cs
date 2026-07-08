using System;
using TMPro;
using UnityEngine;

namespace Figunity.Editor
{
    public static class FigunityPaintRules
    {
        public static bool TrySolid(FigunityNode node, out Color color)
        {
            color = Color.white;
            if (node == null || node.fills == null)
            {
                return false;
            }

            for (var i = 0; i < node.fills.Count; i++)
            {
                var paint = node.fills[i];
                if (paint == null || !string.Equals(paint.type, "SOLID", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                color = ConvertColor(paint, NodeAlpha(node));
                return true;
            }

            return false;
        }

        public static bool IsFlatFill(FigunityNode node)
        {
            if (node == null || node.fills == null || node.fills.Count != 1)
            {
                return false;
            }

            var paint = node.fills[0];
            if (paint == null || paint.hasImage)
            {
                return false;
            }

            if (!string.Equals(paint.type, "SOLID", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return !string.Equals(node.type, "VECTOR", StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(node.type, "BOOLEAN_OPERATION", StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(node.type, "STAR", StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(node.type, "POLYGON", StringComparison.OrdinalIgnoreCase);
        }

        public static Color TextColor(FigunityNode node)
        {
            Color color;
            return TrySolid(node, out color) ? color : Color.black;
        }

        public static TextAlignmentOptions TextAlign(FigunityText text)
        {
            var horizontal = (text != null ? text.textAlignHorizontal : null) ?? "LEFT";
            var vertical = (text != null ? text.textAlignVertical : null) ?? "TOP";

            var center = string.Equals(horizontal, "CENTER", StringComparison.OrdinalIgnoreCase);
            var right = string.Equals(horizontal, "RIGHT", StringComparison.OrdinalIgnoreCase);
            var middle = string.Equals(vertical, "CENTER", StringComparison.OrdinalIgnoreCase);
            var bottom = string.Equals(vertical, "BOTTOM", StringComparison.OrdinalIgnoreCase);

            if (middle && center) return TextAlignmentOptions.Center;
            if (middle && right) return TextAlignmentOptions.Right;
            if (middle) return TextAlignmentOptions.Left;
            if (bottom && center) return TextAlignmentOptions.Bottom;
            if (bottom && right) return TextAlignmentOptions.BottomRight;
            if (bottom) return TextAlignmentOptions.BottomLeft;
            if (center) return TextAlignmentOptions.Top;
            if (right) return TextAlignmentOptions.TopRight;
            return TextAlignmentOptions.TopLeft;
        }

        public static float NodeAlpha(FigunityNode node)
        {
            return node == null ? 1f : Mathf.Clamp01(node.opacity);
        }

        public static Color ConvertColor(FigunityPaint paint, float nodeAlpha)
        {
            if (paint == null)
            {
                return Color.white;
            }

            var paintAlpha = Mathf.Clamp01(paint.opacity);
            return new Color(
                Mathf.Clamp01(paint.color.r),
                Mathf.Clamp01(paint.color.g),
                Mathf.Clamp01(paint.color.b),
                Mathf.Clamp01(paintAlpha * nodeAlpha));
        }
    }
}
