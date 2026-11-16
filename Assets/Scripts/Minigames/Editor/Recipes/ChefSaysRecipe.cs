using FoodIsekaiZ.Display;
using FoodIsekaiZ.Minigames.Views;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Minigames.EditorTools
{
    // Starting layout of the Chef Says prefab (ChefSays): see ChefSaysView.
    public sealed class ChefSaysRecipe : IMinigamePrefabRecipe
    {
        private static readonly Color CallBarGreen = new Color(0.36f, 0.62f, 0.24f, 1f);
        private static readonly Color CallBarShine = new Color(0.62f, 0.82f, 0.45f, 1f);

        public string Name => "ChefSays";

        public void Build()
        {
            MinigamePrefabKit kit = MinigamePrefabKit.Begin(out ChefSaysView view);
            AddChefPopups(view);

            kit.Bind(view, "leftGauge", Gauge(kit, "Left Call Gauge", -ChefX));
            kit.Bind(view, "rightGauge", Gauge(kit, "Right Call Gauge", ChefX));
            kit.Bind(view, "leftWord", Word(kit, "Left Call Word", -ChefX));
            kit.Bind(view, "rightWord", Word(kit, "Right Call Word", ChefX));

            ChefSaysFloorMark mark = BuildMark(kit);
            kit.Bind(view, "markTemplate", mark);
            AddIngredients(view, mark);
            kit.Save(Name);
        }

        private const string IngredientFolder = "Assets/Art/Minigame/ChefSays/Ingredients";
        private const int IngredientPictures = 6;
        // The floor ingredient, in floor units (100 = 1 m); ChefSaysGame keeps its half width in metres.
        private const float FloorIngredientSize = 72f;
        // The basket sits on top of the HUD's call counter (220 x 52 at y -175.5) in the bottom middle.
        private const float BasketWidth = 230f;
        private const float BasketBottom = -150f;
        private const float PileIngredientSize = 92f;
        // Where the pile starts, as a share of the basket picture's height from its bottom: just above
        // the front fold, so each ingredient's lower half hides behind the front wall.
        private const float MouthHeight = 0.62f;
        private static readonly Vector2 BasketShadowSize = new Vector2(310f, 52f);

        // Also used to migrate the existing prefab: the faint ingredient at the end of each mark's
        // chevrons and the wall basket the collected ones fly into.
        public static void AddIngredients(ChefSaysView view, ChefSaysFloorMark mark)
        {
            var sprites = new Sprite[IngredientPictures];
            for (int i = 0; i < sprites.Length; i++) sprites[i] = LoadSprite($"{IngredientFolder}/ingredient-{i}.png");
            Sprite basketSprite = LoadSprite($"{IngredientFolder}/basket.png");
            Sprite shadowSprite = LoadSprite($"{IngredientFolder}/basket-shadow.png");
            Sprite frontSprite = LoadSprite($"{IngredientFolder}/basket-front.png");

            Image floorIngredient = Picture("Ingredient", (RectTransform)mark.transform, sprites[0], new Color(1f, 1f, 1f, 0.45f),
                Vector2.zero, new Vector2(FloorIngredientSize, FloorIngredientSize));
            floorIngredient.preserveAspect = true;
            floorIngredient.transform.SetSiblingIndex(2);
            floorIngredient.enabled = false;
            var markSettings = new UnityEditor.SerializedObject(mark);
            markSettings.FindProperty("ingredient").objectReferenceValue = floorIngredient;
            markSettings.ApplyModifiedPropertiesWithoutUndo();

            ChefSaysBasket basket = Basket(view.Wall, basketSprite, frontSprite, shadowSprite, sprites[0]);
            var viewSettings = new UnityEditor.SerializedObject(view);
            viewSettings.FindProperty("basket").objectReferenceValue = basket;
            SetFrames(viewSettings, "ingredients", sprites);
            viewSettings.ApplyModifiedPropertiesWithoutUndo();
        }

        // The basket picture (its inside), the pile, the basket's front, then the flying ingredients, all covering the
        // whole wall so positions are wall positions. Drawn over the rest of the wall.
        private static ChefSaysBasket Basket(RectTransform wall, Sprite basketSprite, Sprite frontSprite, Sprite shadowSprite, Sprite sample)
        {
            RectTransform root = Rect("Ingredient Basket", wall, Vector2.zero, MinigamePrefabKit.WallSize);
            float basketHeight = BasketWidth * basketSprite.rect.height / basketSprite.rect.width;
            // A soft contact shadow under the basket so it sits on the tavern floor instead of floating.
            Picture("Basket Shadow", root, shadowSprite, Color.white,
                new Vector2(0f, BasketBottom), BasketShadowSize);
            Image picture = Picture("Basket", root, basketSprite, Color.white,
                new Vector2(0f, BasketBottom + basketHeight * 0.5f), new Vector2(BasketWidth, basketHeight));
            picture.preserveAspect = true;
            RectTransform pile = Rect("Pile", root, Vector2.zero, MinigamePrefabKit.WallSize);
            // The basket's front wall and cloth (cut from the same picture along the front fold),
            // drawn over the pile so the ingredients sit inside.
            Image front = Picture("Basket Front", root, frontSprite, Color.white, picture.rectTransform.anchoredPosition, picture.rectTransform.sizeDelta);
            front.preserveAspect = true;
            RectTransform flying = Rect("Flying", root, Vector2.zero, MinigamePrefabKit.WallSize);
            // The template marks where the pile starts: the middle of the basket's mouth.
            Image template = Picture("Ingredient Template", flying, sample, Color.white,
                new Vector2(0f, BasketBottom + basketHeight * MouthHeight), new Vector2(PileIngredientSize, PileIngredientSize));
            template.preserveAspect = true;
            template.gameObject.SetActive(false);

            var basket = root.gameObject.AddComponent<ChefSaysBasket>();
            var settings = new UnityEditor.SerializedObject(basket);
            settings.FindProperty("pile").objectReferenceValue = pile;
            settings.FindProperty("flying").objectReferenceValue = flying;
            settings.FindProperty("ingredientTemplate").objectReferenceValue = template;
            settings.ApplyModifiedPropertiesWithoutUndo();
            return basket;
        }

        // Each side: the chef rising from the wall's bottom edge, sized so her head matches the
        // smallest tavern NPC, with her countdown gauge just over her head. The called word starts
        // at the middle of the wall and floats up to the top, so it is seen before she appears.
        private const float ChefX = 450f;
        private const float WallBottom = -217.5f;
        private const float ChefHeight = 365f;
        private const float ChefWidth = ChefHeight * 355f / 372f;
        // Her head sits left of the picture's centre because her tail fills the right side, so the
        // picture is moved right to put her head under the gauge.
        private const float HeadCentre = 0.39f;
        private const float GaugeY = 170f;
        private const float GaugeWidth = 250f;
        private const float WordY = 0f;
        // The word stops just under the gauge and is already fading by then, so it never covers it.
        private const float WordRise = 125f;
        private const float WordHoldShare = 0.45f;
        private const float WordSize = 70f;

        // The gilded gauge the players fill when they get ready, drawn at its own 420 x 62 shape.
        private static ReadyConfirmationGauge Gauge(MinigamePrefabKit kit, string name, float x)
        {
            RectTransform gaugeRect = kit.Group(name, kit.Wall, new Vector2(x, GaugeY), new Vector2(GaugeWidth, GaugeWidth * 62f / 420f));
            gaugeRect.gameObject.AddComponent<CanvasRenderer>();
            var gauge = gaugeRect.gameObject.AddComponent<ReadyConfirmationGauge>();
            gauge.raycastTarget = false;
            // The call bar drains in green, not the ready gauge's gold.
            var gaugeSettings = new UnityEditor.SerializedObject(gauge);
            gaugeSettings.FindProperty("amber").colorValue = CallBarGreen;
            gaugeSettings.FindProperty("magic").colorValue = CallBarShine;
            gaugeSettings.ApplyModifiedPropertiesWithoutUndo();
            return gauge;
        }

        // The called direction, gold over a dark copy that acts as its shadow, floating up above
        // the head of the chef about to appear.
        private static ChefSaysCallWord Word(MinigamePrefabKit kit, string name, float x)
        {
            RectTransform root = kit.Group(name, kit.Wall, new Vector2(x, WordY), new Vector2(360f, 90f));
            root.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            TMPro.TMP_Text shadow = kit.Label("Shadow", root, "RIGHT!", WordSize, CallWordShadow, new Vector2(4f, -5f), new Vector2(360f, 90f));
            TMPro.TMP_Text main = kit.Label("Words", root, "RIGHT!", WordSize, CallWordGold, Vector2.zero, new Vector2(360f, 90f));
            var word = root.gameObject.AddComponent<ChefSaysCallWord>();
            var settings = new UnityEditor.SerializedObject(word);
            settings.FindProperty("rise").floatValue = WordRise;
            settings.FindProperty("holdShare").floatValue = WordHoldShare;
            var labels = settings.FindProperty("labels");
            labels.arraySize = 2;
            labels.GetArrayElementAtIndex(0).objectReferenceValue = shadow;
            labels.GetArrayElementAtIndex(1).objectReferenceValue = main;
            settings.ApplyModifiedPropertiesWithoutUndo();
            return word;
        }

        private static readonly Color CallWordGold = new Color(1f, 0.86f, 0.38f, 1f);
        private static readonly Color CallWordShadow = new Color(0.24f, 0.12f, 0.06f, 0.9f);

        private const string WolfgirlFolder = "Assets/Art/Minigame/ChefSays/Wolfgirl";
        // wolfgirl-00..78 at 12 fps: 00-18 jump in, 19-71 searching loop (71 leads back into 19),
        // 72 is the loop's twin of 19 and is skipped, 73-78 duck down.
        private const int EnterFirst = 0;
        private const int IdleFirst = 19;
        private const int IdleLast = 71;
        private const int ExitFirst = 73;
        private const int ExitLast = 78;

        // Also used to migrate the existing prefab without rebuilding its floor or gauge.
        public static void AddChefPopups(ChefSaysView view)
        {
            ChefSaysPopup left = Popup(view.Wall, "Left Chef", -ChefX);
            ChefSaysPopup right = Popup(view.Wall, "Right Chef", ChefX);
            var settings = new UnityEditor.SerializedObject(view);
            settings.FindProperty("leftChef").objectReferenceValue = left;
            settings.FindProperty("rightChef").objectReferenceValue = right;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static ChefSaysPopup Popup(RectTransform wall, string name, float x)
        {
            Sprite[] idle = WolfgirlFrames(IdleFirst, IdleLast);
            Image image = Picture(name, wall, idle[0], Color.white,
                new Vector2(x + (0.5f - HeadCentre) * ChefWidth, WallBottom + ChefHeight * 0.5f), new Vector2(ChefWidth, ChefHeight));
            image.transform.SetSiblingIndex(1);
            var popup = image.gameObject.AddComponent<ChefSaysPopup>();
            var settings = new UnityEditor.SerializedObject(popup);
            settings.FindProperty("portrait").objectReferenceValue = image;
            SetFrames(settings, "enterFrames", WolfgirlFrames(EnterFirst, IdleFirst - 1));
            SetFrames(settings, "idleFrames", idle);
            SetFrames(settings, "exitFrames", WolfgirlFrames(ExitFirst, ExitLast));
            settings.FindProperty("sparkleSprite").objectReferenceValue = LoadSprite($"{WolfgirlFolder}/sparkle-star.png");
            settings.ApplyModifiedPropertiesWithoutUndo();
            return popup;
        }

        private static void SetFrames(UnityEditor.SerializedObject settings, string field, Sprite[] frames)
        {
            var sprites = settings.FindProperty(field);
            sprites.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++) sprites.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
        }

        // The wolfgirl frames numbered first to last, inclusive.
        private static Sprite[] WolfgirlFrames(int first, int last)
        {
            var frames = new Sprite[last - first + 1];
            for (int i = 0; i < frames.Length; i++) frames[i] = LoadSprite($"{WolfgirlFolder}/wolfgirl-{first + i:00}.png");
            return frames;
        }

        private static Sprite LoadSprite(string path)
        {
            Sprite sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new System.InvalidOperationException($"Missing popup sprite: {path}");
            return sprite;
        }

        // One player's floor arrows, centred on the player; 100 units = 1 m. Only the chevrons.
        private static ChefSaysFloorMark BuildMark(MinigamePrefabKit kit)
        {
            RectTransform group = kit.Group("Player Mark", kit.Floor, Vector2.zero, new Vector2(100f, 100f));
            var mark = group.gameObject.AddComponent<ChefSaysFloorMark>();

            Image leftChevron = kit.Shape("Left Chevron", group, MinigamePrefabBuilder.Chevron, MinigameArt.Cream, new Vector2(-50f, 0f), new Vector2(26f, 44f));
            leftChevron.preserveAspect = false;
            leftChevron.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            Image rightChevron = kit.Shape("Right Chevron", group, MinigamePrefabBuilder.Chevron, MinigameArt.Cream, new Vector2(50f, 0f), new Vector2(26f, 44f));
            rightChevron.preserveAspect = false;

            AddResultMarks(group, mark);
            kit.Bind(mark, "leftChevron", leftChevron);
            kit.Bind(mark, "rightChevron", rightChevron);
            return MinigamePrefabKit.Template(mark);
        }

        // The right and wrong marks shown where the player stands after each call: a badge just in
        // front of them (towards the wall) with a tick or a cross. No ring here: ChefSaysFloorMark plays its own glow, rings,
        // stars, confetti or smoke around the badge instead. Good is
        // left showing in the prefab for editing, bad is switched off; the game shows one at a time.
        public static void AddResultMarks(RectTransform mark, ChefSaysFloorMark component)
        {
            CanvasGroup good = ResultMark(mark, "Good", MinigameArt.Good);
            Bar(good.transform, "Tick Short", new Vector2(-9f, 95f), 7f, 18f, 45f);
            Bar(good.transform, "Tick Long", new Vector2(5f, 100f), 7f, 30f, -42f);
            CanvasGroup bad = ResultMark(mark, "Bad", MinigameArt.Bad);
            Bar(bad.transform, "Cross A", new Vector2(0f, 100f), 7f, 32f, 45f);
            Bar(bad.transform, "Cross B", new Vector2(0f, 100f), 7f, 32f, -45f);
            bad.gameObject.SetActive(false);

            var settings = new UnityEditor.SerializedObject(component);
            settings.FindProperty("good").objectReferenceValue = good;
            settings.FindProperty("bad").objectReferenceValue = bad;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static CanvasGroup ResultMark(RectTransform parent, string name, Color colour)
        {
            RectTransform root = Rect(name, parent, Vector2.zero, new Vector2(130f, 130f));
            CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            Picture("Badge", root, MinigamePrefabBuilder.Circle, colour, new Vector2(0f, 100f), new Vector2(60f, 60f));
            return group;
        }

        // A white stroke of the badge's tick or cross, turned by angle degrees.
        private static void Bar(Transform parent, string name, Vector2 position, float width, float length, float angle)
        {
            Image bar = Picture(name, (RectTransform)parent, null, Color.white, position, new Vector2(width, length));
            bar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private static Image Picture(string name, RectTransform parent, Sprite sprite, Color colour, Vector2 position, Vector2 size)
        {
            RectTransform rect = Rect(name, parent, position, size);
            rect.gameObject.AddComponent<CanvasRenderer>();
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform Rect(string name, RectTransform parent, Vector2 position, Vector2 size)
        {
            var part = new GameObject(name, typeof(RectTransform));
            part.layer = parent.gameObject.layer;
            var rect = (RectTransform)part.transform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }
    }
}
