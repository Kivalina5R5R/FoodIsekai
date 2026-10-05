using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // Links the floor to the wall's perk purchase without an explosion of its own.
    // While the card gathers and charges, motes of light lift off the floor and glide on curved comet trails
    // into the wall edge under the card. When the card releases, a soft arc wave rolls out from under the card
    // across the whole floor. The Big tier uses rainbow light.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PerkFloorBlastGraphic : MaskableGraphic
    {
        private const int Motes = 54;
        private const int TailSegments = 10;
        private const int WakeSpecks = 46;
        private static readonly float[] SeamRows = { 0f, .35f, 1f };
        private static readonly float[] SeamRowAlpha = { 1f, .4f, 0f };
        private static readonly float[] GlowRings = { .3f, .6f, 1f };
        private static readonly float[] GlowRingAlpha = { .6f, .2f, 0f };
        private const int ArcSegments = 72;
        private const int CrestGlints = 16;
        private float flow;
        private float release;
        private bool rainbow;
        private Vector2 wallSide = Vector2.up;
        private readonly Vector2[] tailPoints = new Vector2[TailSegments + 1];

        public void SetRainbow(bool enabled)
        {
            rainbow = enabled;
            SetVerticesDirty();
        }

        // The side of the floor image that touches the wall; motes flow into it and the wave rolls out of it.
        public void SetWallSide(Vector2 side)
        {
            wallSide = side.sqrMagnitude > .0001f ? side.normalized : Vector2.up;
            SetVerticesDirty();
        }

        // Flow runs from 0 to 1 across the whole gather and charge, so every mote arrives before the release.
        // Release runs from 0 to 1 after the card bursts and drives the wave.
        public void SetFlow(float flowAmount, float releaseAmount)
        {
            raycastTarget = false;
            flow = Mathf.Clamp01(flowAmount);
            release = Mathf.Clamp01(releaseAmount);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (release >= 1f || (flow <= 0f && release <= 0f)) return;
            Rect rect = rectTransform.rect;
            Vector2 inward = -wallSide;
            Vector2 across = new Vector2(-wallSide.y, wallSide.x);
            float depth = Mathf.Abs(wallSide.x) * rect.width + Mathf.Abs(wallSide.y) * rect.height;
            float span = Mathf.Abs(across.x) * rect.width + Mathf.Abs(across.y) * rect.height;
            Vector2 origin = rect.center + wallSide * depth * .5f;
            float unit = Mathf.Min(rect.width, rect.height);
            if (release <= 0f)
            {
                DrawInlet(mesh, origin, inward, across, span, depth, unit, Mathf.SmoothStep(0f, 1f, flow * 1.4f));
                int motes = rainbow ? Motes * 3 / 2 : Motes;
                for (int i = 0; i < motes; i++) DrawMote(mesh, origin, inward, across, depth, span, unit, i);
                return;
            }
            DrawInlet(mesh, origin, inward, across, span, depth, unit, Mathf.Pow(1f - Mathf.Clamp01(release / .35f), 2f));
            DrawWave(mesh, origin, inward, across, depth, span, unit);
        }

        private Color MoteTint(int index, float seed)
        {
            if (rainbow) return PerkEffectMesh.Rainbow(seed + flow * .3f, 1f, .6f);
            return index % 5 == 0 ? PerkEffectMesh.Blush : index % 3 == 0 ? PerkEffectMesh.Cream : PerkEffectMesh.Gold;
        }

        // The point under the card where the motes disappear into the wall: a soft pool of light along the seam.
        private void DrawInlet(VertexHelper mesh, Vector2 origin, Vector2 inward, Vector2 across,
            float span, float depth, float unit, float strength)
        {
            if (strength <= 0f) return;
            Color seam = rainbow ? PerkEffectMesh.Rainbow(flow * .5f + release, strength * .3f, .45f)
                : PerkEffectMesh.Tint(PerkEffectMesh.Gold, strength * .3f);
            DrawSeamBand(mesh, origin, inward, across, span * .34f, depth * .1f, seam);
            DrawSoftGlow(mesh, origin, across, inward, new Vector2(1.5f, 1f) * unit * (.16f + .18f * strength),
                PerkEffectMesh.Tint(PerkEffectMesh.Cream, strength * .45f));
            DrawSoftGlow(mesh, origin, across, inward, new Vector2(1.3f, 1f) * unit * .07f,
                PerkEffectMesh.Tint(Color.white, strength * .6f));
        }

        // A band along the wall seam whose brightness falls off smoothly toward both ends and into the floor,
        // so it never shows a hard corner or a straight cut.
        private static void DrawSeamBand(VertexHelper mesh, Vector2 origin, Vector2 inward, Vector2 across,
            float halfLength, float thickness, Color tint)
        {
            const int columns = 40;
            float[] rows = SeamRows;
            float[] rowAlpha = SeamRowAlpha;
            int start = mesh.currentVertCount;
            for (int column = 0; column <= columns; column++)
            {
                float x = (float)column / columns * 2f - 1f;
                float falloff = Mathf.Exp(-x * x * 4.5f);
                for (int row = 0; row < rows.Length; row++)
                    mesh.AddVert(origin + across * x * halfLength + inward * thickness * rows[row] * (.4f + .6f * falloff),
                        PerkEffectMesh.Tint(tint, tint.a * falloff * rowAlpha[row]), Vector2.zero);
            }
            for (int column = 0; column < columns; column++)
            {
                for (int row = 0; row < rows.Length - 1; row++)
                {
                    int a = start + column * rows.Length + row;
                    int b = a + rows.Length;
                    mesh.AddTriangle(a, b, b + 1);
                    mesh.AddTriangle(a, b + 1, a + 1);
                }
            }
        }

        // An elliptical glow with a smooth, curved falloff instead of the linear cone of a plain disc.
        private static void DrawSoftGlow(VertexHelper mesh, Vector2 center, Vector2 across, Vector2 inward,
            Vector2 radius, Color tint)
        {
            if (tint.a <= 0f) return;
            const int segments = 40;
            float[] rings = GlowRings;
            float[] ringAlpha = GlowRingAlpha;
            int start = mesh.currentVertCount;
            mesh.AddVert(center, tint, Vector2.zero);
            for (int ring = 0; ring < rings.Length; ring++)
            {
                for (int i = 0; i < segments; i++)
                {
                    float angle = i * Mathf.PI * 2f / segments;
                    Vector2 offset = across * Mathf.Cos(angle) * radius.x + inward * Mathf.Sin(angle) * radius.y;
                    mesh.AddVert(center + offset * rings[ring], PerkEffectMesh.Tint(tint, tint.a * ringAlpha[ring]), Vector2.zero);
                }
            }
            for (int i = 0; i < segments; i++)
                mesh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % segments);
            for (int ring = 0; ring < rings.Length - 1; ring++)
            {
                for (int i = 0; i < segments; i++)
                {
                    int a = start + 1 + ring * segments + i;
                    int b = start + 1 + ring * segments + (i + 1) % segments;
                    mesh.AddTriangle(a, b, b + segments);
                    mesh.AddTriangle(a, b + segments, a + segments);
                }
            }
        }

        // Each mote pops out of the floor with a small ripple, then glides on a bowed path into the inlet,
        // speeding up as it is drawn in, with a comet tail that follows the curve.
        private void DrawMote(VertexHelper mesh, Vector2 origin, Vector2 inward, Vector2 across,
            float depth, float span, float unit, int index)
        {
            float seed = PerkEffectMesh.Hash(index, 41f);
            float launch = PerkEffectMesh.Hash(index, 42f) * .68f;
            float travel = .2f + seed * .12f;
            float life = (flow - launch) / travel;
            if (life <= -.15f || life >= 1f) return;
            // Golden-ratio spacing spreads the start points evenly across the floor instead of in clumps.
            float lane = Mathf.Repeat(index * .618034f + .1f, 1f) - .5f;
            Vector2 start = origin + across * lane * span * .92f +
                inward * depth * (.35f + Mathf.Sqrt(PerkEffectMesh.Hash(index, 43f)) * .58f);
            Vector2 end = origin + across * (seed - .5f) * span * .05f;
            float side = lane >= 0f ? 1f : -1f;
            Vector2 bend = (start + end) * .5f + across * side * span * (.08f + seed * .1f) + inward * depth * .12f;
            Color tint = MoteTint(index, seed);
            float size = unit * (.014f + seed * .014f);
            if (life < 0f)
            {
                // A short twinkle marks where the mote is about to lift off.
                float rise = 1f + life / .15f;
                PerkEffectMesh.Glow(mesh, start, size * 1.6f * rise, PerkEffectMesh.Tint(tint, rise * .6f), 12);
                return;
            }
            float ripple = Mathf.Clamp01(life / .35f);
            if (ripple < 1f)
                PerkEffectMesh.Ring(mesh, start, new Vector2(1.4f, 1f) * unit * (.02f + ripple * .07f), .25f,
                    PerkEffectMesh.Tint(tint, (1f - ripple) * .5f), 28);
            float alpha = Mathf.SmoothStep(0f, 1f, life / .12f) * (1f - Mathf.SmoothStep(0f, 1f, (life - .82f) / .18f));
            // Each mote drifts like a firefly: a gentle sway that settles as it is drawn into the inlet.
            float sway = span * (.012f + seed * .02f);
            float swayRate = 2.5f + seed * 2.5f;
            float swayPhase = PerkEffectMesh.Hash(index, 44f) * Mathf.PI * 2f;
            DrawDust(mesh, start, bend, end, across, sway, swayRate, swayPhase, life, size, tint, index);
            Vector2 head = Path(start, bend, end, across, sway, swayRate, swayPhase, life);

            for (int segment = 0; segment <= TailSegments; segment++)
                tailPoints[segment] = Path(start, bend, end, across, sway, swayRate, swayPhase, Mathf.Max(0f, life - segment * .035f));
            DrawRibbon(mesh, tailPoints, size * 1.3f, tint, alpha * .55f);
            float breath = 1f + .22f * Mathf.Sin(flow * 40f + swayPhase);
            PerkEffectMesh.Glow(mesh, head, size * 2.4f * breath, PerkEffectMesh.Tint(tint, alpha * .4f), 16);
            if (index % 3 == 0)
                PerkEffectMesh.Sparkle(mesh, head, size * 2.8f * breath, life * 1.5f + seed, PerkEffectMesh.Tint(tint, alpha));
            else
                PerkEffectMesh.Glow(mesh, head, size * .8f * breath, PerkEffectMesh.Tint(Color.white, alpha), 12);
        }

        // The travel eases out of the floor and then accelerates into the inlet, with the sway fading on arrival.
        private static Vector2 Path(Vector2 start, Vector2 bend, Vector2 end, Vector2 across,
            float sway, float swayRate, float swayPhase, float life)
        {
            float t = life * life * (2f - life);
            t = Mathf.Lerp(t, t * t, .45f);
            float u = 1f - t;
            Vector2 point = u * u * start + 2f * u * t * bend + t * t * end;
            return point + across * Mathf.Sin(life * swayRate * Mathf.PI + swayPhase) * sway * (1f - life);
        }

        // A few glitter specks drop off the trail and twinkle out where they fell.
        private void DrawDust(VertexHelper mesh, Vector2 start, Vector2 bend, Vector2 end, Vector2 across,
            float sway, float swayRate, float swayPhase, float life, float size, Color tint, int index)
        {
            for (int speck = 0; speck < 3; speck++)
            {
                float dropped = .15f + speck * .22f + PerkEffectMesh.Hash(index * 3 + speck, 45f) * .08f;
                float age = (life - dropped) / .45f;
                if (age <= 0f || age >= 1f) continue;
                Vector2 point = Path(start, bend, end, across, sway, swayRate, swayPhase, dropped) +
                    across * (PerkEffectMesh.Hash(index * 3 + speck, 46f) - .5f) * size * 6f * age;
                float twinkle = .5f + .5f * Mathf.Sin(age * 18f + speck);
                PerkEffectMesh.Glow(mesh, point, size * .55f, PerkEffectMesh.Tint(tint, (1f - age) * .8f * twinkle), 8);
            }
        }

        // One continuous, tapering strip, so the comet tail bends smoothly instead of breaking into pieces.
        private static void DrawRibbon(VertexHelper mesh, Vector2[] points, float width, Color tint, float alpha)
        {
            if (alpha <= 0f) return;
            int start = mesh.currentVertCount;
            int last = points.Length - 1;
            for (int i = 0; i <= last; i++)
            {
                Vector2 along = points[Mathf.Max(0, i - 1)] - points[Mathf.Min(last, i + 1)];
                Vector2 normal = along.sqrMagnitude > .0001f ? new Vector2(-along.y, along.x).normalized : Vector2.zero;
                float fade = 1f - (float)i / last;
                float half = width * .5f * fade * fade;
                Color center = PerkEffectMesh.Tint(tint, alpha * fade);
                Color edge = PerkEffectMesh.Tint(tint, 0f);
                mesh.AddVert(points[i] + normal * half, edge, Vector2.zero);
                mesh.AddVert(points[i], center, Vector2.zero);
                mesh.AddVert(points[i] - normal * half, edge, Vector2.zero);
            }
            for (int i = 0; i < last; i++)
            {
                int a = start + i * 3;
                int b = a + 3;
                mesh.AddTriangle(a, a + 1, b + 1);
                mesh.AddTriangle(a, b + 1, b);
                mesh.AddTriangle(a + 1, a + 2, b + 2);
                mesh.AddTriangle(a + 1, b + 2, b + 1);
            }
        }

        // A lead wave and two softer ripples roll out from under the card. Each is an arc of a circle centred
        // beyond the wall, so it bulges forward in the middle and spreads to the floor's sides as it travels.
        private void DrawWave(VertexHelper mesh, Vector2 origin, Vector2 inward, Vector2 across,
            float depth, float span, float unit)
        {
            Vector2 center = origin - inward * depth * .9f;
            float baseAngle = Mathf.Atan2(inward.y, inward.x);
            float leadRadius = 0f;
            for (int wave = 0; wave < 3; wave++)
            {
                float delay = wave * .14f;
                float age = Mathf.Clamp01((release - delay) / (1f - delay));
                if (age <= 0f || age >= 1f) continue;
                float travel = 1f - Mathf.Pow(1f - Mathf.Clamp01(age / .8f), 2.4f);
                float radius = depth * (.9f + travel * 1.35f);
                if (wave == 0) leadRadius = radius;
                float alpha = Mathf.SmoothStep(0f, 1f, age / .08f) * Mathf.Pow(1f - age, 1.3f) * (wave == 0 ? 1f : .55f - wave * .1f);
                float front = depth * (wave == 0 ? .05f : .035f);
                float trail = depth * (wave == 0 ? .3f : .16f);
                DrawArcBand(mesh, center, radius, front, trail, baseAngle, span, depth, wave, alpha);
                if (wave == 0) DrawCrestGlints(mesh, center, radius, baseAngle, span, depth, unit, alpha);
            }
            DrawWakeSpecks(mesh, origin, inward, across, center, leadRadius, depth, span, unit);
        }

        // Two travelling sine ripples bend the crest so it rolls like water instead of a rigid arc.
        private float Undulate(float radius, float along, int wave, float depth) =>
            radius + depth * (.035f * Mathf.Sin(along * Mathf.PI * 4f + release * 9f + wave * 1.7f) +
                .018f * Mathf.Sin(along * Mathf.PI * 9f - release * 13f + wave));

        // Glitter rests on the floor where the lead wave has passed, drifts a little and twinkles out.
        private void DrawWakeSpecks(VertexHelper mesh, Vector2 origin, Vector2 inward, Vector2 across,
            Vector2 center, float leadRadius, float depth, float span, float unit)
        {
            if (leadRadius <= 0f) return;
            for (int i = 0; i < WakeSpecks; i++)
            {
                float seed = PerkEffectMesh.Hash(i, 61f);
                Vector2 rest = origin + across * (Mathf.Repeat(i * .618034f, 1f) - .5f) * span * .95f +
                    inward * depth * (.08f + PerkEffectMesh.Hash(i, 62f) * .88f);
                float passed = (leadRadius - Vector2.Distance(rest, center)) / depth;
                if (passed <= 0f || passed >= .65f) continue;
                float age = passed / .65f;
                Vector2 point = rest + inward * age * unit * (.04f + seed * .06f) +
                    across * Mathf.Sin(age * 5f + i) * unit * .02f;
                float twinkle = .5f + .5f * Mathf.Pow(Mathf.Sin(release * 16f + i * 2.1f), 2f);
                Color tint = rainbow ? PerkEffectMesh.Rainbow(seed + release * .5f, 1f, .5f)
                    : i % 3 == 0 ? PerkEffectMesh.Blush : i % 3 == 1 ? PerkEffectMesh.Gold : PerkEffectMesh.Cream;
                float alpha = Mathf.Sin(Mathf.PI * age) * twinkle * (1f - release * .4f);
                if (i % 4 == 0)
                    PerkEffectMesh.Sparkle(mesh, point, unit * (.02f + seed * .025f), seed, PerkEffectMesh.Tint(tint, alpha));
                else
                    PerkEffectMesh.Glow(mesh, point, unit * (.008f + seed * .01f), PerkEffectMesh.Tint(tint, alpha * .9f), 10);
            }
        }

        // The crest line is brightest; it falls off sharply ahead of the wave and slowly behind it.
        private void DrawArcBand(VertexHelper mesh, Vector2 center, float radius, float front, float trail,
            float baseAngle, float span, float depth, int wave, float alpha)
        {
            if (alpha <= 0f) return;
            float half = ArcHalfAngle(radius, span);
            float hue = wave * .2f + release * .4f;
            for (int i = 0; i < ArcSegments; i++)
            {
                float t0 = (float)i / ArcSegments;
                float t1 = (float)(i + 1) / ArcSegments;
                Vector2 a = Direction(baseAngle + Mathf.Lerp(-half, half, t0));
                Vector2 b = Direction(baseAngle + Mathf.Lerp(-half, half, t1));
                Color crestA = CrestTint(t0, hue, alpha);
                Color crestB = CrestTint(t1, hue, alpha);
                Color wakeA = PerkEffectMesh.Tint(rainbow ? crestA : PerkEffectMesh.Gold, crestA.a * .35f);
                Color wakeB = PerkEffectMesh.Tint(rainbow ? crestB : PerkEffectMesh.Gold, crestB.a * .35f);
                Vector2 ca = center + a * Undulate(radius, t0, wave, depth);
                Vector2 cb = center + b * Undulate(radius, t1, wave, depth);
                // The band swells and thins along its length, like foam on a rolling wave.
                float swellA = .65f + .7f * Mathf.Pow(Mathf.Sin(t0 * Mathf.PI * 3f + release * 6f + wave), 2f);
                float swellB = .65f + .7f * Mathf.Pow(Mathf.Sin(t1 * Mathf.PI * 3f + release * 6f + wave), 2f);
                AddQuad(mesh, ca, cb, cb + b * front * swellB, ca + a * front * swellA, crestA, crestB,
                    PerkEffectMesh.Tint(crestB, 0f), PerkEffectMesh.Tint(crestA, 0f));
                Vector2 ma = ca - a * trail * .25f * swellA;
                Vector2 mb = cb - b * trail * .25f * swellB;
                AddQuad(mesh, ca, cb, mb, ma, crestA, crestB, wakeB, wakeA);
                AddQuad(mesh, ma, mb, cb - b * trail * swellB, ca - a * trail * swellA,
                    wakeA, wakeB, PerkEffectMesh.Tint(wakeB, 0f), PerkEffectMesh.Tint(wakeA, 0f));
            }
        }

        private Color CrestTint(float along, float hue, float alpha)
        {
            // Light shimmers along the crest so it never looks like a flat, solid stroke.
            float shimmer = .6f + .4f * Mathf.Pow(Mathf.Sin(along * Mathf.PI * 7f - release * 22f), 2f);
            if (rainbow) return PerkEffectMesh.Rainbow(along * 1.2f + hue, alpha * .8f * shimmer, .5f);
            Color tint = Color.Lerp(PerkEffectMesh.Cream, PerkEffectMesh.Gold, Mathf.Abs(along - .5f) * 1.6f);
            return PerkEffectMesh.Tint(tint, alpha * .8f * shimmer);
        }

        // Kira glints ride the lead crest and twinkle as it moves.
        private void DrawCrestGlints(VertexHelper mesh, Vector2 center, float radius, float baseAngle,
            float span, float depth, float unit, float alpha)
        {
            float half = ArcHalfAngle(radius, span);
            for (int i = 0; i < CrestGlints; i++)
            {
                float seed = PerkEffectMesh.Hash(i, 51f);
                float along = (i + .5f + (seed - .5f) * .6f) / CrestGlints;
                Vector2 point = center + Direction(baseAngle + Mathf.Lerp(-half, half, along)) * Undulate(radius, along, 0, depth);
                float twinkle = Mathf.Pow(Mathf.Sin(release * 10f + i * 2.3f), 2f);
                Color tint = rainbow ? PerkEffectMesh.Rainbow(along * 1.2f + release * .4f, alpha * twinkle, .5f)
                    : PerkEffectMesh.Tint(i % 3 == 0 ? PerkEffectMesh.Blush : Color.white, alpha * twinkle);
                PerkEffectMesh.Sparkle(mesh, point, unit * (.03f + seed * .03f), seed, tint);
            }
        }

        // Wide enough that the arc always reaches past both side edges of the floor.
        private static float ArcHalfAngle(float radius, float span)
        {
            float reach = span * .62f;
            return radius > reach ? Mathf.Asin(reach / radius) * 1.08f : Mathf.PI * .62f;
        }

        private static Vector2 Direction(float angle) => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

        private static void AddQuad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d,
            Color ta, Color tb, Color tc, Color td)
        {
            if (ta.a <= 0f && tb.a <= 0f && tc.a <= 0f && td.a <= 0f) return;
            int start = mesh.currentVertCount;
            mesh.AddVert(a, ta, Vector2.zero);
            mesh.AddVert(b, tb, Vector2.zero);
            mesh.AddVert(c, tc, Vector2.zero);
            mesh.AddVert(d, td, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
