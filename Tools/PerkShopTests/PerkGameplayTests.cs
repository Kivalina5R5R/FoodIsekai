using System;
using System.Linq;
using FoodIsekaiZ.Gameplay;

internal static class PerkGameplayTests
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

    internal static void Run()
    {
        var big = PerkDefinitions.All.Where(d => d.Id.StartsWith("Perk_Big_"))
            .Select(d => new PerkOffer(d.Id, 150)).ToArray();
        var tasty = new PerkOffer(PerkDefinitions.Tasty, 80);
        for (int seed = 0; seed < 200; seed++)
        {
            var manager = new PerkManager(PerkDefinitions.All);
            var wallet = new Wallet(1000);
            var shop = new PerkShopSession(wallet, new Random(seed), manager);
            shop.Open(big, true, 3);
            if (shop.Offers.Any(o => o.Id == PerkDefinitions.Happiness)) throw new Exception("Locked Happiness was offered.");
            shop.Open(new[] { tasty }, false, 2);
            if (!shop.TryBuy(0, 2)) throw new Exception("Tasty purchase failed.");
            shop.Close();
            if (manager.FoodCapacity != 2) throw new Exception("Tasty expired when shop closed.");
            shop.Open(big, true, 3);
            int happiness = shop.Offers.ToList().FindIndex(o => o.Id == PerkDefinitions.Happiness);
            if (happiness < 0 || shop.Offers.Count != 4) throw new Exception("Guaranteed offer was not reserved.");
            if (!shop.TryBuy(happiness, 4) || manager.FoodCapacity != 3 || !shop.IsLocked)
                throw new Exception("Happiness upgrade failed.");
            if (manager.Purchases.Count != 2 || manager.Purchases[0].PlayerId != 2 || manager.Purchases[1].BeforeWave != 3)
                throw new Exception("Shared purchase history lost its buyer or meal.");
            manager.Reset();
            if (manager.FoodCapacity != 1 || manager.Purchases.Count != 0) throw new Exception("New game retained perks.");
        }
        Check(true, "200 random shops: prerequisite, guaranteed next-break Happiness, shared history, upgrade and reset.");

        var forcedIds = new[] { "Perk_Big_Bank", "Perk_Big_Home", "Perk_Big_Pairs", "Perk_Big_Sky" };
        for (int seed = 0; seed < 100; seed++)
        {
            var manager = new PerkManager(PerkDefinitions.All);
            var shop = new PerkShopSession(new Wallet(1000), new Random(seed), manager);
            shop.Open(big, true, 3, forcedIds);
            if (!shop.Offers.Select(o => o.Id).SequenceEqual(forcedIds) || manager.Purchases.Count != 0)
                throw new Exception("Simulation picks were randomized or activated without purchase.");
            shop.Open(big, true, 3, new[] { PerkDefinitions.Happiness, "Unknown", forcedIds[0], forcedIds[0] });
            if (shop.Offers.Count != 1 || shop.Offers[0].Id != forcedIds[0])
                throw new Exception("Simulation added unselected cards or bypassed eligibility.");
            if (!shop.TryBuy(0, 1) || !manager.HasPerk(forcedIds[0]) || !shop.IsLocked)
                throw new Exception("Simulation bypassed normal purchase rules.");
            shop.Open(big, true, 4, forcedIds);
            if (shop.Offers.Any(o => o.Id == forcedIds[0])) throw new Exception("Owned simulation pick was offered again.");
            manager.RecordPurchase(new PerkPurchase(tasty, 1, 2));
            shop.Open(big, true, 3, new[] { "Perk_Big_Home" });
            if (shop.Offers.Count != 1 || shop.Offers[0].Id != "Perk_Big_Home")
                throw new Exception("Simulation added an unchecked guaranteed Happiness.");
            shop.Open(big, true, 3, Array.Empty<string>());
            if (shop.Offers.Count != 4 || !shop.Offers.Any(o => o.Id == PerkDefinitions.Happiness) ||
                shop.Offers.Any(o => o.Id == forcedIds[0]))
                throw new Exception("Empty simulation selection must use normal random offers, guarantees and eligibility.");
            shop.Open(big, true, 3, new[] { forcedIds[0], "Unknown" });
            if (shop.Offers.Count != 0) throw new Exception("Ineligible checked selections must not fall back to random offers.");
            shop.Open(big, true, 3, new[] { PerkDefinitions.Happiness });
            if (shop.Offers.Count != 1 || shop.Offers[0].Id != PerkDefinitions.Happiness)
                throw new Exception("Unlocked selected Happiness was not offered.");
            shop.Open(big, true, 3);
            if (shop.Offers.Count != 4 || !shop.Offers.Any(o => o.Id == PerkDefinitions.Happiness))
                throw new Exception("Normal random mode retained the exclusive simulation selection.");
        }
        Check(true, "100 simulation shops: exclusive selections, empty selections, eligibility, purchases, no unchecked guarantee, and return to normal random mode.");

        var poorManager = new PerkManager(PerkDefinitions.All);
        var poorShop = new PerkShopSession(new Wallet(79), new Random(1), poorManager);
        poorShop.Open(new[] { tasty }, false, 2);
        Check(!poorShop.TryBuy(0, 1) && poorManager.Purchases.Count == 0 && poorManager.FoodCapacity == 1,
            "Insufficient funds never activate an effect.");
        var optionalManager = new PerkManager(PerkDefinitions.All);
        optionalManager.RecordPurchase(new PerkPurchase(tasty, 1, 2));
        var optionalShop = new PerkShopSession(new Wallet(1000), new Random(2), optionalManager);
        optionalShop.Open(big, true, 3);
        optionalShop.Close();
        Check(optionalManager.FoodCapacity == 2 && optionalManager.Purchases.Count == 1,
            "Guaranteed Happiness is optional; closing does not buy it.");
        optionalShop.Open(new[] { tasty, new PerkOffer("Unknown", 80) }, false, 4);
        Check(optionalShop.Offers.Count == 0, "Owned and undefined perks cannot charge the wallet.");

        var plate = new FoodInventory();
        Check(plate.TryPick(FoodType.Food1, 1) && plate.TryPick(FoodType.Food2, 1) && plate.Foods.Count == 1,
            "Without perks, a new menu still replaces the held menu.");
        plate.Clear();
        Check(plate.TryPick(FoodType.Food1, 2) && plate.TryPick(FoodType.Food2, 2) && !plate.TryPick(FoodType.Food1, 2),
            "Tasty holds two distinct menus and ignores duplicate pickups.");
        Check(plate.TryPick(FoodType.Food3, 2) && plate.Foods.SequenceEqual(new[] { FoodType.Food2, FoodType.Food3 }),
            "A full Tasty plate replaces its oldest menu with the new pickup.");
        plate.Clear();
        Check(plate.TryPick(FoodType.Food1, 3) && plate.TryPick(FoodType.Food2, 3) && plate.TryPick(FoodType.Food4, 3) &&
            plate.TryPick(FoodType.Food5, 3) && plate.Foods.SequenceEqual(new[] { FoodType.Food2, FoodType.Food4, FoodType.Food5 }),
            "A full Happiness plate replaces its oldest menu with the new pickup.");
        plate.Clear();
        Check(plate.TryPick(FoodType.Food1, 3) && plate.TryPick(FoodType.Food2, 3) &&
            plate.TryPick(FoodType.Food3, 3) && plate.TryConsume(FoodType.Food3) &&
            plate.Foods.SequenceEqual(new[] { FoodType.Food1, FoodType.Food2 }),
            "Happiness can deliver slot three while preserving slots one and two.");
        Check(plate.TryConsume(plate.First) && plate.First == FoodType.Food2 && plate.Foods.Count == 1,
            "Wrong delivery discards only the oldest dish and compacts the plate.");

        var order = new CustomerOrder();
        order.Configure(FoodType.Food1, FoodType.Food3);
        Check(!order.TryServe(FoodType.Food2) && order.TryServe(FoodType.Food3) && !order.IsComplete &&
            order.DisplayFood == FoodType.Food1 && order.TryServe(FoodType.Food1) && order.IsComplete,
            "Paired menus accept either sequence, reject wrong dishes, and finish only after both.");
        Check(!order.TryServe(FoodType.Food1), "A completed order cannot pay another delivery score.");
        order.Configure(FoodType.Food2, FoodType.Food2);
        Check(order.TryServe(FoodType.Food2) && !order.IsComplete && order.TryServe(FoodType.Food2) && order.IsComplete,
            "Identical paired menus require two separate dishes.");
        foreach (FoodType food in Enum.GetValues<FoodType>().Where(f => f != FoodType.None))
        {
            order.Configure(FoodType.Food1, omakase: true);
            if (!order.TryServe(food) || !order.IsComplete || order.LastServedFood != food)
                throw new Exception("Omakase did not accept one of the five foods.");
        }
        Check(true, "Omakase accepts all five menus, including drinks, and retains the actual served menu.");

        var effects = new PerkManager(PerkDefinitions.All);
        foreach (PerkDefinition definition in PerkDefinitions.All)
        {
            if (definition.Id == PerkDefinitions.Happiness) continue;
            effects.RecordPurchase(new PerkPurchase(new PerkOffer(definition.Id, 80), 1, 2));
        }
        Check(effects.GetAmount(PerkEffect.FoodScore, food: FoodType.Food1) == 2 &&
            effects.GetAmount(PerkEffect.FoodScore, food: FoodType.Food5) == 2 &&
            effects.GetAmount(PerkEffect.Patience) == 1.02 && effects.GetAmount(PerkEffect.Payment) == 1.05 &&
            effects.GetAmount(PerkEffect.AngerPenalty) == .95 && effects.GetAmount(PerkEffect.EatingSpeed) == 1.05 &&
            effects.GetAmount(PerkEffect.PairedOrders) == .5 && effects.GetAmount(PerkEffect.Omakase) == .6 &&
            effects.GetAmount(PerkEffect.SpecialMenu) == .3,
            "Independent definitions carry every configured multiplier and probability.");
    }
}
