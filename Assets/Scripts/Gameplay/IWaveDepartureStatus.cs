namespace FoodIsekaiZ.Gameplay
{
    // Reports whether the current wave still has visible NPCs in the restaurant.
    public interface IWaveDepartureStatus
    {
        bool HasNpcsInRestaurant { get; }

        // True while any NPC is still standing at its customer slot; NPCs walking out do not count.
        bool HasNpcsAtSlots { get; }
    }
}
