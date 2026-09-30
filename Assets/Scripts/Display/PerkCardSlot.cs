using FoodIsekaiZ.Gameplay;
using UnityEngine;

namespace FoodIsekaiZ.Display
{
    // Each authored empty slot owns one randomly chosen card.
    public sealed class PerkCardSlot : MonoBehaviour
    {
        [SerializeField] private Transform artworkParent;
        private GameObject artwork;
        private PerkCardAnimation presentation;

        public bool IsHidden => !gameObject.activeInHierarchy || presentation == null || presentation.IsHidden;
        public bool HasRevealed => !gameObject.activeInHierarchy || presentation == null || presentation.HasRevealed;

        private void Awake() => presentation = GetComponent<PerkCardAnimation>();

        // Copies only the card artwork; the purchase presentation owns and disposes the copy.
        public Transform CopyPurchaseArtwork(Transform parent)
        {
            return artworkParent != null ? Instantiate(artworkParent, parent, false) : null;
        }

        public void Hide()
        {
            presentation = GetComponent<PerkCardAnimation>();
            if (presentation != null) presentation.Hide();
        }

        public void SetOffer(PerkOffer offer, GameObject prefab, bool bigPerk)
        {
            if (artwork != null)
            {
                artwork.SetActive(false);
                Destroy(artwork);
            }
            artwork = prefab != null ? Instantiate(prefab, artworkParent, false) : null;
            var frameEffect = GetComponentInChildren<PerkCardSparkles>(true);
            if (frameEffect != null) frameEffect.SetTier(bigPerk);
            gameObject.SetActive(offer != null && artwork != null);
        }
    }
}
