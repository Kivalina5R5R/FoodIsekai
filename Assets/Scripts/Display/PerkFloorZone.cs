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
