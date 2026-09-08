using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    [AddComponentMenu("Food Isekai Z/Display/Customer Panel Ambient Sparkles")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CustomerPanelAmbientSparkles : MaskableGraphic
    {
        [Header("Twinkles")]
        [SerializeField, Range(3, 8)] private int sparkleCount = 6;
        [SerializeField, Min(1f)] private float cycleSeconds = 2.6f;
        [SerializeField, Min(0.2f)] private float lifetimeSeconds = 1.3f;
        [SerializeField] private Vector2 sizeRange = new Vector2(5f, 9f);
        [SerializeField, Min(0f)] private float borderOutset = 1f;
        [SerializeField, Min(0f)] private float risePixels = 6f;
        [SerializeField, Range(0f, 1f)] private float opacity = 0.95f;
        [SerializeField] private Color goldColor = new Color(1f, 0.68f, 0.18f, 1f);
        [SerializeField] private Color creamColor = new Color(1f, 0.98f, 0.85f, 1f);

        private Image borderImage;
        private float elapsed;
        private float phaseOffset;
        private bool playing;

        public void Initialize(Image orderBorder)
        {
            borderImage = orderBorder;
            // Separate each panel's rhythm without consuming the gameplay random sequence.
            phaseOffset = Mathf.Repeat((GetInstanceID() % 997) * 0.618034f, 1f);
            Stop();
        }

        public void Play()
        {
            elapsed = 0f;
            playing = true;
            SetVerticesDirty();
        }

        public void Stop()
        {
            playing = false;
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
            if (!playing || Time.deltaTime <= 0f)
            {
                return;
            }

            elapsed += Time.deltaTime;
            SetVerticesDirty();
        }

        /// <inheritdoc />
        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            if (!playing || borderImage == null)
            {
                return;
            }

            Rect bounds = GetBorderDrawingRect();
            int count = Mathf.Clamp(sparkleCount, 3, 8);
            float cycle = Mathf.Max(1f, cycleSeconds);
            float lifetime = Mathf.Clamp(lifetimeSeconds, 0.2f, cycle * 0.65f);
            for (int index = 0; index < count; index++)
            {
                float clock = elapsed + (phaseOffset + (float)index / count) * cycle;
                float age = Mathf.Repeat(clock, cycle) / lifetime;
                if (age >= 1f)
                {
                    continue;
                }

                int generation = Mathf.FloorToInt(clock / cycle);
                float variation = Mathf.Repeat(index * 0.754878f + generation * 0.56984f + phaseOffset, 1f);
                float edgePosition = Mathf.Lerp(0.25f, 0.8f, variation);
                Vector2 position;
                int edge = (index + generation) % 3;
                if (edge == 0)
                {
                    position = new Vector2(Mathf.Lerp(bounds.xMin, bounds.xMax, edgePosition), bounds.yMax + borderOutset);
                }
                else
                {
                    position = new Vector2(edge == 1 ? bounds.xMin - borderOutset : bounds.xMax + borderOutset,
                        Mathf.Lerp(bounds.yMin, bounds.yMax, edgePosition));
                }

                position.y += age * Mathf.Max(0f, risePixels);
                Vector3 localPosition = rectTransform.InverseTransformPoint(borderImage.rectTransform.TransformPoint(position));
                float pulse = Mathf.Sin(age * Mathf.PI);
                float size = Mathf.Lerp(Mathf.Max(0.5f, sizeRange.x), Mathf.Max(sizeRange.x, sizeRange.y), variation) * pulse;
                Color center = creamColor * color;
                Color tip = goldColor * color;
                center.a *= pulse * pulse * opacity;
                tip.a *= pulse * pulse * opacity * 0.85f;
                DrawGlow(vertices, localPosition, size * 1.8f, tip);
                DrawStar(vertices, localPosition, size, center, tip);
            }
        }

        private Rect GetBorderDrawingRect()
        {
            Rect bounds = borderImage.GetPixelAdjustedRect();
            Sprite sprite = borderImage.overrideSprite;
            if (!borderImage.preserveAspect || sprite == null || sprite.rect.height <= 0f || bounds.height <= 0f)
            {
                return bounds;
            }

            float aspect = sprite.rect.width / sprite.rect.height;
            if (aspect > bounds.width / bounds.height)
            {
                float height = bounds.width / aspect;
                bounds.y += (bounds.height - height) * borderImage.rectTransform.pivot.y;
                bounds.height = height;
            }
            else
            {
                float width = bounds.height * aspect;
                bounds.x += (bounds.width - width) * borderImage.rectTransform.pivot.x;
                bounds.width = width;
            }

            return bounds;
        }

        private static void DrawGlow(VertexHelper vertices, Vector2 position, float radius, Color tint)
        {
            const int segments = 12;
            int first = vertices.currentVertCount;
            Color center = tint;
            center.a *= 0.22f;
            AddVertex(vertices, position, center);
            tint.a = 0f;
            for (int point = 0; point < segments; point++)
            {
                float angle = point * Mathf.PI * 2f / segments;
                AddVertex(vertices, position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, tint);
            }

            for (int point = 0; point < segments; point++)
            {
                vertices.AddTriangle(first, first + (point + 1) % segments + 1, first + point + 1);
            }
        }

        private static void DrawStar(VertexHelper vertices, Vector2 position, float size, Color center, Color tip)
        {
            int first = vertices.currentVertCount;
            AddVertex(vertices, position, center);
            for (int point = 0; point < 8; point++)
            {
                float angle = point * Mathf.PI * 0.25f;
                float radius = point % 2 == 0 ? size : size * 0.32f;
                AddVertex(vertices, position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, tip);
            }

            for (int point = 0; point < 8; point++)
            {
                vertices.AddTriangle(first, first + (point + 1) % 8 + 1, first + point + 1);
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
