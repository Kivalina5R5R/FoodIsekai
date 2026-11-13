using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Minigames.Views
{
    // Health Inspector on the wall and floor (prefab Assets/Prefab/Minigame/Inspector).
    // Wall: the inspector in the middle (his back or front picture with its own tint), his speech
    // and the time left each in a capsule beside him, and a red wash over the wall while he stares.
    // Floor: the start strip (far from the wall) where everyone lines up, the counter strip (by the
    // wall) they walk to, and a pulsing red mark that follows anyone he catches moving. The game
    // reads both strips' areas from here, so moving or resizing them in the prefab moves the zones.
    public sealed class InspectorView : MinigameView
    {
        [SerializeField] private Image inspector;
        [SerializeField] private Sprite backSprite;
        [SerializeField] private Color backColour = Color.white;
        [SerializeField] private Sprite frontSprite;
        [SerializeField] private Color frontColour = Color.white;
        [SerializeField] private GameObject speechPanel;
        [SerializeField] private TMP_Text speech;
        [SerializeField] private Color humColour = new Color(0.99f, 0.91f, 0.78f, 1f);
        [SerializeField] private Color warnColour = new Color(0.88f, 0.72f, 0.47f, 1f);
        [SerializeField] private Color stareColour = new Color(0.9f, 0.27f, 0.2f, 1f);
        [SerializeField] private GameObject clockPanel;
        [SerializeField] private TMP_Text clock;
        [SerializeField] private Image redTint;
        [SerializeField] private RectTransform startZone;
        [SerializeField] private TMP_Text startWords;
        [SerializeField] private RectTransform counterZone;
        [SerializeField] private TMP_Text counterWords;
        [SerializeField] private InspectorCaughtMark caughtTemplate;

        // The strips' areas in arena metres (x from the left, y from the wall).
        public Rect StartArea { get; private set; }
        public Rect CounterArea { get; private set; }

        protected override void OnBound()
        {
            StartArea = ArenaRect(startZone);
            CounterArea = ArenaRect(counterZone);
            redTint.enabled = false;
            SetClock(string.Empty);
            ShowStart(false);
            ShowCounter(false);
        }

        public void SetZoneWords(string start, string counter)
        {
            startWords.text = start;
            counterWords.text = counter;
        }

        public void ShowStart(bool shown)
        {
            startZone.gameObject.SetActive(shown);
        }

        public void ShowCounter(bool shown)
        {
            counterZone.gameObject.SetActive(shown);
        }

        // Empty text hides the clock.
        public void SetClock(string text)
        {
            clock.text = text;
            clockPanel.SetActive(!string.IsNullOrEmpty(text));
        }

        // His back is turned and he says something calm (humming, or calling everyone to the start).
        public void ShowBack(string words)
        {
            ShowInspector(backSprite, backColour);
            SetSpeech(words, humColour);
            redTint.enabled = false;
        }

        // He half-turns with a warning.
        public void ShowWarning(string warning)
        {
            ShowInspector(frontSprite, frontColour);
            SetSpeech(warning, warnColour);
        }

        // He stares: the wall washes red.
        public void ShowStare(string shout)
        {
            ShowInspector(frontSprite, frontColour);
            SetSpeech(shout, stareColour);
            redTint.enabled = true;
        }

        public void HideStare()
        {
            redTint.enabled = false;
        }

        // The pulsing red mark that follows a caught player on the floor.
        public void MarkCaught(MinigamePlayer player)
        {
            Spawn(caughtTemplate).Follow(player, Stage);
        }

        private Rect ArenaRect(RectTransform zone)
        {
            var corners = new Vector3[4];
            zone.GetWorldCorners(corners);
            Vector2 min = Vector2.positiveInfinity;
            Vector2 max = Vector2.negativeInfinity;
            foreach (Vector3 corner in corners)
            {
                Vector2 arena = Stage.ArenaPoint(Floor.InverseTransformPoint(corner));
                min = Vector2.Min(min, arena);
                max = Vector2.Max(max, arena);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private void ShowInspector(Sprite sprite, Color colour)
        {
            inspector.sprite = sprite;
            inspector.color = colour;
        }

        private void SetSpeech(string text, Color colour)
        {
            speech.text = text;
            speech.color = colour;
            speechPanel.SetActive(!string.IsNullOrEmpty(text));
        }
    }
}
