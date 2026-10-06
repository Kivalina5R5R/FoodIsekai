using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // Ambient particles for the meal transition page. With no sprite it draws soft round light motes that drift
    // upward across the page; with a sprite (such as the Star art) it draws twinkles that grow, turn slightly
    // and fade around the artwork. Particles are spread through their lifetimes so the page is already lively
    // the moment it slides in. Place it as a child of the page so it travels with it.
    [AddComponentMenu("Food Isekai Z/Display/Transition Particles")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TransitionParticles : MaskableGraphic
    {
        [SerializeField] private Sprite sprite;
        [SerializeField, Range(1, 60)] private int count = 24;
        // Half the width and height of the oval the particles start in, centered on this object.
        [SerializeField] private Vector2 area = new Vector2(620f, 200f);
        // Particles keep out of this inner oval, so twinkles circle the artwork instead of covering it.
        [SerializeField] private Vector2 innerClearance = Vector2.zero;
        [SerializeField] private Vector2 sizeRange = new Vector2(3f, 11f);
        [SerializeField] private Vector2 lifetimeRange = new Vector2(3f, 5f);
        [SerializeField, Min(0f)] private float rise = 40f;
        [SerializeField, Min(0f)] private float sway = 10f;
        [SerializeField, Range(0f, 90f)] private float spinDegrees = 0f;
        [SerializeField] private Color colorA = new Color(1f, 0.96f, 0.86f, 1f);
        [SerializeField] private Color colorB = new Color(1f, 0.74f, 0.36f, 1f);
        [SerializeField, Range(0f, 1f)] private float opacity = 0.55f;

        private float elapsed;

        public override Texture mainTexture => sprite != null ? sprite.texture : base.mainTexture;

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
        }

        // The page runs during phase changes while gameplay is paused, so particles use real time.
        private void Update()
        {
            if (!Application.isPlaying) return;
            elapsed += Time.unscaledDeltaTime;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            Vector2 center = rectTransform.rect.center;
            for (int index = 0; index < count; index++)
            {
                float lifetime = Mathf.Lerp(lifetimeRange.x, Mathf.Max(lifetimeRange.x, lifetimeRange.y), Hash(index, 0, 1));
                lifetime = Mathf.Max(0.2f, lifetime);
                // Start each particle at a different point of its life so none of them pop in together.
                float clock = elapsed + Hash(index, 0, 2) * lifetime;
                int generation = Mathf.FloorToInt(clock / lifetime);
                float age = clock / lifetime - generation;

                Vector2 start = PickStart(index, generation);
                float swayPhase = Hash(index, generation, 5) * Mathf.PI * 2f;
                Vector2 position = center + start + new Vector2(Mathf.Sin(swayPhase + age * Mathf.PI * 2f) * sway, age * rise);
                float size = Mathf.Lerp(sizeRange.x, Mathf.Max(sizeRange.x, sizeRange.y), Hash(index, generation, 6));
                Color tint = Color.Lerp(colorA, colorB, Hash(index, generation, 7)) * color;

                if (sprite != null)
                {
                    // Twinkles swell and shrink once per life while turning a little.
                    float pulse = Mathf.Sin(age * Mathf.PI);
                    tint.a *= opacity * pulse;
                    float angle = (Hash(index, generation, 8) - 0.5f) * 2f * spinDegrees + age * spinDegrees;
                    DrawSprite(vertices, position, size * (0.35f + 0.65f * pulse), angle, tint);
                }
                else
                {
                    // Motes fade in quickly, linger, then fade out softly as they rise.
                    float fade = Mathf.Clamp01(age / 0.2f) * Mathf.Clamp01((1f - age) / 0.4f);
                    tint.a *= opacity * fade;
                    DrawBokeh(vertices, position, size, tint);
                }
            }
        }

        private Vector2 PickStart(int index, int generation)
        {
            for (int attempt = 0; attempt < 6; attempt++)
            {
                float angle = Hash(index, generation, 3 + attempt * 10) * Mathf.PI * 2f;
                float radius = Mathf.Sqrt(Hash(index, generation, 4 + attempt * 10));
                Vector2 point = new Vector2(Mathf.Cos(angle) * area.x, Mathf.Sin(angle) * area.y) * radius;
                if (innerClearance.x <= 0f || innerClearance.y <= 0f) return point;
                float inner = (point.x * point.x) / (innerClearance.x * innerClearance.x) +
                    (point.y * point.y) / (innerClearance.y * innerClearance.y);
                if (inner >= 1f) return point;
            }
            // Fall back to the outer rim when every try landed inside the clearance.
            float rimAngle = Hash(index, generation, 9) * Mathf.PI * 2f;
            return new Vector2(Mathf.Cos(rimAngle) * area.x, Mathf.Sin(rimAngle) * area.y);
        }

        // A stable pseudo-random value from 0 to 1 for this particle, life and purpose,
        // so the pattern never consumes the gameplay random sequence.
        private static float Hash(int index, int generation, int salt)
        {
            uint value = (uint)(index * 73856093) ^ (uint)(generation * 19349663) ^ (uint)(salt * 83492791);
            value ^= value >> 13;
            value *= 0x5bd1e995;
            value ^= value >> 15;
            return (value & 0xFFFFFF) / (float)0x1000000;
        }

        private void DrawSprite(VertexHelper vertices, Vector2 position, float size, float degrees, Color tint)
        {
            Vector4 uv = UnityEngine.Sprites.DataUtility.GetOuterUV(sprite);
            float radians = degrees * Mathf.Deg2Rad;
            Vector2 right = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * size;
            Vector2 up = new Vector2(-right.y, right.x);
            int first = vertices.currentVertCount;
            vertices.AddVert(position - right - up, tint, new Vector2(uv.x, uv.y));
            vertices.AddVert(position - right + up, tint, new Vector2(uv.x, uv.w));
            vertices.AddVert(position + right + up, tint, new Vector2(uv.z, uv.w));
            vertices.AddVert(position + right - up, tint, new Vector2(uv.z, uv.y));
            vertices.AddTriangle(first, first + 1, first + 2);
            vertices.AddTriangle(first, first + 2, first + 3);
        }

        // A round light with a brighter core and a soft edge, like an out-of-focus highlight.
        private static void DrawBokeh(VertexHelper vertices, Vector2 position, float radius, Color tint)
        {
            const int segments = 20;
            int first = vertices.currentVertCount;
            Color middle = tint;
            middle.a *= 0.55f;
            Color edge = tint;
            edge.a = 0f;
            AddVertex(vertices, position, tint);
            for (int point = 0; point < segments; point++)
            {
                float angle = point * Mathf.PI * 2f / segments;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                AddVertex(vertices, position + direction * radius * 0.45f, middle);
                AddVertex(vertices, position + direction * radius, edge);
            }

            for (int point = 0; point < segments; point++)
            {
                int inner = first + 1 + point * 2;
                int outer = inner + 1;
                int nextInner = first + 1 + (point + 1) % segments * 2;
                int nextOuter = nextInner + 1;
                vertices.AddTriangle(first, nextInner, inner);
                vertices.AddTriangle(inner, nextInner, nextOuter);
                vertices.AddTriangle(inner, nextOuter, outer);
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
