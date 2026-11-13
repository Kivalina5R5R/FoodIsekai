using System;
using System.Collections.Generic;
using FoodIsekaiZ.Minigames.Games;

namespace FoodIsekaiZ.Minigames
{
    // Which minigames each round offers, and how fast (design section 2): after the first meal the
    // easy four at speed 1, after the second the medium four at 1.25, and after the last meal the
    // health inspector plus the three games the team failed most so far, at 1.5. The players pick
    // one of a round's offers. It also remembers each game's passes and fails, and each game's picture.
    public sealed class MinigameCatalog
    {
        public const int GamesPerRound = 4;

        private static readonly string[] FirstRound = { "ChefSays", "MissingTray", "Fry", "HotPlates" };
        private static readonly string[] SecondRound = { "Grill", "PressureValve", "MixDrink", "OrderUp" };
        private static readonly float[] Speeds = { 1f, 1.25f, 1.5f };

        private static readonly Dictionary<string, Func<Minigame>> Factories = new Dictionary<string, Func<Minigame>>
        {
            { "ChefSays", () => new ChefSaysGame() },
            { "MissingTray", () => new MissingTrayGame() },
            { "Fry", () => new FryGame() },
            { "HotPlates", () => new HotPlatesGame() },
            { "Grill", () => new GrillGame() },
            { "PressureValve", () => new PressureValveGame() },
            { "MixDrink", () => new MixDrinkGame() },
            { "OrderUp", () => new OrderUpGame() },
            { "Inspector", () => new InspectorGame() },
        };

        // The picture shown on each game's choice card (Assets/Art/Minigame); a game whose picture
        // has not been added yet keeps the plain hexagon.
        private static readonly Dictionary<string, string> Pictures = new Dictionary<string, string>
        {
            { "ChefSays", "ChefSign" },
            { "MissingTray", "TrayCover" },
            { "Fry", "FryPan" },
            { "HotPlates", "HotPlates" },
            { "Grill", "Grill" },
            { "PressureValve", "PressurePot" },
            { "MixDrink", "DrinkGlass" },
            { "OrderUp", "FoodCrate" },
            { "Inspector", "InspectorFront" },
        };

        // Fails minus passes per game; higher means the team found it harder.
        private readonly Dictionary<string, int> difficulty = new Dictionary<string, int>();

        // The games of round roundIndex (0 first) out of roundCount; the last round is the closing one.
        public IReadOnlyList<string> Lineup(int roundIndex, int roundCount)
        {
            bool closing = roundIndex >= roundCount - 1 && roundIndex > 0;
            if (closing) return ClosingLineup();
            return roundIndex % 2 == 0 ? FirstRound : SecondRound;
        }

        public float Speed(int roundIndex, int roundCount)
        {
            bool closing = roundIndex >= roundCount - 1 && roundIndex > 0;
            if (closing) return Speeds[2];
            return Speeds[Math.Min(roundIndex, 1)];
        }

        public Minigame Create(string id)
        {
            return Factories[id]();
        }

        public string PictureOf(string id)
        {
            return Pictures.TryGetValue(id, out string picture) ? picture : null;
        }

        public void Record(string id, bool passed)
        {
            difficulty.TryGetValue(id, out int score);
            difficulty[id] = score + (passed ? -1 : 1);
        }

        private IReadOnlyList<string> ClosingLineup()
        {
            var played = new List<string>();
            played.AddRange(FirstRound);
            played.AddRange(SecondRound);
            var random = new Random();
            // Shuffle first so ties between equally hard games are broken at random.
            for (int i = played.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (played[i], played[j]) = (played[j], played[i]);
            }

            played.Sort((left, right) => Difficulty(right).CompareTo(Difficulty(left)));
            var lineup = new List<string> { "Inspector" };
            for (int i = 0; i < played.Count && lineup.Count < GamesPerRound; i++) lineup.Add(played[i]);
            return lineup;
        }

        private int Difficulty(string id)
        {
            return difficulty.TryGetValue(id, out int score) ? score : 0;
        }
    }
}
