using System.Collections.Generic;
using FoodIsekaiZ.Minigames.Views;
using UnityEngine;

namespace FoodIsekaiZ.Minigames.Games
{
    // Hot Plates (design 5.4, after Hexagon Heat): four big plates, one per corner of the floor and
    // each its own colour. The chef calls a colour five times, each call a little faster; when the
    // call ends every other plate turns red-hot. Safe means standing well inside a plate of the
    // called colour (within 85% of its radius). Not safe is out for the rest of the game. Passes when
    // at least half the team survives all five calls.
    // The plates, their colours and where they sit come from the HotPlates prefab (HotPlatesView);
    // this class runs the calls.
    public sealed class HotPlatesGame : Minigame
    {
        private const int Calls = 5;
        private const float HotSeconds = 1f;
        private const float CallShrink = 0.86f;
        // Green, yellow, blue and purple; the view holds the actual colours in this order.
        private static readonly string[] EnglishColours = { "GREEN!", "YELLOW!", "BLUE!", "PURPLE!" };
        private static readonly string[] ThaiColours = { "เขียว!", "เหลือง!", "ฟ้า!", "ม่วง!" };

        private readonly HashSet<MinigamePlayer> knockedOut = new HashSet<MinigamePlayer>();
        private readonly int[] calledColours = new int[Calls];
        private int call;
        private float callTime;
        private bool hot;
        private HotPlatesView view;

        public override string EnglishCommand => "PLATES!";
        public override string ThaiCommand => "จาน!";
        public override string EnglishTitle => "HOT PLATES";
        public override string ThaiTitle => "ยืนให้ถูกจาน!";
        public override string EnglishRule => "Stand on the plate the chef calls. Other plates turn hot!";
        public override string ThaiRule => "ไปยืนบนจานสีที่เชฟเรียก จานสีอื่นจะร้อน!";

        private float CallSeconds => 2.3f / Mathf.Pow(Speed, 0.4f) * Mathf.Pow(CallShrink, call);

        protected override void Build()
        {
            view = Stage.LoadView<HotPlatesView>("HotPlates", WallLayer, FloorLayer);
            PickCalls();
            ShowCall();
        }

        protected override void Step(float deltaTime)
        {
            if (Players.Count == 0)
            {
                Finish(false);
                return;
            }

            callTime += deltaTime;
            if (!hot)
            {
                view.SetTimeLeft(1f - callTime / CallSeconds);
                if (callTime < CallSeconds) return;
                JudgeCall();
                StartHot();
                return;
            }

            if (callTime < HotSeconds) return;
            call++;
            if (call >= Calls || knockedOut.Count >= Players.Count)
            {
                FinishGame();
                return;
            }

            ShowCall();
        }

        protected override void OnTimeUp()
        {
            FinishGame();
        }

        // Five calls from the colours the plates actually have, never the same twice in a row, so
        // everyone has to move.
        private void PickCalls()
        {
            var colours = new List<int>();
            for (int i = 0; i < view.PlateCount; i++)
            {
                int colour = view.PlateColour(i);
                if (!colours.Contains(colour) && colour >= 0 && colour < EnglishColours.Length) colours.Add(colour);
            }

            if (colours.Count == 0) colours.Add(0);
            for (int i = 0; i < Calls; i++)
            {
                int colour = colours[Random.Range(0, colours.Count)];
                if (i > 0 && colours.Count > 1)
                    while (colour == calledColours[i - 1]) colour = colours[Random.Range(0, colours.Count)];
                calledColours[i] = colour;
            }
        }

        private void ShowCall()
        {
            hot = false;
            callTime = 0f;
            int colour = calledColours[call];
            view.ShowCall(colour, Pick(EnglishColours[colour], ThaiColours[colour]), call + 1, Calls);
        }

        private void StartHot()
        {
            hot = true;
            callTime = 0f;
            view.HeatAllBut(calledColours[call], Pick("HOT!", "ร้อน!"));
        }

        private void JudgeCall()
        {
            int colour = calledColours[call];
            foreach (MinigamePlayer player in Players)
            {
                if (knockedOut.Contains(player)) continue;
                if (player.IsPresent && IsSafe(player.Position, colour)) continue;
                knockedOut.Add(player);
                SetVerdict(player, false, $"Burnt on call {call + 1}", $"โดนกระทะร้อน ครั้งที่ {call + 1}");
            }
        }

        // Well inside any plate of the called colour.
        private bool IsSafe(Vector2 position, int colour)
        {
            for (int i = 0; i < view.PlateCount; i++)
            {
                if (view.PlateColour(i) != colour) continue;
                if (MinigameZones.DeepInside(position, view.PlateCentre(i), view.PlateRadius(i))) return true;
            }

            return false;
        }

        private void FinishGame()
        {
            foreach (MinigamePlayer player in Players)
                if (!knockedOut.Contains(player)) SetVerdict(player, true, "Survived!", "รอด!");
            Finish(HalfTeam(Players.Count - knockedOut.Count, Players.Count));
        }
    }
}
