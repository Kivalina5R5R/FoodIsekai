using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // A shared burn contour drives a stencil mask and its glowing edge, including TMP text clipping.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PerkBurnGraphic : MaskableGraphic
    {
        private const int Segments = 192;
        private bool maskOnly;
        private float progress;
        private float emberFade;
        private Vector2[] frameContour;
        private Vector2 ignition = Vector2.one * .5f;
        private float noiseSeed;
        private Vector2[] otherIgnitions;
        private int ignitionIndex;

        public void SetOtherIgnitions(Vector2[] origins, int ownIndex)
        {
            otherIgnitions = (Vector2[])origins.Clone();
            ignitionIndex = ownIndex;
        }

        // Uses the authored frame bounds so fire does not float beyond the paper silhouette.
        public void SetFrame(RectTransform frame)
        {
            var corners = new Vector3[4];
            frame.GetWorldCorners(corners);
            Vector2 minimum = rectTransform.InverseTransformPoint(corners[0]);
            Vector2 maximum = rectTransform.InverseTransformPoint(corners[2]);
            var bounds = Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
            frameContour = new Vector2[256];
            for (int i = 0; i < frameContour.Length; i++)
                frameContour[i] = PerkFrameContour.Sample(bounds, i / (float)frameContour.Length);
            SetVerticesDirty();
        }

        public void Configure(bool renderMask, Vector2 origin, float seed)
        {
            maskOnly = renderMask;
            ignition = origin;
            noiseSeed = seed;
            raycastTarget = false;
            SetVerticesDirty();
        }

        public void SetBurn(float amount, float fadingEmbers = 0f)
        {
            progress = Mathf.Clamp01(amount);
            emberFade = Mathf.Clamp01(fadingEmbers);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = rectTransform.rect;
            Vector2 origin = new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, ignition.x),
                Mathf.Lerp(rect.yMin, rect.yMax, ignition.y));
            float reach = new Vector2(Mathf.Max(origin.x - rect.xMin, rect.xMax - origin.x),
                Mathf.Max(origin.y - rect.yMin, rect.yMax - origin.y)).magnitude * 1.2f;
            if (maskOnly && progress >= 1f) return;
            if (!maskOnly && (progress <= 0f || emberFade >= 1f)) return;
            float fade = (1f - emberFade) * Mathf.Clamp01(progress * 14f);
            for (int i = 0; i < Segments; i++)
            {
                float angleA = i * Mathf.PI * 2f / Segments;
                float angleB = (i + 1f) * Mathf.PI * 2f / Segments;
                Vector2 directionA = new Vector2(Mathf.Cos(angleA), Mathf.Sin(angleA));
                Vector2 directionB = new Vector2(Mathf.Cos(angleB), Mathf.Sin(angleB));
                float outerA = DistanceToEdge(origin, directionA, rect);
                float outerB = DistanceToEdge(origin, directionB, rect);
                float radiusA = Radius(angleA, reach);
                float radiusB = Radius(angleB, reach);
                Vector2 a = origin + directionA * Mathf.Min(radiusA, outerA);
                Vector2 b = origin + directionB * Mathf.Min(radiusB, outerB);
                if (maskOnly)
                {
                    Quad(mesh, a, b, origin + directionB * outerB, origin + directionA * outerA, Color.white);
                    continue;
                }
                if (progress >= 1f || radiusA >= outerA || radiusB >= outerB ||
                    !InsideFrame(a) || !InsideFrame(b) || BurnedElsewhere(a, rect, progress) ||
                    BurnedElsewhere(b, rect, progress)) continue;
                float flicker = .8f + .2f * Mathf.Sin(progress * 110f + angleA * 13f + noiseSeed);
                BurnBand(mesh, a, b, directionA, directionB, 9f, new Color(.23f, .085f, .025f, fade * .55f));
                BurnBand(mesh, a, b, directionA, directionB, 5f, new Color(1f, .32f, .045f, fade * .65f * flicker));
                BurnBand(mesh, a, b, directionA, directionB, 1.8f, new Color(1f, .83f, .35f, fade * .8f * flicker));
            }
            if (maskOnly) return;
            for (int i = 0; i < 40; i++)
            {
                float seed = Mathf.Repeat(i * .618034f + noiseSeed, 1f);
                float age = Mathf.Repeat(progress * 3.4f + seed, 1f);
                float angle = seed * Mathf.PI * 2f;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float birth = Mathf.Max(0f, progress - age * .23f);
                float distance = RadiusAt(angle, reach, birth);
                Vector2 point = origin + direction * distance;
                if (!rect.Contains(point) || !InsideFrame(point) || BurnedElsewhere(point, rect, birth)) continue;
                point += new Vector2(Mathf.Sin(i * 7.1f + age * 4f) * 12f, age * 55f + emberFade * 35f);
                float size = Mathf.Lerp(3.1f, .5f, age);
                Color tint = Color.Lerp(new Color(1f, .35f, .04f), new Color(1f, .9f, .48f), seed);
                tint.a = fade * (1f - age);
                Quad(mesh, point + Vector2.left * size, point + Vector2.down * size * 1.6f,
                    point + Vector2.right * size, point + Vector2.up * size * 1.6f, tint);
            }
            DrawAsh(mesh, rect, origin, reach, fade);
        }

        private float Radius(float angle, float reach) => RadiusAt(angle, reach, progress);

        private float RadiusAt(float angle, float reach, float amount)
        {
            float irregularity = 1f + Mathf.Sin(angle * 7f + noiseSeed) * .055f +
                Mathf.Sin(angle * 19f - noiseSeed) * .025f + Mathf.Sin(angle * 37f) * .012f;
            return Mathf.Pow(amount, 1.8f) * reach * irregularity;
        }

        private bool BurnedElsewhere(Vector2 point, Rect rect, float amount)
        {
            if (otherIgnitions == null) return false;
            for (int i = 0; i < otherIgnitions.Length; i++)
            {
                if (i == ignitionIndex) continue;
                Vector2 origin = rect.min + Vector2.Scale(otherIgnitions[i], rect.size);
                Vector2 offset = point - origin;
                float reach = new Vector2(Mathf.Max(origin.x - rect.xMin, rect.xMax - origin.x),
                    Mathf.Max(origin.y - rect.yMin, rect.yMax - origin.y)).magnitude * 1.2f;
                if (offset.magnitude < RadiusAt(Mathf.Atan2(offset.y, offset.x), reach, amount)) return true;
            }
            return false;
        }

        // Ash starts on a past burn edge, then drifts independently as the fire moves on.
        private void DrawAsh(VertexHelper mesh, Rect rect, Vector2 origin, float reach, float fade)
        {
            for (int i = 0; i < 36; i++)
            {
                float seed = Mathf.Repeat(i * .618034f + noiseSeed * .17f, 1f);
                float age = Mathf.Repeat(progress * 3f + seed, 1f);
                float birth = progress - age * .34f;
                if (birth <= .01f) continue;
                float angle = seed * Mathf.PI * 2f;
                Vector2 point = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * RadiusAt(angle, reach, birth);
                if (!rect.Contains(point) || !InsideFrame(point) || BurnedElsewhere(point, rect, birth)) continue;
                float life = age + emberFade;
                if (life >= 1f) continue;
                point += new Vector2(Mathf.Sin(seed * 21f + life * 3f) * life * 35f,
                    life * (65f + seed * 50f));
                Color tint = Color.Lerp(new Color(.28f, .23f, .19f), new Color(.78f, .72f, .62f), seed);
                tint.a = fade * Mathf.Sin(life * Mathf.PI) * .65f;
                SoftDust(mesh, point, Mathf.Lerp(2f, 6f, life) * (.6f + seed), tint);
            }
        }

        private static void SoftDust(VertexHelper mesh, Vector2 center, float radius, Color tint)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(center, tint, Vector2.zero);
            tint.a = 0f;
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4f;
                mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, tint, Vector2.zero);
            }
            for (int i = 0; i < 8; i++) mesh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % 8);
        }

        private static float DistanceToEdge(Vector2 origin, Vector2 direction, Rect rect)
        {
            float horizontal = Mathf.Abs(direction.x) < .00001f ? float.PositiveInfinity :
                ((direction.x > 0f ? rect.xMax : rect.xMin) - origin.x) / direction.x;
            float vertical = Mathf.Abs(direction.y) < .00001f ? float.PositiveInfinity :
                ((direction.y > 0f ? rect.yMax : rect.yMin) - origin.y) / direction.y;
            return Mathf.Min(horizontal, vertical);
        }

        private static void BurnBand(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 directionA,
            Vector2 directionB, float width, Color tint)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(a - directionA * width * .18f, new Color(tint.r, tint.g, tint.b, 0f), Vector2.zero);
            mesh.AddVert(b - directionB * width * .18f, new Color(tint.r, tint.g, tint.b, 0f), Vector2.zero);
            mesh.AddVert(b, tint, Vector2.zero);
            mesh.AddVert(a, tint, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
            start = mesh.currentVertCount;
            mesh.AddVert(a, tint, Vector2.zero);
            mesh.AddVert(b, tint, Vector2.zero);
            tint.a = 0f;
            mesh.AddVert(b + directionB * width, tint, Vector2.zero);
            mesh.AddVert(a + directionA * width, tint, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }
        private bool InsideFrame(Vector2 point)
        {
            if (frameContour == null) return true;
            bool inside = false;
            for (int i = 0, previous = frameContour.Length - 1; i < frameContour.Length; previous = i++)
            {
                Vector2 a = frameContour[i];
                Vector2 b = frameContour[previous];
                if ((a.y > point.y) != (b.y > point.y) &&
                    point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }


        private static void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(a, tint, Vector2.zero);
            mesh.AddVert(b, tint, Vector2.zero);
            mesh.AddVert(c, tint, Vector2.zero);
            mesh.AddVert(d, tint, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
