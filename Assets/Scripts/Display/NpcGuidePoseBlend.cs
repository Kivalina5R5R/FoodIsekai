using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // Crossfades the two authored body images without changing their sprites or proportions.
    public sealed class NpcGuidePoseBlend : MonoBehaviour
    {
        [SerializeField] private Image standingImage;
        [SerializeField] private Image walkingImage;
        [SerializeField, Min(0.01f)] private float fadeSeconds = 0.35f;
        private float walkingWeight = 1f;

        public void ShowWalking()
        {
            walkingWeight = 1f;
            Apply();
        }

        public IEnumerator BlendTo(bool walking)
        {
            float from = walkingWeight;
            float target = walking ? 1f : 0f;
            float elapsed = 0f;
            while (elapsed < fadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / fadeSeconds));
                walkingWeight = Mathf.Lerp(from, target, t);
                Apply();
                yield return null;
            }
            walkingWeight = target;
            Apply();
        }

        private void Apply()
        {
            if (standingImage != null) standingImage.canvasRenderer.SetAlpha(1f - walkingWeight);
            if (walkingImage != null) walkingImage.canvasRenderer.SetAlpha(walkingWeight);
        }
    }
}
