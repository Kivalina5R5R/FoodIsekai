using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Minigames.Views
{
    // One player's column on the Perfect Grill wall, in the HUD's style: a dark capsule label with
    // the player's name (in their colour) and their order, a framed time bar with its green target
    // window and a fill that grows from the left, the grill seconds, and the steak lying on the grill
    // below (no ring around it). The verdict shows over the steak once judged.
    // The template is authored at one bar width; Setup widens or narrows the label, the bar and the
    // words by the same amount when more players have to share the wall.
    // The steak goes raw -> cooked -> burnt; a picture is tinted relative to raw, a plain shape is
    // recoloured.
    public sealed class GrillSteakColumn : MonoBehaviour
    {
        [SerializeField] private RectTransform label;
        [SerializeField] private TMP_Text labelWords;
        [SerializeField] private Image meat;
        [SerializeField] private RectTransform bar;
        [SerializeField] private RectTransform fill;
        [SerializeField] private RectTransform window;
        [SerializeField] private TMP_Text seconds;
        [SerializeField] private TMP_Text result;
        [SerializeField] private Color raw = new Color(0.86f, 0.32f, 0.34f, 1f);
        [SerializeField] private Color cooked = new Color(0.55f, 0.3f, 0.16f, 1f);
        [SerializeField] private Color burnt = new Color(0.14f, 0.09f, 0.07f, 1f);
        [SerializeField] private Color successColour = new Color(0.36f, 0.78f, 0.36f, 1f);
        [SerializeField] private Color failColour = new Color(0.9f, 0.27f, 0.2f, 1f);

        private float fillInset;
        private float fullFill;

        // orderName is already in the game's language; windowLower and windowUpper are the green
        // window's edges as shares (0 -> 1) of the bar.
        public void Setup(MinigamePlayer player, string orderName, float barWidth, float windowLower, float windowUpper)
        {
            float change = barWidth - bar.sizeDelta.x;
            Widen(label, change);
            Widen(bar, change);
            Widen(seconds.rectTransform, change);
            Widen(result.rectTransform, change);

            // The fill and the window sit inside the frame, inset by the fill's authored left gap.
            fillInset = fill.anchoredPosition.x;
            fullFill = barWidth - fillInset * 2f;
            window.anchoredPosition = new Vector2(fillInset + fullFill * windowLower, window.anchoredPosition.y);
            window.sizeDelta = new Vector2(fullFill * (windowUpper - windowLower), window.sizeDelta.y);

            string colour = ColorUtility.ToHtmlStringRGB(player.Colour);
            labelWords.text = $"<color=#{colour}>{player.Name}</color>  {orderName}";
            result.text = string.Empty;
            ShowProgress(0f, 0f, 0f);
        }

        // barShare 0 -> 1 fills the bar; doneness 0 -> 1 browns the steak to cooked, 1 -> 2 burns it.
        public void ShowProgress(float barShare, float grillSeconds, float doneness)
        {
            float height = fill.sizeDelta.y;
            fill.sizeDelta = new Vector2(Mathf.Max(height, fullFill * Mathf.Clamp01(barShare)), height);
            fill.gameObject.SetActive(barShare > 0f);
            seconds.text = $"{grillSeconds:0.0} s";
            Color colour = doneness <= 1f
                ? Color.Lerp(raw, cooked, doneness)
                : Color.Lerp(cooked, burnt, doneness - 1f);
            meat.color = meat.sprite != null ? MinigameArt.RelativeTint(colour, raw) : colour;
        }

        public void ShowResult(string words, bool success)
        {
            result.text = words;
            result.color = success ? successColour : failColour;
        }

        private static void Widen(RectTransform rect, float change)
        {
            rect.sizeDelta = new Vector2(rect.sizeDelta.x + change, rect.sizeDelta.y);
        }
    }
}
