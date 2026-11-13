using System.Collections.Generic;
using FoodIsekaiZ.Minigames.Views;
using UnityEngine;

namespace FoodIsekaiZ.Minigames.Games
{
    // Pressure Pot Roulette (design 5.2, after Bowser's Big Blast): five valves ring the pot on the
    // floor and one of them, hidden from everyone, blows the pot. Players take turns in player order
    // (absent players are skipped): hold still on a valve to open it, or a random one opens for them
    // when the turn runs out. Each valve opens once. Whoever opens the bad one fails and the game
    // ends; everyone else is safe. Passes when nobody exploded. At most five turns are played.
    // The look is the PressureValve prefab (PressureValveView); the valve and pot positions here are
    // the play zones, and the view draws them where these say.
    public sealed class PressureValveGame : Minigame
    {
        private const int ValveCount = 5;
        private const float ValveRadius = 0.5f;
        private const float HoldSeconds = 0.45f;
        private const float RevealSeconds = 0.6f;
        private const float BoomSeconds = 1.2f;
        // Turns must fit the 20 s cap at speed 1, with room for the pauses between them.
        private const float TurnBudgetSeconds = 18.5f;

        private static readonly Vector2 PotCentre = new Vector2(5.5f, 2.6f);
        private static readonly Vector2 ValveSpread = new Vector2(2.3f, 1.75f);

        private enum Phase
        {
            Turn,
            Reveal,
            Boom,
        }

        private readonly Vector2[] valveCentres = new Vector2[ValveCount];
        private readonly PressureValveMark[] valves = new PressureValveMark[ValveCount];
        private readonly bool[] opened = new bool[ValveCount];
        private readonly bool[] standingInside = new bool[ValveCount];
        private readonly HoldToChoose hold = new HoldToChoose(HoldSeconds);
        private readonly HashSet<MinigamePlayer> skipped = new HashSet<MinigamePlayer>();
        private int bombValve;
        private int openedCount;
        private int turnsPlayed;
        private int turnLimit;
        private int nextPlayerIndex;
        private MinigamePlayer current;
        private MinigamePlayer exploded;
        private Phase phase;
        private float phaseTime;
        private float turnSeconds;
        private PressureValveView view;

        public override string EnglishCommand => "PICK!";
        public override string ThaiCommand => "เลือก!";
        public override string EnglishTitle => "PRESSURE POT ROULETTE";
        public override string ThaiTitle => "หม้อแรงดันวัดดวง";
        public override string EnglishRule => "On your turn, stand on a valve to open it. One valve blows the pot!";
        public override string ThaiRule => "ถึงตาใครให้ไปยืนบนวาล์วเพื่อเปิด มีวาล์วเดียวที่ทำหม้อระเบิด!";

        protected override void Build()
        {
            bombValve = Random.Range(0, ValveCount);
            turnLimit = Mathf.Min(ValveCount, Mathf.Max(1, Players.Count));
            float spacedTurn = TurnBudgetSeconds / turnLimit - RevealSeconds;
            turnSeconds = Mathf.Min(4f / Mathf.Pow(Speed, 0.3f), spacedTurn);

            view = Stage.LoadView<PressureValveView>("PressureValve", WallLayer, FloorLayer);
            view.PlacePot(PotCentre);
            for (int i = 0; i < ValveCount; i++)
            {
                // Evenly round the ellipse, starting straight away from the wall.
                float angle = Mathf.PI * 0.5f + i * Mathf.PI * 2f / ValveCount;
                valveCentres[i] = PotCentre + new Vector2(Mathf.Cos(angle) * ValveSpread.x, Mathf.Sin(angle) * ValveSpread.y);
                valves[i] = view.AddValve(valveCentres[i], i + 1);
            }
        }

        protected override void Step(float deltaTime)
        {
            if (Players.Count == 0)
            {
                Finish(false);
                return;
            }

            phaseTime += deltaTime;
            if (phase == Phase.Boom)
            {
                if (phaseTime >= BoomSeconds) FinishGame();
                return;
            }

            if (phase == Phase.Reveal)
            {
                if (phaseTime < RevealSeconds) return;
                StartNextTurn();
                return;
            }

            if (current == null)
            {
                StartNextTurn();
                return;
            }

            PlayTurn(deltaTime);
        }

        protected override void OnTimeUp()
        {
            FinishGame();
        }

        // Picks the next present player in order; ends the game when the turns run out.
        private void StartNextTurn()
        {
            current = null;
            hold.Reset();
            if (turnsPlayed >= turnLimit || openedCount >= ValveCount)
            {
                FinishGame();
                return;
            }

            while (nextPlayerIndex < Players.Count)
            {
                MinigamePlayer candidate = Players[nextPlayerIndex];
                nextPlayerIndex++;
                if (candidate.IsPresent)
                {
                    current = candidate;
                    break;
                }

                skipped.Add(candidate);
            }

            if (current == null)
            {
                FinishGame();
                return;
            }

            phase = Phase.Turn;
            phaseTime = 0f;
            for (int i = 0; i < ValveCount; i++) standingInside[i] = false;
            view.ShowTurn(Pick($"{current.Name}'s turn — pick a valve", $"ตา {current.Name} — เลือกวาล์ว"), current.Colour);
        }

        private void PlayTurn(float deltaTime)
        {
            float left = turnSeconds - phaseTime;
            view.ShowClock(Mathf.CeilToInt(Mathf.Max(0f, left)).ToString());

            int standingOn = ValveUnder(current);
            TintValves(standingOn);
            if (hold.Tick(standingOn, deltaTime))
            {
                OpenValve(standingOn);
                return;
            }

            if (left > 0f) return;
            OpenValve(RandomClosedValve());
        }

        // The unopened valve the player stands in, or -1.
        private int ValveUnder(MinigamePlayer player)
        {
            int found = -1;
            for (int i = 0; i < ValveCount; i++)
            {
                bool inside = !opened[i] && player.IsPresent &&
                    MinigameZones.InCircle(player.Position, valveCentres[i], ValveRadius, standingInside[i]);
                standingInside[i] = inside;
                if (inside && found < 0) found = i;
            }

            return found;
        }

        private void TintValves(int standingOn)
        {
            for (int i = 0; i < ValveCount; i++)
            {
                if (opened[i]) continue;
                if (i == standingOn) valves[i].ShowHeld(current.Colour, hold.Progress);
                else valves[i].ShowIdle();
            }
        }

        private int RandomClosedValve()
        {
            var closed = new List<int>();
            for (int i = 0; i < ValveCount; i++)
                if (!opened[i]) closed.Add(i);
            return closed[Random.Range(0, closed.Count)];
        }

        private void OpenValve(int valve)
        {
            opened[valve] = true;
            openedCount++;
            turnsPlayed++;
            view.SetPressure((float)openedCount / ValveCount);

            if (valve == bombValve)
            {
                exploded = current;
                valves[valve].ShowBoom();
                view.ShowBoom(Pick($"BOOM! {current.Name}", $"ระเบิด! {current.Name}"));
                SetVerdict(current, false, "BOOM!", "ระเบิด!");
                phase = Phase.Boom;
                phaseTime = 0f;
                return;
            }

            valves[valve].ShowOpened();
            view.ShowSafe(Pick($"Phew! {current.Name} is safe", $"รอด! {current.Name} ปลอดภัย"));
            SetVerdict(current, true, "Safe", "รอด");
            phase = Phase.Reveal;
            phaseTime = 0f;
        }

        private void FinishGame()
        {
            foreach (MinigamePlayer player in Players)
            {
                if (HasVerdict(player)) continue;
                if (skipped.Contains(player)) SetVerdict(player, false, "Away", "ไม่อยู่");
                else SetVerdict(player, true, "Safe", "รอด");
            }

            Finish(Players.Count > 0 && exploded == null);
        }
    }
}
