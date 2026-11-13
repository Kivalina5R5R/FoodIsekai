using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace FoodIsekaiZ.Minigames
{
    // The result card after every minigame (prefab Assets/Prefab/Minigame/MinigameResult, wall only).
    // Everything you see is set in the prefab: the shade, the card, the verdict, the coin line with
    // its pile and note, the "no coins" line, and the player rows cloned from the row template into
    // the Rows group, whose layout group stacks them. The words for each verdict and note are fields
    // here in both languages. At runtime the view only fills in text, picks the verdict colour, shows
    // the coin line or the no-coins line, and moves the card to cardAlonePosition when there are no
    // player rows.
    public sealed class MinigameResultView : MinigameView
    {
        [SerializeField] private RectTransform card;
        [SerializeField] private Vector2 cardAlonePosition = Vector2.zero;
        [SerializeField] private TMP_Text verdict;
        [SerializeField] private GameObject coinLine;
        [SerializeField] private TMP_Text coinWords;
        [SerializeField] private TMP_Text note;
        [SerializeField] private TMP_Text noCoinWords;
        [SerializeField] private RectTransform rows;
        [SerializeField] private MinigameResultRow rowTemplate;

        [SerializeField] private Color perfectColour = new Color(0.78f, 0.45f, 0.2f, 1f);
        [SerializeField] private Color clearColour = new Color(0.24f, 0.52f, 0.2f, 1f);
        [SerializeField] private Color missColour = new Color(0.59f, 0.12f, 0.15f, 1f);

        [SerializeField] private string perfectEnglish = "PERFECT!";
        [SerializeField] private string perfectThai = "เพอร์เฟกต์!";
        [SerializeField] private string clearEnglish = "CLEAR!";
        [SerializeField] private string clearThai = "ผ่าน!";
        [SerializeField] private string missEnglish = "MISSED";
        [SerializeField] private string missThai = "พลาด";
        // {0} is the number of coins.
        [SerializeField] private string coinsEnglish = "+{0} coins";
        [SerializeField] private string coinsThai = "+{0} เหรียญ";
        [SerializeField] private string noCoinsEnglish = "No coins";
        [SerializeField] private string noCoinsThai = "ไม่ได้​เหรียญ";
        [SerializeField] private string perkNoteEnglish = "Spend them on perks next!";
        [SerializeField] private string perkNoteThai = "เอา​ไป​ซื้อ​เพิร์ค​ต่อ​ได้​เลย!";
        [SerializeField] private string bonusNoteEnglish = "Coins become bonus score!";
        [SerializeField] private string bonusNoteThai = "เหรียญ​จะ​กลาย​เป็น​คะแนน​โบนัส!";

        // finalRound: after the last meal the coins become bonus score instead of perk money.
        public void Show(Minigame game, int coins, bool finalRound)
        {
            bool perfect = game.EveryonePassed;
            bool passed = game.Passed;
            verdict.text = perfect ? Pick(perfectEnglish, perfectThai) : passed ? Pick(clearEnglish, clearThai) : Pick(missEnglish, missThai);
            verdict.color = perfect ? perfectColour : passed ? clearColour : missColour;

            coinLine.SetActive(coins > 0);
            noCoinWords.gameObject.SetActive(coins <= 0);
            noCoinWords.text = Pick(noCoinsEnglish, noCoinsThai);
            if (coins > 0)
            {
                coinWords.text = string.Format(Pick(coinsEnglish, coinsThai), coins);
                note.text = finalRound ? Pick(bonusNoteEnglish, bonusNoteThai) : Pick(perkNoteEnglish, perkNoteThai);
            }

            IReadOnlyList<PlayerVerdict> verdicts = game.Verdicts;
            rows.gameObject.SetActive(verdicts.Count > 0);
            if (verdicts.Count == 0) card.anchoredPosition = cardAlonePosition;
            foreach (PlayerVerdict each in verdicts)
            {
                MinigameResultRow row = Spawn(rowTemplate);
                row.Show(each.Player, each.Success, Pick(each.English, each.Thai));
            }
        }

        private static string Pick(string english, string thai)
        {
            return Localization.GameLanguage.Pick(english, thai);
        }
    }
}
