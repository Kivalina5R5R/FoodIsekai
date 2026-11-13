using System.Collections.Generic;
using FoodIsekaiZ.Minigames.Views;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Minigames.EditorTools
{
    // Starting layout of the Order Up! prefab (OrderUp): see OrderUpView.
    // Wall, left to right: the customer, her order in a dark panel (two big dishes, each with its
    // found dots), then the time left and the teams in capsules on the right. Nothing about the
    // crates is on the wall; they open on the floor.
    public sealed class OrderUpRecipe : IMinigamePrefabRecipe
    {
        private const int Columns = 4;
        private const int Rows = 2;
        private const int OrderCount = 2;
        // Floor crates: 1.05 m across, 0.3 m apart, grid centred 2.6 m from the wall in the middle.
        private const float CrateSize = 105f;
        private const float CratePitch = 135f;
        private static readonly Vector2 CrateGridCentre = new Vector2(0f, -10f);
        private static readonly Color CrateColour = new Color(0.55f, 0.36f, 0.2f, 1f);
        private static readonly Color DimDot = new Color(1f, 1f, 1f, 0.18f);
        private static readonly Color DarkPanel = new Color(0.16f, 0.09f, 0.04f, 0.92f);
        private const string RealFoodFolder = "Assets/Art/Food";

        // The order panel and the two dishes in it.
        private static readonly Vector2 OrderPanelCentre = new Vector2(-80f, -25f);
        private static readonly Vector2 OrderPanelSize = new Vector2(640f, 300f);
        private const float OrderSpotPitch = 300f;
        private const float OrderDishSize = 170f;

        public string Name => "OrderUp";

        public void Build()
        {
            MinigamePrefabKit kit = MinigamePrefabKit.Begin(out OrderUpView view);
            Sprite cratePicture = MinigamePrefabKit.Picture("FoodCrate");
            Color crateTint = cratePicture != null ? Color.white : CrateColour;

            int count = Columns * Rows;
            var floorCrates = new Object[count];
            for (int i = 0; i < count; i++)
            {
                int column = i % Columns;
                int row = i / Columns;
                // Floor rows count away from the wall, so the first row is the one nearer the wall.
                Vector2 floorAt = CrateGridCentre + new Vector2(column - (Columns - 1) * 0.5f, (Rows - 1) * 0.5f - row) * CratePitch;
                floorCrates[i] = BuildCrate(kit, kit.Floor, $"Crate {i + 1}", cratePicture, crateTint, floorAt);
            }

            kit.Shape("Customer", kit.Wall, MinigamePrefabKit.PictureOr("Customer", MinigamePrefabBuilder.Circle),
                MinigamePrefabKit.Picture("Customer") != null ? Color.white : MinigameArt.Gold, new Vector2(-600f, -40f), new Vector2(330f, 330f));

            Image panel = Capsule(kit, "Order Panel", OrderPanelCentre, OrderPanelSize, "HudCard");
            var spots = new Object[OrderCount];
            OrderUpDish orderDish = null;
            Image dot = null;
            for (int i = 0; i < OrderCount; i++)
            {
                float x = (i - (OrderCount - 1) * 0.5f) * OrderSpotPitch;
                RectTransform spot = kit.Group($"Order {i + 1}", panel.rectTransform, new Vector2(x, 20f), Vector2.zero);
                spots[i] = spot;
                if (i > 0) continue;
                orderDish = MinigamePrefabKit.Template(BuildDish(kit, spot, "Order Dish", Vector2.zero, OrderDishSize, 30f, MinigameArt.Cream));
                dot = MinigamePrefabKit.Template(kit.Shape("Found Dot", spot, MinigamePrefabBuilder.Circle, DimDot, new Vector2(0f, -122f), new Vector2(28f, 28f)));
            }

            Image clockPanel = Capsule(kit, "Clock Panel", new Vector2(500f, 45f), new Vector2(210f, 96f), "HudCard");
            TMP_Text clock = kit.Label("Clock", clockPanel.rectTransform, "10", 70f, MinigameArt.Gold, Vector2.zero, Vector2.zero);
            Pad(clock.rectTransform, new Vector2(24f, 6f));
            Image teamsPanel = Capsule(kit, "Teams Panel", new Vector2(500f, -70f), new Vector2(420f, 58f));
            TMP_Text teams = kit.Label("Teams", teamsPanel.rectTransform, "P1 P3  vs  P2 P4", 28f, MinigameArt.Cream, Vector2.zero, Vector2.zero);
            Pad(teams.rectTransform, new Vector2(26f, 4f));

            BindDishes(view);
            BindObjects(view, "floorCrates", floorCrates);
            BindObjects(view, "orderSpots", spots);
            kit.Bind(view, "orderDishTemplate", orderDish);
            kit.Bind(view, "foundDotTemplate", dot);
            kit.Bind(view, "teams", teams);
            kit.Bind(view, "clock", clock);
            kit.Save(Name);
        }

        // The fifteen real dishes (M- breakfast, L- lunch, D- dinner) by their picture names.
        private static void BindDishes(OrderUpView view)
        {
            var keys = new List<string>();
            var pictures = new List<Object>();
            foreach (string meal in new[] { "M", "L", "D" })
            foreach (string category in new[] { "Meat", "Seafood", "Starter", "Dessert", "Drink" })
            {
                string key = $"{meal}-{category}";
                keys.Add(key);
                pictures.Add(AssetDatabase.LoadAssetAtPath<Sprite>($"{RealFoodFolder}/{key}.png"));
            }

            var serialized = new SerializedObject(view);
            SerializedProperty keyList = serialized.FindProperty("dishKeys");
            keyList.arraySize = keys.Count;
            for (int i = 0; i < keys.Count; i++) keyList.GetArrayElementAtIndex(i).stringValue = keys[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            BindObjects(view, "dishPictures", pictures.ToArray());
        }

        // A floor crate picture with its number in the corner and a hidden dish lying on top.
        private static OrderUpCrate BuildCrate(MinigamePrefabKit kit, RectTransform parent, string name, Sprite picture, Color tint, Vector2 position)
        {
            Image crate = kit.Shape(name, parent, picture, tint, position, new Vector2(CrateSize, CrateSize));
            string number = name.Substring(name.LastIndexOf(' ') + 1);
            kit.Label("Number", crate.rectTransform, number, 30f, MinigameArt.Cream, new Vector2(-CrateSize * 0.38f, CrateSize * 0.38f), new Vector2(30f, 30f));
            OrderUpDish dish = BuildDish(kit, crate.rectTransform, "Dish", Vector2.zero, 80f, 18f, MinigameArt.Cream);
            MinigamePrefabKit.HideAtStart(dish.gameObject);
            var holder = crate.gameObject.AddComponent<OrderUpCrate>();
            kit.Bind(holder, "dish", dish);
            return holder;
        }

        // A dish picture of size with its name under it, grouped so it shows and hides at once.
        private static OrderUpDish BuildDish(MinigamePrefabKit kit, RectTransform parent, string name, Vector2 position,
            float size, float labelSize, Color labelColour)
        {
            Image holder = kit.Shape(name, parent, null, Color.clear, position, new Vector2(size, size + labelSize));
            Image food = kit.Shape("Food", holder.rectTransform, AssetDatabase.LoadAssetAtPath<Sprite>($"{RealFoodFolder}/M-Meat.png"), Color.white,
                new Vector2(0f, labelSize * 0.5f), new Vector2(size, size));
            food.preserveAspect = true;
            TMP_Text dishName = kit.Label("Name", holder.rectTransform, "Breakfast Meat", labelSize, labelColour,
                new Vector2(0f, -size * 0.5f), new Vector2(size * 1.6f, labelSize * 1.3f));
            var dish = holder.gameObject.AddComponent<OrderUpDish>();
            kit.Bind(dish, "food", food);
            kit.Bind(dish, "dishName", dishName);
            return dish;
        }

        // The HUD's dark panel (or a plain dark pill until its picture exists), stretched to size:
        // HudPanel is the capsule for one line, HudCard the same look as a rounded box for taller panels.
        private static Image Capsule(MinigamePrefabKit kit, string name, Vector2 position, Vector2 size, string pictureName = "HudPanel")
        {
            Sprite picture = MinigamePrefabKit.Picture(pictureName);
            Image capsule = kit.Shape(name, kit.Wall, picture != null ? picture : MinigamePrefabBuilder.Pill,
                picture != null ? Color.white : DarkPanel, position, size);
            capsule.type = Image.Type.Sliced;
            capsule.preserveAspect = false;
            return capsule;
        }

        // Stretches rect over its parent, inset by padding on each side.
        private static void Pad(RectTransform rect, Vector2 padding)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = padding;
            rect.offsetMax = -padding;
        }

        private static void BindObjects(Object target, string field, Object[] values)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
