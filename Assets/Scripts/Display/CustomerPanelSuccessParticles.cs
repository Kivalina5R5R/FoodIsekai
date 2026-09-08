using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    [AddComponentMenu("Food Isekai Z/Display/Customer Panel Success Particles")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CustomerPanelSuccessParticles : MaskableGraphic
    {
        [Header("Burst")]
        [SerializeField, Range(8, 64)] private int particleCount = 28;
        [SerializeField, Min(0.1f)] private float duration = 0.75f;
        [SerializeField, Min(0f)] private float startRadius = 12f;
        [SerializeField, Min(0f)] private float travelDistance = 110f;
        [SerializeField, Min(0f)] private float downwardDrift = 34f;
        [SerializeField] private Vector2 particleSizeRange = new Vector2(4f, 9f);
        [SerializeField, Range(0f, 1f)] private float starRatio = 0.5f;

        [Header("Palette")]
        [SerializeField] private Color goldColor = new Color(1f, 0.76f, 0.25f, 1f);
        [SerializeField] private Color creamColor = new Color(1f, 0.96f, 0.77f, 1f);
        [SerializeField] private Color mintColor = new Color(0.51f, 0.95f, 0.77f, 1f);

        [Header("Accent Ring")]
        [SerializeField] private bool showRing = true;
        [SerializeField, Min(0f)] private float ringRadius = 58f;
        [SerializeField, Min(0f)] private float ringWidth = 2.5f;

        private const int RingSegments = 32;
        private Particle[] particles;
        private System.Random random;
        private float elapsed;
        private bool isPlaying;

        public bool IsPlaying => isPlaying;

        public void Play()
        {
            int count = Mathf.Clamp(particleCount, 8, 64);
            if (particles == null || particles.Length != count)
            {
                particles = new Particle[count];
            }

            if (random == null)
            {
                // This presentation effect must not advance the gameplay random sequence.
                random = new System.Random(GetInstanceID());
            }

            float minimumSize = Mathf.Max(0.5f, particleSizeRange.x);
            float maximumSize = Mathf.Max(minimumSize, particleSizeRange.y);
            for (int index = 0; index < count; index++)
            {
                float angle = (index + Next01() * 0.65f) / count * Mathf.PI * 2f;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                bool isStar = Next01() < starRatio;
                Color tint = isStar
                    ? (index % 2 == 0 ? goldColor : creamColor)
                    : (index % 2 == 0 ? mintColor : goldColor);
                particles[index] = new Particle(
                    direction,
                    Mathf.Lerp(0.55f, 1f, Next01()),
                    Mathf.Lerp(minimumSize, maximumSize, Next01()),
                    Next01() * Mathf.PI * 2f,
                    Mathf.Lerp(-5f, 5f, Next01()),
                    Mathf.Lerp(0.72f, 1f, Next01()),
                    Next01() * 0.06f,
                    tint,
                    isStar);
            }

            elapsed = 0f;
            isPlaying = true;
            SetVerticesDirty();
        }

        public void Stop()
        {
            if (!isPlaying && elapsed == 0f)
            {
                return;
            }

            isPlaying = false;
            elapsed = 0f;
            SetVerticesDirty();
        }

        /// <inheritdoc />
        protected override void OnDisable()
        {
            Stop();
            base.OnDisable();
        }

        private void Update()
        {
            if (!isPlaying || Time.deltaTime <= 0f)
            {
                return;
            }

            elapsed += Time.deltaTime;
            if (elapsed >= Mathf.Max(0.1f, duration) + 0.06f)
            {
                Stop();
                return;
            }

            SetVerticesDirty();
        }

        /// <inheritdoc />
        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (!isPlaying || particles == null)
            {
                return;
            }

            float safeDuration = Mathf.Max(0.1f, duration);
            if (showRing)
            {
                DrawRing(vertexHelper, elapsed / (safeDuration * 0.45f));
            }

            for (int index = 0; index < particles.Length; index++)
            {
                Particle particle = particles[index];
                float age = (elapsed - particle.Delay) / (safeDuration * particle.Lifetime);
                if (age <= 0f || age >= 1f)
                {
                    continue;
                }

                float progress = 1f - Mathf.Pow(1f - age, 3f);
                Vector2 position = particle.Direction *
                    (startRadius + travelDistance * particle.Distance * progress);
                position.y -= downwardDrift * age * age;

                float fadeIn = Mathf.Clamp01(age / 0.08f);
                float fadeOut = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, age));
                float size = particle.Size * Mathf.Lerp(1f, 0.55f, age) * fadeIn;
                Color tint = particle.Tint * color;
                tint.a *= fadeIn * fadeOut;
                DrawParticle(vertexHelper, position, size,
                    particle.Rotation + elapsed * particle.Spin, tint, particle.IsStar);
            }
        }

        private void DrawRing(VertexHelper vertexHelper, float age)
        {
            if (age <= 0f || age >= 1f || ringWidth <= 0f)
            {
                return;
            }

            float progress = 1f - (1f - age) * (1f - age);
            float radius = Mathf.Lerp(startRadius, ringRadius, progress);
            float halfWidth = ringWidth * 0.5f * (1f - age);
            Color tint = creamColor * color;
            tint.a *= (1f - age) * 0.48f;
            int firstVertex = vertexHelper.currentVertCount;
            for (int index = 0; index < RingSegments; index++)
            {
                float angle = index * Mathf.PI * 2f / RingSegments;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                AddVertex(vertexHelper, direction * (radius - halfWidth), tint);
                AddVertex(vertexHelper, direction * (radius + halfWidth), tint);
            }

            for (int index = 0; index < RingSegments; index++)
            {
                int inner = firstVertex + index * 2;
                int nextInner = firstVertex + (index + 1) % RingSegments * 2;
                vertexHelper.AddTriangle(inner, nextInner, inner + 1);
                vertexHelper.AddTriangle(inner + 1, nextInner, nextInner + 1);
            }
        }

        private static void DrawParticle(VertexHelper vertexHelper, Vector2 position,
            float size, float rotation, Color tint, bool isStar)
        {
            int sides = isStar ? 8 : 4;
            int firstVertex = vertexHelper.currentVertCount;
            AddVertex(vertexHelper, position, tint);
            for (int index = 0; index < sides; index++)
            {
                float angle = rotation + index * Mathf.PI * 2f / sides;
                float radius = isStar && index % 2 != 0 ? size * 0.3f : size;
                Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                AddVertex(vertexHelper, position + offset, tint);
            }

            for (int index = 0; index < sides; index++)
            {
                vertexHelper.AddTriangle(firstVertex, firstVertex + (index + 1) % sides + 1,
                    firstVertex + index + 1);
            }
        }

        private static void AddVertex(VertexHelper vertexHelper, Vector2 position, Color tint)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = tint;
            vertex.uv0 = new Vector2(0.5f, 0.5f);
            vertexHelper.AddVert(vertex);
        }

        private float Next01()
        {
            return (float)random.NextDouble();
        }

        private readonly struct Particle
        {
            internal Vector2 Direction { get; }
            internal float Distance { get; }
            internal float Size { get; }
            internal float Rotation { get; }
            internal float Spin { get; }
            internal float Lifetime { get; }
            internal float Delay { get; }
            internal Color Tint { get; }
            internal bool IsStar { get; }

            internal Particle(Vector2 direction, float distance, float size, float rotation,
                float spin, float lifetime, float delay, Color tint, bool isStar)
            {
                Direction = direction;
                Distance = distance;
                Size = size;
                Rotation = rotation;
                Spin = spin;
                Lifetime = lifetime;
                Delay = delay;
                Tint = tint;
                IsStar = isStar;
            }
        }
    }
}
