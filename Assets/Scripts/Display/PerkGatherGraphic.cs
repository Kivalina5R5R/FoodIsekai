using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // Golden aroma-light streams in from the whole wall, spirals around the card and gathers on its frame.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PerkGatherGraphic : MaskableGraphic
    {
        private const int Motes = 72;
        private const int OrbitSparkles = 20;
        private float progress;
        private float charge;
        private float opacity = 1f;
        private Rect frameBounds;
        private bool rainbow;

        public void SetFrame(Rect bounds) => frameBounds = bounds;

        // The Big tier uses rainbow motes, half again as many of them, and a second counter-rotating orbit.
        public void SetRainbow(bool enabled)
        {
            rainbow = enabled;
            SetVerticesDirty();
        }

        public void SetEnergy(float gathering, float holding, float alpha = 1f)
        {
            progress = Mathf.Clamp01(gathering);
            charge = Mathf.Clamp01(holding);
            opacity = Mathf.Clamp01(alpha);
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (progress <= 0f || opacity <= 0f) return;
            Rect rect = rectTransform.rect;
            Vector2 frameSize = frameBounds.size.sqrMagnitude > 1f ? frameBounds.size : new Vector2(rect.height * .6f, rect.height * .85f);
            // A true circle that clears the card's corners; it may run past the wall edges. It tightens while charging.
            Vector2 orbit = Vector2.one * frameSize.magnitude * .62f * (1f - charge * .08f);
            int motes = rainbow ? Motes * 3 / 2 : Motes;
            for (int i = 0; i < motes; i++) DrawMote(mesh, rect, orbit, i);
            DrawOrbit(mesh, rect, orbit, 1f, OrbitSparkles, 0f);
            if (rainbow) DrawOrbit(mesh, rect, orbit * 1.22f, -.7f, OrbitSparkles + 8, .5f);
            DrawCardShine(mesh, rect, frameSize);
        }

        private void DrawMote(VertexHelper mesh, Rect rect, Vector2 orbit, int index)
        {
            float seed = PerkEffectMesh.Hash(index);
            float launch = PerkEffectMesh.Hash(index, 3f) * .72f;
            float travel = .22f + seed * .16f;
            float life = (progress - launch) / travel;
            if (life <= 0f || life >= 1f) return;
            float angle = index * 2.39996f;
            Vector2 wall = new Vector2(rect.width * .52f, rect.height * .62f);
            Vector2 head = Spiral(wall, orbit, angle, life);
            Vector2 tail = Spiral(wall, orbit, angle, Mathf.Max(0f, life - .16f));
            float alpha = Mathf.Sin(Mathf.PI * Mathf.Min(1f, life * 1.15f)) * opacity;
            Color tint = rainbow ? PerkEffectMesh.Rainbow(seed + progress * .3f, 1f, .6f)
                : index % 5 == 0 ? PerkEffectMesh.Blush : index % 3 == 0 ? PerkEffectMesh.Cream : PerkEffectMesh.Gold;
            float size = rect.height * (.012f + seed * .016f);
            PerkEffectMesh.Spike(mesh, head, tail, size * 1.4f, PerkEffectMesh.Tint(tint, alpha * .55f), PerkEffectMesh.Tint(tint, 0f));
            if (index % 3 == 0)
                PerkEffectMesh.Sparkle(mesh, head, size * 2.6f, life * 2f, PerkEffectMesh.Tint(tint, alpha));
            else
                PerkEffectMesh.Glow(mesh, head, size, PerkEffectMesh.Tint(tint, alpha), 12);
        }

        // Each mote swirls sideways as it falls from the wall edge onto the card's orbit.
        private static Vector2 Spiral(Vector2 wall, Vector2 orbit, float angle, float life)
        {
            float ease = life * life * (3f - 2f * life);
            float turn = angle + ease * 1.7f;
            Vector2 radius = Vector2.Lerp(wall, orbit, ease);
            return new Vector2(Mathf.Cos(turn) * radius.x, Mathf.Sin(turn) * radius.y);
        }

        // Direction sets the spin speed and sense; the hue offset keeps a second rainbow ring out of phase.
        private void DrawOrbit(VertexHelper mesh, Rect rect, Vector2 orbit, float direction, int count, float hueOffset)
        {
            float ring = Mathf.SmoothStep(0f, 1f, (progress - .3f) / .6f) * opacity;
            if (ring <= 0f) return;
            PerkEffectMesh.Ring(mesh, Vector2.zero, orbit, .035f,
                rainbow ? PerkEffectMesh.Rainbow(hueOffset + progress * .5f, ring * .4f) : PerkEffectMesh.Tint(PerkEffectMesh.Gold, ring * .32f), 96);
            float spin = (progress * 2.6f + charge * 4f) * direction;
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / count + spin;
                Vector2 point = new Vector2(Mathf.Cos(angle) * orbit.x, Mathf.Sin(angle) * orbit.y);
                float twinkle = .45f + .55f * Mathf.Pow(Mathf.Sin(spin * 3f + i * 1.7f), 2f);
                Color tint = rainbow ? PerkEffectMesh.Rainbow(i / (float)count + hueOffset, 1f, .6f)
                    : i % 4 == 0 ? PerkEffectMesh.Blush : PerkEffectMesh.Cream;
                PerkEffectMesh.Sparkle(mesh, point, rect.height * (.025f + .02f * twinkle) * (1f + charge * .4f), 0f,
                    PerkEffectMesh.Tint(tint, ring * twinkle));
            }
        }

        private void DrawCardShine(VertexHelper mesh, Rect rect, Vector2 frameSize)
        {
            // Two big twinkles sit on opposite corners of the card, the anime cue for a dish that looks delicious.
            float shine = Mathf.SmoothStep(0f, 1f, (progress - .45f) / .55f) * opacity;
            if (shine <= 0f) return;
            float pulse = 1f + Mathf.Sin((progress + charge) * Mathf.PI * 6f) * .12f + charge * .35f;
            PerkEffectMesh.Glow(mesh, Vector2.zero, frameSize.y * .45f, PerkEffectMesh.Tint(PerkEffectMesh.Cream, shine * (.1f + charge * .12f)), 48);
            PerkEffectMesh.Sparkle(mesh, frameSize * new Vector2(.36f, .38f), rect.height * .12f * pulse, 0f,
                PerkEffectMesh.Tint(PerkEffectMesh.Cream, shine));
            PerkEffectMesh.Sparkle(mesh, frameSize * new Vector2(-.34f, -.36f), rect.height * .08f * pulse, .3f,
                rainbow ? PerkEffectMesh.Rainbow(progress + charge, shine * .9f) : PerkEffectMesh.Tint(PerkEffectMesh.Blush, shine * .9f));
        }
    }
}
