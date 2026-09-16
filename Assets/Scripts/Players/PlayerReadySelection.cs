using System;
using System.Collections.Generic;
using FoodIsekaiZ.Display;
using UnityEngine;
using UnityEngine.Events;

namespace FoodIsekaiZ.Players
{
    // Scene startup coordinator: intro completion opens selection; all ready tags release gameplay.
    [DefaultExecutionOrder(-100)]
    public sealed class PlayerReadySelection : MonoBehaviour
    {
        [SerializeField] private UWBPlayerSpawner playerSpawner;
        [SerializeField] private GameObject readyRoot;
        [SerializeField] private PlayerReadyZone[] zones;
        [SerializeField] private GameObject[] gameplayObjects;
        [SerializeField, Min(0.1f)] private float holdSeconds = 2f;
        [SerializeField] private UnityEvent onAllPlayersReady = new UnityEvent();

        private PlayerNumberSelection selection;
        private bool selectionRequested;
        private bool completed;
        private bool configurationFailed;
        private readonly List<UWBPlayerController> roundPlayers = new List<UWBPlayerController>();
        private readonly HashSet<int> observedTags = new HashSet<int>();
        private float rosterChangedAt;
        private PlayerReadyZone[] activeZones;

        public bool IsSelecting => selection != null && !completed;
        public bool IsCompleted => completed;

        private void Awake()
        {
            SetGameplayVisible(false);
            if (readyRoot != null) readyRoot.SetActive(false);
        }

        // Connected to the intro's completion event instead of starting the game directly.
        public void BeginSelection()
        {
            if (completed || selectionRequested) return;
            selectionRequested = true;
        }

        private void LateUpdate()
        {
            if (!selectionRequested || completed || configurationFailed) return;
            if (selection == null && !TryInitialize()) return;

            var players = roundPlayers;
            for (int z = 0; z < activeZones.Length; z++)
            {
                PlayerReadyZone zone = activeZones[z];
                if (zone.IsEntering || selection.GetOwner(zone.PlayerNumber).HasValue) continue;
                UWBPlayerController candidate = null;
                int occupants = 0;
                for (int p = 0; p < players.Count; p++)
                {
                    UWBPlayerController player = players[p];
                    if (player == null || selection.HasAssignedNumber(player.TagId) ||
                        !player.IsAvailableForSelection || !zone.Contains(player.transform.position))
                        continue;
                    occupants++;
                    candidate = player;
                }
                bool contested = occupants > 1;
                int? candidateTag = occupants == 1 ? candidate.TagId : (int?)null;
                // A long stalled frame cannot count as an uninterrupted hold at the last sampled position.
                float delta = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
                if (selection.TickNumber(zone.PlayerNumber, candidateTag, delta))
                {
                    candidate.Configure(zone.PlayerNumber, candidate.TagId);
                }
                zone.ShowProgress(selection.GetProgress(zone.PlayerNumber),
                    selection.GetOwner(zone.PlayerNumber), contested, occupants > 0);
            }

            if (!selection.IsComplete) return;
            foreach (PlayerReadyZone zone in activeZones)
            {
                if (selection.GetOwner(zone.PlayerNumber).HasValue && !zone.IsConfirmationFinished) return;
            }
            for (int p = 0; p < players.Count; p++)
            {
                if (players[p] == null || !players[p].IsAvailableForSelection) return;
            }
            completed = true;
            if (readyRoot != null) readyRoot.SetActive(false);
            for (int p = 0; p < players.Count; p++) players[p].EnterGameplay();
            SetGameplayVisible(true);
            onAllPlayersReady.Invoke();
        }

        private bool TryInitialize()
        {
            if (playerSpawner == null || !playerSpawner.SelectsPlayerNumberBeforeGameplay ||
                readyRoot == null || zones == null || zones.Length != 4)
            {
                FailConfiguration("Assign the selection spawner, ready root, and four numbered zones.");
                return false;
            }
            var players = playerSpawner.SpawnedPlayers;
            if (players.Count == 0) return false;
            roundPlayers.Clear();
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] != null && (playerSpawner.IsStandaloneSimulationMode || players[i].IsAvailableForSelection))
                    roundPlayers.Add(players[i]);
            }
            var tags = new int[roundPlayers.Count];
            for (int i = 0; i < tags.Length; i++) tags[i] = roundPlayers[i].TagId;
            if (!playerSpawner.IsStandaloneSimulationMode)
            {
                if (!observedTags.SetEquals(tags))
                {
                    observedTags.Clear();
                    observedTags.UnionWith(tags);
                    rosterChangedAt = Time.unscaledTime;
                    return false;
                }
                if (Time.unscaledTime - rosterChangedAt < 2f) return false;
            }
            if (tags.Length == 0) return false;
            if (tags.Length > zones.Length)
            {
                FailConfiguration("More online participants than available ready cards.");
                return false;
            }
            var numbers = new bool[4];
            for (int i = 0; i < zones.Length; i++)
            {
                if (zones[i] == null || !zones[i].IsConfirmationConfigured || zones[i].PlayerNumber < 1 || zones[i].PlayerNumber > 4 ||
                    numbers[zones[i].PlayerNumber - 1])
                {
                    FailConfiguration("Ready zones must contain each number 1-4 exactly once.");
                    return false;
                }
                numbers[zones[i].PlayerNumber - 1] = true;
            }
            try
            {
                selection = new PlayerNumberSelection(tags, tags.Length, holdSeconds);
            }
            catch (ArgumentException error)
            {
                FailConfiguration(error.Message);
                return false;
            }
            activeZones = new PlayerReadyZone[tags.Length];
            Array.Sort(zones, (left, right) => left.PlayerNumber.CompareTo(right.PlayerNumber));
            var first = (RectTransform)zones[0].transform;
            var last = (RectTransform)zones[zones.Length - 1].transform;
            float center = (first.anchoredPosition.x + last.anchoredPosition.x) * .5f;
            float spacing = (last.anchoredPosition.x - first.anchoredPosition.x) / (zones.Length - 1);
            for (int i = 0; i < zones.Length; i++)
            {
                bool included = i < activeZones.Length;
                zones[i].gameObject.SetActive(included);
                if (!included) continue;
                activeZones[i] = zones[i];
                var rect = (RectTransform)zones[i].transform;
                Vector2 position = rect.anchoredPosition;
                position.x = center + (i - (activeZones.Length - 1) * .5f) * spacing;
                rect.anchoredPosition = position;
            }
            playerSpawner.LockRoundParticipants(roundPlayers);
            readyRoot.SetActive(true);
            for (int i = 0; i < roundPlayers.Count; i++) roundPlayers[i].ShowSelectionMarker(true);
            for (int i = 0; i < activeZones.Length; i++)
            {
                activeZones[i].PlayEntrance(i);
                activeZones[i].ShowProgress(0f, null, false);
            }
            return true;
        }

        private void SetGameplayVisible(bool visible)
        {
            if (gameplayObjects == null) return;
            foreach (GameObject target in gameplayObjects)
            {
                if (target != null) target.SetActive(visible);
            }
        }

        private void FailConfiguration(string message)
        {
            configurationFailed = true;
            Debug.LogError($"[PlayerReadySelection] {message}", this);
        }
    }
}
