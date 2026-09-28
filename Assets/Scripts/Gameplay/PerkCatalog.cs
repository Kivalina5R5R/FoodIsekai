using System;
using System.Collections.Generic;
using UnityEngine;

namespace FoodIsekaiZ.Gameplay
{
    [CreateAssetMenu(menuName = "Food Isekai/Perk Catalog")]
    public sealed class PerkCatalog : ScriptableObject
    {
        [Serializable]
        private sealed class Entry
        {
            [SerializeField] private string id;
            [SerializeField] private GameObject prefab;
            [SerializeField, Min(1)] private int price;
            public string Id => id;
            public GameObject Prefab => prefab;
            public int Price => price;
        }

        [SerializeField] private Entry[] smallPerks;
        [SerializeField] private Entry[] bigPerks;

        public List<PerkOffer> GetOffers(bool big)
        {
            var result = new List<PerkOffer>();
            Entry[] entries = big ? bigPerks : smallPerks;
            if (entries == null) return result;
            foreach (Entry entry in entries)
                if (entry != null && entry.Prefab != null && !string.IsNullOrEmpty(entry.Id) && entry.Price > 0)
                    result.Add(new PerkOffer(entry.Id, entry.Price));
            return result;
        }

        public GameObject GetPrefab(string id)
        {
            foreach (Entry[] entries in new[] { smallPerks, bigPerks })
            {
                if (entries == null) continue;
                foreach (Entry entry in entries)
                    if (entry != null && entry.Id == id) return entry.Prefab;
            }
            return null;
        }
    }
}
