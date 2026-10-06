using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // A soft band of light that glides across each visible player bar every few seconds, one row after another.
    // Place this graphic after the rows in the hierarchy so it draws over them; it fades with each row's entrance.
    [AddComponentMenu("Food Isekai Z/Display/Result Row Shine")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ResultRowShine : MaskableGraphic
    {
        [SerializeField] private RectTransform[] rows = new RectTransform[0];
        [SerializeField, Min(0.5f)] private float intervalSeconds = 3.6f;
        [SerializeField, Min(0.1f)] private float sweepSeconds = 0.9f;
        [SerializeField, Min(0f)] private float rowDelaySeconds = 0.12f;
        // Band width as a share of the bar width.
        [SerializeField, Range(0.05f, 0.5f)] private float bandWidth = 0.16f;
        // Keeps the light inside the bar body, clear of the pointed end caps and the outline.
        [SerializeField] private Vector2 inset = new Vector2(0.08f, 0.2f);
        [SerializeField, Range(0f, 1f)] private float opacity = 0.45f;
        [SerializeField] private Color shineColor = new Color(1f, 0.99f, 0.93f, 1f);

        private float elapsed;
        private Graphic[] rowBars = new Graphic[0];

        // How far the light is across the given target right now, from 0 to 1, or a negative value
        // when no light is passing over it (also before the target has fully faded in).
        public float GetSweepProgress(RectTransform target)
        {
            if (rows == null || target == null || !isActiveAndEnabled) return -1f;
            int row = System.Array.IndexOf(rows, target);
            if (row < 0 || !target.gameObject.activeInHierarchy) return -1f;
            Graphic bar = target.GetComponent<Graphic>();
            if (bar != null && bar.canvasRenderer.GetAlpha() < 0.99f) return -1f;
            float interval = Mathf.Max(0.5f, intervalSeconds);
            float sweep = Mathf.Clamp(sweepSeconds, 0.1f, interval);
            float progress = Mathf.Repeat(elapsed - row * rowDelaySeconds, interval) / sweep;
            return progress < 1f ? progress : -1f;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
            elapsed = 0f;
        }

        // The results stay up while gameplay is paused, so the shine uses real time.
        private void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            if (rows == null) return;
            if (rowBars.Length != rows.Length)
            {
                rowBars = new Graphic[rows.Length];
                for (int i = 0; i < rows.Length; i++) rowBars[i] = rows[i] != null ? rows[i].GetComponent<Graphic>() : null;
            }

            float interval = Mathf.Max(0.5f, intervalSeconds);
            float sweep = Mathf.Clamp(sweepSeconds, 0.1f, interval);
            for (int row = 0; row < rows.Length; row++)
            {
                RectTransform bar = rows[row];
                if (bar == null || !bar.gameObject.activeInHierarchy) continue;
                float rowAlpha = rowBars[row] != null ? rowBars[row].canvasRenderer.GetAlpha() : 1f;
                if (rowAlpha < 0.99f) continue;
                float progress = Mathf.Repeat(elapsed - row * rowDelaySeconds, interval) / sweep;
                if (progress >= 1f) continue;

                Rect bounds = bar.rect;
                float left = bounds.xMin + bounds.width * inset.x;
                float right = bounds.xMax - bounds.width * inset.x;
                float bottom = bounds.yMin + bounds.height * inset.y;
                float top = bounds.yMax - bounds.height * inset.y;
                float half = bounds.width * bandWidth * 0.5f;
                float eased = progress * progress * (3f - 2f * progress);
                float center = Mathf.Lerp(left - half, right + half, eased);
                DrawBand(vertices, bar, center, half, left, right, bottom, top, rowAlpha);
            }
        }

        // Five columns across the band: clear edge, soft shoulder, bright middle, soft shoulder, clear edge.
        // Columns outside the bar body are clipped to its edges, and the light dims near the bar's ends.
        private void DrawBand(VertexHelper vertices, RectTransform bar, float center, float half,
            float left, float right, float bottom, float top, float rowAlpha)
        {
            float[] offsets = { -1f, -0.45f, 0f, 0.45f, 1f };
            int first = vertices.currentVertCount;
            int columns = 0;
            for (int i = 0; i < offsets.Length; i++)
            {
                float x = Mathf.Clamp(center + offsets[i] * half, left, right);
                // Brightness follows the clipped column's distance from the band middle.
                float distance = Mathf.Clamp01(Mathf.Abs(x - center) / Mathf.Max(0.0001f, half));
                float strength = distance <= 0.45f
                    ? Mathf.Lerp(1f, 0.55f, distance / 0.45f)
                    : Mathf.Lerp(0.55f, 0f, (distance - 0.45f) / 0.55f);
                float endFade = Mathf.Clamp01(Mathf.Min(x - left, right - x) / Mathf.Max(0.0001f, half));
                Color tint = shineColor * color;
                tint.a *= strength * endFade * opacity * rowAlpha;
                AddVertex(vertices, ToLocal(bar, new Vector2(x, bottom)), tint);
                AddVertex(vertices, ToLocal(bar, new Vector2(x, top)), tint);
                columns++;
            }

            for (int i = 0; i < columns - 1; i++)
            {
                int a = first + i * 2;
                vertices.AddTriangle(a, a + 1, a + 3);
                vertices.AddTriangle(a, a + 3, a + 2);
            }
        }

        private Vector2 ToLocal(RectTransform bar, Vector2 point) =>
            rectTransform.InverseTransformPoint(bar.TransformPoint(point));

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
