using System.Globalization;

namespace FoodIsekaiZ.Display
{
    // Keeps animated and immediate results on the same compact, single-line breakdown.
    public static class ResultScoreText
    {
        public static string Format(int players, int penalty, int coins, int teamBonus = 0)
        {
            string text = $"PLAYERS {Number(players)}";
            if (teamBonus != 0) text += $"   +{Number(teamBonus)} TEAM";
            return $"{text}   -{Number(penalty)} PENALTY   +{Number(coins)} COINS";
        }

        private static string Number(int value) => value.ToString("N0", CultureInfo.InvariantCulture);
    }
}
