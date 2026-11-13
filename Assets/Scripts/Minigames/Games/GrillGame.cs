using System.Collections.Generic;
using FoodIsekaiZ.Minigames.Views;
using UnityEngine;

namespace FoodIsekaiZ.Minigames.Games
{
    // Perfect Grill (design 4.2, after Well-Done): each player gets an order of RARE, MEDIUM or
    // WELL DONE. Standing on the grill strip on the floor adds to their steak's grill time; they step
    // off when the time bar on the wall reaches the green window. Judged when they step off after
    // more than 0.3 s on the grill: inside the window is perfect, short of it is undercooked, and
    // passing the top of the window while still on the grill burns it at once.
    // Passes when everyone is perfect.
    // The look is the Grill prefab (GrillView, one GrillSteakColumn per player).
    public sealed class GrillGame : Minigame
    {
        private static readonly Rect GrillArea = new Rect(3.25f, 3.0f, 4.5f, 1.93f);
        // Grill time needed before stepping off counts as a finished steak (shorter is tag jitter).
        private const float CountsAfter = 0.3f;

        private static readonly float[] TargetSeconds = { 1.5f, 2.7f, 3.9f };
        private static readonly string[] EnglishOrders = { "RARE", "MEDIUM", "WELL DONE" };
        private static readonly string[] ThaiOrders = { "แรร์", "มีเดียม", "เวลดัน" };

        private sealed class Steak
        {
            public float Target;
            public float Seconds;
            public bool OnGrill;
            public bool Judged;
            public GrillSteakColumn Column;
        }

        private readonly Dictionary<MinigamePlayer, Steak> steaks = new Dictionary<MinigamePlayer, Steak>();
        private GrillView view;

        public override string EnglishCommand => "GRILL!";
        public override string ThaiCommand => "ย่าง!";
        public override string EnglishTitle => "PERFECT GRILL";
        public override string ThaiTitle => "ย่างพอดี";
        public override string EnglishRule => "Stand on the grill, step off when YOUR bar is in the green.";
        public override string ThaiRule => "ยืน​บน​เตา​ย่าง แล้ว​ก้าว​ออก​ตอน​แถบ​ของ​คุณ​อยู่​ใน​ช่วง​เขียว";

        private float Tolerance => 0.48f / Mathf.Pow(Speed, 0.3f);

        private float PlaySeconds => 3.9f / Mathf.Sqrt(Speed) + 3.4f / Speed;

        // The longest grill time the bars show: a little past the top of the well-done window.
        private float BarSeconds => (TargetSeconds[2] / Mathf.Sqrt(Speed) + Tolerance) * 1.15f;

        protected override void Build()
        {
            view = Stage.LoadView<GrillView>("Grill", WallLayer, FloorLayer);
            view.PlaceGrill(GrillArea.center);
            view.SetStatus(Pick("Onto the grill!", "ขึ้น​เตา​ย่าง​เลย!"));

            int firstOrder = Random.Range(0, TargetSeconds.Length);
            float spacing = Mathf.Min(260f, 900f / Mathf.Max(1, Players.Count));
            float barWidth = Mathf.Min(210f, spacing - 30f);
            for (int i = 0; i < Players.Count; i++)
            {
                int order = (firstOrder + i) % TargetSeconds.Length;
                float x = (i - (Players.Count - 1) * 0.5f) * spacing;
                steaks[Players[i]] = BuildSteak(Players[i], order, x, barWidth);
            }
        }

        protected override void Step(float deltaTime)
        {
            if (Elapsed >= PlaySeconds)
            {
                JudgeEveryoneLeft();
                FinishGame();
                return;
            }

            bool waiting = false;
            foreach (MinigamePlayer player in Players)
            {
                Steak steak = steaks[player];
                if (steak.Judged) continue;
                UpdateSteak(player, steak, deltaTime);
                if (!steak.Judged) waiting = true;
            }

            if (waiting) return;
            FinishGame();
        }

        protected override void OnTimeUp()
        {
            JudgeEveryoneLeft();
            FinishGame();
        }

        // One player's column on the wall: order, steak ringed in their colour, time bar with its green
        // window, grill seconds, and the verdict once judged.
        private Steak BuildSteak(MinigamePlayer player, int order, float x, float barWidth)
        {
            var steak = new Steak { Target = TargetSeconds[order] / Mathf.Sqrt(Speed) };
            string orderName = Pick(EnglishOrders[order], ThaiOrders[order]);
            float lower = (steak.Target - Tolerance) / BarSeconds;
            float upper = (steak.Target + Tolerance) / BarSeconds;
            steak.Column = view.AddSteak(player, orderName, x, barWidth, lower, upper);
            return steak;
        }

        private void UpdateSteak(MinigamePlayer player, Steak steak, float deltaTime)
        {
            steak.OnGrill = player.IsPresent && MinigameZones.InRect(player.Position, GrillArea, steak.OnGrill);
            if (steak.OnGrill) steak.Seconds += deltaTime;
            ShowSteak(steak);

            if (steak.OnGrill)
            {
                if (steak.Seconds > steak.Target + Tolerance) Judge(player, steak);
                return;
            }

            if (steak.Seconds > CountsAfter) Judge(player, steak);
        }

        private void ShowSteak(Steak steak)
        {
            steak.Column.ShowProgress(steak.Seconds / BarSeconds, steak.Seconds, Doneness(steak));
        }

        private void Judge(MinigamePlayer player, Steak steak)
        {
            steak.Judged = true;
            ShowSteak(steak);
            if (steak.Seconds <= CountsAfter)
                Verdict(player, steak, false, "Never grilled", "ไม่​ได้​ย่าง");
            else if (steak.Seconds < steak.Target - Tolerance)
                Verdict(player, steak, false, "Undercooked", "ยัง​ไม่​สุก");
            else if (steak.Seconds <= steak.Target + Tolerance)
                Verdict(player, steak, true, "Perfect!", "พอดี!");
            else
                Verdict(player, steak, false, "Burnt", "ไหม้");
        }

        private void Verdict(MinigamePlayer player, Steak steak, bool success, string english, string thai)
        {
            SetVerdict(player, success, english, thai);
            steak.Column.ShowResult(Pick(english, thai), success);
        }

        // Play time is over: steaks still on the grill are judged as they are now.
        private void JudgeEveryoneLeft()
        {
            foreach (MinigamePlayer player in Players)
            {
                Steak steak = steaks[player];
                if (!steak.Judged) Judge(player, steak);
            }
        }

        private void FinishGame()
        {
            bool passed = Players.Count > 0 && SuccessCount() == Players.Count;
            view.SetStatus(passed ? Pick("All perfect!", "พอดี​ทุก​ชิ้น!") : Pick("Grill's off!", "ปิด​เตา​แล้ว!"));
            Finish(passed);
        }

        // 0 -> 1 browning from raw toward the top of the window, then 1 -> 2 burning past it.
        private float Doneness(Steak steak)
        {
            float upper = steak.Target + Tolerance;
            if (steak.Seconds <= upper) return steak.Seconds / upper;
            return 1f + Mathf.Clamp01((steak.Seconds - upper) / Mathf.Max(0.1f, Tolerance * 2f));
        }
    }
}
