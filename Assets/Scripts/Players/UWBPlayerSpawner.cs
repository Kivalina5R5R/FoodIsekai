using System;
using System.Collections.Generic;
using FoodIsekaiZ.Gameplay;
using Fortal.UWB;
using UnityEngine;

namespace FoodIsekaiZ.Players
{
    /// <summary>
    /// สร้าง Player ทั้งหมดจาก array ใน Inspector ทำให้ไม่ต้องวาง Player01..04 ด้วยมือ
    /// รองรับทั้งการใช้ prefab และการสร้างวงกลมเปล่าอัตโนมัติ
    /// </summary>
    public sealed class UWBPlayerSpawner : MonoBehaviour
    {
        [Serializable]
        public sealed class PlayerDefinition
        {
            public bool enabled = true;
            [Min(1)] public int playerId = 1;
            [Min(0)] public int tagId = 1;
            public Color color = Color.cyan;
            [Tooltip("ตำแหน่ง X/Z ก่อน UWB frame แรก (Vector2.y คือ world Z)")]
            public Vector2 initialPosition;

            public PlayerDefinition(int playerId, int tagId, Color color)
            {
                this.playerId = playerId;
                this.tagId = tagId;
                this.color = color;
            }
        }

        [Header("Player Template")]
        [Tooltip("ไม่ใส่ก็ได้ Script จะสร้าง GameObject วงกลมพร้อม component ที่จำเป็นให้เอง")]
        [SerializeField] private UWBPlayerController playerPrefab;
        [SerializeField] private Transform playerParent;
        [SerializeField] private bool spawnOnStart = true;
        [Header("Player Size")]
        [Tooltip("ขนาดโดยรวมของ Player ทุกตัว รวม marker และ collider")]
        [SerializeField, Min(0.05f)] private float playerScale = 1f;

        [Header("Player ID / UWB Tag Mapping")]
        [SerializeField] private PlayerDefinition[] players =
        {
            new PlayerDefinition(1, 1, Color.cyan),
            new PlayerDefinition(2, 2, Color.magenta),
            new PlayerDefinition(3, 3, Color.yellow),
            new PlayerDefinition(4, 4, Color.green)
        };

        [Header("Serial Presence")]
        [Tooltip("In Serial mode, disable each Player GameObject until its configured UWB tag is receiving fresh data. The spawner keeps the tag registered so it can be re-enabled.")]
        [SerializeField] private bool disableOfflinePlayersInSerial = true;

        private readonly List<UWBPlayerController> spawnedPlayers = new List<UWBPlayerController>();
        private UWBManager uwbManager;

        public IReadOnlyList<UWBPlayerController> SpawnedPlayers => spawnedPlayers;

        private void Start()
        {
            uwbManager = FindAnyObjectByType<UWBManager>();
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
            if (uwbManager == null)
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

            ClearSpawnedPlayers();

            if (players == null)
            {
                return;
            }

            Transform targetParent = playerParent != null ? playerParent : transform;
            for (int i = 0; i < players.Length; i++)
            {
                PlayerDefinition definition = players[i];
                if (definition == null || !definition.enabled)
                {
                    continue;
                }

                UWBPlayerController controller = CreatePlayer(targetParent);
                controller.transform.localPosition = new Vector3(
                    definition.initialPosition.x,
                    0.12f,
                    definition.initialPosition.y);
                controller.Configure(definition.playerId, definition.tagId, definition.color);
                controller.SetPlayerScale(playerScale);

                if (controller.GetComponent<FoodIsekaiZPlayerState>() == null)
                {
                    controller.gameObject.AddComponent<FoodIsekaiZPlayerState>();
                }

                controller.gameObject.SetActive(true);
                uwbManager?.RegisterTag(controller.TagId);
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

        private UWBPlayerController CreatePlayer(Transform targetParent)
        {
            if (playerPrefab != null)
            {
                UWBPlayerController instance = Instantiate(playerPrefab, targetParent);
                instance.gameObject.SetActive(false);
                return instance;
            }

            GameObject playerObject = new GameObject("UWBPlayer");
            playerObject.SetActive(false);
            playerObject.transform.SetParent(targetParent, false);

            // RequireComponent ของ controller จะเพิ่ม Rigidbody, SphereCollider และ Renderer ให้ครบ
            return playerObject.AddComponent<UWBPlayerController>();
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
                if (definition == null || !definition.enabled)
                {
                    continue;
                }

                if (!playerIds.Add(definition.playerId))
                {
                    Debug.LogWarning($"[UWBPlayerSpawner] Player ID {definition.playerId} ซ้ำกัน", this);
                }

                if (!tagIds.Add(definition.tagId))
                {
                    Debug.LogWarning($"[UWBPlayerSpawner] Tag ID {definition.tagId} ซ้ำกัน", this);
                }
            }
        }
#endif
    }
}
