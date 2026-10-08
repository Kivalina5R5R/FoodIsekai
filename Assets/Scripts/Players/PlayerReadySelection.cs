using System;
using System.Collections;
using System.Collections.Generic;
using FoodIsekaiZ.Audio;
using FoodIsekaiZ.Display;
using FoodIsekaiZ.Gameplay;
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
        [SerializeField] private ReadyPhaseGuide readyGuide;
        [SerializeField] private GameSoundPlayer soundPlayer;

        private PlayerNumberSelection selection;
        private bool selectionRequested;
        private bool completed;
        private bool configurationFailed;
        private bool readyShown;
        private readonly List<UWBPlayerController> roundPlayers = new List<UWBPlayerController>();
        private readonly HashSet<int> observedTags = new HashSet<int>();
        private float rosterChangedAt;
        private PlayerReadyZone[] activeZones;
        private float zoneCenter;
        private float zoneSpacing;
        private bool rosterLocked;
        // Per-card feedback state so step, hold and contest sounds play once per change.
        private readonly bool[] zoneOccupied = new bool[UWBPlayerSpawner.MaximumPlayers];
        private readonly bool[] zoneContested = new bool[UWBPlayerSpawner.MaximumPlayers];
        private readonly int[] zoneHoldStep = new int[UWBPlayerSpawner.MaximumPlayers];
        // Card ticks climb D, E, F#, G, A, B and hold ticks F#, A, B: notes of the B minor intro music.
        private static readonly float[] CardAppearPitches = { 1f, 1.122f, 1.26f, 1.335f, 1.498f, 1.682f };
        private static readonly float[] HoldTickPitches = { 1f, 1.189f, 1.335f };

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
            AddNewParticipants();

            if (!readyShown)
            {
                if (readyGuide != null && readyGuide.isActiveAndEnabled && !readyGuide.IsReadyForSelection) return;
                ShowReady();
            }

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
                    soundPlayer?.TryPlay(GameSoundCue.ReadyConfirm, true);
                    ResetZoneSound(z);
                }
                else
                {
                    PlayZoneSounds(z, occupants > 0, contested, selection.GetProgress(zone.PlayerNumber));
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
            bool guideFinished = readyGuide == null || readyGuide.TryFinish();
            if (!rosterLocked && (guideFinished || readyGuide.HasStartedExit))
            {
                playerSpawner.LockRoundParticipants(roundPlayers);
                rosterLocked = true;
            }
            if (!guideFinished) return;
            completed = true;
            if (readyRoot != null) readyRoot.SetActive(false);
            for (int p = 0; p < players.Count; p++) players[p].EnterGameplay();
            SetGameplayVisible(true);
            PlayerSessionLog.BeginSession(players.Count);
            onAllPlayersReady.Invoke();
        }

        private bool TryInitialize()
        {
            if (playerSpawner == null || !playerSpawner.SelectsPlayerNumberBeforeGameplay ||
                readyRoot == null || zones == null || zones.Length < 1 || zones.Length > UWBPlayerSpawner.MaximumPlayers)
            {
                FailConfiguration("Assign the selection spawner, ready root, and one numbered zone per player, up to six.");
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
            var numbers = new bool[zones.Length];
            for (int i = 0; i < zones.Length; i++)
            {
                if (zones[i] == null || !zones[i].IsConfirmationConfigured || zones[i].PlayerNumber < 1 ||
                    zones[i].PlayerNumber > zones.Length || numbers[zones[i].PlayerNumber - 1])
                {
                    FailConfiguration($"Ready zones must contain each number 1-{zones.Length} exactly once.");
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
            Array.Sort(zones, (left, right) => left.PlayerNumber.CompareTo(right.PlayerNumber));
            var first = (RectTransform)zones[0].transform;
            var last = (RectTransform)zones[zones.Length - 1].transform;
            zoneCenter = (first.anchoredPosition.x + last.anchoredPosition.x) * .5f;
            zoneSpacing = (last.anchoredPosition.x - first.anchoredPosition.x) / Mathf.Max(1, zones.Length - 1);
            ResizeReadyZones();
            for (int i = 0; i < roundPlayers.Count; i++) roundPlayers[i].ShowSelectionMarker(true);
            return true;
        }

        private void AddNewParticipants()
        {
            if (rosterLocked || (readyGuide != null && readyGuide.HasStartedExit)) return;
            int previousCount = roundPlayers.Count;
            foreach (UWBPlayerController player in playerSpawner.SpawnedPlayers)
            {
                if (roundPlayers.Count >= zones.Length) break;
                if (player == null || !player.IsAvailableForSelection || roundPlayers.Contains(player)) continue;
                if (!selection.TryAddParticipant(player.TagId)) continue;
                roundPlayers.Add(player);
                player.ShowSelectionMarker(true);
            }
            if (roundPlayers.Count == previousCount) return;
            ResizeReadyZones();
            if (!readyShown) return;
            for (int i = previousCount; i < activeZones.Length; i++)
                activeZones[i].PlayEntrance(i - previousCount);
        }

        private void ResizeReadyZones()
        {
            activeZones = new PlayerReadyZone[roundPlayers.Count];
            for (int i = 0; i < zones.Length; i++)
            {
                bool included = i < activeZones.Length;
                if (!included) zones[i].gameObject.SetActive(false);
                else if (!selection.GetOwner(zones[i].PlayerNumber).HasValue) zones[i].gameObject.SetActive(true);
                if (!included) continue;
                activeZones[i] = zones[i];
                var rect = (RectTransform)zones[i].transform;
                Vector2 position = rect.anchoredPosition;
                position.x = zoneCenter + (i - (activeZones.Length - 1) * .5f) * zoneSpacing;
                rect.anchoredPosition = position;
                zones[i].RefreshSelectionBounds();
                ResetZoneSound(i);
                if (readyShown) zones[i].ShowProgress(selection.GetProgress(zones[i].PlayerNumber),
                    selection.GetOwner(zones[i].PlayerNumber), false);
            }
        }

        private void ShowReady()
        {
            readyShown = true;
            readyRoot.SetActive(true);
            for (int i = 0; i < activeZones.Length; i++)
            {
                activeZones[i].PlayEntrance(i);
                activeZones[i].ShowProgress(0f, null, false);
            }
            if (soundPlayer != null) StartCoroutine(PlayCardAppearSounds(activeZones.Length));
        }

        // Matches the cards' staggered entrance, which starts each card 0.09 seconds after the previous one.
        private IEnumerator PlayCardAppearSounds(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (i > 0) yield return new WaitForSecondsRealtime(0.09f);
                soundPlayer.TryPlay(GameSoundCue.ReadyCardAppear, false, CardAppearPitches[Mathf.Min(i, CardAppearPitches.Length - 1)]);
            }
        }

        private void PlayZoneSounds(int index, bool occupied, bool contested, float progress)
        {
            if (index >= zoneOccupied.Length) return;
            if (occupied != zoneOccupied[index])
                soundPlayer?.TryPlay(occupied ? GameSoundCue.ReadyStepOn : GameSoundCue.ReadyStepOff);
            if (contested && !zoneContested[index]) soundPlayer?.TryPlay(GameSoundCue.ReadyContested);
            // One tick per quarter of the hold, each a step higher toward the confirmation.
            int step = Mathf.FloorToInt(progress * 4f);
            if (step > zoneHoldStep[index] && step > 0)
                soundPlayer?.TryPlay(GameSoundCue.ReadyHoldTick, false,
                    HoldTickPitches[Mathf.Min(step - 1, HoldTickPitches.Length - 1)]);
            zoneOccupied[index] = occupied;
            zoneContested[index] = contested;
            zoneHoldStep[index] = step;
        }

        private void ResetZoneSound(int index)
        {
            if (index >= zoneOccupied.Length) return;
            zoneOccupied[index] = false;
            zoneContested[index] = false;
            zoneHoldStep[index] = 0;
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
