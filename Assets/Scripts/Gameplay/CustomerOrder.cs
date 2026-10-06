using System;
using System.Collections.Generic;

namespace FoodIsekaiZ.Gameplay
{
    // Tracks outstanding, distinct menus and accepts deliveries in any order.
    public sealed class CustomerOrder
    {
        private readonly List<FoodType> remaining = new List<FoodType>(3);
        public IReadOnlyList<FoodType> Remaining { get; }
        public int DishCount { get; private set; }
        public FoodType LastServedFood { get; private set; }
        public bool IsOmakase { get; private set; }
        public FoodType DisplayFood => remaining.Count > 0 ? remaining[0] : LastServedFood;
        public bool IsComplete => DishCount > 0 && remaining.Count == 0;

        public CustomerOrder() => Remaining = remaining.AsReadOnly();

        public void Configure(FoodType first, FoodType second = FoodType.None, bool omakase = false,
            FoodType third = FoodType.None)
        {
            if (first < FoodType.Food1 || first > FoodType.Food5) throw new ArgumentOutOfRangeException(nameof(first));
            if (second < FoodType.None || second > FoodType.Food5) throw new ArgumentOutOfRangeException(nameof(second));
            if (third < FoodType.None || third > FoodType.Food5) throw new ArgumentOutOfRangeException(nameof(third));
            if ((second != FoodType.None && second == first) ||
                (third != FoodType.None && (third == first || third == second)))
                throw new ArgumentException("An order cannot contain duplicate menus.");
            Clear();
            IsOmakase = omakase;
            remaining.Add(first);
            if (second != FoodType.None) remaining.Add(second);
            if (third != FoodType.None) remaining.Add(third);
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
