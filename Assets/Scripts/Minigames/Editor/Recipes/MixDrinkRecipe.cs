using FoodIsekaiZ.Minigames.Views;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Minigames.EditorTools
{
    // Starting layout of the Mix the Drink prefab (MixDrink): see MixDrinkView.
    public sealed class MixDrinkRecipe : IMinigamePrefabRecipe
    {
        // The taps' drawn size in floor units (their 0.85 m play radius).
        private const float TapSize = 170f;
        private const float GlassHolds = 1.1f;
        private const float FullFrom = 0.8f;
        private static readonly Vector2 GlassCentre = new Vector2(-380f, -20f);
        private static readonly Vector2 GlassSize = new Vector2(200f, 330f);
        private static readonly string[] JugNames = { "SyrupRed", "SyrupYellow", "SyrupBlue" };

        public string Name => "MixDrink";

        public void Build()
        {
            MinigamePrefabKit kit = MinigamePrefabKit.Begin(out MixDrinkView view);
            MixDrinkTap tap = BuildTap(kit);

            kit.Shape("Backdrop", kit.Wall, null, MinigameArt.WallShade, new Vector2(0f, -10f), new Vector2(1460f, 400f));
            BuildGlass(kit, view);

            TMP_Text recipeName = kit.Label("Recipe", kit.Wall, "Yuzu Orange", 52f, MinigameArt.Gold, new Vector2(330f, 100f), new Vector2(620f, 74f));
            kit.Shape("Target Rim", kit.Wall, MinigamePrefabBuilder.Circle, MinigameArt.Cream, new Vector2(-30f, -10f), new Vector2(150f, 150f));
            Image target = kit.Shape("Target Colour", kit.Wall, MinigamePrefabBuilder.Circle, Color.white, new Vector2(-30f, -10f), new Vector2(134f, 134f));
            TMP_Text ratio = kit.Label("Ratio", kit.Wall, "Red 1 : Yellow 1", 40f, MinigameArt.Cream, new Vector2(330f, -10f), new Vector2(580f, 70f));
            BuildRecipeJugs(kit, view);
            TMP_Text status = kit.Label("Status", kit.Wall, "Pour, then step off to serve!", 34f, MinigameArt.Cream, new Vector2(230f, -140f), new Vector2(820f, 50f));

            kit.Bind(view, "tapTemplate", tap);
            kit.Bind(view, "recipeName", recipeName);
            kit.Bind(view, "targetColour", target);
            kit.Bind(view, "ratioLabel", ratio);
            kit.Bind(view, "status", status);
            BindJugPictures(view);
            kit.Save(Name);
        }

        // One tap, centred on its play circle: see-through pool, rim, jug picture.
        private static MixDrinkTap BuildTap(MinigamePrefabKit kit)
        {
            Vector2 size = new Vector2(TapSize, TapSize);
            RectTransform group = kit.Group("Tap", kit.Floor, Vector2.zero, size);
            var tap = group.gameObject.AddComponent<MixDrinkTap>();
            Image pool = kit.Shape("Pool", group, MinigamePrefabBuilder.Circle, new Color(1f, 1f, 1f, 0.5f), Vector2.zero, size);
            Image rim = kit.Shape("Rim", group, MinigamePrefabBuilder.Ring, Color.white, Vector2.zero, size);
            Sprite jugArt = MinigamePrefabKit.Picture(JugNames[0]);
            Image jug = kit.Shape("Jug", group, jugArt != null ? jugArt : MinigamePrefabBuilder.Circle, Color.white, Vector2.zero, size * 0.72f);
            jug.preserveAspect = true;

            kit.Bind(tap, "pool", pool);
            kit.Bind(tap, "rim", rim);
            kit.Bind(tap, "jug", jug);
            return MinigamePrefabKit.Template(tap);
        }

        // The shared glass: a picture (or a pale box) with the liquid rising from the bottom of its
        // bowl and the 80% and 100% lines across it. With the goblet picture the liquid fills only
        // the bowl, drawn over the glass.
        private static void BuildGlass(MinigamePrefabKit kit, MixDrinkView view)
        {
            Vector2 bowlCentre = GlassCentre;
            Vector2 inner = GlassSize - new Vector2(30f, 20f);
            Vector2 drawn = GlassSize;
            Sprite picture = MinigamePrefabKit.Picture("DrinkGlass");
            if (picture != null)
            {
                float aspect = picture.rect.width / picture.rect.height;
                drawn = aspect > GlassSize.x / GlassSize.y
                    ? new Vector2(GlassSize.x, GlassSize.x / aspect)
                    : new Vector2(GlassSize.y * aspect, GlassSize.y);
                inner = new Vector2(drawn.x * 0.55f, drawn.y * 0.5f);
                bowlCentre = GlassCentre + new Vector2(0f, drawn.y * 0.46f - inner.y * 0.5f);
            }

            kit.Shape("Glass", kit.Wall, picture, picture != null ? Color.white : new Color(1f, 1f, 1f, 0.16f), GlassCentre, GlassSize);

            // Grows upwards from the bowl's bottom as the glass fills. With the goblet picture the
            // liquid is the bowl-shaped DrinkGlassBowl picture, as wide as the drawn glass and
            // filled from the bottom, so it follows the glass's outline instead of a box.
            Sprite bowl = picture != null ? MinigamePrefabKit.Picture("DrinkGlassBowl") : null;
            Vector2 liquidSize = bowl != null ? new Vector2(drawn.x, inner.y) : inner;
            Image liquid = kit.Shape("Liquid", kit.Wall, bowl, Color.clear, bowlCentre, liquidSize);
            liquid.rectTransform.pivot = new Vector2(0.5f, 0f);
            liquid.rectTransform.anchoredPosition = bowlCentre - new Vector2(0f, inner.y * 0.5f);
            if (bowl != null)
            {
                liquid.type = Image.Type.Filled;
                liquid.fillMethod = Image.FillMethod.Vertical;
                liquid.fillOrigin = (int)Image.OriginVertical.Bottom;
                liquid.fillAmount = 0f;
            }

            Line(kit, bowlCentre, inner, FullFrom, "80%");
            Line(kit, bowlCentre, inner, 1f, "100%");
            TMP_Text fill = kit.Label("Fill", kit.Wall, "0%", 34f, MinigameArt.Cream,
                GlassCentre + new Vector2(0f, -GlassSize.y * 0.5f - 22f), new Vector2(200f, 40f));

            kit.Bind(view, "liquid", liquid.rectTransform);
            kit.Bind(view, "liquidColour", liquid);
            kit.Bind(view, "fillLabel", fill);
            kit.BindFloat(view, "glassHolds", GlassHolds);
        }

        private static void Line(MinigamePrefabKit kit, Vector2 centre, Vector2 inner, float level, string words)
        {
            float y = centre.y - inner.y * 0.5f + inner.y * level / GlassHolds;
            kit.Shape("Line " + words, kit.Wall, null, MinigameArt.Cream, new Vector2(centre.x, y), new Vector2(inner.x + 24f, 4f));
            kit.Label("Line Words " + words, kit.Wall, words, 24f, MinigameArt.Cream, new Vector2(centre.x + inner.x * 0.5f + 60f, y), new Vector2(90f, 30f));
        }

        // The recipe row: a jug with its parts, cloned per syrup used, and a "+" between them.
        private static void BuildRecipeJugs(MinigamePrefabKit kit, MixDrinkView view)
        {
            RectTransform row = kit.Group("Recipe Jugs", kit.Wall, new Vector2(330f, -10f), new Vector2(720f, 110f));
            RectTransform item = kit.Group("Recipe Jug", row, Vector2.zero, new Vector2(200f, 110f));
            var jug = item.gameObject.AddComponent<MixDrinkRecipeJug>();
            Sprite jugArt = MinigamePrefabKit.Picture(JugNames[0]);
            Image picture = kit.Shape("Jug", item, jugArt != null ? jugArt : MinigamePrefabBuilder.Circle, Color.white, new Vector2(-30f, 0f), new Vector2(110f, 110f));
            picture.preserveAspect = true;
            TMP_Text parts = kit.Label("Parts", item, "×1", 44f, Color.white, new Vector2(50f, -20f), new Vector2(80f, 56f));
            kit.Bind(jug, "jug", picture);
            kit.Bind(jug, "parts", parts);
            TMP_Text plus = kit.Label("Plus", row, "+", 44f, MinigameArt.Cream, new Vector2(-115f, 0f), new Vector2(40f, 56f));

            kit.Bind(view, "jugTemplate", MinigamePrefabKit.Template(jug));
            kit.Bind(view, "plusTemplate", MinigamePrefabKit.Template(plus));
        }

        // The jug pictures per syrup; left empty when a picture has not been added.
        private static void BindJugPictures(MixDrinkView view)
        {
            var serialized = new SerializedObject(view);
            SerializedProperty pictures = serialized.FindProperty("jugPictures");
            pictures.arraySize = JugNames.Length;
            for (int i = 0; i < JugNames.Length; i++)
                pictures.GetArrayElementAtIndex(i).objectReferenceValue = MinigamePrefabKit.Picture(JugNames[i]);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
