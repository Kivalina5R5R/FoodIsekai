using System.Collections.Generic;

namespace FoodIsekaiZ.Gameplay
{
    public static class PerkDefinitions
    {
        public const string Tasty = "Perk_Small_Tasty";
        public const string Happiness = "Perk_Big_Happiness";

        public static IReadOnlyList<PerkDefinition> All { get; } = System.Array.AsReadOnly(new[]
        {
            new PerkDefinition("Perk_Small_Meat", PerkEffect.FoodScore, 2, FoodType.Food1),
            new PerkDefinition("Perk_Small_Seafood", PerkEffect.FoodScore, 2, FoodType.Food2),
            new PerkDefinition("Perk_Small_Starter", PerkEffect.FoodScore, 2, FoodType.Food3),
            new PerkDefinition("Perk_Small_Dessert", PerkEffect.FoodScore, 2, FoodType.Food4),
            new PerkDefinition("Perk_Small_Drink", PerkEffect.FoodScore, 2, FoodType.Food5),
            new PerkDefinition("Perk_Small_Cross", PerkEffect.AngerPenalty, 0),
            new PerkDefinition("Perk_Small_Glad", PerkEffect.Payment, 5),
            new PerkDefinition("Perk_Small_Longer", PerkEffect.Patience, 5),
            new PerkDefinition("Perk_Small_Spoon", PerkEffect.EatingSpeed, 2),
            new PerkDefinition(Tasty, PerkEffect.FoodCapacity, 2),
            new PerkDefinition(Happiness, PerkEffect.FoodCapacity, 3, prerequisiteId: Tasty),
            new PerkDefinition("Perk_Big_Bank", PerkEffect.AutomaticBank),
            new PerkDefinition("Perk_Big_Home", PerkEffect.AutomaticDrinks),
            new PerkDefinition("Perk_Big_Pairs", PerkEffect.PairedOrders, 2),
            new PerkDefinition("Perk_Big_Sky", PerkEffect.SpecialMenu, .3),
            new PerkDefinition("Perk_Big_Omakase", PerkEffect.Omakase, .6)
        });
    }
}
