using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // Round aura motes spawn at varied positions and drift upward behind Lunar.
    public sealed class ChaosMagicCurrent : MaskableGraphic
    {
        [SerializeField] private Color lavender = new Color(0.82f, 0.66f, 1f, 0.9f);
        [SerializeField] private Color gold = new Color(1f, 0.87f, 0.5f, 0.8f);
        [SerializeField, Min(0.1f)] private float flowSeconds = 6f;
        [SerializeField, Range(1, 48)] private int orbCount = 14;
        [SerializeField] private Vector2 radiusRange = new Vector2(28f, 60f);
        private float elapsed;
        private uint emissionSeed;

        protected override void OnEnable()
        {
            base.OnEnable();
            elapsed = 0f;
            emissionSeed = unchecked((uint)GetInstanceID());
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
            if (!Application.isPlaying) return;
            Rect area = rectTransform.rect;
            for (int orb = 0; orb < orbCount; orb++)
            {
                float duration = Mathf.Max(0.1f, flowSeconds) * Mathf.Lerp(0.85f, 1.6f, Sample(orb, 0, 1));
                float age = elapsed / duration + Sample(orb, 0, 2);
                int cycle = Mathf.FloorToInt(age);
                float phase = Mathf.Repeat(age, 1f);
                // Leave a different quiet gap between each circle's lifetimes.
                float visiblePart = Mathf.Lerp(0.65f, 0.85f, Sample(orb, cycle, 9));
                if (phase >= visiblePart) continue;
                phase /= visiblePart;
                float radius = Mathf.Lerp(Mathf.Max(1f, radiusRange.x), Mathf.Max(1f, radiusRange.y), Sample(orb, cycle, 3));
                float x = Mathf.Lerp(0.08f, 0.92f, Sample(orb, cycle, 4));
                float startY = Mathf.Lerp(0.23f, 0.42f, Sample(orb, cycle, 5));
                float endY = Mathf.Lerp(0.72f, 0.82f, Sample(orb, cycle, 6));
                Vector2 center = area.min + Vector2.Scale(area.size, new Vector2(x, Mathf.Lerp(startY, endY, phase)));
                center.x += Mathf.Sin(phase * Mathf.PI * 2f + Sample(orb, cycle, 7) * Mathf.PI * 2f) * 18f;
                // Each new circle randomly chooses only the authored yellow or purple.
                Color tint = Sample(orb, cycle, 8) < 0.5f ? gold : lavender;
                tint.a *= Mathf.SmoothStep(0f, 1f, phase / 0.12f) *
                    Mathf.SmoothStep(0f, 1f, (1f - phase) / 0.25f);
                DrawOrb(mesh, center, radius, tint);
            }
        }

        // Each lifetime gets new placement and size without consuming gameplay's random sequence.
        private float Sample(int orb, int cycle, uint channel)
        {
            unchecked
            {
                uint value = emissionSeed ^ ((uint)orb * 374761393u) ^
                    ((uint)cycle * 668265263u) ^ (channel * 2246822519u);
                value = (value ^ (value >> 13)) * 1274126177u;
                value ^= value >> 16;
                return (value & 0x00ffffffu) / 16777216f;
            }
        }

        private static void DrawOrb(VertexHelper mesh, Vector2 center, float radius, Color tint)
        {
            const int rings = 10;
            const int segments = 40;
            int first = mesh.currentVertCount;
            for (int ring = 0; ring <= rings; ring++)
            {
                float distance = ring / (float)rings;
                Color shade = tint;
                float fill = 0.5f * (1f - distance * distance);
                float rimDistance = (distance - 0.72f) / 0.13f;
                float rim = 0.85f * Mathf.Exp(-0.5f * rimDistance * rimDistance);
                shade.a *= Mathf.Clamp01(fill + rim) * (1f - Mathf.SmoothStep(0.82f, 1f, distance));
                for (int segment = 0; segment < segments; segment++)
                {
                    float angle = segment * Mathf.PI * 2f / segments;
                    Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * distance;
                    mesh.AddVert(point, shade, Vector2.zero);
                    if (ring == 0) continue;
                    int a = first + (ring - 1) * segments + segment;
                    int b = first + (ring - 1) * segments + (segment + 1) % segments;
                    mesh.AddTriangle(a, a + segments, b);
                    mesh.AddTriangle(b, a + segments, b + segments);
                }
            }
        }
    }
}
