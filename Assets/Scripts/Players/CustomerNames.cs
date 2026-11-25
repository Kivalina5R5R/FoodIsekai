using Fortal.Arena;

namespace FoodIsekaiZ.Players
{
    // The name to show for a player: the customer's name the Fortal launcher sent for the UWB tag in
    // that player's slot (the slots come from ArenaPlayerNames), cut short to fit, or the game's own
    // label ("P1", "PLAYER 1") when the launcher does not know them.
    public static class CustomerNames
    {
        public static string ForPlayer(int playerId, string fallback, int most)
        {
            string name = playerId > 0 ? FortalArenaPlayers.NameForSlot(playerId - 1) : "";
            return name.Length > 0 ? Shorten(name, most) : fallback;
        }

        // Like ForPlayer, but keeps only the first word of the name ("Fam ttera" shows "Fam"), still
        // cut short past most letters when that one word is long. For tight spots such as the MVP bar.
        public static string FirstNameForPlayer(int playerId, string fallback, int most)
        {
            string name = playerId > 0 ? FortalArenaPlayers.NameForSlot(playerId - 1) : "";
            return name.Length > 0 ? Shorten(FirstWord(name), most) : fallback;
        }

        // The text up to its first space, or the whole text when it has no space.
        public static string FirstWord(string name)
        {
            string text = name == null ? "" : name.Trim();
            int space = text.IndexOfAny(new[] { ' ', '\t', '　' });
            return space > 0 ? text.Substring(0, space) : text;
        }

        // The name trimmed and, past most letters, cut short with "…". Letters are counted as the eye
        // does, so Thai marks and emoji are never split.
        public static string Shorten(string name, int most)
        {
            string text = name == null ? "" : name.Trim();
            var letters = new System.Globalization.StringInfo(text);
            if (letters.LengthInTextElements <= most) return text;
            return letters.SubstringByTextElements(0, most - 1) + "…";
        }
    }
}
