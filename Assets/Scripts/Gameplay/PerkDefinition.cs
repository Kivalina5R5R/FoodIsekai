namespace FoodIsekaiZ.Gameplay
{
    // Gameplay data is independent of the card prefab and its translated text.
    public sealed class PerkDefinition
    {
        public string Id { get; }
        public PerkEffect Effect { get; }
        public double Amount { get; }
        public FoodType Food { get; }
        public string PrerequisiteId { get; }

        public PerkDefinition(string id, PerkEffect effect, double amount = 1,
            FoodType food = FoodType.None, string prerequisiteId = null)
        {
            Id = id;
            Effect = effect;
            Amount = amount;
            Food = food;
            PrerequisiteId = prerequisiteId;
        }
    }
}
