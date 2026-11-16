using System.Collections.Generic;
using FoodIsekaiZ.Localization;
using UnityEngine;

namespace FoodIsekaiZ.Minigames
{
    // How one player did in a minigame, shown on the wall when it ends.
    public readonly struct PlayerVerdict
    {
        public PlayerVerdict(MinigamePlayer player, bool success, string english, string thai)
        {
            Player = player;
            Success = success;
            English = english;
            Thai = thai;
        }

        public MinigamePlayer Player { get; }
        public bool Success { get; }
        public string English { get; }
        public string Thai { get; }
    }

    // One minigame, played by MinigameRunner: Begin (its floor zones and wall pictures appear while
    // the command word shows), then Tick every frame until it calls Finish, then End takes its
    // drawings away. There is no time limit, so every game must finish by itself; the runner only
    // calls TimeUp for the simulation skip key or a long hidden safety stop.
    // A subclass builds its look in Build, plays in Step, and must call Finish from Step or OnTimeUp.
    // Everything runs on unscaled time. Speed is the round's speed-up k (1, 1.25 or 1.5).
    public abstract class Minigame
    {
        private const string HudPrefab = "MinigameHud";

        private readonly List<PlayerVerdict> verdicts = new List<PlayerVerdict>();

        protected MinigameStage Stage { get; private set; }
        protected IReadOnlyList<MinigamePlayer> Players { get; private set; }
        protected float Speed { get; private set; } = 1f;
        // Drawings on the wall and on the floor; both are removed when the minigame ends.
        protected RectTransform WallLayer { get; private set; }
        protected RectTransform FloorLayer { get; private set; }
        // The shared wall frame: name and rule, step counter, player chips.
        protected MinigameHudView Hud { get; private set; }
        // Seconds since play started (not counting the command word).
        protected float Elapsed { get; private set; }

        // The one-word command shown big before play ("FRY!" / "ทอด!").
        public abstract string EnglishCommand { get; }
        public abstract string ThaiCommand { get; }
        // The minigame's name and a one-line rule, shown under the command.
        public abstract string EnglishTitle { get; }
        public abstract string ThaiTitle { get; }
        public abstract string EnglishRule { get; }
        public abstract string ThaiRule { get; }
        // Whether the wall is darkened behind the game while it plays. A game whose wall art
        // should sit straight on the tavern scene turns this off.
        public virtual bool ShadesWall => true;

        public bool IsDone { get; private set; }
        public bool Passed { get; private set; }
        // Passed with nobody failing: earns the extra coins.
        public bool EveryonePassed { get; private set; }
        public IReadOnlyList<PlayerVerdict> Verdicts => verdicts;

        public void Begin(MinigameStage stage, IReadOnlyList<MinigamePlayer> players, float speed)
        {
            Stage = stage;
            Players = players;
            Speed = Mathf.Max(0.5f, speed);
            WallLayer = stage.Group(GetType().Name + " Wall", stage.Wall);
            FloorLayer = stage.Group(GetType().Name + " Floor", stage.Floor);
            Hud = stage.LoadView<MinigameHudView>(HudPrefab, WallLayer, FloorLayer);
            Hud.SetTitle(Pick(EnglishTitle, ThaiTitle), Pick(EnglishRule, ThaiRule));
            Build();
            // The HUD stays over the game's own drawings.
            Hud.transform.SetAsLastSibling();
        }

        public void Tick(float deltaTime)
        {
            if (IsDone) return;
            Elapsed += deltaTime;
            Step(deltaTime);
        }

        // The game is being cut short (simulation skip or the safety stop): it decides its result now.
        public void TimeUp()
        {
            if (IsDone) return;
            OnTimeUp();
            if (!IsDone) Finish(false);
        }

        public void End()
        {
            if (WallLayer != null) Object.Destroy(WallLayer.gameObject);
            if (FloorLayer != null) Object.Destroy(FloorLayer.gameObject);
        }

        protected abstract void Build();

        protected abstract void Step(float deltaTime);

        // Called at the 20 s cap; decide verdicts and call Finish.
        protected abstract void OnTimeUp();

        protected void Finish(bool passed)
        {
            if (IsDone) return;
            IsDone = true;
            Passed = passed;
            bool everyone = passed;
            foreach (PlayerVerdict verdict in verdicts) everyone &= verdict.Success;
            EveryonePassed = everyone && verdicts.Count > 0;
        }

        // Records (or replaces) how this player did.
        protected void SetVerdict(MinigamePlayer player, bool success, string english, string thai)
        {
            for (int i = 0; i < verdicts.Count; i++)
            {
                if (verdicts[i].Player != player) continue;
                verdicts[i] = new PlayerVerdict(player, success, english, thai);
                return;
            }

            verdicts.Add(new PlayerVerdict(player, success, english, thai));
        }

        protected bool HasVerdict(MinigamePlayer player)
        {
            foreach (PlayerVerdict verdict in verdicts)
                if (verdict.Player == player) return true;
            return false;
        }

        // Co-op pass rule for elimination games: at least half the team (rounded up) made it.
        protected static bool HalfTeam(int succeeded, int total)
        {
            return total > 0 && succeeded >= (total + 1) / 2;
        }

        protected int SuccessCount()
        {
            int count = 0;
            foreach (PlayerVerdict verdict in verdicts)
                if (verdict.Success) count++;
            return count;
        }

        protected static string Pick(string english, string thai)
        {
            return GameLanguage.Pick(english, thai);
        }
    }
}
