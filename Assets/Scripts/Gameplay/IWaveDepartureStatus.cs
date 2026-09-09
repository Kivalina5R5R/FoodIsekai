namespace FoodIsekaiZ.Gameplay
{
    // Reports whether the current wave still has visible NPCs in the restaurant.
    public interface IWaveDepartureStatus
    {
        bool HasNpcsInRestaurant { get; }
    }
}
