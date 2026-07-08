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

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = GetPixelAdjustedRect();
            var radius = Mathf.Min(Mathf.Max(0f, cornerRadius), Mathf.Min(rect.width, rect.height) * 0.5f);
            if (radius <= 0.01f)
            {
                AddQuad(vh, rect, color);
                return;
            }

            var points = new List<Vector2>(segmentsPerCorner * 4 + 4);
            AddArc(points, new Vector2(rect.xMax - radius, rect.yMax - radius), radius, 0f, 90f);
            AddArc(points, new Vector2(rect.xMin + radius, rect.yMax - radius), radius, 90f, 180f);
            AddArc(points, new Vector2(rect.xMin + radius, rect.yMin + radius), radius, 180f, 270f);
            AddArc(points, new Vector2(rect.xMax - radius, rect.yMin + radius), radius, 270f, 360f);

            var center = rect.center;
            vh.AddVert(center, color, Vector2.one * 0.5f);
            for (var i = 0; i < points.Count; i++)
            {
                var uv = new Vector2(
                    Mathf.InverseLerp(rect.xMin, rect.xMax, points[i].x),
                    Mathf.InverseLerp(rect.yMin, rect.yMax, points[i].y));
                vh.AddVert(points[i], color, uv);
            }

            for (var i = 0; i < points.Count; i++)
            {
                var next = i + 1 >= points.Count ? 1 : i + 2;
                vh.AddTriangle(0, i + 1, next);
            }
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
