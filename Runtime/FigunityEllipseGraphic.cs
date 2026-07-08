using UnityEngine;
using UnityEngine.UI;

namespace Figunity.Runtime
{
    [AddComponentMenu("UI/FIGUNITY/Ellipse Graphic")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FigunityEllipseGraphic : MaskableGraphic
    {
        [SerializeField] private int segments = 48;

        public int Segments
        {
            get => segments;
            set
            {
                segments = Mathf.Clamp(value, 12, 128);
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

            vh.AddVert(center, color, Vector2.one * 0.5f);
            for (var i = 0; i < steps; i++)
            {
                var angle = i / (float)steps * Mathf.PI * 2f;
                var point = new Vector2(
                    center.x + Mathf.Cos(angle) * radius.x,
                    center.y + Mathf.Sin(angle) * radius.y);
                var uv = new Vector2(
                    Mathf.InverseLerp(rect.xMin, rect.xMax, point.x),
                    Mathf.InverseLerp(rect.yMin, rect.yMax, point.y));
                vh.AddVert(point, color, uv);
            }

            for (var i = 0; i < steps; i++)
            {
                var next = i + 1 >= steps ? 1 : i + 2;
                vh.AddTriangle(0, i + 1, next);
            }
        }
    }
}
