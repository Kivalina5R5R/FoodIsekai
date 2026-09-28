namespace FoodIsekaiZ.Gameplay
{
    public interface IPerkWallet
    {
        int Balance { get; }
        bool TrySpend(int amount);
    }
}
