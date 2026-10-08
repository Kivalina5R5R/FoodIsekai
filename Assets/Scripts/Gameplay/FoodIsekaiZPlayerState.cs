using FoodIsekaiZ.Players;
using System.Collections.Generic;
using UnityEngine;

namespace FoodIsekaiZ.Gameplay
{
    public sealed class FoodIsekaiZPlayerState : MonoBehaviour
    {
        private const int BreakfastMoneyCarryLimit = 50;

        [SerializeField] private UWBPlayerController trackedPlayer;
        [SerializeField, Min(1)] private int fallbackPlayerId = 1;

        [Header("Runtime (Read Only)")]
        [SerializeField] private FoodType heldFood = FoodType.None;
        [SerializeField, Min(0)] private int carriedMoney;
        private int moneyCarryLimit = BreakfastMoneyCarryLimit;
        private readonly FoodInventory inventory = new FoodInventory();
        private int foodCapacity = 1;

        public int PlayerId => trackedPlayer != null ? trackedPlayer.PlayerId : fallbackPlayerId;
        public FoodType HeldFood => inventory.First;
        public IReadOnlyList<FoodType> HeldFoods => inventory.Foods;
        public int InventoryRevision => inventory.Revision;
        public bool HasFood(FoodType food) => inventory.Contains(food);
        public void SetFoodCapacity(int capacity) => foodCapacity = Mathf.Clamp(capacity, 1, 3);
        public int CarriedMoney => carriedMoney;
        // Gets the maximum money this player can carry before visiting the bank.
        public int MaximumCarriedMoney => moneyCarryLimit;

        // Meals are numbered breakfast=1, lunch=2, dinner=3; changing capacity preserves carried coins.
        public void SetMoneyCapacityForMeal(int meal)
        {
            moneyCarryLimit = meal >= 3 ? 300 : meal == 2 ? 200 : BreakfastMoneyCarryLimit;
        }

        private void Awake()
        {
            if (heldFood != FoodType.None) inventory.TryPick(heldFood, foodCapacity);
            carriedMoney = Mathf.Clamp(carriedMoney, 0, moneyCarryLimit);
            if (trackedPlayer == null)
            {
                trackedPlayer = GetComponent<UWBPlayerController>();
            }
        }

        // A normal plate replaces its food; perk plates accumulate distinct menus until full.
        public bool TryPickFood(FoodType food)
        {
            if (carriedMoney > 0 || !inventory.TryPick(food, foodCapacity))
            {
                return false;
            }

            heldFood = inventory.First;
            return true;
        }

        public bool TryConsumeFood(FoodType requiredFood)
        {
            if (!inventory.TryConsume(requiredFood))
            {
                return false;
            }

            heldFood = inventory.First;
            return true;
        }

        public bool TryDiscardHeldFood()
        {
            if (!inventory.Clear())
            {
                return false;
            }

            heldFood = FoodType.None;
            return true;
        }

        // Collecting a valid money pile replaces held food when the full amount fits in the wallet.
        // A rejected pickup preserves both held food and the current balance.
        public bool TryAddMoney(int amount)
        {
            if (amount <= 0 || amount > moneyCarryLimit - carriedMoney)
            {
                return false;
            }

            inventory.Clear();
            heldFood = FoodType.None;
            carriedMoney += amount;
            return true;
        }

        public int DepositAllMoney()
        {
            int deposited = carriedMoney;
            carriedMoney = 0;
            return deposited;
        }
    }
}
