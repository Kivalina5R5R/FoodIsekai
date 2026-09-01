using System.Collections.Generic;
using UnityEngine;

namespace FoodIsekaiZ.Gameplay
{
    [ExecuteAlways]
    public sealed class FoodIsekaiZArenaLayout : MonoBehaviour
    {
        private const string ManualArenaRootName = "Arena";

        [Header("Manual Arena Layout")]
        [Tooltip("Root of the hand-authored arena hierarchy. Edit its children directly in the Scene view.")]
        [SerializeField] private Transform arenaRoot;
        [Tooltip("World-space size used for UWB-to-floor mapping. Keep this synced with the manual arena bounds.")]
        [SerializeField] private Vector2 arenaSize = new Vector2(11f, 5f);

        [Header("Manual Arena Visuals")]
        [SerializeField] private Color backgroundColor = Color.black;
        [SerializeField] private Color customerColor = new Color(1f, 0.5f, 0.16f, 1f);
        [SerializeField] private Color foodStationColor = new Color(0.2f, 0.75f, 0.35f, 1f);
        [SerializeField] private Color depositColor = new Color(1f, 0.82f, 0.15f, 1f);

        [Header("Runtime References")]
        [SerializeField] private FoodIsekaiZGameManager gameManager;

        private Material backgroundMaterial;
        private Material customerMaterial;
        private Material foodStationMaterial;
        private Material depositMaterial;
        private Mesh backgroundMesh;
        private Mesh customerMesh;
        private Mesh moneyMesh;
        private Mesh foodStationMesh;
        private MaterialPropertyBlock manualVisualProperties;

        public Vector2 ArenaSize => arenaSize;

        public Rect ArenaBounds => new Rect(
            new Vector2(transform.position.x, transform.position.z) - (arenaSize * 0.5f),
            arenaSize);

        private void Awake()
        {
            CreateManualVisuals();
            if (Application.isPlaying)
            {
                BindManualArena();
            }
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                CreateManualVisuals();
            }
        }

        private void CreateManualVisuals()
        {
            if (arenaRoot == null)
            {
                return;
            }

            CreateManualVisual(
                arenaRoot.Find("Background"),
                arenaSize,
                backgroundColor,
                ref backgroundMesh,
                ref backgroundMaterial);

            MeshFilter[] filters = arenaRoot.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter filter = filters[i];
                if (filter == null)
                {
                    continue;
                }

                string objectName = filter.gameObject.name;
                if (objectName.StartsWith("CustomerSlot"))
                {
                    CreateManualVisual(
                        filter.transform,
                        GetColliderSize(filter.transform, new Vector2(2.2f, 1.05f)),
                        customerColor,
                        ref customerMesh,
                        ref customerMaterial);
                }
                else if (objectName == "MoneyVisual")
                {
                    CreateManualVisual(
                        filter.transform,
                        GetColliderSize(filter.transform, new Vector2(1.056f, 0.504f)),
                        depositColor,
                        ref moneyMesh,
                        ref depositMaterial);
                }
                else if (objectName.StartsWith("FoodStation"))
                {
                    CreateManualVisual(
                        filter.transform,
                        GetColliderSize(filter.transform, new Vector2(1.65f, 1.05f)),
                        foodStationColor,
                        ref foodStationMesh,
                        ref foodStationMaterial);
                }
                else if (objectName == "MoneyDeposit")
                {
                    CreateManualVisual(
                        filter.transform,
                        GetColliderSize(filter.transform, new Vector2(1.65f, 1.05f)),
                        depositColor,
                        ref foodStationMesh,
                        ref depositMaterial);
                }
            }
        }

        private static Vector2 GetColliderSize(Transform target, Vector2 fallback)
        {
            BoxCollider collider = target.GetComponent<BoxCollider>();
            if (collider == null)
            {
                return fallback;
            }

            return new Vector2(Mathf.Abs(collider.size.x), Mathf.Abs(collider.size.z));
        }

        private void CreateManualVisual(
            Transform target,
            Vector2 size,
            Color color,
            ref Mesh mesh,
            ref Material material)
        {
            if (target == null)
            {
                return;
            }

            MeshFilter filter = target.GetComponent<MeshFilter>();
            MeshRenderer renderer = target.GetComponent<MeshRenderer>();
            if (filter == null || renderer == null)
            {
                return;
            }

            if (mesh == null)
            {
                mesh = CreateManualMesh(target.name, size);
            }

            if (material == null)
            {
                material = CreateManualMaterial(target.name, color);
            }

            if (filter.sharedMesh == null || filter.sharedMesh.hideFlags == HideFlags.HideAndDontSave)
            {
                filter.sharedMesh = mesh;
            }

            if (renderer.sharedMaterial == null || renderer.sharedMaterial.hideFlags == HideFlags.HideAndDontSave)
            {
                renderer.sharedMaterial = material;
            }

            if (manualVisualProperties == null)
            {
                manualVisualProperties = new MaterialPropertyBlock();
            }

            manualVisualProperties.Clear();
            manualVisualProperties.SetColor("_BaseColor", color);
            manualVisualProperties.SetColor("_Color", color);
            renderer.SetPropertyBlock(manualVisualProperties);
        }

        private static Mesh CreateManualMesh(string objectName, Vector2 size)
        {
            float halfWidth = size.x * 0.5f;
            float halfDepth = size.y * 0.5f;
            var mesh = new Mesh
            {
                name = $"{objectName} Manual Mesh",
                hideFlags = HideFlags.HideAndDontSave,
                vertices = new[]
                {
                    new Vector3(-halfWidth, 0f, -halfDepth),
                    new Vector3(halfWidth, 0f, -halfDepth),
                    new Vector3(halfWidth, 0f, halfDepth),
                    new Vector3(-halfWidth, 0f, halfDepth)
                },
                triangles = new[] { 0, 2, 1, 0, 3, 2 },
                uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(1f, 1f),
                    new Vector2(0f, 1f)
                },
                normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up }
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material CreateManualMaterial(string objectName, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Sprites/Default") ??
                Shader.Find("Unlit/Color") ??
                Shader.Find("Standard");
            if (shader == null)
            {
                return null;
            }

            var material = new Material(shader)
            {
                name = $"{objectName} Manual Material",
                hideFlags = HideFlags.HideAndDontSave
            };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (material.HasProperty("_Cull"))
            {
                material.SetInt("_Cull", 0);
            }

            return material;
        }

        private void BindManualArena()
        {
            if (arenaRoot == null)
            {
                Debug.LogError(
                    $"[{nameof(FoodIsekaiZArenaLayout)}] Arena root is not assigned. " +
                    $"Assign the '{ManualArenaRootName}' Transform in the Inspector.",
                    this);
                return;
            }

            if (gameManager == null)
            {
                gameManager = FindAnyObjectByType<FoodIsekaiZGameManager>();
            }

            List<ArenaSlot2D> customerSlots = FindSlots(ArenaSlotType.Customer);
            List<ArenaSlot2D> stationSlots = FindStationSlots();
            if (gameManager == null)
            {
                Debug.LogError(
                    $"[{nameof(FoodIsekaiZArenaLayout)}] FoodIsekaiZGameManager was not found.",
                    this);
                return;
            }

            if (customerSlots.Count == 0 || stationSlots.Count == 0)
            {
                Debug.LogWarning(
                    $"[{nameof(FoodIsekaiZArenaLayout)}] Manual Arena contains " +
                    $"{customerSlots.Count} customer slot(s) and {stationSlots.Count} station slot(s).",
                    this);
            }

            gameManager.ConfigureSlots(
                customerSlots.ToArray(),
                stationSlots.ToArray(),
                false);

            BindExistingSlotVisuals(customerSlots);
            BindExistingSlotVisuals(stationSlots);
            gameManager.EnsureCustomerFlowStarted();
        }

        private void BindExistingSlotVisuals(List<ArenaSlot2D> slots)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                ArenaSlot2D slot = slots[i];
                if (slot == null)
                {
                    continue;
                }

                Transform moneyTransform = slot.transform.Find("MoneyVisual");
                GameObject moneyVisual = moneyTransform != null ? moneyTransform.gameObject : null;
                TextMesh statusLabel = slot.GetComponentInChildren<TextMesh>(true);
                slot.ConfigureVisuals(moneyVisual, statusLabel);
            }
        }

        private List<ArenaSlot2D> FindSlots(ArenaSlotType slotType)
        {
            var result = new List<ArenaSlot2D>();
            ArenaSlot2D[] slots = arenaRoot.GetComponentsInChildren<ArenaSlot2D>(true);
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && slots[i].SlotType == slotType)
                {
                    result.Add(slots[i]);
                }
            }

            return result;
        }

        private List<ArenaSlot2D> FindStationSlots()
        {
            var result = new List<ArenaSlot2D>();
            ArenaSlot2D[] slots = arenaRoot.GetComponentsInChildren<ArenaSlot2D>(true);
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null ||
                    (slots[i].SlotType != ArenaSlotType.FoodStation &&
                     slots[i].SlotType != ArenaSlotType.MoneyDeposit))
                {
                    continue;
                }

                result.Add(slots[i]);
            }

            return result;
        }
    }
}
