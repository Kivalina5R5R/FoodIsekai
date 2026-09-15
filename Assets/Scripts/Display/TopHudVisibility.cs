using FoodIsekaiZ.Gameplay;
using UnityEngine;

namespace FoodIsekaiZ.Display
{
    // Owns opacity for the three authored HUD frames, including their captions and values.
    public sealed class TopHudVisibility : MonoBehaviour
    {
        [SerializeField] private FoodIsekaiZGameManager gameManager;
        [SerializeField] private CanvasGroup scoreFrame;
        [SerializeField] private CanvasGroup mvpFrame;
        [SerializeField] private CanvasGroup timeFrame;
        [SerializeField, Min(0.01f)] private float fadeDuration = 0.5f;

        private bool reportVisible;
        private float scoreProgress = 1f;
        private float mvpProgress = 1f;
        private float timeProgress = 1f;

        // Keep the HUD hidden until the outgoing report has finished its fade.
        public void SetReportVisible(bool visible)
        {
            reportVisible = visible;
        }

        // These frames stay active while their CanvasGroups animate visibility.
        public bool Owns(GameObject target)
        {
            return scoreFrame != null && scoreFrame.gameObject == target ||
                mvpFrame != null && mvpFrame.gameObject == target ||
                timeFrame != null && timeFrame.gameObject == target;
        }

        private void Update()
        {
            bool clearing = gameManager != null && gameManager.UsesMealWaves &&
                gameManager.CurrentMealWavePhase == MealWavePhase.Clearing;
            Fade(scoreFrame, !reportVisible, ref scoreProgress);
            Fade(mvpFrame, !reportVisible, ref mvpProgress);
            Fade(timeFrame, !reportVisible && !clearing, ref timeProgress);
        }

        private void Fade(CanvasGroup group, bool visible, ref float progress)
        {
            if (group == null) return;
            progress = Mathf.MoveTowards(progress, visible ? 1f : 0f,
                Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeDuration));
            group.alpha = Mathf.SmoothStep(0f, 1f, progress);
        }
    }
}
