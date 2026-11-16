using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Minigames
{
    // The runner's cards on the wall, drawn over the minigame on show: the big command word before
    // the game, nothing while it plays (the game's HUD has its name; time left is on the meal clock) and the result
    // card with coins and each player's verdict.
    // Words are in the game's language at the moment a card is shown.
    public sealed class MinigameOverlay
    {
        private readonly MinigameStage stage;
        private readonly RectTransform backdrop;
        private readonly RectTransform layer;
        private RectTransform card;

        public MinigameOverlay(MinigameStage stage)
        {
            this.stage = stage;
            backdrop = stage.Group("Minigame Backdrop", stage.Wall);
            backdrop.SetAsFirstSibling();
            stage.Shape("Shade", backdrop, MinigameArt.Square, MinigameArt.WallShade, Vector2.zero, new Vector2(1536f, 435f));
            layer = stage.Group("Minigame Cards", stage.Wall);
        }

        public void Hide()
        {
            Clear();
        }

        // The dark shade over the wall behind the games; off while the players choose, so the wall
        // stays clean like the perk shop.
        public void SetBackdrop(bool shown)
        {
            backdrop.gameObject.SetActive(shown);
        }

        // The command card in the game's cream style: the game's name on a small banner, the command
        // word on the wide banner the meal transitions use, and the rule underneath.
        public void ShowCommand(Minigame game)
        {
            Clear();
            stage.Shape("Shade", card, MinigameArt.Square, new Color(0.08f, 0.04f, 0.02f, 0.72f), Vector2.zero, new Vector2(1536f, 435f));
            Image nameBanner = Banner("Name Banner", card, new Vector2(0f, 145f), new Vector2(560f, 78f));
            stage.Label("Number", nameBanner.rectTransform, Pick(game.EnglishTitle, game.ThaiTitle), 32f, MinigameArt.PanelInk, Vector2.zero, new Vector2(420f, 50f));
            Image commandBanner = Banner("Command Banner", card, new Vector2(0f, 10f), new Vector2(1180f, 164f));
            stage.Label("Command", commandBanner.rectTransform, Pick(game.EnglishCommand, game.ThaiCommand), 118f, MinigameArt.PanelRed, new Vector2(0f, 4f), new Vector2(960f, 140f));
            stage.Label("Rule", card, Pick(game.EnglishRule, game.ThaiRule), 36f, MinigameArt.Cream, new Vector2(0f, -140f), new Vector2(1300f, 60f));
        }

        // One step of the 3, 2, 1 count-in between the command card and play: a big number dropping
        // into the middle of the wall over seconds, with the game's own drawings visible behind it.
        public void ShowCountIn(int number, float seconds)
        {
            Clear();
            TMP_Text label = stage.Label("Count In", card, number.ToString(), 260f, MinigameArt.Cream, Vector2.zero, new Vector2(420f, 340f));
            label.enableAutoSizing = false;
            label.outlineColor = MinigameArt.Brown;
            label.outlineWidth = 0.22f;
            label.gameObject.AddComponent<MinigameCountIn>().Play(label, seconds);
        }

        // The game's own HUD shows its name and rule while it plays, so the card is left empty.
        public void ShowPlaying(Minigame game)
        {
            Clear();
        }

        // The result card is the MinigameResult prefab (Assets/Prefab/Minigame), so its look is edited there.
        // finalRound: after the last meal the coins become bonus score instead of perk money.
        public void ShowResult(Minigame game, int coins, bool finalRound)
        {
            Clear();
            var result = stage.LoadView<MinigameResultView>("MinigameResult", card, null);
            if (result != null) result.Show(game, coins, finalRound);
        }

        // A cream banner (the transitions' text box) stretched to size; its pointed ends keep their
        // shape however wide it is. Falls back to a plain cream pill when the picture is missing.
        private Image Banner(string name, RectTransform parent, Vector2 position, Vector2 size)
        {
            Image banner = stage.Shape(name, parent, MinigameArt.Pill, MinigameArt.Cream, position, size);
            Sprite picture = MinigameArt.Picture("ThemeBanner");
            if (picture != null) SliceTo(banner, picture);
            else banner.type = Image.Type.Sliced;
            return banner;
        }

        // Draws picture 9-sliced on image, its borders scaled with the image's height.
        private static void SliceTo(Image image, Sprite picture)
        {
            image.sprite = picture;
            image.color = Color.white;
            image.preserveAspect = false;
            image.type = Image.Type.Sliced;
            float height = image.rectTransform.sizeDelta.y;
            if (height > 0f) image.pixelsPerUnitMultiplier = picture.rect.height / height;
        }

        // Removes the card on show and starts an empty one, on top of every minigame drawing.
        private void Clear()
        {
            if (card != null) Object.Destroy(card.gameObject);
            layer.SetAsLastSibling();
            card = stage.Group("Card", layer);
        }

        private static string Pick(string english, string thai)
        {
            return Localization.GameLanguage.Pick(english, thai);
        }
    }
}
