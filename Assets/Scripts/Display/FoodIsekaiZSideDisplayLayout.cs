using FoodIsekaiZ.Gameplay;
using FoodIsekaiZ.Players;
using Fortal.UWB;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    /// <summary>
    /// Drives runtime values on the manually authored wall-display Canvas.
    /// All wall layout, sizing, and visual hierarchy are edited directly in the Unity scene.
    /// </summary>
    public sealed class FoodIsekaiZSideDisplayLayout : MonoBehaviour
    {
        private const int CustomerPanelCapacity = 6;

        [Header("Manual Wall Display (T1-T6)")]
        [Tooltip("Canvas authored in the scene. Edit its children directly; this component never creates or removes them.")]
        [SerializeField] private Canvas sideCanvas;
        [SerializeField] private Camera sideCamera;
        [SerializeField] private FoodIsekaiZArenaLayout arenaLayout;
        [SerializeField] private FoodIsekaiZGameManager gameManager;
        [SerializeField] private UWBManager uwbManager;
        [SerializeField] private UWBPlayerSpawner playerSpawner;

        [Header("Wall Background")]
        [Tooltip("Optional Image used as the wall-display background. Assign a Sprite in its Source Image field.")]
        [SerializeField] private Image backgroundImage;
        [Tooltip("Tint applied when the background Image has a Sprite assigned. White keeps the source image colors unchanged.")]
        [SerializeField] private Color backgroundImageTint = Color.white;

        [Header("Runtime Colors")]
        [SerializeField] private Color panelColor = new Color(0.065f, 0.09f, 0.13f, 1f);
        [SerializeField] private Color accentColor = new Color(0.1f, 0.85f, 1f, 1f);
        [SerializeField] private Color moneyColor = new Color(1f, 0.82f, 0.15f, 1f);

        private Text scoreText;
        private Text mvpText;
        private Text mealWaveTimerText;
        private Text intermissionCountdownText;
        private Text uwbStatusText;
        private readonly Text[] customerStatusTexts = new Text[CustomerPanelCapacity];
        private readonly Image[] customerPanelImages = new Image[CustomerPanelCapacity];
        private readonly Slider[] customerTimerSliders = new Slider[CustomerPanelCapacity];
        private readonly Image[] customerTimerFills = new Image[CustomerPanelCapacity];
        private FoodIsekaiZGameManager subscribedGameManager;
        private bool teamScoreDisplayDirty = true;
        private bool mvpDisplayDirty = true;
        private bool mealWaveDisplayDirty = true;
        private bool uwbStatusDisplayInitialized;
        private int lastUwbDisplayMode = -1;
        private int lastUwbAgeTenths = int.MinValue;
        private bool lastUwbSimulationMode;
        private string lastUwbStatus;
        private readonly bool[] customerDisplayInitialized = new bool[CustomerPanelCapacity];
        private readonly FoodType[] lastCustomerDisplayedFood = new FoodType[CustomerPanelCapacity];
        private readonly Color[] lastCustomerDisplayedColor = new Color[CustomerPanelCapacity];

        /// <summary>Gets the manually authored wall-display Canvas.</summary>
        public Canvas SideCanvas => sideCanvas;

        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                ApplyBackgroundImage();
                ConfigureWallCamera();
            }
        }

        private void Awake()
        {
            EnsureReferences();
            CacheManualDisplay();
            ApplyBackgroundImage();
            ConfigureWallCamera();
        }

        private void Start()
        {
            EnsureReferences();
            CacheManualDisplay();
            ApplyBackgroundImage();
            ConfigureWallCamera();
            SubscribeToGameEvents();
            MarkScoreDisplayDirty();
            FlushScoreDisplayUpdates();
            MarkMealWaveDisplayDirty();
            FlushMealWaveDisplayUpdate();
        }

        private void OnDisable()
        {
            UnsubscribeFromGameEvents();
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            UpdateRealtimeText();
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            FlushScoreDisplayUpdates();
            FlushMealWaveDisplayUpdate();
        }

        private void UpdateRealtimeText()
        {
            UpdateUwbStatusText();

            for (int i = 0; i < customerStatusTexts.Length; i++)
            {
                Text statusText = customerStatusTexts[i];
                Slider timerSlider = customerTimerSliders[i];
                if (gameManager != null &&
                    gameManager.CurrentMealWavePhase == MealWavePhase.Intermission)
                {
                    if (statusText != null)
                    {
                        if (statusText.text != string.Empty)
                        {
                            statusText.text = string.Empty;
                        }

                        statusText.color = Color.white;
                    }

                    customerDisplayInitialized[i] = false;
                    SetCustomerPanelVisible(i, false);
                    if (timerSlider != null)
                    {
                        timerSlider.gameObject.SetActive(false);
                    }

                    continue;
                }

                SetCustomerPanelVisible(i, true);
                if (statusText == null)
                {
                    continue;
                }

                ArenaSlot2D slot = gameManager != null ? gameManager.GetCustomerSlot(i) : null;
                if (slot == null || !slot.HasCustomer)
                {
                    if (!customerDisplayInitialized[i] ||
                        lastCustomerDisplayedFood[i] != FoodType.None ||
                        lastCustomerDisplayedColor[i] != Color.white)
                    {
                        statusText.text = string.Empty;
                        statusText.color = Color.white;
                        lastCustomerDisplayedFood[i] = FoodType.None;
                        lastCustomerDisplayedColor[i] = Color.white;
                        customerDisplayInitialized[i] = true;
                    }

                    SetCustomerPanelColor(i, WithAlphaMultiplier(panelColor, 0.5f));
                    if (timerSlider != null)
                    {
                        timerSlider.gameObject.SetActive(false);
                    }

                    continue;
                }

                FoodType requestedFood = slot.RequestedFood;
                Color requestedFoodColor = gameManager != null
                    ? gameManager.GetFoodColor(slot.RequestedFood)
                    : Color.white;
                if (!customerDisplayInitialized[i] ||
                    lastCustomerDisplayedFood[i] != requestedFood ||
                    lastCustomerDisplayedColor[i] != requestedFoodColor)
                {
                    statusText.text = requestedFood >= FoodType.Food1 && requestedFood <= FoodType.Food5
                        ? $"F{(int)requestedFood}"
                        : string.Empty;
                    statusText.color = requestedFoodColor;
                    lastCustomerDisplayedFood[i] = requestedFood;
                    lastCustomerDisplayedColor[i] = requestedFoodColor;
                    customerDisplayInitialized[i] = true;
                }

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

        private void UpdateUwbStatusText()
        {
            if (uwbStatusText == null)
            {
                return;
            }

            int displayMode;
            int ageTenths = int.MinValue;
            bool simulationMode = false;
            string managerStatus = null;

            if (playerSpawner != null && playerSpawner.IsStandaloneSimulationMode)
            {
                displayMode = 5;
            }
            else if (uwbManager == null)
            {
                displayMode = 0;
            }
            else if (uwbManager.IsReceivingFrames)
            {
                displayMode = 1;
                ageTenths = Mathf.FloorToInt(Mathf.Max(0f, uwbManager.LastFrameAgeSeconds) * 10f + 0.5f);
                simulationMode = uwbManager.IsSimulationMode;
                managerStatus = uwbManager.Status;
            }
            else if (uwbManager.IsConnected)
            {
                displayMode = uwbManager.IsReceivingProtocolFrames ? 2 : 3;
            }
            else
            {
                displayMode = 4;
                managerStatus = uwbManager.Status;
            }

            bool changed = !uwbStatusDisplayInitialized ||
                lastUwbDisplayMode != displayMode ||
                lastUwbAgeTenths != ageTenths ||
                lastUwbSimulationMode != simulationMode ||
                !string.Equals(lastUwbStatus, managerStatus, System.StringComparison.Ordinal);
            if (!changed)
            {
                return;
            }

            switch (displayMode)
            {
                case 0:
                    uwbStatusText.text = "UWB  MISSING\nMANAGER NOT FOUND";
                    uwbStatusText.color = Color.red;
                    break;

                case 1:
                    string source = simulationMode ? "UWB SIM" : "UWB";
                    uwbStatusText.text =
                        $"{source}  ONLINE  {ageTenths / 10f:0.0}s\n{ShortStatus(managerStatus)}";
                    uwbStatusText.color = Color.green;
                    break;

                case 2:
                    uwbStatusText.text = "UWB  LINK OK\nWAITING FOR TAG";
                    uwbStatusText.color = Color.yellow;
                    break;

                case 3:
                    uwbStatusText.text = "UWB  PORT OPEN\nNO BINARY DATA";
                    uwbStatusText.color = Color.yellow;
                    break;

                case 5:
                    uwbStatusText.text = "SIMULATION MODE\nUWB DISABLED";
                    uwbStatusText.color = Color.green;
                    break;

                default:
                    uwbStatusText.text = $"UWB  OFFLINE\n{ShortStatus(managerStatus)}";
                    uwbStatusText.color = Color.red;
                    break;
            }

            uwbStatusDisplayInitialized = true;
            lastUwbDisplayMode = displayMode;
            lastUwbAgeTenths = ageTenths;
            lastUwbSimulationMode = simulationMode;
            lastUwbStatus = managerStatus;
        }

        private void ResetRealtimeDisplayCaches()
        {
            uwbStatusDisplayInitialized = false;
            lastUwbDisplayMode = -1;
            lastUwbAgeTenths = int.MinValue;
            lastUwbSimulationMode = false;
            lastUwbStatus = null;

            for (int i = 0; i < customerDisplayInitialized.Length; i++)
            {
                customerDisplayInitialized[i] = false;
                lastCustomerDisplayedFood[i] = FoodType.None;
                lastCustomerDisplayedColor[i] = default;
            }
        }

        /// <summary>Refreshes the score and MVP labels from the game manager.</summary>
        public void RefreshScoreDisplay()
        {
            MarkScoreDisplayDirty();
            FlushScoreDisplayUpdates();
        }

        /// <summary>Refreshes the meal-wave labels from the game manager.</summary>
        public void RefreshMealWaveDisplay()
        {
            MarkMealWaveDisplayDirty();
            FlushMealWaveDisplayUpdate();
        }

        private void MarkScoreDisplayDirty()
        {
            teamScoreDisplayDirty = true;
            mvpDisplayDirty = true;
        }

        private void MarkMealWaveDisplayDirty()
        {
            mealWaveDisplayDirty = true;
        }

        private void FlushScoreDisplayUpdates()
        {
            if (teamScoreDisplayDirty)
            {
                if (scoreText != null)
                {
                    int score = gameManager != null ? gameManager.TeamScore : 0;
                    scoreText.text = $"TEAM SCORE  {FormatScore(score)}";
                }

                teamScoreDisplayDirty = false;
            }

            if (mvpDisplayDirty)
            {
                if (mvpText != null)
                {
                    if (gameManager != null && gameManager.TryGetMvp(out int playerId, out int playerScore))
                    {
                        mvpText.text = $"MVP  P{playerId}  {FormatScore(playerScore)}";
                    }
                    else
                    {
                        mvpText.text = "MVP  --  0000";
                    }
                }

                mvpDisplayDirty = false;
            }
        }

        private void FlushMealWaveDisplayUpdate()
        {
            if (!mealWaveDisplayDirty)
            {
                return;
            }

            mealWaveDisplayDirty = false;
            if (mealWaveTimerText == null || intermissionCountdownText == null)
            {
                return;
            }

            if (gameManager == null || !gameManager.UsesMealWaves)
            {
                mealWaveTimerText.text = string.Empty;
                intermissionCountdownText.gameObject.SetActive(false);
                return;
            }

            int seconds = Mathf.Max(0, Mathf.CeilToInt(gameManager.MealPhaseRemainingSeconds));
            switch (gameManager.CurrentMealWavePhase)
            {
                case MealWavePhase.Active:
                    mealWaveTimerText.text =
                        $"{gameManager.CurrentWaveName}  {FormatClock(seconds)}  " +
                        $"({gameManager.CurrentWaveNumber}/{gameManager.TotalWaveCount})";
                    intermissionCountdownText.gameObject.SetActive(false);
                    break;

                case MealWavePhase.Intermission:
                    mealWaveTimerText.text = "BREAK TIME";
                    intermissionCountdownText.text =
                        $"NEXT  {gameManager.NextWaveName}\n{seconds}";
                    intermissionCountdownText.color = Color.white;
                    intermissionCountdownText.gameObject.SetActive(true);
                    break;

                case MealWavePhase.Completed:
                    mealWaveTimerText.text = "ALL MEALS COMPLETE";
                    intermissionCountdownText.text = "SERVICE COMPLETE";
                    intermissionCountdownText.color = moneyColor;
                    intermissionCountdownText.gameObject.SetActive(true);
                    break;

                default:
                    mealWaveTimerText.text = "READY";
                    intermissionCountdownText.gameObject.SetActive(false);
                    break;
            }
        }

        private void SubscribeToGameEvents()
        {
            if (!Application.isPlaying || subscribedGameManager == gameManager)
            {
                return;
            }

            UnsubscribeFromGameEvents();
            if (gameManager == null)
            {
                return;
            }

            subscribedGameManager = gameManager;
            subscribedGameManager.PlayerScoreChanged += HandlePlayerScoreChanged;
            subscribedGameManager.TeamScoreChanged += HandleTeamScoreChanged;
            subscribedGameManager.MealWaveDisplayChanged += HandleMealWaveDisplayChanged;
        }

        private void UnsubscribeFromGameEvents()
        {
            if (subscribedGameManager == null)
            {
                return;
            }

            subscribedGameManager.PlayerScoreChanged -= HandlePlayerScoreChanged;
            subscribedGameManager.TeamScoreChanged -= HandleTeamScoreChanged;
            subscribedGameManager.MealWaveDisplayChanged -= HandleMealWaveDisplayChanged;
            subscribedGameManager = null;
        }

        private void HandlePlayerScoreChanged(int playerId, int playerScore)
        {
            mvpDisplayDirty = true;
        }

        private void HandleTeamScoreChanged(int teamScore)
        {
            teamScoreDisplayDirty = true;
        }

        private void HandleMealWaveDisplayChanged()
        {
            mealWaveDisplayDirty = true;
        }

        private void SetCustomerPanelColor(int index, Color color)
        {
            if (index < 0 || index >= customerPanelImages.Length || customerPanelImages[index] == null)
            {
                return;
            }

            customerPanelImages[index].color = color;
        }

        private static Color WithAlphaMultiplier(Color color, float multiplier)
        {
            color.a *= Mathf.Clamp01(multiplier);
            return color;
        }

        private void SetCustomerPanelVisible(int index, bool visible)
        {
            if (index < 0 || index >= customerPanelImages.Length || customerPanelImages[index] == null)
            {
                return;
            }

            customerPanelImages[index].enabled = visible;
        }

        private static string ShortStatus(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "NO STATUS";
            }

            value = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
            const int maxLength = 28;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength - 3) + "...";
        }

        private static string FormatScore(int score)
        {
            return score < 0
                ? $"-{Mathf.Abs(score):0000}"
                : $"{score:0000}";
        }

        private static string FormatClock(int totalSeconds)
        {
            totalSeconds = Mathf.Max(0, totalSeconds);
            return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
        }

        private void CacheManualDisplay()
        {
            if (sideCanvas == null)
            {
                Debug.LogError(
                    $"[{nameof(FoodIsekaiZSideDisplayLayout)}] Wall Canvas is not assigned. " +
                    "Assign the manually authored SideDisplay Canvas in the Inspector.",
                    this);
                return;
            }

            Transform root = sideCanvas.transform;
            if (backgroundImage == null)
            {
                backgroundImage = GetManualComponent<Image>(root, "Background");
            }

            scoreText = GetManualComponent<Text>(root, "TeamScore");
            mvpText = GetManualComponent<Text>(root, "MVPScore");
            mealWaveTimerText = GetManualComponent<Text>(root, "MealWaveTimer");
            intermissionCountdownText = GetManualComponent<Text>(root, "MealIntermissionCountdown");
            uwbStatusText = GetManualComponent<Text>(root, "UWBStatus");

            for (int i = 0; i < customerStatusTexts.Length; i++)
            {
                Transform panel = FindManualTransform(root, $"CustomerPanel{i + 1}");
                if (panel == null)
                {
                    continue;
                }

                customerPanelImages[i] = GetManualComponentInHierarchy<Image>(panel);
                customerStatusTexts[i] = GetManualComponent<Text>(panel, "Status");
                customerTimerSliders[i] = GetManualComponent<Slider>(panel, "OrderTimer");
                customerTimerFills[i] = GetManualComponent<Image>(panel, "OrderTimer/FillArea/Fill");
            }

            ResetRealtimeDisplayCaches();
        }

        private void ApplyBackgroundImage()
        {
            if (backgroundImage == null)
            {
                return;
            }

            backgroundImage.enabled = true;
            backgroundImage.raycastTarget = false;
            backgroundImage.type = Image.Type.Simple;
            backgroundImage.preserveAspect = false;
            backgroundImage.transform.SetAsFirstSibling();

            if (backgroundImage.sprite != null)
            {
                backgroundImage.color = backgroundImageTint;
            }
        }

        private void ConfigureWallCamera()
        {
            if (sideCanvas == null || sideCamera == null)
            {
                return;
            }

            RectTransform canvasRect = sideCanvas.GetComponent<RectTransform>();
            if (canvasRect == null)
            {
                return;
            }

            var corners = new Vector3[4];
            canvasRect.GetWorldCorners(corners);
            float wallWidth = Vector3.Distance(corners[0], corners[3]);
            float wallHeight = Vector3.Distance(corners[0], corners[1]);
            if (wallWidth <= Mathf.Epsilon || wallHeight <= Mathf.Epsilon)
            {
                return;
            }

            Vector3 wallCenter = (corners[0] + corners[2]) * 0.5f;
            sideCamera.targetDisplay = DisplayOutput.WallDisplayIndex;
            sideCamera.orthographic = true;
            sideCamera.aspect = wallWidth / wallHeight;
            sideCamera.orthographicSize = wallHeight * 0.5f;
            sideCamera.transform.position = wallCenter - (sideCamera.transform.forward * 10f);
        }

        private static T GetManualComponent<T>(Transform root, string relativePath) where T : Component
        {
            Transform target = FindManualTransform(root, relativePath);
            return target != null ? target.GetComponent<T>() : null;
        }

        private static T GetManualComponentInHierarchy<T>(Transform root) where T : Component
        {
            if (root == null)
            {
                return null;
            }

            T component = root.GetComponent<T>();
            return component != null ? component : root.GetComponentInChildren<T>(true);
        }

        private static Transform FindManualTransform(Transform root, string relativePath)
        {
            if (root == null || string.IsNullOrWhiteSpace(relativePath))
            {
                return null;
            }

            Transform directTarget = root.Find(relativePath);
            if (directTarget != null)
            {
                return directTarget;
            }

            string[] pathParts = relativePath.Split('/');
            return FindNestedTransform(root, pathParts, 0);
        }

        private static Transform FindNestedTransform(
            Transform current,
            string[] pathParts,
            int pathIndex)
        {
            if (current.name == pathParts[pathIndex])
            {
                if (pathIndex == pathParts.Length - 1)
                {
                    return current;
                }

                for (int i = 0; i < current.childCount; i++)
                {
                    Transform nestedTarget = FindNestedTransform(
                        current.GetChild(i),
                        pathParts,
                        pathIndex + 1);
                    if (nestedTarget != null)
                    {
                        return nestedTarget;
                    }
                }
            }

            for (int i = 0; i < current.childCount; i++)
            {
                Transform nestedTarget = FindNestedTransform(
                    current.GetChild(i),
                    pathParts,
                    pathIndex);
                if (nestedTarget != null)
                {
                    return nestedTarget;
                }
            }

            return null;
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
    }
}
