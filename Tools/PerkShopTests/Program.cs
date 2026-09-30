using System;
using System.Linq;
using FoodIsekaiZ.Gameplay;

internal static class Program
{
    private sealed class Wallet : IPerkWallet
    {
        public int Balance { get; private set; }
        public Wallet(int balance) => Balance = balance;
        public bool TrySpend(int amount)
        {
            if (amount <= 0 || amount > Balance) return false;
            Balance -= amount;
            return true;
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        Console.WriteLine("PASS: " + message);
    }

    private static void Main()
    {
        PerkGameplayTests.Run();
        CheckFoodScoreOfferLimit();
        var small = Enumerable.Range(0, 10).Select(i => new PerkOffer("Small" + i, 80)).ToArray();
        var big = Enumerable.Range(0, 6).Select(i => new PerkOffer("Big" + i, 150)).ToArray();
        foreach (int balance in new[] { 0, 1, 79 })
        {
            var soloWallet = new Wallet(balance);
            var soloShop = new PerkShopSession(soloWallet, new Random(23));
            soloShop.Open(small, false, 2);
            Check(soloShop.Offers.Count == 4 && soloShop.IsOpen,
                $"Solo player with {balance} coins still receives all four shop offers.");
            Check(Enumerable.Range(0, 4).All(i => !soloShop.TryBuy(i, 1)) &&
                soloShop.Offers.Count == 4 && soloWallet.Balance == balance && soloShop.Purchases.Count == 0,
                $"Solo player with {balance} coins cannot buy; offers remain and money is unchanged.");
        }
        var wallet = new Wallet(400);
        var shop = new PerkShopSession(wallet, new Random(23));
        shop.Open(small.Concat(small), false, 2);
        Check(shop.Offers.Count == 4 && shop.Offers.Select(o => o.Id).Distinct().Count() == 4,
            "Four unique offers even when the catalog contains duplicates.");
        Check(!shop.TryBuy(0, 0) && wallet.Balance == 400, "Unassigned players cannot spend money.");
        Check(shop.TryBuy(0, 1) && wallet.Balance == 320, "A purchase deducts its exact price.");
        Check(!shop.TryBuy(0, 2) && wallet.Balance == 320, "A second player cannot purchase the same card again.");
        Check(shop.TryBuy(1, 2) && shop.TryBuy(2, 1) && wallet.Balance == 160,
            "Small shop accepts multiple distinct purchases for the team.");
        shop.Close();
        Check(!shop.TryBuy(3, 1) && wallet.Balance == 160, "Expired or closed shops cannot spend money.");
        shop.Open(big, true, 3);
        Check(shop.TryBuy(0, 2) && wallet.Balance == 10 && shop.IsLocked,
            "Big shop records one purchase and locks for the whole team.");
        Check(!shop.TryBuy(1, 3), "A different player cannot bypass the Big limit.");
        Check(shop.Purchases.Count == 4 && shop.Purchases[3].BeforeWave == 3 &&
            shop.Purchases[3].PlayerId == 2 && shop.Purchases[3].Price == 150,
            "Purchases from Lunch and Dinner retain perk, buyer, price and target meal.");
        shop.Open(small, false, 2);
        Check(!shop.TryBuy(0, 1) && wallet.Balance == 10, "Insufficient funds do not change money or ownership.");
        shop.Open(Array.Empty<PerkOffer>(), false, 2);
        Check(shop.Offers.Count == 0 && !shop.CanBuy(0), "An empty catalog is safe.");
        shop.Open(small.Take(2), false, 2);
        Check(shop.Offers.Count == 2, "A short catalog never duplicates cards to fill the shop.");
        var hold = new PerkSelectionHold(1.5f);
        hold.Tick(1, 0);
        for (int i = 0; i < 8; i++) hold.Tick(1, .1f);
        Check(hold.Progress > .5f, "Standing in a zone accumulates confirmation progress.");
        Check(!hold.Tick(2, .1f) && hold.Progress == 0, "Another player cannot inherit the hold.");
        hold.Tick(2, .1f);
        hold.Tick(null, .1f);
        Check(hold.Progress == 0, "Leaving, losing tracking or contesting the zone resets confirmation.");
        hold.Tick(1, 0);
        Check(!hold.Tick(1, 20f), "A stalled frame cannot instantly purchase a perk.");
        bool confirmed = false;
        for (int i = 0; i < 16; i++) confirmed |= hold.Tick(1, .1f);
        Check(confirmed, "A continuous hold completes confirmation.");
        var richWallet = new Wallet(1000);
        var bigRace = new PerkShopSession(richWallet, new Random(2));
        bigRace.Open(big, true, 3);
        Check(bigRace.TryBuy(0, 1) && !bigRace.TryBuy(1, 2) && richWallet.Balance == 850,
            "Two same-frame confirmations still spend for only one Big card.");
    }

    private static void CheckFoodScoreOfferLimit()
    {
        var foodIds = PerkDefinitions.All.Where(d => d.Effect == PerkEffect.FoodScore)
            .Select(d => d.Id).ToArray();
        var pool = PerkDefinitions.All.Where(d => d.Id.StartsWith("Perk_Small_"))
            .Select(d => new PerkOffer(d.Id, 80)).ToArray();
        bool validRandomOffers = true;
        for (int seed = 0; seed < 1000; seed++)
        {
            var shop = new PerkShopSession(new Wallet(1000), new Random(seed));
            shop.Open(pool, false, 2);
            validRandomOffers &= shop.Offers.Count == 4 &&
                shop.Offers.Select(o => o.Id).Distinct().Count() == 4 &&
                shop.Offers.Count(o => foodIds.Contains(o.Id)) <= 2;
        }
        Check(validRandomOffers, "Random shops offer four unique cards with at most two food-score perks across 1000 seeds.");

        var simulation = new PerkShopSession(new Wallet(1000), new Random(23));
        simulation.Open(pool, false, 2, pool.Select(o => o.Id));
        Check(simulation.Offers.Count == 4 && simulation.Offers.Count(o => foodIds.Contains(o.Id)) == 2,
            "Simulation skips excess food-score selections and fills remaining slots from other selected perks.");
        simulation.Open(pool, false, 2, foodIds);
        Check(simulation.Offers.Count == 2 && simulation.Offers.All(o => foodIds.Contains(o.Id)),
            "Food-only simulation selections show two cards without adding unselected perks.");
        simulation.Open(pool.Where(o => foodIds.Contains(o.Id)), false, 2);
        Check(simulation.Offers.Count == 2,
            "A food-only random pool stops at two cards and resets its limit on reopening.");
    }
}
