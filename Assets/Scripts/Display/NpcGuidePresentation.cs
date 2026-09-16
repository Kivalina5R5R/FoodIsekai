using System.Collections;
using UnityEngine;

namespace FoodIsekaiZ.Display
{
    // Moves the guide while preserving the prefab's authored body and dialogue styling.
    public sealed class NpcGuidePresentation : MonoBehaviour
    {
        [SerializeField] private GameObject dialogue;
        [SerializeField] private RectTransform visualBody;
        [SerializeField] private NpcIdleBreathing breathing;
        [SerializeField] private NpcGuidePoseBlend poseBlend;
        [SerializeField, Min(0.01f)] private float walkingSpeedCanvasMultiplier = 0.264f;
        [SerializeField, Min(0.05f)] private float arrivalStoppingSeconds = 0.28f;
        [SerializeField, Min(0f)] private float walkingBobHeight = 12f;
        [SerializeField, Min(0.1f)] private float walkingBobFrequency = 2.2f;
        private Coroutine entrance;
        private Vector2 groundedPosition;
        private float speed;
        private RectTransform dialogueRect;
        private Vector3 dialogueScale;

        public bool HasExited { get; private set; }

        private void Awake()
        {
            dialogueRect = dialogue != null ? dialogue.transform as RectTransform : null;
            if (dialogueRect != null) dialogueScale = dialogueRect.localScale;
            if (dialogue != null) dialogue.SetActive(false);
            if (breathing != null) breathing.Initialize(visualBody, 3.6f, 0.008f, 0.0025f, 0.45f, 0f, dialogueRect);
            if (poseBlend != null) poseBlend.ShowWalking();
        }

        public void WalkIn(Vector2 start, Vector2 destination, float canvasWidth)
        {
            if (entrance != null) StopCoroutine(entrance);
            if (dialogue != null) dialogue.SetActive(false);
            speed = Mathf.Max(1f, canvasWidth * walkingSpeedCanvasMultiplier);
            HasExited = false;
            groundedPosition = start;
            if (poseBlend != null) poseBlend.ShowWalking();
            entrance = StartCoroutine(Walk(start, destination, false));
        }

        public void WalkOut(Vector2 destination)
        {
            if (entrance != null) StopCoroutine(entrance);
            entrance = StartCoroutine(TurnAndExit(destination));
        }

        private IEnumerator TurnAndExit(Vector2 destination)
        {
            yield return AnimateDialogue(false);
            if (breathing != null) breathing.EndIdle();
            if (poseBlend != null) yield return poseBlend.BlendTo(true);
            var rect = (RectTransform)transform;
            Vector3 startScale = rect.localScale;
            Quaternion startRotation = rect.localRotation;
            float direction = Mathf.Sign(destination.x - groundedPosition.x);
            float elapsed = 0f;
            // Match the customers' 0.3-second turn: compress, flip halfway, then recover.
            while (elapsed < 0.3f)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / 0.3f);
                float collapse = Mathf.Sin(t * Mathf.PI);
                float eased = Mathf.SmoothStep(0f, 1f, collapse);
                Vector3 scale = startScale;
                scale.x *= Mathf.Lerp(1f, 0.78f, eased) * (t < .5f ? 1f : -1f);
                scale.y *= 1f + 0.02f * eased;
                rect.localScale = scale;
                rect.localRotation = startRotation * Quaternion.Euler(0f, 0f, collapse * 2.5f * direction);
                rect.anchoredPosition = groundedPosition + Vector2.up * (collapse * 2f);
                yield return null;
            }
            rect.localScale = new Vector3(-startScale.x, startScale.y, startScale.z);
            rect.localRotation = startRotation;
            rect.anchoredPosition = groundedPosition;
            yield return Walk(groundedPosition, destination, true);
        }

        private IEnumerator Walk(Vector2 start, Vector2 destination, bool exiting)
        {
            var rect = (RectTransform)transform;
            float distance = Vector2.Distance(start, destination);
            float stopDistance = exiting ? 0f : Mathf.Min(distance, .5f * speed * arrivalStoppingSeconds);
            float cruiseSeconds = (distance - stopDistance) / speed;
            float stopSeconds = stopDistance * 2f / speed;
            float duration = cruiseSeconds + stopSeconds;
            float elapsed = 0f;
            rect.anchoredPosition = start;
            while (elapsed < duration)
            {
                elapsed = Mathf.Min(duration, elapsed + Time.unscaledDeltaTime);
                float travelled;
                if (elapsed <= cruiseSeconds || stopSeconds <= 0f)
                    travelled = Mathf.Min(distance, speed * elapsed);
                else
                {
                    float remaining = 1f - Mathf.Clamp01((elapsed - cruiseSeconds) / stopSeconds);
                    travelled = distance - stopDistance * remaining * remaining;
                }
                groundedPosition = Vector2.Lerp(start, destination, distance > 0f ? travelled / distance : 1f);
                // Match customer stride: remaining distance grounds the final step at the destination.
                float phase = -(distance - travelled) / speed * walkingBobFrequency * Mathf.PI * 2f;
                float bob = (.5f - .5f * Mathf.Cos(phase)) * walkingBobHeight;
                rect.anchoredPosition = groundedPosition + Vector2.up * bob;
                yield return null;
            }
            rect.anchoredPosition = destination;
            groundedPosition = destination;
            if (exiting)
            {
                HasExited = true;
                gameObject.SetActive(false);
                yield break;
            }
            if (poseBlend != null) yield return poseBlend.BlendTo(false);
            if (breathing != null) breathing.BeginIdle();
            yield return AnimateDialogue(true);
            entrance = null;
        }

        private IEnumerator AnimateDialogue(bool show)
        {
            if (dialogueRect == null) yield break;
            if (!show && !dialogue.activeSelf) yield break;
            if (show)
            {
                dialogueRect.localScale = dialogueScale * 0.05f;
                dialogue.SetActive(true);
            }
            Vector3 from = dialogueRect.localScale;
            Vector3 peak = dialogueScale * 1.12f;
            yield return ScaleDialogue(from, peak, show ? 0.22f : 0.12f);
            yield return ScaleDialogue(peak, show ? dialogueScale : Vector3.zero, show ? 0.16f : 0.2f);
            if (!show)
            {
                dialogue.SetActive(false);
                dialogueRect.localScale = dialogueScale;
            }
        }

        // Only scale is animated here; NpcIdleBreathing owns the dialogue's vertical following.
        private IEnumerator ScaleDialogue(Vector3 from, Vector3 to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                dialogueRect.localScale = Vector3.LerpUnclamped(from, to, t);
                yield return null;
            }
            dialogueRect.localScale = to;
        }
    }
}
