using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // Draws individual spinning coins from an authored floor anchor.
    public sealed class FloorCoinBurst : MaskableGraphic
    {
        [SerializeField, Range(4, 24)] private int coinCount = 12;
        [SerializeField, Min(0.1f)] private float duration = 0.85f;
        [SerializeField, Min(1f)] private float spread = 72f;
        [SerializeField, Min(1f)] private float coinRadius = 5f;
        [SerializeField] private Color rimColor = new Color(0.57f, 0.29f, 0.06f);
        [SerializeField] private Color goldColor = new Color(1f, 0.72f, 0.18f);
        [SerializeField] private Color gleamColor = new Color(1f, 0.95f, 0.65f);
        private float elapsed;
        private bool playing;
        private bool gathering;
        private Vector2 destination;

        public bool IsPlaying => playing;

        public void Play(bool collect, Vector3 target)
        {
            gathering = collect;
            Vector3 local = transform.InverseTransformPoint(target);
            destination = Vector2.ClampMagnitude(new Vector2(local.x, local.y), 110f);
            elapsed = 0f;
            playing = true;
            SetVerticesDirty();
        }

        public void Stop()
        {
            playing = false;
            SetVerticesDirty();
        }

        protected override void OnDisable()
        {
            Stop();
            base.OnDisable();
        }

        private void Update() => Advance(Time.deltaTime);

        private void Advance(float deltaTime)
        {
            if (!playing) return;
            elapsed += Mathf.Max(0f, deltaTime);
            playing = elapsed < duration;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (!playing) return;
            for (int i = 0; i < coinCount; i++)
            {
                float age = Mathf.Clamp01((elapsed / duration - i * 0.013f) / 0.82f);
                if (age <= 0f || age >= 1f) continue;
                float angle = i * 2.399963f;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float travel = 1f - Mathf.Pow(1f - age, 3f);
                Vector2 center = direction * spread * (0.55f + (i % 3) * 0.2f) * travel;
                center.y += Mathf.Sin(age * Mathf.PI) * 28f - age * age * 12f;
                if (gathering) center = Vector2.Lerp(center, destination, Mathf.SmoothStep(0f, 1f, age));
                float fade = Mathf.Clamp01(age * 14f) * (1f - Mathf.SmoothStep(0.6f, 1f, age));
                float radius = coinRadius * (0.8f + (i % 3) * 0.13f);
                float spin = 0.25f + 0.75f * Mathf.Abs(Mathf.Cos(age * 12f + angle));
                Ellipse(mesh, center + new Vector2(1f, -1.3f), new Vector2(radius * spin, radius), rimColor, fade);
                Ellipse(mesh, center, new Vector2(radius * spin, radius), gleamColor, fade);
                Ellipse(mesh, center, new Vector2(radius * spin, radius) * 0.77f, goldColor, fade);
                Ellipse(mesh, center + new Vector2(-radius * spin * 0.2f, radius * 0.15f),
                    new Vector2(radius * spin * 0.16f, radius * 0.5f), gleamColor, fade);
            }
        }

        private static void Ellipse(VertexHelper mesh, Vector2 center, Vector2 radius, Color tint, float alpha)
        {
            tint.a *= alpha;
            int first = mesh.currentVertCount;
            mesh.AddVert(center, tint, Vector2.zero);
            const int segments = 16;
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                mesh.AddVert(center + new Vector2(Mathf.Cos(angle) * radius.x, Mathf.Sin(angle) * radius.y), tint, Vector2.zero);
                if (i > 0) mesh.AddTriangle(first, first + i, first + i + 1);
            }
        }
    }
}
