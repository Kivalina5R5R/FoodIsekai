using System.Collections.Generic;
using UnityEngine;

namespace FoodIsekaiZ.Gameplay
{
    internal sealed class FoodOrderGenerator
    {
        private const int MaximumConsecutiveSameFoodOrders = 2;

        private readonly List<FoodType> foodCandidates = new List<FoodType>(5);
        private FoodType lastRequestedFood = FoodType.None;
        private int consecutiveRequestedFoodCount;

        internal void Reset()
        {
            foodCandidates.Clear();
            lastRequestedFood = FoodType.None;
            consecutiveRequestedFoodCount = 0;
        }

        internal FoodType PickRandomFood(
            ArenaSlot2D[] customerSlots,
            int slotIndex,
            FoodIsekaiZGameManager.FoodOption[] foodOptions)
        {
            BuildFoodCandidates(customerSlots, slotIndex, foodOptions, true, true);
            if (foodCandidates.Count == 0)
            {
                BuildFoodCandidates(customerSlots, slotIndex, foodOptions, true, false);
            }

            if (foodCandidates.Count == 0)
            {
                BuildFoodCandidates(customerSlots, slotIndex, foodOptions, false, true);
            }

            if (foodCandidates.Count == 0)
            {
                BuildFoodCandidates(customerSlots, slotIndex, foodOptions, false, false);
            }

            FoodType selectedFood = foodCandidates.Count > 0
                ? foodCandidates[UnityEngine.Random.Range(0, foodCandidates.Count)]
                : (FoodType)UnityEngine.Random.Range((int)FoodType.Food1, (int)FoodType.Food5 + 1);
            RememberRequestedFood(selectedFood);
            return selectedFood;
        }

        private void BuildFoodCandidates(
            ArenaSlot2D[] customerSlots,
            int slotIndex,
            FoodIsekaiZGameManager.FoodOption[] foodOptions,
            bool avoidAdjacentFood,
            bool avoidConsecutiveFood)
        {
            foodCandidates.Clear();
            if (foodOptions == null)
            {
                return;
            }

            for (int i = 0; i < foodOptions.Length; i++)
            {
                FoodIsekaiZGameManager.FoodOption option = foodOptions[i];
                if (!IsOrderable(option) || foodCandidates.Contains(option.food))
                {
                    continue;
                }

                if (avoidAdjacentFood && HasAdjacentCustomerFood(customerSlots, slotIndex, option.food))
                {
                    continue;
                }

                if (avoidConsecutiveFood &&
                    lastRequestedFood == option.food &&
                    consecutiveRequestedFoodCount >= MaximumConsecutiveSameFoodOrders)
                {
                    continue;
                }

                foodCandidates.Add(option.food);
            }
        }

        private static bool HasAdjacentCustomerFood(
            ArenaSlot2D[] customerSlots,
            int slotIndex,
            FoodType food)
        {
            return HasCustomerFoodAtSlot(customerSlots, slotIndex - 1, food) ||
                HasCustomerFoodAtSlot(customerSlots, slotIndex + 1, food);
        }

        private static bool HasCustomerFoodAtSlot(
            ArenaSlot2D[] customerSlots,
            int slotIndex,
            FoodType food)
        {
            if (customerSlots == null || slotIndex < 0 || slotIndex >= customerSlots.Length)
            {
                return false;
            }

            ArenaSlot2D adjacentSlot = customerSlots[slotIndex];
            return adjacentSlot != null &&
                adjacentSlot.CustomerState != CustomerSlotState.Empty &&
                adjacentSlot.RequestedFood == food;
        }

        private void RememberRequestedFood(FoodType food)
        {
            if (lastRequestedFood == food)
            {
                consecutiveRequestedFoodCount++;
                return;
            }

            lastRequestedFood = food;
            consecutiveRequestedFoodCount = 1;
        }

        private static bool IsOrderable(FoodIsekaiZGameManager.FoodOption option)
        {
            return option != null && option.canBeOrdered &&
                option.food >= FoodType.Food1 && option.food <= FoodType.Food5;
        }
    }
}
