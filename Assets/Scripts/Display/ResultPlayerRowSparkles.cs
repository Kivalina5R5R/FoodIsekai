using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // Soft golden light motes (round bokeh) drifting slowly up from behind each visible player bar on the result screen.
    // Place this graphic before the rows in the hierarchy so it draws behind them.
    // Motes fade together with their row, so they arrive with the row's entrance.
    [AddComponentMenu("Food Isekai Z/Display/Result Player Row Sparkles")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ResultPlayerRowSparkles : MaskableGraphic
    {
        [SerializeField] private RectTransform[] rows = new RectTransform[0];
        [SerializeField, Range(1, 10)] private int particlesPerRow = 5;
        [SerializeField, Min(0.5f)] private float cycleSeconds = 4f;
        [SerializeField, Min(0.2f)] private float lifetimeSeconds = 3.2f;
        [SerializeField] private Vector2 sizeRange = new Vector2(2.5f, 5.5f);
        [SerializeField, Min(0f)] private float risePixels = 8f;
        [SerializeField, Min(0f)] private float edgeSpread = 4f;
        // Warm gold reads against the cream parchment; a pale cream would disappear into it.
        [SerializeField, Range(0f, 1f)] private float opacity = 0.7f;
        [SerializeField] private Color goldColor = new Color(0.98f, 0.66f, 0.2f, 1f);
        [SerializeField] private Color creamColor = new Color(1f, 0.9f, 0.6f, 1f);

        private float elapsed;
        private Graphic[] rowBars = new Graphic[0];

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
            elapsed = 0f;
        }

        // The results stay up while gameplay is paused, so the motes use real time.
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

            int count = Mathf.Clamp(particlesPerRow, 1, 10);
            float cycle = Mathf.Max(0.5f, cycleSeconds);
            float lifetime = Mathf.Clamp(lifetimeSeconds, 0.2f, cycle);
            for (int row = 0; row < rows.Length; row++)
            {
                RectTransform bar = rows[row];
                if (bar == null || !bar.gameObject.activeInHierarchy) continue;
                float rowAlpha = rowBars[row] != null ? rowBars[row].canvasRenderer.GetAlpha() : 1f;
                if (rowAlpha <= 0.01f) continue;
                Rect bounds = bar.rect;
                for (int index = 0; index < count; index++)
                {
                    // Spread each row's motes through the cycle and give every row its own rhythm.
                    float clock = elapsed + ((float)index / count + row * 0.37f) * cycle;
                    float age = Mathf.Repeat(clock, cycle) / lifetime;
                    if (age >= 1f) continue;
                    int generation = Mathf.FloorToInt(clock / cycle);
                    float along = Mathf.Repeat(index * 0.754878f + generation * 0.56984f + row * 0.31f, 1f);
                    bool above = Mathf.Repeat(index * 0.618034f + generation * 0.4142f, 1f) < 0.6f;

                    // Motes start along the bar's top or bottom edge, away from the pointed ends.
                    float x = Mathf.Lerp(bounds.xMin + bounds.width * 0.1f, bounds.xMax - bounds.width * 0.1f, along);
                    Vector3 local = rectTransform.InverseTransformPoint(bar.TransformPoint(
                        new Vector2(x, above ? bounds.yMax : bounds.yMin)));
                    float sway = Mathf.Sin((age * 0.8f + along) * Mathf.PI * 2f) * edgeSpread * 0.4f;
                    Vector2 position = new Vector2(local.x + sway,
                        local.y + (above ? edgeSpread * 0.3f : -edgeSpread * 0.3f) + age * risePixels);

                    // Fade in quickly, linger, then fade out softly.
                    float fade = Mathf.Clamp01(age / 0.2f) * Mathf.Clamp01((1f - age) / 0.45f);
                    float size = Mathf.Lerp(sizeRange.x, Mathf.Max(sizeRange.x, sizeRange.y), along);
                    Color tint = Color.Lerp(goldColor, creamColor, along) * color;
                    tint.a *= fade * opacity * rowAlpha;
                    DrawBokeh(vertices, position, size, tint);
                }
            }
        }

        // A round light with a brighter core and a soft edge, like an out-of-focus highlight.
        private static void DrawBokeh(VertexHelper vertices, Vector2 position, float radius, Color tint)
        {
            const int segments = 20;
            int first = vertices.currentVertCount;
            Color core = tint;
            Color middle = tint;
            middle.a *= 0.55f;
            Color edge = tint;
            edge.a = 0f;
            AddVertex(vertices, position, core);
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
