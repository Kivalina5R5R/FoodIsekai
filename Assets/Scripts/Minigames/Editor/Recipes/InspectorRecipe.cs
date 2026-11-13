using FoodIsekaiZ.Minigames.Views;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Minigames.EditorTools
{
    // Starting layout of the Health Inspector prefab (Inspector): see InspectorView.
    // Wall: the inspector in the middle, the time left in a box on his left and his speech on his
    // right, and the red wash on top. Floor: a 1.2 m start strip along the far edge and a 1.2 m
    // counter strip along the wall edge, plus the caught mark template.
    public sealed class InspectorRecipe : IMinigamePrefabRecipe
    {
        // The inspector's plain tints, used only until his pictures are added.
        private static readonly Color BackFallback = new Color(0.45f, 0.5f, 0.62f, 1f);
        private static readonly Color FrontFallback = new Color(0.85f, 0.35f, 0.3f, 1f);
        private static readonly Color DarkPanel = new Color(0.16f, 0.09f, 0.04f, 0.92f);
        private static readonly Vector2 StripSize = new Vector2(1100f, 120f);
        // Floor up is towards the wall, so the counter is at the top and the start at the bottom.
        private const float StripY = 190f;

        public string Name => "Inspector";

        public void Build()
        {
            MinigamePrefabKit kit = MinigamePrefabKit.Begin(out InspectorView view);

            Sprite back = MinigamePrefabKit.Picture("InspectorBack");
            Sprite front = MinigamePrefabKit.Picture("InspectorFront");
            Color backColour = back != null ? Color.white : BackFallback;
            Color frontColour = front != null ? Color.white : FrontFallback;
            if (back == null) back = MinigamePrefabBuilder.Circle;
            if (front == null) front = MinigamePrefabBuilder.Circle;
            Image inspector = kit.Shape("Inspector", kit.Wall, back, backColour, new Vector2(0f, -30f), new Vector2(300f, 370f));
            inspector.preserveAspect = true;

            Image clockPanel = Card(kit, "Clock Panel", new Vector2(-330f, 60f), new Vector2(220f, 84f));
            TMP_Text clock = kit.Label("Clock", clockPanel.rectTransform, "9.0 s", 46f, MinigameArt.Gold, Vector2.zero, Vector2.zero);
            Pad(clock.rectTransform, new Vector2(22f, 6f));
            Image speechPanel = Card(kit, "Speech Panel", new Vector2(360f, 60f), new Vector2(420f, 84f));
            TMP_Text speech = kit.Label("Speech", speechPanel.rectTransform, "~ humming ~", 44f, MinigameArt.Cream, Vector2.zero, Vector2.zero);
            Pad(speech.rectTransform, new Vector2(26f, 6f));

            // Last on the wall so the stare's red wash lies over everything else.
            Image redTint = kit.Shape("Red Tint", kit.Wall, null, new Color(0.9f, 0.1f, 0.08f, 0.22f), Vector2.zero, MinigamePrefabKit.WallSize);
            redTint.enabled = false;

            Color startColour = MinigameArt.Cream;
            startColour.a = 0.35f;
            Image start = kit.Shape("Start Zone", kit.Floor, null, startColour, new Vector2(0f, -StripY), StripSize);
            TMP_Text startWords = kit.Label("Start Words", start.rectTransform, "START", 60f, MinigameArt.Cream, Vector2.zero, new Vector2(400f, 80f));
            Color counterColour = MinigameArt.Gold;
            counterColour.a = 0.45f;
            Image counter = kit.Shape("Counter Zone", kit.Floor, null, counterColour, new Vector2(0f, StripY), StripSize);
            TMP_Text counterWords = kit.Label("Counter Words", counter.rectTransform, "COUNTER", 60f, MinigameArt.Brown, Vector2.zero, new Vector2(400f, 80f));
            InspectorCaughtMark caught = BuildCaughtMark(kit);

            kit.Bind(view, "inspector", inspector);
            kit.Bind(view, "backSprite", back);
            kit.BindColour(view, "backColour", backColour);
            kit.Bind(view, "frontSprite", front);
            kit.BindColour(view, "frontColour", frontColour);
            kit.Bind(view, "speechPanel", speechPanel.gameObject);
            kit.Bind(view, "speech", speech);
            kit.Bind(view, "clockPanel", clockPanel.gameObject);
            kit.Bind(view, "clock", clock);
            kit.Bind(view, "redTint", redTint);
            kit.Bind(view, "startZone", start.rectTransform);
            kit.Bind(view, "startWords", startWords);
            kit.Bind(view, "counterZone", counter.rectTransform);
            kit.Bind(view, "counterWords", counterWords);
            kit.Bind(view, "caughtTemplate", caught);
            kit.Save(Name);
        }

        // A red ring 1 m across with a cross, centred on the caught player.
        private static InspectorCaughtMark BuildCaughtMark(MinigamePrefabKit kit)
        {
            RectTransform group = kit.Group("Caught Mark", kit.Floor, Vector2.zero, new Vector2(100f, 100f));
            var mark = group.gameObject.AddComponent<InspectorCaughtMark>();
            kit.Shape("Ring", group, MinigamePrefabBuilder.Ring, MinigameArt.Bad, Vector2.zero, new Vector2(100f, 100f));
            foreach (float angle in new[] { 45f, -45f })
            {
                Image stroke = kit.Shape("Cross", group, null, MinigameArt.Bad, Vector2.zero, new Vector2(10f, 70f));
                stroke.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            }

            return MinigamePrefabKit.Template(mark);
        }

        // The HUD's dark rounded box (or a plain dark pill until its picture exists), stretched to size.
        private static Image Card(MinigamePrefabKit kit, string name, Vector2 position, Vector2 size)
        {
            Sprite picture = MinigamePrefabKit.Picture("HudCard");
            Image card = kit.Shape(name, kit.Wall, picture != null ? picture : MinigamePrefabBuilder.Pill,
                picture != null ? Color.white : DarkPanel, position, size);
            card.type = Image.Type.Sliced;
            card.preserveAspect = false;
            return card;
        }

        // Stretches rect over its parent, inset by padding on each side.
        private static void Pad(RectTransform rect, Vector2 padding)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = padding;
            rect.offsetMax = -padding;
        }
    }
}
