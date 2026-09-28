using FoodIsekaiZ.Gameplay;
using TMPro;
using UnityEngine;

namespace FoodIsekaiZ.Display
{
    // Explicit prefab references survive renaming or moving the price label.
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class PerkPriceDisplay : MonoBehaviour
    {
        [SerializeField] private PerkPrice source;
        [SerializeField] private TMP_Text priceText;

        public bool HasValidReferences => source != null && priceText != null;

        private void OnEnable() => Refresh();

        private void Update() => Refresh();

        private void Refresh()
        {
            if (!HasValidReferences) return;
            string value = source.Price.ToString();
            if (priceText.text != value) priceText.text = value;
        }
    }
}
