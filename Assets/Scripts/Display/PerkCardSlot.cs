using FoodIsekaiZ.Gameplay;
using UnityEngine;

namespace FoodIsekaiZ.Display
{
    // Each authored empty slot owns one randomly chosen card.
    public sealed class PerkCardSlot : MonoBehaviour
    {
        [SerializeField] private Transform artworkParent;
        private GameObject artwork;

        public void SetOffer(PerkOffer offer, GameObject prefab)
        {
            if (artwork != null)
            {
                artwork.SetActive(false);
                Destroy(artwork);
            }
            artwork = prefab != null ? Instantiate(prefab, artworkParent, false) : null;
            gameObject.SetActive(offer != null && artwork != null);
        }
    }
}
