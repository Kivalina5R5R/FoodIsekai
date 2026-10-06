using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // Light streaks that shoot outward from behind the transition artwork toward the wall's side edges,
    // echoing the rays painted in the artwork. They start outside the artwork's ring and stay within a band
    // around the horizontal, so they fill the empty sides instead of crossing the twinkles, the art or the banner.
    // Each beam stretches out as it speeds away, then fades before it reaches the edge.
    // Place it as a child of the page, before the artwork, so the streaks emerge from behind it.
    [AddComponentMenu("Food Isekai Z/Display/Transition Streaks")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TransitionStreaks : MaskableGraphic
    {
        [SerializeField, Range(1, 24)] private int count = 7;
        // Half the width and height of the oval the streaks set off from, just outside the artwork's ring.
        [SerializeField] private Vector2 startRadius = new Vector2(300f, 140f);
        // How far from the center the streaks travel before they vanish.
        [SerializeField, Min(0f)] private float travel = 430f;
        // Streaks leave within this many degrees above or below the horizontal, on both sides.
        [SerializeField, Range(0f, 80f)] private float spreadDegrees = 45f;
        [SerializeField] private Vector2 lengthRange = new Vector2(180f, 300f);
        [SerializeField] private Vector2 widthRange = new Vector2(8f, 14f);
        [SerializeField] private Vector2 lifetimeRange = new Vector2(0.9f, 1.5f);
        // Idle time between one streak finishing and the same slot firing again, so only a few fly at once.
        [SerializeField] private Vector2 restRange = new Vector2(0.3f, 1.2f);
        [SerializeField] private Color headColor = new Color(0.99f, 0.88f, 0.58f, 1f);
        [SerializeField] private Color tailColor = new Color(0.98f, 0.76f, 0.5f, 1f);
        [SerializeField, Range(0f, 1f)] private float opacity = 0.8f;

        private float elapsed;

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
        }

        // The page runs during phase changes while gameplay is paused, so streaks use real time.
        private void Update()
        {
            if (!Application.isPlaying) return;
            elapsed += Time.unscaledDeltaTime;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            Vector2 center = rectTransform.rect.center;
            for (int index = 0; index < count; index++)
            {
                float life = Mathf.Max(0.2f, Mathf.Lerp(lifetimeRange.x, Mathf.Max(lifetimeRange.x, lifetimeRange.y), Hash(index, 0, 1)));
                float rest = Mathf.Max(0f, Mathf.Lerp(restRange.x, Mathf.Max(restRange.x, restRange.y), Hash(index, 0, 2)));
                float cycle = life + rest;
                // Offsets spread the slots through the cycle so the streaks take turns.
                float clock = elapsed + Hash(index, 0, 3) * cycle;
                int generation = Mathf.FloorToInt(clock / cycle);
                float age = (clock - generation * cycle) / life;
                if (age >= 1f) continue;

                // Alternate sides and pick a heading within the spread above or below the horizontal.
                bool left = (index + generation) % 2 == 0;
                float tilt = (Hash(index, generation, 4) * 2f - 1f) * spreadDegrees * Mathf.Deg2Rad;
                float heading = (left ? Mathf.PI : 0f) + (left ? -tilt : tilt);
                Vector2 direction = new Vector2(Mathf.Cos(heading), Mathf.Sin(heading));
                Vector2 origin = center + new Vector2(direction.x * startRadius.x, direction.y * startRadius.y);

                // Fast out of the start, easing as it travels; the beam reaches full length early and keeps it.
                float eased = 1f - (1f - age) * (1f - age);
                float distance = eased * travel;
                float length = Mathf.Lerp(lengthRange.x, Mathf.Max(lengthRange.x, lengthRange.y), Hash(index, generation, 5)) *
                    Mathf.Clamp01(age / 0.3f);
                float width = Mathf.Lerp(widthRange.x, Mathf.Max(widthRange.x, widthRange.y), Hash(index, generation, 6)) *
                    (0.75f + 0.25f * Mathf.Sin(age * Mathf.PI));
                float fade = Mathf.Clamp01(age / 0.15f) * Mathf.Clamp01((1f - age) / 0.35f);

                Vector2 head = origin + direction * distance;
                Vector2 tail = head - direction * length;
                Color headTint = headColor * color;
                Color tailTint = tailColor * color;
                headTint.a *= opacity * fade;
                tailTint.a *= opacity * fade * 0.6f;
                DrawStreak(vertices, tail, head, width, tailTint, headTint);
            }
        }

        // A beam shaped like the rays painted in the artwork: a sharp point toward the center, swelling to its
        // widest about two thirds of the way out, then rounding off at the outer end. The color runs from the
        // tail tint at the point to the head tint at the outer end.
        private static void DrawStreak(VertexHelper vertices, Vector2 tail, Vector2 head, float width, Color tailTint, Color headTint)
        {
            Vector2 along = head - tail;
            if (along.sqrMagnitude < 0.01f) return;
            Vector2 side = new Vector2(-along.y, along.x).normalized;
            const int steps = 10;
            int first = vertices.currentVertCount;
            for (int step = 0; step <= steps; step++)
            {
                float t = (float)step / steps;
                // Rises slowly from the point, peaks near 0.7, and closes quickly in a rounded end.
                float profile = t < 0.7f
                    ? Mathf.Pow(t / 0.7f, 1.3f)
                    : Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((t - 0.7f) / 0.3f, 2f)));
                Vector2 center = tail + along * t;
                Vector2 offset = side * (width * 0.5f * profile);
                Color tint = Color.Lerp(tailTint, headTint, t);
                vertices.AddVert(center + offset, tint, Vector2.zero);
                vertices.AddVert(center - offset, tint, Vector2.zero);
            }

            for (int step = 0; step < steps; step++)
            {
                int a = first + step * 2;
                vertices.AddTriangle(a, a + 2, a + 3);
                vertices.AddTriangle(a, a + 3, a + 1);
            }
        }

        // A stable pseudo-random value from 0 to 1 for this streak, flight and purpose,
        // so the pattern never consumes the gameplay random sequence.
        private static float Hash(int index, int generation, int salt)
        {
            uint value = (uint)(index * 73856093) ^ (uint)(generation * 19349663) ^ (uint)(salt * 83492791);
            value ^= value >> 13;
            value *= 0x5bd1e995;
            value ^= value >> 15;
            return (value & 0xFFFFFF) / (float)0x1000000;
        }
    }
}
