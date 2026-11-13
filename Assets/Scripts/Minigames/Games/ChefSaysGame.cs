using System.Collections.Generic;
using FoodIsekaiZ.Minigames.Views;
using UnityEngine;

namespace FoodIsekaiZ.Minigames.Games
{
    // Chef Says (design 5.1, after Shy Guy Says): the chef on the wall holds up LEFT or RIGHT five
    // times; each player steps that way from where they stand when the call appears (facing the wall),
    // and steps back to the middle during the short rest between calls. A player too close to the
    // called side's edge to step there is sent the other way instead, and their floor arrows say so.
    // At the end of each call: 0.35 m or more to that side is right, the other side is wrong, and in
    // between is too slow. Each player then sees their own right or wrong mark pop up where they stand
    // during the rest. A miss only costs that call. A player passes with at least PassHits right
    // calls; the game passes when at least half the team does. The look is the ChefSays prefab
    // (ChefSaysView): one big lit arrow per call with a countdown gauge, and moving floor chevrons that
    // start at each player's feet every call and never reach a neighbour. The HUD counts the calls in dots.
    public sealed class ChefSaysGame : Minigame
    {
        private const int Calls = 5;
        private const int PassHits = 3;
        private const float StepNeeded = 0.35f;
        private const float GapSeconds = 1.2f;
        // Furthest the chevrons run past the step line, in metres.
        private const float MaxReach = 1.1f;
        // Room left between two neighbours' chevrons, in metres.
        private const float NeighbourClearance = 0.05f;
        // Floor a player needs on the called side: room for the step plus UWB jitter, and for at least
        // the first chevron (0.73 m out, wave included) to show inside the map. With less, that player
        // is sent the other way, back into the arena.
        private const float EdgeRoom = 0.8f;

        private readonly Dictionary<MinigamePlayer, float> startX = new Dictionary<MinigamePlayer, float>();
        private readonly Dictionary<MinigamePlayer, bool> goRight = new Dictionary<MinigamePlayer, bool>();
        private readonly Dictionary<MinigamePlayer, int> hits = new Dictionary<MinigamePlayer, int>();
        private readonly List<bool> calls = new List<bool>();
        private readonly Dictionary<MinigamePlayer, ChefSaysFloorMark> marks = new Dictionary<MinigamePlayer, ChefSaysFloorMark>();
        private ChefSaysView view;
        private bool started;
        private int call;
        private float callTime;
        private bool resting;

        public override string EnglishCommand => "LEFT-RIGHT!";
        public override string ThaiCommand => "ซ้าย–ขวา!";
        public override string EnglishTitle => "CHEF SAYS";
        public override string ThaiTitle => "เชฟสั่ง!";
        public override string EnglishRule => "Step where the arrow points before the bar runs out, then back to the middle.";
        public override string ThaiRule => "ก้าว​ตาม​ลูกศร​ก่อน​หลอด​หมด แล้ว​กลับ​มา​ตรง​กลาง";

        // Time to make each step: 2.8 s on the first round, a little less on later, faster rounds.
        private float CallSeconds => 2.8f / Mathf.Sqrt(Speed);

        protected override void Build()
        {
            for (int i = 0; i < Calls; i++) calls.Add(Random.value < 0.5f);

            view = Stage.LoadView<ChefSaysView>("ChefSays", WallLayer, FloorLayer);
            view.SetWords(Pick("LEFT", "ซ้าย"), Pick("RIGHT", "ขวา"));
            Hud.ShowCounter(1, Calls);
            Hud.ShowPlayers(Players);
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

            view.SetBar(1f - callTime / CallSeconds);
            if (callTime < CallSeconds) return;
            JudgeCall();
            call++;
            if (call >= Calls)
            {
                FinishGame();
                return;
            }

            resting = true;
            callTime = 0f;
            view.Quiet();
        }

        protected override void OnTimeUp()
        {
            FinishGame();
        }

        // Gives every player their floor chevrons and shows the first call.
        private void StartCalls()
        {
            started = true;
            foreach (MinigamePlayer player in Players) marks[player] = view.AddMark(StepNeeded + MaxReach);
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
                view.PointMark(marks[player], player.Position, toRight, reach);
            }

            Hud.ShowCounter(call + 1, Calls);
        }

        // The called way, unless a player at x (arena metres) is too close to that side's edge to
        // step there; then the other way, back into the arena. Their floor arrows show which.
        private bool TowardsRoom(float x, bool calledRight)
        {
            float room = calledRight ? Stage.ArenaSize.x - x : x;
            return room >= EdgeRoom ? calledRight : !calledRight;
        }

        private void JudgeCall()
        {
            foreach (MinigamePlayer player in Players)
            {
                bool right = goRight[player];
                float moved = player.Position.x - startX[player];
                bool wentRight = moved >= StepNeeded;
                bool wentLeft = moved <= -StepNeeded;
                bool correct = right ? wentRight : wentLeft;
                if (correct) hits[player] = Hits(player) + 1;
                if (marks.TryGetValue(player, out ChefSaysFloorMark mark)) view.ShowResult(mark, player.Position, correct);
            }
        }

        private int Hits(MinigamePlayer player)
        {
            return hits.TryGetValue(player, out int count) ? count : 0;
        }

        private void FinishGame()
        {
            foreach (MinigamePlayer player in Players)
            {
                int count = Hits(player);
                bool passed = count >= PassHits;
                string score = $"{count}/{Calls}";
                if (count == Calls) SetVerdict(player, true, "Perfect!", "เป๊ะ!");
                else SetVerdict(player, passed, score, score);
                Hud.SetPlayer(player, passed ? PlayerMark.Safe : PlayerMark.Out);
            }

            Finish(HalfTeam(SuccessCount(), Players.Count));
        }
    }
}
