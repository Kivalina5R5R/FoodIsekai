using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Minigames.Views
{
    // Order Up! on the wall and floor (prefab Assets/Prefab/Minigame/OrderUp).
    // Floor: the crates, each with its number and hidden dish; standing on one opens it right there.
    // Where the crates sit and how big they are is also where players stand to open them, so moving
    // a crate in the prefab moves its zone.
    // Wall: only what to find. The customer, her order panel with one spot per ordered dish (a dish
    // and a row of found dots, one per team, cloned from templates), the time left and the teams.
    // The dishes' pictures are kept here by key (the picture's name in Assets/Art/Food).
    public sealed class OrderUpView : MinigameView
    {
        [SerializeField] private OrderUpCrate[] floorCrates;
        [SerializeField] private RectTransform[] orderSpots;
        [SerializeField] private OrderUpDish orderDishTemplate;
        [SerializeField] private Image foundDotTemplate;
        [SerializeField] private float foundDotSpacing = 44f;
        [SerializeField] private string[] dishKeys;
        [SerializeField] private Sprite[] dishPictures;
        [SerializeField] private Color missingPictureTint = Color.grey;
        [SerializeField] private TMP_Text teams;
        [SerializeField] private TMP_Text clock;

        private readonly Dictionary<(int, int), Image> foundDots = new Dictionary<(int, int), Image>();

        public int CrateCount => floorCrates.Length;

        public int OrderCount => orderSpots.Length;

        // A crate's centre on the floor, in arena metres (x from the left, z from the wall).
        public Vector2 CrateCentre(int crate)
        {
            Vector2 point = ((RectTransform)floorCrates[crate].transform).anchoredPosition / MinigameStage.UnitsPerMetre;
            Vector2 arena = Stage.ArenaSize;
            return new Vector2(point.x + arena.x * 0.5f, arena.y * 0.5f - point.y);
        }

        // Half the crate's width, in metres.
        public float CrateRadius(int crate)
        {
            return ((RectTransform)floorCrates[crate].transform).sizeDelta.x * 0.5f / MinigameStage.UnitsPerMetre;
        }

        // Puts the dish with this key, named name, inside this floor crate (closed).
        public void FillCrate(int crate, string key, string name)
        {
            ShowDish(floorCrates[crate].Dish, key, name);
            SetCrateOpen(crate, false);
        }

        public void SetCrateOpen(int crate, bool open)
        {
            floorCrates[crate].SetOpen(open);
        }

        // Shows the ordered dish in order spot spot, with a dim found dot per team under it.
        // teamCount is the real number of teams (0 with nobody playing), dotCount how many dots to draw.
        public void ShowOrder(int spot, string key, string name, int teamCount, int dotCount)
        {
            RectTransform parent = orderSpots[spot];
            ShowDish(Spawn(orderDishTemplate, parent), key, name);
            Vector2 dotHome = foundDotTemplate.rectTransform.anchoredPosition;
            for (int team = 0; team < dotCount; team++)
            {
                Image dot = Spawn(foundDotTemplate, parent);
                float x = (team - (teamCount - 1) * 0.5f) * foundDotSpacing;
                dot.rectTransform.anchoredPosition = dotHome + new Vector2(x, 0f);
                foundDots[(team, spot)] = dot;
            }
        }

        // Lights team's dot under order spot spot in the team's colour.
        public void MarkFound(int team, int spot, Color colour)
        {
            if (foundDots.TryGetValue((team, spot), out Image dot)) dot.color = colour;
        }

        // text may use rich-text colour tags for the team names.
        public void SetTeams(string text)
        {
            teams.text = text;
        }

        public void SetClock(string text)
        {
            clock.text = text;
        }

        private void ShowDish(OrderUpDish dish, string key, string name)
        {
            int index = System.Array.IndexOf(dishKeys, key);
            Sprite picture = index >= 0 && index < dishPictures.Length ? dishPictures[index] : null;
            dish.Show(picture, picture != null ? Color.white : missingPictureTint, name);
        }
    }
}
