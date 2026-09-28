using System;
using System.Collections.Generic;

namespace FoodIsekaiZ.Gameplay
{
    // Owns offers and purchases for one round; purchases are recorded without applying gameplay effects.
    public sealed class PerkShopSession
    {
        private readonly IPerkWallet wallet;
        private readonly Random random;
        private readonly List<PerkOffer> offers = new List<PerkOffer>();
        private readonly List<PerkPurchase> purchases = new List<PerkPurchase>();
        private readonly HashSet<string> purchasedOffers = new HashSet<string>();
        private bool bigShop;
        private int beforeWave;

        public IReadOnlyList<PerkOffer> Offers => offers.AsReadOnly();
        public IReadOnlyList<PerkPurchase> Purchases => purchases.AsReadOnly();
        public bool IsOpen { get; private set; }
        public bool IsLocked => bigShop && purchasedOffers.Count > 0;

        public PerkShopSession(IPerkWallet wallet, Random random)
        {
            this.wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public void Open(IEnumerable<PerkOffer> pool, bool big, int nextWave)
        {
            Close();
            offers.Clear();
            purchasedOffers.Clear();
            bigShop = big;
            beforeWave = nextWave;
            var candidates = new List<PerkOffer>();
            var ids = new HashSet<string>();
            foreach (PerkOffer offer in pool)
                if (offer != null && ids.Add(offer.Id)) candidates.Add(offer);
            while (offers.Count < 4 && candidates.Count > 0)
            {
                int index = random.Next(candidates.Count);
                offers.Add(candidates[index]);
                candidates.RemoveAt(index);
            }
            IsOpen = true;
        }

        public bool IsPurchased(int index) => index >= 0 && index < offers.Count &&
            purchasedOffers.Contains(offers[index].Id);

        public bool CanBuy(int index) => IsOpen && !IsLocked && index >= 0 && index < offers.Count &&
            !IsPurchased(index) && wallet.Balance >= offers[index].Price;

        public bool TryBuy(int index, int playerId)
        {
            if (playerId <= 0 || !CanBuy(index)) return false;
            PerkOffer offer = offers[index];
            if (!wallet.TrySpend(offer.Price)) return false;
            purchasedOffers.Add(offer.Id);
            purchases.Add(new PerkPurchase(offer, playerId, beforeWave));
            return true;
        }

        public void Close() => IsOpen = false;
    }
}
