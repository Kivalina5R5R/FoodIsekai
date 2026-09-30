using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // A warm pulse expands across the wall after the card has finished burning.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PerkPowerGraphic : MaskableGraphic
    {
        private float progress;

        public void SetPower(float amount)
        {
            raycastTarget = false;
            progress = Mathf.Clamp01(amount);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (progress <= 0f || progress >= 1f) return;
            Rect rect = rectTransform.rect;
            float reach = rect.size.magnitude * .58f;
            float travel = 1f - Mathf.Pow(1f - progress, 3f);
            float radius = Mathf.Lerp(rect.height * .04f, reach, travel);
            float fade = Mathf.Sin(Mathf.PI * Mathf.Min(1f, progress / .16f) * .5f) * Mathf.Pow(1f - progress, 1.5f);
            Color clear = new Color(1f, .79f, .32f, 0f);
            Color glow = new Color(1f, .87f, .52f, fade * .48f);
            Color warmth = new Color(1f, .74f, .25f, fade * .085f);
            // Overscan the wash because the authored canvas can be narrower than the camera view.
            Vector2 minimum = rect.min * 1.25f;
            Vector2 maximum = rect.max * 1.25f;
            Quad(mesh, minimum, new Vector2(maximum.x, minimum.y), maximum,
                new Vector2(minimum.x, maximum.y), warmth, warmth);
            for (int i = 0; i < 128; i++)
            {
                float angle = i * Mathf.PI * 2f / 128f;
                float next = (i + 1) * Mathf.PI * 2f / 128f;
                Vector2 a = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 b = new Vector2(Mathf.Cos(next), Mathf.Sin(next));
                Quad(mesh, a * radius * .88f, b * radius * .88f, b * radius, a * radius, clear, glow);
                Quad(mesh, a * radius, b * radius, b * radius * 1.055f, a * radius * 1.055f, glow, clear);
                if (i % 2 != 0) continue;
                float seed = Mathf.Repeat(i * .618034f, 1f);
                Vector2 point = a * radius * (.4f + seed * .62f);
                Vector2 normal = new Vector2(-a.y, a.x);
                float length = (5f + seed * 23f) * (1f - progress);
                Color spark = new Color(1f, .89f, .57f, fade * (.35f + seed * .5f));
                Quad(mesh, point - a * length, point + normal * 1.1f, point + a * length * .35f,
                    point - normal * 1.1f, clear, spark);
            }
        }

        private static void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color inner, Color outer)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(a, inner, Vector2.zero);
            mesh.AddVert(b, inner, Vector2.zero);
            mesh.AddVert(c, outer, Vector2.zero);
            mesh.AddVert(d, outer, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
