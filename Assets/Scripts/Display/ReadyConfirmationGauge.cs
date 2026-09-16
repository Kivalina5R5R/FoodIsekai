using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // Draws an authored gilded fantasy gauge without textures or runtime hierarchy changes.
    public sealed class ReadyConfirmationGauge : MaskableGraphic
    {
        [SerializeField] private Color gold = new Color(0.76f, 0.61f, 0.39f, 1f);
        [SerializeField] private Color ivory = new Color(1f, 0.96f, 0.84f, 1f);
        [SerializeField] private Color well = new Color(0.91f, 0.84f, 0.68f, 1f);
        [SerializeField] private Color amber = new Color(0.76f, 0.56f, 0.29f, 1f);
        [SerializeField] private Color magic = new Color(0.91f, 0.73f, 0.43f, 1f);
        [SerializeField] private Color confirmed = new Color(0.7f, 0.78f, 0.49f, 1f);
        [SerializeField] private Color contestedColor = new Color(0.83f, 0.51f, 0.38f, 1f);

        private float progress;
        private bool isConfirmed;
        private bool isContested;

        // Updates live selection state; layout and palette remain authored on this component.
        public void ShowProgress(float value, bool claimed, bool contested)
        {
            value = Mathf.Clamp01(value);
            if (progress == value && isConfirmed == claimed && isContested == contested) return;
            progress = value;
            isConfirmed = claimed;
            isContested = contested;
            SetVerticesDirty();
        }

        private void Update()
        {
            if (progress > 0f && !isConfirmed && !isContested) SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect r = GetPixelAdjustedRect();
            Vector2 origin = r.center;
            float sx = Mathf.Min(r.width / 420f, r.height / 62f);
            float sy = sx;
            // A cream inset with a single muted gold rim echoes the surrounding player card.
            Capsule(mesh, origin, sx, sy, -190, 190, -16, 16, gold);
            Capsule(mesh, origin, sx, sy, -187, 187, -13, 13, ivory);
            Capsule(mesh, origin, sx, sy, -183, 183, -9, 9, well);

            Color energy = isConfirmed ? confirmed : isContested ? contestedColor : magic;
            float amount = isConfirmed ? 1f : progress;
            if (amount > 0f)
            {
                float end = Mathf.Lerp(-181f, 181f, amount);
                Capsule(mesh, origin, sx, sy, -181, end, -7, 7,
                    isConfirmed || isContested ? energy : amber);
                if (end > -167f)
                    Capsule(mesh, origin, sx, sy, -176, end - 5, 2, 4, energy);
                if (!isConfirmed && !isContested && end > -167f)
                {
                    float pulse = 2f + Mathf.Sin(Time.unscaledTime * 5f) * .5f;
                    Diamond(mesh, origin, sx, sy, end - 6, 0, pulse, 4, ivory);
                }
            }
        }

        private static void Capsule(VertexHelper mesh, Vector2 origin, float sx, float sy,
            float left, float right, float bottom, float top, Color tint)
        {
            if (right <= left) return;
            float radius = Mathf.Min((top - bottom) * .5f, (right - left) * .5f);
            float middle = (top + bottom) * .5f;
            int start = mesh.currentVertCount;
            Add(mesh, origin, sx, sy, (left + right) * .5f, middle, tint);
            const int steps = 12;
            for (int side = 0; side < 2; side++)
            {
                float center = side == 0 ? right - radius : left + radius;
                for (int i = 0; i <= steps; i++)
                {
                    float angle = (-90f + side * 180f + i * 180f / steps) * Mathf.Deg2Rad;
                    Add(mesh, origin, sx, sy, center + Mathf.Cos(angle) * radius,
                        middle + Mathf.Sin(angle) * radius, tint);
                }
            }
            int count = (steps + 1) * 2;
            for (int i = 0; i < count; i++)
                mesh.AddTriangle(start, start + 1 + (i + 1) % count, start + 1 + i);
        }

        private static void Diamond(VertexHelper mesh, Vector2 origin, float sx, float sy,
            float x, float y, float width, float height, Color tint)
        {
            int start = mesh.currentVertCount;
            Add(mesh, origin, sx, sy, x - width, y, tint);
            Add(mesh, origin, sx, sy, x, y + height, tint);
            Add(mesh, origin, sx, sy, x + width, y, tint);
            Add(mesh, origin, sx, sy, x, y - height, tint);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }

        private static void Add(VertexHelper mesh, Vector2 origin, float sx, float sy,
            float x, float y, Color tint)
        {
            mesh.AddVert(origin + new Vector2(x * sx, y * sy), tint, Vector2.zero);
        }
    }
}
