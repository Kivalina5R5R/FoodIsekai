using System;
using System.Collections.Generic;

namespace FoodIsekaiZ.Gameplay
{
    // Tracks each outstanding dish, including repeated menus in a paired order.
    public sealed class CustomerOrder
    {
        private readonly List<FoodType> remaining = new List<FoodType>(2);
        public IReadOnlyList<FoodType> Remaining { get; }
        public int DishCount { get; private set; }
        public FoodType LastServedFood { get; private set; }
        public bool IsOmakase { get; private set; }
        public FoodType DisplayFood => remaining.Count > 0 ? remaining[0] : LastServedFood;
        public bool IsComplete => DishCount > 0 && remaining.Count == 0;

        public CustomerOrder() => Remaining = remaining.AsReadOnly();

        public void Configure(FoodType first, FoodType second = FoodType.None, bool omakase = false)
        {
            if (first < FoodType.Food1 || first > FoodType.Food5) throw new ArgumentOutOfRangeException(nameof(first));
            if (second < FoodType.None || second > FoodType.Food5) throw new ArgumentOutOfRangeException(nameof(second));
            Clear();
            IsOmakase = omakase;
            remaining.Add(first);
            if (second != FoodType.None) remaining.Add(second);
            DishCount = remaining.Count;
        }

        public bool Accepts(FoodType food) => remaining.Count > 0 &&
            food >= FoodType.Food1 && food <= FoodType.Food5 && (IsOmakase || remaining.Contains(food));

        public bool TryServe(FoodType food)
        {
            if (!Accepts(food)) return false;
            if (IsOmakase) remaining.RemoveAt(0);
            else remaining.Remove(food);
            LastServedFood = food;
            return true;
        }

        public void Clear()
        {
            remaining.Clear();
            DishCount = 0;
            LastServedFood = FoodType.None;
            IsOmakase = false;
        }
    }
}
