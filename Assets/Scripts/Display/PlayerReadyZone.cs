using UnityEngine;

namespace FoodIsekaiZ.Display
{
    // Reads an authored floor card's bounds and displays its current selection progress.
    public sealed class PlayerReadyZone : MonoBehaviour
    {
        [SerializeField, Range(1, 4)] private int playerNumber = 1;
        [SerializeField] private RectTransform selectionBounds;
        [SerializeField] private ReadyConfirmationGauge confirmationGauge;
        [SerializeField] private ReadyCardConfirmation confirmation;
        private Matrix4x4 selectionWorldToLocal;
        private Rect authoredBounds;
        private bool boundsCaptured;

        public int PlayerNumber => playerNumber;
        public bool IsConfirmationFinished => confirmation != null && confirmation.IsFinished;
        public bool IsConfirmationConfigured => confirmation != null && confirmation.IsConfigured;
        public bool IsEntering => confirmation != null && confirmation.IsEntering;

        public void PlayEntrance(int order)
        {
            if (selectionBounds != null)
            {
                // The interaction footprint stays fixed while the card visually bounces.
                selectionWorldToLocal = selectionBounds.worldToLocalMatrix;
                authoredBounds = selectionBounds.rect;
                boundsCaptured = true;
            }
            if (confirmation != null) confirmation.PlayEntrance(order);
        }

        private void Awake()
        {
            if (confirmationGauge != null) confirmationGauge.gameObject.SetActive(false);
        }

        public bool Contains(Vector3 worldPosition)
        {
            if (selectionBounds == null) return false;
            if (boundsCaptured)
            {
                Vector3 point = selectionWorldToLocal.MultiplyPoint3x4(worldPosition);
                return authoredBounds.Contains(new Vector2(point.x, point.y));
            }
            Vector3 local = selectionBounds.InverseTransformPoint(worldPosition);
            return selectionBounds.rect.Contains(new Vector2(local.x, local.y));
        }

        public void ShowProgress(float progress, int? ownerTag, bool contested, bool occupied = false)
        {
            if (ownerTag.HasValue && confirmation != null) confirmation.Confirm();
            else if (confirmation != null) confirmation.SetOccupied(occupied);
            if (confirmationGauge != null)
            {
                bool visible = occupied || ownerTag.HasValue;
                if (confirmationGauge.gameObject.activeSelf != visible)
                    confirmationGauge.gameObject.SetActive(visible);
                confirmationGauge.ShowProgress(progress, ownerTag.HasValue, contested);
            }
        }
    }
}
