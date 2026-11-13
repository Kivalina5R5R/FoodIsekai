using System.Collections.Generic;
using FoodIsekaiZ.Minigames.Views;
using UnityEngine;

namespace FoodIsekaiZ.Minigames.Games
{
    // Mix the Drink (design 4.4, after The Way I Like It): three taps on the floor, red, yellow and
    // blue. Everyone standing in a tap pours its colour into one shared team glass on the wall
    // (more players on a tap pour faster). The team fills it to 80-100% with the recipe's colour
    // ratio, then steps off the taps together to serve. Ends when the glass is at least 80% full and
    // nobody has poured for 0.8 s, when it spills past 100% (fails at once), or when time runs out.
    // Passes when the glass is 80-100% full and no colour's share is more than 16% off the recipe.
    // A team game: every player gets the team's verdict.
    // The look is the MixDrink prefab (MixDrinkView); the tap positions here are the play zones and
    // the view draws the taps where these say.
    public sealed class MixDrinkGame : Minigame
    {
        private const int Colours = 3;
        private const float TapRadius = 0.85f;
        private const float PourPerSecond = 0.24f;
        private const float FullFrom = 0.8f;
        private const float ServeAfterQuiet = 0.8f;
        private const float AllowedError = 0.16f;

        private static readonly Vector2[] TapCentres =
        {
            new Vector2(1.9f, 1.25f),
            new Vector2(5.5f, 1.25f),
            new Vector2(9.1f, 1.25f),
        };

        private static readonly string[] EnglishColourNames = { "Red", "Yellow", "Blue" };
        private static readonly string[] ThaiColourNames = { "แดง", "เหลือง", "ฟ้า" };

        private static readonly Recipe[] Recipes =
        {
            new Recipe("Yuzu Orange", "น้ำส้มยูซุ", 1, 1, 0),
            new Recipe("Elf Matcha", "มัทฉะเอลฟ์", 0, 1, 1),
            new Recipe("Enchanted Grape Wine", "ไวน์องุ่นมนตรา", 1, 0, 1),
            new Recipe("Dragon Lime Tea", "ชามะนาวมังกร", 1, 2, 0),
            new Recipe("Mana Potion", "ยาฟื้นมานา", 0, 1, 2),
            new Recipe("Red Sun Soup", "ซุปตะวันแดง", 2, 1, 0),
        };

        // A drink on the menu: its names and how many parts of red, yellow and blue it takes.
        private readonly struct Recipe
        {
            private readonly int[] parts;

            public Recipe(string english, string thai, int red, int yellow, int blue)
            {
                English = english;
                Thai = thai;
                parts = new[] { red, yellow, blue };
            }

            public string English { get; }
            public string Thai { get; }

            public int Parts(int colour)
            {
                return parts[colour];
            }

            public float Share(int colour)
            {
                return (float)parts[colour] / (parts[0] + parts[1] + parts[2]);
            }

            public int[] AllParts()
            {
                return new[] { parts[0], parts[1], parts[2] };
            }
        }

        private readonly Dictionary<MinigamePlayer, int> tapOf = new Dictionary<MinigamePlayer, int>();
        private readonly float[] poured = new float[Colours];
        private Recipe recipe;
        private float quietSeconds;
        private MixDrinkView view;

        public override string EnglishCommand => "MIX!";
        public override string ThaiCommand => "ผสม!";
        public override string EnglishTitle => "MIX THE DRINK";
        public override string ThaiTitle => "ผสม​เครื่องดื่ม";
        public override string EnglishRule => "Stand at the taps to pour. Match the colour, fill 80-100%, then step off together.";
        public override string ThaiRule => "ยืน​ที่​ก๊อก​เพื่อ​เท ผสม​ให้​สี​ตรง​สูตร เต็ม 80–100% แล้ว​ออก​จาก​ก๊อก​พร้อม​กัน";

        private float PlaySeconds => 8f / Mathf.Sqrt(Speed);

        private float PourRate => PourPerSecond * Mathf.Sqrt(Speed);

        private float Fill => poured[0] + poured[1] + poured[2];

        protected override void Build()
        {
            recipe = Recipes[Random.Range(0, Recipes.Length)];
            foreach (MinigamePlayer player in Players) tapOf[player] = -1;

            view = Stage.LoadView<MixDrinkView>("MixDrink", WallLayer, FloorLayer);
            for (int i = 0; i < Colours; i++) view.AddTap(TapCentres[i], i);
            var colourNames = new string[Colours];
            for (int i = 0; i < Colours; i++) colourNames[i] = Pick(EnglishColourNames[i], ThaiColourNames[i]);
            view.ShowRecipe(Pick(recipe.English, recipe.Thai), recipe.AllParts(), colourNames);
            view.ShowStatus(Pick("Pour, then step off to serve!", "เท​แล้ว​ออก​จาก​ก๊อก​เพื่อ​เสิร์ฟ!"));
        }

        protected override void Step(float deltaTime)
        {
            if (Players.Count == 0)
            {
                Finish(false);
                return;
            }

            bool anyonePouring = Pour(deltaTime);
            quietSeconds = anyonePouring ? 0f : quietSeconds + deltaTime;
            ShowGlass();

            if (Fill > 1f)
            {
                Serve();
                return;
            }

            bool served = Fill >= FullFrom && quietSeconds >= ServeAfterQuiet;
            if (served || Elapsed >= PlaySeconds) Serve();
        }

        protected override void OnTimeUp()
        {
            Serve();
        }

        // Pours each standing player's tap into the glass; true when anyone poured this frame.
        private bool Pour(float deltaTime)
        {
            bool anyone = false;
            foreach (MinigamePlayer player in Players)
            {
                int tap = TapUnder(player);
                tapOf[player] = tap;
                if (tap < 0) continue;
                poured[tap] += PourRate * deltaTime;
                anyone = true;
            }

            return anyone;
        }

        // The tap the player stands in (keeping the tap they were already in until they leave it
        // past the exit margin), or -1.
        private int TapUnder(MinigamePlayer player)
        {
            if (!player.IsPresent) return -1;
            int was = tapOf[player];
            for (int i = 0; i < Colours; i++)
            {
                if (MinigameZones.InCircle(player.Position, TapCentres[i], TapRadius, was == i)) return i;
            }

            return -1;
        }

        private void ShowGlass()
        {
            view.ShowGlass(poured, FullFrom);
        }

        // The largest gap between a colour's share of what was poured and its share in the recipe.
        private float RatioError()
        {
            float fill = Fill;
            if (fill <= 0f) return 1f;
            float worst = 0f;
            for (int i = 0; i < Colours; i++)
                worst = Mathf.Max(worst, Mathf.Abs(poured[i] / fill - recipe.Share(i)));
            return worst;
        }

        private void Serve()
        {
            ShowGlass();
            float fill = Fill;
            bool passed = false;
            if (fill > 1f) Result(false, "Spilled!", "ล้น​แก้ว!");
            else if (fill < FullFrom) Result(false, "Not full", "ยัง​ไม่​เต็ม");
            else if (RatioError() > AllowedError) Result(false, "Wrong mix", "สี​ไม่​ตรง​สูตร");
            else
            {
                passed = true;
                Result(true, "Served!", "เสิร์ฟ​ได้!");
            }

            Finish(passed);
        }

        private void Result(bool success, string english, string thai)
        {
            foreach (MinigamePlayer player in Players) SetVerdict(player, success, english, thai);
            view.ShowResult(Pick(english, thai), success);
        }
    }
}
