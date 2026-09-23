using UnityEngine;

namespace FoodIsekaiZ.Display
{
    // Fades a table's rendered material opacity while preserving its sprite and authored color.
    [DisallowMultipleComponent]
    public sealed class FloorTableFade : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer table;
        [SerializeField] private MealMenuTransition revealGate;
        [SerializeField, Min(0.05f)] private float duration = 0.7f;

        private static readonly int TintId = Shader.PropertyToID("_Color");
        private MaterialPropertyBlock originalProperties;
        private MaterialPropertyBlock fadingProperties;
        private Color materialTint;
        private float elapsed;
        private bool playing;

        private void OnEnable()
        {
            if (table == null || table.sharedMaterial == null || !table.sharedMaterial.HasProperty(TintId)) return;
            originalProperties ??= new MaterialPropertyBlock();
            fadingProperties ??= new MaterialPropertyBlock();
            table.GetPropertyBlock(originalProperties);
            table.GetPropertyBlock(fadingProperties);
            materialTint = originalProperties.HasColor(TintId)
                ? originalProperties.GetColor(TintId) : table.sharedMaterial.GetColor(TintId);
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
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.05f, duration));
            float eased = progress * progress * progress * (progress * (progress * 6f - 15f) + 10f);
            SetOpacity(eased);
            if (progress >= 1f) RestoreMaterial();
        }

        private void SetOpacity(float opacity)
        {
            Color tint = materialTint;
            tint.a *= opacity;
            fadingProperties.SetColor(TintId, tint);
            table.SetPropertyBlock(fadingProperties);
        }

        private void OnDisable()
        {
            RestoreMaterial();
        }

        private void RestoreMaterial()
        {
            if (playing && table != null) table.SetPropertyBlock(originalProperties);
            playing = false;
        }
    }
}
