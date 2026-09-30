using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // Two substantial energy masses converge from the upper left and lower right.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PerkGatherGraphic : MaskableGraphic
    {
        private float progress;
        private float charge;
        private float opacity = 1f;

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
            float appear = Mathf.SmoothStep(0f, 1f, progress * 8f) * opacity;
            float streamAlpha = appear * (1f - Mathf.SmoothStep(0f, 1f, (progress - .88f) / .12f));
            for (int side = -1; side <= 1; side += 2)
                DrawEnergyLance(mesh, rect, side, streamAlpha);
            DrawOrbits(mesh, rect);
            float buildup = Mathf.SmoothStep(0f, 1f, progress);
            float breath = 1f + Mathf.Sin(charge * Mathf.PI * 2f) * .045f;
            float coreRadius = rect.height * Mathf.Lerp(.015f, .18f, buildup) * breath;
            Glow(mesh, Vector2.zero, coreRadius * 2.4f, new Color(1f, .6f, .12f, buildup * .22f * opacity));
            Glow(mesh, Vector2.zero, coreRadius * 1.3f, new Color(1f, .78f, .27f, buildup * .65f * opacity));
            Glow(mesh, Vector2.zero, coreRadius * .6f, new Color(1f, .97f, .78f, buildup * .96f * opacity));
        }

        private void DrawEnergyLance(VertexHelper mesh, Rect rect, int side, float alpha)
        {
            float head = Mathf.Lerp(.22f, 1f, Mathf.SmoothStep(0f, 1f, progress));
            float tail = Mathf.Max(0f, head - .3f);
            for (int segment = 0; segment < 96; segment++)
            {
                float u = segment / 96f;
                float v = (segment + 1f) / 96f;
                Vector2 a = Path(rect, side, Mathf.Lerp(tail, head, u));
                Vector2 b = Path(rect, side, Mathf.Lerp(tail, head, v));
                Vector2 tangent = (b - a).normalized;
                Vector2 normal = new Vector2(-tangent.y, tangent.x);
                // Both ends close to a true point; the broad body trails behind the leading tip.
                float widthA = Mathf.Pow(Mathf.Sin(u * Mathf.PI), .9f) * (1f - u * .45f) * rect.height * .065f;
                float widthB = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(v * Mathf.PI)), .9f) * (1f - v * .45f) * rect.height * .065f;
                Color center = new Color(1f, .69f, .1f, alpha * .32f);
                Color clear = new Color(1f, .5f, .06f, 0f);
                Strip(mesh, a - normal * widthA * 1.5f, b - normal * widthB * 1.5f, a, b, clear, center);
                Strip(mesh, a, b, a + normal * widthA * 1.5f, b + normal * widthB * 1.5f, center, clear);
                for (int strand = 0; strand < 5; strand++)
                {
                    float offset = (strand - 2f) / 2f;
                    float waveA = Mathf.Sin(u * 18f - progress * 16f + strand * 1.7f) * .09f;
                    float waveB = Mathf.Sin(v * 18f - progress * 16f + strand * 1.7f) * .09f;
                    Vector2 from = a + normal * widthA * (offset + waveA);
                    Vector2 to = b + normal * widthB * (offset + waveB);
                    float taperA = Mathf.Sin(u * Mathf.PI);
                    float taperB = Mathf.Max(0f, Mathf.Sin(v * Mathf.PI));
                    float thickness = strand == 2 ? 1.6f : .75f;
                    Color gold = new Color(1f, .82f + strand * .012f, .25f + strand * .05f, alpha * .8f);
                    Strip(mesh, from - normal * thickness * taperA, to - normal * thickness * taperB,
                        from + normal * thickness * taperA, to + normal * thickness * taperB, gold, gold);
                }
            }
        }

        private static Vector2 Path(Rect rect, int side, float amount)
        {
            float angle = 2.35f + (side > 0 ? Mathf.PI : 0f);
            Vector2 entry = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * rect.height * .48f;
            if (amount < .4f)
            {
                float t = amount / .4f;
                Vector2 start = new Vector2(side * rect.width * .62f, -side * rect.height * .85f);
                Vector2 control = new Vector2(side * rect.width * .2f, -side * rect.height * .65f);
                return start * (1f - t) * (1f - t) + control * 2f * t * (1f - t) + entry * t * t;
            }
            float orbit = (amount - .4f) / .6f;
            angle -= orbit * Mathf.PI * 1.5f;
            float radius = rect.height * .48f * (1f - Mathf.SmoothStep(0f, 1f, (orbit - .72f) / .28f));
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        private void DrawOrbits(VertexHelper mesh, Rect rect)
        {
            float strength = Mathf.SmoothStep(0f, 1f, (progress - .3f) / .45f) * opacity;
            for (int ring = 0; ring < 3; ring++)
            {
                float radius = rect.height * (.39f + ring * .045f);
                float turn = progress * 4f + charge * 1.5f + ring;
                for (int i = 0; i < 128; i++)
                {
                    float angle = i * Mathf.PI * 2f / 128f + turn;
                    float next = (i + 1) * Mathf.PI * 2f / 128f + turn;
                    Vector2 a = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    Vector2 b = new Vector2(Mathf.Cos(next), Mathf.Sin(next)) * radius;
                    float bright = .18f + Mathf.Pow((Mathf.Sin(angle - turn) + 1f) * .5f, 5f) * .75f;
                    Color gold = new Color(1f, .84f, .49f, strength * bright);
                    Color clear = new Color(gold.r, gold.g, gold.b, 0f);
                    Strip(mesh, a * .988f, b * .988f, a, b, clear, gold);
                    Strip(mesh, a, b, a * 1.012f, b * 1.012f, gold, clear);
                    if (i % 16 == 0) Glow(mesh, a, 2.2f, new Color(1f, .95f, .75f, strength * .8f));
                }
            }
        }
        private static void Strip(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color inner, Color outer)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(a, inner, Vector2.zero);
            mesh.AddVert(b, inner, Vector2.zero);
            mesh.AddVert(d, outer, Vector2.zero);
            mesh.AddVert(c, outer, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }
        private static void Glow(VertexHelper mesh, Vector2 center, float radius, Color tint)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(center, tint, Vector2.zero);
            tint.a = 0f;
            for (int i = 0; i < 64; i++)
            {
                float angle = i * Mathf.PI * 2f / 64f;
                mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, tint, Vector2.zero);
            }
            for (int i = 0; i < 64; i++) mesh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % 64);
        }
    }
}
