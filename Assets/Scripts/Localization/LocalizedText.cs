using TMPro;
using UnityEngine;

namespace FoodIsekaiZ.Localization
{
    // Swaps one authored TextMeshPro label between its English and Thai wording.
    // Thai mode can also switch to a Thai font asset; English mode restores the authored font.
    // Color and layout stay as authored on the label.
    // Strings and fonts are serialized so a runtime copy of the card keeps the correct English source.
    // With both strings empty, only the font changes; use that for labels whose text a script writes.
    public sealed class LocalizedText : MonoBehaviour
    {
        [SerializeField] private TMP_Text target;
        [SerializeField, TextArea] private string english;
        [SerializeField, TextArea] private string thai;
        // Leave the English font empty to use whatever font the label was authored with.
        [SerializeField] private TMP_FontAsset englishFont;
        [SerializeField] private TMP_FontAsset thaiFont;
        // Thai letters read smaller than Latin at the same point size, so Thai mode multiplies
        // the authored size and auto-size range by this amount; English mode restores them.
        [SerializeField, Min(0.1f)] private float thaiSizeScale = 1f;
        // Extra letter spacing for Thai mode, added to the authored spacing. TMP skips this on
        // positioned vowel and tone marks, so it opens up bold Thai without gaps after marks.
        [SerializeField] private float thaiCharacterSpacing;
        // Thin Thai strokes are hard to read at small sizes; this adds Bold in Thai mode only.
        [SerializeField] private bool thaiBold;
        // Moves the label in Thai mode only, for artwork the taller Thai text would otherwise overlap.
        [SerializeField] private Vector2 thaiOffset;
        private FontStyles authoredStyle;
        private Vector2 authoredPosition;
        private float authoredSize;
        private float authoredSizeMin;
        private float authoredSizeMax;
        private float authoredCharacterSpacing;

        private void Awake()
        {
            if (target == null) target = GetComponent<TMP_Text>();
            if (target == null) return;
            if (englishFont == null && target.font != thaiFont) englishFont = target.font;
            // A runtime copy made in Thai mode already carries the enlarged sizes; recover the authored ones.
            bool copiedInThai = thaiFont != null && thaiFont != englishFont && target.font == thaiFont;
            float restore = copiedInThai ? 1f / thaiSizeScale : 1f;
            authoredSize = target.fontSize * restore;
            authoredSizeMin = target.fontSizeMin * restore;
            authoredSizeMax = target.fontSizeMax * restore;
            authoredCharacterSpacing = target.characterSpacing - (copiedInThai ? thaiCharacterSpacing : 0f);
            authoredStyle = copiedInThai && thaiBold ? target.fontStyle & ~FontStyles.Bold : target.fontStyle;
            authoredPosition = target.rectTransform.anchoredPosition - (copiedInThai ? thaiOffset : Vector2.zero);
        }

        private void OnEnable()
        {
            GameLanguage.Changed += Apply;
            Apply(GameLanguage.Current);
        }

        private void OnDisable()
        {
            GameLanguage.Changed -= Apply;
        }

        private void Apply(GameLanguageId language)
        {
            if (target == null) return;
            bool thaiMode = language == GameLanguageId.Thai;
            TMP_FontAsset font = thaiMode && thaiFont != null ? thaiFont : englishFont;
            if (font != null && target.font != font) target.font = font;
            if (!Mathf.Approximately(thaiSizeScale, 1f))
            {
                float scale = thaiMode ? thaiSizeScale : 1f;
                target.fontSizeMin = authoredSizeMin * scale;
                target.fontSizeMax = authoredSizeMax * scale;
                target.fontSize = authoredSize * scale;
            }
            if (thaiBold) target.fontStyle = thaiMode ? authoredStyle | FontStyles.Bold : authoredStyle;
            if (thaiOffset != Vector2.zero)
                target.rectTransform.anchoredPosition = authoredPosition + (thaiMode ? thaiOffset : Vector2.zero);
            if (thaiCharacterSpacing != 0f)
                target.characterSpacing = authoredCharacterSpacing + (thaiMode ? thaiCharacterSpacing : 0f);
            if (string.IsNullOrEmpty(english)) return;
            string text = thaiMode && !string.IsNullOrEmpty(thai) ? thai : english;
            if (target.text != text) target.text = text;
        }
    }
}
