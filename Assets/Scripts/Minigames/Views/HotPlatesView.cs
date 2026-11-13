using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Minigames.Views
{
    // Hot Plates on the wall and floor (prefab Assets/Prefab/Minigame/HotPlates).
    // Wall: a big plate in the called colour, the colour word, a time bar that empties towards the
    // left, and the call counter.
    // Floor: the plates placed in the prefab (one per corner to start with), each tinted
    // dishShare of the way from white to its colour. When a call ends, every other plate turns hot.
    // The game reads each plate's centre and size from here, so moving or resizing a plate in the
    // prefab moves its zone.
    public sealed class HotPlatesView : MinigameView
    {
        [SerializeField] private Color[] plateColours =
        {
            new Color(0.3f, 0.78f, 0.35f, 1f),
            new Color(1f, 0.85f, 0.2f, 1f),
            new Color(0.3f, 0.6f, 1f, 1f),
            new Color(0.68f, 0.42f, 0.95f, 1f),
        };
        [SerializeField] private Color hotColour = new Color(1f, 0.42f, 0.18f, 1f);
        [SerializeField, Range(0f, 1f)] private float dishShare = 0.75f;
        [SerializeField] private Image calledPlate;
        [SerializeField] private TMP_Text calledWords;
        [SerializeField] private RectTransform timeBar;
        [SerializeField] private TMP_Text counter;
        [SerializeField] private List<HotPlatesPlate> plates = new List<HotPlatesPlate>();

        private readonly List<Vector2> centres = new List<Vector2>();
        private readonly List<float> radii = new List<float>();

        public int PlateCount => plates.Count;

        public int ColourCount => plateColours.Length;

        protected override void OnBound()
        {
            foreach (HotPlatesPlate plate in plates)
            {
                // The plate's box on the floor, in arena metres; the plate is the circle inside it.
                var corners = new Vector3[4];
                plate.Area.GetWorldCorners(corners);
                Vector2 min = Vector2.positiveInfinity;
                Vector2 max = Vector2.negativeInfinity;
                foreach (Vector3 corner in corners)
                {
                    Vector2 arena = Stage.ArenaPoint(Floor.InverseTransformPoint(corner));
                    min = Vector2.Min(min, arena);
                    max = Vector2.Max(max, arena);
                }

                centres.Add((min + max) * 0.5f);
                radii.Add(Mathf.Min(max.x - min.x, max.y - min.y) * 0.5f);
            }

            Cool();
        }

        // Plate index's centre and radius in arena metres, and the colour that keeps it safe.
        public Vector2 PlateCentre(int index) => centres[index];

        public float PlateRadius(int index) => radii[index];

        public int PlateColour(int index) => plates[index].ColourIndex;

        // A new call: shows the called colour and word, fills the time bar and cools every plate.
        public void ShowCall(int colourIndex, string words, int callNumber, int calls)
        {
            calledPlate.color = DishTint(colourIndex);
            calledWords.text = words;
            calledWords.color = plateColours[colourIndex];
            counter.text = $"{callNumber} / {calls}";
            SetTimeLeft(1f);
            Cool();
        }

        // share runs 1 -> 0 as the call runs out.
        public void SetTimeLeft(float share)
        {
            timeBar.localScale = new Vector3(Mathf.Clamp01(share), 1f, 1f);
        }

        // The call is over: every plate not of the safe colour turns hot.
        public void HeatAllBut(int safeColourIndex, string words)
        {
            calledWords.text = words;
            calledWords.color = hotColour;
            foreach (HotPlatesPlate plate in plates)
            {
                if (plate.ColourIndex == safeColourIndex) continue;
                plate.Paint(hotColour);
            }

            SetTimeLeft(0f);
        }

        private void Cool()
        {
            foreach (HotPlatesPlate plate in plates) plate.Paint(DishTint(plate.ColourIndex));
        }

        // The white plate picture tinted towards its colour.
        private Color DishTint(int colourIndex)
        {
            return Color.Lerp(Color.white, plateColours[colourIndex], dishShare);
        }
    }
}
