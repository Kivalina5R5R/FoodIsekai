using FoodIsekaiZ.Minigames.Views;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Minigames.EditorTools
{
    // Starting layout of the Fry It Golden prefab (Fry): see FryView.
    public sealed class FryRecipe : IMinigamePrefabRecipe
    {
        // The game's pan zone: centre (5.5, 2.75 m) on the 11 x 5 m floor is (0, -25) on the floor canvas.
        private static readonly Vector2 FloorPan = new Vector2(0f, -25f);
        private const float PanDiameter = 280f;

        public string Name => "Fry";

        public void Build()
        {
            MinigamePrefabKit kit = MinigamePrefabKit.Begin(out FryView view);
            Sprite pan = MinigamePrefabKit.Picture("FryPan");
            Sprite piece = MinigamePrefabKit.Picture("FryPiece");

            // The floor pan is just the pan picture, with no ring or backdrop on top; its oil zone
            // child is what counts as "in the pan". A plain dark circle stands in until the picture exists.
            RectTransform zone;
            if (pan != null)
            {
                float width = PanDiameter * 2.75f / 2f;
                Image picture = kit.Shape("Pan Picture", kit.Floor, pan, Color.white, FloorPan + new Vector2(30f, 0f),
                    new Vector2(width, width * pan.rect.height / pan.rect.width));
                zone = AddOilZone(picture.rectTransform);
            }
            else
            {
                Image plain = kit.Shape("Pan", kit.Floor, MinigamePrefabBuilder.Circle, new Color(0.2f, 0.16f, 0.14f, 0.85f), FloorPan, Vector2.one * PanDiameter);
                zone = AddZone(plain.rectTransform, Vector2.zero, Vector2.one);
            }

            kit.Bind(view, "panZone", zone);

            kit.Shape("Pan Picture", kit.Wall, pan != null ? pan : MinigamePrefabBuilder.Circle, pan != null ? Color.white : new Color(0.18f, 0.14f, 0.12f, 1f),
                new Vector2(0f, -14f), new Vector2(440f, 420f));
            TMP_Text status = kit.Label("Status", kit.Wall, "Into the pan!", 46f, MinigameArt.Cream, new Vector2(-520f, 10f), new Vector2(430f, 220f));

            // The right of the wall: the three stages of a piece, so players know which one to step out on.
            TMP_Text guideTitle = kit.Label("Guide Title", kit.Wall, "Step out at", 30f, MinigameArt.Gold, new Vector2(520f, 120f), new Vector2(420f, 40f));
            TMP_Text raw = GuidePiece(kit, piece, 380f, 0.1f, "Raw", MinigameArt.Bad);
            TMP_Text golden = GuidePiece(kit, piece, 520f, (FryCooking.GoldenFrom + FryCooking.BurntFrom) * 0.5f, "Golden!", MinigameArt.Good);
            TMP_Text burnt = GuidePiece(kit, piece, 660f, 1.04f, "Burnt", MinigameArt.Bad);

            FryPieceSlot slot = BuildPiece(kit, piece);

            kit.Bind(view, "status", status);
            kit.Bind(view, "guideTitle", guideTitle);
            kit.Bind(view, "guideRaw", raw);
            kit.Bind(view, "guideGolden", golden);
            kit.Bind(view, "guideBurnt", burnt);
            kit.Bind(view, "pieceTemplate", slot);
            kit.Save(Name);
        }

        // The oil surface inside FryPan.png, in its pixels (1015 x 976, from the top-left): an ellipse
        // centred at (480, 560) with half-widths 412 x 288. The iron rim and handles are outside it.
        private static readonly Vector2 PanPictureSize = new Vector2(1015f, 976f);
        private static readonly Vector2 OilCentre = new Vector2(480f, 560f);
        private static readonly Vector2 OilRadii = new Vector2(412f, 288f);

        // Adds the oil zone under the floor pan picture, anchored to the oil so it follows the picture
        // when it is moved or resized. The picture's box must keep the picture's own shape.
        public static RectTransform AddOilZone(RectTransform panPicture)
        {
            Vector2 min = new Vector2((OilCentre.x - OilRadii.x) / PanPictureSize.x, 1f - (OilCentre.y + OilRadii.y) / PanPictureSize.y);
            Vector2 max = new Vector2((OilCentre.x + OilRadii.x) / PanPictureSize.x, 1f - (OilCentre.y - OilRadii.y) / PanPictureSize.y);
            return AddZone(panPicture, min, max);
        }

        // An empty "Oil Zone" spanning anchors min..max of parent; nothing is drawn for it.
        private static RectTransform AddZone(RectTransform parent, Vector2 min, Vector2 max)
        {
            var zoneObject = new GameObject("Oil Zone", typeof(RectTransform));
            zoneObject.layer = parent.gameObject.layer;
            var zone = (RectTransform)zoneObject.transform;
            zone.SetParent(parent, false);
            zone.anchorMin = min;
            zone.anchorMax = max;
            zone.offsetMin = Vector2.zero;
            zone.offsetMax = Vector2.zero;
            return zone;
        }

        private static TMP_Text GuidePiece(MinigamePrefabKit kit, Sprite picture, float x, float cooked, string words, Color wordColour)
        {
            Color colour = FryCooking.DefaultColour(cooked);
            kit.Shape("Guide Piece", kit.Wall, picture != null ? picture : MinigamePrefabBuilder.Circle,
                picture != null ? MinigameArt.RelativeTint(colour, FryCooking.DefaultRaw) : colour, new Vector2(x, 20f), new Vector2(104f, 104f));
            return kit.Label("Guide Words", kit.Wall, words, 30f, wordColour, new Vector2(x, -60f), new Vector2(140f, 40f));
        }

        // One player's piece, centred where the view places it.
        private static FryPieceSlot BuildPiece(MinigamePrefabKit kit, Sprite picture)
        {
            RectTransform group = kit.Group("Piece", kit.Wall, Vector2.zero, new Vector2(120f, 140f));
            var slot = group.gameObject.AddComponent<FryPieceSlot>();
            Image ring = kit.Shape("Ring", group, MinigamePrefabBuilder.Circle, Color.white, Vector2.zero, new Vector2(108f, 108f));
            Image food = kit.Shape("Food", group, picture != null ? picture : MinigamePrefabBuilder.Circle,
                picture != null ? Color.white : FryCooking.DefaultRaw, Vector2.zero, new Vector2(94f, 94f));

            kit.Shape("Meter Back", group, null, new Color(0.08f, 0.04f, 0.02f, 0.9f), new Vector2(0f, -60f), new Vector2(100f, 13f));
            Color window = FryCooking.DefaultGolden;
            window.a = 0.75f;
            float windowCentre = ((FryCooking.GoldenFrom + FryCooking.BurntFrom) * 0.5f / FryCooking.CharredAt - 0.5f) * 100f;
            kit.Shape("Golden Window", group, null, window, new Vector2(windowCentre, -60f),
                new Vector2(100f * (FryCooking.BurntFrom - FryCooking.GoldenFrom) / FryCooking.CharredAt, 13f));
            RectTransform track = kit.Group("Meter Track", group, new Vector2(0f, -60f), new Vector2(100f, 5f));
            Image meter = kit.Shape("Meter", track, null, MinigameArt.Cream, Vector2.zero, new Vector2(0f, 5f));
            MinigamePrefabKit.PinLeft(meter.rectTransform, 0f);

            kit.Shape("Name Chip", group, null, new Color(0.1f, 0.05f, 0.02f, 0.85f), new Vector2(0f, 54f), new Vector2(58f, 26f));
            TMP_Text name = kit.Label("Name", group, "P1", 22f, Color.white, new Vector2(0f, 54f), new Vector2(58f, 26f));

            kit.Bind(slot, "ring", ring);
            kit.Bind(slot, "food", food);
            kit.Bind(slot, "meter", meter.rectTransform);
            kit.Bind(slot, "playerName", name);
            SetFoodIsPicture(slot, picture != null);
            return MinigamePrefabKit.Template(slot);
        }

        // A picture is tinted relative to raw; the plain circle takes the colour directly.
        private static void SetFoodIsPicture(FryPieceSlot slot, bool isPicture)
        {
            var serialized = new SerializedObject(slot);
            serialized.FindProperty("foodIsPicture").boolValue = isPicture;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
