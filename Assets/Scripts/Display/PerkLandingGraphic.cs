using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // The card lands face-on with an anime "don!": bright rings, radial speed lines and kira sparkles leave its edge.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PerkLandingGraphic : MaskableGraphic
    {
        private const int SpeedLines = 36;
        private const int Sparkles = 16;
        private const int Bubbles = 12;
        private float progress = -1f;
        private bool rainbow;

        // The Big tier recolors everything in a pastel rainbow and adds a third ripple and more sparkles.
        public void SetRainbow(bool enabled)
        {
            rainbow = enabled;
            SetVerticesDirty();
        }

        public void SetImpact(float amount)
        {
            raycastTarget = false;
            progress = amount;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (progress < 0f || progress >= 1f) return;
            Rect rect = rectTransform.rect;
            Vector2 contact = rect.center;
            float travel = 1f - Mathf.Pow(1f - progress, 3f);
            float fade = Mathf.Pow(1f - progress, 1.5f);
            float amount = rainbow ? 1.5f : 1f;
            DrawRipple(mesh, contact, rect.size, progress, rainbow ? PerkEffectMesh.Rainbow(progress * .5f) : PerkEffectMesh.Cream, .45f);
            if (progress > .12f)
                DrawRipple(mesh, contact, rect.size, (progress - .12f) / .88f,
                    rainbow ? PerkEffectMesh.Rainbow(.33f + progress * .5f) : PerkEffectMesh.Gold, .3f);
            if (rainbow && progress > .24f)
                DrawRipple(mesh, contact, rect.size, (progress - .24f) / .76f, PerkEffectMesh.Rainbow(.66f + progress * .5f), .3f);
            for (int i = 0; i < SpeedLines; i++)
            {
                float seed = PerkEffectMesh.Hash(i, 1f);
                Vector2 direction = Direction((i + seed * .7f) / SpeedLines);
                Vector2 origin = contact + direction * Boundary(rect, direction);
                Vector2 from = origin + direction * rect.height * travel * (.04f + seed * .08f);
                Vector2 tip = from + direction * rect.height * (.12f + seed * .22f) * (1f - progress * .7f);
                Color line = PerkEffectMesh.Tint(Color.white, fade * (.35f + seed * .45f));
                PerkEffectMesh.Spike(mesh, from, tip, rect.height * (.012f + seed * .012f) * (1f - progress),
                    line, PerkEffectMesh.Tint(Color.white, 0f));
            }
            float pop = Mathf.Min(1f, progress * 9f);
            int sparkles = Mathf.RoundToInt(Sparkles * amount);
            for (int i = 0; i < sparkles; i++)
            {
                float seed = PerkEffectMesh.Hash(i, 2f);
                Vector2 direction = Direction(seed);
                Vector2 point = contact + direction * (Boundary(rect, direction) + rect.height * travel * (.08f + seed * .3f)) +
                    Vector2.up * progress * rect.height * .05f;
                Color tint = rainbow ? PerkEffectMesh.Rainbow(seed + progress * .4f)
                    : i % 3 == 0 ? PerkEffectMesh.Blush : i % 3 == 1 ? PerkEffectMesh.Gold : PerkEffectMesh.Cream;
                float size = rect.height * (.035f + seed * .045f) * pop * (1f - progress * .6f);
                PerkEffectMesh.Sparkle(mesh, point, size, seed * .6f, PerkEffectMesh.Tint(tint, fade));
            }
            // Pastel light bubbles drift upward after the hit, softening the landing into the reaction shot.
            int bubbles = Mathf.RoundToInt(Bubbles * amount);
            for (int i = 0; i < bubbles; i++)
            {
                float seed = PerkEffectMesh.Hash(i, 4f);
                Vector2 direction = Direction(seed);
                Vector2 point = contact + direction * Boundary(rect, direction) * (1f + travel * .15f) +
                    Vector2.up * rect.height * progress * (.1f + seed * .2f);
                Color tint = rainbow ? PerkEffectMesh.Rainbow(seed * 3f) : i % 2 == 0 ? PerkEffectMesh.Blush : PerkEffectMesh.Peach;
                PerkEffectMesh.Glow(mesh, point, rect.height * (.015f + seed * .025f) * pop,
                    PerkEffectMesh.Tint(tint, fade * .55f), 16);
            }
        }

        private static Vector2 Direction(float turn) =>
            new Vector2(Mathf.Cos(turn * Mathf.PI * 2f), Mathf.Sin(turn * Mathf.PI * 2f));

        private static float Boundary(Rect rect, Vector2 direction) =>
            Mathf.Min(rect.width * .5f / Mathf.Max(.001f, Mathf.Abs(direction.x)),
                rect.height * .5f / Mathf.Max(.001f, Mathf.Abs(direction.y)));

        private static void DrawRipple(VertexHelper mesh, Vector2 center, Vector2 size, float age, Color color, float opacity)
        {
            float spread = 1f - Mathf.Pow(1f - age, 3f);
            Vector2 radius = Vector2.one * size.y * (.42f + spread * .5f);
            PerkEffectMesh.Ring(mesh, center, radius, .03f, PerkEffectMesh.Tint(color, Mathf.Pow(1f - age, 2f) * opacity));
        }
    }
}
