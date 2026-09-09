using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // Renders an engraved brass timer with distinct authored palettes for patience and eating.
    public sealed class TavernTimerGraphic : MaskableGraphic
    {
        [SerializeField] private Color frameColor = new Color(0.69f, 0.4f, 0.13f);
        [SerializeField] private Color highlightColor = new Color(1f, 0.86f, 0.56f);
        [SerializeField] private Color trackColor = new Color(0.19f, 0.105f, 0.09f);
        [SerializeField] private Color waitingColor = new Color(1f, 0.61f, 0.17f);
        [SerializeField] private Color eatingColor = new Color(0.22f, 0.8f, 0.59f);
        [SerializeField] private Color warningColor = new Color(0.93f, 0.26f, 0.22f);
        [SerializeField, Range(0f, 1f)] private float warningThreshold = 0.2f;
        private float progress = 1f;
        private bool eating;
        public float Progress => progress;
        public bool IsEating => eating;

        public void SetState(float remaining, bool isEating)
        {
            remaining = Mathf.Clamp01(remaining);
            if (Mathf.Approximately(progress, remaining) && eating == isEating) return;
            progress = remaining;
            eating = isEating;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            float halfWidth = rect.width * 0.5f - 5f;
            float halfHeight = rect.height * 0.5f;
            Vector2 center = rect.center;
            Panel(mesh, center + Vector2.down, halfWidth, halfHeight, 3f, trackColor);
            Panel(mesh, center, halfWidth, halfHeight, 3f, frameColor);
            Panel(mesh, center, halfWidth - 0.8f, halfHeight - 0.8f, 2.6f, highlightColor);
            Panel(mesh, center, halfWidth - 1.6f, halfHeight - 1.6f, 2f, trackColor);
            Color fill = eating ? eatingColor : progress <= warningThreshold ? warningColor : waitingColor;
            float width = (halfWidth - 2.5f) * 2f * progress;
            if (width > 0.05f)
            {
                Vector2 fillCenter = center + Vector2.right * (-halfWidth + 2.5f + width * 0.5f);
                Panel(mesh, fillCenter, width * 0.5f, halfHeight - 2.5f, Mathf.Min(1.4f, width * 0.25f), fill);
                Panel(mesh, fillCenter + Vector2.up * (halfHeight - 3.3f), width * 0.5f, 0.35f, 0f,
                    Color.Lerp(fill, highlightColor, 0.55f));
            }
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Vector2 jewel = center + Vector2.right * sign * (halfWidth + 1.2f);
                Panel(mesh, jewel, 3.4f, 3.4f, 3.4f, frameColor);
                Panel(mesh, jewel, 2.2f, 2.2f, 2.2f, eating ? eatingColor : waitingColor);
                Panel(mesh, jewel + new Vector2(-0.5f, 0.5f), 0.65f, 0.65f, 0.65f, highlightColor);
            }
        }

        private static void Panel(VertexHelper mesh, Vector2 center, float x, float y, float bevel, Color tint)
        {
            int first = mesh.currentVertCount;
            bevel = Mathf.Min(bevel, Mathf.Min(x, y));
            mesh.AddVert(center, tint, Vector2.zero);
            Vector2[] corners = { new Vector2(-x + bevel, -y), new Vector2(x - bevel, -y),
                new Vector2(x, -y + bevel), new Vector2(x, y - bevel), new Vector2(x - bevel, y),
                new Vector2(-x + bevel, y), new Vector2(-x, y - bevel), new Vector2(-x, -y + bevel) };
            for (int i = 0; i < corners.Length; i++) mesh.AddVert(center + corners[i], tint, Vector2.zero);
            for (int i = 0; i < corners.Length; i++) mesh.AddTriangle(first, first + i + 1, first + (i + 1) % 8 + 1);
        }
    }
}
