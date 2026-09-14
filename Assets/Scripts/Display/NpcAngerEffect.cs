using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // Draws an anger accent over the NPC portrait without changing the authored body image.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class NpcAngerEffect : MaskableGraphic
    {
        private readonly SteamPuff[] puffs = new SteamPuff[6];
        // Keep cosmetic randomness independent of NPC selection and gameplay random state.
        private readonly System.Random steamRandom = new System.Random();
        private bool playing;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        // Starts a small cloud pop, then emits irregular steam puffs until the mood changes.
        public void Play()
        {
            for (int index = 0; index < puffs.Length; index++)
            {
                puffs[index] = CreatePuff(index < 3 ? index * 0.055f : Range(0.3f, 0.9f), index < 3);
            }
            playing = true;
            SetMaterialDirty();
            SetVerticesDirty();
        }

        public void Stop()
        {
            playing = false;
            canvasRenderer.SetMesh(null);
            SetVerticesDirty();
        }

        private void Update()
        {
            if (!playing || Time.deltaTime <= 0f)
            {
                return;
            }

            for (int index = 0; index < puffs.Length; index++)
            {
                puffs[index].Advance(Time.deltaTime);
                if (puffs[index].Age >= puffs[index].Lifetime)
                {
                    puffs[index] = CreatePuff(Range(0.06f, 0.4f), false);
                }
            }
            SetVerticesDirty();
        }

        protected override void OnDisable()
        {
            Stop();
            base.OnDisable();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (!playing)
            {
                return;
            }

            Rect bounds = rectTransform.rect;
            float unit = Mathf.Min(bounds.width, bounds.height);
            if (unit <= 0f)
            {
                return;
            }

            Vector2 head = bounds.min + Vector2.Scale(bounds.size, new Vector2(0.5f, 0.79f));
            DrawSteam(mesh, head, unit);
        }

        private SteamPuff CreatePuff(float delay, bool entrance)
        {
            float horizontal = Range(-0.065f, 0.065f);
            // Distribute origins along the crown, keeping the face clear below it.
            Vector2 origin = new Vector2(horizontal, 0.045f + 0.026f * (1f - Mathf.Abs(horizontal) / 0.065f));
            return new SteamPuff(delay, Range(0.85f, 1.3f), origin,
                new Vector2(Range(-0.025f, 0.025f), Range(0.045f, 0.075f)),
                Range(0.016f, 0.024f) * (entrance ? 1.15f : 1f), Range(0f, Mathf.PI * 2f));
        }

        private float Range(float minimum, float maximum)
        {
            return Mathf.Lerp(minimum, maximum, (float)steamRandom.NextDouble());
        }

        private void DrawSteam(VertexHelper mesh, Vector2 head, float unit)
        {
            foreach (SteamPuff puff in puffs)
            {
                if (puff.Age < 0f)
                {
                    continue;
                }

                float progress = Mathf.Clamp01(puff.Age / puff.Lifetime);
                float alpha = Mathf.SmoothStep(0f, 1f, progress / 0.12f) *
                    (1f - Mathf.SmoothStep(0f, 1f, (progress - 0.3f) / 0.7f));
                float curl = (Mathf.Sin(puff.Phase + progress * 4f) - Mathf.Sin(puff.Phase)) * 0.006f;
                Vector2 center = head + (puff.Origin + puff.Travel * progress + new Vector2(curl, 0f)) * unit;
                float radius = unit * puff.Radius * Mathf.Lerp(0.4f, 1.2f, progress);
                // Warm ivory with a muted umber underside suits the painted tavern palette.
                DrawCloud(mesh, center + new Vector2(0f, -unit * 0.002f), radius * 1.07f,
                    new Color(0.48f, 0.42f, 0.35f, alpha * 0.22f));
                DrawCloud(mesh, center, radius, new Color(0.95f, 0.92f, 0.85f, alpha * 0.64f));
            }
        }

        private static void DrawCloud(VertexHelper mesh, Vector2 center, float radius, Color tint)
        {
            DrawDisc(mesh, center, radius, tint);
            DrawDisc(mesh, center + new Vector2(radius * 0.72f, -radius * 0.12f), radius * 0.72f, tint);
            DrawDisc(mesh, center + new Vector2(-radius * 0.67f, -radius * 0.18f), radius * 0.65f, tint);
        }

        private static void DrawDisc(VertexHelper mesh, Vector2 center, float radius, Color tint)
        {
            const int segments = 12;
            int first = mesh.currentVertCount;
            AddVertex(mesh, center, tint);
            Color edge = tint;
            edge.a = 0f;
            for (int index = 0; index < segments; index++)
            {
                float angle = index * Mathf.PI * 2f / segments;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                AddVertex(mesh, center + direction * radius * 0.65f, tint);
                AddVertex(mesh, center + direction * radius, edge);
            }

            for (int index = 0; index < segments; index++)
            {
                int inner = first + 1 + index * 2;
                int nextInner = first + 1 + (index + 1) % segments * 2;
                mesh.AddTriangle(first, inner, nextInner);
                mesh.AddTriangle(inner, inner + 1, nextInner + 1);
                mesh.AddTriangle(inner, nextInner + 1, nextInner);
            }
        }

        private struct SteamPuff
        {
            public float Age { get; private set; }
            public float Lifetime { get; }
            public Vector2 Origin { get; }
            public Vector2 Travel { get; }
            public float Radius { get; }
            public float Phase { get; }

            public SteamPuff(float delay, float lifetime, Vector2 origin, Vector2 travel, float radius, float phase)
            {
                Age = -delay;
                Lifetime = lifetime;
                Origin = origin;
                Travel = travel;
                Radius = radius;
                Phase = phase;
            }

            public void Advance(float seconds)
            {
                Age += seconds;
            }
        }

        private static void AddVertex(VertexHelper mesh, Vector2 position, Color tint)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = tint;
            vertex.uv0 = new Vector2(0.5f, 0.5f);
            mesh.AddVert(vertex);
        }
    }
}
