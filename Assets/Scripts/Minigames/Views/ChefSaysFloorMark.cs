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
    // called way in turn, so the run reads as "go this way". A faint ingredient waits past the last
    // chevron; it pops to full colour when the player reaches it.
    // After a call the mark shows that player's own result where they stand: the good part (tick
    // badge) or the bad part (cross badge) pops in, holds, then fades. A right call also flashes a
    // green glow and two shockwave rings behind the badge, pops gold sparkle stars around it and
    // throws round and strip confetti; a wrong call shakes the cross badge over a red flash and ring,
    // puffs dark smoke and drops a spray of red and grey chips.
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
        // Chevrons always shown before the ingredient, even when neighbours stand close.
        [SerializeField, Min(1)] private int minChevrons = 2;
        [SerializeField] private CanvasGroup good;
        [SerializeField] private CanvasGroup bad;
        [SerializeField] private float popSeconds = 0.25f;
        [SerializeField] private float popOvershoot = 1.2f;
        [SerializeField] private float holdSeconds = 0.75f;
        [SerializeField] private float fadeSeconds = 0.3f;
        // Layers played together around the badge on a right call, and on a wrong one.
        [SerializeField] private FloorBurstStyle[] cheerEffects = CheerEffects();
        [SerializeField] private FloorBurstStyle[] failEffects = FailEffects();
        // Side-to-side shake of the wrong badge, in floor units, and how long it lasts.
        [SerializeField] private float shakeDistance = 14f;
        [SerializeField] private float shakeSeconds = 0.4f;
        // The ingredient waiting at the end of the chevrons: faint until the player reaches it, then it
        // pops to full colour and stays there until the call ends.
        [SerializeField] private Image ingredient;
        [SerializeField, Range(0f, 1f)] private float ingredientWaitingAlpha = 0.45f;
        // A slow size pulse while it waits, as a share of its size.
        [SerializeField] private float ingredientPulse = 0.06f;
        [SerializeField] private float ingredientPopSeconds = 0.3f;
        [SerializeField] private float ingredientPopScale = 1.35f;
        [SerializeField] private float ingredientCollectedScale = 1.15f;

        private readonly List<Chevron> left = new List<Chevron>();
        private readonly List<Chevron> right = new List<Chevron>();
        private List<Chevron> pointing;
        private float pointSide;
        private CanvasGroup result;
        private float resultTime;
        private FloorBurst burst;
        private bool ingredientCollected;
        private float ingredientTime;

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
        // The chevrons take playerColour, keeping their authored transparency, so each player
        // can tell their own arrows apart.
        public void Setup(float maxReach, Color playerColour)
        {
            Tint(leftChevron, playerColour);
            Tint(rightChevron, playerColour);
            Fill(leftChevron, left, -1f, maxReach);
            Fill(rightChevron, right, 1f, maxReach);
            burst = new FloorBurst((RectTransform)transform);
            Hide();
        }

        // Shows the chevrons on the called side that fit wholly within reach floor units of the
        // centre, counting their width and how far the wave nudges them.
        // Always shows at least minChevrons chevrons. Returns how far from the centre the last shown
        // chevron's far side reaches, in floor units: where the ingredient goes.
        public float Point(bool toRight, float reach)
        {
            HideResult();
            List<Chevron> run = toRight ? right : left;
            if (run.Count > 0) reach = Mathf.Max(reach, OuterEdge(run[Mathf.Clamp(minChevrons, 1, run.Count) - 1]));
            Show(left, !toRight, reach);
            Show(right, toRight, reach);
            pointing = run;
            pointSide = toRight ? 1f : -1f;

            float tip = 0f;
            foreach (Chevron chevron in run)
                if (chevron.Image.enabled) tip = Mathf.Max(tip, OuterEdge(chevron));
            return tip;
        }

        // Puts the faint ingredient picture centre floor units out on the called side.
        public void ShowIngredient(Sprite picture, bool toRight, float centre)
        {
            if (ingredient == null) return;
            ingredient.sprite = picture;
            ingredient.rectTransform.anchoredPosition = new Vector2((toRight ? 1f : -1f) * centre, 0f);
            ingredientCollected = false;
            ingredientTime = 0f;
            ingredient.enabled = picture != null;
            ShowIngredientFrame();
        }

        // The player has reached the ingredient: it pops to full colour.
        public void CollectIngredient()
        {
            if (ingredient == null || !ingredient.enabled || ingredientCollected) return;
            ingredientCollected = true;
            ingredientTime = 0f;
            ShowIngredientFrame();
        }

        // Hides the chevrons and shows whether this player got the call right.
        public void ShowResult(bool correct)
        {
            StopPointing();
            HideIngredient();
            HideResult();
            result = correct ? good : bad;
            result.gameObject.SetActive(true);
            resultTime = 0f;
            burst.Play(correct ? cheerEffects : failEffects);
            ShowResultFrame();
        }

        public void Hide()
        {
            StopPointing();
            HideIngredient();
            HideResult();
            burst?.Clear();
        }

        private void Update()
        {
            if (pointing != null) Wave();
            if (ingredient != null && ingredient.enabled)
            {
                ingredientTime += Time.unscaledDeltaTime;
                ShowIngredientFrame();
            }
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

        // Waiting: faint with a slow pulse. Collected: pops past full size and settles a little larger.
        private void ShowIngredientFrame()
        {
            float scale;
            Color colour = Color.white;
            if (!ingredientCollected)
            {
                scale = 1f + Mathf.Sin(ingredientTime * Mathf.PI * 2f * 0.8f) * ingredientPulse;
                colour.a = ingredientWaitingAlpha;
            }
            else if (ingredientTime < ingredientPopSeconds)
            {
                scale = Mathf.Lerp(1f, ingredientPopScale, ingredientTime / ingredientPopSeconds);
            }
            else
            {
                float settle = Mathf.Clamp01((ingredientTime - ingredientPopSeconds) / ingredientPopSeconds);
                scale = Mathf.Lerp(ingredientPopScale, ingredientCollectedScale, settle);
            }

            ingredient.color = colour;
            ingredient.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        private void HideIngredient()
        {
            if (ingredient != null) ingredient.enabled = false;
            ingredientCollected = false;
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

        // The badge sits 100 units in front of the player's feet and is 60 units wide.
        private static readonly Vector2 BadgeCentre = new Vector2(0f, 100f);

        private static FloorBurstStyle[] CheerEffects()
        {
            Color green = new Color(0.36f, 0.78f, 0.36f, 1f);
            Color gold = new Color(1f, 0.82f, 0.3f, 1f);
            Color cream = new Color(0.99f, 0.91f, 0.78f, 1f);
            return new[]
            {
                new FloorBurstStyle(label: "Glow", shape: FloorBurstShape.Glow,
                    colours: new[] { new Color(0.6f, 1f, 0.5f, 0.85f) }, count: 1, origin: BadgeCentre, startRadius: 0f,
                    speed: Vector2.zero, gravity: 0f, drag: 1f, size: new Vector2(110f, 110f), aspect: 1f,
                    lifetime: new Vector2(0.4f, 0.4f), delay: Vector2.zero, startScale: 0.5f, peakScale: 1.7f,
                    peakShare: 1f, endScale: 1.7f, fadeFrom: 0.1f, spin: 0f, aimAngle: 90f, spreadAngle: 0f, behind: true),
                new FloorBurstStyle(label: "Ring", shape: FloorBurstShape.Ring,
                    colours: new[] { green }, count: 1, origin: BadgeCentre, startRadius: 0f,
                    speed: Vector2.zero, gravity: 0f, drag: 1f, size: new Vector2(64f, 64f), aspect: 1f,
                    lifetime: new Vector2(0.5f, 0.5f), delay: Vector2.zero, startScale: 0.9f, peakScale: 2.6f,
                    peakShare: 1f, endScale: 2.6f, fadeFrom: 0.25f, spin: 0f, aimAngle: 90f, spreadAngle: 0f, behind: true),
                new FloorBurstStyle(label: "Late Ring", shape: FloorBurstShape.Ring,
                    colours: new[] { gold }, count: 1, origin: BadgeCentre, startRadius: 0f,
                    speed: Vector2.zero, gravity: 0f, drag: 1f, size: new Vector2(64f, 64f), aspect: 1f,
                    lifetime: new Vector2(0.45f, 0.45f), delay: new Vector2(0.1f, 0.1f), startScale: 0.9f, peakScale: 2f,
                    peakShare: 1f, endScale: 2f, fadeFrom: 0.2f, spin: 0f, aimAngle: 90f, spreadAngle: 0f, behind: true),
                new FloorBurstStyle(label: "Stars", shape: FloorBurstShape.Star,
                    colours: new[] { gold, Color.white, new Color(1f, 0.95f, 0.6f, 1f) }, count: 7, origin: BadgeCentre,
                    startRadius: 36f, speed: new Vector2(50f, 130f), gravity: 0f, drag: 0.1f, size: new Vector2(22f, 38f),
                    aspect: 1f, lifetime: new Vector2(0.5f, 0.75f), delay: new Vector2(0f, 0.15f), startScale: 0f,
                    peakScale: 1f, peakShare: 0.25f, endScale: 0f, fadeFrom: 0.75f, spin: 160f, aimAngle: 90f,
                    spreadAngle: 360f, behind: false),
                new FloorBurstStyle(label: "Dots", shape: FloorBurstShape.Circle,
                    colours: new[] { green, gold, cream }, count: 12, origin: BadgeCentre, startRadius: 26f,
                    speed: new Vector2(200f, 380f), gravity: 0f, drag: 0.06f, size: new Vector2(8f, 13f), aspect: 1f,
                    lifetime: new Vector2(0.6f, 0.9f), delay: Vector2.zero, startScale: 1f, peakScale: 1f, peakShare: 0f,
                    endScale: 0f, fadeFrom: 0.6f, spin: 0f, aimAngle: 90f, spreadAngle: 360f, behind: false),
                new FloorBurstStyle(label: "Strips", shape: FloorBurstShape.Square,
                    colours: new[] { green, gold, cream }, count: 10, origin: BadgeCentre, startRadius: 26f,
                    speed: new Vector2(220f, 420f), gravity: 0f, drag: 0.08f, size: new Vector2(9f, 14f), aspect: 0.45f,
                    lifetime: new Vector2(0.7f, 1f), delay: new Vector2(0f, 0.05f), startScale: 1f, peakScale: 1f,
                    peakShare: 0f, endScale: 0.2f, fadeFrom: 0.6f, spin: 720f, aimAngle: 90f, spreadAngle: 360f, behind: false),
            };
        }

        private static FloorBurstStyle[] FailEffects()
        {
            Color red = new Color(0.9f, 0.27f, 0.2f, 1f);
            return new[]
            {
                new FloorBurstStyle(label: "Flash", shape: FloorBurstShape.Glow,
                    colours: new[] { new Color(1f, 0.3f, 0.22f, 0.8f) }, count: 1, origin: BadgeCentre, startRadius: 0f,
                    speed: Vector2.zero, gravity: 0f, drag: 1f, size: new Vector2(110f, 110f), aspect: 1f,
                    lifetime: new Vector2(0.3f, 0.3f), delay: Vector2.zero, startScale: 0.6f, peakScale: 1.4f,
                    peakShare: 1f, endScale: 1.4f, fadeFrom: 0f, spin: 0f, aimAngle: 90f, spreadAngle: 0f, behind: true),
                new FloorBurstStyle(label: "Ring", shape: FloorBurstShape.Ring,
                    colours: new[] { red }, count: 1, origin: BadgeCentre, startRadius: 0f,
                    speed: Vector2.zero, gravity: 0f, drag: 1f, size: new Vector2(64f, 64f), aspect: 1f,
                    lifetime: new Vector2(0.32f, 0.32f), delay: Vector2.zero, startScale: 1f, peakScale: 1.8f,
                    peakShare: 1f, endScale: 1.8f, fadeFrom: 0f, spin: 0f, aimAngle: 90f, spreadAngle: 0f, behind: true),
                new FloorBurstStyle(label: "Smoke", shape: FloorBurstShape.Glow,
                    colours: new[] { new Color(0.22f, 0.19f, 0.19f, 0.65f), new Color(0.38f, 0.35f, 0.34f, 0.55f) },
                    count: 6, origin: BadgeCentre, startRadius: 18f, speed: new Vector2(25f, 60f), gravity: 40f,
                    drag: 0.25f, size: new Vector2(42f, 62f), aspect: 1f, lifetime: new Vector2(0.7f, 1f),
                    delay: new Vector2(0f, 0.08f), startScale: 0.4f, peakScale: 1.2f, peakShare: 1f, endScale: 1.2f,
                    fadeFrom: 0.2f, spin: 40f, aimAngle: 90f, spreadAngle: 360f, behind: true),
                new FloorBurstStyle(label: "Chips", shape: FloorBurstShape.Hexagon,
                    colours: new[] { red, new Color(0.55f, 0.16f, 0.12f, 1f), new Color(0.45f, 0.42f, 0.4f, 1f) },
                    count: 8, origin: BadgeCentre, startRadius: 20f, speed: new Vector2(130f, 240f), gravity: -520f,
                    drag: 0.3f, size: new Vector2(7f, 12f), aspect: 0.75f, lifetime: new Vector2(0.45f, 0.7f),
                    delay: Vector2.zero, startScale: 1f, peakScale: 1f, peakShare: 0f, endScale: 0.3f, fadeFrom: 0.5f,
                    spin: 400f, aimAngle: 90f, spreadAngle: 160f, behind: false),
            };
        }

        private static void Tint(Image chevron, Color colour)
        {
            colour.a = chevron.color.a;
            chevron.color = colour;
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
