using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // Keeps the existing scene reference while presenting a warm woven restaurant curtain.
    public sealed class WallBlockTransitionGraphic : MaskableGraphic
    {
        [SerializeField, Min(0.01f)] private float darkenDuration = 0.7f;
        [SerializeField, Min(0.01f)] private float revealDuration = 1f;
        [SerializeField, Min(0f)] private float closedHoldSeconds = 0.18f;
        [SerializeField] private Color foldShadow = new Color(0.29f, 0.12f, 0.105f, 1f);
        [SerializeField] private Color foldHighlight = new Color(0.52f, 0.265f, 0.22f, 1f);
        [SerializeField] private Color gold = new Color(0.76f, 0.58f, 0.34f, 1f);
        [SerializeField] private Color cream = new Color(0.96f, 0.875f, 0.71f, 1f);
        private float closure;
        private bool visible;

        protected override void OnEnable()
        {
            base.OnEnable();
            if (Application.isPlaying) return;
            closure = 1f;
            visible = true;
            SetVerticesDirty();
        }

        public IEnumerator Cover()
        {
            gameObject.SetActive(true);
            yield return Animate(1f, darkenDuration);
        }

        public IEnumerator Reveal()
        {
            if (closedHoldSeconds > 0f) yield return new WaitForSecondsRealtime(closedHoldSeconds);
            yield return Animate(0f, revealDuration);
            Hide();
        }

        public void Hide()
        {
            visible = false;
            closure = 0f;
            SetVerticesDirty();
            gameObject.SetActive(false);
        }

        private IEnumerator Animate(float target, float duration)
        {
            visible = true;
            float start = closure;
            float elapsed = 0f;
            duration = Mathf.Max(0.01f, duration);
            while (elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = t * t * t * (t * (t * 6f - 15f) + 10f);
                closure = Mathf.Lerp(start, target, ease);
                SetVerticesDirty();
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            closure = target;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (!visible || closure <= 0f) return;
            Rect area = rectTransform.rect;
            DrawCurtain(mesh, area, false);
            DrawCurtain(mesh, area, true);
            float emblemAlpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.9f, 1f, closure));
            if (emblemAlpha <= 0f) return;
            float radius = area.height * 0.095f;
            Color rim = gold;
            Color face = cream;
            rim.a *= emblemAlpha;
            face.a *= emblemAlpha;
            DrawDisc(mesh, area.center, radius, rim);
            DrawDisc(mesh, area.center, radius * 0.93f, face);
            DrawDisc(mesh, area.center, radius * 0.8f, rim);
            DrawDisc(mesh, area.center, radius * 0.77f, face);
            // A serving cloche on a cream plate connects the transition to the restaurant.
            Color crest = color;
            crest.a *= emblemAlpha;
            DrawCloche(mesh, area.center, radius, crest);
        }

        private static void DrawCloche(VertexHelper mesh, Vector2 center, float radius, Color tint)
        {
            Vector2 baseline = center - Vector2.up * radius * 0.12f;
            float domeRadius = radius * 0.49f;
            int first = mesh.currentVertCount;
            mesh.AddVert(baseline, tint, Vector2.zero);
            const int segments = 24;
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI / segments;
                mesh.AddVert(baseline + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * domeRadius, tint, Vector2.zero);
                if (i > 0) mesh.AddTriangle(first, first + i, first + i + 1);
            }
            DrawDisc(mesh, baseline + Vector2.up * (domeRadius + radius * 0.075f), radius * 0.075f, tint);
            float plateY = baseline.y - radius * 0.1f;
            AddSeam(mesh, baseline.x + radius * 0.57f, baseline.x + radius * 0.57f,
                plateY - radius * 0.04f, plateY + radius * 0.04f, 1f, radius * 1.14f, tint);
        }

        private void DrawCurtain(VertexHelper mesh, Rect area, bool rightSide)
        {
            const int columns = 48;
            const int rows = 16;
            int first = mesh.currentVertCount;
            float sign = rightSide ? -1f : 1f;
            float outside = rightSide ? area.xMax : area.xMin;
            for (int row = 0; row <= rows; row++)
            {
                float y = row / (float)rows;
                float extent = CurtainExtent(area, y);
                for (int column = 0; column <= columns; column++)
                {
                    float x = column / (float)columns;
                    float phase = x * Mathf.PI * 10f + Mathf.Sin(y * Mathf.PI) * 0.65f +
                        Mathf.Sin(x * Mathf.PI * 3f) * 0.4f;
                    float fold = 0.5f + 0.5f * Mathf.Cos(phase);
                    Color tint = Color.Lerp(foldShadow, color, 0.6f + 0.4f * fold);
                    tint = Color.Lerp(tint, foldHighlight, fold * fold * 0.18f);
                    tint = Color.Lerp(tint, foldShadow, (1f - y) * 0.1f);
                    // Keep the curtain opaque; the scene changes only after the seam closes.
                    tint.a = 1f;
                    mesh.AddVert(new Vector2(outside + sign * extent * x, Mathf.Lerp(area.yMin, area.yMax, y)), tint, Vector2.zero);
                    if (row == rows || column == columns) continue;
                    int vertex = first + row * (columns + 1) + column;
                    mesh.AddTriangle(vertex, vertex + columns + 1, vertex + 1);
                    mesh.AddTriangle(vertex + 1, vertex + columns + 1, vertex + columns + 2);
                }
            }
            // Two narrow gold seams bend with the leading edge of each curtain panel.
            for (int row = 0; row < rows; row++)
            {
                float bottom = row / (float)rows;
                float top = (row + 1f) / rows;
                float x0 = outside + sign * CurtainExtent(area, bottom);
                float x1 = outside + sign * CurtainExtent(area, top);
                float seam = Mathf.Min(area.height * 0.005f, area.width * closure * 0.05f);
                AddSeam(mesh, x0, x1, Mathf.Lerp(area.yMin, area.yMax, bottom),
                    Mathf.Lerp(area.yMin, area.yMax, top), sign, seam, gold);
                // An inset cream stitch softens the trim like the existing parchment menu frames.
                AddSeam(mesh, x0 - sign * seam * 3f, x1 - sign * seam * 3f,
                    Mathf.Lerp(area.yMin, area.yMax, bottom), Mathf.Lerp(area.yMin, area.yMax, top),
                    sign, seam * 0.35f, cream);
            }
        }

        private float CurtainExtent(Rect area, float height)
        {
            float drape = Mathf.Sin(height * Mathf.PI) * area.width * 0.055f * closure * (1f - closure);
            return area.width * 0.5f * closure - drape;
        }

        private static void AddSeam(VertexHelper mesh, float bottomX, float topX, float bottomY,
            float topY, float sign, float width, Color tint)
        {
            int first = mesh.currentVertCount;
            mesh.AddVert(new Vector2(bottomX - sign * width, bottomY), tint, Vector2.zero);
            mesh.AddVert(new Vector2(topX - sign * width, topY), tint, Vector2.zero);
            mesh.AddVert(new Vector2(topX, topY), tint, Vector2.zero);
            mesh.AddVert(new Vector2(bottomX, bottomY), tint, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first, first + 2, first + 3);
        }

        private static void DrawDisc(VertexHelper mesh, Vector2 center, float radius, Color tint)
        {
            int first = mesh.currentVertCount;
            mesh.AddVert(center, tint, Vector2.zero);
            const int segments = 48;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, tint, Vector2.zero);
                mesh.AddTriangle(first, first + i + 1, first + (i + 1) % segments + 1);
            }
        }
    }
}
