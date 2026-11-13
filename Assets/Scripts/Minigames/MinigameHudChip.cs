using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Minigames
{
    // One player's chip on the HUD. The panel, the name's font and colour and the layout are all set in
    // the MinigameHud prefab; at runtime only the live data changes: the badge takes the player's
    // colour (grey once out), the name shows the player, and the mark shows nothing while playing,
    // the safe word when through and the out word when out.
    public sealed class MinigameHudChip : MonoBehaviour
    {
        [SerializeField] private Image badge;
        [SerializeField] private TMP_Text playerName;
        [SerializeField] private TMP_Text mark;
        [SerializeField] private string safeWord = "O";
        [SerializeField] private string outWord = "X";
        [SerializeField] private Color safeColour = new Color(0.24f, 0.52f, 0.2f, 1f);
        [SerializeField] private Color outColour = new Color(0.59f, 0.12f, 0.15f, 1f);
        [SerializeField] private Color outGrey = new Color(0.62f, 0.58f, 0.54f, 1f);

        public void Show(MinigamePlayer player, PlayerMark state)
        {
            bool isOut = state == PlayerMark.Out;
            if (badge != null) badge.color = isOut ? outGrey : player.Colour;
            playerName.text = player.Name;
            mark.text = isOut ? outWord : state == PlayerMark.Safe ? safeWord : "";
            mark.color = isOut ? outColour : safeColour;
        }
    }
}
