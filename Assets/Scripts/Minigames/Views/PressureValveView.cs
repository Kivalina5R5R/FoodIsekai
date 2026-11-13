using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Minigames.Views
{
    // Pressure Pot Roulette on the wall and floor (prefab Assets/Prefab/Minigame/PressureValve).
    // Wall: the pot picture, a pressure gauge whose needle rises as valves open, the turn's
    // countdown in a capsule high in the middle (under the meal clock) and the turn status in a
    // capsule below it. Each capsule hides while it has nothing to say.
    // Floor: the pot (a dark disc with a ring in the turning player's colour), placed where the game
    // says, and one PressureValveMark per valve cloned from the template.
    public sealed class PressureValveView : MinigameView
    {
        [SerializeField] private RectTransform floorPot;
        [SerializeField] private Image potRing;
        [SerializeField] private Image potPicture;
        [SerializeField] private RectTransform needle;
        [SerializeField] private GameObject statusPanel;
        [SerializeField] private TMP_Text status;
        [SerializeField] private GameObject clockPanel;
        [SerializeField] private TMP_Text clock;
        [SerializeField] private PressureValveMark valveTemplate;
        // Needle angle either side of straight up, from empty (left) to full (right).
        [SerializeField] private float needleSweep = 80f;
        [SerializeField] private Color safeColour = new Color(0.36f, 0.78f, 0.36f, 1f);
        [SerializeField] private Color boomColour = new Color(0.9f, 0.27f, 0.2f, 1f);

        protected override void OnBound()
        {
            SetStatus(string.Empty, status.color);
            ShowClock(string.Empty);
            SetPressure(0f);
        }

        // Moves the floor pot so its centre sits on this arena point.
        public void PlacePot(Vector2 arena)
        {
            PlaceOnFloor(floorPot, arena);
        }

        // A valve on the floor at this arena point, numbered from 1.
        public PressureValveMark AddValve(Vector2 arena, int valveNumber)
        {
            PressureValveMark valve = Spawn(valveTemplate);
            PlaceOnFloor((RectTransform)valve.transform, arena);
            valve.Setup(valveNumber);
            return valve;
        }

        // share of the valves opened so far, 0 -> 1.
        public void SetPressure(float share)
        {
            needle.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(needleSweep, -needleSweep, Mathf.Clamp01(share)));
        }

        public void ShowTurn(string words, Color playerColour)
        {
            potRing.color = playerColour;
            SetStatus(words, playerColour);
        }

        public void ShowClock(string seconds)
        {
            clock.text = seconds;
            clockPanel.SetActive(!string.IsNullOrEmpty(seconds));
        }

        public void ShowSafe(string words)
        {
            ShowClock(string.Empty);
            SetStatus(words, safeColour);
        }

        public void ShowBoom(string words)
        {
            ShowClock(string.Empty);
            potPicture.color = boomColour;
            SetStatus(words, boomColour);
        }

        private void SetStatus(string words, Color colour)
        {
            status.text = words;
            status.color = colour;
            statusPanel.SetActive(!string.IsNullOrEmpty(words));
        }
    }
}
