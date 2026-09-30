using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // The card lands face-on, so the impact expands in the same screen plane as its artwork.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PerkLandingGraphic : MaskableGraphic
    {
        private float progress = -1f;

        public void SetImpact(float amount)
        {
            raycastTarget = false;
            progress = amount;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (progress < 0f || progress >= 1f) return;
            Rect rect = rectTransform.rect;
            Vector2 contact = rect.center;
            DrawRipple(mesh, contact, rect.size, progress, .3f);
            if (progress > .16f) DrawRipple(mesh, contact, rect.size, (progress - .16f) / .84f, .13f);
            float travel = 1f - Mathf.Pow(1f - progress, 2f);
            for (int i = 0; i < 42; i++)
            {
                float seed = Mathf.Repeat(i * .618034f, 1f);
                float angle = seed * Mathf.PI * 2f;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float boundary = Mathf.Min(rect.width * .5f / Mathf.Max(.001f, Mathf.Abs(direction.x)),
                    rect.height * .5f / Mathf.Max(.001f, Mathf.Abs(direction.y)));
                Vector2 origin = contact + direction * boundary;
                Vector2 point = origin + direction * travel * (25f + seed * 85f) +
                    Vector2.up * progress * 10f;
                float fade = Mathf.Pow(1f - progress, 2f);
                if (i % 3 == 0)
                {
                    Color dust = new Color(.85f, .77f, .61f, fade * .28f);
                    SoftParticle(mesh, point, (4f + seed * 6f) * (1f + travel), dust);
                    continue;
                }
                Color gold = Color.Lerp(new Color(1f, .67f, .24f), new Color(1f, .93f, .67f), seed);
                gold.a = fade * .8f;
                Vector2 normal = new Vector2(-direction.y, direction.x);
                float length = (2f + seed * 5f) * (1f - progress);
                Quad(mesh, point - direction * length, point + normal * .8f,
                    point + direction * length, point - normal * .8f, gold, gold);
            }
        }

        private static void DrawRipple(VertexHelper mesh, Vector2 center, Vector2 size, float age, float opacity)
        {
            float spread = 1f - Mathf.Pow(1f - age, 3f);
            Vector2 radius = Vector2.one * size.y * (.42f + spread * .43f);
            Color glow = new Color(1f, .9f, .65f, Mathf.Pow(1f - age, 2f) * opacity);
            Color clear = new Color(glow.r, glow.g, glow.b, 0f);
            for (int i = 0; i < 96; i++)
            {
                float angle = i * Mathf.PI * 2f / 96f;
                float next = (i + 1) * Mathf.PI * 2f / 96f;
                Vector2 a = Vector2.Scale(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), radius);
                Vector2 b = Vector2.Scale(new Vector2(Mathf.Cos(next), Mathf.Sin(next)), radius);
                Quad(mesh, center + a * .98f, center + b * .98f, center + b, center + a, clear, glow);
                Quad(mesh, center + a, center + b, center + b * 1.025f, center + a * 1.025f, glow, clear);
            }
        }

        private static void SoftParticle(VertexHelper mesh, Vector2 center, float radius, Color tint)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(center, tint, Vector2.zero);
            tint.a = 0f;
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4f;
                mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, tint, Vector2.zero);
            }
            for (int i = 0; i < 8; i++) mesh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % 8);
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
