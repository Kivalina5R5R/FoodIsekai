using System.Collections.Generic;
using FoodIsekaiZ.Minigames.Views;
using UnityEngine;

namespace FoodIsekaiZ.Minigames.Games
{
    // Order Up! (design 5.5, after Order Up): the customer on the wall orders two dishes, hidden in
    // eight crates on the floor. Holding still on a crate opens it for a moment (its dish shows on
    // that floor crate only; the wall shows just what to find), then it closes again, so remember
    // what was where. The crates hold eight different dishes from the real game's fifteen.
    // With two or more players it is odd players against even players: a team finds an ordered dish
    // when one of its players opens the crate holding it, and the first team to find both wins.
    // One player plays alone against the clock. Passes when a team (or the solo player) wins.
    // The look is the OrderUp prefab (OrderUpView); the crates' places and sizes there are also
    // where players stand to open them.
    public sealed class OrderUpGame : Minigame
    {
        private const int OrderCount = 2;
        private const float HoldSeconds = 0.4f;
        private const float OpenSeconds = 1.5f;

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

        private sealed class Box
        {
            public Vector2 Centre;
            public float Radius;
            public int DishIndex;
            public float OpenFor;
        }

        private readonly List<Dish> dishes = new List<Dish>();
        private readonly List<Box> boxes = new List<Box>();
        private readonly int[] order = new int[OrderCount];
        private readonly Dictionary<MinigamePlayer, int> teamOf = new Dictionary<MinigamePlayer, int>();
        private readonly Dictionary<MinigamePlayer, HoldToChoose> holds = new Dictionary<MinigamePlayer, HoldToChoose>();
        private readonly List<string> teamNames = new List<string>();
        private readonly List<Color> teamColours = new List<Color>();
        private bool[,] found;
        private OrderUpView view;

        public override string EnglishCommand => "FIND!";
        public override string ThaiCommand => "หา!";
        public override string EnglishTitle => "ORDER UP!";
        public override string ThaiTitle => "จำออเดอร์";
        public override string EnglishRule => "Stand on crates to peek inside. Find both dishes the customer wants first!";
        public override string ThaiRule => "ยืนบนกล่องเพื่อเปิดดู หาอาหาร 2 อย่างที่ลูกค้าสั่งให้ครบก่อน!";

        private float PlaySeconds => 10f / Mathf.Pow(Speed, 0.4f);

        private int TeamCount => teamNames.Count;

        protected override void Build()
        {
            view = Stage.LoadView<OrderUpView>("OrderUp", WallLayer, FloorLayer);
            dishes.AddRange(AllDishes());
            // Each crate holds a different dish while there are enough; more crates than dishes repeat.
            var pool = new List<int>();
            for (int i = 0; i < dishes.Count; i++) pool.Add(i);
            Shuffle(pool);
            var contents = new List<int>();
            for (int i = 0; i < view.CrateCount; i++) contents.Add(pool[i % pool.Count]);

            var picks = new List<int>(contents);
            Shuffle(picks);
            for (int i = 0; i < OrderCount; i++) order[i] = picks[i];

            MakeTeams();
            found = new bool[Mathf.Max(1, TeamCount), OrderCount];
            foreach (MinigamePlayer player in Players) holds[player] = new HoldToChoose(HoldSeconds);

            BuildBoxes(contents);
            BuildWall();
        }

        protected override void Step(float deltaTime)
        {
            if (Players.Count == 0)
            {
                Finish(false);
                return;
            }

            float left = PlaySeconds - Elapsed;
            view.SetClock(Mathf.CeilToInt(Mathf.Max(0f, left)).ToString());
            CloseBoxes(deltaTime);

            foreach (MinigamePlayer player in Players)
            {
                int standingOn = player.IsPresent ? BoxUnder(player.Position) : -1;
                if (!holds[player].Tick(standingOn, deltaTime)) continue;
                OpenBox(standingOn, player);
                if (IsDone) return;
            }

            if (left <= 0f) FinishWithoutWinner();
        }

        protected override void OnTimeUp()
        {
            FinishWithoutWinner();
        }

        // Odd player numbers against even ones when two or more players are here; one team otherwise.
        // If everyone here shares a parity (P1 and P3, say), they alternate in player order instead.
        private void MakeTeams()
        {
            var present = new List<MinigamePlayer>();
            foreach (MinigamePlayer player in Players)
                if (player.IsPresent) present.Add(player);

            if (present.Count < 2)
            {
                foreach (MinigamePlayer player in Players) teamOf[player] = 0;
                if (Players.Count > 0) AddTeam(Players[0].Name, Players[0].Colour);
                return;
            }

            bool mixed = false;
            foreach (MinigamePlayer player in present)
                if (player.PlayerId % 2 != present[0].PlayerId % 2) mixed = true;

            for (int i = 0; i < Players.Count; i++)
            {
                MinigamePlayer player = Players[i];
                teamOf[player] = mixed ? (player.PlayerId % 2 == 1 ? 0 : 1) : i % 2;
            }

            for (int team = 0; team < 2; team++)
            {
                var names = new List<string>();
                Color colour = MinigameArt.Cream;
                foreach (MinigamePlayer player in Players)
                {
                    if (teamOf[player] != team) continue;
                    if (names.Count == 0) colour = player.Colour;
                    names.Add(player.Name);
                }

                AddTeam(string.Join(" ", names), colour);
            }
        }

        private void AddTeam(string name, Color colour)
        {
            teamNames.Add(name);
            teamColours.Add(colour);
        }

        private void BuildBoxes(List<int> contents)
        {
            for (int i = 0; i < view.CrateCount; i++)
            {
                Dish dish = dishes[contents[i]];
                view.FillCrate(i, dish.Key, Pick(dish.English, dish.Thai));
                boxes.Add(new Box { Centre = view.CrateCentre(i), Radius = view.CrateRadius(i), DishIndex = contents[i] });
            }
        }

        private void BuildWall()
        {
            for (int i = 0; i < OrderCount; i++)
            {
                Dish dish = dishes[order[i]];
                view.ShowOrder(i, dish.Key, Pick(dish.English, dish.Thai), TeamCount, Mathf.Max(1, TeamCount));
            }

            string teams = TeamCount >= 2
                ? Pick($"{TeamLabel(0)}  vs  {TeamLabel(1)}", $"{TeamLabel(0)}  ปะทะ  {TeamLabel(1)}")
                : Pick("Find both before time runs out!", "หาให้ครบก่อนหมดเวลา!");
            view.SetTeams(teams);
            view.SetClock(string.Empty);
        }

        // A team's names in the team's colour.
        private string TeamLabel(int team)
        {
            return $"<color=#{ColorUtility.ToHtmlStringRGB(teamColours[team])}>{teamNames[team]}</color>";
        }

        private int BoxUnder(Vector2 position)
        {
            for (int i = 0; i < boxes.Count; i++)
                if (MinigameZones.DeepInside(position, boxes[i].Centre, boxes[i].Radius)) return i;
            return -1;
        }

        private void CloseBoxes(float deltaTime)
        {
            for (int i = 0; i < boxes.Count; i++)
            {
                Box box = boxes[i];
                if (box.OpenFor <= 0f) continue;
                box.OpenFor -= deltaTime;
                if (box.OpenFor > 0f) continue;
                view.SetCrateOpen(i, false);
            }
        }

        private void OpenBox(int index, MinigamePlayer opener)
        {
            Box box = boxes[index];
            box.OpenFor = OpenSeconds;
            view.SetCrateOpen(index, true);

            int team = teamOf[opener];
            for (int i = 0; i < OrderCount; i++)
            {
                if (order[i] != box.DishIndex || found[team, i]) continue;
                found[team, i] = true;
                view.MarkFound(team, i, teamColours[team]);
            }

            if (TeamFoundAll(team)) FinishWithWinner(team);
        }

        private bool TeamFoundAll(int team)
        {
            for (int i = 0; i < OrderCount; i++)
                if (!found[team, i]) return false;
            return true;
        }

        private void FinishWithWinner(int winner)
        {
            foreach (MinigamePlayer player in Players)
            {
                if (TeamCount < 2) SetVerdict(player, true, "Found both!", "เจอครบ!");
                else if (teamOf[player] == winner) SetVerdict(player, true, "Won!", "ชนะ!");
                else SetVerdict(player, false, "Lost", "แพ้");
            }

            Finish(true);
        }

        private void FinishWithoutWinner()
        {
            foreach (MinigamePlayer player in Players) SetVerdict(player, false, "Time up", "หมดเวลา");
            Finish(false);
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
