using UnityEngine;

namespace FoodIsekaiZ.Minigames
{
    // The shared UWB rules from the minigame design (FOODISEKAIZ_MINIGAMES.md, section 1):
    // a zone is left only 0.15 m past its edge (tags jitter at the edge), a choice needs the player
    // to hold still on it, and "deep inside" means within 85% of the radius so standing on the
    // border between two tiles does not count.
    public static class MinigameZones
    {
        public const float ExitMargin = 0.15f;
        public const float DeepShare = 0.85f;

        // Inside a circle, entered at radius and left at radius + ExitMargin.
        public static bool InCircle(Vector2 point, Vector2 centre, float radius, bool wasInside)
        {
            float limit = wasInside ? radius + ExitMargin : radius;
            return (point - centre).sqrMagnitude <= limit * limit;
        }

        // Inside a rectangle (arena metres), entered at its edge and left ExitMargin past it.
        public static bool InRect(Vector2 point, Rect area, bool wasInside)
        {
            if (wasInside)
            {
                area.xMin -= ExitMargin;
                area.yMin -= ExitMargin;
                area.xMax += ExitMargin;
                area.yMax += ExitMargin;
            }

            return area.Contains(point);
        }

        // Inside an ellipse with these half-widths (arena metres), entered at its edge and left
        // ExitMargin past it.
        public static bool InEllipse(Vector2 point, Vector2 centre, Vector2 radii, bool wasInside)
        {
            Vector2 limit = wasInside ? radii + Vector2.one * ExitMargin : radii;
            if (limit.x <= 0f || limit.y <= 0f) return false;
            Vector2 offset = point - centre;
            float x = offset.x / limit.x;
            float y = offset.y / limit.y;
            return x * x + y * y <= 1f;
        }

        public static bool DeepInside(Vector2 point, Vector2 centre, float radius)
        {
            float limit = radius * DeepShare;
            return (point - centre).sqrMagnitude <= limit * limit;
        }
    }

    // Counts how long one player has held still on one choice (a tile, a valve, a box): a different
    // choice, or none, starts the count again. Done once the count reaches seconds.
    public sealed class HoldToChoose
    {
        private readonly float seconds;
        private int choice = -1;
        private float held;

        public HoldToChoose(float seconds)
        {
            this.seconds = seconds;
        }

        // The choice being held now, or -1.
        public int Choice => choice;

        public float Progress => seconds <= 0f ? 1f : Mathf.Clamp01(held / seconds);

        // Pass the choice the player stands on now (-1 for none); true on the frame the hold completes.
        public bool Tick(int standingOn, float deltaTime)
        {
            if (standingOn != choice)
            {
                choice = standingOn;
                held = 0f;
                return false;
            }

            if (choice < 0) return false;
            bool wasDone = held >= seconds;
            held += Mathf.Min(deltaTime, 0.1f);
            return !wasDone && held >= seconds;
        }

        public void Reset()
        {
            choice = -1;
            held = 0f;
        }
    }
}
