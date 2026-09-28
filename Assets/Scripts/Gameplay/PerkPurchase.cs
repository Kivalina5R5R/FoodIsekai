namespace FoodIsekaiZ.Gameplay
{
    public sealed class PerkPurchase
    {
        public string PerkId { get; }
        public int Price { get; }
        public int PlayerId { get; }
        public int BeforeWave { get; }

        public PerkPurchase(PerkOffer offer, int playerId, int beforeWave)
        {
            PerkId = offer.Id;
            Price = offer.Price;
            PlayerId = playerId;
            BeforeWave = beforeWave;
        }
    }
}
