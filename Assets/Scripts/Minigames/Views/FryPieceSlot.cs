using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Minigames.Views
{
    // One player's piece in the Fry It Golden pan on the wall: a ring and name chip in their colour,
    // the piece itself tinted by how cooked it is, and a doneness meter under it with the golden
    // window marked. A piece picture is painted raw, so it is tinted relative to raw; the plain
    // circle fallback takes the colour directly.
    public sealed class FryPieceSlot : MonoBehaviour
    {
        [SerializeField] private Image ring;
        [SerializeField] private Image food;
        // Pinned at its left edge; its width grows to meterWidth as the piece chars.
        [SerializeField] private RectTransform meter;
        [SerializeField] private float meterWidth = 100f;
        [SerializeField] private TMP_Text playerName;
        [SerializeField] private bool foodIsPicture = true;
        [SerializeField] private Color raw = new Color(0.98f, 0.93f, 0.78f, 1f);
        [SerializeField] private Color golden = new Color(1f, 0.74f, 0.2f, 1f);
        [SerializeField] private Color brown = new Color(0.62f, 0.34f, 0.12f, 1f);
        [SerializeField] private Color burnt = new Color(0.16f, 0.1f, 0.07f, 1f);

        public void Setup(MinigamePlayer player)
        {
            ring.color = player.Colour;
            playerName.text = player.Name;
            playerName.color = player.Colour;
            SetMeter(0f);
        }

        public void ShowCooked(float cooked)
        {
            Color colour = FryCooking.Colour(cooked, raw, golden, brown, burnt);
            food.color = foodIsPicture ? MinigameArt.RelativeTint(colour, raw) : colour;
            SetMeter(cooked / FryCooking.CharredAt);
        }

        private void SetMeter(float share)
        {
            meter.sizeDelta = new Vector2(meterWidth * Mathf.Clamp01(share), meter.sizeDelta.y);
        }
    }
}
