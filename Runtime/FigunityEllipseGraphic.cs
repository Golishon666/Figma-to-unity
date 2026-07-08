using UnityEngine;
using UnityEngine.UI;

namespace Figunity.Runtime
{
    [AddComponentMenu("UI/FIGUNITY/Ellipse Graphic")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FigunityEllipseGraphic : MaskableGraphic
    {
        [SerializeField] private int segments = 48;
        [SerializeField] private Color strokeColor = Color.clear;
        [SerializeField] private float strokeWidth;

        public int Segments
        {
            get => segments;
            set
            {
                segments = Mathf.Clamp(value, 12, 128);
                SetVerticesDirty();
            }
        }

        public Color StrokeColor
        {
            get => strokeColor;
            set
            {
                strokeColor = value;
                SetVerticesDirty();
            }
        }

        public float StrokeWidth
        {
            get => strokeWidth;
            set
            {
                strokeWidth = Mathf.Max(0f, value);
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = GetPixelAdjustedRect();
            var steps = Mathf.Clamp(segments, 12, 128);
            var center = rect.center;
            var radius = new Vector2(rect.width * 0.5f, rect.height * 0.5f);
            var clampedStroke = Mathf.Min(Mathf.Max(0f, strokeWidth), Mathf.Min(rect.width, rect.height) * 0.5f);
            var hasStroke = clampedStroke > 0.01f && strokeColor.a > 0.001f;
            var innerRadius = hasStroke
                ? new Vector2(Mathf.Max(0f, radius.x - clampedStroke), Mathf.Max(0f, radius.y - clampedStroke))
                : radius;

            if (color.a > 0)
            {
                vh.AddVert(center, color, Vector2.one * 0.5f);
                for (var i = 0; i < steps; i++)
                {
                    var point = Point(center, innerRadius, i, steps);
                    vh.AddVert(point, color, Uv(point, rect));
                }

                for (var i = 0; i < steps; i++)
                {
                    var next = i + 1 >= steps ? 1 : i + 2;
                    vh.AddTriangle(0, i + 1, next);
                }
            }

            if (!hasStroke)
            {
                return;
            }

            var start = vh.currentVertCount;
            for (var i = 0; i < steps; i++)
            {
                var outer = Point(center, radius, i, steps);
                var inner = Point(center, innerRadius, i, steps);
                vh.AddVert(outer, strokeColor, Uv(outer, rect));
                vh.AddVert(inner, strokeColor, Uv(inner, rect));
            }

            for (var i = 0; i < steps; i++)
            {
                var next = i + 1 >= steps ? 0 : i + 1;
                var outerA = start + i * 2;
                var innerA = outerA + 1;
                var outerB = start + next * 2;
                var innerB = outerB + 1;
                vh.AddTriangle(outerA, outerB, innerB);
                vh.AddTriangle(innerB, innerA, outerA);
            }
        }

        private static Vector2 Point(Vector2 center, Vector2 radius, int index, int steps)
        {
            var angle = index / (float)steps * Mathf.PI * 2f;
            return new Vector2(
                center.x + Mathf.Cos(angle) * radius.x,
                center.y + Mathf.Sin(angle) * radius.y);
        }

        private static Vector2 Uv(Vector2 point, Rect rect)
        {
            return new Vector2(
                Mathf.InverseLerp(rect.xMin, rect.xMax, point.x),
                Mathf.InverseLerp(rect.yMin, rect.yMax, point.y));
        }
    }
}
