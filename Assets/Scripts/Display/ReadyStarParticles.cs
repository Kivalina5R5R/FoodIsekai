using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // Canvas particles use the authored Star sprite and stay aligned with the floor UI.
    public sealed class ReadyStarParticles : MaskableGraphic
    {
        [SerializeField] private Sprite starSprite;
        [SerializeField, Min(0.1f)] private float burstSeconds = 0.9f;
        [SerializeField, Min(0.1f)] private float floatSeconds = 2.8f;
        [SerializeField, Min(1f)] private float starSize = 34f;
        private float appearedAt;
        private float idleAt;
        private bool idle;
        private uint emissionSeed;

        public override Texture mainTexture => starSprite != null ? starSprite.texture : base.mainTexture;

        protected override void OnEnable()
        {
            base.OnEnable();
            appearedAt = Time.unscaledTime;
            idle = false;
            emissionSeed = unchecked((uint)GetInstanceID() ^ (uint)System.Environment.TickCount);
        }

        public void BeginIdle()
        {
            idle = true;
            idleAt = Time.unscaledTime;
        }

        private void Update()
        {
            if (idle || Time.unscaledTime - appearedAt <= burstSeconds) SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (starSprite == null) return;
            Rect rect = rectTransform.rect;
            Vector4 uv = DataUtility.GetOuterUV(starSprite);
            float age = Time.unscaledTime - appearedAt;
            float t = Mathf.Clamp01(age / burstSeconds);
            if (t < 1f)
            {
                for (int i = 0; i < 18; i++)
                {
                    float angle = i * Mathf.PI * 2f / 18f;
                    Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    float travel = 1f - (1f - t) * (1f - t);
                    Vector2 position = rect.center + Vector2.Scale(direction,
                        new Vector2(rect.width * .34f, rect.height * .25f)) + direction * (travel * 180f);
                    DrawStar(mesh, uv, position, starSize * (1.2f - .5f * t), angle + t * 1.4f,
                        Mathf.Sin(Mathf.PI * Mathf.Sqrt(t)));
                }
            }
            if (!idle) return;
            float idleAge = Time.unscaledTime - idleAt;
            for (int i = 0; i < 12; i++)
            {
                float life = idleAge - i * floatSeconds / 12f;
                if (life < 0f) continue;
                float duration = floatSeconds * Mathf.Lerp(.8f, 1.25f, Sample(i, 0, 9));
                int cycle = Mathf.FloorToInt(life / duration);
                float phase = Mathf.Repeat(life / duration, 1f);
                // A new seed per lifetime scatters emission across the entire sign, not fixed lanes.
                float x = Mathf.Lerp(-.48f, .48f, Sample(i, cycle, 1)) * rect.width;
                float y = Mathf.Lerp(-.45f, .45f, Sample(i, cycle, 2)) * rect.height;
                x += Mathf.Lerp(-32f, 32f, Sample(i, cycle, 3)) * phase;
                y += phase * rect.height * Mathf.Lerp(.45f, .85f, Sample(i, cycle, 4));
                DrawStar(mesh, uv, rect.center + new Vector2(x, y),
                    starSize * Mathf.Lerp(.5f, 1f, Sample(i, cycle, 5)),
                    Mathf.Lerp(-.4f, .4f, Sample(i, cycle, 6)) + phase * .25f,
                    Mathf.Sin(phase * Mathf.PI) * .8f);
            }
        }

        private float Sample(int particle, int cycle, uint channel)
        {
            unchecked
            {
                uint value = emissionSeed ^ ((uint)particle * 374761393u) ^
                    ((uint)cycle * 668265263u) ^ (channel * 2246822519u);
                value = (value ^ (value >> 13)) * 1274126177u;
                value ^= value >> 16;
                return (value & 0x00ffffffu) / 16777216f;
            }
        }

        private void DrawStar(VertexHelper mesh, Vector4 uv, Vector2 center, float size, float angle, float opacity)
        {
            int first = mesh.currentVertCount;
            Color tint = color;
            tint.a *= Mathf.Clamp01(opacity);
            float aspect = starSprite.rect.width / starSprite.rect.height;
            Vector2 right = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * size * .5f * aspect;
            Vector2 up = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle)) * size * .5f;
            mesh.AddVert(center - right - up, tint, new Vector2(uv.x, uv.y));
            mesh.AddVert(center - right + up, tint, new Vector2(uv.x, uv.w));
            mesh.AddVert(center + right + up, tint, new Vector2(uv.z, uv.w));
            mesh.AddVert(center + right - up, tint, new Vector2(uv.z, uv.y));
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first, first + 2, first + 3);
        }
    }
}
