using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // Renders the carried-money fraction inside an authored, left-side crescent.
    public sealed class PlayerMoneyMeter : MaskableGraphic
    {
        [SerializeField] private Vector2 arcCenter = new Vector2(-7f, -5f);
        [SerializeField, Min(1f)] private float radius = 94f;
        [SerializeField, Range(1f, 150f)] private float arcDegrees = 68f;
        [SerializeField, Min(10f)] private float width = 18f;
        [SerializeField] private Color edgeColor = new Color(0.22f, 0.09f, 0.04f, 1f);
        [SerializeField] private Color goldColor = new Color(0.85f, 0.58f, 0.22f, 1f);
        [SerializeField] private Color trackColor = new Color(0.23f, 0.12f, 0.14f, 1f);
        [SerializeField] private Color highlightColor = new Color(1f, 0.94f, 0.62f, 1f);
        [SerializeField] private Color jewelColor = new Color(0.62f, 0.1f, 0.19f, 1f);
        [SerializeField, Min(0.01f)] private float fillSeconds = 0.2f;

        private float targetFill;
        private float displayedFill;
        private float fillVelocity;

        // Invalid capacity draws an empty track; over-capacity values are clamped.
        public void SetAmount(int amount, int capacity)
        {
            targetFill = capacity > 0 ? Mathf.Clamp01((float)amount / capacity) : 0f;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            targetFill = 0f;
            displayedFill = 0f;
            fillVelocity = 0f;
        }

        private void Update()
        {
            if (displayedFill == targetFill) return;
            displayedFill = Mathf.SmoothDamp(displayedFill, targetFill, ref fillVelocity,
                Mathf.Max(0.01f, fillSeconds), Mathf.Infinity, Time.unscaledDeltaTime);
            if (Mathf.Abs(displayedFill - targetFill) < 0.0001f)
            {
                displayedFill = targetFill;
                fillVelocity = 0f;
            }
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            float bottom = 180f + arcDegrees * 0.5f;
            float top = 180f - arcDegrees * 0.5f;
            AddArc(mesh, bottom, top, radius, width + 2f, edgeColor);
            AddArc(mesh, bottom, top, radius, width, goldColor);
            AddArc(mesh, bottom, top, radius, width - 5f, trackColor);

            if (displayedFill > 0.001f)
            {
                float tip = Mathf.Lerp(bottom, top, displayedFill);
                AddArc(mesh, bottom, tip, radius, width - 8f, color);
                AddArc(mesh, bottom, tip, radius + 2f, 1.5f, highlightColor);
            }

            // Small divisions make capacity readable without a number over the coins.
            for (int i = 1; i < 5; i++)
            {
                float angle = Mathf.Lerp(bottom, top, i / 5f);
                AddArc(mesh, angle + 0.45f, angle - 0.45f, radius, width - 6f, edgeColor, false);
            }
            AddJewel(mesh, Point(top, radius), 7f, edgeColor);
            AddJewel(mesh, Point(top, radius), 5.5f, goldColor);
            AddJewel(mesh, Point(top, radius), 3.5f, jewelColor);
        }

        private Vector2 Point(float degrees, float distance)
        {
            float angle = degrees * Mathf.Deg2Rad;
            return rectTransform.rect.center + arcCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
        }

        private void AddArc(VertexHelper mesh, float start, float end, float distance,
            float thickness, Color tint, bool rounded = true)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(end - start) / 2f));
            float half = thickness * 0.5f;
            int first = mesh.currentVertCount;
            for (int i = 0; i <= steps; i++)
            {
                float angle = Mathf.Lerp(start, end, i / (float)steps);
                mesh.AddVert(Point(angle, distance - half), tint, Vector2.zero);
                mesh.AddVert(Point(angle, distance + half), tint, Vector2.zero);
                if (i == 0) continue;
                int current = first + i * 2;
                mesh.AddTriangle(current - 2, current - 1, current);
                mesh.AddTriangle(current, current - 1, current + 1);
            }
            if (!rounded) return;
            AddCap(mesh, Point(start, distance), half, tint);
            AddCap(mesh, Point(end, distance), half, tint);
        }

        private static void AddCap(VertexHelper mesh, Vector2 center, float size, Color tint)
        {
            int first = mesh.currentVertCount;
            mesh.AddVert(center, tint, Vector2.zero);
            const int steps = 16;
            for (int i = 0; i <= steps; i++)
            {
                float angle = i * Mathf.PI * 2f / steps;
                mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * size, tint, Vector2.zero);
                if (i > 0) mesh.AddTriangle(first, first + i, first + i + 1);
            }
        }

        private static void AddJewel(VertexHelper mesh, Vector2 center, float size, Color tint)
        {
            int first = mesh.currentVertCount;
            mesh.AddVert(center + Vector2.up * size, tint, Vector2.zero);
            mesh.AddVert(center + Vector2.right * size * 0.7f, tint, Vector2.zero);
            mesh.AddVert(center - Vector2.up * size, tint, Vector2.zero);
            mesh.AddVert(center - Vector2.right * size * 0.7f, tint, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first, first + 2, first + 3);
        }
    }
}
