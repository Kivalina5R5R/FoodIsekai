using UnityEngine;

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
        [SerializeField] private GameObject moneyVisual;

        [Header("Customer Runtime (Read Only)")]
        [SerializeField] private CustomerSlotState customerState = CustomerSlotState.WaitingForFood;
        [SerializeField] private FoodType requestedFood = FoodType.None;
        [SerializeField, Min(0)] private int availableMoney;

        public string SlotId => slotId;
        public ArenaSlotType SlotType => slotType;
        public FoodType StationFood => stationFood;
        public CustomerSlotState CustomerState => customerState;
        public FoodType RequestedFood => requestedFood;
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
        }

        public void ConfigureCustomer(FoodType food)
        {
            requestedFood = food;
            customerState = CustomerSlotState.WaitingForFood;
            availableMoney = 0;
            RefreshVisuals();
        }

        public bool TryBeginEating()
        {
            if (slotType != ArenaSlotType.Customer || customerState != CustomerSlotState.WaitingForFood)
            {
                return false;
            }

            customerState = CustomerSlotState.Eating;
            RefreshVisuals();
            return true;
        }

        public void SpawnMoney(int amount)
        {
            if (slotType != ArenaSlotType.Customer)
            {
                return;
            }

            availableMoney = Mathf.Max(0, amount);
            customerState = CustomerSlotState.MoneyAvailable;
            RefreshVisuals();
        }

        public int CollectMoney()
        {
            if (customerState != CustomerSlotState.MoneyAvailable)
            {
                return 0;
            }

            int collected = availableMoney;
            availableMoney = 0;
            RefreshVisuals();
            return collected;
        }

        private void RefreshVisuals()
        {
            if (moneyVisual != null)
            {
                moneyVisual.SetActive(customerState == CustomerSlotState.MoneyAvailable && availableMoney > 0);
            }
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
        }
#endif
    }
}
