using FoodIsekaiZ.Players;
using UnityEngine;

namespace FoodIsekaiZ.Display
{
    // The ready-phase guide lives on a separately authored wall-display layer.
    public sealed class ReadyPhaseGuide : MonoBehaviour
    {
        [SerializeField] private PlayerReadySelection selection;
        [InspectorName("NPC Guide Player Prefab")]
        [SerializeField] private NpcGuidePresentation guidePrefab;
        [SerializeField, Range(0f, 1f)] private float standingWidthFraction = 0.55f;
        private NpcGuidePresentation instance;
        private bool spawned;
        private bool exitRequested;
        private Vector2 exitPosition;

        // Ready selection waits for this departure before releasing gameplay.
        public bool TryFinish()
        {
            if (instance == null || !instance.gameObject.activeInHierarchy) return true;
            if (!exitRequested)
            {
                exitRequested = true;
                instance.WalkOut(exitPosition);
            }
            return instance.HasExited;
        }

        private void LateUpdate()
        {
            if (selection == null) return;
            if (selection.IsCompleted)
            {
                if (instance != null && instance.gameObject.activeSelf) instance.gameObject.SetActive(false);
                return;
            }
            if (spawned || !selection.IsSelecting || guidePrefab == null) return;
            spawned = true;
            instance = Instantiate(guidePrefab, transform, false);
            Rect area = ((RectTransform)transform).rect;
            exitPosition = new Vector2(area.xMax + area.width * .5f, area.yMin);
            Vector2 destination = new Vector2(Mathf.Lerp(area.xMin, area.xMax, standingWidthFraction), area.yMin);
            instance.WalkIn(exitPosition, destination, area.width);
        }

        private void OnDisable()
        {
            if (instance != null) instance.gameObject.SetActive(false);
        }
    }
}
