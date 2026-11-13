using FoodIsekaiZ.Minigames.Views;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Minigames.EditorTools
{
    // Starting layout of the Perfect Grill prefab (Grill): see GrillView and GrillSteakColumn.
    public sealed class GrillRecipe : IMinigamePrefabRecipe
    {
        // The grill zone (GrillGame.GrillArea, 4.5 x 1.93 m centred 3.965 m from the wall) on the
        // 11 x 5 m floor; the game re-centres it on the zone at run time.
        private static readonly Vector2 FloorGrillCentre = new Vector2(0f, (2.5f - 3.965f) * MinigameStage.UnitsPerMetre);
        private static readonly Vector2 FloorGrillSize = new Vector2(4.5f, 1.93f) * MinigameStage.UnitsPerMetre;
        private const float BarWidth = 210f;

        public string Name => "Grill";

        public void Build()
        {
            MinigamePrefabKit kit = MinigamePrefabKit.Begin(out GrillView view);
            Sprite grate = MinigamePrefabKit.Picture("GrillGrate");

            // The grate picture, or a dark strip until it is added.
            Image floorGrill = kit.Shape("Grill", kit.Floor, grate, grate != null ? Color.white : new Color(0.2f, 0.15f, 0.13f, 0.85f),
                FloorGrillCentre, FloorGrillSize);

            // A grill across the bottom of the wall (its lower part below the wall); the steaks lie on it.
            kit.Shape("Grill Picture", kit.Wall, grate, grate != null ? Color.white : new Color(0.16f, 0.12f, 0.1f, 0.92f),
                new Vector2(0f, -170f), new Vector2(1000f, 429f));
            // Moved down from y 110 to 90 to clear the HUD title chip.
            TMP_Text status = kit.Label("Status", kit.Wall, "Onto the grill!", 34f, MinigameArt.Cream, new Vector2(-590f, 90f), new Vector2(330f, 110f));

            GrillSteakColumn column = BuildColumn(kit);

            kit.Bind(view, "floorGrill", floorGrill.rectTransform);
            kit.Bind(view, "status", status);
            kit.Bind(view, "columnTemplate", column);
            kit.Save(Name);
        }

        // One player's column, centred on x = 0; the game slides each copy sideways. The label and
        // the bar use the HUD's capsule picture (falling back to a plain pill) and a dark pill track.
        private static GrillSteakColumn BuildColumn(MinigamePrefabKit kit)
        {
            RectTransform group = kit.Group("Steak Column", kit.Wall, Vector2.zero, new Vector2(BarWidth + 30f, 300f));
            var column = group.gameObject.AddComponent<GrillSteakColumn>();

            Sprite panel = MinigamePrefabKit.Picture("HudPanel");
            Image label = kit.Shape("Label", group, panel != null ? panel : MinigamePrefabBuilder.Pill,
                panel != null ? Color.white : new Color(0.16f, 0.09f, 0.04f, 0.92f), new Vector2(0f, 128f), new Vector2(BarWidth + 30f, 42f));
            label.type = Image.Type.Sliced;
            label.preserveAspect = false;
            TMP_Text labelWords = kit.Label("Words", label.rectTransform, "P1  MEDIUM", 26f, MinigameArt.Cream, Vector2.zero, Vector2.zero);
            Fill(labelWords.rectTransform, new Vector2(18f, 4f));

            Sprite steak = MinigamePrefabKit.Picture("Steak");
            Image meat = steak != null
                ? kit.Shape("Steak", group, steak, Color.white, new Vector2(0f, -100f), new Vector2(132f, 132f))
                : kit.Shape("Steak", group, null, new Color(0.86f, 0.32f, 0.34f, 1f), new Vector2(0f, -100f), new Vector2(84f, 66f));

            Image bar = kit.Shape("Bar", group, MinigamePrefabBuilder.Pill, new Color(0.06f, 0.04f, 0.03f, 0.9f),
                new Vector2(0f, 84f), new Vector2(BarWidth, 22f));
            bar.type = Image.Type.Sliced;
            bar.preserveAspect = false;
            Image fill = kit.Shape("Fill", bar.rectTransform, MinigamePrefabBuilder.Pill, MinigameArt.Gold, Vector2.zero, new Vector2(BarWidth - 10f, 12f));
            MinigamePrefabKit.PinLeft(fill.rectTransform, 5f);
            Color green = MinigameArt.Good;
            green.a = 0.6f;
            Image window = kit.Shape("Target Window", bar.rectTransform, MinigamePrefabBuilder.Pill, green, Vector2.zero, new Vector2(60f, 16f));
            MinigamePrefabKit.PinLeft(window.rectTransform, 5f);

            TMP_Text seconds = kit.Label("Seconds", group, "0.0 s", 22f, MinigameArt.Cream, new Vector2(0f, 56f), new Vector2(BarWidth, 26f));
            TMP_Text result = kit.Label("Result", group, string.Empty, 34f, MinigameArt.Cream, new Vector2(0f, -100f), new Vector2(BarWidth + 30f, 44f));

            kit.Bind(column, "label", label.rectTransform);
            kit.Bind(column, "labelWords", labelWords);
            kit.Bind(column, "meat", meat);
            kit.Bind(column, "bar", bar.rectTransform);
            kit.Bind(column, "fill", fill.rectTransform);
            kit.Bind(column, "window", window.rectTransform);
            kit.Bind(column, "seconds", seconds);
            kit.Bind(column, "result", result);
            return MinigamePrefabKit.Template(column);
        }

        // Stretches rect over its parent, inset by padding on each side.
        private static void Fill(RectTransform rect, Vector2 padding)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = padding;
            rect.offsetMax = -padding;
        }
    }
}
