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
        /// <summary>Gets the maximum money this player can carry before visiting the bank.</summary>
        public int MaximumCarriedMoney => MoneyCarryLimit;

        private void Awake()
        {
            carriedMoney = Mathf.Clamp(carriedMoney, 0, MoneyCarryLimit);
            if (trackedPlayer == null)
            {
                trackedPlayer = GetComponent<UWBPlayerController>();
            }
        }

        /// <summary>
        /// Picks up food from a food station, replacing any food currently held by the player.
        /// </summary>
        /// <param name="food">The food assigned to the station being interacted with.</param>
        /// <returns><see langword="true"/> when a valid food was picked up.</returns>
        public bool TryPickFood(FoodType food)
        {
            if (food < FoodType.Food1 || food > FoodType.Food5)
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

        /// <summary>Accepts a complete money pile only when it fits within the player's carry limit.</summary>
        /// <returns>True when the full positive amount was added; otherwise the balance stays unchanged.</returns>
        public bool TryAddMoney(int amount)
        {
            if (amount <= 0 || amount > MoneyCarryLimit - carriedMoney)
            {
                return false;
            }

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
