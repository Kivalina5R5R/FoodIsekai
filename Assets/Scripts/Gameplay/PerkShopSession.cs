using System;
using System.Collections.Generic;

namespace FoodIsekaiZ.Gameplay
{
    // Owns one shop's offers and commits successful purchases to the shared game progression.
    public sealed class PerkShopSession
    {
        private readonly IPerkWallet wallet;
        private readonly Random random;
        private readonly IPerkProgression progression;
        private readonly List<PerkOffer> offers = new List<PerkOffer>();
        private readonly List<PerkPurchase> purchases = new List<PerkPurchase>();
        private readonly HashSet<string> purchasedOffers = new HashSet<string>();
        private bool bigShop;
        private int beforeWave;

        public IReadOnlyList<PerkOffer> Offers => offers.AsReadOnly();
        public IReadOnlyList<PerkPurchase> Purchases => purchases.AsReadOnly();
        public bool IsOpen { get; private set; }
        public bool IsLocked => bigShop && purchasedOffers.Count > 0;

        public PerkShopSession(IPerkWallet wallet, Random random, IPerkProgression progression = null)
        {
            this.wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            this.progression = progression;
        }

        // A non-null selection is exclusive, including an empty selection. Null uses normal random offers.
        // Purchase eligibility still applies in both modes.
        public void Open(IEnumerable<PerkOffer> pool, bool big, int nextWave,
            IEnumerable<string> preferredIds = null)
        {
            Close();
            offers.Clear();
            purchasedOffers.Clear();
            bigShop = big;
            beforeWave = nextWave;
            var candidates = new List<PerkOffer>();
            var ids = new HashSet<string>();
            foreach (PerkOffer offer in pool)
                if (offer != null && ids.Add(offer.Id) && (progression == null || progression.CanPurchase(offer.Id)))
                    candidates.Add(offer);
            if (preferredIds != null)
            {
                foreach (string id in preferredIds)
                {
                    if (offers.Count == 4) break;
                    int index = candidates.FindIndex(candidate => candidate.Id == id);
                    if (index < 0) continue;
                    offers.Add(candidates[index]);
                    candidates.RemoveAt(index);
                }
                IsOpen = true;
                return;
            }
            for (int i = candidates.Count - 1; i >= 0 && offers.Count < 4; i--)
            {
                if (progression == null || !progression.IsGuaranteedOffer(candidates[i].Id, nextWave)) continue;
                offers.Add(candidates[i]);
                candidates.RemoveAt(i);
            }
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
            !IsPurchased(index) && wallet.Balance >= offers[index].Price &&
            (progression == null || progression.CanPurchase(offers[index].Id));

        public bool TryBuy(int index, int playerId)
        {
            if (playerId <= 0 || !CanBuy(index)) return false;
            PerkOffer offer = offers[index];
            if (!wallet.TrySpend(offer.Price)) return false;
            purchasedOffers.Add(offer.Id);
            var purchase = new PerkPurchase(offer, playerId, beforeWave);
            progression?.RecordPurchase(purchase);
            purchases.Add(purchase);
            return true;
        }

        public void Close() => IsOpen = false;
    }
}
