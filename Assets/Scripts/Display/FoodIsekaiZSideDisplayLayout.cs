using FoodIsekaiZ.Gameplay;
using Fortal.UWB;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{

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

        [Header("Wall Screen Background")]
        [Tooltip("Optional texture stretched across the whole wall display behind the UI.")]
        [SerializeField] private Texture2D wallBackgroundTexture;
        [SerializeField] private Color wallBackgroundTextureTint = Color.white;
        [SerializeField] private Rect wallBackgroundUvRect = new Rect(0f, 0f, 1f, 1f);

        [Header("Scene References")]
        [SerializeField] private Camera sideCamera;
        [SerializeField] private FoodIsekaiZArenaLayout arenaLayout;
        [SerializeField] private FoodIsekaiZGameManager gameManager;
        [SerializeField] private UWBManager uwbManager;
        [SerializeField] private bool autoBuildPreview = true;
        [Tooltip("Off keeps manual Scene View layout edits. Use Build / Refresh Side Display when you want a full rebuild.")]
        [SerializeField] private bool rebuildPreviewWhenSettingsChange;

        private Canvas sideCanvas;
        private Text scoreText;
        private Text uwbStatusText;
        private readonly Text[] customerStatusTexts = new Text[4];
        private readonly Image[] customerPanelImages = new Image[4];
        private readonly Slider[] customerTimerSliders = new Slider[4];
        private readonly Image[] customerTimerFills = new Image[4];
        private Sprite generatedWallBackgroundSprite;
        private Texture2D generatedWallBackgroundTexture;
        private Rect generatedWallBackgroundUvRect;
        private bool isBuilding;
        private int appliedHash;

        public Canvas SideCanvas => sideCanvas;

        private void OnEnable()
        {
            Transform existing = transform.Find(GeneratedRootName);
            if (existing != null)
            {
                Transform obsoleteAccent = existing.Find("TopAccent");
                if (obsoleteAccent != null)
                {
                    SafeDestroy(obsoleteAccent.gameObject);
                }

                CacheGeneratedDisplay(existing);
                ApplyWallBackgroundAppearance();
                EnsureReferences();
                appliedHash = CalculateHash();
                return;
            }

            if (autoBuildPreview)
            {
                BuildSideDisplay();
            }
        }

        private void Start()
        {
            EnsureReferences();
        }

        private void OnDestroy()
        {
            ReleaseGeneratedWallBackgroundSprite();
        }

        private void Update()
        {
            ApplyWallBackgroundAppearance();

            if (!Application.isPlaying)
            {
                if (autoBuildPreview && rebuildPreviewWhenSettingsChange && !isBuilding)
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

            for (int i = 0; i < customerStatusTexts.Length; i++)
            {
                float cellMin = i / (float)customerStatusTexts.Length;
                float cellMax = (i + 1f) / customerStatusTexts.Length;
                Vector2 min = new Vector2(cellMin + 0.008f, 0.08f);
                Vector2 max = new Vector2(cellMax - 0.008f, 0.49f);
                Transform panel = CreatePanel($"CustomerPanel{i + 1}", canvasObject.transform, min, max, panelColor);
                customerPanelImages[i] = panel.GetComponent<Image>();
                customerStatusTexts[i] = CreateText(
                    "Status",
                    panel,
                    new Vector2(0.05f, 0.30f),
                    new Vector2(0.95f, 0.96f),
                    string.Empty,
                    68,
                    TextAnchor.MiddleCenter,
                    Color.white,
                    font);
                customerStatusTexts[i].fontStyle = FontStyle.Bold;
                customerTimerSliders[i] = CreateTimerSlider(
                    "OrderTimer",
                    panel,
                    new Vector2(0.08f, 0.12f),
                    new Vector2(0.92f, 0.29f),
                    backgroundColor,
                    accentColor,
                    out customerTimerFills[i]);
                customerTimerSliders[i].gameObject.SetActive(false);
            }

            ConfigurePhysicalWall(canvasObject.GetComponent<RectTransform>());
            ConfigureSideCamera();
            ApplyWallBackgroundAppearance();
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
                if (uwbManager == null)
                {
                    uwbStatusText.text = "UWB  MISSING\nMANAGER NOT FOUND";
                    uwbStatusText.color = Color.red;
                }
                else if (uwbManager.IsReceivingFrames)
                {
                    string source = uwbManager.IsSimulationMode ? "UWB SIM" : "UWB";
                    uwbStatusText.text = $"{source}  ONLINE  {uwbManager.LastFrameAgeSeconds:0.0}s\n{ShortStatus(uwbManager.Status)}";
                    uwbStatusText.color = Color.green;
                }
                else if (uwbManager.IsConnected)
                {
                    uwbStatusText.text = uwbManager.IsReceivingProtocolFrames
                        ? "UWB  LINK OK\nWAITING FOR TAG"
                        : "UWB  PORT OPEN\nNO BINARY DATA";
                    uwbStatusText.color = Color.yellow;
                }
                else
                {
                    uwbStatusText.text = $"UWB  OFFLINE\n{ShortStatus(uwbManager.Status)}";
                    uwbStatusText.color = Color.red;
                }
            }

            for (int i = 0; i < customerStatusTexts.Length; i++)
            {
                Text statusText = customerStatusTexts[i];
                Slider timerSlider = customerTimerSliders[i];
                if (statusText == null)
                {
                    continue;
                }

                ArenaSlot2D slot = gameManager != null ? gameManager.GetCustomerSlot(i) : null;
                if (slot == null || slot.CustomerState == CustomerSlotState.Empty)
                {
                    statusText.text = string.Empty;
                    statusText.color = Color.white;
                    SetCustomerPanelColor(i, panelColor);
                    if (timerSlider != null)
                    {
                        timerSlider.gameObject.SetActive(false);
                    }
                    continue;
                }

                statusText.text = slot.RequestedFood >= FoodType.Food1 && slot.RequestedFood <= FoodType.Food5
                    ? $"F{(int)slot.RequestedFood}"
                    : string.Empty;
                statusText.color = gameManager != null
                    ? gameManager.GetFoodColor(slot.RequestedFood)
                    : Color.white;
                SetCustomerPanelColor(i, panelColor);

                switch (slot.CustomerState)
                {
                    case CustomerSlotState.WaitingForFood:
                    case CustomerSlotState.Eating:
                        if (timerSlider != null)
                        {
                            timerSlider.gameObject.SetActive(true);
                            timerSlider.SetValueWithoutNotify(slot.StateTimeNormalized);
                        }

                        if (customerTimerFills[i] != null)
                        {
                            customerTimerFills[i].color = accentColor;
                        }
                        break;

                    case CustomerSlotState.MoneyAvailable:
                        if (timerSlider != null)
                        {
                            timerSlider.gameObject.SetActive(false);
                        }
                        break;
                }
            }
        }

        private void SetCustomerPanelColor(int index, Color color)
        {
            if (index < 0 || index >= customerPanelImages.Length || customerPanelImages[index] == null)
            {
                return;
            }

            color.a = 1f;
            customerPanelImages[index].color = color;
        }

        private static string ShortStatus(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "NO STATUS";
            }

            const int maxLength = 34;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength - 3) + "...";
        }

        private void CacheGeneratedDisplay(Transform root)
        {
            sideCanvas = root.GetComponent<Canvas>();
            scoreText = GetGeneratedComponent<Text>(root, "TeamMoney");
            uwbStatusText = GetGeneratedComponent<Text>(root, "UWBStatus");
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            for (int i = 0; i < customerStatusTexts.Length; i++)
            {
                bool migratedLegacyPanel = false;
                Transform panel = root.Find($"CustomerPanel{i + 1}");
                if (panel == null)
                {
                    panel = root.Find($"PlayerPanel{i + 1}");
                    if (panel != null)
                    {
                        panel.name = $"CustomerPanel{i + 1}";
                        migratedLegacyPanel = true;
                    }
                }

                if (panel == null)
                {
                    continue;
                }

                customerPanelImages[i] = panel != null ? panel.GetComponent<Image>() : null;
                customerStatusTexts[i] = GetGeneratedComponent<Text>(panel, "Status");
                if (customerStatusTexts[i] == null)
                {
                    customerStatusTexts[i] = CreateText(
                        "Status",
                        panel,
                        new Vector2(0.05f, 0.30f),
                        new Vector2(0.95f, 0.96f),
                        string.Empty,
                        68,
                        TextAnchor.MiddleCenter,
                        Color.white,
                        font);
                }
                else if (migratedLegacyPanel)
                {
                    // Upgrade only the old P#/TAG layout. Once it is a CustomerPanel,
                    // keep any manual RectTransform edits made in the Scene view.
                    SetRect(
                        customerStatusTexts[i].rectTransform,
                        new Vector2(0.05f, 0.30f),
                        new Vector2(0.95f, 0.96f));
                    customerStatusTexts[i].font = font;
                    customerStatusTexts[i].fontSize = 68;
                    customerStatusTexts[i].alignment = TextAnchor.MiddleCenter;
                    customerStatusTexts[i].fontStyle = FontStyle.Bold;
                }

                customerTimerSliders[i] = GetGeneratedComponent<Slider>(panel, "OrderTimer");
                if (customerTimerSliders[i] == null)
                {
                    customerTimerSliders[i] = CreateTimerSlider(
                        "OrderTimer",
                        panel,
                        new Vector2(0.08f, 0.12f),
                        new Vector2(0.92f, 0.29f),
                        backgroundColor,
                        accentColor,
                        out customerTimerFills[i]);
                    customerTimerSliders[i].gameObject.SetActive(false);
                }
                else
                {
                    customerTimerFills[i] = GetGeneratedComponent<Image>(panel, "OrderTimer/FillArea/Fill");
                }
            }
        }

        private void ApplyWallBackgroundAppearance()
        {
            Transform root = sideCanvas != null ? sideCanvas.transform : transform.Find(GeneratedRootName);
            Transform background = root != null ? root.Find("Background") : null;
            if (background == null)
            {
                return;
            }

            Image image = background.GetComponent<Image>();
            RawImage rawImage = background.GetComponent<RawImage>();
            if (image == null && rawImage != null)
            {
                rawImage.enabled = true;
                rawImage.raycastTarget = false;
                rawImage.texture = wallBackgroundTexture;
                rawImage.color = wallBackgroundTexture != null
                    ? wallBackgroundTextureTint
                    : backgroundColor;
                rawImage.uvRect = wallBackgroundUvRect;
                return;
            }

            if (image == null)
            {
                image = background.gameObject.AddComponent<Image>();
            }

            if (rawImage != null)
            {
                rawImage.enabled = false;
            }

            image.enabled = true;
            image.raycastTarget = false;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.color = wallBackgroundTexture != null
                ? wallBackgroundTextureTint
                : backgroundColor;
            image.sprite = GetOrCreateWallBackgroundSprite();
        }

        private Sprite GetOrCreateWallBackgroundSprite()
        {
            if (wallBackgroundTexture == null)
            {
                ReleaseGeneratedWallBackgroundSprite();
                return null;
            }

            if (generatedWallBackgroundSprite != null &&
                generatedWallBackgroundTexture == wallBackgroundTexture &&
                generatedWallBackgroundUvRect == wallBackgroundUvRect)
            {
                return generatedWallBackgroundSprite;
            }

            ReleaseGeneratedWallBackgroundSprite();
            float xMin = Mathf.Clamp01(wallBackgroundUvRect.x);
            float yMin = Mathf.Clamp01(wallBackgroundUvRect.y);
            float xMax = Mathf.Clamp01(wallBackgroundUvRect.x + wallBackgroundUvRect.width);
            float yMax = Mathf.Clamp01(wallBackgroundUvRect.y + wallBackgroundUvRect.height);
            if (xMax <= xMin || yMax <= yMin)
            {
                xMin = 0f;
                yMin = 0f;
                xMax = 1f;
                yMax = 1f;
            }

            Rect pixelRect = Rect.MinMaxRect(
                xMin * wallBackgroundTexture.width,
                yMin * wallBackgroundTexture.height,
                xMax * wallBackgroundTexture.width,
                yMax * wallBackgroundTexture.height);
            generatedWallBackgroundSprite = Sprite.Create(
                wallBackgroundTexture,
                pixelRect,
                new Vector2(0.5f, 0.5f),
                100f);
            generatedWallBackgroundSprite.name = "Runtime Wall Background";
            generatedWallBackgroundSprite.hideFlags = HideFlags.HideAndDontSave;
            generatedWallBackgroundTexture = wallBackgroundTexture;
            generatedWallBackgroundUvRect = wallBackgroundUvRect;
            return generatedWallBackgroundSprite;
        }

        private void ReleaseGeneratedWallBackgroundSprite()
        {
            if (generatedWallBackgroundSprite != null)
            {
                SafeDestroy(generatedWallBackgroundSprite);
            }

            generatedWallBackgroundSprite = null;
            generatedWallBackgroundTexture = null;
        }

        private static T GetGeneratedComponent<T>(Transform root, string relativePath) where T : Component
        {
            if (root == null)
            {
                return null;
            }

            Transform target = root.Find(relativePath);
            return target != null ? target.GetComponent<T>() : null;
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

        private static Slider CreateTimerSlider(
            string objectName,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color trackColor,
            Color fillColor,
            out Image fillImage)
        {
            GameObject sliderObject = new GameObject(objectName, typeof(RectTransform), typeof(Slider));
            sliderObject.transform.SetParent(parent, false);
            SetRect(sliderObject.GetComponent<RectTransform>(), anchorMin, anchorMax);

            Transform track = CreatePanel("Track", sliderObject.transform, Vector2.zero, Vector2.one, trackColor);
            Image trackImage = track.GetComponent<Image>();
            trackImage.raycastTarget = false;

            GameObject fillAreaObject = new GameObject("FillArea", typeof(RectTransform));
            fillAreaObject.transform.SetParent(sliderObject.transform, false);
            SetRect(
                fillAreaObject.GetComponent<RectTransform>(),
                new Vector2(0.025f, 0.16f),
                new Vector2(0.975f, 0.84f));

            Transform fill = CreatePanel("Fill", fillAreaObject.transform, Vector2.zero, Vector2.one, fillColor);
            fillImage = fill.GetComponent<Image>();
            fillImage.raycastTarget = false;

            Slider slider = sliderObject.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            slider.wholeNumbers = false;
            slider.direction = Slider.Direction.LeftToRight;
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = null;
            slider.targetGraphic = fillImage;
            slider.interactable = false;
            slider.transition = Selectable.Transition.None;
            Navigation navigation = slider.navigation;
            navigation.mode = Navigation.Mode.None;
            slider.navigation = navigation;
            return slider;
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
            for (int i = 0; i < customerStatusTexts.Length; i++)
            {
                customerStatusTexts[i] = null;
                customerPanelImages[i] = null;
                customerTimerSliders[i] = null;
                customerTimerFills[i] = null;
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
                hash = (hash * 31) + (wallBackgroundTexture != null ? wallBackgroundTexture.GetInstanceID() : 0);
                hash = (hash * 31) + wallBackgroundTextureTint.GetHashCode();
                hash = (hash * 31) + wallBackgroundUvRect.GetHashCode();
                hash = (hash * 31) + rebuildPreviewWhenSettingsChange.GetHashCode();
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
