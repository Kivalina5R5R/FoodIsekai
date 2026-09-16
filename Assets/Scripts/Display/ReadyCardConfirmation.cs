using System.Collections;
using UnityEngine;

namespace FoodIsekaiZ.Display
{
    // Owns the requested card pop/shrink animation and its Ready prefab replacement.
    public sealed class ReadyCardConfirmation : MonoBehaviour
    {
        [SerializeField] private RectTransform readyPrefab;
        [SerializeField, Min(1f)] private float popScale = 1.16f;
        [SerializeField, Min(0.01f)] private float expandSeconds = 0.18f;
        [SerializeField, Min(0.01f)] private float shrinkSeconds = 0.24f;
        [SerializeField, Min(0f)] private float readyVisibleSeconds = 0.5f;
        [SerializeField, Min(1f)] private float readyPopScale = 1.18f;
        [SerializeField, Min(0.01f)] private float readyGrowSeconds = 0.24f;
        [SerializeField, Min(0.01f)] private float readySettleSeconds = 0.18f;

        private bool started;
        private bool revealed;
        private float revealedAt;
        private Vector3 authoredScale;
        private bool scaleCaptured;
        private bool occupied;
        private Coroutine feedback;

        public bool IsEntering { get; private set; }

        public bool IsFinished => revealed && Time.unscaledTime - revealedAt >= readyVisibleSeconds;
        public bool IsConfigured => readyPrefab != null && transform is RectTransform;

        // Entrance and occupancy share one animation channel so confirmation can take over cleanly.
        public void PlayEntrance(int order)
        {
            CaptureScale();
            if (feedback != null) StopCoroutine(feedback);
            IsEntering = true;
            transform.localScale = authoredScale * 0.001f;
            feedback = StartCoroutine(Enter(order * 0.09f));
        }

        public void SetOccupied(bool value)
        {
            if (started || IsEntering || occupied == value) return;
            occupied = value;
            CaptureScale();
            if (feedback != null) StopCoroutine(feedback);
            feedback = StartCoroutine(AnimateOccupancy());
        }

        private void CaptureScale()
        {
            if (scaleCaptured) return;
            authoredScale = transform.localScale;
            scaleCaptured = true;
        }

        private IEnumerator Enter(float delay)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            var card = (RectTransform)transform;
            yield return ScaleCard(card, authoredScale, 0.001f, 1.07f, 0.28f);
            yield return ScaleCard(card, authoredScale, 1.07f, 1f, 0.16f);
            IsEntering = false;
            feedback = null;
        }

        private IEnumerator AnimateOccupancy()
        {
            var card = (RectTransform)transform;
            float from = card.localScale.x / authoredScale.x;
            if (!occupied)
            {
                yield return ScaleCard(card, authoredScale, from, 1f, 0.16f);
                yield break;
            }
            yield return ScaleCard(card, authoredScale, from, 1.09f, 0.14f);
            yield return ScaleCard(card, authoredScale, 1.09f, 1.035f, 0.18f);
            float began = Time.unscaledTime;
            while (occupied)
            {
                card.localScale = authoredScale * (1.035f + Mathf.Sin((Time.unscaledTime - began) * 5f) * 0.012f);
                yield return null;
            }
        }

        public void Confirm()
        {
            if (started || !IsConfigured) return;
            CaptureScale();
            if (feedback != null) StopCoroutine(feedback);
            started = true;
            StartCoroutine(Animate());
        }

        private IEnumerator Animate()
        {
            var card = (RectTransform)transform;
            float from = card.localScale.x / authoredScale.x;
            yield return ScaleCard(card, authoredScale, from, popScale, expandSeconds);
            yield return ScaleCard(card, authoredScale, popScale, 0f, shrinkSeconds);

            var replacement = Instantiate(readyPrefab, card.parent, false);
            replacement.anchoredPosition3D = card.anchoredPosition3D;
            replacement.localRotation = card.localRotation;
            // Uniform fitting keeps the Ready sprite and its authored text proportions intact.
            float width = Mathf.Abs(card.rect.width * authoredScale.x);
            float prefabWidth = Mathf.Abs(readyPrefab.rect.width * readyPrefab.localScale.x);
            Vector3 readyScale = readyPrefab.localScale * (width / Mathf.Max(0.001f, prefabWidth));
            replacement.localScale = readyScale * 0.05f;
            replacement.gameObject.SetActive(true);
            yield return ScaleCard(replacement, readyScale, 0.05f, readyPopScale, readyGrowSeconds);
            yield return ScaleCard(replacement, readyScale, readyPopScale, 1f, readySettleSeconds);
            var stars = replacement.GetComponentInChildren<ReadyStarParticles>();
            if (stars != null) stars.BeginIdle();
            revealed = true;
            revealedAt = Time.unscaledTime;
            card.gameObject.SetActive(false);
        }

        private static IEnumerator ScaleCard(RectTransform card, Vector3 authoredScale,
            float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = t * t * (3f - 2f * t);
                card.localScale = authoredScale * Mathf.Lerp(from, to, t);
                yield return null;
            }
            card.localScale = authoredScale * to;
        }
    }
}
