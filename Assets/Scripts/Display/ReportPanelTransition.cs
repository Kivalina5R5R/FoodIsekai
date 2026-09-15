using System.Collections;
using UnityEngine;

namespace FoodIsekaiZ.Display
{
    // Animates report visibility, with an optional bounce relative to the authored scale.
    public sealed class ReportPanelTransition : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private CustomerPanelSuccessParticles[] particles;
        [SerializeField] private CustomerPanelAmbientSparkles borderSparkles;
        [SerializeField] private CanvasGroup backgroundGroup;
        [SerializeField] private RectTransform animatedContent;
        [SerializeField, Min(0.01f)] private float backgroundFadeDuration = 0.9f;
        [SerializeField, Min(0.01f)] private float enterDuration = 0.65f;
        [SerializeField, Min(0.01f)] private float exitDuration = 0.55f;
        [SerializeField] private bool animateScale;
        [SerializeField, Range(0.01f, 1f)] private float entranceScale = 0.18f;
        [SerializeField, Min(1f)] private float peakScale = 1.12f;

        private Transform ScaleTarget => animatedContent != null ? animatedContent : transform;

        private Vector3 authoredScale;
        private bool scaleCaptured;
        private float scaleMultiplier = 1f;
        private float backgroundStartAlpha;
        private float backgroundTargetAlpha;
        private float backgroundElapsed;
        private bool backgroundFading;

        // The parent controller runs this routine so it can finish after the panel is disabled.
        // Reversing a transition continues from its current opacity.
        public IEnumerator Fade(bool visible)
        {
            if (canvasGroup == null)
            {
                gameObject.SetActive(visible);
                yield break;
            }

            if (backgroundGroup != null)
            {
                // BG_Alpha is a sibling of the animated content and has its own opacity.
                if (!gameObject.activeSelf) backgroundGroup.alpha = 0f;
                backgroundStartAlpha = backgroundGroup.alpha;
                backgroundTargetAlpha = visible ? 1f : 0f;
                backgroundElapsed = 0f;
                backgroundFading = true;
            }

            if (animateScale)
            {
                yield return Bounce(visible);
            }
            else
            {
                if (!gameObject.activeSelf) canvasGroup.alpha = 0f;
                gameObject.SetActive(true);
                PlayParticles();

                yield return Animate(1f, visible ? 1f : 0f, visible ? enterDuration : exitDuration);
            }
            if (!visible)
            {
                // A slower background fade must finish before disabling the whole report.
                while (backgroundFading) yield return null;
                gameObject.SetActive(false);
            }
        }

        private IEnumerator Bounce(bool visible)
        {
            if (!scaleCaptured)
            {
                authoredScale = ScaleTarget.localScale;
                scaleCaptured = true;
            }
            if (!gameObject.activeSelf)
            {
                scaleMultiplier = entranceScale;
                ScaleTarget.localScale = authoredScale * scaleMultiplier;
                canvasGroup.alpha = 0f;
            }
            gameObject.SetActive(true);

            float duration = Mathf.Max(0.01f, visible ? enterDuration : exitDuration);
            float expandFraction = visible ? 0.65f : 0.25f;
            // Reach full opacity and the oversized pose before emitting the burst.
            yield return Animate(peakScale, 1f, duration * expandFraction);
            PlayParticles();
            yield return Animate(visible ? 1f : 0f, visible ? 1f : 0f,
                duration * (1f - expandFraction));
        }

        private void PlayParticles()
        {
            if (borderSparkles != null) borderSparkles.Pulse();
            if (particles != null)
            {
                foreach (CustomerPanelSuccessParticles particle in particles)
                {
                    if (particle != null) particle.Play();
                }
            }
        }

        private IEnumerator Animate(float targetScale, float targetAlpha, float duration)
        {
            float startAlpha = canvasGroup.alpha;
            float startScale = scaleMultiplier;
            duration = Mathf.Max(0.01f, duration);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, progress);
                if (animateScale)
                {
                    scaleMultiplier = Mathf.Lerp(startScale, targetScale, progress);
                    ScaleTarget.localScale = authoredScale * scaleMultiplier;
                }
                yield return null;
            }

            canvasGroup.alpha = targetAlpha;
            if (animateScale)
            {
                scaleMultiplier = targetScale;
                ScaleTarget.localScale = authoredScale * scaleMultiplier;
            }
        }

        private void OnDisable()
        {
            backgroundFading = false;
            if (backgroundGroup != null) backgroundGroup.alpha = 1f;
            // Restore the authored size after hiding or cancellation so repeated breaks cannot drift.
            if (!scaleCaptured) return;
            ScaleTarget.localScale = authoredScale;
            scaleMultiplier = 1f;
        }

        private void Update()
        {
            if (!backgroundFading || backgroundGroup == null) return;
            backgroundElapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(backgroundElapsed / Mathf.Max(0.01f, backgroundFadeDuration));
            backgroundGroup.alpha = Mathf.Lerp(backgroundStartAlpha,
                backgroundTargetAlpha, Mathf.SmoothStep(0f, 1f, progress));
            if (progress >= 1f) backgroundFading = false;
        }
    }
}
