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
    /// <summary>
    /// Draws a unique NPC prefab for each active customer slot during a meal wave.
    /// </summary>
    public sealed class FoodIsekaiZNpcWaveSpawner : MonoBehaviour
    {
        private const int DisplaySlotCount = 6;
        private const string BackLayerName = "NPCBackLayer";
        private const string FrontLayerName = "NPCFrontLayer";
        private const string DefaultNpcPrefabFolder = "Assets/Prefab";
        private const string DefaultNpcPrefabNamePrefix = "NPC";

        [Header("NPC Prefab Pool")]
        [Tooltip("NPC prefabs are drawn without replacement during one Wave. The list has no character-count limit and is refreshed from the configured folder in the Unity Editor.")]
        [SerializeField] private GameObject[] npcPrefabs = Array.Empty<GameObject>();
        [Tooltip("Folder searched recursively for NPC prefabs when the scene is validated or play mode starts in the Unity Editor.")]
        [SerializeField] private string npcPrefabFolder = DefaultNpcPrefabFolder;
        [Tooltip("Only prefab assets whose names start with this prefix are included in the automatic pool.")]
        [SerializeField] private string npcPrefabNamePrefix = DefaultNpcPrefabNamePrefix;

        [Header("Wall Display")]
        [SerializeField] private Canvas sideCanvas;
        [SerializeField] private FoodIsekaiZGameManager gameManager;
        [Tooltip("Optional containers. Leave empty to let the system create the layers automatically.")]
        [SerializeField] private Transform backLayer;
        [SerializeField] private Transform frontLayer;

        private readonly GameObject[] spawnedNpcs = new GameObject[DisplaySlotCount];
        private readonly List<GameObject> availableNpcPrefabs = new List<GameObject>();
        private int activeWaveNumber = -1;
        private bool npcPoolExhaustedLogged;
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
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying || repairingNpcPrefabReferences)
            {
                return;
            }

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
        }

        private void BeginWave(int waveNumber)
        {
            ClearSpawnedNpcs();
            BuildAvailablePrefabPool();
            activeWaveNumber = waveNumber;
        }

        private void BuildAvailablePrefabPool()
        {
            availableNpcPrefabs.Clear();
            npcPoolExhaustedLogged = false;
            npcPoolConfigured = false;
            npcPrefabConfigurationLogged = false;

            if (npcPrefabs == null)
            {
                LogNpcPrefabConfigurationError();
                return;
            }

            var uniquePrefabs = new HashSet<GameObject>();
            for (int i = 0; i < npcPrefabs.Length; i++)
            {
                GameObject prefab = npcPrefabs[i];
                if (!IsUsableNpcPrefab(prefab) || !uniquePrefabs.Add(prefab))
                {
                    continue;
                }

                availableNpcPrefabs.Add(prefab);
            }

            npcPoolConfigured = availableNpcPrefabs.Count > 0;
            if (!npcPoolConfigured)
            {
                LogNpcPrefabConfigurationError();
            }
        }

        private void SynchronizeNpcSlots()
        {
            for (int slotIndex = 0; slotIndex < spawnedNpcs.Length; slotIndex++)
            {
                ArenaSlot2D slot = gameManager.GetCustomerSlot(slotIndex);
                bool shouldHaveNpc = slot != null && slot.CustomerState != CustomerSlotState.Empty;
                if (!shouldHaveNpc)
                {
                    DestroyNpcAtSlot(slotIndex);
                    continue;
                }

                if (spawnedNpcs[slotIndex] != null)
                {
                    continue;
                }

                SpawnNpcAtSlot(slotIndex);
            }
        }

        private void SpawnNpcAtSlot(int slotIndex)
        {
            GameObject prefab = DrawNextNpcPrefab();
            if (prefab == null)
            {
                return;
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
                return;
            }

            if (prefabTransform == null)
            {
                LogNpcPrefabConfigurationError();
                return;
            }

            Transform parent = IsBackLayerPrefabName(prefabName) ? backLayer : frontLayer;
            if (parent == null)
            {
                Debug.LogWarning(
                    $"[{nameof(FoodIsekaiZNpcWaveSpawner)}] NPC layer is missing; cannot spawn '{prefabName}'.",
                    this);
                return;
            }

            Transform instanceTransform = Instantiate(prefabTransform, parent, false);
            GameObject instance = instanceTransform.gameObject;
            instance.name = prefabName;
            AlignNpcToDisplaySlot(instance, slotIndex);
            spawnedNpcs[slotIndex] = instance;
        }

        private GameObject DrawNextNpcPrefab()
        {
            if (availableNpcPrefabs.Count == 0)
            {
                LogNpcPoolExhausted();
                return null;
            }

            int selectedIndex = UnityEngine.Random.Range(0, availableNpcPrefabs.Count);
            GameObject selectedPrefab = availableNpcPrefabs[selectedIndex];
            availableNpcPrefabs.RemoveAt(selectedIndex);
            if (availableNpcPrefabs.Count == 0)
            {
                LogNpcPoolExhausted();
            }

            return selectedPrefab;
        }

        private void LogNpcPoolExhausted()
        {
            if (!npcPoolConfigured || npcPoolExhaustedLogged)
            {
                return;
            }

            npcPoolExhaustedLogged = true;
            Debug.Log(
                $"[{nameof(FoodIsekaiZNpcWaveSpawner)}] NPC หมดแล้ว — ไม่ Spawn เพิ่มใน Wave นี้.",
                    this);
        }

        private void LogNpcPrefabConfigurationError()
        {
            if (npcPrefabConfigurationLogged)
            {
                return;
            }

            npcPrefabConfigurationLogged = true;
            Debug.LogError(
                $"[{nameof(FoodIsekaiZNpcWaveSpawner)}] ไม่พบ NPC Prefab ที่ใช้งานได้ใน npcPrefabs — ตรวจรายการ Prefab ใน Inspector.",
                this);
        }

        private void DestroyNpcAtSlot(int slotIndex)
        {
            if (spawnedNpcs[slotIndex] == null)
            {
                return;
            }

            Destroy(spawnedNpcs[slotIndex]);
            spawnedNpcs[slotIndex] = null;
        }

        private void ClearSpawnedNpcs()
        {
            for (int i = 0; i < spawnedNpcs.Length; i++)
            {
                DestroyNpcAtSlot(i);
            }
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

            if (discoveredPrefabs.Count == 0 || HasSamePrefabReferences(discoveredPrefabs))
            {
                return;
            }

            repairingNpcPrefabReferences = true;
            npcPrefabs = discoveredPrefabs.ToArray();
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

        private bool HasSamePrefabReferences(List<GameObject> discoveredPrefabs)
        {
            if (npcPrefabs == null || npcPrefabs.Length != discoveredPrefabs.Count)
            {
                return false;
            }

            for (int i = 0; i < discoveredPrefabs.Count; i++)
            {
                if (npcPrefabs[i] != discoveredPrefabs[i])
                {
                    return false;
                }
            }

            return true;
        }
#endif

        private void AlignNpcToDisplaySlot(GameObject instance, int slotIndex)
        {
            RectTransform instanceRect = instance.GetComponent<RectTransform>();
            RectTransform canvasRect = sideCanvas.GetComponent<RectTransform>();
            Transform target = FindNestedTransform(sideCanvas.transform, $"CustomerPanel{slotIndex + 1}");
            if (instanceRect == null || canvasRect == null || target == null)
            {
                return;
            }

            float authoredVerticalPosition = instanceRect.anchoredPosition.y;
            Vector3 targetLocalPosition = canvasRect.InverseTransformPoint(target.position);
            instanceRect.anchorMin = new Vector2(0.5f, 0.5f);
            instanceRect.anchorMax = new Vector2(0.5f, 0.5f);
            instanceRect.anchoredPosition = new Vector2(
                targetLocalPosition.x,
                authoredVerticalPosition);
            instanceRect.localRotation = Quaternion.identity;
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
