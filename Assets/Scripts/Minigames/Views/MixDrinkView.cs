using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Minigames.Views
{
    // Mix the Drink on the wall and floor (prefab Assets/Prefab/Minigame/MixDrink).
    // Wall: a dark backdrop, the shared glass with its liquid rising from the bottom of the bowl and
    // the 80% / 100% lines, the fill percentage under it, and to the right the recipe: its name, its
    // target colour, the jugs to pour with their parts (or a coloured ratio line while the jug
    // pictures are missing) and the status line.
    // Floor: one MixDrinkTap per tap, cloned from the template where the game puts the taps.
    // The three syrup colours and jug pictures (red, yellow, blue) are set here and also colour the
    // liquid, the target and the recipe.
    public sealed class MixDrinkView : MinigameView
    {
        [SerializeField] private MixDrinkTap tapTemplate;
        [SerializeField] private Color[] syrupColours =
        {
            new Color(0.9f, 0.2f, 0.15f, 1f),
            new Color(1f, 0.85f, 0.2f, 1f),
            new Color(0.2f, 0.45f, 0.9f, 1f),
        };
        [SerializeField] private Sprite[] jugPictures = new Sprite[3];

        [SerializeField] private RectTransform liquid;
        [SerializeField] private Image liquidColour;
        [SerializeField] private TMP_Text fillLabel;
        // The glass holds up to this much, so the 100% line sits below its rim.
        [SerializeField] private float glassHolds = 1.1f;
        [SerializeField, Range(0f, 1f)] private float liquidAlpha = 0.82f;
        [SerializeField] private Color fillingColour = new Color(0.99f, 0.91f, 0.78f, 1f);
        [SerializeField] private Color fullColour = new Color(0.36f, 0.78f, 0.36f, 1f);
        [SerializeField] private Color spillColour = new Color(0.9f, 0.27f, 0.2f, 1f);

        [SerializeField] private TMP_Text recipeName;
        [SerializeField] private Image targetColour;
        [SerializeField] private TMP_Text ratioLabel;
        [SerializeField] private MixDrinkRecipeJug jugTemplate;
        [SerializeField] private TMP_Text plusTemplate;
        [SerializeField] private float jugPitch = 240f;
        // Where a "+" sits after the gap before a jug, from that jug's centre.
        [SerializeField] private float plusOffset = -115f;

        [SerializeField] private TMP_Text status;
        [SerializeField] private Color servedColour = new Color(0.36f, 0.78f, 0.36f, 1f);
        [SerializeField] private Color failedColour = new Color(0.9f, 0.27f, 0.2f, 1f);

        private float fullHeight;

        protected override void OnBound()
        {
            fullHeight = liquid.sizeDelta.y;
            SetLiquidHeight(0f);
        }

        // A tap on the floor at this arena point, for syrup 0 red, 1 yellow, 2 blue.
        public void AddTap(Vector2 arena, int syrup)
        {
            MixDrinkTap tap = Spawn(tapTemplate);
            PlaceOnFloor((RectTransform)tap.transform, arena);
            tap.Setup(syrupColours[syrup], jugPictures[syrup]);
        }

        // name in the current language; parts per syrup; colourNames per syrup, for the ratio line.
        public void ShowRecipe(string name, IReadOnlyList<int> parts, IReadOnlyList<string> colourNames)
        {
            recipeName.text = name;
            targetColour.color = Mix(parts[0], parts[1], parts[2]);
            if (jugPictures[0] == null)
            {
                ratioLabel.gameObject.SetActive(true);
                ratioLabel.text = RatioText(parts, colourNames);
                return;
            }

            ratioLabel.gameObject.SetActive(false);
            var used = new List<int>();
            for (int i = 0; i < parts.Count; i++)
                if (parts[i] > 0) used.Add(i);
            for (int k = 0; k < used.Count; k++)
            {
                int syrup = used[k];
                float x = (k - (used.Count - 1) * 0.5f) * jugPitch;
                MixDrinkRecipeJug jug = Spawn(jugTemplate);
                RectTransform rect = (RectTransform)jug.transform;
                rect.anchoredPosition = new Vector2(x, rect.anchoredPosition.y);
                jug.Setup(jugPictures[syrup], parts[syrup], syrupColours[syrup]);
                if (k == 0) continue;
                TMP_Text plus = Spawn(plusTemplate);
                plus.rectTransform.anchoredPosition = new Vector2(x + plusOffset, plus.rectTransform.anchoredPosition.y);
            }
        }

        // poured: how much of each syrup is in the glass (1 = a full glass); fullFrom is the fill
        // from which the glass counts as full.
        public void ShowGlass(IReadOnlyList<float> poured, float fullFrom)
        {
            float fill = poured[0] + poured[1] + poured[2];
            SetLiquidHeight(Mathf.Clamp01(fill / glassHolds));
            if (fill > 0f)
            {
                Color mixed = Mix(poured[0], poured[1], poured[2]);
                mixed.a = liquidAlpha;
                liquidColour.color = mixed;
            }

            fillLabel.text = $"{Mathf.RoundToInt(fill * 100f)}%";
            fillLabel.color = fill > 1f ? spillColour : fill >= fullFrom ? fullColour : fillingColour;
        }

        public void ShowStatus(string words)
        {
            status.text = words;
        }

        public void ShowResult(string words, bool served)
        {
            status.text = words;
            status.color = served ? servedColour : failedColour;
        }

        // With the bowl-shaped liquid picture (a vertically filled Image) the liquid keeps the glass's
        // outline and only its fill rises; the plain box fallback grows its height instead.
        private void SetLiquidHeight(float share)
        {
            if (liquidColour.type == Image.Type.Filled)
            {
                liquidColour.fillAmount = share;
                return;
            }

            liquid.sizeDelta = new Vector2(liquid.sizeDelta.x, fullHeight * share);
        }

        // A watercolour-like mix: the syrup colours weighted by their amounts.
        private Color Mix(float red, float yellow, float blue)
        {
            float total = red + yellow + blue;
            if (total <= 0f) return Color.clear;
            Color mixed = (syrupColours[0] * red + syrupColours[1] * yellow + syrupColours[2] * blue) / total;
            mixed.a = 1f;
            return mixed;
        }

        private string RatioText(IReadOnlyList<int> parts, IReadOnlyList<string> colourNames)
        {
            var words = new List<string>();
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i] == 0) continue;
                string hex = ColorUtility.ToHtmlStringRGB(syrupColours[i]);
                words.Add($"<color=#{hex}>{colourNames[i]} {parts[i]}</color>");
            }

            return string.Join(" : ", words);
        }
    }
}
