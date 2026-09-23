using UnityEngine;

namespace FoodIsekaiZ.Display
{
    // Reveals a decorative UI layer without changing its authored layout or tint.
    [DisallowMultipleComponent]
    public sealed class FloorDecorFade : MonoBehaviour
    {
        [SerializeField] private CanvasGroup visibility;
        [SerializeField] private MealMenuTransition revealGate;
        [SerializeField, Min(0.05f)] private float duration = 0.7f;
        [SerializeField, Min(0f)] private float delay = 0.06f;

        private float elapsed;
        private float authoredAlpha;
        private bool playing;

        private void OnEnable()
        {
            if (visibility == null) return;
            authoredAlpha = visibility.alpha;
            elapsed = 0f;
            playing = true;
            visibility.alpha = 0f;
        }

        private void LateUpdate()
        {
            if (!playing || visibility == null) return;
            // Activation happens behind the menu; it must not consume the fade there.
            if (revealGate != null && revealGate.IsVisible)
            {
                elapsed = 0f;
                visibility.alpha = 0f;
                return;
            }
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01((elapsed - delay) / Mathf.Max(0.05f, duration));
            float eased = progress * progress * progress * (progress * (progress * 6f - 15f) + 10f);
            visibility.alpha = authoredAlpha * eased;
            if (progress >= 1f) playing = false;
        }

        private void OnDisable()
        {
            if (visibility != null && playing) visibility.alpha = authoredAlpha;
            playing = false;
        }
    }
}
