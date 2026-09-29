namespace FoodIsekaiZ.Gameplay
{
    // The shop asks eligibility before spending, then records the successful purchase once.
    public interface IPerkProgression
    {
        bool CanPurchase(string id);
        bool IsGuaranteedOffer(string id, int beforeWave);
        void RecordPurchase(PerkPurchase purchase);
    }
}
