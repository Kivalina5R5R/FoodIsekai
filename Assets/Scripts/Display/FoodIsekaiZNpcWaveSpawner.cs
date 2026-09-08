using System;
using System.Collections.Generic;
using System.IO;
using FoodIsekaiZ.Gameplay;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace FoodIsekaiZ.Display
{

    // Coordinates the customer NPCs shown for each wave on the wall display.
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
        [Tooltip("Entrance distance from the canvas center, in canvas widths. Also sets the reference travel distance for exits.")]
        [SerializeField, Min(0.1f)] private float entranceDistanceCanvasMultiplier = 0.75f;
        [InspectorName("Walking Speed Canvas Multiplier")]
        [Tooltip("Minimum walking speed in canvas widths per second. Each NPC samples its own pace on spawn and keeps it for both entrance and exit.")]
        [SerializeField, Min(0.01f)] private float entranceSpeedCanvasMultiplier = 0.264f;
        [Tooltip("Maximum random increase above the walking speed, in percent. 40 gives each NPC a pace between 100% and 140% of the base speed.")]
        [SerializeField, Range(0f, 100f)] private float maximumWalkingSpeedIncreasePercent = 40f;
        [Tooltip("Seconds for the final short step to slow from walking speed to a complete stop.")]
        [SerializeField, Min(0.05f)] private float arrivalStoppingSeconds = 0.28f;
        [Tooltip("Distance from an inner T slot where the NPC steps forward, capped at 6% of the canvas width to keep it behind the preceding customer while passing. Blends during the stopping step and finishes before arrival.")]
        [SerializeField, Range(0.01f, 0.06f)] private float foregroundApproachDistanceCanvasMultiplier = 0.06f;
        [Tooltip("Vertical offset of the rear walking lane, in canvas pixels. Blends to zero as T2-T5 step forward without changing their final size or position.")]
        [SerializeField, Min(0f)] private float foregroundApproachRearOffsetPixels = 18f;
        [Tooltip("Minimum horizontal gap in canvas widths between NPCs entering together from the same side. The nearer slot leads.")]
        [SerializeField, Min(0f)] private float pairedEntranceSpacingCanvasMultiplier = 0.14f;
        [Tooltip("Time in seconds an NPC remains at its assigned T slot before the food UI and timer are shown.")]
        [SerializeField, Min(0f)] private float arrivalHoldDurationSeconds = 1f;

        [Header("NPC Walking Motion")]
        [Tooltip("Vertical lift in canvas pixels per walking step. The entrance stride ends on a grounded step at the destination.")]
        [SerializeField, Min(0f)] private float walkingBobHeight = 12f;
        [Tooltip("Number of up-and-down walking cycles per second.")]
        [SerializeField, Min(0.1f)] private float walkingBobFrequency = 2.2f;

        [Header("NPC Idle Breathing")]
        [Tooltip("Seconds for one gentle inhale and exhale while the NPC stands at its slot.")]
        [SerializeField, Min(0.2f)] private float idleBreathingCycleSeconds = 3.6f;
        [Tooltip("Height expansion of the NPC image during an inhale; its lower edge stays grounded.")]
        [SerializeField, Range(0f, 0.03f)] private float idleBreathingHeight = 0.008f;
        [Tooltip("Subtle width expansion of the NPC image during an inhale.")]
        [SerializeField, Range(0f, 0.02f)] private float idleBreathingWidth = 0.0025f;
        [Tooltip("Seconds to blend breathing in after arrival and out when leaving.")]
        [SerializeField, Min(0.01f)] private float idleBreathingBlendSeconds = 0.45f;

        [Header("NPC Emoji")]
        [Tooltip("Seconds after the food menu appears before the NPC emoji becomes visible.")]
        [SerializeField, Min(0f)] private float emojiDelayAfterMenuSeconds = 0.5f;
        [Tooltip("Seconds to show Angry at the assigned slot after an order expires, before the NPC starts leaving.")]
        [SerializeField, Min(0f)] private float angryHoldDurationSeconds = 1.5f;

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
        private readonly CustomerPanelPresentation[] customerPanelPresentations =
            new CustomerPanelPresentation[DisplaySlotCount];
        private FoodIsekaiZGameManager subscribedGameManager;
        private readonly Vector2[] npcTargetPositions = new Vector2[DisplaySlotCount];
        private readonly Vector2[] npcMovementPositions = new Vector2[DisplaySlotCount];
        private readonly Vector2[] npcApproachStartPositions = new Vector2[DisplaySlotCount];
        private readonly float[] npcApproachElapsedSeconds = new float[DisplaySlotCount];
        private readonly float[] npcApproachDurations = new float[DisplaySlotCount];
        private readonly bool[] npcApproachingAtSlots = new bool[DisplaySlotCount];
        private readonly NpcIdleBreathing[] npcIdleBreathing = new NpcIdleBreathing[DisplaySlotCount];
        private readonly NpcForegroundBlend[] npcForegroundBlends = new NpcForegroundBlend[DisplaySlotCount];
        private readonly NpcEmojiPresentation[] npcEmojiPresentations = new NpcEmojiPresentation[DisplaySlotCount];
        private readonly float[] npcEmojiReadyTimes = new float[DisplaySlotCount];
        private readonly bool[] npcOrdersExpired = new bool[DisplaySlotCount];
        private readonly float[] npcAngryUntilTimes = new float[DisplaySlotCount];
        private System.Random npcEmojiRandom;
        private readonly Vector2[] npcExitTargetPositions = new Vector2[DisplaySlotCount];
        private readonly float[] npcWalkingSpeedCanvasMultipliers = new float[DisplaySlotCount];
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
        private readonly List<int> spawnBatchSlots = new List<int>(2);
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
            UnsubscribeFromCustomerEvents();
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
            SubscribeToCustomerEvents();
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
                UpdateNpcEmoji(slotIndex, slot);
                // Completing keeps the NPC at its slot. The money becomes available
                // after the success celebration, allowing the NPC to leave.
                bool shouldHaveNpc = slot != null && slot.HasCustomer;
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

                if (customerPanelPresentations[slotIndex] == null ||
                    !customerPanelPresentations[slotIndex].IsCelebrating)
                {
                    pendingSpawnSlots.Add(slotIndex);
                }
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
            spawnBatchSlots.Clear();
            for (int i = 0; i < batchSize; i++)
            {
                int pendingIndex = UnityEngine.Random.Range(0, pendingSpawnSlots.Count);
                int slotIndex = pendingSpawnSlots[pendingIndex];
                pendingSpawnSlots.RemoveAt(pendingIndex);
                spawnBatchSlots.Add(slotIndex);
            }

            // Send the nearer customer first when a pair shares an entrance,
            // so the farther customer starts behind it even when their paces differ.
            spawnBatchSlots.Sort((left, right) => GetNpcDepthOrder(right).CompareTo(GetNpcDepthOrder(left)));
            int spawnedCount = 0;
            for (int i = 0; i < spawnBatchSlots.Count; i++)
            {
                int slotIndex = spawnBatchSlots[i];
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

            // Start behind settled NPCs; inner slots step forward only near their destination.
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
            InitializeNpcEmoji(instance, slotIndex);
            RectTransform visualBody = FindNestedTransform(instance.transform, "Image") as RectTransform;
            if (visualBody != null)
            {
                NpcIdleBreathing breathing = instance.AddComponent<NpcIdleBreathing>();
                breathing.Initialize(visualBody, idleBreathingCycleSeconds, idleBreathingHeight,
                    idleBreathingWidth, idleBreathingBlendSeconds,
                    npcWalkPhases[slotIndex] / (Mathf.PI * 2f),
                    FindDirectChild(instance.transform, "Emoji") as RectTransform);
                npcIdleBreathing[slotIndex] = breathing;
            }

            spawnedNpcs[slotIndex] = instance;
            spawnedNpcPrefabs[slotIndex] = prefab;
            BeginNpcForegroundApproach(slotIndex, instance.transform as RectTransform, visualBody, finalLayer);
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
                if (spawnedNpcs[i] == instance ||
                    (npcForegroundBlends[i] != null && npcForegroundBlends[i].ForegroundRoot == instance))
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
            if (IsNpcShowingAngryHold(slotIndex))
            {
                return;
            }

            CustomerPanelPresentation presentation = customerPanelPresentations[slotIndex];
            if (presentation != null && presentation.IsCelebrating)
            {
                return;
            }

            RectTransform canvasRect = sideCanvas.GetComponent<RectTransform>();
            if (canvasRect == null)
            {
                DestroyNpcAtSlot(slotIndex);
                return;
            }

            // A customer can leave during entry. Keep its current lane height when cancelling the blend.
            if (npcForegroundBlends[slotIndex] != null)
            {
                npcMovementPositions[slotIndex] += Vector2.up * npcForegroundBlends[slotIndex].RearLaneOffset;
                ClearNpcForegroundApproach(slotIndex);
            }

            float exitDistance = Mathf.Max(
                1f,
                canvasRect.rect.width * entranceDistanceCanvasMultiplier);
            float direction = slotIndex < DisplaySlotCount / 2 ? -1f : 1f;
            npcExitTargetPositions[slotIndex] = npcTargetPositions[slotIndex] +
                new Vector2(direction * exitDistance, 0f);
            npcExitingAtSlots[slotIndex] = true;
            npcArrivedAtSlots[slotIndex] = false;
            npcApproachingAtSlots[slotIndex] = false;
            npcIdleBreathing[slotIndex]?.EndIdle();
            if (!npcOrdersExpired[slotIndex])
            {
                npcEmojiPresentations[slotIndex]?.Hide();
            }
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

            MoveNpcToExitLayer(slotIndex);
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

        private void AnimateNpcExit(int slotIndex, float movementSpeed)
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

            Vector2 previousPosition = npcMovementPositions[slotIndex];
            Vector2 exitPosition = Vector2.MoveTowards(
                previousPosition,
                npcExitTargetPositions[slotIndex],
                movementSpeed * Time.deltaTime);
            npcMovementPositions[slotIndex] = exitPosition;
            if (Vector2.Distance(exitPosition, npcExitTargetPositions[slotIndex]) <= 0.01f)
            {
                npcRect.anchoredPosition = npcExitTargetPositions[slotIndex];
                DestroyNpcAtSlot(slotIndex);
                return;
            }

            ApplyNpcWalkingPose(slotIndex, npcRect, previousPosition, movementSpeed, 1f);
        }

        private void AnimateNpcArrivals()
        {
            RectTransform canvasRect = sideCanvas.GetComponent<RectTransform>();
            if (canvasRect == null)
            {
                return;
            }

            float stoppingDuration = Mathf.Max(0.05f, arrivalStoppingSeconds);
            for (int slotIndex = 0; slotIndex < spawnedNpcs.Length; slotIndex++)
            {
                if (spawnedNpcs[slotIndex] == null)
                {
                    continue;
                }

                float movementSpeed = Mathf.Max(1f,
                    canvasRect.rect.width * npcWalkingSpeedCanvasMultipliers[slotIndex]);
                if (npcExitingAtSlots[slotIndex])
                {
                    AnimateNpcExit(slotIndex, movementSpeed);
                    continue;
                }

                if (npcOrdersExpired[slotIndex])
                {
                    // The slot may already belong to the next generation. Keep its
                    // timer/UI untouched while the expired NPC finishes showing Angry.
                    if (!IsNpcShowingAngryHold(slotIndex))
                    {
                        BeginNpcExit(slotIndex);
                    }
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

                AnimateNpcArrival(slotIndex, npcRect, movementSpeed, stoppingDuration, Time.deltaTime);
            }
        }

        private void AnimateNpcArrival(int slotIndex, RectTransform npcRect,
            float movementSpeed, float stoppingDuration, float deltaSeconds)
        {
            if (deltaSeconds <= 0f)
            {
                return;
            }

            Vector2 previousPosition = npcMovementPositions[slotIndex];
            // Count stride distance backwards from the destination's grounded pose.
            // Every entrance then finishes its actual down-step at the target,
            // rather than fading an arbitrary airborne pose down to the floor.
            npcWalkPhases[slotIndex] = -Vector2.Distance(previousPosition, npcTargetPositions[slotIndex]) /
                movementSpeed * walkingBobFrequency * Mathf.PI * 2f;
            float nearTargetDistance = 0.5f * movementSpeed * stoppingDuration;
            float remainingFrameSeconds = Mathf.Max(0f, deltaSeconds);
            if (!npcApproachingAtSlots[slotIndex])
            {
                float distanceToTarget = Vector2.Distance(
                    npcMovementPositions[slotIndex], npcTargetPositions[slotIndex]);
                // Split the frame at the start of the stopping zone, so entering it
                // preserves the incoming velocity even at a low frame rate.
                float farWalkSeconds = Mathf.Min(remainingFrameSeconds,
                    Mathf.Max(0f, distanceToTarget - nearTargetDistance) / movementSpeed);
                npcMovementPositions[slotIndex] = Vector2.MoveTowards(
                    npcMovementPositions[slotIndex], npcTargetPositions[slotIndex],
                    movementSpeed * farWalkSeconds);
                remainingFrameSeconds -= farWalkSeconds;
                if (distanceToTarget - movementSpeed * farWalkSeconds <= nearTargetDistance + 0.01f)
                {
                    BeginNpcFinalApproach(slotIndex, movementSpeed, stoppingDuration);
                }
            }

            if (npcApproachingAtSlots[slotIndex])
            {
                npcApproachElapsedSeconds[slotIndex] += remainingFrameSeconds;
                float progress = Mathf.Clamp01(npcApproachElapsedSeconds[slotIndex] /
                    npcApproachDurations[slotIndex]);
                float remaining = 1f - progress;
                // A short, constant deceleration preserves the incoming speed
                // and reaches zero exactly, without a long easing tail.
                float easedProgress = 1f - remaining * remaining;
                npcMovementPositions[slotIndex] = Vector2.Lerp(
                    npcApproachStartPositions[slotIndex], npcTargetPositions[slotIndex], easedProgress);
                if (progress >= 1f)
                {
                    npcRect.anchoredPosition = npcTargetPositions[slotIndex];
                    MarkNpcArrived(slotIndex);
                    return;
                }
            }

            UpdateNpcForegroundApproach(slotIndex, nearTargetDistance);
            ApplyNpcWalkingPose(slotIndex, npcRect, previousPosition, movementSpeed, 1f);
        }

        private void ApplyNpcWalkingPose(int slotIndex, RectTransform npcRect,
            Vector2 previousPosition, float movementSpeed, float bobWeight)
        {
            // Use the same distance-driven, grounded gait in both directions,
            // including the partial stopping step on arrival.
            float distanceMoved = Vector2.Distance(previousPosition, npcMovementPositions[slotIndex]);
            npcWalkPhases[slotIndex] += distanceMoved / movementSpeed * walkingBobFrequency * Mathf.PI * 2f;
            float walkingBobOffset = (0.5f - 0.5f * Mathf.Cos(npcWalkPhases[slotIndex])) * walkingBobHeight * bobWeight;
            NpcForegroundBlend foregroundBlend = npcForegroundBlends[slotIndex];
            float laneOffset = foregroundBlend != null ? foregroundBlend.RearLaneOffset : 0f;
            npcRect.anchoredPosition = npcMovementPositions[slotIndex] + Vector2.up * (walkingBobOffset + laneOffset);
            foregroundBlend?.SynchronizePose();
        }

        private void BeginNpcForegroundApproach(int slotIndex, RectTransform npcRect,
            RectTransform visualBody, Transform finalLayer)
        {
            // Prefabs authored to stay in the rear layer have no foreground handoff.
            if (slotIndex <= 0 || slotIndex >= DisplaySlotCount - 1 || finalLayer != frontLayer ||
                npcRect == null || visualBody == null)
            {
                return;
            }

            Image npcImage = visualBody.GetComponent<Image>();
            RectTransform canvasRect = sideCanvas.GetComponent<RectTransform>();
            if (npcImage == null || canvasRect == null)
            {
                return;
            }

            float canvasWidth = canvasRect.rect.width;
            // Keep the forward step close to the destination, including scenes with the old
            // 0.18 setting. A faster walking pace must not widen this zone into the preceding slot.
            float startDistance = canvasWidth * Mathf.Clamp(foregroundApproachDistanceCanvasMultiplier, 0.01f, 0.06f);
            NpcForegroundBlend blend = npcRect.gameObject.AddComponent<NpcForegroundBlend>();
            blend.Initialize(npcRect, npcImage, frontLayer, startDistance,
                Mathf.Max(0f, foregroundApproachRearOffsetPixels));
            npcForegroundBlends[slotIndex] = blend;
            npcRect.anchoredPosition += Vector2.up * blend.RearLaneOffset;
            blend.SynchronizePose();
            SortNpcLayerChildren(frontLayer);
        }

        private void UpdateNpcForegroundApproach(int slotIndex, float stoppingDistance)
        {
            NpcForegroundBlend blend = npcForegroundBlends[slotIndex];
            if (blend == null)
            {
                return;
            }

            float remainingDistance = Vector2.Distance(npcMovementPositions[slotIndex], npcTargetPositions[slotIndex]);
            // Use the braking step to finish the blend smoothly while there is still
            // distance left to walk, rather than requiring an early foreground handoff.
            float completionDistance = Mathf.Min(stoppingDistance * 0.2f, blend.ApproachStartDistance * 0.25f);
            float progress = Mathf.Clamp01((blend.ApproachStartDistance - remainingDistance) /
                Mathf.Max(0.0001f, blend.ApproachStartDistance - completionDistance));
            blend.SetProgress(Mathf.SmoothStep(0f, 1f, progress));
            if (progress >= 1f)
            {
                // The foreground image is already fully visible; replacing it now causes no layer pop at arrival.
                MoveNpcToFinalLayer(slotIndex);
                ClearNpcForegroundApproach(slotIndex);
            }
        }

        private void ClearNpcForegroundApproach(int slotIndex)
        {
            NpcForegroundBlend blend = npcForegroundBlends[slotIndex];
            npcForegroundBlends[slotIndex] = null;
            if (blend == null)
            {
                return;
            }

            blend.Clear();
            Destroy(blend);
        }

        private void BeginNpcFinalApproach(int slotIndex, float incomingSpeed, float stoppingDuration)
        {
            Vector2 startPosition = npcMovementPositions[slotIndex];
            float distance = Mathf.Max(0.0001f, Vector2.Distance(startPosition, npcTargetPositions[slotIndex]));
            float duration = Mathf.Min(stoppingDuration, 2f * distance / incomingSpeed);
            npcApproachStartPositions[slotIndex] = startPosition;
            npcApproachElapsedSeconds[slotIndex] = 0f;
            npcApproachDurations[slotIndex] = duration;
            npcApproachingAtSlots[slotIndex] = true;
        }

        private void MarkNpcArrived(int slotIndex)
        {
            npcArrivedAtSlots[slotIndex] = true;
            npcApproachingAtSlots[slotIndex] = false;
            npcExitingAtSlots[slotIndex] = false;
            npcMovementPositions[slotIndex] = npcTargetPositions[slotIndex];
            npcWalkPhases[slotIndex] = 0f;
            MoveNpcToFinalLayer(slotIndex);
            ClearNpcForegroundApproach(slotIndex);
            npcIdleBreathing[slotIndex]?.BeginIdle();
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

        private void MoveNpcToExitLayer(int slotIndex)
        {
            bool exitsFromOuterSlot = slotIndex == 0 || slotIndex == DisplaySlotCount - 1;
            MoveNpcToLayer(slotIndex, exitsFromOuterSlot ? frontLayer : backLayer);
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
            npcEmojiReadyTimes[slotIndex] = Time.time + Mathf.Max(0f, emojiDelayAfterMenuSeconds);
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
            ClearNpcForegroundApproach(slotIndex);
            npcEmojiPresentations[slotIndex]?.Hide();
            npcEmojiPresentations[slotIndex] = null;
            npcEmojiReadyTimes[slotIndex] = 0f;
            npcOrdersExpired[slotIndex] = false;
            npcAngryUntilTimes[slotIndex] = 0f;
            GameObject instance = spawnedNpcs[slotIndex];
            spawnedNpcPrefabs[slotIndex] = null;
            npcIdleBreathing[slotIndex] = null;
            npcApproachingAtSlots[slotIndex] = false;
            npcMovementPositions[slotIndex] = Vector2.zero;
            npcExitTargetPositions[slotIndex] = Vector2.zero;
            npcWalkingSpeedCanvasMultipliers[slotIndex] = 0f;
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
                customerPanelPresentations[i]?.Hide(true);
                DestroyNpcAtSlot(i);
            }
        }

        private void ClampTimingSettings()
        {
            entranceSpeedCanvasMultiplier = Mathf.Max(0.01f, entranceSpeedCanvasMultiplier);
            maximumWalkingSpeedIncreasePercent = Mathf.Clamp(maximumWalkingSpeedIncreasePercent, 0f, 100f);
            arrivalStoppingSeconds = Mathf.Max(0.05f, arrivalStoppingSeconds);
            foregroundApproachDistanceCanvasMultiplier = Mathf.Clamp(foregroundApproachDistanceCanvasMultiplier, 0.01f, 0.06f);
            foregroundApproachRearOffsetPixels = Mathf.Max(0f, foregroundApproachRearOffsetPixels);
            pairedEntranceSpacingCanvasMultiplier = Mathf.Max(0f, pairedEntranceSpacingCanvasMultiplier);
            arrivalHoldDurationSeconds = Mathf.Max(0f, arrivalHoldDurationSeconds);
            walkingBobHeight = Mathf.Max(0f, walkingBobHeight);
            walkingBobFrequency = Mathf.Max(0.1f, walkingBobFrequency);
            idleBreathingCycleSeconds = Mathf.Max(0.2f, idleBreathingCycleSeconds);
            idleBreathingHeight = Mathf.Clamp(idleBreathingHeight, 0f, 0.03f);
            idleBreathingWidth = Mathf.Clamp(idleBreathingWidth, 0f, 0.02f);
            idleBreathingBlendSeconds = Mathf.Max(0.01f, idleBreathingBlendSeconds);
            angryHoldDurationSeconds = Mathf.Max(0f, angryHoldDurationSeconds);
            emojiDelayAfterMenuSeconds = Mathf.Max(0f, emojiDelayAfterMenuSeconds);
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

            // Match a manually placed NPC on the wall's lower edge. Prefab root Y
            // values are old placement offsets; keep the visual child's authored
            // size, scale and offset so its proportions remain unchanged.
            float standingBaselineY = canvasRect.rect.yMin;
            Vector3 targetLocalPosition = canvasRect.InverseTransformPoint(target.position);
            instanceRect.anchorMin = new Vector2(0.5f, 0.5f);
            instanceRect.anchorMax = new Vector2(0.5f, 0.5f);
            instanceRect.anchoredPosition = new Vector2(
                targetLocalPosition.x,
                standingBaselineY);
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

            instanceRect.anchoredPosition = GetNpcEntrancePosition(slotIndex, canvasRect);
            npcExitTargetPositions[slotIndex] = instanceRect.anchoredPosition;
            npcMovementPositions[slotIndex] = instanceRect.anchoredPosition;
            npcWalkingSpeedCanvasMultipliers[slotIndex] = SampleNpcWalkingSpeed();
            npcWalkPhases[slotIndex] = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            npcApproachingAtSlots[slotIndex] = false;
            npcArrivedAtSlots[slotIndex] = false;
            npcExitingAtSlots[slotIndex] = false;
            npcUiShownAtSlots[slotIndex] = false;
            npcUiReadyTimes[slotIndex] = 0f;
        }

        private void InitializeNpcEmoji(GameObject instance, int slotIndex)
        {
            npcEmojiReadyTimes[slotIndex] = 0f;
            npcOrdersExpired[slotIndex] = false;
            npcAngryUntilTimes[slotIndex] = 0f;
            Transform emojiRoot = FindDirectChild(instance.transform, "Emoji");
            if (emojiRoot == null)
            {
                return;
            }

            if (npcEmojiRandom == null)
            {
                // Cosmetic moods must not change the gameplay food/spawn random sequence.
                npcEmojiRandom = new System.Random(GetInstanceID());
            }

            NpcEmojiPresentation presentation = instance.AddComponent<NpcEmojiPresentation>();
            Transform visualBody = FindNestedTransform(instance.transform, "Image");
            presentation.Initialize(emojiRoot, npcEmojiRandom.Next(0, 3),
                visualBody != null ? visualBody.GetComponent<Image>() : null);
            npcEmojiPresentations[slotIndex] = presentation;
        }

        private void UpdateNpcEmoji(int slotIndex, ArenaSlot2D slot)
        {
            NpcEmojiPresentation presentation = npcEmojiPresentations[slotIndex];
            if (presentation == null || !npcArrivedAtSlots[slotIndex] || npcExitingAtSlots[slotIndex] ||
                npcOrdersExpired[slotIndex] || slot == null ||
                slot.CustomerGeneration != npcCustomerGenerations[slotIndex])
            {
                return;
            }

            if (!npcUiShownAtSlots[slotIndex] || Time.time < npcEmojiReadyTimes[slotIndex])
            {
                presentation.Hide();
                return;
            }

            switch (slot.CustomerState)
            {
                case CustomerSlotState.WaitingForFood:
                    if (slot.IsOrderNearTimeout)
                    {
                        presentation.ShowBad();
                    }
                    else
                    {
                        presentation.ShowInitialMood();
                    }
                    break;
                case CustomerSlotState.Eating:
                    presentation.ShowLove();
                    break;
                default:
                    presentation.Hide();
                    break;
            }
        }

        private bool IsNpcShowingAngryHold(int slotIndex)
        {
            return npcOrdersExpired[slotIndex] && Time.time < npcAngryUntilTimes[slotIndex];
        }

        private float SampleNpcWalkingSpeed()
        {
            float minimumSpeed = Mathf.Max(0.01f, entranceSpeedCanvasMultiplier);
            float maximumIncrease = Mathf.Clamp(maximumWalkingSpeedIncreasePercent, 0f, 100f) * 0.01f;
            return UnityEngine.Random.Range(minimumSpeed, minimumSpeed * (1f + maximumIncrease));
        }

        private Vector2 GetNpcEntrancePosition(int slotIndex, RectTransform canvasRect)
        {
            bool entersFromLeft = slotIndex < DisplaySlotCount / 2;
            float direction = entersFromLeft ? -1f : 1f;
            float entranceDistance = Mathf.Max(1f,
                canvasRect.rect.width * Mathf.Max(0.5f, entranceDistanceCanvasMultiplier));
            float entranceX = canvasRect.rect.center.x + direction * entranceDistance;
            float spacing = canvasRect.rect.width * Mathf.Max(0f, pairedEntranceSpacingCanvasMultiplier);
            for (int i = 0; i < spawnedNpcs.Length; i++)
            {
                if (i == slotIndex || spawnedNpcs[i] == null || npcArrivedAtSlots[i] ||
                    npcExitingAtSlots[i] || (i < DisplaySlotCount / 2) != entersFromLeft)
                {
                    continue;
                }

                float followingX = npcMovementPositions[i].x + direction * spacing;
                entranceX = entersFromLeft ? Mathf.Min(entranceX, followingX) : Mathf.Max(entranceX, followingX);
            }

            return new Vector2(entranceX, npcTargetPositions[slotIndex].y);
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
                customerPanelPresentations[i] = customerPanels[i] != null
                    ? customerPanels[i].GetComponent<CustomerPanelPresentation>()
                    : null;
            }
        }

        private void SetCustomerPanelVisible(int slotIndex, bool visible)
        {
            if (slotIndex < 0 || slotIndex >= customerPanels.Length || customerPanels[slotIndex] == null)
            {
                return;
            }

            CustomerPanelPresentation presentation = customerPanelPresentations[slotIndex];
            if (presentation == null)
            {
                customerPanels[slotIndex].gameObject.SetActive(visible);
                return;
            }

            if (visible)
            {
                presentation.Show();
            }
            else
            {
                presentation.Hide();
            }
        }

        private void SubscribeToCustomerEvents()
        {
            if (subscribedGameManager == gameManager)
            {
                return;
            }

            UnsubscribeFromCustomerEvents();
            subscribedGameManager = gameManager;
            if (subscribedGameManager != null)
            {
                subscribedGameManager.CustomerFinishedEating += HandleCustomerFinishedEating;
                subscribedGameManager.CustomerOrderExpired += HandleCustomerOrderExpired;
            }
        }

        private void UnsubscribeFromCustomerEvents()
        {
            if (subscribedGameManager != null)
            {
                subscribedGameManager.CustomerFinishedEating -= HandleCustomerFinishedEating;
                subscribedGameManager.CustomerOrderExpired -= HandleCustomerOrderExpired;
            }

            subscribedGameManager = null;
        }

        private void HandleCustomerFinishedEating(ArenaSlot2D slot, int reward)
        {
            for (int i = 0; i < customerPanels.Length; i++)
            {
                if (gameManager.GetCustomerSlot(i) != slot || !npcUiShownAtSlots[i] ||
                    npcExitingAtSlots[i] || npcCustomerGenerations[i] != slot.CustomerGeneration)
                {
                    continue;
                }

                npcEmojiPresentations[i]?.Hide();
                CustomerPanelPresentation presentation = customerPanelPresentations[i];
                if (presentation != null)
                {
                    presentation.Complete();
                    slot.WaitForMoneyPresentation(() => presentation == null || !presentation.IsCelebrating);
                }
                return;
            }
        }

        private void HandleCustomerOrderExpired(ArenaSlot2D slot)
        {
            if (slot == null)
            {
                return;
            }

            for (int i = 0; i < spawnedNpcs.Length; i++)
            {
                if (gameManager.GetCustomerSlot(i) != slot || spawnedNpcs[i] == null ||
                    !npcArrivedAtSlots[i] || npcExitingAtSlots[i] || npcOrdersExpired[i] ||
                    npcCustomerGenerations[i] != slot.CustomerGeneration)
                {
                    continue;
                }

                // The event arrives before ClearCustomer; latch this NPC's reaction now.
                npcOrdersExpired[i] = true;
                npcAngryUntilTimes[i] = Time.time + Mathf.Max(0f, angryHoldDurationSeconds);
                npcEmojiPresentations[i]?.ShowAngry();
                npcUiShownAtSlots[i] = false;
                npcUiReadyTimes[i] = 0f;
                SetCustomerPanelVisible(i, false);
                return;
            }
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
