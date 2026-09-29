using FoodIsekaiZ.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // Highlights the special menu with a rainbow border and orbiting magical sparkles.
    public sealed class SpecialMenuFrame : MaskableGraphic
    {
        [SerializeField] private FoodIsekaiZGameManager gameManager;
        [SerializeField] private FoodType food;
        [SerializeField] private Image stationImage;
        [SerializeField, Min(1)] private float thickness = 5;
        [SerializeField, Min(0)] private float cyclesPerSecond = .2f;
        [Tooltip("Closed border contour in normalized background coordinates, centered on zero.")]
        [SerializeField] private Vector2[] borderContour;
        [Header("Rainbow Magic")]
        [SerializeField, Range(4, 12)] private int starCount = 7;
        [SerializeField, Range(8, 32)] private int moteCount = 20;
        [SerializeField, Min(1f)] private float starSize = 4.5f;
        [SerializeField, Min(1f)] private float particleSpread = 12f;
        private bool visible;

        private void Update()
        {
            bool next = gameManager != null && gameManager.SpecialMenuFood == food &&
                gameManager.CurrentMealWavePhase == MealWavePhase.Active && !gameManager.IsPhasePresentationPaused &&
                stationImage != null && stationImage.isActiveAndEnabled;
            if (visible || next) SetVerticesDirty();
            visible = next;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (!visible || borderContour == null || borderContour.Length < 3) return;
            Rect rect = GetPixelAdjustedRect();
            DrawBorder(mesh, rect, thickness + 5f, 4f, .18f);
            DrawBorder(mesh, rect, thickness, .4f, 1f);
            DrawMagic(mesh, rect);
        }

        private void DrawBorder(VertexHelper mesh, Rect rect, float width, float softness, float opacity)
        {
            int firstVertex = mesh.currentVertCount;
            int segments = borderContour.Length;
            for (int i = 0; i <= segments; i++)
            {
                float phase = i / (float)segments;
                int current = i % segments;
                Vector2 point = rect.center + Vector2.Scale(borderContour[current], rect.size);
                Vector2 previous = Vector2.Scale(borderContour[(current + segments - 1) % segments], rect.size);
                Vector2 next = Vector2.Scale(borderContour[(current + 1) % segments], rect.size);
                Vector2 tangent = (next - previous).normalized;
                Vector2 normal = new Vector2(-tangent.y, tangent.x);
                Color tint = Color.HSVToRGB(Mathf.Repeat(phase + Time.unscaledTime * cyclesPerSecond, 1), .7f, 1);
                tint.a = color.a * opacity;
                Color feather = tint;
                feather.a = 0;
                float halfWidth = width * .5f;
                mesh.AddVert(point - normal * (halfWidth + softness), feather, Vector2.zero);
                mesh.AddVert(point - normal * halfWidth, tint, Vector2.zero);
                mesh.AddVert(point + normal * halfWidth, tint, Vector2.zero);
                mesh.AddVert(point + normal * (halfWidth + softness), feather, Vector2.zero);
                if (i == 0) continue;
                int start = firstVertex + (i - 1) * 4;
                for (int strip = 0; strip < 3; strip++)
                {
                    mesh.AddTriangle(start + strip, start + strip + 1, start + strip + 4);
                    mesh.AddTriangle(start + strip + 4, start + strip + 1, start + strip + 5);
                }
            }
        }

        private Vector2 BorderPoint(Rect rect, float phase)
        {
            float index = Mathf.Repeat(phase, 1f) * borderContour.Length;
            int first = Mathf.FloorToInt(index);
            Vector2 point = Vector2.Lerp(borderContour[first], borderContour[(first + 1) % borderContour.Length], index - first);
            return rect.center + Vector2.Scale(point, rect.size);
        }

        private void DrawMagic(VertexHelper mesh, Rect rect)
        {
            float time = Time.unscaledTime;
            for (int i = 0; i < starCount; i++)
            {
                float phase = i / (float)starCount + time * .035f;
                Vector2 point = BorderPoint(rect, phase);
                Vector2 outward = (point - rect.center).normalized;
                float pulse = .5f + .5f * Mathf.Sin(time * 3.1f + i * 2.4f);
                point += outward * (4f + particleSpread * (.3f + .25f * pulse));
                Color tint = MagicColor(phase + time * cyclesPerSecond, .65f + .35f * pulse);
                float radius = starSize * (.7f + .5f * pulse);
                DrawGlow(mesh, point, radius * 2.4f, tint, .28f);
                DrawStar(mesh, point, radius, time * .25f + i, tint);
            }
            for (int i = 0; i < moteCount; i++)
            {
                float life = Mathf.Repeat(time / 2.4f + i * .618034f, 1f);
                Vector2 origin = BorderPoint(rect, i * .381966f);
                Vector2 outward = (origin - rect.center).normalized;
                Vector2 point = origin + outward * (3f + particleSpread * life);
                point += Vector2.up * (life * 16f) + Vector2.right * (Mathf.Sin(life * 6f + i) * 2f);
                float fade = Mathf.Sin(life * Mathf.PI);
                Color tint = MagicColor(i * .137f + time * cyclesPerSecond, fade * .8f);
                float radius = .8f + (i % 3) * .35f;
                DrawGlow(mesh, point, radius * 3f, tint, .3f);
                DrawStar(mesh, point, radius, i, tint);
            }
        }

        private Color MagicColor(float phase, float opacity)
        {
            Color tint = Color.HSVToRGB(Mathf.Repeat(phase, 1f), .6f, 1f);
            tint.a = opacity * color.a;
            return tint;
        }

        private static void DrawGlow(VertexHelper mesh, Vector2 center, float radius, Color tint, float opacity)
        {
            int first = mesh.currentVertCount;
            tint.a *= opacity;
            mesh.AddVert(center, tint, Vector2.zero);
            tint.a = 0f;
            const int segments = 12;
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, tint, Vector2.zero);
                if (i > 0) mesh.AddTriangle(first, first + i, first + i + 1);
            }
        }

        private static void DrawStar(VertexHelper mesh, Vector2 center, float radius, float rotation, Color tint)
        {
            int first = mesh.currentVertCount;
            Color core = Color.Lerp(tint, Color.white, .85f);
            core.a = tint.a;
            mesh.AddVert(center, core, Vector2.zero);
            for (int i = 0; i <= 8; i++)
            {
                float angle = rotation + i * Mathf.PI * .25f;
                float length = (i % 2 == 0 ? 1f : .2f) * radius;
                mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * length, tint, Vector2.zero);
                if (i > 0) mesh.AddTriangle(first, first + i, first + i + 1);
            }
        }
    }
}
