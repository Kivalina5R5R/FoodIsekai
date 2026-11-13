using System.Collections.Generic;
using FoodIsekaiZ.Minigames.Views;
using UnityEngine;

namespace FoodIsekaiZ.Minigames.Games
{
    // What's Missing? (design 5.3, after Absent Minded): five dishes show on a tray on the wall, a
    // cover drops over it, and the tray comes back with one dish gone and the rest shuffled. Six
    // answer tiles on the floor hold the five tray dishes plus one decoy; holding still on a tile
    // locks that answer for good. Passes when at least half the team picks the missing dish.
    // The dishes are the real game's fifteen (Assets/Art/Food). The look is the MissingTray prefab
    // (MissingTrayView); dishes are passed to it by key.
    public sealed class MissingTrayGame : Minigame
    {
        private const int TrayCount = 5;
        private const int TileCount = 6;
        private const int Columns = 3;
        private const float TileSize = 1.1f;
        private const float TileGap = 0.3f;
        private const float HoldSeconds = 0.5f;
        private const float CoverSeconds = 0.5f;

        private static readonly Vector2 TileCentre = new Vector2(5.5f, 2.6f);

        // One of the game's real dishes: its key (the picture's name in Assets/Art/Food, which the
        // view looks up) and its names.
        private readonly struct Dish
        {
            public Dish(string key, string english, string thai)
            {
                Key = key;
                English = english;
                Thai = thai;
            }

            public string Key { get; }
            public string English { get; }
            public string Thai { get; }
        }

        private sealed class Answer
        {
            public HoldToChoose Hold;
            public int Tile = -1;
            public float Seconds;
        }

        private enum Phase
        {
            Show,
            Cover,
            Answer,
        }

        private readonly List<Dish> trayDishes = new List<Dish>();
        private readonly List<Dish> tileDishes = new List<Dish>();
        private readonly Vector2[] tileCentres = new Vector2[TileCount];
        private readonly Dictionary<MinigamePlayer, Answer> answers = new Dictionary<MinigamePlayer, Answer>();
        private Dish missingDish;
        private int missingTile;
        private Phase phase;
        private float phaseTime;
        private MissingTrayView view;

        public override string EnglishCommand => "REMEMBER!";
        public override string ThaiCommand => "จำ!";
        public override string EnglishTitle => "WHAT'S MISSING?";
        public override string ThaiTitle => "อะไรหายไปจากถาด?";
        public override string EnglishRule => "Remember the tray, then stand on the dish that went missing.";
        public override string ThaiRule => "จำอาหารบนถาด แล้วไปยืนบนแผ่นของจานที่หายไป";

        private float ShowSeconds => 2.6f / Mathf.Pow(Speed, 0.4f);
        private float AnswerSeconds => 4.8f / Mathf.Pow(Speed, 0.3f);

        protected override void Build()
        {
            List<Dish> pool = AllDishes();
            Shuffle(pool);
            trayDishes.AddRange(pool.GetRange(0, TrayCount));
            missingDish = trayDishes[Random.Range(0, TrayCount)];

            // The floor tiles hold the five tray dishes plus the next dish as a decoy, shuffled.
            tileDishes.AddRange(trayDishes);
            tileDishes.Add(pool[TrayCount]);
            Shuffle(tileDishes);
            missingTile = tileDishes.FindIndex(dish => SameDish(dish, missingDish));

            foreach (MinigamePlayer player in Players) answers[player] = new Answer { Hold = new HoldToChoose(HoldSeconds) };

            view = Stage.LoadView<MissingTrayView>("MissingTray", WallLayer, FloorLayer);
            BuildFloor();
            BuildWall();
        }

        protected override void Step(float deltaTime)
        {
            if (Players.Count == 0)
            {
                Finish(false);
                return;
            }

            phaseTime += deltaTime;
            if (phase == Phase.Show)
            {
                if (phaseTime < ShowSeconds) return;
                phase = Phase.Cover;
                phaseTime = 0f;
                view.CoverTray();
                view.SetStatus("…");
                return;
            }

            if (phase == Phase.Cover)
            {
                if (phaseTime < CoverSeconds) return;
                StartAnswering();
                return;
            }

            CollectAnswers(deltaTime);
            if (EveryoneAnswered() || phaseTime >= AnswerSeconds) FinishGame();
        }

        protected override void OnTimeUp()
        {
            FinishGame();
        }

        private void BuildFloor()
        {
            int rows = TileCount / Columns;
            float pitch = TileSize + TileGap;
            for (int i = 0; i < TileCount; i++)
            {
                int column = i % Columns;
                int row = i / Columns;
                tileCentres[i] = TileCentre + new Vector2((column - (Columns - 1) * 0.5f) * pitch, (row - (rows - 1) * 0.5f) * pitch);
            }

            for (int i = 0; i < TileCount; i++)
                view.AddTile(tileCentres[i], tileDishes[i].Key, Pick(tileDishes[i].English, tileDishes[i].Thai));
        }

        private void BuildWall()
        {
            view.SetStatus(Pick("Remember the tray!", "จำอาหารบนถาด!"));
            ShowRow(trayDishes, view.FillFullTray);

            // The tray after the cover: the four left over, shuffled into new places, one slot empty.
            var left = new List<Dish>();
            foreach (Dish dish in trayDishes)
                if (!SameDish(dish, missingDish)) left.Add(dish);
            Shuffle(left);
            ShowRow(left, view.FillShortTray);
        }

        // Hands a row of dishes to the view as keys and names in the current language.
        private static void ShowRow(List<Dish> row, System.Action<IReadOnlyList<string>, IReadOnlyList<string>> fill)
        {
            var keys = new List<string>();
            var names = new List<string>();
            foreach (Dish dish in row)
            {
                keys.Add(dish.Key);
                names.Add(Pick(dish.English, dish.Thai));
            }

            fill(keys, names);
        }

        private void StartAnswering()
        {
            phase = Phase.Answer;
            phaseTime = 0f;
            view.RevealShortTray();
            view.SetStatus(Pick("What's missing? Stand on it!", "อะไรหายไป? ไปยืนบนแผ่นนั้น!"));
        }

        private void CollectAnswers(float deltaTime)
        {
            for (int p = 0; p < Players.Count; p++)
            {
                MinigamePlayer player = Players[p];
                Answer answer = answers[player];
                if (answer.Tile >= 0) continue;
                int standingOn = player.IsPresent ? TileUnder(player.Position) : -1;
                if (!answer.Hold.Tick(standingOn, deltaTime)) continue;

                answer.Tile = standingOn;
                answer.Seconds = phaseTime;
                MarkAnswer(player, p, standingOn);
            }
        }

        // True once every player still on the floor has locked an answer.
        private bool EveryoneAnswered()
        {
            foreach (MinigamePlayer player in Players)
                if (player.IsPresent && answers[player].Tile < 0) return false;
            return true;
        }

        private int TileUnder(Vector2 position)
        {
            for (int i = 0; i < TileCount; i++)
                if (MinigameZones.DeepInside(position, tileCentres[i], TileSize * 0.5f)) return i;
            return -1;
        }

        // A dot in the player's colour along the tile's top edge shows their locked answer.
        private void MarkAnswer(MinigamePlayer player, int playerIndex, int tile)
        {
            float slot = (playerIndex % 6 - 2.5f) * 0.17f;
            Vector2 spot = tileCentres[tile] + new Vector2(slot, -TileSize * 0.5f + 0.1f);
            view.MarkAnswer(player.Colour, spot);
        }

        private void FinishGame()
        {
            int correct = 0;
            foreach (MinigamePlayer player in Players)
            {
                Answer answer = answers[player];
                if (answer.Tile < 0)
                {
                    SetVerdict(player, false, "No answer", "ไม่ได้ตอบ");
                    continue;
                }

                if (answer.Tile != missingTile)
                {
                    SetVerdict(player, false, "Wrong", "ผิด");
                    continue;
                }

                correct++;
                SetVerdict(player, true, $"Correct {answer.Seconds:0.0}s", $"ถูก {answer.Seconds:0.0} วิ");
            }

            Finish(HalfTeam(correct, Players.Count));
        }

        private static bool SameDish(Dish a, Dish b)
        {
            return a.Key == b.Key;
        }

        // The fifteen dishes served in the real game: five each for breakfast (M-), lunch (L-) and
        // dinner (D-). A category repeats across meals, so the names say which meal it is from.
        private static List<Dish> AllDishes()
        {
            return new List<Dish>
            {
                new Dish("M-Meat", "Breakfast Meat", "เนื้อ (เช้า)"),
                new Dish("M-Seafood", "Breakfast Seafood", "ซีฟู้ด (เช้า)"),
                new Dish("M-Starter", "Breakfast Starter", "ออเดิร์ฟ (เช้า)"),
                new Dish("M-Dessert", "Breakfast Dessert", "ของหวาน (เช้า)"),
                new Dish("M-Drink", "Breakfast Drink", "เครื่องดื่ม (เช้า)"),
                new Dish("L-Meat", "Lunch Meat", "เนื้อ (กลางวัน)"),
                new Dish("L-Seafood", "Lunch Seafood", "ซีฟู้ด (กลางวัน)"),
                new Dish("L-Starter", "Lunch Starter", "ออเดิร์ฟ (กลางวัน)"),
                new Dish("L-Dessert", "Lunch Dessert", "ของหวาน (กลางวัน)"),
                new Dish("L-Drink", "Lunch Drink", "เครื่องดื่ม (กลางวัน)"),
                new Dish("D-Meat", "Dinner Meat", "เนื้อ (เย็น)"),
                new Dish("D-Seafood", "Dinner Seafood", "ซีฟู้ด (เย็น)"),
                new Dish("D-Starter", "Dinner Starter", "ออเดิร์ฟ (เย็น)"),
                new Dish("D-Dessert", "Dinner Dessert", "ของหวาน (เย็น)"),
                new Dish("D-Drink", "Dinner Drink", "เครื่องดื่ม (เย็น)"),
            };
        }

        private static void Shuffle<T>(IList<T> items)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }
    }
}
