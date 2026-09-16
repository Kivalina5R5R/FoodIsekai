using FoodIsekaiZ.Players;
using UnityEngine;

namespace FoodIsekaiZ.Gameplay
{
    public sealed class FoodIsekaiZPlayerState : MonoBehaviour
    {
        private const int MoneyCarryLimit = 50;

        [SerializeField] private UWBPlayerController trackedPlayer;
        [SerializeField, Min(1)] private int fallbackPlayerId = 1;

        [Header("Runtime (Read Only)")]
        [SerializeField] private FoodType heldFood = FoodType.None;
        [SerializeField, Range(0, MoneyCarryLimit)] private int carriedMoney;

        public int PlayerId => trackedPlayer != null ? trackedPlayer.PlayerId : fallbackPlayerId;
        public FoodType HeldFood => heldFood;
        public int CarriedMoney => carriedMoney;
        // Gets the maximum money this player can carry before visiting the bank.
        public int MaximumCarriedMoney => MoneyCarryLimit;

        private void Awake()
        {
            carriedMoney = Mathf.Clamp(carriedMoney, 0, MoneyCarryLimit);
            if (trackedPlayer == null)
            {
                trackedPlayer = GetComponent<UWBPlayerController>();
            }
        }

        // Picks up a different station food, replacing the food currently held by this player.
        // Returns false while carrying money, for invalid food or the same food already held.
        public bool TryPickFood(FoodType food)
        {
            if (carriedMoney > 0 || food < FoodType.Food1 || food > FoodType.Food5 || heldFood == food)
            {
                return false;
            }

            // Picking up from F is an explicit replacement action. The food that was
            // already held is discarded as part of this pickup, so the player does
            // not need to visit a customer slot before choosing a different food.
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

        public bool TryDiscardHeldFood()
        {
            if (heldFood == FoodType.None)
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
            if (amount <= 0 || amount > MoneyCarryLimit - carriedMoney)
            {
                return false;
            }

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
