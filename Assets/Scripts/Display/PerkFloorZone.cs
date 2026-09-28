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

        public bool Contains(Vector3 worldPosition)
        {
            if (selectionBounds == null || !gameObject.activeInHierarchy) return false;
            Vector3 point = selectionBounds.InverseTransformPoint(worldPosition);
            return selectionBounds.rect.Contains(new Vector2(point.x, point.y));
        }

        public void ShowStatus(string message, float amount, bool bought, bool contested)
        {
            if (statusText != null && statusText.text != message) statusText.text = message;
            if (progress != null) progress.ShowProgress(amount, bought, contested);
        }
    }
}
