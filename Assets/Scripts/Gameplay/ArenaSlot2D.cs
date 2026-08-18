using UnityEngine;
using UnityEngine.Serialization;

namespace FoodIsekaiZ.Gameplay
{
    /// <summary>Trigger zone 3D บนพื้น XZ ทั้ง Customer, Food Station และ Deposit ใช้ component เดียวกัน</summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ArenaSlot2D : MonoBehaviour
    {
        [Header("Slot Setup")]
        [SerializeField] private string slotId = "Slot-01";
        [SerializeField] private ArenaSlotType slotType;
        [SerializeField] private FoodType stationFood = FoodType.None;
        [SerializeField] private FoodIsekaiZGameManager gameManager;

        [Header("Optional Visuals")]
        [SerializeField] private GameObject customerVisual;
        [SerializeField] private GameObject moneyVisual;
        [SerializeField] private Renderer customerVisualRenderer;
        [SerializeField] private TextMesh statusLabel;

        [Header("Floor Text Warning")]
        [SerializeField, Range(0f, 1f)] private float warningTimeNormalized = 0.25f;
        [SerializeField, Min(0.1f)] private float warningBlinkCyclesPerSecond = 3f;
        [SerializeField] private Color normalBlockColor = new Color(1f, 0.5f, 0.16f, 1f);
        [FormerlySerializedAs("warningTextColor")]
        [SerializeField] private Color warningBlockColor = new Color(1f, 0.08f, 0.04f, 1f);
        [SerializeField] private Color normalLabelColor = Color.white;
        [SerializeField] private Color warningLabelColor = new Color(1f, 0.9f, 0.15f, 1f);

        [Header("Customer Runtime (Read Only)")]
        [SerializeField] private CustomerSlotState customerState = CustomerSlotState.Empty;
        [SerializeField] private string customerDisplayName = string.Empty;
        [SerializeField] private Color customerColor = Color.white;
        [SerializeField] private FoodType requestedFood = FoodType.None;
        [SerializeField, Min(0f)] private float stateRemainingSeconds;
        [SerializeField, Min(0f)] private float stateDurationSeconds;
        [SerializeField, Min(0f)] private float orderDurationSeconds;
        [SerializeField, Min(0)] private int orderReward;
        [SerializeField, Min(0)] private int availableMoney;

        private MaterialPropertyBlock customerVisualProperties;
        private MaterialPropertyBlock slotVisualProperties;
        private Renderer slotRenderer;

        public string SlotId => slotId;
        public ArenaSlotType SlotType => slotType;
        public FoodType StationFood => stationFood;
        public CustomerSlotState CustomerState => customerState;
        public bool HasCustomer => customerState == CustomerSlotState.WaitingForFood || customerState == CustomerSlotState.Eating;
        public string CustomerDisplayName => customerDisplayName;
        public Color CustomerColor => customerColor;
        public FoodType RequestedFood => requestedFood;
        public float StateRemainingSeconds => stateRemainingSeconds;
        public float StateTimeNormalized => stateDurationSeconds > 0f
            ? Mathf.Clamp01(stateRemainingSeconds / stateDurationSeconds)
            : 0f;
        public float OrderDurationSeconds => orderDurationSeconds;
        public float OrderTimeNormalized => orderDurationSeconds > 0f
            ? Mathf.Clamp01(stateRemainingSeconds / orderDurationSeconds)
            : 0f;
        public int OrderReward => orderReward;
        public int AvailableMoney => availableMoney;

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
            if (gameManager == null)
            {
                gameManager = FindAnyObjectByType<FoodIsekaiZGameManager>();
            }

            RefreshVisuals();
        }

        private void OnTriggerEnter(Collider other)
        {
            FoodIsekaiZPlayerState player = other.GetComponentInParent<FoodIsekaiZPlayerState>();
            if (player != null)
            {
                gameManager?.TryInteract(player, this);
            }
        }

        /// <summary>ใช้โดย Arena Layout เพื่อสร้างและกำหนด Slot จากโครงสนามอัตโนมัติ</summary>
        public void Configure(
            string newSlotId,
            ArenaSlotType newSlotType,
            FoodType newStationFood,
            FoodIsekaiZGameManager newGameManager)
        {
            slotId = newSlotId;
            slotType = newSlotType;
            stationFood = newSlotType == ArenaSlotType.FoodStation
                ? newStationFood
                : FoodType.None;
            gameManager = newGameManager;
            RefreshVisuals();
        }

        public void ConfigureVisuals(
            GameObject newCustomerVisual,
            GameObject newMoneyVisual,
            Renderer newCustomerVisualRenderer,
            TextMesh newStatusLabel)
        {
            customerVisual = newCustomerVisual;
            moneyVisual = newMoneyVisual;
            customerVisualRenderer = newCustomerVisualRenderer;
            statusLabel = newStatusLabel;
            RefreshVisuals();
        }

        public void ConfigureFloorTextWarning(
            float normalizedThreshold,
            float blinkCyclesPerSecond,
            Color baseBlockColor,
            Color alertBlockColor,
            Color alertLabelColor)
        {
            warningTimeNormalized = Mathf.Clamp01(normalizedThreshold);
            warningBlinkCyclesPerSecond = Mathf.Max(0.1f, blinkCyclesPerSecond);
            normalBlockColor = baseBlockColor;
            warningBlockColor = alertBlockColor;
            warningLabelColor = alertLabelColor;
            RefreshVisuals();
        }

        public void ConfigureCustomer(
            string displayName,
            Color displayColor,
            FoodType food,
            float orderTimeSeconds,
            int reward)
        {
            customerDisplayName = string.IsNullOrWhiteSpace(displayName) ? "CUSTOMER" : displayName;
            customerColor = displayColor;
            requestedFood = food;
            customerState = CustomerSlotState.WaitingForFood;
            orderDurationSeconds = Mathf.Max(0.1f, orderTimeSeconds);
            stateDurationSeconds = orderDurationSeconds;
            stateRemainingSeconds = stateDurationSeconds;
            orderReward = Mathf.Max(0, reward);
            availableMoney = 0;
            RefreshVisuals();
        }

        public bool TryBeginEating(float eatingDurationSeconds)
        {
            if (slotType != ArenaSlotType.Customer || customerState != CustomerSlotState.WaitingForFood)
            {
                return false;
            }

            customerState = CustomerSlotState.Eating;
            stateDurationSeconds = Mathf.Max(0.1f, eatingDurationSeconds);
            stateRemainingSeconds = stateDurationSeconds;
            RefreshVisuals();
            return true;
        }

        public bool AdvanceStateTimer(float deltaTime)
        {
            if (customerState != CustomerSlotState.WaitingForFood && customerState != CustomerSlotState.Eating)
            {
                return false;
            }

            stateRemainingSeconds = Mathf.Max(0f, stateRemainingSeconds - Mathf.Max(0f, deltaTime));
            RefreshVisuals();
            return stateRemainingSeconds <= 0f;
        }

        public void SpawnMoney(int amount)
        {
            if (slotType != ArenaSlotType.Customer)
            {
                return;
            }

            availableMoney = Mathf.Max(0, amount);
            customerState = CustomerSlotState.MoneyAvailable;
            stateRemainingSeconds = 0f;
            stateDurationSeconds = 0f;
            RefreshVisuals();
        }

        public int CollectMoney()
        {
            if (customerState != CustomerSlotState.MoneyAvailable)
            {
                return 0;
            }

            int collected = availableMoney;
            ClearCustomer();
            return collected;
        }

        public void ClearCustomer()
        {
            customerState = CustomerSlotState.Empty;
            customerDisplayName = string.Empty;
            customerColor = Color.white;
            requestedFood = FoodType.None;
            stateRemainingSeconds = 0f;
            stateDurationSeconds = 0f;
            orderDurationSeconds = 0f;
            orderReward = 0;
            availableMoney = 0;
            RefreshVisuals();
        }

        private void RefreshVisuals()
        {
            bool showCustomer = customerState == CustomerSlotState.WaitingForFood ||
                customerState == CustomerSlotState.Eating;
            if (customerVisual != null)
            {
                customerVisual.SetActive(showCustomer);
            }

            if (showCustomer && customerVisualRenderer != null)
            {
                if (customerVisualProperties == null)
                {
                    customerVisualProperties = new MaterialPropertyBlock();
                }

                customerVisualRenderer.GetPropertyBlock(customerVisualProperties);
                customerVisualProperties.SetColor("_BaseColor", customerColor);
                customerVisualProperties.SetColor("_Color", customerColor);
                customerVisualRenderer.SetPropertyBlock(customerVisualProperties);
            }

            if (moneyVisual != null)
            {
                moneyVisual.SetActive(customerState == CustomerSlotState.MoneyAvailable && availableMoney > 0);
            }

            if (statusLabel != null && slotType == ArenaSlotType.Customer)
            {
                statusLabel.text = GetShortSlotId();
            }

            RefreshFloorWarningAppearance();
        }

        private void RefreshFloorWarningAppearance()
        {
            if (slotType != ArenaSlotType.Customer)
            {
                return;
            }

            bool nearTimeout = customerState == CustomerSlotState.WaitingForFood &&
                OrderTimeNormalized > 0f && OrderTimeNormalized <= warningTimeNormalized;
            bool alertPhase = nearTimeout && Mathf.Repeat(
                Time.unscaledTime * warningBlinkCyclesPerSecond,
                1f) < 0.5f;

            if (statusLabel != null)
            {
                statusLabel.color = alertPhase ? warningLabelColor : normalLabelColor;
            }

            if (slotRenderer == null)
            {
                slotRenderer = GetComponent<Renderer>();
            }

            if (slotRenderer == null)
            {
                return;
            }

            if (slotVisualProperties == null)
            {
                slotVisualProperties = new MaterialPropertyBlock();
            }

            Color blockColor = alertPhase ? warningBlockColor : normalBlockColor;
            slotRenderer.GetPropertyBlock(slotVisualProperties);
            slotVisualProperties.SetColor("_BaseColor", blockColor);
            slotVisualProperties.SetColor("_Color", blockColor);
            slotRenderer.SetPropertyBlock(slotVisualProperties);
        }

        private string GetShortSlotId()
        {
            if (!string.IsNullOrEmpty(slotId) && slotId.StartsWith("CustomerSlot"))
            {
                string number = slotId.Substring("CustomerSlot".Length).TrimStart('0');
                return $"C{(string.IsNullOrEmpty(number) ? "1" : number)}";
            }

            return slotId;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            Collider trigger = GetComponent<Collider>();
            if (trigger != null)
            {
                trigger.isTrigger = true;
            }

            if (slotType != ArenaSlotType.FoodStation)
            {
                stationFood = FoodType.None;
            }

            warningTimeNormalized = Mathf.Clamp01(warningTimeNormalized);
            warningBlinkCyclesPerSecond = Mathf.Max(0.1f, warningBlinkCyclesPerSecond);
        }
#endif
    }
}
