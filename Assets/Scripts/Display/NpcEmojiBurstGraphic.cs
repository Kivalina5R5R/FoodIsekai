using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class NpcEmojiBurstGraphic : MaskableGraphic
    {
        private RectTransform bubble;
        private Vector3 authoredScale;
        private Color burstTint;
        private float elapsed;
        private float duration;
        private float phase;
        private int sparkCount;
        private bool playing;

        public void Initialize(RectTransform target)
        {
            bubble = target;
            authoredScale = target.localScale;
            raycastTarget = false;
            phase = Mathf.Repeat(GetInstanceID() * 0.618034f, 1f) * Mathf.PI * 2f;
            Stop();
        }

        public void Play(int moodLevel, bool entrance)
        {
            elapsed = 0f;
            duration = entrance ? 0.7f : 0.55f;
            sparkCount = entrance ? 14 : 10;
            burstTint = GetMoodTint(moodLevel);
            phase += 0.37f;
            playing = true;
            FollowBubble();
            SetVerticesDirty();
        }

        public void Stop()
        {
            playing = false;
            elapsed = 0f;
            SetVerticesDirty();
        }

        private void LateUpdate()
        {
            if (!playing)
            {
                return;
            }
            if (bubble == null || !bubble.gameObject.activeInHierarchy)
            {
                Stop();
                return;
            }

            FollowBubble();
            elapsed += Time.deltaTime;
            if (elapsed >= duration)
            {
                Stop();
                return;
            }
            SetVerticesDirty();
        }

        private void FollowBubble()
        {
            if (bubble == null)
            {
                return;
            }

            RectTransform effect = rectTransform;
            effect.anchorMin = bubble.anchorMin;
            effect.anchorMax = bubble.anchorMax;
            effect.pivot = bubble.pivot;
            effect.sizeDelta = bubble.sizeDelta;
            effect.anchoredPosition3D = bubble.anchoredPosition3D;
            effect.localRotation = bubble.localRotation;
            // Particles expand independently while the bubble pops in front of them.
            effect.localScale = authoredScale;
        }

        /// <inheritdoc />
        protected override void OnDisable()
        {
            Stop();
            base.OnDisable();
        }

        /// <inheritdoc />
        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            if (!playing || bubble == null)
            {
                return;
            }

            Rect bounds = rectTransform.rect;
            float progress = Mathf.Clamp01(elapsed / duration);
            float expansion = 1f - Mathf.Pow(1f - progress, 3f);
            float sizeBasis = Mathf.Min(bounds.width, bounds.height);
            for (int index = 0; index < sparkCount; index++)
            {
                float variation = Mathf.Repeat(index * 0.754878f, 1f);
                float angle = phase + index * Mathf.PI * 2f / sparkCount;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float radius = Mathf.Lerp(0.36f, 0.72f + variation * 0.18f, expansion);
                Vector2 position = bounds.center + Vector2.Scale(direction, bounds.size) * radius;
                float pulse = Mathf.Sin(Mathf.PI * Mathf.Clamp01(progress / 0.9f));
                float size = sizeBasis * Mathf.Lerp(0.035f, 0.06f, variation) * pulse;
                Color tip = burstTint;
                tip.a *= pulse;
                Color center = Color.Lerp(burstTint, Color.white, 0.8f);
                center.a = pulse;

                // Fading rays make the burst legible behind the bubble at small display sizes.
                Vector2 rayEnd = position - direction * sizeBasis * 0.06f;
                Vector2 rayStart = rayEnd - direction * sizeBasis * 0.16f * (1f - progress);
                DrawRay(vertices, rayStart, rayEnd, sizeBasis * 0.006f * pulse, tip);
                DrawStar(vertices, position, size, center, tip, angle);
            }
        }

        private static Color GetMoodTint(int moodLevel)
        {
            switch (moodLevel)
            {
                case -2: return new Color(1f, 0.3f, 0.18f, 1f);
                case -1: return new Color(1f, 0.62f, 0.16f, 1f);
                case 3: return new Color(1f, 0.45f, 0.66f, 1f);
                default: return new Color(1f, 0.8f, 0.35f, 1f);
            }
        }

        private static void DrawRay(VertexHelper vertices, Vector2 start, Vector2 end, float width, Color tint)
        {
            Vector2 direction = (end - start).normalized;
            Vector2 side = new Vector2(-direction.y, direction.x) * width;
            int first = vertices.currentVertCount;
            Color faded = tint;
            faded.a = 0f;
            AddVertex(vertices, start - side, faded);
            AddVertex(vertices, start + side, faded);
            AddVertex(vertices, end + side, tint);
            AddVertex(vertices, end - side, tint);
            vertices.AddTriangle(first, first + 1, first + 2);
            vertices.AddTriangle(first, first + 2, first + 3);
        }

        private static void DrawStar(VertexHelper vertices, Vector2 position, float size, Color center, Color tip, float rotation)
        {
            int first = vertices.currentVertCount;
            AddVertex(vertices, position, center);
            for (int point = 0; point < 8; point++)
            {
                float angle = rotation + point * Mathf.PI * 0.25f;
                float radius = point % 2 == 0 ? size : size * 0.28f;
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
