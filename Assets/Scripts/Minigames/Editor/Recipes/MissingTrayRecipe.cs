using System.Collections.Generic;
using FoodIsekaiZ.Minigames.Views;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Minigames.EditorTools
{
    // Starting layout of the What's Missing? prefab (MissingTray): see MissingTrayView.
    public sealed class MissingTrayRecipe : IMinigamePrefabRecipe
    {
        private const string RealFoodFolder = "Assets/Art/Food";

        private static readonly Color NameChip = new Color(0.12f, 0.06f, 0.03f, 0.82f);
        private static readonly Color DimPlate = new Color(1f, 1f, 1f, 0.4f);

        public string Name => "MissingTray";

        public void Build()
        {
            MinigamePrefabKit kit = MinigamePrefabKit.Begin(out MissingTrayView view);

            // Sits just under the HUD's title chip.
            TMP_Text status = kit.Label("Status", kit.Wall, "Remember the tray!", 34f, MinigameArt.Cream, new Vector2(-590f, 90f), new Vector2(330f, 110f));
            Sprite tray = MinigamePrefabKit.Picture("Tray");
            kit.Shape("Tray", kit.Wall, tray, tray != null ? Color.white : MinigameArt.Copper, new Vector2(0f, -58f), new Vector2(980f, 420f));
            RectTransform trayFull = kit.Fill("Tray Full", kit.Wall);
            RectTransform trayShort = kit.Fill("Tray Short", kit.Wall);
            MinigamePrefabKit.HideAtStart(trayShort.gameObject);
            Sprite coverPicture = MinigamePrefabKit.Picture("TrayCover");
            Image cover = kit.Shape("Cover", kit.Wall, coverPicture != null ? coverPicture : MinigamePrefabBuilder.Circle,
                coverPicture != null ? Color.white : new Color(0.75f, 0.76f, 0.8f, 1f), new Vector2(0f, -34f), new Vector2(1000f, 330f));
            MinigamePrefabKit.HideAtStart(cover.gameObject);

            RectTransform wallDish = kit.Group("Wall Dish", kit.Wall, new Vector2(0f, -44f), new Vector2(174f, 160f));
            MissingTrayDishSlot wallSlot = BuildDish(kit, wallDish, 124f, 22f, MinigameArt.Brown);
            MinigamePrefabKit.Template(wallSlot);

            MissingTrayTile tile = BuildTile(kit);
            Image dot = MinigamePrefabKit.Template(kit.Shape("Answer Dot", kit.Floor, MinigamePrefabBuilder.Circle, Color.white, Vector2.zero, new Vector2(15f, 15f)));

            kit.Bind(view, "status", status);
            kit.Bind(view, "trayFull", trayFull);
            kit.Bind(view, "trayShort", trayShort);
            kit.Bind(view, "cover", cover);
            kit.Bind(view, "wallDishTemplate", wallSlot);
            kit.Bind(view, "tileTemplate", tile);
            kit.Bind(view, "answerDotTemplate", dot);
            BindDishes(view);
            kit.Save(Name);
        }

        // One answer tile (1.1 m plate) centred on its arena point.
        private static MissingTrayTile BuildTile(MinigamePrefabKit kit)
        {
            RectTransform group = kit.Group("Tile", kit.Floor, Vector2.zero, new Vector2(110f, 110f));
            var tile = group.gameObject.AddComponent<MissingTrayTile>();
            Image plate = kit.Shape("Plate", group, MinigamePrefabKit.PictureOr("Plate", MinigamePrefabBuilder.Circle), DimPlate, Vector2.zero, new Vector2(110f, 110f));
            kit.Shape("Name Chip", group, null, NameChip, new Vector2(0f, -41f), new Vector2(100f, 26f));
            RectTransform dishGroup = kit.Group("Dish", group, new Vector2(0f, -4f), new Vector2(104f, 100f));
            MissingTrayDishSlot dish = BuildDish(kit, dishGroup, 74f, 20f, MinigameArt.Cream);
            kit.Bind(tile, "plate", plate);
            kit.Bind(tile, "dish", dish);
            return MinigamePrefabKit.Template(tile);
        }

        // A dish picture of this size with its name below, centred on the group.
        private static MissingTrayDishSlot BuildDish(MinigamePrefabKit kit, RectTransform group, float size, float labelSize, Color labelColour)
        {
            var slot = group.gameObject.AddComponent<MissingTrayDishSlot>();
            Image food = kit.Shape("Food", group, MinigamePrefabBuilder.Circle, Color.white, new Vector2(0f, labelSize * 0.6f), new Vector2(size, size));
            TMP_Text name = kit.Label("Name", group, "Dish", labelSize, labelColour, new Vector2(0f, -size * 0.5f), new Vector2(size * 1.4f, labelSize * 1.3f));
            kit.Bind(slot, "food", food);
            kit.Bind(slot, "dishName", name);
            return slot;
        }

        // Each real dish's picture from Assets/Art/Food (M- breakfast, L- lunch, D- dinner), keyed by
        // its file name. Also run on the existing prefab when the dish list changes, so other edits stay.
        public static void BindDishes(MissingTrayView view)
        {
            var dishes = new List<(string key, Color colour, Sprite shape)>();
            foreach (string meal in new[] { "M", "L", "D" })
            foreach (string category in new[] { "Meat", "Seafood", "Starter", "Dessert", "Drink" })
                dishes.Add(($"{meal}-{category}", Color.white, MinigamePrefabBuilder.Circle));

            var serialized = new SerializedObject(view);
            SerializedProperty list = serialized.FindProperty("dishes");
            list.arraySize = dishes.Count;
            for (int i = 0; i < dishes.Count; i++)
            {
                (string key, Color colour, Sprite shape) = dishes[i];
                Sprite picture = AssetDatabase.LoadAssetAtPath<Sprite>($"{RealFoodFolder}/{key}.png");
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("key").stringValue = key;
                entry.FindPropertyRelative("sprite").objectReferenceValue = picture != null ? picture : shape;
                // A missing picture shows as a grey circle so the gap is easy to spot.
                entry.FindPropertyRelative("colour").colorValue = picture != null ? colour : Color.grey;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
