using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // Animates a menu replacement in the image mesh without changing authored transforms.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class MealFoodSwapEffect : BaseMeshEffect
    {
        [SerializeField] private Image foodImage;
        [SerializeField] private CustomerPanelSuccessParticles revealParticles;
        [SerializeField, Min(0.01f)] private float anticipationSeconds = 0.16f;
        [SerializeField, Min(0.01f)] private float shrinkSeconds = 0.22f;
        [SerializeField, Min(0.01f)] private float growSeconds = 0.45f;
        [SerializeField, Min(0.01f)] private float settleSeconds = 0.4f;
        [SerializeField, Range(1f, 1.5f)] private float outgoingScale = 1.16f;
        [SerializeField, Range(1f, 2f)] private float incomingScale = 1.5f;
        // Keep the swell and disappearance connected without a stationary hold at the peak.
        [SerializeField, Range(1f, 2f)] private float hidePeakScale = 1.45f;
        [SerializeField, Min(0.01f)] private float hideEnlargeSeconds = 0.35f;
        [SerializeField, Min(0.01f)] private float hideShrinkSeconds = 0.58f;

        private Sprite nextSprite;
        private float elapsed;
        private float meshScale = 1f;
        private bool playing;
        private bool swapped;
        private bool particlesPlayed;
        private bool hiding;
        private float hideStartScale;
        private MealMenuTransition revealGate;

        // Companion labels follow the icon's visibility during meal transitions.
        public bool IsIconVisible => foodImage != null && foodImage.enabled && meshScale > 0.01f;

        // Initialization and disabled displays use the final sprite immediately.
        public void ShowImmediately(Sprite sprite)
        {
            playing = false;
            hiding = false;
            revealGate = null;
            nextSprite = sprite;
            meshScale = 1f;
            if (foodImage != null)
            {
                foodImage.sprite = sprite;
                foodImage.enabled = sprite != null;
            }
            revealParticles?.Stop();
            if (graphic != null) graphic.SetVerticesDirty();
        }

        // Repeated requests for the pending menu do not restart the animation.
        public void Play(Sprite sprite)
        {
            if (playing && nextSprite == sprite) return;
            if (!isActiveAndEnabled || foodImage == null || foodImage.sprite == null || sprite == null)
            {
                ShowImmediately(sprite);
                return;
            }
            if (!playing && foodImage.sprite == sprite) return;

            ShowImmediately(foodImage.sprite);
            nextSprite = sprite;
            elapsed = 0f;
            swapped = false;
            particlesPlayed = false;
            playing = true;
        }

        // Hold the incoming food at zero size until the optional menu transition finishes.
        public void PlayReveal(Sprite sprite, MealMenuTransition waitForTransition = null)
        {
            PrepareHidden(sprite);
            // Prepare the hidden mesh even when gameplay's parent is still inactive.
            // Otherwise enabling it shows the full icon before the reveal event hides it.
            if (foodImage == null || sprite == null) return;

            elapsed = Mathf.Max(0.01f, anticipationSeconds) + Mathf.Max(0.01f, shrinkSeconds);
            meshScale = 0f;
            revealGate = waitForTransition;
            swapped = true;
            particlesPlayed = false;
            playing = true;
            graphic.SetVerticesDirty();
        }

        // Disable drawing before assigning artwork, including any cached full-size canvas mesh.
        public void PrepareHidden(Sprite sprite)
        {
            if (foodImage != null)
            {
                foodImage.enabled = false;
                foodImage.sprite = sprite;
            }
            playing = false;
            hiding = false;
            revealGate = null;
            nextSprite = sprite;
            meshScale = 0f;
            revealParticles?.Stop();
            if (graphic != null) graphic.SetVerticesDirty();
        }

        protected override void OnDisable()
        {
            if (hiding)
            {
                meshScale = 0f;
                playing = false;
                revealGate = null;
                RefreshImageVisibility();
            }
            // Retain a queued or partly played entrance across parent activation changes.
            // Restoring full size here could expose it before the next reveal request.
            base.OnDisable();
        }

        // After the page transition, enlarge the outgoing icon before shrinking it away.
        public void PlayHide(MealMenuTransition waitForTransition = null)
        {
            if (hiding) return;
            hiding = true;
            hideStartScale = meshScale;
            elapsed = 0f;
            revealGate = waitForTransition;
            playing = true;
            revealParticles?.Stop();
        }

        private void Update()
        {
            if (!playing) return;
            if (revealGate != null && revealGate.IsVisible) return;
            revealGate = null;
            elapsed += Time.unscaledDeltaTime;
            if (hiding)
            {
                float enlargeDuration = Mathf.Max(0.01f, hideEnlargeSeconds);
                float shrinkDuration = Mathf.Max(0.01f, hideShrinkSeconds);
                float peakScale = hideStartScale * hidePeakScale;
                if (elapsed < enlargeDuration)
                    meshScale = SmootherScale(hideStartScale, peakScale, elapsed / enlargeDuration);
                else
                    meshScale = SmootherScale(peakScale, 0f, (elapsed - enlargeDuration) / shrinkDuration);
                if (elapsed >= enlargeDuration + shrinkDuration)
                {
                    meshScale = 0f;
                    playing = false;
                    if (hideStartScale > 0f) revealParticles?.Play();
                }
                RefreshImageVisibility();
                graphic.SetVerticesDirty();
                return;
            }
            float anticipation = Mathf.Max(0.01f, anticipationSeconds);
            float swapAt = anticipation + Mathf.Max(0.01f, shrinkSeconds);
            float peakAt = swapAt + Mathf.Max(0.01f, growSeconds);
            float finishAt = peakAt + Mathf.Max(0.01f, settleSeconds);

            if (!swapped && elapsed >= swapAt)
            {
                swapped = true;
                foodImage.sprite = nextSprite;
            }
            // Sparkles burst during the new food's upward bounce, just before its peak.
            if (!particlesPlayed && elapsed >= Mathf.Lerp(swapAt, peakAt, 0.65f))
            {
                particlesPlayed = true;
                revealParticles?.Play();
            }

            if (elapsed < anticipation)
                meshScale = SmoothScale(1f, outgoingScale, elapsed / anticipation);
            else if (elapsed < swapAt)
                meshScale = SmoothScale(outgoingScale, 0f, (elapsed - anticipation) / (swapAt - anticipation));
            else if (elapsed < peakAt)
                meshScale = SmootherScale(0f, incomingScale, (elapsed - swapAt) / (peakAt - swapAt));
            else
                meshScale = SmootherScale(incomingScale, 1f, (elapsed - peakAt) / (finishAt - peakAt));

            if (elapsed >= finishAt)
            {
                meshScale = 1f;
                playing = false;
            }
            RefreshImageVisibility();
            graphic.SetVerticesDirty();
        }

        private void RefreshImageVisibility()
        {
            if (foodImage != null) foodImage.enabled = meshScale > 0.001f && foodImage.sprite != null;
        }

        private static float SmoothScale(float from, float to, float progress)
        {
            return Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress)));
        }

        // Zero velocity and acceleration at both ends avoid a sharp change at the swell's peak.
        private static float SmootherScale(float from, float to, float progress)
        {
            float t = Mathf.Clamp01(progress);
            float eased = t * t * t * (t * (6f * t - 15f) + 10f);
            return Mathf.Lerp(from, to, eased);
        }

        public override void ModifyMesh(VertexHelper vertices)
        {
            if (!IsActive()) return;
            Vector3 center = graphic.rectTransform.rect.center;
            UIVertex vertex = default;
            for (int i = 0; i < vertices.currentVertCount; i++)
            {
                vertices.PopulateUIVertex(ref vertex, i);
                vertex.position = center + (vertex.position - center) * meshScale;
                vertices.SetUIVertex(vertex, i);
            }
        }
    }
}
