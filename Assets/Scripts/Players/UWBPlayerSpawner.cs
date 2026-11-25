using FoodIsekaiZ.Display;
using System.Collections.Generic;
using Fortal.Arena;
using Fortal.UWB;
using UnityEngine;

namespace FoodIsekaiZ.Players
{
    // Spawns the authored player prefab for each UWB tag that connects, so nobody has to remember
    // which tag belongs to which player (the same way Bounty Hunter joins its players).
    // With Only Launcher Players on, a tag joins only when a customer logged in with it on the
    // Fortal launcher; any other tag is ignored. A player who has joined stays for the session even
    // if the launcher's list changes later (the ready phase keeps its players, so one must never be
    // swapped for a new object under it); the launcher's START check stops anyone who has not paid.
    // A tag that is heard goes to a free slot, first come first served, up to Max Players:
    //   with the ready phase on, it joins without a number and the player picks one on a card;
    //   with it off, it becomes the lowest free Player number straight away.
    // A tag keeps its player for the session, so a player who drops out comes back as themself.
    // Once every slot is taken, or the roster is locked, further tags are turned away.
    // Standalone Simulation spawns Simulation Player Count players instead.
    // Console lines start with [UWBPlayerSpawner].
    public sealed class UWBPlayerSpawner : MonoBehaviour
    {
        // The wall and floor support up to six players at once.
        public const int MaximumPlayers = 6;

        [Header("Player Template")]
        [Tooltip("Required authored player prefab, including its plate and inventory display.")]
        [SerializeField] private UWBPlayerController playerPrefab;
        [SerializeField] private Transform playerParent;
        [SerializeField] private MealFoodDisplay mealFoodDisplay;
        [SerializeField] private bool spawnOnStart = true;
        [Tooltip("Tags join without a number; the ready phase chooses Player 1-6 before gameplay. Off: each tag becomes the next free Player number.")]
        [SerializeField] private bool selectPlayerNumberBeforeGameplay;

        [Header("Player Size")]
        [Tooltip("ขนาดโดยรวมของ Player ทุกตัว รวมจานและ collider")]
        [SerializeField, Min(0.05f)] private float playerScale = 1f;

        [Header("Players")]
        [Tooltip("จำนวนผู้เล่นสูงสุด Tag ที่ต่อเข้ามาเกินจำนวนนี้จะไม่ได้เล่น ไม่ต้องตั้ง Tag ID เอง Tag ไหนต่อก่อนได้ช่องก่อน")]
        [InspectorName("Max Players")]
        [SerializeField, Range(1, MaximumPlayers)] private int playerCount = MaximumPlayers;
        [Tooltip("ตำแหน่ง X/Z เริ่มต้นของ Player ช่อง 1, 2, ... ก่อน UWB frame แรก และใช้เป็นจุดเกิดใน standalone Simulation (Vector2.y คือ world Z)")]
        [SerializeField] private Vector2[] startPositions =
        {
            new Vector2(-3.3f, -1.65f),
            new Vector2(-1.1f, -1.65f),
            new Vector2(1.1f, -1.65f),
            new Vector2(3.3f, -1.65f),
            new Vector2(-4.4f, -1.65f),
            new Vector2(4.4f, -1.65f)
        };

        [Header("Simulation")]
        [Tooltip("จำนวนผู้เล่นจำลอง 1-6 คน มีผลตอน Spawn / เริ่มเกมใหม่ เฉพาะโหมด Simulation")]
        [SerializeField, Range(1, MaximumPlayers)] private int simulationPlayerCount = 4;

        [Header("Launcher Players")]
        [Tooltip("เปิด: เฉพาะ Tag ที่ลูกค้า login ผ่าน Launcher เท่านั้นที่ได้เล่น Tag อื่นต่อเข้ามาเกมก็ไม่ detect / ปิด: Tag ไหนต่อก่อนได้เล่นก่อน (ไว้เทสโดยไม่มี Launcher)")]
        [SerializeField] private bool onlyLauncherPlayers = true;

        [Header("Serial Presence")]
        [Tooltip("In Serial mode, disable each Player GameObject until its UWB tag is receiving fresh data. The spawner keeps the tag registered so it can be re-enabled.")]
        [SerializeField] private bool disableOfflinePlayersInSerial = true;

        private readonly List<UWBPlayerController> spawnedPlayers = new List<UWBPlayerController>();
        // Tags turned away, logged once each.
        private readonly HashSet<int> refusedTags = new HashSet<int>();
        // Tags kept out because the launcher does not list them, logged once each.
        private readonly HashSet<int> ignoredTags = new HashSet<int>();
        private UWBManager uwbManager;
        private bool standaloneSimulationMode;
        private HashSet<int> roundTagIds;

        public IReadOnlyList<UWBPlayerController> SpawnedPlayers => spawnedPlayers;
        public bool SelectsPlayerNumberBeforeGameplay => selectPlayerNumberBeforeGameplay;

        // คืนค่า true เมื่อ Spawner อยู่ใน standalone Simulation ของ UWBManager
        public bool IsStandaloneSimulationMode => standaloneSimulationMode;

        // Excluded tags stay hidden even if their serial signal arrives after the roster locks.
        public void LockRoundParticipants(IReadOnlyList<UWBPlayerController> participants)
        {
            roundTagIds = new HashSet<int>();
            for (int i = 0; i < participants.Count; i++) roundTagIds.Add(participants[i].TagId);
            foreach (var player in spawnedPlayers)
                if (player != null && !roundTagIds.Contains(player.TagId)) player.gameObject.SetActive(false);
            Debug.Log($"[UWBPlayerSpawner] Roster locked: tags {string.Join(", ", roundTagIds)}", this);
        }

        private void Awake()
        {
            UWBManager manager = FindAnyObjectByType<UWBManager>();
            standaloneSimulationMode = manager != null && manager.IsSimulationMode;
            if (!standaloneSimulationMode || manager == null)
            {
                return;
            }

            // Simulation ของ UWBManager ใช้เป็นตัวเลือกโหมดเท่านั้นใน Spawner นี้
            // จึงปิด pipeline จำลองของ UWB เพื่อไม่ให้ตำแหน่ง Player ถูกเขียนทับ
            manager.enabled = false;
        }

        private void Start()
        {
            uwbManager = standaloneSimulationMode ? null : FindAnyObjectByType<UWBManager>();
            if (uwbManager != null) uwbManager.TagDetected += ConnectDetectedTag;
            if (spawnOnStart)
            {
                SpawnPlayers();
            }
        }

        private void OnEnable()
        {
            FortalArenaPlayers.Changed += ignoredTags.Clear;
        }

        private void OnDisable()
        {
            FortalArenaPlayers.Changed -= ignoredTags.Clear;
        }

        private void Update()
        {
            RefreshSerialPlayerPresence();
        }

        // A tag may play only when a customer logged in with it on the launcher page.
        private bool IsLauncherPlayer(int tagId)
        {
            return !onlyLauncherPlayers || FortalArenaPlayers.TryGetByTag(tagId, out _);
        }

        // Says once per tag (again after the launcher's list changes) that a heard tag is kept out, with
        // the tags the launcher did list, so a tag number mismatch shows in the log.
        private void LogIgnoredTag(int tagId)
        {
            if (!ignoredTags.Add(tagId)) return;

            var listed = new List<int>();
            foreach (FortalArenaPlayers.Player customer in FortalArenaPlayers.Players) listed.Add(customer.tag);
            Debug.Log($"[UWBPlayerSpawner] Tag {tagId} is heard but kept out: the launcher lists tags " +
                $"{(listed.Count > 0 ? string.Join(", ", listed) : "none")}", this);
        }

        private void OnDestroy()
        {
            if (uwbManager != null) uwbManager.TagDetected -= ConnectDetectedTag;
            if (standaloneSimulationMode || uwbManager == null)
            {
                return;
            }

            for (int i = 0; i < spawnedPlayers.Count; i++)
            {
                if (spawnedPlayers[i] != null)
                {
                    uwbManager.UnregisterTag(spawnedPlayers[i].TagId);
                }
            }
        }

        [ContextMenu("Spawn Players")]
        public void SpawnPlayers()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[UWBPlayerSpawner] Spawn Players ใช้ใน Play Mode", this);
                return;
            }

            if (playerPrefab == null)
            {
                Debug.LogError("[UWBPlayerSpawner] Assign the authored player prefab before spawning.", this);
                return;
            }

            ClearSpawnedPlayers();
            roundTagIds = null;

            // Live UWB spawns nobody up front: every tag that connects joins by itself.
            if (!standaloneSimulationMode)
            {
                Debug.Log($"[UWBPlayerSpawner] Waiting for UWB tags: the first {MaxPlayers} to connect join, no tag IDs to set", this);
                return;
            }

            // Simulated players have no real tag; slot numbers stand in as their tag IDs.
            Transform targetParent = playerParent != null ? playerParent : transform;
            int count = Mathf.Clamp(simulationPlayerCount, 1, MaximumPlayers);
            for (int slot = 1; slot <= count; slot++)
            {
                UWBPlayerController controller = CreatePlayer(targetParent);
                controller.SetUwbTrackingEnabled(false);
                AssignIdentity(controller, slot, slot);
                controller.SetStandaloneWorldPosition(StartPosition(slot));
                controller.SetPlayerScale(playerScale);
                controller.gameObject.SetActive(true);
                spawnedPlayers.Add(controller);
            }
        }

        public bool TryGetPlayer(int playerId, out UWBPlayerController player)
        {
            for (int i = 0; i < spawnedPlayers.Count; i++)
            {
                if (spawnedPlayers[i] != null && spawnedPlayers[i].PlayerId == playerId)
                {
                    player = spawnedPlayers[i];
                    return true;
                }
            }

            player = null;
            return false;
        }

        private int MaxPlayers => Mathf.Clamp(playerCount, 1, MaximumPlayers);

        // Start positions are listed per slot, starting at slot 1.
        private Vector2 StartPosition(int slot)
        {
            int index = slot - 1;
            return startPositions != null && index >= 0 && index < startPositions.Length ? startPositions[index] : Vector2.zero;
        }

        // With the ready phase the slot only picks the start position; without it the slot is the Player number.
        private void AssignIdentity(UWBPlayerController controller, int slot, int tagId)
        {
            if (selectPlayerNumberBeforeGameplay)
            {
                controller.ConfigureUnassignedTag(tagId);
                return;
            }

            controller.Configure(slot, tagId);
        }

        private UWBPlayerController CreatePlayer(Transform targetParent)
        {
            UWBPlayerController instance = Instantiate(playerPrefab, targetParent);
            instance.GetComponentInChildren<PlayerPlateDisplay>(true)?.BindMealDisplay(mealFoodDisplay);
            instance.gameObject.SetActive(false);
            return instance;
        }

        private void ClearSpawnedPlayers()
        {
            for (int i = 0; i < spawnedPlayers.Count; i++)
            {
                if (spawnedPlayers[i] != null)
                {
                    if (uwbManager != null)
                    {
                        uwbManager.UnregisterTag(spawnedPlayers[i].TagId);
                    }

                    spawnedPlayers[i].gameObject.SetActive(false);
                    Destroy(spawnedPlayers[i].gameObject);
                }
            }

            spawnedPlayers.Clear();
            refusedTags.Clear();
        }

        // A tag was heard. A new one takes the next free slot; with none left it is turned away.
        private void ConnectDetectedTag(int tagId)
        {
            if (standaloneSimulationMode || playerPrefab == null || tagId < 0 || refusedTags.Contains(tagId)) return;
            foreach (UWBPlayerController player in spawnedPlayers)
            {
                if (player != null && player.TagId == tagId) return;
            }

            // Not refused for good: the tag is heard every frame, so it joins once the launcher lists it.
            if (!IsLauncherPlayer(tagId))
            {
                LogIgnoredTag(tagId);
                return;
            }

            if (roundTagIds != null)
            {
                RefuseTag(tagId, "the round has already started");
                return;
            }

            int slot = FreeSlot();
            if (slot == 0)
            {
                RefuseTag(tagId, $"all {MaxPlayers} players already have a tag");
                return;
            }

            UWBPlayerController controller = CreatePlayer(playerParent != null ? playerParent : transform);
            controller.SetUwbTrackingEnabled(true);
            AssignIdentity(controller, slot, tagId);
            Vector2 start = StartPosition(slot);
            controller.transform.localPosition = new Vector3(start.x, 0.12f, start.y);
            controller.SetPlayerScale(playerScale);
            uwbManager.RegisterTag(tagId);
            spawnedPlayers.Add(controller);
            controller.gameObject.SetActive(true);
            Debug.Log(selectPlayerNumberBeforeGameplay
                ? $"[UWBPlayerSpawner] Tag {tagId} joined; it picks a Player number on the ready cards"
                : $"[UWBPlayerSpawner] Tag {tagId} joined as Player {slot}", this);
        }

        // The lowest slot (1 to Max Players) nobody holds yet, or 0 when all are taken. Unnumbered
        // ready-phase players hold slots by count, so their next slot is simply the next index.
        private int FreeSlot()
        {
            if (spawnedPlayers.Count >= MaxPlayers) return 0;
            if (selectPlayerNumberBeforeGameplay) return spawnedPlayers.Count + 1;

            for (int slot = 1; slot <= MaxPlayers; slot++)
            {
                if (!TryGetPlayer(slot, out _)) return slot;
            }

            return 0;
        }

        private void RefuseTag(int tagId, string reason)
        {
            refusedTags.Add(tagId);
            Debug.Log($"[UWBPlayerSpawner] Tag {tagId} turned away: {reason}", this);
        }

        private void RefreshSerialPlayerPresence()
        {
            if (standaloneSimulationMode)
            {
                return;
            }

            if (uwbManager == null)
            {
                uwbManager = FindAnyObjectByType<UWBManager>();
                if (uwbManager != null) uwbManager.TagDetected += ConnectDetectedTag;
            }

            if (uwbManager == null || !disableOfflinePlayersInSerial || !uwbManager.IsSerialMode)
            {
                return;
            }

            for (int i = 0; i < spawnedPlayers.Count; i++)
            {
                UWBPlayerController player = spawnedPlayers[i];
                if (player == null)
                {
                    continue;
                }

                if (roundTagIds != null && !roundTagIds.Contains(player.TagId))
                {
                    if (player.gameObject.activeSelf) player.gameObject.SetActive(false);
                    continue;
                }

                bool online = uwbManager.IsTagOnline(player.TagId);
                if (player.gameObject.activeSelf != online)
                {
                    player.gameObject.SetActive(online);
                }
            }
        }
    }
}
