using Fortal.UWB;
using UnityEngine;

namespace FoodIsekaiZ.Gameplay
{
    /// <summary>
    /// สร้างพื้นสนาม, กรอบ, grid และ Slot แบบ procedural เหมือนแนวทาง MapBackground ของ PaperArena
    /// พร้อมปรับ Floor Camera ให้เห็นสนามเต็มจออัตโนมัติ และแสดง preview ใน Edit Mode
    /// </summary>
    [ExecuteAlways]
    public sealed class FoodIsekaiZArenaLayout : MonoBehaviour
    {
        private const string GeneratedRootName = "_GeneratedArena";
        private const int CurrentLayoutConfigVersion = 2;

        [Header("Arena Size")]
        // GameConfig 10x10 คือ logical grid แต่พื้นจริงต้องใช้ aspect 2816:1280 = 11:5
        [SerializeField] private Vector2 arenaSize = new Vector2(11f, 5f);
        [SerializeField] private bool matchFloorDisplayAspect = true;
        [SerializeField] private Vector2Int floorDisplayResolution = new Vector2Int(2816, 1280);
        [SerializeField, Min(1f)] private float floorWorldWidth = 11f;
        [SerializeField, Min(0.02f)] private float borderThickness = 0.12f;
        [SerializeField, Min(0f)] private float cameraPadding = 0f;

        [Header("Slot Layout")]
        [SerializeField] private Vector2 customerSlotSize = new Vector2(2.2f, 1.05f);
        [SerializeField] private Vector2 stationSlotSize = new Vector2(1.65f, 1.05f);
        [SerializeField, Min(0f)] private float slotEdgeInset = 0.45f;

        [Header("Grid")]
        [SerializeField] private bool showGrid = true;
        [SerializeField, Range(2, 30)] private int gridColumns = 10;
        [SerializeField, Range(2, 30)] private int gridRows = 10;
        [SerializeField, Range(0.005f, 0.1f)] private float gridLineThickness = 0.018f;

        [Header("Colors")]
        [SerializeField] private Color backgroundColor = new Color(0.035f, 0.055f, 0.075f, 1f);
        [SerializeField] private Color borderColor = new Color(0.1f, 0.85f, 1f, 1f);
        [SerializeField] private Color gridColor = new Color(0.15f, 0.45f, 0.55f, 0.28f);
        [SerializeField] private Color customerColor = new Color(1f, 0.5f, 0.16f, 1f);
        [SerializeField] private Color foodStationColor = new Color(0.2f, 0.75f, 0.35f, 1f);
        [SerializeField] private Color depositColor = new Color(1f, 0.82f, 0.15f, 1f);

        [Header("Scene References")]
        [SerializeField] private Camera floorCamera;
        [SerializeField] private FoodIsekaiZGameManager gameManager;
        [SerializeField] private UWBManager uwbManager;
        [SerializeField] private bool floorUsesDisplay2 = true;
        [SerializeField] private bool autoBuildPreview = true;
        [SerializeField, HideInInspector] private int layoutConfigVersion;

        private Material sharedMaterial;
        private Transform generatedRoot;
        private bool isBuilding;
        private int appliedLayoutHash;

        public Vector2 ArenaSize => arenaSize;
        public Rect ArenaBounds => new Rect(
            new Vector2(transform.position.x, transform.position.z) - (arenaSize * 0.5f),
            arenaSize);

        private void OnEnable()
        {
            ApplyPaperArenaJsonDefaultsOnce();

            if (autoBuildPreview)
            {
                BuildLayout();
            }
        }

        private void OnValidate()
        {
            ApplyPaperArenaJsonDefaultsOnce();
        }

        [ContextMenu("Apply PaperArena Floor Display Defaults")]
        private void ApplyPaperArenaJsonDefaults()
        {
            arenaSize = new Vector2(11f, 5f);
            matchFloorDisplayAspect = true;
            floorDisplayResolution = new Vector2Int(2816, 1280);
            floorWorldWidth = 11f;
            cameraPadding = 0f;
            gridColumns = 10;
            gridRows = 10;
            layoutConfigVersion = CurrentLayoutConfigVersion;
            BuildLayout();
        }

        private void ApplyPaperArenaJsonDefaultsOnce()
        {
            if (layoutConfigVersion >= CurrentLayoutConfigVersion)
            {
                return;
            }

            arenaSize = new Vector2(11f, 5f);
            matchFloorDisplayAspect = true;
            floorDisplayResolution = new Vector2Int(2816, 1280);
            floorWorldWidth = 11f;
            cameraPadding = 0f;
            gridColumns = 10;
            gridRows = 10;
            layoutConfigVersion = CurrentLayoutConfigVersion;
        }

        private void Update()
        {
            if (Application.isPlaying || !autoBuildPreview || isBuilding)
            {
                return;
            }

            EnsureReferences();
            int currentHash = CalculateLayoutHash();
            if (currentHash != appliedLayoutHash)
            {
                BuildLayout();
            }
        }

        private void OnDisable()
        {
            if (sharedMaterial != null)
            {
                SafeDestroy(sharedMaterial);
                sharedMaterial = null;
            }
        }

        [ContextMenu("Build / Refresh Arena Preview")]
        public void BuildLayout()
        {
            if (isBuilding)
            {
                return;
            }

            isBuilding = true;
            if (matchFloorDisplayAspect && floorDisplayResolution.x > 0 && floorDisplayResolution.y > 0)
            {
                // Floor 2816x1280 ใช้ aspect 11:5 เพื่อให้ภาพเต็มจอโดยไม่เกิด pillarbox
                arenaSize.x = floorWorldWidth;
                arenaSize.y = floorWorldWidth * floorDisplayResolution.y / floorDisplayResolution.x;
            }

            arenaSize.x = Mathf.Max(2f, arenaSize.x);
            arenaSize.y = Mathf.Max(2f, arenaSize.y);

            ClearGeneratedLayout();
            EnsureReferences();
            EnsureMaterial();

            GameObject rootObject = new GameObject(GeneratedRootName);
            generatedRoot = rootObject.transform;
            generatedRoot.SetParent(transform, false);

            CreateRectangle("Background", generatedRoot, Vector2.zero, arenaSize, 0f, backgroundColor);
            if (showGrid)
            {
                CreateGrid(generatedRoot);
            }

            CreateBoundary(generatedRoot);
            ArenaSlot2D[] customers = CreateCustomerSlots(generatedRoot);
            ArenaSlot2D[] stations = CreateStationSlots(generatedRoot);

            if (gameManager != null)
            {
                gameManager.ConfigureSlots(customers, stations);
            }

            if (uwbManager != null)
            {
                uwbManager.SetArenaBounds2D(ArenaBounds);
            }

            ConfigureFloorCamera();
            appliedLayoutHash = CalculateLayoutHash();
            isBuilding = false;
        }

        [ContextMenu("Fit Floor Camera")]
        public void ConfigureFloorCamera()
        {
            if (floorCamera == null)
            {
                return;
            }

            floorCamera.orthographic = true;
            floorCamera.targetDisplay = floorUsesDisplay2 ? 1 : 0;
            floorCamera.clearFlags = CameraClearFlags.SolidColor;
            floorCamera.backgroundColor = Color.black;

            Vector3 center = transform.position;
            floorCamera.transform.position = new Vector3(center.x, center.y + 10f, center.z);
            floorCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // ใน Editor ค่า Camera.aspect ของ targetDisplay 1 อาจยังเป็นค่าจอหลัก
            // จึงคำนวณจาก resolution ของ Floor โดยตรงเพื่อให้ preview และ build ตรงกัน
            float aspect = floorDisplayResolution.x > 0 && floorDisplayResolution.y > 0
                ? floorDisplayResolution.x / (float)floorDisplayResolution.y
                : Mathf.Max(0.1f, floorCamera.aspect);
            floorCamera.aspect = aspect;
            float sizeByHeight = arenaSize.y * 0.5f;
            float sizeByWidth = arenaSize.x / (2f * aspect);
            floorCamera.orthographicSize = Mathf.Max(sizeByHeight, sizeByWidth) + cameraPadding;
        }

        private void EnsureReferences()
        {
            if (gameManager == null)
            {
                gameManager = FindAnyObjectByType<FoodIsekaiZGameManager>();
            }

            if (uwbManager == null)
            {
                uwbManager = FindAnyObjectByType<UWBManager>();
            }

            if (floorCamera == null)
            {
                Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
                for (int i = 0; i < cameras.Length; i++)
                {
                    if (cameras[i].name.Contains("Floor"))
                    {
                        floorCamera = cameras[i];
                        break;
                    }
                }
            }
        }

        private void EnsureMaterial()
        {
            if (sharedMaterial != null)
            {
                return;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            sharedMaterial = new Material(shader)
            {
                name = "FoodIsekaiZ Arena Preview Material",
                hideFlags = HideFlags.HideAndDontSave
            };
            if (sharedMaterial.HasProperty("_Cull"))
            {
                sharedMaterial.SetInt("_Cull", 0);
            }
        }

        private ArenaSlot2D[] CreateCustomerSlots(Transform parent)
        {
            Transform group = CreateGroup("TopCustomerSlots", parent);
            var result = new ArenaSlot2D[4];
            float y = (arenaSize.y * 0.5f) - slotEdgeInset - (customerSlotSize.y * 0.5f);

            for (int i = 0; i < result.Length; i++)
            {
                float x = GetEvenlySpacedX(i, result.Length);
                result[i] = CreateSlot(
                    $"CustomerSlot{i + 1:00}",
                    $"C{i + 1}",
                    group,
                    new Vector2(x, y),
                    customerSlotSize,
                    customerColor,
                    ArenaSlotType.Customer,
                    FoodType.None);
            }

            return result;
        }

        private ArenaSlot2D[] CreateStationSlots(Transform parent)
        {
            Transform group = CreateGroup("BottomStationSlots", parent);
            var result = new ArenaSlot2D[6];
            float y = (-arenaSize.y * 0.5f) + slotEdgeInset + (stationSlotSize.y * 0.5f);

            for (int i = 0; i < 5; i++)
            {
                float x = GetEvenlySpacedX(i, result.Length);
                FoodType food = (FoodType)((int)FoodType.Food1 + i);
                result[i] = CreateSlot(
                    $"FoodStation{i + 1:00}",
                    $"F{i + 1}",
                    group,
                    new Vector2(x, y),
                    stationSlotSize,
                    foodStationColor,
                    ArenaSlotType.FoodStation,
                    food);
            }

            float depositX = GetEvenlySpacedX(5, result.Length);
            result[5] = CreateSlot(
                "MoneyDeposit",
                "$ BANK",
                group,
                new Vector2(depositX, y),
                stationSlotSize,
                depositColor,
                ArenaSlotType.MoneyDeposit,
                FoodType.None);
            return result;
        }

        private ArenaSlot2D CreateSlot(
            string objectName,
            string label,
            Transform parent,
            Vector2 position,
            Vector2 size,
            Color color,
            ArenaSlotType slotType,
            FoodType foodType)
        {
            GameObject slotObject = CreateRectangle(objectName, parent, position, size, 0.04f, color);
            BoxCollider trigger = slotObject.AddComponent<BoxCollider>();
            trigger.size = new Vector3(size.x, 0.3f, size.y);
            trigger.isTrigger = true;

            ArenaSlot2D slot = slotObject.AddComponent<ArenaSlot2D>();
            slot.Configure(objectName, slotType, foodType, gameManager);
            CreateLabel(label, slotObject.transform);
            return slot;
        }

        private void CreateBoundary(Transform parent)
        {
            Transform group = CreateGroup("Boundary", parent);
            float halfWidth = arenaSize.x * 0.5f;
            float halfHeight = arenaSize.y * 0.5f;

            CreateRectangle("Top", group, new Vector2(0f, halfHeight), new Vector2(arenaSize.x + borderThickness, borderThickness), 0.06f, borderColor);
            CreateRectangle("Bottom", group, new Vector2(0f, -halfHeight), new Vector2(arenaSize.x + borderThickness, borderThickness), 0.06f, borderColor);
            CreateRectangle("Left", group, new Vector2(-halfWidth, 0f), new Vector2(borderThickness, arenaSize.y), 0.06f, borderColor);
            CreateRectangle("Right", group, new Vector2(halfWidth, 0f), new Vector2(borderThickness, arenaSize.y), 0.06f, borderColor);
        }

        private void CreateGrid(Transform parent)
        {
            Transform group = CreateGroup("Grid", parent);
            float halfWidth = arenaSize.x * 0.5f;
            float halfHeight = arenaSize.y * 0.5f;

            for (int x = 1; x < gridColumns; x++)
            {
                float px = Mathf.Lerp(-halfWidth, halfWidth, x / (float)gridColumns);
                CreateRectangle($"V{x:00}", group, new Vector2(px, 0f), new Vector2(gridLineThickness, arenaSize.y), 0.01f, gridColor);
            }

            for (int y = 1; y < gridRows; y++)
            {
                float py = Mathf.Lerp(-halfHeight, halfHeight, y / (float)gridRows);
                CreateRectangle($"H{y:00}", group, new Vector2(0f, py), new Vector2(arenaSize.x, gridLineThickness), 0.01f, gridColor);
            }
        }

        private GameObject CreateRectangle(
            string objectName,
            Transform parent,
            Vector2 localPosition,
            Vector2 size,
            float localHeight,
            Color color)
        {
            GameObject rectangle = new GameObject(objectName);
            rectangle.transform.SetParent(parent, false);
            rectangle.transform.localPosition = new Vector3(localPosition.x, localHeight, localPosition.y);

            Mesh mesh = new Mesh
            {
                name = $"{objectName} Mesh",
                hideFlags = HideFlags.HideAndDontSave
            };
            float halfWidth = size.x * 0.5f;
            float halfHeight = size.y * 0.5f;
            mesh.vertices = new[]
            {
                new Vector3(-halfWidth, 0f, -halfHeight),
                new Vector3(halfWidth, 0f, -halfHeight),
                new Vector3(halfWidth, 0f, halfHeight),
                new Vector3(-halfWidth, 0f, halfHeight)
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();

            MeshFilter filter = rectangle.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = rectangle.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = sharedMaterial;

            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
            return rectangle;
        }

        private static Transform CreateGroup(string groupName, Transform parent)
        {
            GameObject group = new GameObject(groupName);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        private static void CreateLabel(string text, Transform parent)
        {
            GameObject labelObject = new GameObject("Label");
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            labelObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.text = text;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 64;
            label.characterSize = 0.055f;
            label.color = Color.white;
            label.fontStyle = FontStyle.Bold;
        }

        private float GetEvenlySpacedX(int index, int count)
        {
            float cellWidth = arenaSize.x / count;
            return (-arenaSize.x * 0.5f) + (cellWidth * (index + 0.5f));
        }

        private void ClearGeneratedLayout()
        {
            Transform existing = transform.Find(GeneratedRootName);
            if (existing != null)
            {
                SafeDestroy(existing.gameObject);
            }

            generatedRoot = null;
        }

        private int CalculateLayoutHash()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + arenaSize.GetHashCode();
                hash = (hash * 31) + matchFloorDisplayAspect.GetHashCode();
                hash = (hash * 31) + floorDisplayResolution.GetHashCode();
                hash = (hash * 31) + floorWorldWidth.GetHashCode();
                hash = (hash * 31) + borderThickness.GetHashCode();
                hash = (hash * 31) + cameraPadding.GetHashCode();
                hash = (hash * 31) + customerSlotSize.GetHashCode();
                hash = (hash * 31) + stationSlotSize.GetHashCode();
                hash = (hash * 31) + slotEdgeInset.GetHashCode();
                hash = (hash * 31) + showGrid.GetHashCode();
                hash = (hash * 31) + gridColumns;
                hash = (hash * 31) + gridRows;
                hash = (hash * 31) + gridLineThickness.GetHashCode();
                hash = (hash * 31) + backgroundColor.GetHashCode();
                hash = (hash * 31) + borderColor.GetHashCode();
                hash = (hash * 31) + gridColor.GetHashCode();
                hash = (hash * 31) + customerColor.GetHashCode();
                hash = (hash * 31) + foodStationColor.GetHashCode();
                hash = (hash * 31) + depositColor.GetHashCode();
                hash = (hash * 31) + floorUsesDisplay2.GetHashCode();
                hash = (hash * 31) + transform.position.GetHashCode();
                hash = (hash * 31) + (floorCamera != null ? floorCamera.GetInstanceID() : 0);
                hash = (hash * 31) + (gameManager != null ? gameManager.GetInstanceID() : 0);
                hash = (hash * 31) + (uwbManager != null ? uwbManager.GetInstanceID() : 0);
                return hash;
            }
        }

        private static void SafeDestroy(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
