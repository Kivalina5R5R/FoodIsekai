using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // Draws subtle background-only light and dust on the authored tavern artwork.
    public sealed class TavernAmbientLight : MaskableGraphic
    {
        [System.Serializable]
        private sealed class WindowLight
        {
            [SerializeField] private Vector2 source;
            [SerializeField] private Vector2 end;
            [SerializeField] private Vector2 widths = new Vector2(0.04f, 0.12f);

            public Vector2 Source => source;
            public Vector2 End => end;
            public Vector2 Widths => widths;
        }

        [System.Serializable]
        private sealed class LampLight
        {
            [SerializeField] private Vector2 position;
            [SerializeField] private Vector2 radius = new Vector2(0.026f, 0.095f);
            [SerializeField] private Color tint = new Color(1f, 0.68f, 0.27f, 0.12f);
            [SerializeField] private float phase;

            public Vector2 Position => position;
            public Vector2 Radius => radius;
            public Color Tint => tint;
            public float Phase => phase;
        }

        [Tooltip("Positions and widths are fractions of the background, measured from its bottom-left.")]
        [SerializeField] private WindowLight[] windows;
        [SerializeField] private LampLight[] lamps;
        [SerializeField] private Color sunlight = new Color(1f, 0.89f, 0.62f, 0.085f);
        [SerializeField] private Color dustColor = new Color(1f, 0.94f, 0.76f, 0.3f);
        [SerializeField, Range(0, 24)] private int dustPerWindow = 10;
        [SerializeField, Range(0f, 0.02f)] private float beamSway = 0.004f;
        [SerializeField, Range(0, 80)] private int ambientParticleCount;
        [SerializeField] private Color ambientParticleColor = new Color(1f, 0.93f, 0.73f, 0.35f);
        [SerializeField] private Vector2 ambientParticleRadius = new Vector2(0.7f, 1.5f);
        [SerializeField, Min(0.001f)] private float ambientParticleSpeed = 0.025f;
        private float nextRefresh;

        private void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 1f / 30f;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect area = rectTransform.rect;
            if (area.width <= 0f || area.height <= 0f) return;
            float time = Application.isPlaying ? Time.unscaledTime : 0f;
            if (windows != null)
            {
                for (int i = 0; i < windows.Length; i++)
                {
                    if (windows[i] == null) continue;
                    DrawWindow(mesh, area, windows[i], i, time);
                }
            }
            DrawAmbientParticles(mesh, area, time);
            if (lamps == null) return;
            foreach (LampLight lamp in lamps)
            {
                if (lamp == null) continue;
                // Small independent intensity changes avoid blinking or a synchronized pulse.
                float flicker = 1f + 0.07f * Mathf.Sin(time * 1.7f + lamp.Phase) +
                    0.035f * Mathf.Sin(time * 3.13f + lamp.Phase * 2.3f);
                Color tint = lamp.Tint;
                tint.a *= flicker;
                DrawGlow(mesh, ToLocal(area, lamp.Position), Vector2.Scale(area.size, lamp.Radius), tint);
            }
        }

        private void DrawWindow(VertexHelper mesh, Rect area, WindowLight window, int index, float time)
        {
            const int rows = 12;
            const int columns = 10;
            Vector2 start = ToLocal(area, window.Source);
            Vector2 end = ToLocal(area, window.End);
            end.x += Mathf.Sin(time * 0.18f + index * 1.7f) * beamSway * area.width;
            // Keep the beam's top edge level with the window sill, even for an angled ray.
            Vector2 across = Vector2.right;
            int first = mesh.currentVertCount;
            for (int row = 0; row <= rows; row++)
            {
                float along = row / (float)rows;
                float width = Mathf.Lerp(window.Widths.x, window.Widths.y, along) * area.width;
                for (int column = 0; column <= columns; column++)
                {
                    float cross = column / (float)columns;
                    Vector2 point = Vector2.Lerp(start, end, along) + across * ((cross - 0.5f) * width);
                    Color tint = sunlight;
                    float edge = Mathf.Sin(cross * Mathf.PI);
                    tint.a *= edge * edge * Mathf.Sin(along * Mathf.PI);
                    mesh.AddVert(point, tint, Vector2.zero);
                    if (row == rows || column == columns) continue;
                    int vertex = first + row * (columns + 1) + column;
                    mesh.AddTriangle(vertex, vertex + columns + 1, vertex + 1);
                    mesh.AddTriangle(vertex + 1, vertex + columns + 1, vertex + columns + 2);
                }
            }
            for (int i = 0; i < dustPerWindow; i++)
            {
                float seed = i * 0.618034f + index * 0.317f;
                float life = Mathf.Repeat(seed + time * (0.022f + (i % 3) * 0.004f), 1f);
                float along = Mathf.Lerp(0.86f, 0.16f, life);
                float width = Mathf.Lerp(window.Widths.x, window.Widths.y, along) * area.width;
                float lane = (Mathf.Repeat(seed * 7.31f, 1f) - 0.5f) * 0.55f;
                lane += Mathf.Sin(time * 0.45f + i * 2.4f) * 0.035f;
                Vector2 point = Vector2.Lerp(start, end, along) + across * (lane * width);
                Color tint = dustColor;
                tint.a *= Mathf.Pow(Mathf.Sin(life * Mathf.PI), 2f);
                float radius = Mathf.Lerp(0.75f, 1.5f, Mathf.Repeat(seed * 3.7f, 1f)) * area.height / 435f;
                DrawGlow(mesh, point, Vector2.one * radius, tint);
            }
        }

        private static Vector2 ToLocal(Rect area, Vector2 normalized)
        {
            return area.min + Vector2.Scale(area.size, normalized);
        }

        private void DrawAmbientParticles(VertexHelper mesh, Rect area, float time)
        {
            for (int i = 0; i < ambientParticleCount; i++)
            {
                float seed = (i + 1) * 0.618034f;
                float life = Mathf.Repeat(seed + time * ambientParticleSpeed *
                    Mathf.Lerp(0.7f, 1.3f, Mathf.Repeat(seed * 3.17f, 1f)), 1f);
                float x = Mathf.Lerp(0.18f, 0.82f, Mathf.Repeat(seed * 7.31f, 1f));
                x += Mathf.Sin(time * 0.23f + seed * 19f) * 0.008f;
                float y = Mathf.Lerp(0.12f, 0.78f, life);
                Color tint = ambientParticleColor;
                // Fade at both ends so wrapping particles never pop into view.
                tint.a *= Mathf.Pow(Mathf.Sin(life * Mathf.PI), 2f);
                tint.a *= 0.8f + 0.2f * Mathf.Sin(time * 0.9f + seed * 23f);
                float radius = Mathf.Lerp(ambientParticleRadius.x, ambientParticleRadius.y,
                    Mathf.Repeat(seed * 5.73f, 1f)) * area.height / 435f;
                DrawGlow(mesh, ToLocal(area, new Vector2(x, y)), Vector2.one * radius, tint);
            }
        }

        private static void DrawGlow(VertexHelper mesh, Vector2 center, Vector2 radius, Color tint)
        {
            const int rings = 4;
            const int segments = 24;
            int first = mesh.currentVertCount;
            for (int ring = 0; ring <= rings; ring++)
            {
                float distance = ring / (float)rings;
                Color faded = tint;
                faded.a *= (1f - distance) * (1f - distance);
                for (int segment = 0; segment < segments; segment++)
                {
                    float angle = segment * Mathf.PI * 2f / segments;
                    mesh.AddVert(center + Vector2.Scale(radius, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle))) * distance,
                        faded, Vector2.zero);
                    if (ring == rings) continue;
                    int current = first + ring * segments + segment;
                    int next = first + ring * segments + (segment + 1) % segments;
                    mesh.AddTriangle(current, current + segments, next);
                    mesh.AddTriangle(next, current + segments, next + segments);
                }
            }
        }
    }
}
