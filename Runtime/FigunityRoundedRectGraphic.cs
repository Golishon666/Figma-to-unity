using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Figunity.Runtime
{
    [AddComponentMenu("UI/FIGUNITY/Rounded Rect Graphic")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FigunityRoundedRectGraphic : MaskableGraphic
    {
        [SerializeField] private float cornerRadius = 8f;
        [SerializeField] private int segmentsPerCorner = 6;
        [SerializeField] private Color strokeColor = Color.clear;
        [SerializeField] private float strokeWidth;

        public float CornerRadius
        {
            get => cornerRadius;
            set
            {
                cornerRadius = Mathf.Max(0f, value);
                SetVerticesDirty();
            }
        }

        public int SegmentsPerCorner
        {
            get => segmentsPerCorner;
            set
            {
                segmentsPerCorner = Mathf.Clamp(value, 1, 24);
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
            var radius = Mathf.Min(Mathf.Max(0f, cornerRadius), Mathf.Min(rect.width, rect.height) * 0.5f);
            var clampedStroke = Mathf.Min(Mathf.Max(0f, strokeWidth), Mathf.Min(rect.width, rect.height) * 0.5f);
            var hasStroke = clampedStroke > 0.01f && strokeColor.a > 0.001f;
            if (!hasStroke && radius <= 0.01f)
            {
                AddQuad(vh, rect, color);
                return;
            }

            var outer = RoundedPoints(rect, radius);
            if (!hasStroke)
            {
                AddFan(vh, outer, color, rect);
                return;
            }

            var innerRect = new Rect(
                rect.xMin + clampedStroke,
                rect.yMin + clampedStroke,
                Mathf.Max(0f, rect.width - clampedStroke * 2f),
                Mathf.Max(0f, rect.height - clampedStroke * 2f));
            if (innerRect.width <= 0.01f || innerRect.height <= 0.01f)
            {
                AddFan(vh, outer, strokeColor, rect);
                return;
            }

            var innerRadius = Mathf.Min(Mathf.Max(0f, radius - clampedStroke), Mathf.Min(innerRect.width, innerRect.height) * 0.5f);
            var inner = RoundedPoints(innerRect, innerRadius);
            AddFan(vh, inner, color, rect);
            AddRing(vh, outer, inner, strokeColor, rect);
        }

        private static void AddQuad(VertexHelper vh, Rect rect, Color32 vertexColor)
        {
            vh.AddVert(new Vector2(rect.xMin, rect.yMin), vertexColor, new Vector2(0f, 0f));
            vh.AddVert(new Vector2(rect.xMin, rect.yMax), vertexColor, new Vector2(0f, 1f));
            vh.AddVert(new Vector2(rect.xMax, rect.yMax), vertexColor, new Vector2(1f, 1f));
            vh.AddVert(new Vector2(rect.xMax, rect.yMin), vertexColor, new Vector2(1f, 0f));
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }

        private List<Vector2> RoundedPoints(Rect rect, float radius)
        {
            var points = new List<Vector2>(segmentsPerCorner * 4 + 4);
            if (radius <= 0.01f)
            {
                var steps = Mathf.Max(1, segmentsPerCorner);
                AddRepeatedPoint(points, new Vector2(rect.xMax, rect.yMax), steps + 1);
                AddRepeatedPoint(points, new Vector2(rect.xMin, rect.yMax), steps + 1);
                AddRepeatedPoint(points, new Vector2(rect.xMin, rect.yMin), steps + 1);
                AddRepeatedPoint(points, new Vector2(rect.xMax, rect.yMin), steps + 1);
                return points;
            }

            AddArc(points, new Vector2(rect.xMax - radius, rect.yMax - radius), radius, 0f, 90f);
            AddArc(points, new Vector2(rect.xMin + radius, rect.yMax - radius), radius, 90f, 180f);
            AddArc(points, new Vector2(rect.xMin + radius, rect.yMin + radius), radius, 180f, 270f);
            AddArc(points, new Vector2(rect.xMax - radius, rect.yMin + radius), radius, 270f, 360f);
            return points;
        }

        private static void AddRepeatedPoint(ICollection<Vector2> points, Vector2 point, int count)
        {
            for (var i = 0; i < count; i++)
            {
                points.Add(point);
            }
        }

        private static void AddFan(VertexHelper vh, IReadOnlyList<Vector2> points, Color32 vertexColor, Rect uvRect)
        {
            if (points == null || points.Count < 3 || vertexColor.a == 0)
            {
                return;
            }

            var center = Vector2.zero;
            for (var i = 0; i < points.Count; i++)
            {
                center += points[i];
            }

            center /= points.Count;
            var start = vh.currentVertCount;
            vh.AddVert(center, vertexColor, Uv(center, uvRect));
            for (var i = 0; i < points.Count; i++)
            {
                vh.AddVert(points[i], vertexColor, Uv(points[i], uvRect));
            }

            for (var i = 0; i < points.Count; i++)
            {
                var next = i + 1 >= points.Count ? 1 : i + 2;
                vh.AddTriangle(start, start + i + 1, start + next);
            }
        }

        private static void AddRing(VertexHelper vh, IReadOnlyList<Vector2> outer, IReadOnlyList<Vector2> inner, Color32 vertexColor, Rect uvRect)
        {
            if (outer == null || inner == null || outer.Count != inner.Count || outer.Count < 3 || vertexColor.a == 0)
            {
                return;
            }

            var start = vh.currentVertCount;
            for (var i = 0; i < outer.Count; i++)
            {
                vh.AddVert(outer[i], vertexColor, Uv(outer[i], uvRect));
                vh.AddVert(inner[i], vertexColor, Uv(inner[i], uvRect));
            }

            for (var i = 0; i < outer.Count; i++)
            {
                var next = i + 1 >= outer.Count ? 0 : i + 1;
                var outerA = start + i * 2;
                var innerA = outerA + 1;
                var outerB = start + next * 2;
                var innerB = outerB + 1;
                vh.AddTriangle(outerA, outerB, innerB);
                vh.AddTriangle(innerB, innerA, outerA);
            }
        }

        private static Vector2 Uv(Vector2 point, Rect rect)
        {
            return new Vector2(
                Mathf.InverseLerp(rect.xMin, rect.xMax, point.x),
                Mathf.InverseLerp(rect.yMin, rect.yMax, point.y));
        }

        private void AddArc(ICollection<Vector2> points, Vector2 center, float radius, float startDegrees, float endDegrees)
        {
            var steps = Mathf.Max(1, segmentsPerCorner);
            for (var i = 0; i <= steps; i++)
            {
                var t = i / (float)steps;
                var angle = Mathf.Lerp(startDegrees, endDegrees, t) * Mathf.Deg2Rad;
                points.Add(new Vector2(
                    center.x + Mathf.Cos(angle) * radius,
                    center.y + Mathf.Sin(angle) * radius));
            }
        }
    }
}
