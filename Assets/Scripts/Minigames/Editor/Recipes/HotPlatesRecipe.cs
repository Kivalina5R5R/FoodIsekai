using FoodIsekaiZ.Minigames.Views;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Minigames.EditorTools
{
    // Starting layout of the Hot Plates prefab (HotPlates): see HotPlatesView.
    // Four plates, one per corner of the 11 x 5 m floor, each 1.8 m across and 0.3 m in from the
    // edges: green and yellow on the wall side, blue and purple on the far side.
    public sealed class HotPlatesRecipe : IMinigamePrefabRecipe
    {
        private const float PlateDiameter = 180f;
        private static readonly Vector2 CornerOffset = new Vector2(430f, 130f);
        // The view's default plate colours, in the same order.
        private static readonly Color[] PreviewColours =
        {
            new Color(0.3f, 0.78f, 0.35f, 1f),
            new Color(1f, 0.85f, 0.2f, 1f),
            new Color(0.3f, 0.6f, 1f, 1f),
            new Color(0.68f, 0.42f, 0.95f, 1f),
        };

        public string Name => "HotPlates";

        public void Build()
        {
            MinigamePrefabKit kit = MinigamePrefabKit.Begin(out HotPlatesView view);
            Sprite plate = MinigamePrefabKit.PictureOr("Plate", MinigamePrefabBuilder.Circle);

            Image calledPlate = kit.Shape("Called Plate", kit.Wall, plate, Color.Lerp(Color.white, PreviewColours[0], 0.75f), new Vector2(-380f, -10f), new Vector2(300f, 300f));
            TMP_Text words = kit.Label("Called Words", kit.Wall, "GREEN!", 150f, MinigameArt.Cream, new Vector2(230f, 40f), new Vector2(820f, 190f));
            kit.Shape("Time Bar Back", kit.Wall, null, new Color(0f, 0f, 0f, 0.4f), new Vector2(230f, -100f), new Vector2(700f, 28f));
            Image timeBar = kit.Shape("Time Bar", kit.Wall, null, MinigameArt.Gold, new Vector2(230f, -100f), new Vector2(700f, 28f));
            // Pinned at its left end so it empties towards the left.
            timeBar.rectTransform.pivot = new Vector2(0f, 0.5f);
            timeBar.rectTransform.anchoredPosition = new Vector2(230f - 350f, -100f);
            TMP_Text counter = kit.Label("Counter", kit.Wall, "1 / 5", 36f, MinigameArt.Cream, new Vector2(230f, -160f), new Vector2(520f, 50f));

            // Floor up is towards the wall: positive y is the wall side.
            var corners = new[]
            {
                ("Plate Green", new Vector2(-CornerOffset.x, CornerOffset.y), 0),
                ("Plate Yellow", new Vector2(CornerOffset.x, CornerOffset.y), 1),
                ("Plate Blue", new Vector2(-CornerOffset.x, -CornerOffset.y), 2),
                ("Plate Purple", new Vector2(CornerOffset.x, -CornerOffset.y), 3),
            };

            var serializedView = new SerializedObject(view);
            SerializedProperty list = serializedView.FindProperty("plates");
            list.arraySize = corners.Length;
            for (int i = 0; i < corners.Length; i++)
            {
                (string name, Vector2 at, int colour) = corners[i];
                // Shown in its colour while editing too; the view repaints it the same way in play.
                Image dish = kit.Shape(name, kit.Floor, plate, Color.Lerp(Color.white, PreviewColours[colour], 0.75f), at, Vector2.one * PlateDiameter);
                var mark = dish.gameObject.AddComponent<HotPlatesPlate>();
                var serializedMark = new SerializedObject(mark);
                serializedMark.FindProperty("dish").objectReferenceValue = dish;
                serializedMark.FindProperty("colourIndex").intValue = colour;
                serializedMark.ApplyModifiedPropertiesWithoutUndo();
                list.GetArrayElementAtIndex(i).objectReferenceValue = mark;
            }

            serializedView.ApplyModifiedPropertiesWithoutUndo();

            kit.Bind(view, "calledPlate", calledPlate);
            kit.Bind(view, "calledWords", words);
            kit.Bind(view, "timeBar", timeBar.rectTransform);
            kit.Bind(view, "counter", counter);
            kit.Save(Name);
        }
    }
}
