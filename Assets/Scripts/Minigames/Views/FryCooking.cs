using UnityEngine;

namespace FoodIsekaiZ.Minigames.Views
{
    // The doneness scale of Fry It Golden, shared by the game's rules and its look: cooked runs from
    // 0 (raw) to CharredAt, golden between GoldenFrom and BurntFrom. Colour turns raw cream to golden,
    // then brown at 1 and burnt at CharredAt.
    public static class FryCooking
    {
        public const float GoldenFrom = 0.6f;
        public const float BurntFrom = 0.8f;
        public const float CharredAt = 1.08f;

        public static readonly Color DefaultRaw = new Color(0.98f, 0.93f, 0.78f, 1f);
        public static readonly Color DefaultGolden = new Color(1f, 0.74f, 0.2f, 1f);
        public static readonly Color DefaultBrown = new Color(0.62f, 0.34f, 0.12f, 1f);
        public static readonly Color DefaultBurnt = new Color(0.16f, 0.1f, 0.07f, 1f);

        public static Color Colour(float cooked, Color raw, Color golden, Color brown, Color burnt)
        {
            if (cooked < GoldenFrom) return Color.Lerp(raw, golden, cooked / GoldenFrom);
            if (cooked < BurntFrom) return golden;
            if (cooked < 1f) return Color.Lerp(golden, brown, (cooked - BurntFrom) / (1f - BurntFrom));
            return Color.Lerp(brown, burnt, (cooked - 1f) / (CharredAt - 1f));
        }

        // The default colour, used for the guide pieces when the prefab is built.
        public static Color DefaultColour(float cooked)
        {
            return Colour(cooked, DefaultRaw, DefaultGolden, DefaultBrown, DefaultBurnt);
        }
    }
}
