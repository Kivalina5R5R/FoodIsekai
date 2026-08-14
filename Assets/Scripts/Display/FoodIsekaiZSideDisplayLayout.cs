using FoodIsekaiZ.Gameplay;
using FoodIsekaiZ.Players;
using Fortal.UWB;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    /// <summary>
    /// สร้าง UI จอข้าง Display 1 ตามสัดส่วน 1536x435 ของ PaperArena
    /// เป็น preview เริ่มต้นสำหรับคะแนน, UWB status และสถานะผู้เล่น
    /// </summary>
    [ExecuteAlways]
    public sealed class FoodIsekaiZSideDisplayLayout : MonoBehaviour
    {
        private const string GeneratedRootName = "_GeneratedSideDisplay";

        [Header("PaperArena Side Display")]
        [SerializeField] private Vector2 referenceResolution = new Vector2(1536f, 435f);
        [SerializeField] private Vector2Int sideDisplayResolution = new Vector2Int(1536, 435);
        [SerializeField] private Vector2Int floorDisplayResolution = new Vector2Int(2816, 1280);
        [Tooltip("ให้ความกว้างจอข้างเท่าขอบหลังของพื้นตามรูปติดตั้งจริง")]
        [SerializeField] private bool wallMatchesFloorWidth = true;
        [SerializeField, Range(0.25f, 1f)] private float wallWidthRatio = 1f;
        [SerializeField] private Color backgroundColor = new Color(0.025f, 0.035f, 0.055f, 1f);
        [SerializeField] private Color panelColor = new Color(0.065f, 0.09f, 0.13f, 1f);
        [SerializeField] private Color accentColor = new Color(0.1f, 0.85f, 1f, 1f);
        [SerializeField] private Color moneyColor = new Color(1f, 0.82f, 0.15f, 1f);

        [Header("Scene References")]
        [SerializeField] private Camera sideCamera;
        [SerializeField] private FoodIsekaiZArenaLayout arenaLayout;
        [SerializeField] private FoodIsekaiZGameManager gameManager;
        [SerializeField] private UWBManager uwbManager;
        [SerializeField] private UWBPlayerSpawner playerSpawner;
        [SerializeField] private bool autoBuildPreview = true;

        private Canvas sideCanvas;
        private Text scoreText;
        private Text uwbStatusText;
        private readonly Text[] playerStatusTexts = new Text[4];
        private bool isBuilding;
        private int appliedHash;

        public Canvas SideCanvas => sideCanvas;

        private void OnEnable()
        {
            if (autoBuildPreview)
            {
                BuildSideDisplay();
            }
        }

        private void Start()
        {
            EnsureReferences();
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                if (autoBuildPreview && !isBuilding)
                {
                    EnsureReferences();
                    int currentHash = CalculateHash();
                    if (currentHash != appliedHash)
                    {
                        BuildSideDisplay();
                    }
                }

                return;
            }

            UpdateRuntimeText();
        }

        [ContextMenu("Build / Refresh Side Display")]
        public void BuildSideDisplay()
        {
            if (isBuilding)
            {
                return;
            }

            isBuilding = true;
            ClearGeneratedDisplay();
            EnsureReferences();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject canvasObject = new GameObject(
                GeneratedRootName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            sideCanvas = canvasObject.GetComponent<Canvas>();
            sideCanvas.renderMode = RenderMode.WorldSpace;
            sideCanvas.worldCamera = sideCamera;
            sideCanvas.targetDisplay = 0;
            sideCanvas.sortingOrder = 0;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.referenceResolution = referenceResolution;
            scaler.scaleFactor = 1f;

            CreatePanel("Background", canvasObject.transform, Vector2.zero, Vector2.one, backgroundColor);
            CreatePanel("TopAccent", canvasObject.transform, new Vector2(0f, 0.965f), Vector2.one, accentColor);

            Text title = CreateText(
                "Title",
                canvasObject.transform,
                new Vector2(0.02f, 0.55f),
                new Vector2(0.29f, 0.93f),
                "FOOD ISEKAI Z",
                46,
                TextAnchor.MiddleLeft,
                Color.white,
                font);
            title.fontStyle = FontStyle.Bold;

            scoreText = CreateText(
                "TeamMoney",
                canvasObject.transform,
                new Vector2(0.30f, 0.55f),
                new Vector2(0.71f, 0.93f),
                "TEAM MONEY  0000",
                54,
                TextAnchor.MiddleCenter,
                moneyColor,
                font);
            scoreText.fontStyle = FontStyle.Bold;

            uwbStatusText = CreateText(
                "UWBStatus",
                canvasObject.transform,
                new Vector2(0.72f, 0.55f),
                new Vector2(0.98f, 0.93f),
                "UWB  WAITING",
                26,
                TextAnchor.MiddleRight,
                accentColor,
                font);

            for (int i = 0; i < playerStatusTexts.Length; i++)
            {
                float cellMin = i / (float)playerStatusTexts.Length;
                float cellMax = (i + 1f) / playerStatusTexts.Length;
                Vector2 min = new Vector2(cellMin + 0.008f, 0.08f);
                Vector2 max = new Vector2(cellMax - 0.008f, 0.49f);
                Transform panel = CreatePanel($"PlayerPanel{i + 1}", canvasObject.transform, min, max, panelColor);
                playerStatusTexts[i] = CreateText(
                    "Status",
                    panel,
                    Vector2.zero,
                    Vector2.one,
                    $"P{i + 1}   TAG --\nWAITING",
                    28,
                    TextAnchor.MiddleCenter,
                    Color.white,
                    font);
            }

            ConfigurePhysicalWall(canvasObject.GetComponent<RectTransform>());
            ConfigureSideCamera();
            UpdateRuntimeText();
            appliedHash = CalculateHash();
            isBuilding = false;
        }

        private void ConfigureSideCamera()
        {
            if (sideCamera == null)
            {
                return;
            }

            sideCamera.targetDisplay = 0;
            sideCamera.orthographic = true;
            sideCamera.clearFlags = CameraClearFlags.SolidColor;
            sideCamera.backgroundColor = backgroundColor;

            GetPhysicalWallMetrics(out Vector3 wallCenter, out float wallWidth, out float wallHeight);
            sideCamera.aspect = wallWidth / Mathf.Max(0.01f, wallHeight);
            sideCamera.orthographicSize = wallHeight * 0.5f;
            sideCamera.transform.position = new Vector3(wallCenter.x, wallCenter.y, wallCenter.z - 10f);
            sideCamera.transform.rotation = Quaternion.identity;
        }

        private void ConfigurePhysicalWall(RectTransform canvasRect)
        {
            GetPhysicalWallMetrics(out Vector3 wallCenter, out float wallWidth, out _);
            float worldUnitsPerPixel = wallWidth / Mathf.Max(1f, sideDisplayResolution.x);

            canvasRect.sizeDelta = new Vector2(sideDisplayResolution.x, sideDisplayResolution.y);
            canvasRect.position = wallCenter;
            canvasRect.rotation = Quaternion.identity;
            canvasRect.localScale = Vector3.one * worldUnitsPerPixel;
        }

        private void GetPhysicalWallMetrics(out Vector3 center, out float width, out float height)
        {
            float floorWidth = arenaLayout != null ? arenaLayout.ArenaSize.x : 11f;
            float floorDepth = arenaLayout != null ? arenaLayout.ArenaSize.y : 5f;
            if (wallMatchesFloorWidth)
            {
                width = floorWidth * wallWidthRatio;
                height = width * sideDisplayResolution.y / Mathf.Max(1f, sideDisplayResolution.x);
            }
            else
            {
                float worldUnitsPerPixel = floorWidth / Mathf.Max(1, floorDisplayResolution.x);
                width = sideDisplayResolution.x * worldUnitsPerPixel;
                height = sideDisplayResolution.y * worldUnitsPerPixel;
            }

            Vector3 arenaOrigin = arenaLayout != null ? arenaLayout.transform.position : Vector3.zero;
            center = new Vector3(
                arenaOrigin.x,
                arenaOrigin.y + (height * 0.5f),
                arenaOrigin.z + (floorDepth * 0.5f) + 0.02f);
        }

        private void UpdateRuntimeText()
        {
            if (scoreText != null)
            {
                int score = gameManager != null ? gameManager.TeamBankedMoney : 0;
                scoreText.text = $"TEAM MONEY  {score:0000}";
            }

            if (uwbStatusText != null)
            {
                bool connected = uwbManager != null && uwbManager.IsConnected;
                uwbStatusText.text = connected ? "UWB  ONLINE" : "UWB  WAITING";
                uwbStatusText.color = connected ? Color.green : accentColor;
            }

            for (int i = 0; i < playerStatusTexts.Length; i++)
            {
                if (playerStatusTexts[i] == null)
                {
                    continue;
                }

                if (playerSpawner != null &&
                    i < playerSpawner.SpawnedPlayers.Count &&
                    playerSpawner.SpawnedPlayers[i] != null)
                {
                    UWBPlayerController player = playerSpawner.SpawnedPlayers[i];
                    string state = player.IsTracking ? "TRACKING" : "WAITING";
                    playerStatusTexts[i].text = $"P{player.PlayerId}   TAG {player.TagId}\n{state}";
                    playerStatusTexts[i].color = player.IsTracking ? Color.green : Color.white;
                }
                else
                {
                    playerStatusTexts[i].text = $"P{i + 1}   TAG --\nWAITING";
                    playerStatusTexts[i].color = Color.white;
                }
            }
        }

        private void EnsureReferences()
        {
            if (sideCamera == null)
            {
                Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
                for (int i = 0; i < cameras.Length; i++)
                {
                    if (cameras[i].name.Contains("Side"))
                    {
                        sideCamera = cameras[i];
                        break;
                    }
                }
            }

            if (arenaLayout == null)
            {
                arenaLayout = FindAnyObjectByType<FoodIsekaiZArenaLayout>();
            }

            if (gameManager == null)
            {
                gameManager = FindAnyObjectByType<FoodIsekaiZGameManager>();
            }

            if (uwbManager == null)
            {
                uwbManager = FindAnyObjectByType<UWBManager>();
            }

            if (playerSpawner == null)
            {
                playerSpawner = FindAnyObjectByType<UWBPlayerSpawner>();
            }
        }

        private static Transform CreatePanel(
            string objectName,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color color)
        {
            GameObject panelObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panelObject.transform.SetParent(parent, false);
            RectTransform rect = panelObject.GetComponent<RectTransform>();
            SetRect(rect, anchorMin, anchorMax);
            panelObject.GetComponent<Image>().color = color;
            return panelObject.transform;
        }

        private static Text CreateText(
            string objectName,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            string value,
            int fontSize,
            TextAnchor alignment,
            Color color,
            Font font)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            SetRect(rect, anchorMin, anchorMax);

            Text text = textObject.GetComponent<Text>();
            text.text = value;
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private void ClearGeneratedDisplay()
        {
            Transform existing = transform.Find(GeneratedRootName);
            if (existing != null)
            {
                SafeDestroy(existing.gameObject);
            }

            sideCanvas = null;
            scoreText = null;
            uwbStatusText = null;
            for (int i = 0; i < playerStatusTexts.Length; i++)
            {
                playerStatusTexts[i] = null;
            }
        }

        private int CalculateHash()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + referenceResolution.GetHashCode();
                hash = (hash * 31) + sideDisplayResolution.GetHashCode();
                hash = (hash * 31) + floorDisplayResolution.GetHashCode();
                hash = (hash * 31) + wallMatchesFloorWidth.GetHashCode();
                hash = (hash * 31) + wallWidthRatio.GetHashCode();
                hash = (hash * 31) + backgroundColor.GetHashCode();
                hash = (hash * 31) + panelColor.GetHashCode();
                hash = (hash * 31) + accentColor.GetHashCode();
                hash = (hash * 31) + moneyColor.GetHashCode();
                hash = (hash * 31) + (sideCamera != null ? sideCamera.GetInstanceID() : 0);
                hash = (hash * 31) + (arenaLayout != null ? arenaLayout.ArenaSize.GetHashCode() : 0);
                hash = (hash * 31) + (arenaLayout != null ? arenaLayout.transform.position.GetHashCode() : 0);
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
