using UnityEngine;

namespace FoodIsekaiZ.Display
{
    // Reveals all customer pads together, independently of their live warning and occupancy state.
    [DisallowMultipleComponent]
    public sealed class CustomerSlotsFade : MonoBehaviour
    {
        [SerializeField] private Renderer[] slotRenderers;
        [SerializeField] private MealMenuTransition revealGate;
        [SerializeField, Min(0.05f)] private float duration = 0.7f;
        [SerializeField, Min(0f)] private float delay = 0.06f;

        private static readonly int OpacityId = Shader.PropertyToID("_RevealOpacity");
        private MaterialPropertyBlock properties;
        private float elapsed;
        private bool playing;

        private void OnEnable()
        {
            properties ??= new MaterialPropertyBlock();
            elapsed = 0f;
            playing = true;
            SetOpacity(0f);
        }

        private void LateUpdate()
        {
            if (!playing) return;
            if (revealGate != null && revealGate.IsVisible)
            {
                elapsed = 0f;
                SetOpacity(0f);
                return;
            }
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01((elapsed - delay) / Mathf.Max(0.05f, duration));
            float eased = progress * progress * progress * (progress * (progress * 6f - 15f) + 10f);
            SetOpacity(eased);
            if (progress >= 1f) playing = false;
        }

        private void OnDisable()
        {
            if (properties != null) SetOpacity(1f);
            playing = false;
        }

        private void SetOpacity(float opacity)
        {
            if (slotRenderers == null) return;
            foreach (Renderer slot in slotRenderers)
            {
                if (slot == null) continue;
                // Refresh the block so a live warning update is never overwritten by the fade.
                slot.GetPropertyBlock(properties);
                properties.SetFloat(OpacityId, opacity);
                slot.SetPropertyBlock(properties);
            }
        }
    }
}
