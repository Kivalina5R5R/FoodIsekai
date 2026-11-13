using FoodIsekaiZ.Minigames.Views;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Minigames.EditorTools
{
    // Starting layout of the Pressure Pot Roulette prefab (PressureValve): see PressureValveView.
    public sealed class PressureValveRecipe : IMinigamePrefabRecipe
    {
        // The valves' and the pot's drawn sizes, in floor units (1 m valve circle, 1.7 m pot).
        private const float ValveSize = 100f;
        private const float PotSize = 170f;
        private static readonly Vector2 GaugeCentre = new Vector2(420f, -60f);
        private const float GaugeSize = 260f;

        public string Name => "PressureValve";

        public void Build()
        {
            MinigamePrefabKit kit = MinigamePrefabKit.Begin(out PressureValveView view);

            // The game moves the pot to its play spot; it starts where that spot is (5.5, 2.6 m).
            RectTransform floorPot = kit.Group("Pot", kit.Floor, new Vector2(0f, -10f), new Vector2(PotSize, PotSize));
            kit.Shape("Pot Disc", floorPot, MinigamePrefabBuilder.Circle, new Color(0.25f, 0.22f, 0.22f, 0.9f), Vector2.zero, new Vector2(PotSize, PotSize));
            Image potRing = kit.Shape("Pot Ring", floorPot, MinigamePrefabBuilder.Ring, MinigameArt.Gold, Vector2.zero, new Vector2(PotSize, PotSize));
            PressureValveMark valve = BuildValve(kit);

            Sprite potArt = MinigamePrefabKit.Picture("PressurePot");
            Image potPicture = kit.Shape("Pot Picture", kit.Wall, potArt != null ? potArt : MinigamePrefabBuilder.Circle,
                potArt != null ? Color.white : new Color(0.4f, 0.38f, 0.4f, 1f), new Vector2(-420f, -20f), new Vector2(330f, 330f));

            RectTransform needle = BuildGauge(kit);
            // The drawn gauge's scale runs a full half-circle, from flat left to flat right.
            if (MinigamePrefabKit.Picture("GaugeFace") != null) kit.BindFloat(view, "needleSweep", 90f);

            // The turn's countdown, high in the middle under the meal clock, and the status below it,
            // each in the HUD's dark capsule.
            Image clockPanel = Capsule(kit, "Clock Panel", new Vector2(0f, 118f), new Vector2(150f, 58f));
            TMP_Text clock = kit.Label("Clock", clockPanel.rectTransform, "4", 44f, MinigameArt.Gold, Vector2.zero, Vector2.zero);
            Pad(clock.rectTransform, new Vector2(20f, 4f));
            Image statusPanel = Capsule(kit, "Status Panel", new Vector2(0f, 30f), new Vector2(480f, 72f));
            TMP_Text status = kit.Label("Status", statusPanel.rectTransform, "P1's turn - pick a valve", 40f, MinigameArt.Cream, Vector2.zero, Vector2.zero);
            Pad(status.rectTransform, new Vector2(34f, 6f));

            kit.Bind(view, "floorPot", floorPot);
            kit.Bind(view, "potRing", potRing);
            kit.Bind(view, "potPicture", potPicture);
            kit.Bind(view, "needle", needle);
            kit.Bind(view, "statusPanel", statusPanel.gameObject);
            kit.Bind(view, "status", status);
            kit.Bind(view, "clockPanel", clockPanel.gameObject);
            kit.Bind(view, "clock", clock);
            kit.Bind(view, "valveTemplate", valve);
            kit.Save(Name);
        }

        // The pressure gauge: the GaugeFace picture with the GaugeNeedle picture turning on top
        // (both cut from one drawing so they line up), or plain shapes until they are added.
        // Returns the needle, which turns about its pivot at the gauge centre.
        private static RectTransform BuildGauge(MinigamePrefabKit kit)
        {
            Sprite face = MinigamePrefabKit.Picture("GaugeFace");
            Sprite needleArt = MinigamePrefabKit.Picture("GaugeNeedle");
            if (face != null && needleArt != null)
            {
                Image dial = kit.Shape("Gauge", kit.Wall, face, Color.white, GaugeCentre, new Vector2(GaugeSize, GaugeSize));
                // The needle picture is the same size as the face and points straight up from the
                // face's centre, so it turns about its own centre.
                Image hand = kit.Shape("Needle", dial.rectTransform, needleArt, Color.white, Vector2.zero, new Vector2(GaugeSize, GaugeSize));
                return hand.rectTransform;
            }

            kit.Shape("Gauge", kit.Wall, MinigamePrefabBuilder.Circle, MinigameArt.Cream, GaugeCentre, new Vector2(GaugeSize, GaugeSize));
            kit.Shape("Gauge Rim", kit.Wall, MinigamePrefabBuilder.Ring, MinigameArt.Brown, GaugeCentre, new Vector2(GaugeSize, GaugeSize));
            kit.Shape("Gauge Danger", kit.Wall, MinigamePrefabBuilder.Circle, MinigameArt.Bad, GaugeCentre + new Vector2(95f, 70f), new Vector2(30f, 30f));
            Image needle = kit.Shape("Needle", kit.Wall, null, MinigameArt.Bad, GaugeCentre, new Vector2(12f, 110f));
            // Turns about its foot at the gauge centre.
            needle.rectTransform.pivot = new Vector2(0.5f, 0f);
            needle.rectTransform.anchoredPosition = GaugeCentre;
            kit.Shape("Needle Pin", kit.Wall, MinigamePrefabBuilder.Circle, MinigameArt.Brown, GaugeCentre, new Vector2(30f, 30f));
            return needle.rectTransform;
        }

        // The HUD's dark capsule (or a plain dark pill until its picture exists), stretched to size.
        private static Image Capsule(MinigamePrefabKit kit, string name, Vector2 position, Vector2 size)
        {
            Sprite panel = MinigamePrefabKit.Picture("HudPanel");
            Image capsule = kit.Shape(name, kit.Wall, panel != null ? panel : MinigamePrefabBuilder.Pill,
                panel != null ? Color.white : new Color(0.16f, 0.09f, 0.04f, 0.92f), position, size);
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

        // One valve, centred on its play circle: disc, rim, brass wheel, and the numbered chip at its
        // lower edge so the number does not cover the wheel's cap.
        private static PressureValveMark BuildValve(MinigamePrefabKit kit)
        {
            Vector2 size = new Vector2(ValveSize, ValveSize);
            RectTransform group = kit.Group("Valve", kit.Floor, Vector2.zero, size);
            var valve = group.gameObject.AddComponent<PressureValveMark>();
            Image disc = kit.Shape("Disc", group, MinigamePrefabBuilder.Circle, new Color(0.72f, 0.74f, 0.78f, 1f), Vector2.zero, size);
            kit.Shape("Rim", group, MinigamePrefabBuilder.Ring, MinigameArt.Brown, Vector2.zero, size);
            Sprite wheelArt = MinigamePrefabKit.Picture("Valve");
            kit.Shape("Wheel", group, wheelArt != null ? wheelArt : MinigamePrefabBuilder.Circle,
                wheelArt != null ? Color.white : Color.clear, Vector2.zero, size * 0.86f);
            Vector2 chip = new Vector2(0f, -ValveSize * 0.5f);
            kit.Shape("Chip", group, MinigamePrefabBuilder.Circle, MinigameArt.Brown, chip, size * 0.42f);
            TMP_Text number = kit.Label("Number", group, "1", 34f, MinigameArt.Cream, chip, size * 0.42f);

            kit.Bind(valve, "disc", disc);
            kit.Bind(valve, "number", number);
            return MinigamePrefabKit.Template(valve);
        }
    }
}
