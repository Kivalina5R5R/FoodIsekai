using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // Warm local halos breathe independently over the background's existing lamps.
    public sealed class ChaosLanternGlow : MaskableGraphic
    {
        [SerializeField] private Vector2[] lampPositions =
        {
            new Vector2(0.08f, 0.82f), new Vector2(0.92f, 0.82f),
            new Vector2(0.2f, 0.68f), new Vector2(0.8f, 0.68f)
        };
        [SerializeField, Min(1f)] private float radius = 145f;
        [SerializeField, Min(0.1f)] private float cycleSeconds = 4.8f;
        private float elapsed;

        protected override void OnEnable()
        {
            base.OnEnable();
            elapsed = 0f;
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            elapsed += Time.unscaledDeltaTime;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (!Application.isPlaying || lampPositions == null) return;
            Rect area = rectTransform.rect;
            for (int lamp = 0; lamp < lampPositions.Length; lamp++)
            {
                Vector2 normalized = lampPositions[lamp];
                Vector2 center = new Vector2(area.xMin + area.width * normalized.x, area.yMin + area.height * normalized.y);
                float wave = Mathf.Sin(elapsed * Mathf.PI * 2f / Mathf.Max(0.1f, cycleSeconds) + lamp * 1.7f);
                Color core = color;
                core.a *= 0.72f + wave * 0.18f;
                Color edge = core;
                edge.a = 0f;
                int first = mesh.currentVertCount;
                mesh.AddVert(center, core, Vector2.zero);
                const int segments = 32;
                for (int i = 0; i < segments; i++)
                {
                    float angle = i * Mathf.PI * 2f / segments;
                    Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    point.x = Mathf.Clamp(point.x, area.xMin, area.xMax);
                    point.y = Mathf.Clamp(point.y, area.yMin, area.yMax);
                    mesh.AddVert(point, edge, Vector2.zero);
                }
                for (int i = 0; i < segments; i++)
                    mesh.AddTriangle(first, first + 1 + i, first + 1 + (i + 1) % segments);
            }
        }
    }
}
