using TMPro;
using UnityEngine;

namespace FoodIsekaiZ.Display
{
    // Tests a tracked player's position against an authored floor selection rectangle.
    public sealed class PerkFloorZone : MonoBehaviour
    {
        [SerializeField] private RectTransform selectionBounds;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private ReadyConfirmationGauge progress;
        private PerkCardAnimation presentation;
        private bool purchased;
        public bool HasRevealed => !gameObject.activeInHierarchy || presentation == null || presentation.HasRevealed;
        public bool IsHidden => !gameObject.activeInHierarchy || presentation == null || presentation.IsHidden;

        public void Hide()
        {
            if (progress != null) progress.gameObject.SetActive(false);
            if (presentation != null) presentation.Hide();
        }

        private void Awake() => presentation = GetComponent<PerkCardAnimation>();

        // Swells and gently pulses like the player-selection cards while a player holds to buy here.
        public void SetOccupied(bool value)
        {
            if (presentation != null) presentation.SetOccupied(value);
        }

        // Warns a player standing here that the team cannot afford this card.
        public void ShakeWarning()
        {
            if (presentation != null) presentation.Shake();
        }

        private void OnEnable()
        {
            purchased = false;
            if (progress != null) progress.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (purchased && (presentation == null || presentation.IsHidden)) gameObject.SetActive(false);
        }

        // Celebrates the purchase on the tile itself. The burst lives on the floor canvas, not under this
        // tile, because the tile shrinks away and is deactivated while the burst is still playing.
        public void PlayPurchaseBurst(bool rainbow)
        {
            if (selectionBounds == null || !gameObject.activeInHierarchy) return;
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            var canvasRect = (RectTransform)canvas.rootCanvas.transform;
            var corners = new Vector3[4];
            selectionBounds.GetWorldCorners(corners);
            Vector2 minimum = canvasRect.InverseTransformPoint(corners[0]);
            Vector2 maximum = canvasRect.InverseTransformPoint(corners[2]);
            float radius = Mathf.Max(maximum.x - minimum.x, maximum.y - minimum.y) * .5f;
            var root = (RectTransform)new GameObject("Perk Purchase Burst", typeof(RectTransform)).transform;
            root.gameObject.layer = canvasRect.gameObject.layer;
            root.SetParent(canvasRect, false);
            root.anchorMin = root.anchorMax = root.pivot = Vector2.one * .5f;
            root.sizeDelta = Vector2.one * radius * 6f;
            root.anchoredPosition = (minimum + maximum) * .5f;
            var overlay = root.gameObject.AddComponent<Canvas>();
            overlay.overrideSorting = true;
            overlay.sortingLayerID = canvas.rootCanvas.sortingLayerID;
            overlay.sortingOrder = canvas.rootCanvas.sortingOrder + 25;
            root.gameObject.AddComponent<PerkFloorPurchaseBurst>().Play(radius, rainbow, root.gameObject);
        }

        public bool Contains(Vector3 worldPosition)
        {
            if (selectionBounds == null || !gameObject.activeInHierarchy) return false;
            Vector3 point = selectionBounds.InverseTransformPoint(worldPosition);
            return selectionBounds.rect.Contains(new Vector2(point.x, point.y));
        }

        public void ShowStatus(string message, float amount, bool bought, bool contested, bool showGauge)
        {
            if (statusText != null && statusText.text != message) statusText.text = message;
            if (progress != null)
            {
                progress.gameObject.SetActive(showGauge && !bought);
                progress.ShowProgress(amount, bought, contested);
            }
            if (bought && !purchased)
            {
                purchased = true;
                if (presentation != null) presentation.Hide();
            }
        }
    }
}
