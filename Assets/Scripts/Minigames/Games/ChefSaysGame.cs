using System.Collections.Generic;
using FoodIsekaiZ.Minigames.Views;
using UnityEngine;

namespace FoodIsekaiZ.Minigames.Games
{
    // Chef Says (design 5.1, after Shy Guy Says): the chef pops up on the left or right five
    // times; each player steps that way from where they stand when the call appears (facing the wall),
    // and steps back to the middle during the short rest between calls. A player too close to the
    // called side's edge to step there is sent the other way instead, and their floor arrows say so.
    // A faint fantasy ingredient waits at the end of each player's arrows (never nearer than 0.35 m);
    // reaching it during the call collects it and makes the call right, anything else is wrong.
    // Collected ingredients fly into the basket at the bottom middle of the wall. Each player then sees their own right or wrong mark pop up where they stand
    // during the rest. A miss only costs that call. A player passes with at least PassHits right
    // calls; the game passes when at least half the team does. The look is the ChefSays prefab
    // (ChefSaysView): one wolfgirl chef per call with a countdown gauge, and moving floor chevrons that
    // start at each player's feet every call and never reach a neighbour. The HUD counts the calls in dots.
    public sealed class ChefSaysGame : Minigame
    {
        private const int Calls = 5;
        private const int PassHits = 3;
        private const float StepNeeded = 0.35f;
        private const float GapSeconds = 1.2f;
        // Wait after the last call, so the chef finishes ducking away and the last right or wrong
        // marks are seen before the result card.
        private const float OutroSeconds = 1.5f;
        // Furthest the chevrons run past the step line, in metres.
        private const float MaxReach = 1.4f;
        // Room left between two neighbours' chevrons, in metres.
        private const float NeighbourClearance = 0.05f;
        // Floor a player needs on the called side: room for at least two chevrons and the ingredient
        // after them (about 1.75 m) plus UWB jitter. With less, that player is sent the other way,
        // back into the arena.
        private const float EdgeRoom = 1.8f;
        // The ingredient at the end of the chevrons: half its width, in metres, and how close (side to
        // side) a player must come to its centre to collect it. It never sits nearer than StepNeeded.
        private const float IngredientHalf = 0.36f;
        private const float CollectRadius = 0.25f;

        private readonly Dictionary<MinigamePlayer, float> startX = new Dictionary<MinigamePlayer, float>();
        private readonly Dictionary<MinigamePlayer, bool> goRight = new Dictionary<MinigamePlayer, bool>();
        private readonly Dictionary<MinigamePlayer, int> hits = new Dictionary<MinigamePlayer, int>();
        private readonly List<bool> calls = new List<bool>();
        private readonly Dictionary<MinigamePlayer, ChefSaysFloorMark> marks = new Dictionary<MinigamePlayer, ChefSaysFloorMark>();
        private readonly Dictionary<MinigamePlayer, float> ingredientAt = new Dictionary<MinigamePlayer, float>();
        private readonly Dictionary<MinigamePlayer, int> ingredientOf = new Dictionary<MinigamePlayer, int>();
        private readonly HashSet<MinigamePlayer> collected = new HashSet<MinigamePlayer>();
        private ChefSaysView view;
        private bool started;
        private int call;
        private float callTime;
        private bool resting;

        public override string EnglishCommand => "LEFT-RIGHT!";
        public override string ThaiCommand => "ซ้าย–ขวา!";
        public override string EnglishTitle => "CHEF SAYS";
        public override string ThaiTitle => "เชฟสั่ง!";
        public override string EnglishRule => "Step towards the chef and grab the ingredient at the end of your floor arrows before the bar runs out, then back to the middle.";
        public override string ThaiRule => "ก้าว​ไป​ฝั่ง​ที่​เชฟ​โผล่​ เก็บ​วัตถุดิบ​ที่​ปลาย​ลูกศร​ก่อน​หลอด​หมด​ แล้ว​กลับ​มา​ตรง​กลาง";
        // The chef pops up straight out of the tavern scene, so the wall is not darkened.
        public override bool ShadesWall => false;

        // Time to make each step: as long as the chef takes to pop out and look both ways (about
        // 6 s), the same on every round, so the call never ends before her animation does.
        private float CallSeconds => view.CallSeconds;

        protected override void Build()
        {
            for (int i = 0; i < Calls; i++) calls.Add(Random.value < 0.5f);

            view = Stage.LoadView<ChefSaysView>("ChefSays", WallLayer, FloorLayer);
            // Only the call counter: no player badges while playing, the result card sums everyone up.
            Hud.ShowCounter(1, Calls);
        }

        protected override void Step(float deltaTime)
        {
            if (!started) StartCalls();

            callTime += deltaTime;
            if (resting)
            {
                if (callTime < GapSeconds) return;
                resting = false;
                callTime = 0f;
                ShowCall();
                return;
            }

            if (call >= Calls)
            {
                // After the last call: let the chef duck away and the last marks show before the end.
                if (callTime >= OutroSeconds) FinishGame();
                return;
            }

            view.SetBar(1f - callTime / CallSeconds);
            CollectReached();
            if (callTime < CallSeconds) return;
            JudgeCall();
            call++;
            callTime = 0f;
            view.Quiet();
            if (call < Calls) resting = true;
        }

        protected override void OnTimeUp()
        {
            FinishGame();
        }

        // Gives every player their floor chevrons and shows the first call.
        private void StartCalls()
        {
            started = true;
            foreach (MinigamePlayer player in Players) marks[player] = view.AddMark(StepNeeded + MaxReach, player.Colour);
            ShowCall();
        }

        // Side-to-side room to the closest player in the same row (within 1 m front to back).
        private float NearestNeighbourGap(MinigamePlayer player)
        {
            float gap = float.MaxValue;
            foreach (MinigamePlayer other in Players)
            {
                if (other == player || Mathf.Abs(other.Position.y - player.Position.y) > 1f) continue;
                gap = Mathf.Min(gap, Mathf.Abs(startX[other] - startX[player]));
            }

            return gap;
        }

        private void ShowCall()
        {
            bool right = calls[call];
            view.ShowCall(right);
            // Each call measures from, and draws its chevrons at, where everyone stands right now.
            foreach (MinigamePlayer player in Players) startX[player] = player.Position.x;
            foreach (MinigamePlayer player in Players)
            {
                bool toRight = TowardsRoom(player.Position.x, right);
                goRight[player] = toRight;
                float edge = toRight ? Stage.ArenaSize.x - player.Position.x : player.Position.x;
                float reach = Mathf.Min(NearestNeighbourGap(player) * 0.5f - NeighbourClearance, StepNeeded + MaxReach, edge);
                // The chevrons leave room for the ingredient, which sits right at the tip of the last one;
                // at least one chevron always shows, so a crowded player still sees which way to go.
                float arrowTip = view.PointMark(marks[player], player.Position, toRight, reach - IngredientHalf);
                float centre = Mathf.Max(arrowTip + IngredientHalf, StepNeeded);
                ingredientAt[player] = centre;
                ingredientOf[player] = view.IngredientCount > 0 ? Random.Range(0, view.IngredientCount) : -1;
                view.ShowIngredient(marks[player], ingredientOf[player], toRight, centre);
            }

            collected.Clear();

            Hud.ShowCounter(call + 1, Calls);
        }

        // The called way, unless a player at x (arena metres) is too close to that side's edge to
        // step there; then the other way, back into the arena. Their floor arrows show which.
        private bool TowardsRoom(float x, bool calledRight)
        {
            float room = calledRight ? Stage.ArenaSize.x - x : x;
            return room >= EdgeRoom ? calledRight : !calledRight;
        }

        // A player collects their ingredient the moment they come within CollectRadius of it side to
        // side, or step past it; once collected it counts even if they drift back before the end.
        private void CollectReached()
        {
            foreach (MinigamePlayer player in Players)
            {
                if (collected.Contains(player) || !ingredientAt.TryGetValue(player, out float centre)) continue;
                float moved = player.Position.x - startX[player];
                float towards = goRight[player] ? moved : -moved;
                if (towards < Mathf.Max(centre - CollectRadius, StepNeeded)) continue;
                collected.Add(player);
                if (marks.TryGetValue(player, out ChefSaysFloorMark mark)) view.CollectIngredient(mark);
            }
        }

        // Right means the player reached their ingredient during the call; it then flies into the
        // wall basket from that player's side.
        private void JudgeCall()
        {
            CollectReached();
            foreach (MinigamePlayer player in Players)
            {
                bool correct = collected.Contains(player);
                if (correct)
                {
                    hits[player] = Hits(player) + 1;
                    view.SendToBasket(ingredientOf[player], player.Position.x > Stage.ArenaSize.x * 0.5f);
                }

                if (marks.TryGetValue(player, out ChefSaysFloorMark mark)) view.ShowResult(mark, player.Position, correct);
            }
        }

        private int Hits(MinigamePlayer player)
        {
            return hits.TryGetValue(player, out int count) ? count : 0;
        }

        private void FinishGame()
        {
            view.Quiet();
            foreach (MinigamePlayer player in Players)
            {
                int count = Hits(player);
                bool passed = count >= PassHits;
                string score = $"{count}/{Calls}";
                if (count == Calls) SetVerdict(player, true, "Perfect!", "เป๊ะ!");
                else SetVerdict(player, passed, score, score);
            }

            Finish(HalfTeam(SuccessCount(), Players.Count));
        }
    }
}
