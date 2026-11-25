using System.Collections.Generic;
using FoodIsekaiZ.Gameplay;
using FoodIsekaiZ.Players;
using Fortal.Arena;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FoodIsekaiZ.Display
{
    // Sends each player's final score to the Fortal launcher when the last meal wave ends, by their
    // UWB tag in player order, with the MVP starred as the winner. A player with no tag (as in
    // Simulation) or no number is left out. It adds itself every time a scene loads (a new round
    // reloads the scene). Console lines start with [ArenaScoreReport].
    public sealed class ArenaScoreReport : MonoBehaviour
    {
        private UWBPlayerSpawner spawner;
        private FoodIsekaiZGameManager game;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            Attach();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Attach();
        }

        private static void Attach()
        {
            if (FindFirstObjectByType<ArenaScoreReport>() != null) return;

            var playerSpawner = FindFirstObjectByType<UWBPlayerSpawner>(FindObjectsInactive.Include);
            var manager = FindFirstObjectByType<FoodIsekaiZGameManager>(FindObjectsInactive.Include);
            if (playerSpawner == null || manager == null)
            {
                Debug.LogWarning("[ArenaScoreReport] Needs the spawner and the game manager: no scores go to the launcher");
                return;
            }

            new GameObject("Arena Score Report").AddComponent<ArenaScoreReport>().Connect(playerSpawner, manager);
        }

        private void Connect(UWBPlayerSpawner playerSpawner, FoodIsekaiZGameManager manager)
        {
            spawner = playerSpawner;
            game = manager;
            game.GameCompleted += Report;
        }

        private void OnDestroy()
        {
            if (game != null) game.GameCompleted -= Report;
        }

        private void Report()
        {
            int mvp = game.TryGetMvp(out int mvpPlayer, out _) ? mvpPlayer : 0;
            var players = new List<UWBPlayerController>();
            foreach (UWBPlayerController player in spawner.SpawnedPlayers)
            {
                if (player != null && player.PlayerId > 0 && player.TagId > 0) players.Add(player);
            }

            if (players.Count == 0)
            {
                Debug.Log("[ArenaScoreReport] No player has a UWB tag and a number: no scores sent", this);
                return;
            }

            players.Sort((a, b) => a.PlayerId.CompareTo(b.PlayerId));
            var tags = new int[players.Count];
            var scores = new int[players.Count];
            var wins = new bool[players.Count];
            for (int i = 0; i < players.Count; i++)
            {
                tags[i] = players[i].TagId;
                scores[i] = game.GetPlayerScore(players[i].PlayerId);
                wins[i] = players[i].PlayerId == mvp;
            }

            FortalArenaPlayers.ReportScoresByTag(tags, scores, wins);
            Debug.Log($"[ArenaScoreReport] Scores sent for tags {string.Join(", ", tags)}", this);
        }
    }
}
