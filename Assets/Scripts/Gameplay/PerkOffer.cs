using System;

namespace FoodIsekaiZ.Gameplay
{
    public sealed class PerkOffer
    {
        public string Id { get; }
        public int Price { get; }

        public PerkOffer(string id, int price)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A perk needs a stable ID.", nameof(id));
            if (price <= 0) throw new ArgumentOutOfRangeException(nameof(price));
            Id = id;
            Price = price;
        }
    }
}
