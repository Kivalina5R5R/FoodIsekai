using FoodIsekaiZ.Players;
using UnityEngine;

namespace FoodIsekaiZ.Gameplay
{
    /// <summary>Inventory/เงินของผู้เล่น แยกจากระบบ tracking เพื่อให้ทดสอบด้วย keyboard player ได้</summary>
    public sealed class FoodIsekaiZPlayerState : MonoBehaviour
    {
        [SerializeField] private UWBPlayerController trackedPlayer;
        [SerializeField, Min(1)] private int fallbackPlayerId = 1;

        [Header("Runtime (Read Only)")]
        [SerializeField] private FoodType heldFood = FoodType.None;
        [SerializeField, Min(0)] private int carriedMoney;

        public int PlayerId => trackedPlayer != null ? trackedPlayer.PlayerId : fallbackPlayerId;
        public FoodType HeldFood => heldFood;
        public int CarriedMoney => carriedMoney;

        private void Awake()
        {
            if (trackedPlayer == null)
            {
                trackedPlayer = GetComponent<UWBPlayerController>();
            }
        }

        public bool TryPickFood(FoodType food)
        {
            if (food == FoodType.None || heldFood != FoodType.None)
            {
                return false;
            }

            heldFood = food;
            return true;
        }

        public bool TryConsumeFood(FoodType requiredFood)
        {
            if (heldFood != requiredFood || requiredFood == FoodType.None)
            {
                return false;
            }

            heldFood = FoodType.None;
            return true;
        }

        public void AddMoney(int amount)
        {
            carriedMoney += Mathf.Max(0, amount);
        }

        public int DepositAllMoney()
        {
            int deposited = carriedMoney;
            carriedMoney = 0;
            return deposited;
        }
    }
}
