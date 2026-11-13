using FoodIsekaiZ.Display;
using FoodIsekaiZ.Minigames.Views;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Minigames.EditorTools
{
    // Starting layout of the Chef Says prefab (ChefSays): see ChefSaysView.
    public sealed class ChefSaysRecipe : IMinigamePrefabRecipe
    {
        private const float ArrowX = 450f;
        private const float ArrowY = 10f;
        private static readonly Color CallBarGreen = new Color(0.36f, 0.62f, 0.24f, 1f);
        private static readonly Color CallBarShine = new Color(0.62f, 0.82f, 0.45f, 1f);

        public string Name => "ChefSays";

        public void Build()
        {
            MinigamePrefabKit kit = MinigamePrefabKit.Begin(out ChefSaysView view);
            Sprite arrow = MinigamePrefabKit.PictureOr("ArrowPlain", MinigamePrefabKit.Picture("ArrowSign"));
            bool plain = MinigamePrefabKit.Picture("ArrowPlain") != null;
            // Brown words on the cream plain arrow, cream words on the dark wooden sign.
            Color wordColour = plain ? MinigameArt.Brown : MinigameArt.Cream;

            Image left = kit.Shape("Left Arrow", kit.Wall, arrow, Color.white, new Vector2(-ArrowX, ArrowY), new Vector2(470f, 264f));
            left.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            Image right = kit.Shape("Right Arrow", kit.Wall, arrow, Color.white, new Vector2(ArrowX, ArrowY), new Vector2(470f, 264f));
            TMP_Text leftWord = kit.Label("Left Word", kit.Wall, "LEFT", 110f, wordColour, new Vector2(-ArrowX + 60f, ArrowY), new Vector2(280f, 140f));
            TMP_Text rightWord = kit.Label("Right Word", kit.Wall, "RIGHT", 110f, wordColour, new Vector2(ArrowX - 60f, ArrowY), new Vector2(280f, 140f));

            kit.Shape("Chef", kit.Wall, MinigamePrefabKit.PictureOr("ChefSign", MinigamePrefabBuilder.Circle), Color.white,
                new Vector2(0f, -10f), new Vector2(204f, 300f));
            // Sits on the board the chef holds up.
            Image sign = kit.Shape("Sign Arrow", kit.Wall, arrow != null ? arrow : MinigamePrefabBuilder.Chevron,
                arrow != null ? Color.white : MinigameArt.Brown, new Vector2(0f, 109f), new Vector2(96f, 44f));

            // The gilded gauge the players fill when they get ready, drawn at its own 420 x 62 shape.
            RectTransform gaugeRect = kit.Group("Call Gauge", kit.Wall, new Vector2(0f, -168f), new Vector2(390f, 390f * 62f / 420f));
            gaugeRect.gameObject.AddComponent<CanvasRenderer>();
            var gauge = gaugeRect.gameObject.AddComponent<ReadyConfirmationGauge>();
            gauge.raycastTarget = false;
            // The call bar drains in green, not the ready gauge's gold.
            var gaugeSettings = new UnityEditor.SerializedObject(gauge);
            gaugeSettings.FindProperty("amber").colorValue = CallBarGreen;
            gaugeSettings.FindProperty("magic").colorValue = CallBarShine;
            gaugeSettings.ApplyModifiedPropertiesWithoutUndo();

            ChefSaysFloorMark mark = BuildMark(kit);

            kit.Bind(view, "leftArrow", left);
            kit.Bind(view, "rightArrow", right);
            kit.Bind(view, "leftWord", leftWord);
            kit.Bind(view, "rightWord", rightWord);
            kit.Bind(view, "signArrow", sign);
            kit.Bind(view, "callGauge", gauge);
            kit.Bind(view, "markTemplate", mark);
            kit.Save(Name);
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
        // front of them (towards the wall) with a tick or a cross. No ring: ChefSaysFloorMark throws
        // confetti or shards from the player's feet instead. Good is
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
