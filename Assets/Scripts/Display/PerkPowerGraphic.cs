using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // The food-reaction climax: a white flash, a burst of sunrays, kira stars and pastel light drifting across the wall.
    // The Big tier adds rainbow shockwaves and a rainbow star shower.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PerkPowerGraphic : MaskableGraphic
    {
        private const int Rays = 22;
        private const int Stars = 34;
        private const int Lights = 70;
        private const int ShowerStars = 60;
        private float progress;
        private bool rainbow;

        public void SetRainbow(bool enabled)
        {
            rainbow = enabled;
            SetVerticesDirty();
        }

        public void SetPower(float amount)
        {
            raycastTarget = false;
            progress = Mathf.Clamp01(amount);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (progress <= 0f || progress >= 1f) return;
            Rect rect = rectTransform.rect;
            float reach = rect.size.magnitude * .58f;
            float travel = 1f - Mathf.Pow(1f - progress, 3f);
            float fade = Mathf.Sin(Mathf.PI * Mathf.Min(1f, progress / .12f) * .5f) * Mathf.Pow(1f - progress, 1.5f);
            float flash = Mathf.Exp(-progress * 9f);
            // Overscan the wash because the authored canvas can be narrower than the camera view.
            Color wash = PerkEffectMesh.Tint(PerkEffectMesh.Cream, flash * .7f + fade * .1f);
            Vector2 minimum = rect.min * 1.25f;
            Vector2 maximum = rect.max * 1.25f;
            PerkEffectMesh.Quad(mesh, minimum, new Vector2(maximum.x, minimum.y), maximum,
                new Vector2(minimum.x, maximum.y), wash, wash);
            DrawRays(mesh, rect, reach, travel);
            DrawShockwaves(mesh, rect, reach, fade);
            DrawStars(mesh, rect, reach, travel);
            DrawLights(mesh, rect);
            if (rainbow) DrawStarShower(mesh, rect);
            // A horizontal lens streak and the central flash sell the "shine" of the finished dish.
            PerkEffectMesh.Spike(mesh, Vector2.zero, Vector2.right * rect.width * .65f, rect.height * .08f,
                PerkEffectMesh.Tint(Color.white, flash), PerkEffectMesh.Tint(Color.white, 0f));
            PerkEffectMesh.Spike(mesh, Vector2.zero, Vector2.left * rect.width * .65f, rect.height * .08f,
                PerkEffectMesh.Tint(Color.white, flash), PerkEffectMesh.Tint(Color.white, 0f));
            PerkEffectMesh.Glow(mesh, Vector2.zero, rect.height * (.3f + travel * .7f),
                PerkEffectMesh.Tint(Color.white, Mathf.Exp(-progress * 5f) * .95f), 64);
        }

        private Color Palette(int index, float seed, float alpha)
        {
            if (rainbow) return PerkEffectMesh.Rainbow(seed + progress * .6f, alpha, .6f);
            Color tint = index % 3 == 0 ? PerkEffectMesh.Blush : index % 3 == 1 ? PerkEffectMesh.Gold : PerkEffectMesh.Cream;
            return PerkEffectMesh.Tint(tint, alpha);
        }

        private void DrawShockwaves(VertexHelper mesh, Rect rect, float reach, float fade)
        {
            // Small: a gold front and a lagging blush front. Big: three staggered rainbow fronts.
            int waves = rainbow ? 3 : 2;
            for (int wave = 0; wave < waves; wave++)
            {
                float delay = wave * .08f;
                float age = 1f - Mathf.Pow(1f - Mathf.Clamp01((progress - delay) / (1f - delay)), 3f);
                if (age <= 0f) continue;
                float radius = Mathf.Lerp(rect.height * .05f, reach * (1f - wave * (rainbow ? .1f : .2f)), age);
                Color tint = rainbow ? PerkEffectMesh.Rainbow(wave / 3f + progress * .5f, fade * .55f, .7f)
                    : PerkEffectMesh.Tint(wave == 0 ? PerkEffectMesh.Gold : PerkEffectMesh.Blush, fade * (wave == 0 ? .5f : .35f));
                PerkEffectMesh.Ring(mesh, Vector2.zero, Vector2.one * radius, wave == 0 ? .07f : .05f, tint, 128);
            }
        }

        private void DrawRays(VertexHelper mesh, Rect rect, float reach, float travel)
        {
            float strength = Mathf.Pow(1f - progress, 2f);
            float turn = progress * .35f;
            for (int i = 0; i < Rays; i++)
            {
                float seed = PerkEffectMesh.Hash(i, 5f);
                float angle = turn + (i + seed * .5f) * Mathf.PI * 2f / Rays;
                Vector2 tip = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * reach * (1.2f + travel * 1.3f) * (.7f + seed * .3f);
                Color tint = rainbow ? PerkEffectMesh.Rainbow(i / (float)Rays, strength * .5f, .6f)
                    : PerkEffectMesh.Tint(i % 2 == 0 ? PerkEffectMesh.Cream : PerkEffectMesh.Gold, strength * .45f);
                PerkEffectMesh.Spike(mesh, Vector2.zero, tip, rect.height * (.1f + seed * .14f) * strength,
                    tint, PerkEffectMesh.Tint(tint, 0f));
            }
        }

        private void DrawStars(VertexHelper mesh, Rect rect, float reach, float travel)
        {
            int stars = rainbow ? Stars * 3 / 2 : Stars;
            for (int i = 0; i < stars; i++)
            {
                float seed = PerkEffectMesh.Hash(i, 6f);
                float angle = PerkEffectMesh.Hash(i, 7f) * Mathf.PI * 2f;
                Vector2 point = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * .55f) * reach * travel * (.25f + seed * .75f);
                point += Vector2.up * progress * rect.height * .08f;
                float twinkle = .55f + .45f * Mathf.Pow(Mathf.Sin(progress * 12f + i * 1.3f), 2f);
                float size = rect.height * (.04f + seed * .06f) * twinkle * Mathf.Min(1f, progress * 10f);
                PerkEffectMesh.Sparkle(mesh, point, size, seed * .5f, Palette(i, seed, (1f - progress) * twinkle));
            }
        }

        private void DrawLights(VertexHelper mesh, Rect rect)
        {
            // Pastel light bubbles hang in the air and rise slowly after the shock front has passed.
            float appear = Mathf.SmoothStep(0f, 1f, progress / .2f);
            for (int i = 0; i < Lights; i++)
            {
                float seed = PerkEffectMesh.Hash(i, 8f);
                float x = (PerkEffectMesh.Hash(i, 9f) - .5f) * rect.width * 1.15f;
                float y = (PerkEffectMesh.Hash(i, 10f) - .5f) * rect.height;
                Vector2 point = new Vector2(x, y + progress * rect.height * (.08f + seed * .22f));
                float twinkle = .45f + .55f * Mathf.Pow(Mathf.Sin(progress * 9f + i), 2f);
                Color tint = Palette(i, seed, appear * (1f - progress) * twinkle);
                if (i % 4 == 0)
                    PerkEffectMesh.Sparkle(mesh, point, rect.height * (.02f + seed * .025f), 0f, tint);
                else
                    PerkEffectMesh.Glow(mesh, point, rect.height * (.006f + seed * .012f), PerkEffectMesh.Tint(tint, tint.a * .85f), 12);
            }
        }

        // A shower of rainbow kira stars streams diagonally across the wall, each with a soft glowing tail.
        private void DrawStarShower(VertexHelper mesh, Rect rect)
        {
            float appear = Mathf.SmoothStep(0f, 1f, (progress - .06f) / .2f) * (1f - Mathf.SmoothStep(0f, 1f, (progress - .65f) / .35f));
            if (appear <= 0f) return;
            Vector2 fall = new Vector2(-.45f, -1f).normalized;
            for (int i = 0; i < ShowerStars; i++)
            {
                float seed = PerkEffectMesh.Hash(i, 11f);
                float delay = PerkEffectMesh.Hash(i, 12f) * .45f;
                float life = Mathf.Clamp01((progress - delay) / (.35f + seed * .2f));
                if (life <= 0f || life >= 1f) continue;
                // Stars enter above the wall's upper edge and travel across its full height.
                Vector2 start = new Vector2((PerkEffectMesh.Hash(i, 13f) - .3f) * rect.width * 1.3f, rect.height * (.55f + seed * .2f));
                Vector2 head = start + fall * rect.height * 1.6f * life;
                Vector2 tail = head - fall * rect.height * (.12f + seed * .18f);
                float twinkle = .6f + .4f * Mathf.Pow(Mathf.Sin(life * 18f + i), 2f);
                float alpha = appear * Mathf.Sin(Mathf.PI * life) * twinkle;
                Color tint = PerkEffectMesh.Rainbow(seed * 4f + progress * .5f, alpha, .6f);
                PerkEffectMesh.Spike(mesh, head, tail, rect.height * (.01f + seed * .012f),
                    PerkEffectMesh.Tint(tint, alpha * .6f), PerkEffectMesh.Tint(tint, 0f));
                PerkEffectMesh.Sparkle(mesh, head, rect.height * (.025f + seed * .03f) * twinkle, 0f, tint);
            }
        }
    }
}
