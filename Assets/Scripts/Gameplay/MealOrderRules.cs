using System;

namespace FoodIsekaiZ.Gameplay
{
    // Meal difficulty controls dish counts independently of purchased perks.
    public static class MealOrderRules
    {
        // Uses a uniform roll in [0, 1); meals are numbered breakfast=1 through dinner=3.
        public static int GetDishCount(int meal, double roll)
        {
            if (roll < 0 || roll >= 1 || double.IsNaN(roll)) throw new ArgumentOutOfRangeException(nameof(roll));
            if (meal <= 1) return 1;
            if (meal == 2) return roll < 0.4 ? 2 : 1;
            if (roll < 0.2) return 3;
            return roll < 0.6 ? 2 : 1;
        }
    }
}
