using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Minigames
{
    // How a player stands in the minigame on show, drawn on their HUD chip.
    public enum PlayerMark
    {
        Playing,
        Safe,
        Out,
    }

    // The frame every minigame shares on the wall (prefab Assets/Prefab/Minigame/MinigameHud), kept clear
    // of the meal clock in the top middle: the game's name and one-line rule top-left, an optional
    // step counter top-right (just a row of dots on the cream banner), and an optional row of player chips along the bottom. The counter and
    // the chip row stay hidden until a game asks for them. Pips and chips are cloned from the inactive
    // templates and laid out by their rows' layout groups.
    public sealed class MinigameHudView : MinigameView
    {
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text rule;
        [SerializeField] private GameObject counter;
        [SerializeField] private Image pipTemplate;
        [SerializeField] private RectTransform playerRow;
        [SerializeField] private MinigameHudChip chipTemplate;
        [SerializeField] private Color pipDone = new Color(0.78f, 0.45f, 0.2f, 1f);
        [SerializeField] private Color pipNow = new Color(0.36f, 0.2f, 0.11f, 1f);
        [SerializeField] private Color pipLater = new Color(0.36f, 0.2f, 0.11f, 0.2f);

        private readonly List<Image> pips = new List<Image>();
        private readonly Dictionary<MinigamePlayer, MinigameHudChip> chips = new Dictionary<MinigamePlayer, MinigameHudChip>();

        public void SetTitle(string name, string oneLineRule)
        {
            title.text = name;
            rule.text = oneLineRule;
        }

        // Shows one dot per step top-right: done, the current one, and the ones still to come.
        // current counts from 1.
        public void ShowCounter(int current, int total)
        {
            counter.SetActive(true);
            while (pips.Count < total) pips.Add(Spawn(pipTemplate));
            for (int i = 0; i < pips.Count; i++)
            {
                pips[i].gameObject.SetActive(i < total);
                pips[i].color = i < current - 1 ? pipDone : i == current - 1 ? pipNow : pipLater;
            }
        }

        // A chip per player along the bottom, all starting as Playing.
        public void ShowPlayers(IReadOnlyList<MinigamePlayer> players)
        {
            playerRow.gameObject.SetActive(true);
            foreach (MinigamePlayer player in players)
            {
                if (chips.ContainsKey(player)) continue;
                MinigameHudChip chip = Spawn(chipTemplate);
                chip.Show(player, PlayerMark.Playing);
                chips[player] = chip;
            }
        }

        public void SetPlayer(MinigamePlayer player, PlayerMark mark)
        {
            if (chips.TryGetValue(player, out MinigameHudChip chip)) chip.Show(player, mark);
        }
    }
}
