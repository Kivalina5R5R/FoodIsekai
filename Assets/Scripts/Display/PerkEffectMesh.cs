using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // Shared shapes and palette for the anime food-reaction purchase effects.
    internal static class PerkEffectMesh
    {
        public static readonly Color Gold = new Color(1f, .8f, .34f);
        public static readonly Color Cream = new Color(1f, .97f, .86f);
        public static readonly Color Blush = new Color(1f, .66f, .78f);
        public static readonly Color Peach = new Color(1f, .68f, .42f);

        public static Color Tint(Color color, float alpha) => new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));

        // Pastel rainbow used by the Big Perk tier; soft saturation keeps it inside the anime palette.
        public static Color Rainbow(float hue, float alpha = 1f, float saturation = .55f)
        {
            Color color = Color.HSVToRGB(Mathf.Repeat(hue, 1f), saturation, 1f);
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        // Stable pseudo-random value for an index, so effects stay deterministic in recordings and verification.
        public static float Hash(float index, float salt = 0f) =>
            Mathf.Repeat(Mathf.Sin(index * 12.9898f + salt * 78.233f) * 43758.547f, 1f);

        // A soft disc that fades from the tint at the center to clear at the rim.
        public static void Glow(VertexHelper mesh, Vector2 center, float radius, Color tint, int segments = 24)
        {
            if (tint.a <= 0f || radius <= 0f) return;
            int start = mesh.currentVertCount;
            mesh.AddVert(center, tint, Vector2.zero);
            Color clear = Tint(tint, 0f);
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, clear, Vector2.zero);
            }
            for (int i = 0; i < segments; i++) mesh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % segments);
        }

        // The anime "kira" twinkle: a long cross, a shorter diagonal cross and a bright core.
        public static void Sparkle(VertexHelper mesh, Vector2 center, float size, float angle, Color tint)
        {
            if (tint.a <= 0f || size <= 0f) return;
            Glow(mesh, center, size * .55f, Tint(tint, tint.a * .35f), 16);
            for (int i = 0; i < 8; i++)
            {
                bool major = i % 2 == 0;
                float direction = angle + i * Mathf.PI * .25f;
                Vector2 axis = new Vector2(Mathf.Cos(direction), Mathf.Sin(direction));
                Spike(mesh, center, center + axis * size * (major ? 1f : .42f), size * (major ? .13f : .07f),
                    tint, Tint(tint, 0f));
            }
            Glow(mesh, center, size * .16f, Tint(Color.white, tint.a), 12);
        }

        // A tapered triangle from a wide base to a sharp tip, used for focus lines, rays and spikes.
        public static void Spike(VertexHelper mesh, Vector2 from, Vector2 tip, float width, Color baseTint, Color tipTint)
        {
            Vector2 axis = tip - from;
            if (axis.sqrMagnitude < .0001f || (baseTint.a <= 0f && tipTint.a <= 0f)) return;
            Vector2 normal = new Vector2(-axis.y, axis.x).normalized * width * .5f;
            int start = mesh.currentVertCount;
            mesh.AddVert(from, baseTint, Vector2.zero);
            mesh.AddVert(from + normal, Tint(baseTint, baseTint.a * .35f), Vector2.zero);
            mesh.AddVert(tip, tipTint, Vector2.zero);
            mesh.AddVert(from - normal, Tint(baseTint, baseTint.a * .35f), Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }

        public static void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color inner, Color outer)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(a, inner, Vector2.zero);
            mesh.AddVert(b, inner, Vector2.zero);
            mesh.AddVert(c, outer, Vector2.zero);
            mesh.AddVert(d, outer, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }

        // An elliptical band that is brightest on its middle line and clear on both edges.
        public static void Ring(VertexHelper mesh, Vector2 center, Vector2 radius, float thickness, Color tint, int segments = 96)
        {
            if (tint.a <= 0f) return;
            Color clear = Tint(tint, 0f);
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                float next = (i + 1) * Mathf.PI * 2f / segments;
                Vector2 a = Vector2.Scale(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), radius);
                Vector2 b = Vector2.Scale(new Vector2(Mathf.Cos(next), Mathf.Sin(next)), radius);
                Quad(mesh, center + a, center + b, center + b * (1f - thickness), center + a * (1f - thickness), tint, clear);
                Quad(mesh, center + a, center + b, center + b * (1f + thickness), center + a * (1f + thickness), tint, clear);
            }
        }
    }
}
