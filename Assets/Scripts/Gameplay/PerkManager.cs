using System;
using System.Collections.Generic;

namespace FoodIsekaiZ.Gameplay
{
    // One instance belongs to one game; ownership is shared by every player for its lifetime.
    public sealed class PerkManager : IPerkProgression
    {
        private readonly Dictionary<string, PerkDefinition> definitions = new Dictionary<string, PerkDefinition>();
        private readonly Dictionary<string, PerkPurchase> owned = new Dictionary<string, PerkPurchase>();
        private readonly List<PerkPurchase> purchases = new List<PerkPurchase>();
        public IReadOnlyList<PerkPurchase> Purchases { get; }

        public PerkManager(IEnumerable<PerkDefinition> definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            foreach (PerkDefinition definition in definitions) this.definitions.Add(definition.Id, definition);
            Purchases = purchases.AsReadOnly();
        }

        public bool HasPerk(string id) => owned.ContainsKey(id);

        public bool CanPurchase(string id) => definitions.TryGetValue(id, out PerkDefinition definition) &&
            !HasPerk(id) && (definition.PrerequisiteId == null || HasPerk(definition.PrerequisiteId));

        public bool IsGuaranteedOffer(string id, int beforeWave) => id == PerkDefinitions.Happiness &&
            CanPurchase(id) && owned.TryGetValue(PerkDefinitions.Tasty, out PerkPurchase tasty) &&
            beforeWave == tasty.BeforeWave + 1;

        public void RecordPurchase(PerkPurchase purchase)
        {
            if (purchase == null) throw new ArgumentNullException(nameof(purchase));
            if (!CanPurchase(purchase.PerkId)) throw new InvalidOperationException("Perk is owned, unknown or missing its prerequisite.");
            owned.Add(purchase.PerkId, purchase);
            purchases.Add(purchase);
        }

        public bool HasEffect(PerkEffect effect)
        {
            foreach (string id in owned.Keys)
                if (definitions[id].Effect == effect) return true;
            return false;
        }

        public double GetAmount(PerkEffect effect, double fallback = 1, FoodType food = FoodType.None)
        {
            double value = fallback;
            bool found = false;
            foreach (string id in owned.Keys)
            {
                PerkDefinition definition = definitions[id];
                if (definition.Effect != effect || definition.Food != food) continue;
                value = found ? Math.Max(value, definition.Amount) : definition.Amount;
                found = true;
            }
            return value;
        }

        public int FoodCapacity => (int)GetAmount(PerkEffect.FoodCapacity);

        // Explicit new-game reset; closing a shop or beginning a meal never calls this.
        public void Reset()
        {
            owned.Clear();
            purchases.Clear();
        }
    }
}
