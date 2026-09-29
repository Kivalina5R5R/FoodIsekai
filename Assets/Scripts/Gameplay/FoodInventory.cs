using System.Collections.Generic;

namespace FoodIsekaiZ.Gameplay
{
    // A plate holds at most one of each menu, in pickup order.
    public sealed class FoodInventory
    {
        private readonly List<FoodType> foods = new List<FoodType>(3);
        public IReadOnlyList<FoodType> Foods { get; }
        public FoodType First => foods.Count > 0 ? foods[0] : FoodType.None;
        public int Revision { get; private set; }

        public FoodInventory() => Foods = foods.AsReadOnly();

        public bool Contains(FoodType food) => foods.Contains(food);

        public bool TryPick(FoodType food, int capacity)
        {
            if (food < FoodType.Food1 || food > FoodType.Food5 || foods.Contains(food) || capacity < 1) return false;
            if (capacity == 1) foods.Clear();
            else if (foods.Count >= capacity) return false;
            foods.Add(food);
            Revision++;
            return true;
        }

        public bool TryConsume(FoodType food)
        {
            if (!foods.Remove(food)) return false;
            Revision++;
            return true;
        }

        public bool Clear()
        {
            if (foods.Count == 0) return false;
            foods.Clear();
            Revision++;
            return true;
        }
    }
}
