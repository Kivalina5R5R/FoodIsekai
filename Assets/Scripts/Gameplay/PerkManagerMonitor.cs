using System;
using System.Collections.Generic;
using UnityEngine;

namespace FoodIsekaiZ.Gameplay
{
    // Provides the Inspector's perk inspection and simulation controls.
    [DisallowMultipleComponent]
    [AddComponentMenu("Food Isekai/Perk Manager Monitor")]
    public sealed class PerkManagerMonitor : MonoBehaviour
    {
        [SerializeField] private FoodIsekaiZGameManager gameManager;
        [SerializeField] private List<string> smallSimulationOffers = new List<string>();
        [SerializeField] private List<string> bigSimulationOffers = new List<string>();

        public FoodIsekaiZGameManager GameManager => gameManager;
        public IReadOnlyList<PerkPurchase> ActivePerks => gameManager != null
            ? gameManager.Perks.Purchases : Array.Empty<PerkPurchase>();

        public IReadOnlyList<string> GetSimulationOffers(bool big)
        {
            if (gameManager == null || !gameManager.IsSimulationMode)
                return Array.Empty<string>();
            return (big ? bigSimulationOffers : smallSimulationOffers).AsReadOnly();
        }
    }
}
