using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Minigames.Views
{
    // One player's arrows on the Chef Says floor: a run of chevrons pointing the called way, starting
    // at the player's feet. The template has one chevron each way; more are cloned outwards every
    // chevronSpacing units up to the longest reach the game allows. Each call the game moves the mark
    // to where the player stands now and shows only the chevrons that stay clear of their neighbours.
    // While pointing, a wave runs outwards along the chevrons: each one brightens and nudges the
    // called way in turn, so the run reads as "go this way".
    // After a call the mark shows that player's own result where they stand: the good part (tick
    // badge) or the bad part (cross badge) pops in, holds, then fades. A right call also throws a
    // burst of green and gold confetti from the player's feet; a wrong call shakes the cross badge
    // and drops a spray of red and grey shards.
    public sealed class ChefSaysFloorMark : MonoBehaviour
    {
        [SerializeField] private Image leftChevron;
        [SerializeField] private Image rightChevron;
        [SerializeField] private float chevronSpacing = 30f;
        // Waves per second running outwards along the chevrons.
        [SerializeField] private float waveSpeed = 1.4f;
        // How far behind its inner neighbour each chevron lights up, as a share of one wave.
        [SerializeField] private float waveLag = 0.16f;
        // How far a chevron moves the called way at the top of the wave, in floor units.
        [SerializeField] private float waveNudge = 10f;
        [SerializeField, Range(0f, 1f)] private float dimAlpha = 0.3f;
        [SerializeField] private CanvasGroup good;
        [SerializeField] private CanvasGroup bad;
        [SerializeField] private float popSeconds = 0.25f;
        [SerializeField] private float popOvershoot = 1.2f;
        [SerializeField] private float holdSeconds = 0.75f;
        [SerializeField] private float fadeSeconds = 0.3f;
        [SerializeField] private FloorBurstStyle cheerBurst = new FloorBurstStyle(
            new[] { new Color(0.36f, 0.78f, 0.36f, 1f), new Color(1f, 0.82f, 0.3f, 1f), new Color(0.99f, 0.91f, 0.78f, 1f) },
            22, new Vector2(220f, 460f), 0f, 0.08f, new Vector2(10f, 18f), new Vector2(0.7f, 1.1f), 720f, 90f, 360f);
        [SerializeField] private FloorBurstStyle failBurst = new FloorBurstStyle(
            new[] { new Color(0.9f, 0.27f, 0.2f, 1f), new Color(0.55f, 0.16f, 0.12f, 1f), new Color(0.45f, 0.42f, 0.4f, 1f) },
            12, new Vector2(120f, 240f), -520f, 0.3f, new Vector2(9f, 15f), new Vector2(0.5f, 0.8f), 300f, 90f, 150f);
        // Side-to-side shake of the wrong badge, in floor units, and how long it lasts.
        [SerializeField] private float shakeDistance = 14f;
        [SerializeField] private float shakeSeconds = 0.4f;

        private readonly List<Chevron> left = new List<Chevron>();
        private readonly List<Chevron> right = new List<Chevron>();
        private List<Chevron> pointing;
        private float pointSide;
        private CanvasGroup result;
        private float resultTime;
        private FloorBurst burst;

        // A chevron with the place and colour it rests at between wave frames.
        private readonly struct Chevron
        {
            public readonly Image Image;
            public readonly float BaseX;
            public readonly Color BaseColour;

            public Chevron(Image image)
            {
                Image = image;
                BaseX = image.rectTransform.anchoredPosition.x;
                BaseColour = image.color;
            }
        }

        // maxReach is in floor units from the centre outwards: the furthest any call may show.
        public void Setup(float maxReach)
        {
            Fill(leftChevron, left, -1f, maxReach);
            Fill(rightChevron, right, 1f, maxReach);
            burst = new FloorBurst((RectTransform)transform);
            Hide();
        }

        // Shows the chevrons on the called side that fit wholly within reach floor units of the
        // centre, counting their width and how far the wave nudges them.
        public void Point(bool toRight, float reach)
        {
            HideResult();
            Show(left, !toRight, reach);
            Show(right, toRight, reach);
            pointing = toRight ? right : left;
            pointSide = toRight ? 1f : -1f;
        }

        // Hides the chevrons and shows whether this player got the call right.
        public void ShowResult(bool correct)
        {
            StopPointing();
            HideResult();
            result = correct ? good : bad;
            result.gameObject.SetActive(true);
            resultTime = 0f;
            burst.Play(correct ? cheerBurst : failBurst);
            ShowResultFrame();
        }

        public void Hide()
        {
            StopPointing();
            HideResult();
            burst?.Clear();
        }

        private void Update()
        {
            if (pointing != null) Wave();
            burst?.Tick(Time.unscaledDeltaTime);
            if (result != null)
            {
                resultTime += Time.unscaledDeltaTime;
                ShowResultFrame();
            }
        }

        private void Wave()
        {
            float time = Time.unscaledTime * waveSpeed;
            for (int i = 0; i < pointing.Count; i++)
            {
                Chevron chevron = pointing[i];
                if (!chevron.Image.enabled) continue;
                float phase = Mathf.Repeat(time - i * waveLag, 1f);
                float glow = 0.5f + 0.5f * Mathf.Cos(phase * Mathf.PI * 2f);
                Color colour = chevron.BaseColour;
                colour.a *= Mathf.Lerp(dimAlpha, 1f, glow);
                chevron.Image.color = colour;
                RectTransform rect = chevron.Image.rectTransform;
                rect.anchoredPosition = new Vector2(chevron.BaseX + pointSide * waveNudge * glow, rect.anchoredPosition.y);
            }
        }

        // Pops the result in past full size and back, holds it, then fades it out.
        private void ShowResultFrame()
        {
            float scale;
            if (resultTime < popSeconds) scale = Mathf.Lerp(0.4f, popOvershoot, resultTime / popSeconds);
            else scale = Mathf.Lerp(popOvershoot, 1f, Mathf.Clamp01((resultTime - popSeconds) / popSeconds));
            result.transform.localScale = new Vector3(scale, scale, 1f);
            ((RectTransform)result.transform).anchoredPosition = new Vector2(ShakeOffset(), 0f);
            float fading = resultTime - popSeconds - holdSeconds;
            result.alpha = fading <= 0f ? 1f : Mathf.Clamp01(1f - fading / fadeSeconds);
            if (result.alpha > 0f) return;
            HideResult();
        }

        // Only the wrong badge shakes: a quick side-to-side wobble that dies away.
        private float ShakeOffset()
        {
            if (result != bad || resultTime >= shakeSeconds) return 0f;
            float fade = 1f - resultTime / shakeSeconds;
            return Mathf.Sin(resultTime * 55f) * shakeDistance * fade;
        }

        private void StopPointing()
        {
            pointing = null;
            Show(left, false, 0f);
            Show(right, false, 0f);
        }

        private void HideResult()
        {
            good.gameObject.SetActive(false);
            bad.gameObject.SetActive(false);
            result = null;
        }

        // Puts every chevron back at its resting place and colour, then shows those in reach.
        private void Show(List<Chevron> run, bool shown, float reach)
        {
            foreach (Chevron chevron in run)
            {
                RectTransform rect = chevron.Image.rectTransform;
                rect.anchoredPosition = new Vector2(chevron.BaseX, rect.anchoredPosition.y);
                chevron.Image.color = chevron.BaseColour;
                chevron.Image.enabled = shown && OuterEdge(chevron) <= reach;
            }
        }

        // How far from the centre a chevron's far side gets at the top of the wave, in floor units.
        private float OuterEdge(Chevron chevron)
        {
            RectTransform rect = chevron.Image.rectTransform;
            float halfWidth = rect.rect.width * Mathf.Abs(rect.localScale.x) * 0.5f;
            return Mathf.Abs(chevron.BaseX) + halfWidth + waveNudge;
        }

        private void Fill(Image first, List<Chevron> run, float side, float reach)
        {
            run.Add(new Chevron(first));
            Vector2 start = first.rectTransform.anchoredPosition;
            for (int k = 1; ; k++)
            {
                Vector2 at = start + new Vector2(side * chevronSpacing * k, 0f);
                if (Mathf.Abs(at.x) > reach) break;
                Image copy = Instantiate(first, first.transform.parent, false);
                copy.rectTransform.anchoredPosition = at;
                run.Add(new Chevron(copy));
            }
        }
    }
}
