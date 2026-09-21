using System;
using System.Collections.Generic;
using Fortal.UWB;
using UnityEngine;

namespace FoodIsekaiZ.Players
{
    // Spawns the authored player prefab using the configured UWB identities.
    public sealed class UWBPlayerSpawner : MonoBehaviour
    {
        [Serializable]
        public sealed class PlayerDefinition
        {
            [SerializeField] private bool enabled = true;
            [SerializeField, Min(1)] private int playerId = 1;
            [SerializeField, Min(0)] private int tagId = 1;
            [Tooltip("ตำแหน่ง X/Z เริ่มต้นก่อน UWB frame แรก และใช้เป็นจุดเกิดใน standalone Simulation (Vector2.y คือ world Z)")]
            [SerializeField] private Vector2 initialPosition;

            public bool Enabled => enabled;
            public int PlayerId => playerId;
            public int TagId => tagId;
            public Vector2 InitialPosition => initialPosition;

            public PlayerDefinition(int playerId, int tagId)
            {
                this.playerId = playerId;
                this.tagId = tagId;
            }
        }

        [Header("Player Template")]
        [Tooltip("Required authored player prefab, including its plate and inventory display.")]
        [SerializeField] private UWBPlayerController playerPrefab;
        [SerializeField] private Transform playerParent;
        [SerializeField] private bool spawnOnStart = true;
        [Tooltip("Spawn unassigned tag markers; the ready phase chooses Player 1-4 before gameplay.")]
        [SerializeField] private bool selectPlayerNumberBeforeGameplay;

        [Header("Player Size")]
        [Tooltip("ขนาดโดยรวมของ Player ทุกตัว รวมจานและ collider")]
        [SerializeField, Min(0.05f)] private float playerScale = 1f;

        [Header("Simulation")]
        [Tooltip("จำนวนผู้เล่นจำลอง 1-4 คน ใช้รายการที่ Enabled ใน Players ตามลำดับ มีผลตอน Spawn / เริ่มเกมใหม่ เฉพาะโหมด Simulation")]
        [SerializeField, Range(1, 4)] private int simulationPlayerCount = 4;

        [Header("Player ID / UWB Tag Mapping")]
        [SerializeField] private PlayerDefinition[] players =
        {
            new PlayerDefinition(1, 1),
            new PlayerDefinition(2, 2),
            new PlayerDefinition(3, 3),
            new PlayerDefinition(4, 4)
        };

        [Header("Serial Presence")]
        [Tooltip("In Serial mode, disable each Player GameObject until its configured UWB tag is receiving fresh data. The spawner keeps the tag registered so it can be re-enabled.")]
        [SerializeField] private bool disableOfflinePlayersInSerial = true;

        private readonly List<UWBPlayerController> spawnedPlayers = new List<UWBPlayerController>();
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
            if (spawnOnStart)
            {
                SpawnPlayers();
            }
        }

        private void Update()
        {
            RefreshSerialPlayerPresence();
        }

        private void OnDestroy()
        {
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

            if (players == null)
            {
                return;
            }

            Transform targetParent = playerParent != null ? playerParent : transform;
            bool useUwbTracking = !standaloneSimulationMode;
            for (int i = 0; i < players.Length; i++)
            {
                if (standaloneSimulationMode && spawnedPlayers.Count >= Mathf.Clamp(simulationPlayerCount, 1, 4))
                {
                    break;
                }

                PlayerDefinition definition = players[i];
                if (definition == null || !definition.Enabled)
                {
                    continue;
                }

                UWBPlayerController controller = CreatePlayer(targetParent);
                controller.SetUwbTrackingEnabled(useUwbTracking);
                if (selectPlayerNumberBeforeGameplay)
                {
                    controller.ConfigureUnassignedTag(definition.TagId);
                }
                else
                {
                    controller.Configure(definition.PlayerId, definition.TagId);
                }
                if (useUwbTracking)
                {
                    controller.transform.localPosition = new Vector3(
                        definition.InitialPosition.x,
                        0.12f,
                        definition.InitialPosition.y);
                }
                else
                {
                    // ใช้ตำแหน่งเริ่มต้นเดิมเพื่อให้ผู้เล่นเรียงถัดกันบนเส้นกลางสนาม
                    controller.SetStandaloneWorldPosition(definition.InitialPosition);
                }

                controller.SetPlayerScale(playerScale);

                controller.gameObject.SetActive(true);
                if (useUwbTracking)
                {
                    uwbManager?.RegisterTag(controller.TagId);
                }

                spawnedPlayers.Add(controller);
            }

            if (standaloneSimulationMode && spawnedPlayers.Count < Mathf.Clamp(simulationPlayerCount, 1, 4))
            {
                Debug.LogWarning("[UWBPlayerSpawner] จำนวนรายการ Players ที่ Enabled ไม่พอกับ Simulation Player Count", this);
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

        private UWBPlayerController CreatePlayer(Transform targetParent)
        {
            UWBPlayerController instance = Instantiate(playerPrefab, targetParent);
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

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (players == null)
            {
                return;
            }

            var playerIds = new HashSet<int>();
            var tagIds = new HashSet<int>();
            for (int i = 0; i < players.Length; i++)
            {
                PlayerDefinition definition = players[i];
                if (definition == null || !definition.Enabled)
                {
                    continue;
                }

                if (!playerIds.Add(definition.PlayerId))
                {
                    Debug.LogWarning($"[UWBPlayerSpawner] Player ID {definition.PlayerId} ซ้ำกัน", this);
                }

                if (!tagIds.Add(definition.TagId))
                {
                    Debug.LogWarning($"[UWBPlayerSpawner] Tag ID {definition.TagId} ซ้ำกัน", this);
                }
            }
        }
#endif
    }
}
