using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // A one-shot celebration on the floor tile where a perk was bought: a flash, two expanding rings,
    // short rays and kira stars that burst outward and twinkle out. Gold for Small perks, rainbow for Big ones.
    // It animates itself on unscaled time and removes its temporary object when finished.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PerkFloorPurchaseBurst : MaskableGraphic
    {
        private const int Rays = 14;
        private const int Stars = 28;
        private const int Glints = 22;
        private float duration = 1.4f;
        private float age;
        private bool rainbow;
        private float tileRadius = 100f;
        private GameObject owner;
        private bool finished;

        // The tile radius sets the burst scale; the owner object is destroyed when the burst ends.
        public void Play(float radius, bool rainbowTier, GameObject root)
        {
            raycastTarget = false;
            tileRadius = Mathf.Max(1f, radius);
            rainbow = rainbowTier;
            duration = rainbowTier ? 1.8f : 1.4f;
            owner = root;
            age = 0f;
            SetVerticesDirty();
        }

        private void Update()
        {
            age += Time.unscaledDeltaTime;
            if (age >= duration)
            {
                Finish();
                return;
            }
            SetVerticesDirty();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            // A shop that closes mid-burst must not leave the burst waiting for the next shop.
            if (Application.isPlaying && age > 0f) Finish();
        }

        private void Finish()
        {
            if (finished) return;
            finished = true;
            GameObject target = owner != null ? owner : gameObject;
            owner = null;
            if (Application.isPlaying) Destroy(target);
        }

        private Color Tint(int index, float seed, float alpha)
        {
            if (rainbow) return PerkEffectMesh.Rainbow(seed + age * .4f, alpha, .6f);
            Color tint = index % 3 == 0 ? PerkEffectMesh.Blush : index % 3 == 1 ? PerkEffectMesh.Gold : PerkEffectMesh.Cream;
            return PerkEffectMesh.Tint(tint, alpha);
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            float progress = Mathf.Clamp01(age / duration);
            if (progress >= 1f) return;
            float r = tileRadius;
            // A quick flash where the player stands.
            float flash = Mathf.Exp(-progress * 10f) * Mathf.Min(1f, age / .03f);
            PerkEffectMesh.Glow(mesh, Vector2.zero, r * (.9f + progress * .8f), PerkEffectMesh.Tint(PerkEffectMesh.Cream, flash * .8f), 48);
            PerkEffectMesh.Glow(mesh, Vector2.zero, r * .45f, PerkEffectMesh.Tint(Color.white, flash), 32);
            DrawRings(mesh, progress, r);
            DrawRays(mesh, progress, r);
            DrawStars(mesh, progress, r);
            DrawGlints(mesh, progress, r);
        }

        private void DrawRings(VertexHelper mesh, float progress, float r)
        {
            for (int ring = 0; ring < 2; ring++)
            {
                float local = Mathf.Clamp01((progress - ring * .08f) / .6f);
                if (local <= 0f || local >= 1f) continue;
                float spread = 1f - Mathf.Pow(1f - local, 3f);
                float alpha = Mathf.Sin(Mathf.PI * Mathf.Min(1f, local / .12f) * .5f) * (1f - local);
                Color tint = rainbow ? PerkEffectMesh.Rainbow(ring * .33f + local * .5f, alpha * .75f, .65f)
                    : PerkEffectMesh.Tint(ring == 0 ? PerkEffectMesh.Gold : PerkEffectMesh.Blush, alpha * (ring == 0 ? .75f : .5f));
                PerkEffectMesh.Ring(mesh, Vector2.zero, Vector2.one * r * (.6f + spread * (1.9f - ring * .35f)), ring == 0 ? .08f : .06f, tint, 72);
            }
        }

        private void DrawRays(VertexHelper mesh, float progress, float r)
        {
            float strength = Mathf.Pow(1f - Mathf.Clamp01(progress / .45f), 2f);
            if (strength <= 0f) return;
            float travel = 1f - Mathf.Pow(1f - Mathf.Clamp01(progress / .45f), 3f);
            for (int i = 0; i < Rays; i++)
            {
                float seed = PerkEffectMesh.Hash(i, 71f);
                float angle = (i + seed * .5f) * Mathf.PI * 2f / Rays;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 tip = direction * r * (.8f + travel * (1.1f + seed * .6f));
                Color tint = rainbow ? PerkEffectMesh.Rainbow(i / (float)Rays, strength * .6f, .6f)
                    : PerkEffectMesh.Tint(i % 2 == 0 ? PerkEffectMesh.Cream : PerkEffectMesh.Gold, strength * .55f);
                PerkEffectMesh.Spike(mesh, direction * r * .25f, tip, r * (.14f + seed * .1f) * strength, tint, PerkEffectMesh.Tint(tint, 0f));
            }
        }

        // Stars burst outward, slow down, drift a little and twinkle away.
        private void DrawStars(VertexHelper mesh, float progress, float r)
        {
            int count = rainbow ? Stars * 3 / 2 : Stars;
            for (int i = 0; i < count; i++)
            {
                float seed = PerkEffectMesh.Hash(i, 72f);
                float delay = PerkEffectMesh.Hash(i, 73f) * .1f;
                float life = Mathf.Clamp01((progress - delay) / (.75f + seed * .2f));
                if (life <= 0f || life >= 1f) continue;
                float angle = PerkEffectMesh.Hash(i, 74f) * Mathf.PI * 2f;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float flight = 1f - Mathf.Pow(1f - life, 3f);
                Vector2 point = direction * r * (.3f + flight * (1.3f + seed * 1.1f)) +
                    new Vector2(Mathf.Sin(life * 6f + i), 0f) * r * .06f * life;
                float twinkle = .55f + .45f * Mathf.Pow(Mathf.Sin(age * 14f + i * 1.7f), 2f);
                float alpha = Mathf.Min(1f, life * 8f) * Mathf.Pow(1f - life, 1.2f) * twinkle;
                float size = r * (.1f + seed * .12f) * (1.2f - life * .5f);
                Color tint = Tint(i, seed, alpha);
                if (life < .4f)
                    PerkEffectMesh.Spike(mesh, point, point - direction * r * .3f * (1f - life / .4f), size * .3f,
                        PerkEffectMesh.Tint(tint, alpha * .5f), PerkEffectMesh.Tint(tint, 0f));
                PerkEffectMesh.Sparkle(mesh, point, size, life * 2f + seed, tint);
            }
        }

        // Small pastel glints hang around the tile after the burst.
        private void DrawGlints(VertexHelper mesh, float progress, float r)
        {
            float appear = Mathf.SmoothStep(0f, 1f, (progress - .15f) / .2f);
            if (appear <= 0f) return;
            for (int i = 0; i < Glints; i++)
            {
                float seed = PerkEffectMesh.Hash(i, 75f);
                float angle = PerkEffectMesh.Hash(i, 76f) * Mathf.PI * 2f;
                float distance = r * (.4f + PerkEffectMesh.Hash(i, 77f) * 1.6f);
                Vector2 point = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance + Vector2.up * progress * r * .25f;
                float twinkle = .4f + .6f * Mathf.Pow(Mathf.Sin(age * 9f + i), 2f);
                Color tint = Tint(i, seed, appear * (1f - progress) * twinkle);
                PerkEffectMesh.Glow(mesh, point, r * (.03f + seed * .04f), tint, 12);
            }
        }
    }
}
