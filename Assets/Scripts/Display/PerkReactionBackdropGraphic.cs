using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // The anime food-reaction backdrop: a turning golden sunburst behind the card and flickering manga focus lines.
    // The Big tier turns the sunburst into a rainbow, adds a counter-rotating second layer and a rainbow halo.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PerkReactionBackdropGraphic : MaskableGraphic
    {
        private const int Wedges = 28;
        private const int FocusLines = 84;
        private const int HaloSegments = 120;
        private float strength;
        private float seconds;
        private float flare;
        private bool rainbow;

        public void SetRainbow(bool enabled)
        {
            rainbow = enabled;
            SetVerticesDirty();
        }

        // Amount fades the whole backdrop; burst briefly brightens and speeds up the sunburst at the release.
        public void SetReaction(float amount, float time, float burst = 0f)
        {
            raycastTarget = false;
            strength = Mathf.Clamp01(amount);
            seconds = time;
            flare = Mathf.Clamp01(burst);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (strength <= 0f) return;
            Rect rect = rectTransform.rect;
            float reach = rect.size.magnitude * .62f;
            PerkEffectMesh.Glow(mesh, Vector2.zero, rect.height * (.9f + flare * .4f),
                PerkEffectMesh.Tint(rainbow ? Color.white : PerkEffectMesh.Peach, strength * (.22f + flare * .25f)), 48);
            if (rainbow) DrawSunburst(mesh, rect, reach * 1.1f, -seconds * .06f, Wedges * 2, .45f, .5f);
            DrawSunburst(mesh, rect, reach, seconds * (.1f + flare * .5f), Wedges, 1f, 0f);
            if (rainbow) DrawHalo(mesh, rect);
            DrawFocusLines(mesh, rect, reach);
        }

        private void DrawSunburst(VertexHelper mesh, Rect rect, float reach, float turn, int wedges, float opacity, float hueOffset)
        {
            float inner = rect.height * .55f;
            // Only alternate wedges are lit, which gives the classic striped reaction-shot sunburst.
            for (int i = 0; i < wedges; i += 2)
            {
                float from = turn + i * Mathf.PI * 2f / wedges;
                float to = from + Mathf.PI * 2f / wedges;
                Vector2 a = new Vector2(Mathf.Cos(from), Mathf.Sin(from));
                Vector2 b = new Vector2(Mathf.Cos(to), Mathf.Sin(to));
                float hue = i / (float)wedges + seconds * .08f + hueOffset;
                Color core = rainbow ? PerkEffectMesh.Tint(Color.white, strength * (.3f + flare * .45f) * opacity)
                    : PerkEffectMesh.Tint(PerkEffectMesh.Cream, strength * (.3f + flare * .45f));
                Color middle = rainbow ? PerkEffectMesh.Rainbow(hue, strength * (.28f + flare * .3f) * opacity, .6f)
                    : PerkEffectMesh.Tint(PerkEffectMesh.Gold, strength * (.2f + flare * .3f));
                Color clear = PerkEffectMesh.Tint(middle, 0f);
                int start = mesh.currentVertCount;
                mesh.AddVert(Vector2.zero, core, Vector2.zero);
                mesh.AddVert(a * inner, middle, Vector2.zero);
                mesh.AddVert(b * inner, middle, Vector2.zero);
                mesh.AddTriangle(start, start + 1, start + 2);
                PerkEffectMesh.Quad(mesh, a * inner, b * inner, b * reach, a * reach, middle, clear);
            }
        }

        // A turning rainbow ring just outside the card, like a halo over a legendary dish.
        private void DrawHalo(VertexHelper mesh, Rect rect)
        {
            float radius = rect.height * (.72f + flare * .25f);
            float width = radius * .06f;
            for (int i = 0; i < HaloSegments; i++)
            {
                float from = i * Mathf.PI * 2f / HaloSegments + seconds * .4f;
                float to = (i + 1) * Mathf.PI * 2f / HaloSegments + seconds * .4f;
                Vector2 a = new Vector2(Mathf.Cos(from), Mathf.Sin(from));
                Vector2 b = new Vector2(Mathf.Cos(to), Mathf.Sin(to));
                Color band = PerkEffectMesh.Rainbow(i / (float)HaloSegments, strength * (.55f + flare * .4f), .65f);
                Color clear = PerkEffectMesh.Tint(band, 0f);
                PerkEffectMesh.Quad(mesh, a * radius, b * radius, b * (radius - width), a * (radius - width), band, clear);
                PerkEffectMesh.Quad(mesh, a * radius, b * radius, b * (radius + width), a * (radius + width), band, clear);
            }
        }

        private void DrawFocusLines(VertexHelper mesh, Rect rect, float reach)
        {
            // Lines are redrawn in a new random set several times a second, like hand-drawn manga speed lines.
            float frame = Mathf.Floor(seconds * 14f);
            float appear = Mathf.SmoothStep(0f, 1f, strength);
            for (int i = 0; i < FocusLines; i++)
            {
                float seed = PerkEffectMesh.Hash(i, frame);
                float depth = PerkEffectMesh.Hash(i + 211f, frame);
                float angle = (i + seed * .9f) / FocusLines * Mathf.PI * 2f;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                // The lines stop on an ellipse around the card so the artwork stays clean.
                float radiusX = rect.height * (.5f + depth * .45f);
                float radiusY = rect.height * (.5f + depth * .12f);
                float stop = 1f / Mathf.Sqrt(Mathf.Pow(direction.x / radiusX, 2f) + Mathf.Pow(direction.y / radiusY, 2f));
                Vector2 outer = direction * reach;
                Vector2 inner = direction * Mathf.Lerp(stop, reach * .8f, 1f - appear);
                float width = rect.height * (.006f + seed * .018f);
                Color line = PerkEffectMesh.Tint(Color.white, appear * (.18f + seed * .32f) * (1f - flare * .6f));
                PerkEffectMesh.Spike(mesh, outer, inner, width, line, PerkEffectMesh.Tint(Color.white, 0f));
            }
        }
    }
}
