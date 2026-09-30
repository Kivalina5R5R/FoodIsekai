using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // Canvas particles stay around the authored frame without covering the card text.
    public sealed class PerkCardSparkles : MaskableGraphic
    {
        [SerializeField] private Color starColor = new Color(1f, .85f, .42f, .9f);
        private float age;
        private float burstAge = 2f;
        private bool emitting;
        private bool rainbow;

        // The shop supplies the tier; changing offers also resets the previous tier's finish.
        public void SetTier(bool bigPerk)
        {
            rainbow = bigPerk;
            SetVerticesDirty();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            age = 0f;
            burstAge = 2f;
            emitting = true;
        }

        public void Burst() => burstAge = 0f;
        public void StopEmitting() => emitting = false;

        private void Update()
        {
            age += Time.unscaledDeltaTime;
            burstAge += Time.unscaledDeltaTime;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect frame = rectTransform.rect;
            // The rect is authored at the Small frame's aspect; the taller Big frame keeps the same center.
            if (rainbow) frame = new Rect(frame.x, frame.center.y - frame.height * PerkFrameContour.BigHeightScale * .5f,
                frame.width, frame.height * PerkFrameContour.BigHeightScale);
            if (emitting)
            {
                DrawSheen(mesh, frame);
                for (int i = 0; i < 8; i++)
                {
                    float phase = transform.parent != null ? transform.parent.GetSiblingIndex() * .85f : 0f;
                    float pulse = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(age * 1.4f + i * 2.4f + phase)), 3f);
                    Vector2 point = OnFrame(frame, Mathf.Repeat(i * .618034f, 1f));
                    point += (point - frame.center).normalized * 6f;
                    Star(mesh, point, 4f + pulse * 7f, pulse * Mathf.Clamp01(age / .5f), age * .15f);
                }
            }
            if (burstAge >= .65f) return;
            float t = burstAge / .65f;
            for (int i = 0; i < 20; i++)
            {
                Vector2 point = OnFrame(frame, i / 20f);
                Vector2 direction = (point - frame.center).normalized;
                point += direction * (8f + 24f * t);
                Star(mesh, point, (1f - t) * 12f, 1f - t, i + t);
            }
        }

        private void DrawSheen(VertexHelper mesh, Rect frame)
        {
            float offset = transform.parent != null ? transform.parent.GetSiblingIndex() * .11f : 0f;
            float phase = Mathf.Repeat(age / 5.8f + offset, 1f);
            // Ease the sweep in and out beyond the silhouette, where the loop reset is invisible.
            float travel = .5f - .5f * Mathf.Cos(phase * Mathf.PI);
            float sweep = Mathf.Lerp(-.24f, 1.24f, travel);
            DrawSurfaceSheen(mesh, frame, sweep);
            const int segments = 128;
            for (int i = 0; i < segments; i++)
            {
                Vector2 a = OnFrame(frame, i / (float)segments);
                Vector2 b = OnFrame(frame, (i + 1) / (float)segments);
                Vector2 insideA = a + (frame.center - a).normalized * 2.5f;
                Vector2 insideB = b + (frame.center - b).normalized * 2.5f;
                float lightA = SheenOpacity(frame, a, sweep);
                float lightB = SheenOpacity(frame, b, sweep);
                if (lightA + lightB < .01f) continue;
                int start = mesh.currentVertCount;
                Color tintA = SheenColor(frame, a, sweep, .84f);
                Color tintB = SheenColor(frame, b, sweep, .84f);
                mesh.AddVert(a, tintA, Vector2.zero);
                mesh.AddVert(b, tintB, Vector2.zero);
                mesh.AddVert(insideB, SheenColor(frame, insideB, sweep, .4f), Vector2.zero);
                mesh.AddVert(insideA, SheenColor(frame, insideA, sweep, .4f), Vector2.zero);
                mesh.AddTriangle(start, start + 1, start + 2);
                mesh.AddTriangle(start, start + 2, start + 3);
            }
        }

        // A translucent foil band sweeps over the card face, clipped to its actual silhouette.
        private void DrawSurfaceSheen(VertexHelper mesh, Rect frame, float sweep)
        {
            const int segments = 128;
            // Finer spacing avoids visible color steps as the narrow band crosses the mesh.
            const int rings = 32;
            for (int i = 0; i < segments; i++)
            {
                Vector2 edgeA = OnFrame(frame, i / (float)segments);
                Vector2 edgeB = OnFrame(frame, (i + 1) / (float)segments);
                for (int ring = 0; ring < rings; ring++)
                {
                    float inner = ring / (float)rings;
                    float outer = (ring + 1) / (float)rings;
                    Vector2 a = Vector2.Lerp(frame.center, edgeA, inner);
                    Vector2 b = Vector2.Lerp(frame.center, edgeA, outer);
                    Vector2 c = Vector2.Lerp(frame.center, edgeB, outer);
                    Vector2 d = Vector2.Lerp(frame.center, edgeB, inner);
                    Color ca = SheenColor(frame, a, sweep, .3f);
                    Color cb = SheenColor(frame, b, sweep, .3f);
                    Color cc = SheenColor(frame, c, sweep, .3f);
                    Color cd = SheenColor(frame, d, sweep, .3f);
                    if (ca.a + cb.a + cc.a + cd.a < .01f) continue;
                    int start = mesh.currentVertCount;
                    mesh.AddVert(a, ca, Vector2.zero);
                    mesh.AddVert(b, cb, Vector2.zero);
                    mesh.AddVert(c, cc, Vector2.zero);
                    mesh.AddVert(d, cd, Vector2.zero);
                    mesh.AddTriangle(start, start + 1, start + 2);
                    mesh.AddTriangle(start, start + 2, start + 3);
                }
            }
        }

        private Color SheenColor(Rect frame, Vector2 point, float sweep, float strength)
        {
            float diagonal = ((point.x - frame.xMin) / frame.width +
                (point.y - frame.yMin) / frame.height) * .5f;
            float offset = diagonal - sweep;
            Color tint;
            if (rainbow)
            {
                // Keep the rainbow coating stable while its reflection moves across it.
                float hue = Mathf.Repeat(diagonal * 2.4f, 1f);
                tint = Color.HSVToRGB(hue, .65f, 1f);
                tint = Color.Lerp(tint, Color.white, Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(offset) / .055f), 2f) * .55f);
            }
            else
            {
                // Broad pearl-silver blending avoids a dark stripe or a hard white center.
                float distance = offset / .062f;
                float core = Mathf.Exp(-.5f * distance * distance);
                tint = Color.Lerp(new Color(.67f, .72f, .79f, 1f), new Color(.98f, .99f, 1f, 1f), core);
            }
            float opacity = SheenOpacity(frame, point, sweep);
            if (!rainbow)
            {
                // Zero slope and curvature at the edge fade the silver into the card surface.
                float fade = Mathf.Clamp01(1f - Mathf.Abs(offset) / .1f);
                opacity = fade * fade * fade * (fade * (6f * fade - 15f) + 10f);
            }
            tint.a = opacity * strength;
            return tint;
        }

        private static float SheenOpacity(Rect frame, Vector2 point, float sweep)
        {
            float diagonal = ((point.x - frame.xMin) / frame.width +
                (point.y - frame.yMin) / frame.height) * .5f;
            float band = Mathf.Clamp01(1f - Mathf.Abs(diagonal - sweep) / .1f);
            return band * band * (3f - 2f * band);
        }

        private Vector2 OnFrame(Rect frame, float progress)
        {
            return PerkFrameContour.Sample(frame, progress, rainbow);
        }

        private void Star(VertexHelper mesh, Vector2 center, float radius, float alpha, float rotation)
        {
            if (alpha <= .01f) return;
            Color tint = starColor;
            tint.a *= alpha;
            int start = mesh.currentVertCount;
            mesh.AddVert(center, tint, Vector2.zero);
            for (int i = 0; i < 8; i++)
            {
                float angle = rotation + i * Mathf.PI / 4f;
                float size = radius * (i % 2 == 0 ? 1f : .25f);
                mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * size, tint, Vector2.zero);
            }
            for (int i = 0; i < 8; i++) mesh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % 8);
        }
    }
}
