using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Minigames.EditorTools
{
    // Starting layout of the result card prefab (MinigameResult), in the end-of-game results' style:
    // the cream card on the left, the player rows stacked on the right.
    public sealed class MinigameResultRecipe : IMinigamePrefabRecipe
    {
        private const float RowHeight = 54f;

        public string Name => "MinigameResult";

        public void Build()
        {
            MinigamePrefabKit kit = MinigamePrefabKit.BeginWallOnly(out MinigameResultView view);
            kit.Shape("Shade", kit.Wall, null, new Color(0.08f, 0.04f, 0.02f, 0.6f), Vector2.zero, MinigamePrefabKit.WallSize);

            Sprite cardPicture = MinigamePrefabKit.Picture("ResultCard");
            Image card = kit.Shape("Card", kit.Wall, cardPicture, cardPicture != null ? Color.white : MinigameArt.Cream,
                new Vector2(-300f, 0f), new Vector2(500f, 385f));
            RectTransform inside = card.rectTransform;
            TMP_Text verdict = kit.Label("Verdict", inside, "MISSED", 92f, MinigameArt.PanelRed, new Vector2(0f, 50f), new Vector2(420f, 110f));

            // The coin pile, the coin count and the note show together when coins were won.
            RectTransform coinLine = kit.Group("Coin Line", inside, Vector2.zero, new Vector2(500f, 385f));
            Sprite pilePicture = MinigamePrefabKit.Picture("CoinPile");
            kit.Shape("Coin Pile", coinLine, pilePicture != null ? pilePicture : MinigamePrefabBuilder.Circle,
                pilePicture != null ? Color.white : MinigameArt.Gold, new Vector2(-140f, -52f), new Vector2(84f, 84f));
            TMP_Text coins = kit.Label("Coins", coinLine, "+15 coins", 42f, MinigameArt.PanelInk, new Vector2(40f, -52f), new Vector2(340f, 60f));
            TMP_Text note = kit.Label("Note", coinLine, "Spend them on perks next!", 24f, MinigameArt.Copper, new Vector2(0f, -112f), new Vector2(400f, 36f));
            TMP_Text noCoins = kit.Label("No Coins", inside, "No coins", 42f, MinigameArt.PanelInk, new Vector2(0f, -52f), new Vector2(340f, 60f));

            // The rows stack from the middle of this group, one per player.
            RectTransform rows = kit.Group("Rows", kit.Wall, new Vector2(330f, 0f), new Vector2(600f, 400f));
            var stack = rows.gameObject.AddComponent<VerticalLayoutGroup>();
            stack.spacing = 8f;
            stack.childAlignment = TextAnchor.MiddleCenter;
            stack.childControlWidth = false;
            stack.childControlHeight = false;
            stack.childForceExpandWidth = false;
            stack.childForceExpandHeight = false;

            Sprite rowPicture = MinigamePrefabKit.Picture("ResultRow");
            Image rowImage = kit.Shape("Row", rows, rowPicture != null ? rowPicture : MinigamePrefabBuilder.Pill,
                rowPicture != null ? Color.white : MinigameArt.Brown, Vector2.zero, new Vector2(600f, RowHeight));
            rowImage.type = Image.Type.Sliced;
            rowImage.preserveAspect = false;
            if (rowPicture != null) rowImage.pixelsPerUnitMultiplier = rowPicture.rect.height / RowHeight;
            var row = rowImage.gameObject.AddComponent<MinigameResultRow>();
            Image badge = kit.Shape("Badge", rowImage.rectTransform, MinigamePrefabBuilder.Circle, MinigameArt.PlayerColour(1), new Vector2(-250f, 0f), new Vector2(46f, 46f));
            TMP_Text playerName = kit.Label("Name", badge.rectTransform, "P1", 22f, MinigameArt.Brown, Vector2.zero, new Vector2(44f, 40f));
            TMP_Text words = kit.Label("Words", rowImage.rectTransform, "0/5", 28f, Color.white, new Vector2(10f, 0f), new Vector2(400f, 48f));
            TMP_Text mark = kit.Label("Mark", rowImage.rectTransform, "X", 34f, MinigameArt.Bad, new Vector2(250f, 0f), new Vector2(50f, 48f));
            kit.Bind(row, "badge", badge);
            kit.Bind(row, "playerName", playerName);
            kit.Bind(row, "words", words);
            kit.Bind(row, "mark", mark);
            MinigamePrefabKit.Template(row);

            kit.Bind(view, "card", inside);
            kit.Bind(view, "verdict", verdict);
            kit.Bind(view, "coinLine", coinLine.gameObject);
            kit.Bind(view, "coinWords", coins);
            kit.Bind(view, "note", note);
            kit.Bind(view, "noCoinWords", noCoins);
            kit.Bind(view, "rows", rows);
            kit.Bind(view, "rowTemplate", row);
            kit.Save(Name);
        }
    }
}
