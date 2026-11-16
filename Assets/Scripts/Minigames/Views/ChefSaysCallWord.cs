using TMPro;
using UnityEngine;

namespace FoodIsekaiZ.Minigames.Views
{
    // The called direction ("LEFT!" / "RIGHT!") floating up on the chef's side just before she pops
    // out: it pops in past full size, rises, holds, then fades away. Every label in labels shows the
    // same words, so a darker copy behind the main one can act as its shadow.
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class ChefSaysCallWord : MonoBehaviour
    {
        [SerializeField] private TMP_Text[] labels;
        // How far the word climbs from where it is placed, in wall units.
        [SerializeField] private float rise = 150f;
        [SerializeField] private float seconds = 1.1f;
        [SerializeField] private float popSeconds = 0.18f;
        [SerializeField] private float popOvershoot = 1.25f;
        // Share of the time the word stays fully visible before it starts fading.
        [SerializeField, Range(0f, 1f)] private float holdShare = 0.55f;

        private CanvasGroup group;
        private RectTransform rect;
        private Vector2 restingPosition;
        private float elapsed;
        private bool playing;

        private void Awake()
        {
            group = GetComponent<CanvasGroup>();
            rect = (RectTransform)transform;
            restingPosition = rect.anchoredPosition;
        }

        public void Play(string words)
        {
            foreach (TMP_Text label in labels) label.text = words;
            elapsed = 0f;
            playing = true;
            gameObject.SetActive(true);
            Draw();
        }

        public void HideImmediately()
        {
            playing = false;
            if (group == null) Awake();
            group.alpha = 0f;
            rect.anchoredPosition = restingPosition;
            rect.localScale = Vector3.one;
        }

        private void Update()
        {
            if (!playing) return;
            elapsed += Time.deltaTime;
            if (elapsed >= seconds)
            {
                HideImmediately();
                return;
            }

            Draw();
        }

        private void Draw()
        {
            float share = elapsed / seconds;
            float climb = 1f - (1f - share) * (1f - share);
            rect.anchoredPosition = restingPosition + Vector2.up * rise * climb;

            float scale;
            if (elapsed < popSeconds) scale = Mathf.Lerp(0.5f, popOvershoot, elapsed / popSeconds);
            else scale = Mathf.Lerp(popOvershoot, 1f, Mathf.Clamp01((elapsed - popSeconds) / popSeconds));
            rect.localScale = new Vector3(scale, scale, 1f);

            group.alpha = share <= holdShare ? 1f : 1f - (share - holdShare) / (1f - holdShare);
        }
    }
}
