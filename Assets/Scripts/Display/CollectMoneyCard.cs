using FoodIsekaiZ.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // The wall's "collect the money" sign for one customer seat: glowing coins with an arrow pop up where
    // the order card was while that customer's money waits on the floor, so players see it and walk over,
    // and pop away once the money is taken. Auto Bank money is collected by itself, so no sign shows for
    // it. The motion is gentle: the arrow bobs, the light behind the coins turns and pulses, the sign
    // breathes and the sparkles float up. Only offsets from the authored positions, rotations, scales and
    // colours are applied, never new layout.
    public sealed class CollectMoneyCard : MonoBehaviour
    {
        [SerializeField] private FoodIsekaiZGameManager gameManager;
        // Which customer seat this card belongs to, counted from 0 like CustomerPanel1 = 0.
        [SerializeField, Min(0)] private int seatIndex;
        // The card itself, shown and hidden as a whole.
        [SerializeField] private RectTransform card;
        [SerializeField] private RectTransform arrow;
        // The golden light behind the coins; it turns slowly and glows softly brighter and dimmer.
        [SerializeField] private RectTransform glow;
        [SerializeField] private Graphic[] sparkles = new Graphic[0];

        [Header("Motion")]
        [SerializeField, Min(0.05f)] private float popSeconds = 0.35f;
        [SerializeField, Min(0.05f)] private float leaveSeconds = 0.2f;
        [SerializeField, Min(0f)] private float arrowBobPixels = 6f;
        [SerializeField, Min(0.1f)] private float arrowBobSeconds = 0.9f;
        [SerializeField, Range(0f, 0.1f)] private float breatheAmount = 0.025f;
        [SerializeField, Min(0.1f)] private float breatheSeconds = 1.6f;
        [SerializeField, Min(0.1f)] private float twinkleSeconds = 1.3f;
        // Seconds for the light to turn once; negative turns it the other way.
        [SerializeField] private float glowTurnSeconds = 8f;
        [SerializeField, Range(0f, 0.3f)] private float glowPulseAmount = 0.08f;
        // How far each sparkle floats up while it twinkles before it fades and starts again.
        [SerializeField, Min(0f)] private float sparkleRisePixels = 22f;

        private Vector3 cardScale;
        private Vector2 arrowPosition;
        private Quaternion glowRotation;
        private Vector3 glowScale;
        private float[] sparkleAlpha;
        private Vector3[] sparkleScale;
        private Vector2[] sparklePosition;
        // 0 hidden, 1 fully shown; it climbs while the money waits and falls once it is gone.
        private float presence;
        private float age;
        private bool showing;

        private void Awake()
        {
            if (card != null) cardScale = card.localScale;
            if (arrow != null) arrowPosition = arrow.anchoredPosition;
            if (glow != null)
            {
                glowRotation = glow.localRotation;
                glowScale = glow.localScale;
            }
            sparkleAlpha = new float[sparkles.Length];
            sparkleScale = new Vector3[sparkles.Length];
            sparklePosition = new Vector2[sparkles.Length];
            for (int i = 0; i < sparkles.Length; i++)
            {
                if (sparkles[i] == null) continue;
                sparkleAlpha[i] = sparkles[i].color.a;
                sparkleScale[i] = sparkles[i].rectTransform.localScale;
                sparklePosition[i] = sparkles[i].rectTransform.anchoredPosition;
            }
            presence = 0f;
            if (card != null) card.gameObject.SetActive(false);
        }

        private void Update()
        {
            bool wanted = IsMoneyWaiting();
            if (wanted && !showing)
            {
                showing = true;
                age = 0f;
                if (card != null) card.gameObject.SetActive(true);
            }
            else if (!wanted && showing)
            {
                showing = false;
            }

            float step = Time.deltaTime / (showing ? popSeconds : leaveSeconds);
            presence = Mathf.Clamp01(presence + (showing ? step : -step));
            age += Time.deltaTime;
            if (!showing && presence <= 0f)
            {
                if (card != null && card.gameObject.activeSelf) card.gameObject.SetActive(false);
                return;
            }
            Animate();
        }

        private bool IsMoneyWaiting()
        {
            if (gameManager == null) return false;
            MealWavePhase phase = gameManager.CurrentMealWavePhase;
            if (phase == MealWavePhase.Intermission || phase == MealWavePhase.Minigame ||
                phase == MealWavePhase.Completed) return false;
            ArenaSlot2D slot = gameManager.GetCustomerSlot(seatIndex);
            return slot != null && slot.CustomerState == CustomerSlotState.MoneyAvailable &&
                !slot.IsAutomaticCollectionPending;
        }

        private void Animate()
        {
            // Pops in with a small overshoot; leaves by shrinking straight down.
            float pop = showing ? BackOut(presence) : presence * presence;
            float breathe = 1f + breatheAmount * Mathf.Sin(age * Mathf.PI * 2f / breatheSeconds);
            if (card != null) card.localScale = cardScale * (pop * breathe);

            if (arrow != null)
            {
                float bob = (0.5f - 0.5f * Mathf.Cos(age * Mathf.PI * 2f / arrowBobSeconds)) * arrowBobPixels;
                arrow.anchoredPosition = arrowPosition + Vector2.down * bob;
            }

            if (glow != null)
            {
                float turn = Mathf.Abs(glowTurnSeconds) > 0.01f ? age * 360f / glowTurnSeconds : 0f;
                glow.localRotation = glowRotation * Quaternion.Euler(0f, 0f, -turn);
                float pulse = 1f + glowPulseAmount * Mathf.Sin(age * Mathf.PI * 2f / breatheSeconds);
                glow.localScale = glowScale * pulse;
            }

            for (int i = 0; i < sparkles.Length; i++)
            {
                Graphic sparkle = sparkles[i];
                if (sparkle == null) continue;
                // Each sparkle floats up and twinkles on its own beat so they never flash together.
                float beat = Mathf.Repeat(age / twinkleSeconds + i * 0.37f, 1f);
                float twinkle = Mathf.Sin(beat * Mathf.PI);
                Color color = sparkle.color;
                color.a = sparkleAlpha[i] * twinkle;
                sparkle.color = color;
                sparkle.rectTransform.localScale = sparkleScale[i] * (0.6f + 0.4f * twinkle);
                sparkle.rectTransform.anchoredPosition = sparklePosition[i] + Vector2.up * (beat * sparkleRisePixels);
            }
        }

        private static float BackOut(float t)
        {
            const float overshoot = 1.4f;
            float back = t - 1f;
            return 1f + (overshoot + 1f) * back * back * back + overshoot * back * back;
        }

        private void OnDisable()
        {
            presence = 0f;
            showing = false;
            if (card != null)
            {
                card.localScale = cardScale;
                card.gameObject.SetActive(false);
            }
            if (arrow != null) arrow.anchoredPosition = arrowPosition;
            if (glow != null)
            {
                glow.localRotation = glowRotation;
                glow.localScale = glowScale;
            }
            for (int i = 0; i < sparkles.Length; i++)
                if (sparkles[i] != null) sparkles[i].rectTransform.anchoredPosition = sparklePosition[i];
        }
    }
}
