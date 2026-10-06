using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // Brings the result screen's MVP badge to life: a small heartbeat pop of the badge itself
    // and a few soft light motes drifting up around it.
    // Place this graphic just before the badge in the hierarchy so the motes sit behind it.
    // Everything fades with the badge, so nothing shows before the reveal brings the badge in.
    [AddComponentMenu("Food Isekai Z/Display/Result MVP Effect")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ResultMvpEffect : MaskableGraphic
    {
        [SerializeField] private RectTransform badge;
        [SerializeField, Min(0.3f)] private float beatSeconds = 1.6f;
        [SerializeField, Range(0f, 0.2f)] private float popScale = 0.06f;
        // The badge also grows while the row shine passes over it, peaking as the light crosses its middle.
        [SerializeField] private ResultRowShine shine;
        [SerializeField, Range(0f, 0.3f)] private float shinePopScale = 0.12f;

        [Header("Motes")]
        [SerializeField, Range(0, 8)] private int moteCount = 4;
        [SerializeField] private Vector2 moteSize = new Vector2(2.5f, 4.5f);
        [SerializeField, Min(0f)] private float moteRise = 8f;
        [SerializeField, Range(0f, 1f)] private float moteOpacity = 0.75f;
        [SerializeField] private Color moteColor = new Color(1f, 0.82f, 0.4f, 1f);

        private float elapsed;
        private Vector3 authoredScale = Vector3.one;
        private Graphic badgeGraphic;

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
            elapsed = 0f;
            if (badge != null)
            {
                authoredScale = badge.localScale;
                badgeGraphic = badge.GetComponent<Graphic>();
            }
        }

        protected override void OnDisable()
        {
            if (badge != null) badge.localScale = authoredScale;
            base.OnDisable();
        }

        // The results stay up while gameplay is paused, so the effect uses real time.
        private void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            if (badge != null)
            {
                float sweep = shine != null ? shine.GetSweepProgress(badge) : -1f;
                float shinePop = sweep >= 0f ? Mathf.Sin(sweep * Mathf.PI) * shinePopScale : 0f;
                // The larger of the two pops wins, so the beat never stacks on top of the shine.
                badge.localScale = authoredScale * (1f + Mathf.Max(Beat() * popScale, shinePop));
            }
            SetVerticesDirty();
        }

        // A quick swell at the start of each beat, then a rest until the next one.
        private float Beat()
        {
            float phase = Mathf.Repeat(elapsed / Mathf.Max(0.3f, beatSeconds), 1f);
            return Mathf.Sin(Mathf.Clamp01(phase / 0.3f) * Mathf.PI);
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            if (badge == null || !badge.gameObject.activeInHierarchy) return;
            float badgeAlpha = badgeGraphic != null ? badgeGraphic.canvasRenderer.GetAlpha() : 1f;
            if (badgeAlpha <= 0.01f) return;

            Rect bounds = badge.rect;
            float cycle = Mathf.Max(0.3f, beatSeconds) * 2f;
            for (int index = 0; index < moteCount; index++)
            {
                float clock = elapsed + (float)index / Mathf.Max(1, moteCount) * cycle;
                float age = Mathf.Repeat(clock, cycle) / cycle;
                int generation = Mathf.FloorToInt(clock / cycle);
                float along = Mathf.Repeat(index * 0.754878f + generation * 0.56984f, 1f);
                // Motes rise from around the badge's lower half and fade as they climb past it.
                Vector2 start = new Vector2(Mathf.Lerp(bounds.xMin, bounds.xMax, along),
                    Mathf.Lerp(bounds.yMin, bounds.center.y, Mathf.Repeat(along * 1.7f, 1f)));
                Vector2 local = ToLocal(start);
                Vector2 position = local + Vector2.up * age * moteRise;
                float fade = Mathf.Clamp01(age / 0.2f) * Mathf.Clamp01((1f - age) / 0.45f);
                Color mote = moteColor * color;
                mote.a *= fade * moteOpacity * badgeAlpha;
                DrawBokeh(vertices, position, Mathf.Lerp(moteSize.x, Mathf.Max(moteSize.x, moteSize.y), along), mote);
            }
        }

        private Vector2 ToLocal(Vector2 badgePoint) =>
            rectTransform.InverseTransformPoint(badge.TransformPoint(badgePoint));

        // A round light with a brighter core and a soft edge, matching the row motes.
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
            AddRings(vertices, first, segments);
        }

        // Joins a center vertex and pairs of inner/outer ring vertices into a filled two-band disc.
        private static void AddRings(VertexHelper vertices, int first, int segments)
        {
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
