using UnityEngine;

namespace FoodIsekaiZ.Display
{
    // Temporarily scales the existing button for its requested entrance, then restores its exact authored pose.
    [DisallowMultipleComponent]
    public sealed class ChaosButtonEntrance : MonoBehaviour
    {
        [SerializeField] private ChaosDisplayParticles particles;
        [SerializeField, Min(0f)] private float delay = 0.3f;
        [SerializeField, Min(0.05f)] private float growSeconds = 0.45f;
        [SerializeField, Min(0.05f)] private float settleSeconds = 0.3f;
        [SerializeField, Range(0.01f, 1f)] private float startScale = 0.08f;
        [SerializeField, Range(1f, 1.5f)] private float peakScale = 1.14f;
        private RectTransform target;
        private Vector3 authoredScale;
        private Vector3 authoredPosition;
        private float elapsed;
        private bool playing;
        private bool emitted;

        private void OnEnable()
        {
            target = transform as RectTransform;
            if (target == null) return;
            authoredScale = target.localScale;
            authoredPosition = target.anchoredPosition3D;
            elapsed = 0f;
            emitted = false;
            playing = true;
            ApplyScale(startScale);
        }

        private void Update()
        {
            if (!playing) return;
            elapsed += Time.unscaledDeltaTime;
            float age = elapsed - delay;
            if (age < 0f) return;
            if (age < growSeconds)
            {
                ApplyScale(Mathf.Lerp(startScale, peakScale, Mathf.SmoothStep(0f, 1f, age / growSeconds)));
                return;
            }
            if (!emitted)
            {
                emitted = true;
                particles?.Burst();
            }
            float progress = Mathf.Clamp01((age - growSeconds) / Mathf.Max(0.05f, settleSeconds));
            ApplyScale(Mathf.Lerp(peakScale, 1f, Mathf.SmoothStep(0f, 1f, progress)));
            if (progress >= 1f) Restore();
        }

        private void ApplyScale(float scale)
        {
            target.localScale = authoredScale * scale;
            // Compensate for the authored bottom pivot so the pop expands around the button's center.
            Vector3 compensation = target.localRotation * Vector3.Scale(target.rect.center, authoredScale * (1f - scale));
            target.anchoredPosition3D = authoredPosition + compensation;
        }

        private void OnDisable() => Restore();

        private void Restore()
        {
            if (!playing || target == null) return;
            target.localScale = authoredScale;
            target.anchoredPosition3D = authoredPosition;
            playing = false;
        }
    }
}
