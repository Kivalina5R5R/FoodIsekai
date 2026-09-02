using System;
using System.Collections.Generic;
using System.IO;
using FoodIsekaiZ.Gameplay;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace FoodIsekaiZ.Display
{

    public sealed class FoodIsekaiZNpcWaveSpawner : MonoBehaviour
    {
        private const int DisplaySlotCount = 6;
        private const string BackLayerName = "NPCBackLayer";
        private const string FrontLayerName = "NPCFrontLayer";
        private const string DefaultNpcPrefabFolder = "Assets/Prefab";
        private const string DefaultNpcPrefabNamePrefix = "NPC";

        [Serializable]
        private sealed class NpcPrefabSetting
        {
            [SerializeField] private GameObject prefab;
            [SerializeField] private bool flipWhenEnteringFromLeft;
            [SerializeField] private bool flipWhenEnteringFromRight;

            public GameObject Prefab => prefab;
            public bool FlipWhenEnteringFromLeft => flipWhenEnteringFromLeft;
            public bool FlipWhenEnteringFromRight => flipWhenEnteringFromRight;

            public NpcPrefabSetting(
                GameObject prefab,
                bool flipWhenEnteringFromLeft,
                bool flipWhenEnteringFromRight)
            {
                this.prefab = prefab;
                this.flipWhenEnteringFromLeft = flipWhenEnteringFromLeft;
                this.flipWhenEnteringFromRight = flipWhenEnteringFromRight;
            }
        }

        [Header("NPC Prefab Pool")]
        [Tooltip("NPC prefabs are drawn without replacement during one Wave. Across Waves, prefabs with lower Power are selected first. Configure each Prefab and its entry orientation in the same list.")]
        [InspectorName("NPC Prefabs")]
        [SerializeField] private NpcPrefabSetting[] npcPrefabSettings = Array.Empty<NpcPrefabSetting>();
        [Tooltip("Folder searched recursively for NPC prefabs when the scene is validated or play mode starts in the Unity Editor.")]
        [SerializeField] private string npcPrefabFolder = DefaultNpcPrefabFolder;
        [Tooltip("Only prefab assets whose names start with this prefix are included in the automatic pool.")]
        [SerializeField] private string npcPrefabNamePrefix = DefaultNpcPrefabNamePrefix;

        [Header("NPC Entrance")]
        [Tooltip("Distance outside the wall canvas from which each NPC starts walking.")]
        [SerializeField, Min(0.1f)] private float entranceDistanceCanvasMultiplier = 0.75f;
        [Tooltip("Time in seconds for an NPC to walk from its entrance point to its assigned T slot.")]
        [SerializeField, Min(0.05f)] private float entranceDurationSeconds = 1.5f;
        [Tooltip("Distance from the assigned T slot at which the NPC switches to the near-target walking duration.")]
        [SerializeField, Min(0.01f)] private float nearTargetDistanceCanvasMultiplier = 0.15f;
        [Tooltip("Walking duration used while the NPC is near its assigned T slot. The default slows the final approach from 1.5 to 2 seconds.")]
        [SerializeField, Min(0.05f)] private float nearTargetDurationSeconds = 2f;
        [Tooltip("Time in seconds an NPC remains at its assigned T slot before the food UI and timer are shown.")]
        [SerializeField, Min(0f)] private float arrivalHoldDurationSeconds = 1f;

        [Header("NPC Walking Motion")]
        [Tooltip("Vertical distance in canvas pixels that an NPC bobs while walking.")]
        [SerializeField, Min(0f)] private float walkingBobHeight = 6f;
        [Tooltip("Number of up-and-down walking cycles per second.")]
        [SerializeField, Min(0.1f)] private float walkingBobFrequency = 4.5f;

        [Header("NPC Spawn Schedule")]
        [Tooltip("Maximum number of NPCs allowed to start walking in one batch. The value is limited to 1 or 2.")]
        [SerializeField, Range(1, 2)] private int maximumNpcSpawnsPerBatch = 2;
        [Tooltip("Random minimum delay before the first NPC batch of a Wave.")]
        [SerializeField, Min(0f)] private float minimumInitialSpawnDelaySeconds = 0.35f;
        [Tooltip("Random maximum delay before the first NPC batch of a Wave.")]
        [SerializeField, Min(0f)] private float maximumInitialSpawnDelaySeconds = 1.1f;
        [Tooltip("Random minimum delay between NPC spawn batches. The next batch also waits until the previous batch reaches its slots.")]
        [SerializeField, Min(0.05f)] private float minimumBatchDelaySeconds = 2f;
        [Tooltip("Random maximum delay between NPC spawn batches. The next batch also waits until the previous batch reaches its slots.")]
        [SerializeField, Min(0.05f)] private float maximumBatchDelaySeconds = 3f;

        [Header("NPC Exit")]
        [Tooltip("Time in seconds for an NPC to walk from its slot back beyond the side it entered from.")]
        [SerializeField, Min(0.05f)] private float exitDurationSeconds = 1.5f;
        [Tooltip("Time in seconds for an NPC to turn around smoothly before leaving. The turn uses a 2D squash-and-flip motion.")]
        [SerializeField, Min(0f)] private float exitTurnDurationSeconds = 0.3f;
        [Tooltip("Small sideways lean in degrees used during the 2D turn.")]
        [SerializeField, Range(0f, 15f)] private float exitTurnLeanDegrees = 2.5f;
        [Tooltip("The narrowest width ratio reached at the middle of the 2D turn. Keep this high for a subtle flip.")]
        [SerializeField, Range(0.6f, 1f)] private float exitTurnMinimumWidth = 0.78f;
        [Tooltip("Small upward lift in canvas pixels used during the 2D turn.")]
        [SerializeField, Min(0f)] private float exitTurnLiftPixels = 2f;
        [Tooltip("Subtle vertical stretch ratio used while the NPC is compressed during the 2D turn.")]
        [SerializeField, Range(0f, 0.25f)] private float exitTurnHeightStretch = 0.02f;

        [Header("Wall Display")]
        [SerializeField] private Canvas sideCanvas;
        [SerializeField] private FoodIsekaiZGameManager gameManager;
        [Tooltip("Optional containers. Leave empty to let the system create the layers automatically.")]
        [SerializeField] private Transform backLayer;
        [SerializeField] private Transform frontLayer;

        private readonly GameObject[] spawnedNpcs = new GameObject[DisplaySlotCount];
        private readonly GameObject[] spawnedNpcPrefabs = new GameObject[DisplaySlotCount];
        private readonly Transform[] customerPanels = new Transform[DisplaySlotCount];
        private readonly Vector2[] npcTargetPositions = new Vector2[DisplaySlotCount];
        private readonly Vector2[] npcMovementPositions = new Vector2[DisplaySlotCount];
        private readonly Vector2[] npcExitTargetPositions = new Vector2[DisplaySlotCount];
        private readonly float[] npcWalkPhases = new float[DisplaySlotCount];
        private readonly float[] npcExitTurnStartTimes = new float[DisplaySlotCount];
        private readonly Vector3[] npcExitTurnStartScales = new Vector3[DisplaySlotCount];
        private readonly Quaternion[] npcExitTurnStartRotations = new Quaternion[DisplaySlotCount];
        private readonly bool[] npcArrivedAtSlots = new bool[DisplaySlotCount];
        private readonly bool[] npcExitingAtSlots = new bool[DisplaySlotCount];
        private readonly bool[] npcUiShownAtSlots = new bool[DisplaySlotCount];
        private readonly float[] npcUiReadyTimes = new float[DisplaySlotCount];
        private readonly int[] npcCustomerGenerations = new int[DisplaySlotCount];
        private readonly List<int> pendingSpawnSlots = new List<int>(DisplaySlotCount);
        private readonly List<GameObject> npcPrefabPool = new List<GameObject>();
        private readonly List<Transform> sortedNpcLayerChildren = new List<Transform>(DisplaySlotCount);
        private readonly Dictionary<GameObject, int> npcPrefabPowerByPrefab =
            new Dictionary<GameObject, int>();
        private int activeWaveNumber = -1;
        private float nextSpawnBatchTime;
        private int lastNpcSpawnBatchSize;
        private GameObject lastSpawnedNpcPrefab;
        private bool npcPoolConfigured;
        private bool npcPrefabConfigurationLogged;

#if UNITY_EDITOR
        private bool repairingNpcPrefabReferences;
#endif

        private void Awake()
        {
#if UNITY_EDITOR
            RepairNpcPrefabReferences();
#endif
            EnsureReferences();
            EnsureNpcLayers();
            CacheCustomerPanels();
            HideLegacyNpcPlaceholders();
        }

        [ContextMenu("Refresh NPC Prefab List")]
        private void RefreshNpcPrefabList()
        {
#if UNITY_EDITOR
            RepairNpcPrefabReferences();
#endif
        }

        private void Start()
        {
            EnsureReferences();
            EnsureNpcLayers();
            CacheCustomerPanels();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying || repairingNpcPrefabReferences)
            {
                return;
            }

            ClampTimingSettings();
            RepairNpcPrefabReferences();
        }
#endif

        private void OnDisable()
        {
            if (!Application.isPlaying)
            {
                activeWaveNumber = -1;
                return;
            }

            ClearSpawnedNpcs();
            activeWaveNumber = -1;
            nextSpawnBatchTime = 0f;
            lastNpcSpawnBatchSize = 0;
        }

        private void Update()
        {
            EnsureReferences();
            if (gameManager == null || sideCanvas == null)
            {
                return;
            }

            if (gameManager.UsesMealWaves &&
                gameManager.CurrentMealWavePhase != MealWavePhase.Active)
            {
                ClearSpawnedNpcs();
                activeWaveNumber = -1;
                return;
            }

            int waveNumber = gameManager.UsesMealWaves
                ? gameManager.CurrentWaveNumber
                : 1;
            if (waveNumber <= 0)
            {
                return;
            }

            if (activeWaveNumber != waveNumber)
            {
                BeginWave(waveNumber);
            }

            SynchronizeNpcSlots();
            AnimateNpcArrivals();
        }

        private void BeginWave(int waveNumber)
        {
            ClearSpawnedNpcs();
            BuildAvailablePrefabPool();
            activeWaveNumber = waveNumber;
            lastNpcSpawnBatchSize = 0;
            nextSpawnBatchTime = Time.time + GetRandomDelay(
                minimumInitialSpawnDelaySeconds,
                maximumInitialSpawnDelaySeconds);
        }

        private void BuildAvailablePrefabPool()
        {
            npcPrefabPool.Clear();
            npcPoolConfigured = false;
            npcPrefabConfigurationLogged = false;

            if (npcPrefabSettings == null)
            {
                LogNpcPrefabConfigurationError();
                return;
            }

            var uniquePrefabs = new HashSet<GameObject>();
            for (int i = 0; i < npcPrefabSettings.Length; i++)
            {
                NpcPrefabSetting setting = npcPrefabSettings[i];
                GameObject prefab = setting != null ? setting.Prefab : null;
                if (!IsUsableNpcPrefab(prefab) || !uniquePrefabs.Add(prefab))
                {
                    continue;
                }

                npcPrefabPool.Add(prefab);
            }

            npcPoolConfigured = npcPrefabPool.Count > 0;
            if (!npcPoolConfigured)
            {
                LogNpcPrefabConfigurationError();
            }
        }

        private void SynchronizeNpcSlots()
        {
            pendingSpawnSlots.Clear();
            for (int slotIndex = 0; slotIndex < spawnedNpcs.Length; slotIndex++)
            {
                ArenaSlot2D slot = gameManager.GetCustomerSlot(slotIndex);
                bool shouldHaveNpc = slot != null && slot.CustomerState != CustomerSlotState.Empty;
                if (!shouldHaveNpc)
                {
                    if (spawnedNpcs[slotIndex] != null && !npcExitingAtSlots[slotIndex])
                    {
                        BeginNpcExit(slotIndex);
                    }

                    continue;
                }

                if (spawnedNpcs[slotIndex] != null)
                {
                    if (!npcExitingAtSlots[slotIndex] &&
                        slot.CustomerGeneration != npcCustomerGenerations[slotIndex])
                    {
                        BeginNpcExit(slotIndex);
                    }

                    continue;
                }

                pendingSpawnSlots.Add(slotIndex);
            }

            if (pendingSpawnSlots.Count == 0 ||
                Time.time < nextSpawnBatchTime ||
                HasNpcEntrancesInProgress())
            {
                return;
            }

            if (!npcPoolConfigured)
            {
                LogNpcPrefabConfigurationError();
                return;
            }

            int maximumBatchSize = lastNpcSpawnBatchSize >= 2
                ? 1
                : Mathf.Clamp(maximumNpcSpawnsPerBatch, 1, 2);
            int batchSize = Mathf.Min(
                UnityEngine.Random.Range(1, maximumBatchSize + 1),
                pendingSpawnSlots.Count);
            int spawnedCount = 0;
            for (int i = 0; i < batchSize; i++)
            {
                int pendingIndex = UnityEngine.Random.Range(0, pendingSpawnSlots.Count);
                int slotIndex = pendingSpawnSlots[pendingIndex];
                pendingSpawnSlots.RemoveAt(pendingIndex);
                if (SpawnNpcAtSlot(slotIndex))
                {
                    spawnedCount++;
                }
            }

            if (spawnedCount > 0)
            {
                lastNpcSpawnBatchSize = spawnedCount;
                nextSpawnBatchTime = Time.time + GetRandomDelay(
                    minimumBatchDelaySeconds,
                    maximumBatchDelaySeconds);
            }
        }

        private bool HasNpcEntrancesInProgress()
        {
            for (int slotIndex = 0; slotIndex < spawnedNpcs.Length; slotIndex++)
            {
                if (spawnedNpcs[slotIndex] != null &&
                    !npcArrivedAtSlots[slotIndex] &&
                    !npcExitingAtSlots[slotIndex])
                {
                    return true;
                }
            }

            return false;
        }

        private bool SpawnNpcAtSlot(int slotIndex)
        {
            SetCustomerPanelVisible(slotIndex, false);
            GameObject prefab = DrawNextNpcPrefab();
            if (prefab == null)
            {
                return false;
            }

            Transform prefabTransform;
            string prefabName;
            try
            {
                prefabName = prefab.name;
                prefabTransform = prefab.transform;
            }
            catch (MissingReferenceException)
            {
                LogNpcPrefabConfigurationError();
                return false;
            }

            if (prefabTransform == null)
            {
                LogNpcPrefabConfigurationError();
                return false;
            }

            Transform finalLayer = IsBackLayerPrefabName(prefabName) ? backLayer : frontLayer;
            Transform movementLayer = backLayer;
            if (finalLayer == null || movementLayer == null)
            {
                Debug.LogWarning(
                    $"[{nameof(FoodIsekaiZNpcWaveSpawner)}] NPC layer is missing; cannot spawn '{prefabName}'.",
                    this);
                return false;
            }

            // Keep every moving NPC behind settled NPCs until it reaches its slot.
            Transform instanceTransform = Instantiate(prefabTransform, movementLayer, false);
            GameObject instance = instanceTransform.gameObject;
            instance.name = prefabName;
            if (!AlignNpcToDisplaySlot(instance, slotIndex))
            {
                Destroy(instance);
                return false;
            }

            ApplyNpcEntranceOrientation(instance, slotIndex, prefab);
            PlaceNpcAtEntrance(instance, slotIndex);
            spawnedNpcs[slotIndex] = instance;
            spawnedNpcPrefabs[slotIndex] = prefab;
            npcCustomerGenerations[slotIndex] = gameManager.GetCustomerSlot(slotIndex)?.CustomerGeneration ?? 0;
            IncreaseNpcPrefabPower(prefab);
            SortNpcLayerChildren(movementLayer);
            return true;
        }

        private void SortNpcLayerChildren(Transform layer)
        {
            sortedNpcLayerChildren.Clear();
            for (int i = 0; i < layer.childCount; i++)
            {
                Transform child = layer.GetChild(i);
                if (GetSpawnedNpcSlotIndex(child.gameObject) >= 0)
                {
                    sortedNpcLayerChildren.Add(child);
                }
            }

            sortedNpcLayerChildren.Sort((left, right) =>
                GetNpcDepthOrder(GetSpawnedNpcSlotIndex(left.gameObject)).CompareTo(
                    GetNpcDepthOrder(GetSpawnedNpcSlotIndex(right.gameObject))));
            for (int i = 0; i < sortedNpcLayerChildren.Count; i++)
            {
                sortedNpcLayerChildren[i].SetSiblingIndex(i);
            }
        }

        private int GetSpawnedNpcSlotIndex(GameObject instance)
        {
            for (int i = 0; i < spawnedNpcs.Length; i++)
            {
                if (spawnedNpcs[i] == instance)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int GetNpcDepthOrder(int slotIndex)
        {
            int slotsPerSide = DisplaySlotCount / 2;
            return slotIndex < slotsPerSide
                ? slotsPerSide - 1 - slotIndex
                : slotIndex;
        }

        private void BeginNpcExit(int slotIndex)
        {
            RectTransform canvasRect = sideCanvas.GetComponent<RectTransform>();
            if (canvasRect == null)
            {
                DestroyNpcAtSlot(slotIndex);
                return;
            }

            float exitDistance = Mathf.Max(
                1f,
                canvasRect.rect.width * entranceDistanceCanvasMultiplier);
            float direction = slotIndex < DisplaySlotCount / 2 ? -1f : 1f;
            npcExitTargetPositions[slotIndex] = npcTargetPositions[slotIndex] +
                new Vector2(direction * exitDistance, 0f);
            npcExitingAtSlots[slotIndex] = true;
            npcArrivedAtSlots[slotIndex] = false;
            npcUiShownAtSlots[slotIndex] = false;
            npcUiReadyTimes[slotIndex] = 0f;
            SetCustomerPanelVisible(slotIndex, false);
            RectTransform npcRect = spawnedNpcs[slotIndex].GetComponent<RectTransform>();
            if (npcRect != null)
            {
                npcExitTurnStartTimes[slotIndex] = Time.time;
                npcExitTurnStartScales[slotIndex] = npcRect.localScale;
                npcExitTurnStartRotations[slotIndex] = npcRect.localRotation;
            }

            MoveNpcToMovementLayer(slotIndex);
        }

        private bool AnimateNpcExitTurn(int slotIndex, RectTransform npcRect)
        {
            float turnDuration = Mathf.Max(0f, exitTurnDurationSeconds);
            Vector3 startScale = npcExitTurnStartScales[slotIndex];
            float startScaleX = Mathf.Max(0.0001f, Mathf.Abs(startScale.x));
            float startScaleXSign = startScale.x < 0f ? -1f : 1f;
            if (turnDuration <= 0f)
            {
                startScale.x = -startScaleX * startScaleXSign;
                npcRect.localScale = startScale;
                npcRect.localRotation = npcExitTurnStartRotations[slotIndex];
                return true;
            }

            float turnProgress = Mathf.Clamp01(
                (Time.time - npcExitTurnStartTimes[slotIndex]) / turnDuration);
            float collapseProgress = Mathf.Sin(turnProgress * Mathf.PI);
            float easedCollapseProgress = Mathf.SmoothStep(0f, 1f, collapseProgress);
            float widthRatio = Mathf.Lerp(
                1f,
                Mathf.Clamp(exitTurnMinimumWidth, 0.6f, 1f),
                easedCollapseProgress);
            float heightRatio = 1f +
                Mathf.Clamp01(exitTurnHeightStretch) * easedCollapseProgress;
            float scaleXSign = turnProgress < 0.5f
                ? startScaleXSign
                : -startScaleXSign;
            startScale.x = startScaleX * widthRatio * scaleXSign;
            startScale.y *= heightRatio;
            npcRect.localScale = startScale;

            float exitDirection = slotIndex < DisplaySlotCount / 2 ? -1f : 1f;
            float lean = collapseProgress * exitTurnLeanDegrees * exitDirection;
            npcRect.localRotation = npcExitTurnStartRotations[slotIndex] *
                Quaternion.Euler(0f, 0f, lean);
            return turnProgress >= 1f;
        }

        private void AnimateNpcExit(int slotIndex, RectTransform canvasRect)
        {
            GameObject instance = spawnedNpcs[slotIndex];
            if (instance == null)
            {
                npcExitingAtSlots[slotIndex] = false;
                return;
            }

            RectTransform npcRect = instance.GetComponent<RectTransform>();
            if (npcRect == null)
            {
                DestroyNpcAtSlot(slotIndex);
                return;
            }

            bool turnCompleted = AnimateNpcExitTurn(slotIndex, npcRect);
            if (!turnCompleted)
            {
                float turnLift = Mathf.Sin(
                    Mathf.Clamp01(
                        (Time.time - npcExitTurnStartTimes[slotIndex]) /
                        Mathf.Max(0.05f, exitTurnDurationSeconds)) * Mathf.PI) *
                    Mathf.Max(0f, exitTurnLiftPixels);
                npcRect.anchoredPosition = npcMovementPositions[slotIndex] +
                    Vector2.up * turnLift;
                return;
            }

            float exitDistance = Mathf.Max(
                1f,
                canvasRect.rect.width * entranceDistanceCanvasMultiplier);
            float exitSpeed = exitDistance / Mathf.Max(0.05f, exitDurationSeconds);
            Vector2 exitPosition = Vector2.MoveTowards(
                npcMovementPositions[slotIndex],
                npcExitTargetPositions[slotIndex],
                exitSpeed * Time.deltaTime);
            npcMovementPositions[slotIndex] = exitPosition;
            if (Vector2.Distance(exitPosition, npcExitTargetPositions[slotIndex]) <= 0.01f)
            {
                npcRect.anchoredPosition = npcExitTargetPositions[slotIndex];
                DestroyNpcAtSlot(slotIndex);
                return;
            }

            npcWalkPhases[slotIndex] +=
                Time.deltaTime * walkingBobFrequency * Mathf.PI * 2f;
            float walkingBobOffset = Mathf.Sin(npcWalkPhases[slotIndex]) * walkingBobHeight;
            npcRect.anchoredPosition = exitPosition + Vector2.up * walkingBobOffset;
        }

        private void AnimateNpcArrivals()
        {
            RectTransform canvasRect = sideCanvas.GetComponent<RectTransform>();
            if (canvasRect == null)
            {
                return;
            }

            float entranceDistance = Mathf.Max(
                1f,
                canvasRect.rect.width * entranceDistanceCanvasMultiplier);
            float nearTargetDistance = Mathf.Max(
                1f,
                canvasRect.rect.width * nearTargetDistanceCanvasMultiplier);
            for (int slotIndex = 0; slotIndex < spawnedNpcs.Length; slotIndex++)
            {
                if (spawnedNpcs[slotIndex] == null)
                {
                    continue;
                }

                if (npcExitingAtSlots[slotIndex])
                {
                    AnimateNpcExit(slotIndex, canvasRect);
                    continue;
                }

                if (npcArrivedAtSlots[slotIndex])
                {
                    if (!npcUiShownAtSlots[slotIndex] && Time.time >= npcUiReadyTimes[slotIndex])
                    {
                        ShowNpcUi(slotIndex);
                    }

                    continue;
                }

                RectTransform npcRect = spawnedNpcs[slotIndex].GetComponent<RectTransform>();
                if (npcRect == null)
                {
                    MarkNpcArrived(slotIndex);
                    continue;
                }

                float distanceToTarget = Vector2.Distance(
                    npcMovementPositions[slotIndex],
                    npcTargetPositions[slotIndex]);
                float movementDuration = distanceToTarget <= nearTargetDistance
                    ? nearTargetDurationSeconds
                    : entranceDurationSeconds;
                float movementSpeed = entranceDistance /
                    Mathf.Max(0.05f, movementDuration);
                Vector2 movementPosition = Vector2.MoveTowards(
                    npcMovementPositions[slotIndex],
                    npcTargetPositions[slotIndex],
                    movementSpeed * Time.deltaTime);

                npcMovementPositions[slotIndex] = movementPosition;
                bool reachedTarget = Vector2.Distance(
                    movementPosition,
                    npcTargetPositions[slotIndex]) <= 0.01f;
                if (reachedTarget)
                {
                    npcRect.anchoredPosition = npcTargetPositions[slotIndex];
                    npcMovementPositions[slotIndex] = npcTargetPositions[slotIndex];
                    MarkNpcArrived(slotIndex);
                    continue;
                }

                npcWalkPhases[slotIndex] +=
                    Time.deltaTime * walkingBobFrequency * Mathf.PI * 2f;
                float walkingBobOffset = Mathf.Sin(npcWalkPhases[slotIndex]) *
                    walkingBobHeight;
                npcRect.anchoredPosition = movementPosition + Vector2.up * walkingBobOffset;
            }
        }

        private void MarkNpcArrived(int slotIndex)
        {
            npcArrivedAtSlots[slotIndex] = true;
            npcExitingAtSlots[slotIndex] = false;
            npcMovementPositions[slotIndex] = npcTargetPositions[slotIndex];
            npcWalkPhases[slotIndex] = 0f;
            MoveNpcToFinalLayer(slotIndex);
            npcUiShownAtSlots[slotIndex] = false;
            npcUiReadyTimes[slotIndex] = Time.time + Mathf.Max(0f, arrivalHoldDurationSeconds);
            SetCustomerPanelVisible(slotIndex, false);
        }

        private void MoveNpcToFinalLayer(int slotIndex)
        {
            GameObject instance = spawnedNpcs[slotIndex];
            if (instance == null)
            {
                return;
            }

            Transform finalLayer = IsBackLayerPrefabName(instance.name) ? backLayer : frontLayer;
            MoveNpcToLayer(slotIndex, finalLayer);
        }

        private void MoveNpcToMovementLayer(int slotIndex)
        {
            MoveNpcToLayer(slotIndex, backLayer);
        }

        private void MoveNpcToLayer(int slotIndex, Transform targetLayer)
        {
            GameObject instance = spawnedNpcs[slotIndex];
            if (instance == null)
            {
                return;
            }

            Transform currentLayer = instance.transform.parent;
            if (targetLayer == null || currentLayer == targetLayer)
            {
                return;
            }

            RectTransform instanceRect = instance.GetComponent<RectTransform>();
            Vector2 anchoredPosition = instanceRect != null
                ? instanceRect.anchoredPosition
                : Vector2.zero;
            instance.transform.SetParent(targetLayer, false);
            if (instanceRect != null)
            {
                instanceRect.anchoredPosition = anchoredPosition;
            }

            if (currentLayer != null)
            {
                SortNpcLayerChildren(currentLayer);
            }

            SortNpcLayerChildren(targetLayer);
        }

        private void ShowNpcUi(int slotIndex)
        {
            npcUiShownAtSlots[slotIndex] = true;
            SetCustomerPanelVisible(slotIndex, true);
            StartCustomerTimerForSlot(slotIndex);
        }

        private void StartCustomerTimerForSlot(int slotIndex)
        {
            ArenaSlot2D slot = gameManager.GetCustomerSlot(slotIndex);
            slot?.StartCustomerTimer();
        }

        private GameObject DrawNextNpcPrefab()
        {
            int lowestPower = int.MaxValue;
            int lowestPowerPrefabCount = 0;
            bool lastPrefabHasLowestPower = false;
            for (int i = 0; i < npcPrefabPool.Count; i++)
            {
                GameObject prefab = npcPrefabPool[i];
                if (!IsUsableNpcPrefab(prefab) || IsNpcPrefabInUse(prefab))
                {
                    continue;
                }

                int power = GetNpcPrefabPower(prefab);
                if (power < lowestPower)
                {
                    lowestPower = power;
                    lowestPowerPrefabCount = 1;
                    lastPrefabHasLowestPower = prefab == lastSpawnedNpcPrefab;
                }
                else if (power == lowestPower)
                {
                    lowestPowerPrefabCount++;
                    lastPrefabHasLowestPower |= prefab == lastSpawnedNpcPrefab;
                }
            }

            if (lowestPowerPrefabCount == 0)
            {
                return null;
            }

            // Avoid an immediate repeat when another prefab has the same lowest Power.
            bool excludeLastSpawnedPrefab = lastPrefabHasLowestPower && lowestPowerPrefabCount > 1;
            int selectablePrefabCount = excludeLastSpawnedPrefab
                ? lowestPowerPrefabCount - 1
                : lowestPowerPrefabCount;
            int selectedPrefabIndex = UnityEngine.Random.Range(0, selectablePrefabCount);
            for (int i = 0; i < npcPrefabPool.Count; i++)
            {
                GameObject prefab = npcPrefabPool[i];
                if (!IsUsableNpcPrefab(prefab) || IsNpcPrefabInUse(prefab))
                {
                    continue;
                }

                if (GetNpcPrefabPower(prefab) != lowestPower ||
                    (excludeLastSpawnedPrefab && prefab == lastSpawnedNpcPrefab))
                {
                    continue;
                }

                if (selectedPrefabIndex == 0)
                {
                    return prefab;
                }

                selectedPrefabIndex--;
            }

            return null;
        }

        private int GetNpcPrefabPower(GameObject prefab)
        {
            if (prefab == null)
            {
                return int.MaxValue;
            }

            return npcPrefabPowerByPrefab.TryGetValue(prefab, out int power)
                ? power
                : 0;
        }

        private void IncreaseNpcPrefabPower(GameObject prefab)
        {
            if (prefab == null)
            {
                return;
            }

            int nextPower = GetNpcPrefabPower(prefab) + 1;
            npcPrefabPowerByPrefab[prefab] = nextPower;
            lastSpawnedNpcPrefab = prefab;
        }

        [ContextMenu("Reset NPC Power")]
        private void ResetNpcPower()
        {
            npcPrefabPowerByPrefab.Clear();
            lastSpawnedNpcPrefab = null;
        }

        private bool IsNpcPrefabInUse(GameObject prefab)
        {
            for (int slotIndex = 0; slotIndex < spawnedNpcPrefabs.Length; slotIndex++)
            {
                if (spawnedNpcs[slotIndex] != null &&
                    spawnedNpcPrefabs[slotIndex] == prefab)
                {
                    return true;
                }
            }

            return false;
        }

        private void ApplyNpcEntranceOrientation(GameObject instance, int slotIndex, GameObject prefab)
        {
            bool enteringFromLeft = slotIndex < DisplaySlotCount / 2;
            if (!ShouldFlipForEntry(prefab, enteringFromLeft))
            {
                return;
            }

            RectTransform instanceRect = instance.GetComponent<RectTransform>();
            if (instanceRect == null)
            {
                return;
            }

            Vector3 scale = instanceRect.localScale;
            instanceRect.localScale = new Vector3(-scale.x, scale.y, scale.z);
        }

        private bool ShouldFlipForEntry(GameObject prefab, bool enteringFromLeft)
        {
            if (npcPrefabSettings == null)
            {
                return false;
            }

            for (int i = 0; i < npcPrefabSettings.Length; i++)
            {
                NpcPrefabSetting setting = npcPrefabSettings[i];
                if (setting != null && setting.Prefab == prefab)
                {
                    return enteringFromLeft
                        ? setting.FlipWhenEnteringFromLeft
                        : setting.FlipWhenEnteringFromRight;
                }
            }

            return false;
        }

        private void LogNpcPrefabConfigurationError()
        {
            if (npcPrefabConfigurationLogged)
            {
                return;
            }

            npcPrefabConfigurationLogged = true;
            Debug.LogError(
                $"[{nameof(FoodIsekaiZNpcWaveSpawner)}] ไม่พบ NPC Prefab ที่ใช้งานได้ใน NPC Prefabs — ตรวจรายการ Prefab ใน Inspector.",
                this);
        }

        private void DestroyNpcAtSlot(int slotIndex)
        {
            GameObject instance = spawnedNpcs[slotIndex];
            spawnedNpcPrefabs[slotIndex] = null;
            npcMovementPositions[slotIndex] = Vector2.zero;
            npcExitTargetPositions[slotIndex] = Vector2.zero;
            npcWalkPhases[slotIndex] = 0f;
            npcArrivedAtSlots[slotIndex] = false;
            npcExitingAtSlots[slotIndex] = false;
            npcUiShownAtSlots[slotIndex] = false;
            npcUiReadyTimes[slotIndex] = 0f;
            npcCustomerGenerations[slotIndex] = 0;
            SetCustomerPanelVisible(slotIndex, false);
            if (instance == null)
            {
                spawnedNpcs[slotIndex] = null;
                return;
            }

            Destroy(instance);
            spawnedNpcs[slotIndex] = null;
        }

        private void ClearSpawnedNpcs()
        {
            for (int i = 0; i < spawnedNpcs.Length; i++)
            {
                DestroyNpcAtSlot(i);
            }
        }

        private void ClampTimingSettings()
        {
            entranceDurationSeconds = Mathf.Max(0.05f, entranceDurationSeconds);
            nearTargetDistanceCanvasMultiplier = Mathf.Max(
                0.01f,
                nearTargetDistanceCanvasMultiplier);
            nearTargetDurationSeconds = Mathf.Max(0.05f, nearTargetDurationSeconds);
            arrivalHoldDurationSeconds = Mathf.Max(0f, arrivalHoldDurationSeconds);
            walkingBobHeight = Mathf.Max(0f, walkingBobHeight);
            walkingBobFrequency = Mathf.Max(0.1f, walkingBobFrequency);
            maximumNpcSpawnsPerBatch = Mathf.Clamp(maximumNpcSpawnsPerBatch, 1, 2);
            minimumInitialSpawnDelaySeconds = Mathf.Max(0f, minimumInitialSpawnDelaySeconds);
            maximumInitialSpawnDelaySeconds = Mathf.Max(
                minimumInitialSpawnDelaySeconds,
                maximumInitialSpawnDelaySeconds);
            minimumBatchDelaySeconds = Mathf.Max(0.05f, minimumBatchDelaySeconds);
            maximumBatchDelaySeconds = Mathf.Max(
                minimumBatchDelaySeconds,
                maximumBatchDelaySeconds);
            exitTurnDurationSeconds = Mathf.Max(0f, exitTurnDurationSeconds);
            exitTurnLeanDegrees = Mathf.Clamp(exitTurnLeanDegrees, 0f, 15f);
            exitTurnMinimumWidth = Mathf.Clamp(exitTurnMinimumWidth, 0.6f, 1f);
            exitTurnLiftPixels = Mathf.Max(0f, exitTurnLiftPixels);
            exitTurnHeightStretch = Mathf.Clamp(exitTurnHeightStretch, 0f, 0.25f);
        }

        private static float GetRandomDelay(float minimumSeconds, float maximumSeconds)
        {
            float minimum = Mathf.Max(0f, minimumSeconds);
            float maximum = Mathf.Max(minimum, maximumSeconds);
            return UnityEngine.Random.Range(minimum, maximum);
        }

#if UNITY_EDITOR
        private void RepairNpcPrefabReferences()
        {
            string searchFolder = string.IsNullOrWhiteSpace(npcPrefabFolder)
                ? DefaultNpcPrefabFolder
                : npcPrefabFolder.Trim();
            string namePrefix = string.IsNullOrWhiteSpace(npcPrefabNamePrefix)
                ? DefaultNpcPrefabNamePrefix
                : npcPrefabNamePrefix.Trim();
            if (!AssetDatabase.IsValidFolder(searchFolder))
            {
                return;
            }

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { searchFolder });
            var discoveredPrefabs = new List<GameObject>();
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                string assetName = Path.GetFileNameWithoutExtension(assetPath);
                if (!assetName.StartsWith(namePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab != null)
                {
                    discoveredPrefabs.Add(prefab);
                }
            }

            discoveredPrefabs.Sort((left, right) =>
                string.Compare(left.name, right.name, StringComparison.OrdinalIgnoreCase));

            if (discoveredPrefabs.Count == 0)
            {
                return;
            }

            NpcPrefabSetting[] synchronizedSettings = BuildNpcPrefabSettings(discoveredPrefabs);
            bool prefabSettingsChanged = !HasSameNpcPrefabSettings(synchronizedSettings);
            if (!prefabSettingsChanged)
            {
                return;
            }

            repairingNpcPrefabReferences = true;
            npcPrefabSettings = synchronizedSettings;

            repairingNpcPrefabReferences = false;

            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(this);
                if (gameObject.scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(gameObject.scene);
                }
            }
        }

        private NpcPrefabSetting[] BuildNpcPrefabSettings(List<GameObject> discoveredPrefabs)
        {
            var synchronizedSettings = new NpcPrefabSetting[discoveredPrefabs.Count];
            for (int i = 0; i < discoveredPrefabs.Count; i++)
            {
                GameObject prefab = discoveredPrefabs[i];
                bool flipWhenEnteringFromLeft = ShouldFlipForEntry(prefab, true);
                bool flipWhenEnteringFromRight = ShouldFlipForEntry(prefab, false);
                synchronizedSettings[i] = new NpcPrefabSetting(
                    prefab,
                    flipWhenEnteringFromLeft,
                    flipWhenEnteringFromRight);
            }

            return synchronizedSettings;
        }

        private bool HasSameNpcPrefabSettings(NpcPrefabSetting[] synchronizedSettings)
        {
            if (npcPrefabSettings == null || npcPrefabSettings.Length != synchronizedSettings.Length)
            {
                return false;
            }

            for (int i = 0; i < synchronizedSettings.Length; i++)
            {
                NpcPrefabSetting current = npcPrefabSettings[i];
                NpcPrefabSetting synchronized = synchronizedSettings[i];
                if (current == null || current.Prefab != synchronized.Prefab ||
                    current.FlipWhenEnteringFromLeft != synchronized.FlipWhenEnteringFromLeft ||
                    current.FlipWhenEnteringFromRight != synchronized.FlipWhenEnteringFromRight)
                {
                    return false;
                }
            }

            return true;
        }
#endif

        private bool AlignNpcToDisplaySlot(GameObject instance, int slotIndex)
        {
            RectTransform instanceRect = instance.GetComponent<RectTransform>();
            RectTransform canvasRect = sideCanvas.GetComponent<RectTransform>();
            Transform target = FindNestedTransform(sideCanvas.transform, $"CustomerPanel{slotIndex + 1}");
            if (instanceRect == null || canvasRect == null || target == null)
            {
                return false;
            }

            float authoredVerticalPosition = instanceRect.anchoredPosition.y;
            Vector3 targetLocalPosition = canvasRect.InverseTransformPoint(target.position);
            instanceRect.anchorMin = new Vector2(0.5f, 0.5f);
            instanceRect.anchorMax = new Vector2(0.5f, 0.5f);
            instanceRect.anchoredPosition = new Vector2(
                targetLocalPosition.x,
                authoredVerticalPosition);
            instanceRect.localRotation = Quaternion.identity;
            npcTargetPositions[slotIndex] = instanceRect.anchoredPosition;
            return true;
        }

        private void PlaceNpcAtEntrance(GameObject instance, int slotIndex)
        {
            RectTransform instanceRect = instance.GetComponent<RectTransform>();
            RectTransform canvasRect = sideCanvas.GetComponent<RectTransform>();
            if (instanceRect == null || canvasRect == null)
            {
                return;
            }

            float entranceDistance = Mathf.Max(
                1f,
                canvasRect.rect.width * entranceDistanceCanvasMultiplier);
            float direction = slotIndex < DisplaySlotCount / 2 ? -1f : 1f;
            instanceRect.anchoredPosition = npcTargetPositions[slotIndex] +
                new Vector2(direction * entranceDistance, 0f);
            npcExitTargetPositions[slotIndex] =
                npcTargetPositions[slotIndex] + new Vector2(direction * entranceDistance, 0f);
            npcMovementPositions[slotIndex] = instanceRect.anchoredPosition;
            npcWalkPhases[slotIndex] = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            npcArrivedAtSlots[slotIndex] = false;
            npcExitingAtSlots[slotIndex] = false;
            npcUiShownAtSlots[slotIndex] = false;
            npcUiReadyTimes[slotIndex] = 0f;
        }

        private void CacheCustomerPanels()
        {
            if (sideCanvas == null)
            {
                return;
            }

            for (int i = 0; i < customerPanels.Length; i++)
            {
                customerPanels[i] = FindNestedTransform(sideCanvas.transform, $"CustomerPanel{i + 1}");
            }
        }

        private void SetCustomerPanelVisible(int slotIndex, bool visible)
        {
            if (slotIndex < 0 || slotIndex >= customerPanels.Length || customerPanels[slotIndex] == null)
            {
                return;
            }

            customerPanels[slotIndex].gameObject.SetActive(visible);
        }

        private void EnsureReferences()
        {
            if (sideCanvas == null)
            {
                sideCanvas = GetComponent<Canvas>();
                if (sideCanvas == null)
                {
                    sideCanvas = FindAnyObjectByType<Canvas>();
                }
            }

            if (gameManager == null)
            {
                gameManager = FindAnyObjectByType<FoodIsekaiZGameManager>();
            }
        }

        private void EnsureNpcLayers()
        {
            if (sideCanvas == null)
            {
                return;
            }

            Transform canvasTransform = sideCanvas.transform;
            backLayer = backLayer != null ? backLayer : FindDirectChild(canvasTransform, BackLayerName);
            frontLayer = frontLayer != null ? frontLayer : FindDirectChild(canvasTransform, FrontLayerName);
            if (backLayer == null)
            {
                backLayer = CreateLayer(canvasTransform, BackLayerName);
            }

            if (frontLayer == null)
            {
                frontLayer = CreateLayer(canvasTransform, FrontLayerName);
            }

            ConfigureLayerRectTransform(backLayer);
            ConfigureLayerRectTransform(frontLayer);
            SetLayerOrder(canvasTransform);
        }

        private static Transform CreateLayer(Transform canvasTransform, string layerName)
        {
            var layerObject = new GameObject(layerName, typeof(RectTransform));
            Transform layer = layerObject.transform;
            layer.SetParent(canvasTransform, false);
            return layer;
        }

        private static void ConfigureLayerRectTransform(Transform layer)
        {
            RectTransform rect = layer as RectTransform;
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localPosition = Vector3.zero;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
        }

        private void SetLayerOrder(Transform canvasTransform)
        {
            int menuIndex = FindDisplayContentIndex(canvasTransform);
            int maxIndex = Mathf.Max(0, canvasTransform.childCount - 1);
            backLayer.SetSiblingIndex(Mathf.Clamp(menuIndex, 0, maxIndex));
            frontLayer.SetSiblingIndex(Mathf.Clamp(menuIndex + 1, 0, maxIndex));
        }

        private int FindDisplayContentIndex(Transform canvasTransform)
        {
            int contentIndex = 0;
            for (int i = 0; i < canvasTransform.childCount; i++)
            {
                Transform child = canvasTransform.GetChild(i);
                if (child == backLayer || child == frontLayer)
                {
                    continue;
                }

                if (string.Equals(child.name, "Menu", StringComparison.OrdinalIgnoreCase) ||
                    child.name.StartsWith("CustomerPanel", StringComparison.OrdinalIgnoreCase))
                {
                    return contentIndex;
                }

                contentIndex++;
            }

            return contentIndex;
        }

        private void HideLegacyNpcPlaceholders()
        {
            if (sideCanvas == null)
            {
                return;
            }

            Transform[] children = sideCanvas.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child == sideCanvas.transform ||
                    child.IsChildOf(backLayer) ||
                    child.IsChildOf(frontLayer) ||
                    !IsLegacyNpcName(child.name))
                {
                    continue;
                }

                child.gameObject.SetActive(false);
            }
        }

        private static bool IsLegacyNpcName(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName) ||
                !objectName.StartsWith("NPC", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            int index = 3;
            while (index < objectName.Length && char.IsDigit(objectName[index]))
            {
                index++;
            }

            return index > 3;
        }

        private static bool IsBackLayerPrefabName(string prefabName)
        {
            return !string.IsNullOrWhiteSpace(prefabName) &&
                prefabName.EndsWith("-1", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsUsableNpcPrefab(GameObject prefab)
        {
            if (prefab == null)
            {
                return false;
            }

            try
            {
                return prefab.transform != null;
            }
            catch (MissingReferenceException)
            {
                return false;
            }
        }

        private static Transform FindDirectChild(Transform root, string childName)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (string.Equals(child.name, childName, StringComparison.Ordinal))
                {
                    return child;
                }
            }

            return null;
        }

        private static Transform FindNestedTransform(Transform root, string targetName)
        {
            if (string.Equals(root.name, targetName, StringComparison.Ordinal))
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform match = FindNestedTransform(root.GetChild(i), targetName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }
    }
}
