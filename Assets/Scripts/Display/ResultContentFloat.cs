using UnityEngine;

namespace FoodIsekaiZ.Display
{
    // Slowly bobs the whole result card up and down around its authored position while the results are shown.
    // Only this object moves; the dimmed BG_Alpha backdrop beside it stays still.
    // The panel transition animates this object's scale, so the float only touches its position.
    [AddComponentMenu("Food Isekai Z/Display/Result Content Float")]
    public sealed class ResultContentFloat : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float amplitude = 1.5f;
        [SerializeField, Min(0.5f)] private float cycleSeconds = 4f;

        private RectTransform target;
        private Vector2 origin;
        private float elapsed;

        private void OnEnable()
        {
            target = transform as RectTransform;
            if (target != null) origin = target.anchoredPosition;
            elapsed = 0f;
        }

        private void OnDisable()
        {
            if (target != null) target.anchoredPosition = origin;
        }

        // The results stay up while gameplay is paused, so the float uses real time.
        // The sine starts at zero, so the card begins exactly at its authored position.
        private void LateUpdate()
        {
            if (target == null) return;
            elapsed += Time.unscaledDeltaTime;
            float offset = Mathf.Sin(elapsed / Mathf.Max(0.5f, cycleSeconds) * Mathf.PI * 2f) * amplitude;
            target.anchoredPosition = origin + Vector2.up * offset;
        }
    }
}
