using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // Draws floating pink hearts around the NPC whose Love expression is visible.
    [AddComponentMenu("Food Isekai Z/Display/NPC Love Heart Particles")]
    [RequireComponent(typeof(CanvasRenderer))]
    [DisallowMultipleComponent]
    public sealed class NpcLoveHeartParticles : MaskableGraphic
    {
        private const int HeartSegments = 32;
        private const int MaximumHearts = 8;
        private const float FadeInSeconds = 0.22f;
        private static readonly Vector2[] HeartOutline = CreateHeartOutline();

        [Header("Floating Hearts")]
        [SerializeField, Range(3, MaximumHearts)] private int heartCount = 5;
        [SerializeField, Min(0.1f)] private float fadeOutSeconds = 1.2f;
        [Tooltip("Heart radius as a fraction of the rendered body height.")]
        [SerializeField] private Vector2 sizeBodyHeightRange = new Vector2(0.015f, 0.022f);
        [Tooltip("Random spawn positions relative to the body center, in fractions of its width.")]
        [SerializeField] private Vector2 horizontalSpawnRange = new Vector2(-0.38f, 0.38f);
        [Tooltip("Random spawn heights measured from the bottom of the body Image.")]
        [SerializeField] private Vector2 verticalSpawnRange = new Vector2(0.504f, 0.864f);
        [Tooltip("Upward distance per second, as a fraction of the NPC's body height.")]
        [SerializeField, Range(0.01f, 0.15f)] private float riseBodyHeightPerSecond = 0.045f;
        [SerializeField, Range(0f, 0.1f)] private float swayBodyWidth = 0.045f;
        [SerializeField, Range(0f, 1f)] private float opacity = 0.94f;
        [SerializeField] private Color pinkColor = new Color(1f, 0.25f, 0.48f, 1f);
        [SerializeField] private Color highlightColor = new Color(1f, 0.76f, 0.86f, 1f);

        private Image body;
        private readonly Vector2[] spawnPositions = new Vector2[MaximumHearts];
        private readonly float[] sizeVariations = new float[MaximumHearts];
        private readonly float[] riseMultipliers = new float[MaximumHearts];
        private readonly float[] swayPhases = new float[MaximumHearts];
        private readonly float[] swayPeriods = new float[MaximumHearts];
        private System.Random particleRandom;
        private int spawnedHeartCount;
        private float elapsedSeconds;
        private float fadeElapsedSeconds;
        private float fadeStartOpacity;
        private bool playing;
        private bool fadingOut;

        // Binds the NPC's body Image and prepares a hidden effect.
        // Particle bounds follow the authored body and its current breathing pose.
        public void Initialize(Image visualBody)
        {
            Stop();
            body = visualBody;
            raycastTarget = false;
            // Cosmetic phases stay independent from the gameplay random sequence.
            particleRandom = new System.Random(GetInstanceID());
        }

        // Spawns one small batch; repeated calls never emit more hearts or restart their motion.
        public void Play()
        {
            if (playing || !HasVisibleBody())
            {
                return;
            }

            elapsedSeconds = 0f;
            fadeElapsedSeconds = 0f;
            fadingOut = false;
            SampleHeartBatch();
            playing = true;
            // CanvasRenderer.Clear (including Graphic.OnDisable) also removes render state.
            // A vertex-only rebuild cannot restore that state when Love starts later.
            SetMaterialDirty();
            SetVerticesDirty();
        }

        // Lets the current hearts keep drifting while fading away after the NPC finishes eating.
        public void FadeOut()
        {
            if (!playing || fadingOut)
            {
                return;
            }

            fadingOut = true;
            fadeElapsedSeconds = 0f;
            fadeStartOpacity = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsedSeconds / FadeInSeconds));
        }

        // Clears the batch immediately when the NPC is disabled, destroyed, or rebound.
        public void Stop()
        {
            playing = false;
            fadingOut = false;
            elapsedSeconds = 0f;
            fadeElapsedSeconds = 0f;
            spawnedHeartCount = 0;
            // Clear geometry immediately while preserving the material/texture binding.
            canvasRenderer.SetMesh(null);
            SetVerticesDirty();
        }

        // Keeps the decorative hearts from intercepting UI input.
        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        private void LateUpdate()
        {
            if (!playing)
            {
                return;
            }

            if (!HasVisibleBody())
            {
                Stop();
                return;
            }

            AdvanceParticles(Time.deltaTime);
        }

        private void AdvanceParticles(float deltaSeconds)
        {
            float delta = Mathf.Max(0f, deltaSeconds);
            elapsedSeconds += delta;
            if (fadingOut)
            {
                fadeElapsedSeconds += delta;
                if (fadeElapsedSeconds >= Mathf.Max(0.1f, fadeOutSeconds))
                {
                    Stop();
                    return;
                }
            }
            // Rebuild after the body's breathing update so particles follow its current pose.
            SetVerticesDirty();
        }

        // Clears the hearts when their graphic is disabled.
        protected override void OnDisable()
        {
            Stop();
            base.OnDisable();
        }

        // Builds the heart mesh around the body's current rendered bounds.
        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            if (!playing || !HasVisibleBody())
            {
                return;
            }

            Rect bounds = GetBodyDrawingRect();
            if (bounds.width <= 0f || bounds.height <= 0f)
            {
                return;
            }

            RectTransform source = body.rectTransform;
            Vector2 center = ToEffectPoint(source, bounds.center);
            Vector2 rightExtent = ToEffectPoint(source, bounds.center + Vector2.right * bounds.width) - center;
            Vector2 upExtent = ToEffectPoint(source, bounds.center + Vector2.up * bounds.height) - center;
            float bodyHeight = upExtent.magnitude;
            if (bodyHeight <= 0.001f || rightExtent.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            Vector2 right = rightExtent.normalized;
            Vector2 up = upExtent.normalized;
            float minimumSize = Mathf.Clamp(sizeBodyHeightRange.x, 0.005f, 0.06f);
            float maximumSize = Mathf.Clamp(sizeBodyHeightRange.y, minimumSize, 0.08f);
            float fadeIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsedSeconds / FadeInSeconds));
            float fadeOut = fadingOut
                ? 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(fadeElapsedSeconds / Mathf.Max(0.1f, fadeOutSeconds)))
                : 1f;
            float fade = (fadingOut ? fadeStartOpacity : fadeIn) * fadeOut;

            for (int index = 0; index < spawnedHeartCount; index++)
            {
                float swayAngle = swayPhases[index] + elapsedSeconds * Mathf.PI * 2f / swayPeriods[index];
                float horizontal = spawnPositions[index].x + Mathf.Sin(swayAngle) * Mathf.Clamp(swayBodyWidth, 0f, 0.1f);
                float height = spawnPositions[index].y + elapsedSeconds
                    * Mathf.Clamp(riseBodyHeightPerSecond, 0.01f, 0.15f) * riseMultipliers[index];
                // Height never wraps: these same hearts keep rising until the meal ends.
                Vector2 sourcePosition = new Vector2(bounds.center.x + bounds.width * horizontal,
                    bounds.yMin + bounds.height * height);
                Vector2 position = ToEffectPoint(source, sourcePosition);
                float size = bodyHeight * Mathf.Lerp(minimumSize, maximumSize, sizeVariations[index])
                    * Mathf.Lerp(0.82f, 1f, fadeIn);
                float tilt = Mathf.Sin(swayAngle) * 0.14f;
                Vector2 tiltedRight = right * Mathf.Cos(tilt) + up * Mathf.Sin(tilt);
                Vector2 tiltedUp = up * Mathf.Cos(tilt) - right * Mathf.Sin(tilt);
                Color rim = pinkColor * color;
                Color highlight = highlightColor * color;
                rim.a *= fade * Mathf.Clamp01(opacity);
                highlight.a *= fade * Mathf.Clamp01(opacity);
                Color outline = Color.Lerp(rim, new Color(0.7f, 0.04f, 0.24f, rim.a), 0.35f);

                DrawGlow(vertices, position, size * 1.6f, tiltedRight, tiltedUp, rim);
                DrawHeart(vertices, position, size * 1.08f, tiltedRight, tiltedUp, outline, outline);
                DrawHeart(vertices, position, size, tiltedRight, tiltedUp, highlight, rim);
            }
        }

        private void SampleHeartBatch()
        {
            spawnedHeartCount = Mathf.Clamp(heartCount, 3, MaximumHearts);
            float minimumX = Mathf.Clamp(horizontalSpawnRange.x, -0.6f, 0.6f);
            float maximumX = Mathf.Clamp(horizontalSpawnRange.y, minimumX, 0.6f);
            float minimumY = Mathf.Clamp01(verticalSpawnRange.x);
            float maximumY = Mathf.Clamp(verticalSpawnRange.y, minimumY, 1f);
            for (int index = 0; index < spawnedHeartCount; index++)
            {
                // Randomize within separate horizontal bands to keep a sparse batch spread around the body.
                float horizontal = (index + (float)particleRandom.NextDouble()) / spawnedHeartCount;
                spawnPositions[index] = new Vector2(Mathf.Lerp(minimumX, maximumX, horizontal),
                    Mathf.Lerp(minimumY, maximumY, (float)particleRandom.NextDouble()));
                sizeVariations[index] = (float)particleRandom.NextDouble();
                riseMultipliers[index] = Mathf.Lerp(0.8f, 1.15f, (float)particleRandom.NextDouble());
                swayPhases[index] = (float)particleRandom.NextDouble() * Mathf.PI * 2f;
                swayPeriods[index] = Mathf.Lerp(1.8f, 2.8f, (float)particleRandom.NextDouble());
            }
        }

        private bool HasVisibleBody()
        {
            return body != null && body.isActiveAndEnabled && body.gameObject.activeInHierarchy;
        }

        private Vector2 ToEffectPoint(RectTransform source, Vector2 position)
        {
            return rectTransform.InverseTransformPoint(source.TransformPoint(position));
        }

        private Rect GetBodyDrawingRect()
        {
            Rect bounds = body.GetPixelAdjustedRect();
            Sprite sprite = body.overrideSprite;
            if (!body.preserveAspect || sprite == null || sprite.rect.width <= 0f || sprite.rect.height <= 0f
                || bounds.width <= 0f || bounds.height <= 0f)
            {
                return bounds;
            }

            float spriteAspect = sprite.rect.width / sprite.rect.height;
            if (spriteAspect > bounds.width / bounds.height)
            {
                float height = bounds.width / spriteAspect;
                bounds.y += (bounds.height - height) * body.rectTransform.pivot.y;
                bounds.height = height;
            }
            else
            {
                float width = bounds.height * spriteAspect;
                bounds.x += (bounds.width - width) * body.rectTransform.pivot.x;
                bounds.width = width;
            }

            return bounds;
        }

        private static Vector2[] CreateHeartOutline()
        {
            var points = new Vector2[HeartSegments];
            for (int index = 0; index < points.Length; index++)
            {
                float angle = index * Mathf.PI * 2f / HeartSegments;
                float sine = Mathf.Sin(angle);
                float x = sine * sine * sine;
                float y = (13f * Mathf.Cos(angle) - 5f * Mathf.Cos(2f * angle)
                    - 2f * Mathf.Cos(3f * angle) - Mathf.Cos(4f * angle) + 2.5f) / 16f;
                points[index] = new Vector2(x, y);
            }

            return points;
        }

        private static void DrawHeart(VertexHelper vertices, Vector2 position, float size,
            Vector2 right, Vector2 up, Color center, Color edge)
        {
            int first = vertices.currentVertCount;
            AddVertex(vertices, position, center);
            for (int index = 0; index < HeartOutline.Length; index++)
            {
                Vector2 point = HeartOutline[index];
                AddVertex(vertices, position + (right * point.x + up * point.y) * size, edge);
            }

            for (int index = 0; index < HeartOutline.Length; index++)
            {
                vertices.AddTriangle(first, first + index + 1, first + (index + 1) % HeartOutline.Length + 1);
            }
        }

        private static void DrawGlow(VertexHelper vertices, Vector2 position, float radius,
            Vector2 right, Vector2 up, Color tint)
        {
            const int segments = 12;
            int first = vertices.currentVertCount;
            Color center = tint;
            center.a *= 0.2f;
            AddVertex(vertices, position, center);
            tint.a = 0f;
            for (int index = 0; index < segments; index++)
            {
                float angle = index * Mathf.PI * 2f / segments;
                AddVertex(vertices, position + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius, tint);
            }

            for (int index = 0; index < segments; index++)
            {
                vertices.AddTriangle(first, first + index + 1, first + (index + 1) % segments + 1);
            }
        }

        private static void AddVertex(VertexHelper vertices, Vector2 position, Color tint)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = tint;
            vertex.uv0 = new Vector2(0.5f, 0.5f);
            vertices.AddVert(vertex);
        }
    }
}
