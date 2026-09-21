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

        public void ShowWalking()
        {
            SetWalkingWeight(1f);
        }

        // The movement timeline supplies an eased weight so pose changes overlap motion.
        public void SetWalkingWeight(float weight)
        {
            float walkingWeight = Mathf.Clamp01(weight);
            if (standingImage != null) standingImage.canvasRenderer.SetAlpha(1f - walkingWeight);
            if (walkingImage != null) walkingImage.canvasRenderer.SetAlpha(walkingWeight);
        }
    }
}
