using System.Collections.Generic;
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
        private readonly List<CoinTransfer> transfers = new List<CoinTransfer>(4);

        private readonly struct CoinTransfer
        {
            public Vector2 Source { get; }
            public Vector2 Target { get; }
            public float StartedAt { get; }

            public CoinTransfer(Vector2 source, Vector2 target, float startedAt)
            {
                Source = source;
                Target = target;
                StartedAt = startedAt;
            }
        }

        public bool IsPlaying => playing || transfers.Count > 0;

        // Each deposit has its own captured source, so simultaneous players keep distinct streams.
        public void PlayTransfer(Vector3 source, Vector3 target)
        {
            if (!isActiveAndEnabled) return;
            Vector3 localSource = transform.InverseTransformPoint(source);
            Vector3 localTarget = transform.InverseTransformPoint(target);
            if (transfers.Count >= 8) transfers.RemoveAt(0);
            transfers.Add(new CoinTransfer(new Vector2(localSource.x, localSource.y),
                new Vector2(localTarget.x, localTarget.y), Time.time));
            SetVerticesDirty();
        }

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
            transfers.Clear();
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
            if (!IsPlaying) return;
            for (int i = transfers.Count - 1; i >= 0; i--)
                if (Time.time - transfers[i].StartedAt >= Mathf.Max(0.1f, duration)) transfers.RemoveAt(i);
            elapsed += Mathf.Max(0f, deltaTime);
            playing = playing && elapsed < duration;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            foreach (CoinTransfer transfer in transfers) DrawTransfer(mesh, transfer);
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
                DrawCoin(mesh, center, radius, spin, fade);
            }
        }

        private void DrawTransfer(VertexHelper mesh, CoinTransfer transfer)
        {
            float progress = (Time.time - transfer.StartedAt) / Mathf.Max(0.1f, duration);
            int count = Mathf.Clamp(coinCount, 4, 24);
            Vector2 offset = transfer.Target - transfer.Source;
            Vector2 side = offset.sqrMagnitude > 0.001f
                ? new Vector2(-offset.y, offset.x).normalized : Vector2.right;
            for (int i = 0; i < count; i++)
            {
                float delay = i / (float)(count - 1) * 0.28f;
                float age = (progress - delay) / 0.72f;
                if (age <= 0f || age >= 1f) continue;
                float travel = age * age * (3f - 2f * age);
                Vector2 center = Vector2.Lerp(transfer.Source, transfer.Target, travel);
                center += side * (Mathf.Sin(age * Mathf.PI) * ((i % 3) - 1) * 8f);
                center += Vector2.up * (Mathf.Sin(age * Mathf.PI) * 16f);
                float fade = Mathf.Clamp01(age / 0.08f) * (1f - Mathf.SmoothStep(0.9f, 1f, age));
                float radius = coinRadius * Mathf.Lerp(1f, 0.55f, travel);
                float spin = 0.35f + 0.65f * Mathf.Abs(Mathf.Cos(age * 10f + i));
                DrawCoin(mesh, center, radius, spin, fade);
            }
        }

        private void DrawCoin(VertexHelper mesh, Vector2 center, float radius, float spin, float fade)
        {
            Ellipse(mesh, center + new Vector2(1f, -1.3f), new Vector2(radius * spin, radius), rimColor, fade);
            Ellipse(mesh, center, new Vector2(radius * spin, radius), gleamColor, fade);
            Ellipse(mesh, center, new Vector2(radius * spin, radius) * 0.77f, goldColor, fade);
            Ellipse(mesh, center + new Vector2(-radius * spin * 0.2f, radius * 0.15f),
                new Vector2(radius * spin * 0.16f, radius * 0.5f), gleamColor, fade);
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
