namespace FoodIsekaiZ.Gameplay
{
    // The break clock waits for the guide; the next meal waits for her departure.
    public interface IMealIntermissionGate
    {
        bool CanCountDown { get; }
        bool TryFinish();
    }
}
