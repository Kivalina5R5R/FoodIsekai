using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Minigames.EditorTools
{
    // Starting layout of the shared HUD prefab (MinigameHud).
    public sealed class MinigameHudRecipe : IMinigamePrefabRecipe
    {
        private static readonly Color DarkPanel = new Color(0.16f, 0.09f, 0.04f, 0.92f);

        public string Name => "MinigameHud";

        public void Build()
        {
            MinigamePrefabKit kit = MinigamePrefabKit.BeginWallOnly(out MinigameHudView view);
            Vector2 half = MinigamePrefabKit.WallSize * 0.5f;
            const float margin = 16f;
            const float chipHeight = 52f;
            float topY = half.y - margin - chipHeight * 0.5f;

            Image titleChip = Panel(kit, "Title Chip", kit.Wall, new Vector2(-half.x + margin + 235f, topY), new Vector2(470f, chipHeight));
            TMP_Text title = kit.Label("Title", titleChip.rectTransform, "CHEF SAYS", 32f, MinigameArt.Gold, new Vector2(-155f, 0f), new Vector2(140f, 44f));
            TMP_Text rule = kit.Label("Rule", titleChip.rectTransform, "One-line rule", 24f, MinigameArt.Cream, new Vector2(75f, 0f), new Vector2(300f, 44f));
            rule.alignment = TextAlignmentOptions.Left;
            // The name and rule chip is switched off: the wall shows no title while a game plays.
            titleChip.gameObject.SetActive(false);

            // The counter sits at the bottom middle of the wall.
            Image counter = Banner(kit, "Counter", kit.Wall, new Vector2(0f, -half.y + margin + chipHeight * 0.5f), new Vector2(220f, chipHeight));
            RectTransform pips = kit.Group("Pips", counter.rectTransform, Vector2.zero, new Vector2(140f, 20f));
            Row(pips, 8f);
            Image pip = MinigamePrefabKit.Template(kit.Shape("Pip", pips, MinigamePrefabBuilder.Circle, MinigameArt.Brown, Vector2.zero, new Vector2(14f, 14f)));
            MinigamePrefabKit.HideAtStart(counter.gameObject);

            // The player chips run along the top left, where the prefab places them.
            RectTransform players = kit.Group("Players", kit.Wall, new Vector2(-669f, 175f), new Vector2(1000f, 52f));
            Row(players, 16f);
            // A cream banner per player: a badge in the player's colour with their name, then the mark.
            Image chipPanel = Banner(kit, "Player Chip", players, Vector2.zero, new Vector2(150f, 52f));
            var chip = chipPanel.gameObject.AddComponent<MinigameHudChip>();
            Image badge = kit.Shape("Badge", chipPanel.rectTransform, MinigamePrefabBuilder.Circle, MinigameArt.PlayerColour(1), new Vector2(-30f, 0f), new Vector2(40f, 40f));
            TMP_Text chipName = kit.Label("Name", badge.rectTransform, "P1", 20f, MinigameArt.Brown, Vector2.zero, new Vector2(40f, 36f));
            TMP_Text chipMark = kit.Label("Mark", chipPanel.rectTransform, "", 30f, MinigameArt.PanelGreen, new Vector2(28f, 0f), new Vector2(44f, 44f));
            kit.Bind(chip, "badge", badge);
            kit.Bind(chip, "playerName", chipName);
            kit.Bind(chip, "mark", chipMark);
            MinigamePrefabKit.Template(chip);
            MinigamePrefabKit.HideAtStart(players.gameObject);

            kit.Bind(view, "title", title);
            kit.Bind(view, "rule", rule);
            kit.Bind(view, "counter", counter.gameObject);
            kit.Bind(view, "pipTemplate", pip);
            kit.Bind(view, "playerRow", players);
            kit.Bind(view, "chipTemplate", chip);
            kit.Save(Name);
        }

        // The dark capsule: the HudPanel picture, or a plain dark pill until it is added.
        private static Image Panel(MinigamePrefabKit kit, string name, RectTransform parent, Vector2 position, Vector2 size)
        {
            Sprite picture = MinigamePrefabKit.Picture("HudPanel");
            return kit.Shape(name, parent, picture != null ? picture : MinigamePrefabBuilder.Pill,
                picture != null ? Color.white : DarkPanel, position, size);
        }

        // The cream banner with a star at each pointed end (ThemeBanner, the transitions' text box),
        // 9-sliced so the ends keep their shape; a plain cream pill until the picture is added.
        private static Image Banner(MinigamePrefabKit kit, string name, RectTransform parent, Vector2 position, Vector2 size)
        {
            Sprite picture = MinigamePrefabKit.Picture("ThemeBanner");
            Image banner = kit.Shape(name, parent, picture != null ? picture : MinigamePrefabBuilder.Pill,
                picture != null ? Color.white : MinigameArt.Cream, position, size);
            banner.type = Image.Type.Sliced;
            banner.preserveAspect = false;
            if (picture != null) banner.pixelsPerUnitMultiplier = picture.rect.height / size.y;
            return banner;
        }

        // Lays the children out side by side, centred, at their own sizes.
        private static void Row(RectTransform rect, float spacing)
        {
            var row = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = spacing;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = false;
            row.childControlHeight = false;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
        }
    }
}
