using System;
using System.Collections.Generic;
using FoodIsekaiZ.Audio;
using FoodIsekaiZ.Display;
using FoodIsekaiZ.Gameplay;
using FoodIsekaiZ.Players;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace FoodIsekaiZ.Minigames
{
    // Plays one minigame after each meal, chosen by the players:
    //   choice (MinigameChoice): the offers pop up as four cards in the middle of the wall and on
    //   the perk shop's floor tiles, and standing on a tile picks it; if nobody picks in time one
    //   is drawn at random, then everything animates away.
    //   then the chosen game: command word 2 s, a 3, 2, 1 count-in on the wall (1 s each), play until the game finishes by itself (no clock
    //   is shown), result ResultSeconds.
    // When the result is over the game manager moves on, through its transition, to the perk shop
    // (or the results after the last meal). Runs on unscaled time.
    // Coins: a cleared minigame puts ClearCoins in the team wallet and a perfect one PerfectCoins more;
    // after the last meal they end up as bonus score. In Simulation mode N skips the current step
    // (on the choice it draws a random game).
    // It adds itself, under System's MinigameManager, every time a scene loads and registers with the game
    // manager, so no scene set-up is needed. Console lines start with [Minigames].
    public sealed class MinigameRunner : MonoBehaviour, IMinigameRoundHost
    {
        private const float CommandSeconds = 2f;
        // After the command card every game counts in 3, 2, 1 on the wall, one number per step.
        private const int CountInFrom = 3;
        private const float CountInStepSeconds = 1f;
        // Games play with no clock and end themselves; this only rescues the round if a game
        // somehow never finishes, and is never shown.
        private const float PlaySafetySeconds = 120f;
        private const float ResultSeconds = 4f;
        private const int ClearCoins = 10;
        private const int PerfectCoins = 5;

        private enum Step { Idle, Choice, Command, CountIn, Play, Result }

        private static MinigameRunner current;

        private readonly MinigameCatalog catalog = new MinigameCatalog();
        private readonly List<MinigamePlayer> players = new List<MinigamePlayer>();
        private readonly List<Minigame> offers = new List<Minigame>();
        private readonly List<string> offerIds = new List<string>();

        private FoodIsekaiZGameManager gameManager;
        private UWBPlayerSpawner spawner;
        private GameSoundPlayer sounds;
        private MinigameStage stage;
        private MinigameOverlay overlay;
        private MinigameChoice choice;
        private PerkShopPresentation shop;

        private Step step;
        private float clock;
        private float stepEnds;
        private int roundIndex;
        private int roundCount;
        private Minigame game;
        private int countInNumber;
        private string gameId;
        private Action finished;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            current = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            ConnectScene();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ConnectScene();
        }

        private static void ConnectScene()
        {
            if (current != null) return;

            var manager = FindFirstObjectByType<FoodIsekaiZGameManager>(FindObjectsInactive.Include);
            var displays = FindFirstObjectByType<DisplayManager>(FindObjectsInactive.Include);
            var arena = FindFirstObjectByType<FoodIsekaiZArenaLayout>(FindObjectsInactive.Include);
            RectTransform wall = DisplayManager.FindWallCanvas();
            if (manager == null || displays == null || arena == null || wall == null || !manager.UsesMealWaves)
            {
                Debug.LogWarning("[Minigames] Needs the game manager (with meal waves), the displays, the arena and the wall canvas: no minigames this game");
                return;
            }

            var settings = FindFirstObjectByType<MinigameManager>(FindObjectsInactive.Include);
            if (settings != null && !settings.PlayMinigames)
            {
                Debug.Log("[Minigames] Play Minigames is off on MinigameManager: no minigames this game");
                return;
            }

            var host = new GameObject("Minigame Runner");
            current = host.AddComponent<MinigameRunner>();
            Transform parent = settings != null ? settings.transform : manager.transform.root;
            host.transform.SetParent(parent, false);
            current.Connect(manager, displays, arena, wall);
        }

        private void Connect(FoodIsekaiZGameManager manager, DisplayManager displays, FoodIsekaiZArenaLayout arena, RectTransform wall)
        {
            gameManager = manager;
            spawner = FindFirstObjectByType<UWBPlayerSpawner>(FindObjectsInactive.Include);
            sounds = FindFirstObjectByType<GameSoundPlayer>(FindObjectsInactive.Include);
            // The runner sits under MinigameManager (System); its floor canvas stays under the
            // scene's Map, next to the arena.
            Transform map = arena.transform.root;
            shop = FindFirstObjectByType<PerkShopPresentation>(FindObjectsInactive.Include);
            if (shop == null || shop.Zones == null) Debug.LogWarning("[Minigames] No perk shop to borrow the floor tiles from: minigames are drawn at random", this);
            stage = new MinigameStage(wall, displays.FloorCamera, arena.ArenaBounds, arena.gameObject.layer,
                MinigameArt.FindFont("Poppins-Bold SDF"), map);
            overlay = new MinigameOverlay(stage);
            gameManager.RegisterMinigameHost(this);
            Debug.Log("[Minigames] Ready: the players pick a minigame after every meal", this);
        }

        private void OnDestroy()
        {
            if (gameManager != null) gameManager.ReleaseMinigameHost(this);
            if (current == this) current = null;
        }

        public void BeginRound(int round, int count, Action onFinished)
        {
            roundIndex = round;
            roundCount = count;
            finished = onFinished;
            clock = 0f;
            game = null;
            CollectPlayers();
            CreateOffers();
            stage.SetVisible(true);
            overlay.Hide();
            overlay.SetBackdrop(false);

            var pictures = new List<string>();
            foreach (string id in offerIds) pictures.Add(catalog.PictureOf(id));
            choice = shop != null && shop.Zones != null
                ? new MinigameChoice(stage, players, offers, pictures, shop) : null;
            sounds?.TryPlay(GameSoundCue.SmallPerkReveal, true);
            step = Step.Choice;
            Debug.Log($"[Minigames] Round {roundIndex + 1}/{roundCount} offers {string.Join(", ", offerIds)}, players={players.Count}", this);
        }

        private void CollectPlayers()
        {
            players.Clear();
            if (spawner == null) return;
            foreach (UWBPlayerController controller in spawner.SpawnedPlayers)
            {
                if (controller == null || controller.PlayerId <= 0) continue;
                var player = new MinigamePlayer(controller, stage);
                player.Sample(0f);
                players.Add(player);
            }

            players.Sort((left, right) => left.PlayerId.CompareTo(right.PlayerId));
        }

        private void CreateOffers()
        {
            offers.Clear();
            offerIds.Clear();
            foreach (string id in catalog.Lineup(roundIndex, roundCount))
            {
                offerIds.Add(id);
                offers.Add(catalog.Create(id));
            }
        }

        private void Update()
        {
            // A script reload during Play mode keeps this component but drops its stage and overlay
            // (plain C# objects Unity does not carry across the reload). Minigames stay off until
            // Play mode is restarted, instead of failing every frame.
            if (stage == null || overlay == null)
            {
                Debug.LogWarning("[Minigames] Scripts were reloaded during Play mode: minigames are off until Play mode restarts", this);
                if (gameManager != null) gameManager.ReleaseMinigameHost(this);
                enabled = false;
                return;
            }

            if (step == Step.Idle)
            {
                // The last card stays up until the next screen has covered the wall.
                if (stage.Wall.gameObject.activeSelf && gameManager.CurrentMealWavePhase != MealWavePhase.Minigame)
                {
                    overlay.Hide();
                    stage.SetVisible(false);
                }

                return;
            }

            float deltaTime = Time.unscaledDeltaTime;
            clock += deltaTime;
            foreach (MinigamePlayer player in players) player.Sample(clock);

            bool skip = gameManager.IsSimulationMode && Keyboard.current != null && Keyboard.current.nKey.wasPressedThisFrame;
            if (skip) stepEnds = Mathf.Min(stepEnds, clock);

            switch (step)
            {
                case Step.Choice:
                    TickChoice(deltaTime, skip);
                    break;
                case Step.Command:
                    if (clock >= stepEnds) ShowCountIn(CountInFrom);
                    break;
                case Step.CountIn:
                    if (clock < stepEnds) break;
                    if (countInNumber > 1) ShowCountIn(countInNumber - 1);
                    else StartPlay();
                    break;
                case Step.Play:
                    TickPlay(deltaTime, skip);
                    break;
                case Step.Result:
                    if (clock >= stepEnds) EndRound();
                    break;
            }
        }

        private void TickChoice(float deltaTime, bool skip)
        {
            if (choice == null)
            {
                StartCommand(UnityEngine.Random.Range(0, offers.Count));
                return;
            }

            choice.Tick(deltaTime, skip);
            gameManager.ShowMinigameSeconds(choice.SecondsLeft);
            if (!choice.IsFinished) return;
            int picked = choice.Picked;
            choice.End();
            choice = null;
            Debug.Log($"[Minigames] Playing {offerIds[picked]}", this);
            StartCommand(picked);
        }

        private void StartCommand(int picked)
        {
            sounds?.TryPlay(GameSoundCue.SmallPerkPurchase, true);
            game = offers[picked];
            gameId = offerIds[picked];
            game.Begin(stage, players, catalog.Speed(roundIndex, roundCount));
            overlay.SetBackdrop(true);
            overlay.ShowCommand(game);
            step = Step.Command;
            stepEnds = clock + CommandSeconds;
        }

        // Shows number on the wall over the game's layout, which is already drawn, so the players see
        // where to stand before it starts.
        private void ShowCountIn(int number)
        {
            if (step != Step.CountIn) overlay.SetBackdrop(game.ShadesWall);
            countInNumber = number;
            overlay.ShowCountIn(number, CountInStepSeconds);
            sounds?.TryPlay(GameSoundCue.SmallPerkHoldTick, true);
            step = Step.CountIn;
            stepEnds = clock + CountInStepSeconds;
        }

        private void StartPlay()
        {
            overlay.ShowPlaying(game);
            overlay.SetBackdrop(game.ShadesWall);
            step = Step.Play;
            stepEnds = clock + PlaySafetySeconds;
            // No countdown while a game plays: the meal clock is hidden and the game ends itself.
            gameManager.ShowMinigameSeconds(0f);
        }

        private void TickPlay(float deltaTime, bool skip)
        {
            game.Tick(deltaTime);
            if (!game.IsDone && (clock >= stepEnds || skip)) game.TimeUp();
            if (game.IsDone) ShowResult();
        }

        private void ShowResult()
        {
            int coins = game.Passed ? ClearCoins + (game.EveryonePassed ? PerfectCoins : 0) : 0;
            gameManager.AwardTeamCoins(coins);
            catalog.Record(gameId, game.Passed);
            bool finalRound = roundIndex >= roundCount - 1;
            // The game's own drawings go now, so the result card is not drawn over them.
            game.End();
            gameManager.ShowMinigameSeconds(0f);
            overlay.ShowResult(game, coins, finalRound);
            sounds?.TryPlay(game.Passed ? GameSoundCue.PerkPurchase : GameSoundCue.CustomerExpired, true);
            Debug.Log($"[Minigames] {gameId}: passed={game.Passed}, perfect={game.EveryonePassed}, coins={coins}", this);
            step = Step.Result;
            stepEnds = clock + ResultSeconds;
        }

        private void EndRound()
        {
            step = Step.Idle;
            game.End();
            Debug.Log($"[Minigames] Round {roundIndex + 1} over after {clock:0.0} s", this);
            Action done = finished;
            finished = null;
            done?.Invoke();
        }
    }
}
