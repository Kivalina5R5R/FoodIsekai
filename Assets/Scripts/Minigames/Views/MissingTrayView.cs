using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Minigames.Views
{
    // What's Missing? on the wall and floor (prefab Assets/Prefab/Minigame/MissingTray).
    // Wall: the status words, the tray, the full row of dishes, the cover that drops over it and the
    // short row (one dish gone) that comes back. Rows are cloned from the wall dish template and
    // spaced slotSpacing apart (the short row spreads by shortSpread).
    // Floor: one MissingTrayTile per answer, and a dot in the player's colour per locked answer.
    // The dishes' looks live in the dishes list, found by key.
    public sealed class MissingTrayView : MinigameView
    {
        [SerializeField] private TMP_Text status;
        [SerializeField] private RectTransform trayFull;
        [SerializeField] private RectTransform trayShort;
        [SerializeField] private Image cover;
        [SerializeField] private MissingTrayDishSlot wallDishTemplate;
        [SerializeField] private float slotSpacing = 142f;
        [SerializeField] private float shortSpread = 1.15f;
        [SerializeField] private MissingTrayTile tileTemplate;
        [SerializeField] private Image answerDotTemplate;
        [SerializeField] private List<MissingTrayDishArt> dishes = new List<MissingTrayDishArt>();

        private readonly List<MissingTrayTile> tiles = new List<MissingTrayTile>();

        protected override void OnBound()
        {
            trayFull.gameObject.SetActive(true);
            trayShort.gameObject.SetActive(false);
            cover.gameObject.SetActive(false);
        }

        public void SetStatus(string words)
        {
            status.text = words;
        }

        // The tray as first shown; keys and names in tray order.
        public void FillFullTray(IReadOnlyList<string> keys, IReadOnlyList<string> names)
        {
            FillRow(trayFull, keys, names, 1f);
        }

        // The tray after the cover lifts, with one dish gone.
        public void FillShortTray(IReadOnlyList<string> keys, IReadOnlyList<string> names)
        {
            FillRow(trayShort, keys, names, shortSpread);
        }

        // An answer tile centred on arena point centre, holding this dish.
        public void AddTile(Vector2 centre, string key, string name)
        {
            MissingTrayTile tile = Spawn(tileTemplate);
            PlaceOnFloor((RectTransform)tile.transform, centre);
            tile.Show(ArtOf(key), name);
            tiles.Add(tile);
        }

        public void CoverTray()
        {
            trayFull.gameObject.SetActive(false);
            cover.gameObject.SetActive(true);
        }

        // The cover lifts on the short tray and the tiles light up for answers.
        public void RevealShortTray()
        {
            cover.gameObject.SetActive(false);
            trayShort.gameObject.SetActive(true);
            foreach (MissingTrayTile tile in tiles) tile.Brighten();
        }

        // A dot in this colour at arena point spot.
        public void MarkAnswer(Color colour, Vector2 spot)
        {
            Image dot = Spawn(answerDotTemplate);
            dot.color = colour;
            PlaceOnFloor(dot.rectTransform, spot);
        }

        private void FillRow(RectTransform row, IReadOnlyList<string> keys, IReadOnlyList<string> names, float spread)
        {
            float y = wallDishTemplate.GetComponent<RectTransform>().anchoredPosition.y;
            for (int i = 0; i < keys.Count; i++)
            {
                MissingTrayDishSlot slot = Spawn(wallDishTemplate, row);
                ((RectTransform)slot.transform).anchoredPosition = new Vector2((i - (keys.Count - 1) * 0.5f) * slotSpacing * spread, y);
                slot.Show(ArtOf(keys[i]), names[i]);
            }
        }

        private MissingTrayDishArt ArtOf(string key)
        {
            foreach (MissingTrayDishArt art in dishes)
                if (art.Key == key) return art;
            Debug.LogWarning($"[Minigames] MissingTray has no art for dish '{key}'", this);
            return new MissingTrayDishArt(key, null, Color.white);
        }
    }
}
