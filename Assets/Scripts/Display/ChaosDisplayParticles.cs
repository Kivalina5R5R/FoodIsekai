using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // Draws warm drifting motes or a short button burst in the authored Canvas layer.
    public sealed class ChaosDisplayParticles : MaskableGraphic
    {
        [SerializeField] private bool ambient = true;
        [SerializeField, Range(1, 64)] private int particleCount = 26;
        [SerializeField, Min(0.1f)] private float lifetime = 7f;
        [SerializeField, Min(0.1f)] private float particleSize = 5f;
        private float elapsed;
        private float burstAge = -1f;

        protected override void OnEnable()
        {
            base.OnEnable();
            elapsed = 0f;
            burstAge = -1f;
        }

        // Emit once at the button's expansion peak; ambient layers keep their own rhythm.
        public void Burst()
        {
            burstAge = 0f;
            SetVerticesDirty();
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            elapsed += Time.unscaledDeltaTime;
            bool burstWasVisible = burstAge >= 0f;
            if (burstWasVisible)
            {
                burstAge += Time.unscaledDeltaTime;
                if (burstAge >= lifetime) burstAge = -1f;
            }
            if (ambient || burstWasVisible) SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (!Application.isPlaying) return;
            Rect area = rectTransform.rect;
            for (int i = 0; i < particleCount; i++)
            {
                float seed = Mathf.Repeat(i * 0.618034f + 0.13f, 1f);
                Vector2 point;
                float opacity;
                float size = particleSize * Mathf.Lerp(0.65f, 1.3f, seed);
                if (ambient)
                {
                    float phase = Mathf.Repeat(elapsed / Mathf.Max(0.1f, lifetime) * (0.7f + seed * 0.5f) + seed, 1f);
                    point = new Vector2(Mathf.Lerp(area.xMin + 35f, area.xMax - 35f, seed),
                        Mathf.Lerp(area.yMin + 35f, area.yMax - 35f, phase));
                    point.x += Mathf.Sin(elapsed * 0.4f + i * 2.4f) * 22f;
                    opacity = Mathf.Sin(phase * Mathf.PI) * (0.55f + 0.2f * Mathf.Sin(elapsed + i));
                }
                else
                {
                    if (burstAge < 0f || burstAge >= lifetime) break;
                    float phase = Mathf.Clamp01(burstAge / Mathf.Max(0.1f, lifetime));
                    float angle = i * Mathf.PI * 2f / particleCount;
                    Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    point = area.center + Vector2.Scale(direction, area.size * 0.42f) + direction * (65f * phase);
                    opacity = 1f - phase;
                    size *= 1.2f - phase * 0.7f;
                }
                DrawMote(mesh, point, size, opacity);
            }
        }

        private void DrawMote(VertexHelper mesh, Vector2 point, float size, float opacity)
        {
            int first = mesh.currentVertCount;
            Color core = color;
            core.a *= Mathf.Clamp01(opacity);
            Color edge = core;
            edge.a = 0f;
            mesh.AddVert(point, core, Vector2.zero);
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * 0.25f;
                float radius = size * (i % 2 == 0 ? 1f : 0.35f);
                mesh.AddVert(point + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, edge, Vector2.zero);
            }
            for (int i = 0; i < 8; i++) mesh.AddTriangle(first, first + 1 + i, first + 1 + (i + 1) % 8);
        }
    }
}
