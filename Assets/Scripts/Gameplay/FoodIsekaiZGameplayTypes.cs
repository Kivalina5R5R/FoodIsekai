namespace FoodIsekaiZ.Gameplay
{
    public enum FoodType
    {
        None = 0,
        Food1 = 1,
        Food2 = 2,
        Food3 = 3,
        Food4 = 4,
        Food5 = 5
    }

    public enum ArenaSlotType
    {
        Customer,
        FoodStation,
        MoneyDeposit
    }

    public enum CustomerSlotState
    {
        Empty,
        WaitingForFood,
        Eating,
        MoneyAvailable
    }

    public enum MealWavePhase
    {
        NotStarted,
        Active,
        Intermission,
        Completed
    }
}
