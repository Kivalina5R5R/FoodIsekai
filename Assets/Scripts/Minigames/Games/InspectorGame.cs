using System.Collections.Generic;
using FoodIsekaiZ.Minigames.Views;
using UnityEngine;

namespace FoodIsekaiZ.Minigames.Games
{
    // Health Inspector! (design 4.3, after Stony Look, played like red light / green light).
    // First everyone lines up on the start strip on the far side of the floor; play begins once all
    // present players have stood in it together for a moment (or when the gathering time runs out).
    // Then they walk to the counter strip by the wall, facing the wall, so they can watch the
    // inspector in the middle of it. He hums with his back turned, half-turns with a warning, then
    // stares. Anyone who moves more than FreezeTolerance away from where they stood when the stare's
    // grace ended is caught; measuring distance rather than speed leaves room for the UWB tags'
    // jitter. Reaching the counter is safe; anyone not there when play time runs out is too late.
    // Passes when everyone reaches the counter.
    // The look and both strips come from the Inspector prefab (InspectorView); each player's result
    // shows on the HUD's player chips, and caught players get a red mark on the floor.
    public sealed class InspectorGame : Minigame
    {
        // Longest wait for everyone to line up before play starts anyway.
        private const float GatherSeconds = 6f;
        // How long everyone must stand in the start strip together.
        private const float ReadySeconds = 0.5f;
        // The strips count a little beyond their drawn edge, for tag jitter.
        private const float ZoneTolerance = 0.2f;
        // The stare forgives this long, so a player who was mid-step when he turned can stop.
        private const float StareGrace = 0.25f;
        // How far a frozen player may drift (tag jitter, swaying) before it counts as moving.
        private const float FreezeTolerance = 0.5f;

        private enum Look
        {
            Back,
            Warning,
            Stare,
        }

        private sealed class Walker
        {
            public bool Done;
            public bool InStart;
            public bool HasAnchor;
            public Vector2 Anchor;
        }

        private readonly Dictionary<MinigamePlayer, Walker> walkers = new Dictionary<MinigamePlayer, Walker>();
        private bool walking;
        private float readyTime;
        private float walkTime;
        private Look look;
        private float lookTime;
        private float lookSeconds;
        private InspectorView view;

        public override string EnglishCommand => "FREEZE!";
        public override string ThaiCommand => "นิ่ง!";
        public override string EnglishTitle => "HEALTH INSPECTOR!";
        public override string ThaiTitle => "ผู้ตรวจ​สุขอนามัย​มา!";
        public override string EnglishRule => "Line up at the start, walk to the counter while he looks away. Freeze when he stares!";
        public override string ThaiRule => "ยืน​รวม​ที่​จุด​เริ่ม แล้ว​เดิน​ไป​เคาน์เตอร์​ตอน​เขา​หัน​หลัง พอ​เขา​หัน​มา​ต้อง​นิ่ง!";

        private float PlaySeconds => 9f / Mathf.Pow(Speed, 0.55f);

        protected override void Build()
        {
            view = Stage.LoadView<InspectorView>("Inspector", WallLayer, FloorLayer);
            view.SetZoneWords(Pick("START", "จุด​เริ่ม"), Pick("COUNTER", "เคาน์เตอร์"));
            foreach (MinigamePlayer player in Players) walkers[player] = new Walker();
            Hud.ShowPlayers(Players);
            view.ShowStart(true);
            view.ShowBack(Pick("Everyone to the start!", "ทุก​คน​ไป​ยืน​ที่​จุด​เริ่ม!"));
        }

        protected override void Step(float deltaTime)
        {
            if (Players.Count == 0)
            {
                Finish(false);
                return;
            }

            if (!walking)
            {
                Gather(deltaTime);
                return;
            }

            walkTime += deltaTime;
            if (walkTime >= PlaySeconds)
            {
                JudgeEveryoneLeft();
                FinishGame();
                return;
            }

            view.SetClock($"{PlaySeconds - walkTime:0.0} s");
            AdvanceLook(deltaTime);

            bool waiting = false;
            foreach (MinigamePlayer player in Players)
            {
                Walker walker = walkers[player];
                if (walker.Done) continue;
                CheckWalker(player, walker);
                if (!walker.Done) waiting = true;
            }

            if (waiting) return;
            FinishGame();
        }

        protected override void OnTimeUp()
        {
            JudgeEveryoneLeft();
            FinishGame();
        }

        // Waits until every present player has stood in the start strip together for ReadySeconds,
        // or until GatherSeconds have gone by, then starts the walk.
        private void Gather(float deltaTime)
        {
            Rect start = Grown(view.StartArea);
            bool everyoneIn = true;
            int present = 0;
            foreach (MinigamePlayer player in Players)
            {
                if (!player.IsPresent) continue;
                present++;
                Walker walker = walkers[player];
                walker.InStart = MinigameZones.InRect(player.Position, start, walker.InStart);
                everyoneIn &= walker.InStart;
            }

            readyTime = everyoneIn && present > 0 ? readyTime + deltaTime : 0f;
            if (readyTime < ReadySeconds && Elapsed < GatherSeconds) return;
            StartWalk();
        }

        private void StartWalk()
        {
            walking = true;
            view.ShowStart(false);
            view.ShowCounter(true);
            TurnAway();
        }

        private void CheckWalker(MinigamePlayer player, Walker walker)
        {
            if (!player.IsPresent) return;

            if (MinigameZones.InRect(player.Position, Grown(view.CounterArea), false))
            {
                Verdict(player, walker, true, "Made it!", "ถึง​แล้ว!");
                return;
            }

            if (look != Look.Stare || lookTime <= StareGrace) return;
            // Where they stood when the grace ended is where they must stay.
            if (!walker.HasAnchor)
            {
                walker.HasAnchor = true;
                walker.Anchor = player.Position;
                return;
            }

            if (Vector2.Distance(player.Position, walker.Anchor) <= FreezeTolerance) return;
            Verdict(player, walker, false, "Caught moving!", "โดน​จับ​ได้!");
            view.MarkCaught(player);
        }

        private void AdvanceLook(float deltaTime)
        {
            lookTime += deltaTime;
            if (lookTime < lookSeconds) return;

            if (look == Look.Back) Warn();
            else if (look == Look.Warning) Stare();
            else TurnAway();
        }

        private void TurnAway()
        {
            SetLook(Look.Back, Random.Range(1.0f, 2.1f) / Mathf.Sqrt(Speed));
            view.ShowBack(Pick("~ humming ~", "~ ฮัม​เพลง ~"));
            foreach (Walker walker in walkers.Values) walker.HasAnchor = false;
        }

        private void Warn()
        {
            SetLook(Look.Warning, 0.5f / Mathf.Pow(Speed, 0.3f));
            view.ShowWarning("!?");
        }

        private void Stare()
        {
            SetLook(Look.Stare, Random.Range(1.1f, 1.7f));
            view.ShowStare(Pick("FREEZE!", "นิ่ง!"));
        }

        private void SetLook(Look next, float seconds)
        {
            look = next;
            lookTime = 0f;
            lookSeconds = seconds;
        }

        private void Verdict(MinigamePlayer player, Walker walker, bool success, string english, string thai)
        {
            walker.Done = true;
            SetVerdict(player, success, english, thai);
            Hud.SetPlayer(player, success ? PlayerMark.Safe : PlayerMark.Out);
        }

        private void JudgeEveryoneLeft()
        {
            foreach (MinigamePlayer player in Players)
            {
                Walker walker = walkers[player];
                if (!walker.Done) Verdict(player, walker, false, "Too late", "ไม่​ทัน");
            }
        }

        private void FinishGame()
        {
            view.HideStare();
            view.SetClock(string.Empty);
            Finish(Players.Count > 0 && SuccessCount() == Players.Count);
        }

        private static Rect Grown(Rect area)
        {
            return Rect.MinMaxRect(area.xMin - ZoneTolerance, area.yMin - ZoneTolerance, area.xMax + ZoneTolerance, area.yMax + ZoneTolerance);
        }
    }
}
