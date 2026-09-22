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
        [SerializeField, Min(0.01f)] private float growSeconds = 0.28f;
        [SerializeField, Min(0.01f)] private float settleSeconds = 0.2f;
        [SerializeField, Range(1f, 1.5f)] private float outgoingScale = 1.16f;
        [SerializeField, Range(1f, 1.5f)] private float incomingScale = 1.24f;

        private Sprite nextSprite;
        private float elapsed;
        private float meshScale = 1f;
        private bool playing;
        private bool swapped;
        private bool particlesPlayed;
        private MealMenuTransition revealGate;

        // Initialization and disabled displays use the final sprite immediately.
        public void ShowImmediately(Sprite sprite)
        {
            playing = false;
            revealGate = null;
            nextSprite = sprite;
            meshScale = 1f;
            if (foodImage != null) foodImage.sprite = sprite;
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
            ShowImmediately(sprite);
            if (!isActiveAndEnabled || foodImage == null || sprite == null) return;

            elapsed = Mathf.Max(0.01f, anticipationSeconds) + Mathf.Max(0.01f, shrinkSeconds);
            meshScale = 0f;
            revealGate = waitForTransition;
            swapped = true;
            particlesPlayed = false;
            playing = true;
            graphic.SetVerticesDirty();
        }

        protected override void OnDisable()
        {
            if (playing) ShowImmediately(nextSprite);
            base.OnDisable();
        }

        private void Update()
        {
            if (!playing) return;
            if (revealGate != null && revealGate.IsVisible) return;
            revealGate = null;
            elapsed += Time.unscaledDeltaTime;
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
                meshScale = SmoothScale(0f, incomingScale, (elapsed - swapAt) / (peakAt - swapAt));
            else
                meshScale = SmoothScale(incomingScale, 1f, (elapsed - peakAt) / (finishAt - peakAt));

            if (elapsed >= finishAt)
            {
                meshScale = 1f;
                playing = false;
            }
            graphic.SetVerticesDirty();
        }

        private static float SmoothScale(float from, float to, float progress)
        {
            return Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress)));
        }

        public override void ModifyMesh(VertexHelper vertices)
        {
            if (!IsActive() || !playing) return;
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
