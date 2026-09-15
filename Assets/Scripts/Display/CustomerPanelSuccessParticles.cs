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
        [SerializeField] private bool useUnscaledTime;
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
        private bool isRejection;
        private bool isWalletFull;
        [Header("Wrong Food")]
        [SerializeField] private Sprite rejectionSprite;
        [SerializeField] private Color rejectionSealColor = new Color(0.57f, 0.23f, 0.18f, 1f);
        [SerializeField, Range(0.2f, 0.6f)] private float rejectionSizeRatio = 0.35f;
        [SerializeField, Min(0.1f)] private float rejectionDuration = 1.1f;

        public bool IsPlaying => isPlaying;

        private bool UsesRejectionSprite => isPlaying && isRejection && !isWalletFull && rejectionSprite != null;

        public override Texture mainTexture => UsesRejectionSprite ? rejectionSprite.texture : base.mainTexture;

        public void Play()
        {
            isRejection = false;
            isWalletFull = false;
            SetMaterialDirty();
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

        // Plays the assigned rejection icon within the authored floor effect bounds.
        public void PlayRejection()
        {
            elapsed = 0f;
            isRejection = true;
            isWalletFull = false;
            isPlaying = true;
            SetMaterialDirty();
            SetVerticesDirty();
        }

        // Uses the existing exclamation seal for a full wallet.
        public void PlayWalletFull()
        {
            PlayRejection();
            isWalletFull = true;
            SetMaterialDirty();
        }

        public void Stop()
        {
            if (!isPlaying && elapsed == 0f)
            {
                return;
            }

            isPlaying = false;
            elapsed = 0f;
            SetMaterialDirty();
            SetVerticesDirty();
        }

        // Stops the celebration when its graphic is disabled.
        protected override void OnDisable()
        {
            Stop();
            base.OnDisable();
        }

        private void Update()
        {
            float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (!isPlaying || deltaTime <= 0f)
            {
                return;
            }

            elapsed += deltaTime;
            if (elapsed >= Mathf.Max(0.1f, isRejection ? rejectionDuration : duration) + 0.06f)
            {
                Stop();
                return;
            }

            SetVerticesDirty();
        }

        // Builds the celebration ring and particles for the current animation frame.
        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (isPlaying && isRejection)
            {
                DrawRejection(vertexHelper);
                return;
            }

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

        private void DrawRejection(VertexHelper vertexHelper)
        {
            float age = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, rejectionDuration));
            float entrance = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / 0.16f));
            float fade = entrance * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.65f, 1f, age)));
            float radius = Mathf.Max(12f, ringRadius * Mathf.Clamp(rejectionSizeRatio, 0.2f, 0.6f)) *
                Mathf.Lerp(0.9f, 1f, entrance);
            float tilt = Mathf.Sin(Mathf.Clamp01(age / 0.35f) * Mathf.PI * 2f) *
                0.09f * (1f - Mathf.Clamp01(age / 0.35f));
            Vector2 center = Vector2.up * radius * 0.12f * Mathf.SmoothStep(0f, 1f, age);
            if (!isWalletFull)
            {
                if (rejectionSprite != null)
                {
                    DrawRejectionIcon(vertexHelper, center, radius, tilt, fade);
                }
                return;
            }

            Color seal = rejectionSealColor * color;
            seal.a *= fade;
            Color rim = goldColor * color;
            rim.a *= fade;
            Color ink = creamColor * color;
            ink.a *= fade;
            Color shadow = new Color(0.16f, 0.08f, 0.05f, 0.28f * fade * color.a);
            DrawDisc(vertexHelper, center + Vector2.down * radius * 0.12f, radius * 1.04f, shadow);
            DrawDisc(vertexHelper, center, radius, rim);
            DrawDisc(vertexHelper, center, radius * 0.89f, seal);
            Color warningInk = ink;
            float width = radius * 0.075f;
            Vector2 upright = new Vector2(-Mathf.Sin(tilt), Mathf.Cos(tilt));
            Vector2 sideways = new Vector2(upright.y, -upright.x) * width;
            Vector2 top = center + upright * radius * 0.44f;
            Vector2 bottom = center - upright * radius * 0.06f;
            int first = vertexHelper.currentVertCount;
            AddVertex(vertexHelper, bottom - sideways, warningInk);
            AddVertex(vertexHelper, top - sideways, warningInk);
            AddVertex(vertexHelper, top + sideways, warningInk);
            AddVertex(vertexHelper, bottom + sideways, warningInk);
            vertexHelper.AddTriangle(first, first + 1, first + 2);
            vertexHelper.AddTriangle(first, first + 2, first + 3);
            DrawDisc(vertexHelper, top, width, warningInk);
            DrawDisc(vertexHelper, bottom, width, warningInk);
            DrawDisc(vertexHelper, center - upright * radius * 0.36f, width * 1.15f, warningInk);
        }

        private static void DrawDisc(VertexHelper vertexHelper, Vector2 center, float radius, Color tint)
        {
            int first = vertexHelper.currentVertCount;
            AddVertex(vertexHelper, center, tint);
            for (int index = 0; index < RingSegments; index++)
            {
                float angle = index * Mathf.PI * 2f / RingSegments;
                AddVertex(vertexHelper, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, tint);
            }

            for (int index = 0; index < RingSegments; index++)
            {
                vertexHelper.AddTriangle(first, first + (index + 1) % RingSegments + 1, first + index + 1);
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

        private void DrawRejectionIcon(VertexHelper vertices, Vector2 center, float radius, float tilt, float fade)
        {
            Vector2 size = rejectionSprite.rect.size;
            Vector2 halfSize = size * (radius / Mathf.Max(size.x, size.y));
            Vector2 right = new Vector2(Mathf.Cos(tilt), Mathf.Sin(tilt)) * halfSize.x;
            Vector2 up = new Vector2(-Mathf.Sin(tilt), Mathf.Cos(tilt)) * halfSize.y;
            Vector4 uv = UnityEngine.Sprites.DataUtility.GetOuterUV(rejectionSprite);
            Color tint = color;
            tint.a *= fade;
            vertices.AddVert(center - right - up, tint, new Vector2(uv.x, uv.y));
            vertices.AddVert(center - right + up, tint, new Vector2(uv.x, uv.w));
            vertices.AddVert(center + right + up, tint, new Vector2(uv.z, uv.w));
            vertices.AddVert(center + right - up, tint, new Vector2(uv.z, uv.y));
            vertices.AddTriangle(0, 1, 2);
            vertices.AddTriangle(0, 2, 3);
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
