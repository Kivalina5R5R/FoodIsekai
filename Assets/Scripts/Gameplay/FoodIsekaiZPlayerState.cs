using FoodIsekaiZ.Players;
using UnityEngine;

namespace FoodIsekaiZ.Gameplay
{
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
