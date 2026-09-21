using FoodIsekaiZ.Players;
using UnityEngine;

namespace FoodIsekaiZ.Display
{
    // The ready-phase guide lives on a separately authored wall-display layer.
    public sealed class ReadyPhaseGuide : MonoBehaviour
    {
        [SerializeField] private PlayerReadySelection selection;
        [SerializeField] private MealMenuTransition menuTransition;
        [InspectorName("NPC Guide Player Prefab")]
        [SerializeField] private NpcGuidePresentation guidePrefab;
        [SerializeField, Range(0f, 1f)] private float standingWidthFraction = 0.55f;
        private NpcGuidePresentation instance;
        private bool spawned;
        private bool exitRequested;
        private bool coverRequested;
        private Vector2 exitPosition;

        public bool IsReadyForSelection => guidePrefab == null ||
            (instance != null && instance.HasOpenedDialogue);

        // Ready selection waits for this departure before releasing gameplay.
        public bool TryFinish()
        {
            if (!exitRequested)
            {
                exitRequested = true;
                if (instance != null) instance.WalkOut(exitPosition);
            }
            bool departed = instance == null || !instance.gameObject.activeInHierarchy || instance.HasExited;
            if (!departed) return false;
            if (!coverRequested)
            {
                coverRequested = true;
                if (menuTransition != null) StartCoroutine(menuTransition.Cover("BREAKFAST"));
            }
            return menuTransition == null || menuTransition.IsCovered;
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
