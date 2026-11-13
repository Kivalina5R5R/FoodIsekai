using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Minigames
{
    // One player's line on the minigame result (prefab Assets/Prefab/Minigame/MinigameResult). The row's
    // look is set in the prefab; at runtime only the badge colour, the player's name, their words and
    // the pass or fail mark change.
    public sealed class MinigameResultRow : MonoBehaviour
    {
        [SerializeField] private Image badge;
        [SerializeField] private TMP_Text playerName;
        [SerializeField] private TMP_Text words;
        [SerializeField] private TMP_Text mark;
        [SerializeField] private string passWord = "O";
        [SerializeField] private string failWord = "X";
        [SerializeField] private Color passColour = new Color(0.36f, 0.78f, 0.36f, 1f);
        [SerializeField] private Color failColour = new Color(0.9f, 0.27f, 0.2f, 1f);

        // playerWords is already in the game's language.
        public void Show(MinigamePlayer player, bool success, string playerWords)
        {
            if (badge != null) badge.color = player.Colour;
            playerName.text = player.Name;
            words.text = playerWords;
            mark.text = success ? passWord : failWord;
            mark.color = success ? passColour : failColour;
        }
    }
}
