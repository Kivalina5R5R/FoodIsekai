using System.Linq;
using FoodIsekaiZ.Players;
using Fortal.Arena;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FoodIsekaiZ.Display
{
    // Tells the Fortal launcher which UWB tag plays as Player 1, 2, ... and puts each paying
    // customer's name (the launcher sends one per tag) on that player's floor plate; a player with
    // no name keeps "PLAYER N". Checks twice a second, so tags joining and numbers being picked in
    // the ready phase both show. It adds itself every time a scene loads (a new round reloads the
    // scene). Console lines start with [ArenaPlayerNames].
    public sealed class ArenaPlayerNames : MonoBehaviour
    {
        private const float CheckSeconds = 0.5f;

        private UWBPlayerSpawner spawner;
        private int[] seats = new int[0];
        private float nextCheck;

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
            if (FindFirstObjectByType<ArenaPlayerNames>() != null) return;

            var playerSpawner = FindFirstObjectByType<UWBPlayerSpawner>(FindObjectsInactive.Include);
            if (playerSpawner == null)
            {
                Debug.LogWarning("[ArenaPlayerNames] No player spawner: the launcher gets no players");
                return;
            }

            new GameObject("Arena Player Names").AddComponent<ArenaPlayerNames>().Connect(playerSpawner);
        }

        private void Connect(UWBPlayerSpawner playerSpawner)
        {
            spawner = playerSpawner;
            Debug.Log("[ArenaPlayerNames] Launcher names go on the player plates", this);
        }

        private void OnEnable()
        {
            FortalArenaPlayers.Changed += OnLauncherListChanged;
        }

        private void OnDisable()
        {
            FortalArenaPlayers.Changed -= OnLauncherListChanged;
        }

        private void OnLauncherListChanged()
        {
            string list = string.Join(", ", FortalArenaPlayers.Players.Select(p => $"tag {p.tag} = {p.name}"));
            Debug.Log($"[ArenaPlayerNames] Launcher list: {(list.Length > 0 ? list : "nobody")}", this);
            WarnAboutUnknownSeats();
            ShowNames();
        }

        // A seated tag the launcher's list does not have keeps "P1" / "PLAYER 1"; this says so, so a
        // tag number picked wrong on the launcher page is easy to spot.
        private void WarnAboutUnknownSeats()
        {
            if (FortalArenaPlayers.Players.Count == 0) return;

            for (int slot = 0; slot < seats.Length; slot++)
            {
                if (seats[slot] <= 0 || FortalArenaPlayers.NameForTag(seats[slot]).Length > 0) continue;
                Debug.LogWarning($"[ArenaPlayerNames] Player {slot + 1} carries tag {seats[slot]}, which is not on the launcher's list", this);
            }
        }

        private void Update()
        {
            if (spawner == null || Time.unscaledTime < nextCheck) return;

            nextCheck = Time.unscaledTime + CheckSeconds;
            ReportSeats();
            ShowNames();
        }

        // Slot 0 is Player 1; a number nobody has picked yet sits empty (0).
        private void ReportSeats()
        {
            int[] now = new int[UWBPlayerSpawner.MaximumPlayers];
            int used = 0;
            foreach (UWBPlayerController player in spawner.SpawnedPlayers)
            {
                if (player == null || player.PlayerId < 1 || player.PlayerId > now.Length) continue;
                now[player.PlayerId - 1] = player.TagId;
                used = Mathf.Max(used, player.PlayerId);
            }

            now = now.Take(used).ToArray();
            if (now.SequenceEqual(seats)) return;

            seats = now;
            FortalArenaPlayers.SetSeats(seats);
            Debug.Log($"[ArenaPlayerNames] Seats by player: {string.Join(", ", seats)}", this);
            WarnAboutUnknownSeats();
        }

        private void ShowNames()
        {
            if (spawner == null) return;

            foreach (UWBPlayerController player in spawner.SpawnedPlayers)
            {
                if (player == null) continue;
                PlayerPlateDisplay plate = player.GetComponentInChildren<PlayerPlateDisplay>(true);
                if (plate != null) plate.SetCustomerName(FortalArenaPlayers.NameForTag(player.TagId));
            }
        }
    }
}
