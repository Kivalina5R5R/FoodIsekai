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
        public float FadeSeconds => Mathf.Max(0.01f, fadeSeconds);
        public float WalkingWeight { get; private set; }

        public void ShowWalking()
        {
            SetWalkingWeight(1f);
        }

        // The presentation supplies an eased weight while the guide is stationary.
        public void SetWalkingWeight(float weight)
        {
            float walkingWeight = Mathf.Clamp01(weight);
            WalkingWeight = walkingWeight;
            if (standingImage != null) standingImage.canvasRenderer.SetAlpha(1f - walkingWeight);
            if (walkingImage != null) walkingImage.canvasRenderer.SetAlpha(walkingWeight);
        }
    }
}
