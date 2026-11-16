using FoodIsekaiZ.Minigames.Views;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Minigames
{
    // One minigame offer card on the wall, served like a covered dish:
    //   reveal: the card appears face down (a golden serving dome on its back), glowing gold, in the
    //   middle of the wall, slides out to its place, then steams and flips over to its face (cards
    //   flip one after another);
    //   chosen: a wobble, then it spins to the middle of the wall, bursts with light and stars, grows a
    //   little with light rays turning behind it, holds, then is covered again and sinks away in steam;
    //   not chosen: it flips back to its dome side and sinks away in steam.
    // Moves the slot it sits on and returns it to its authored place and scale. Runs on unscaled time.
    public sealed class MinigameCardAnimation : MonoBehaviour
    {
        private enum Phase { Waiting, Revealing, Idle, Chosen, Leaving, Hidden }

        private const float AppearAt = 0.25f;
        private const float AppearSeconds = 0.35f;
        private const float SlideSeconds = 0.5f;
        // The cards start flipping after this, each FlipStagger after the one to its left.
        private const float FlipAt = 1.6f;
        private const float FlipStagger = 0.15f;
        private const float SteamLeadSeconds = 0.3f;
        private const float FlipSeconds = 0.42f;
        private const float WobbleSeconds = 0.2f;
        private const float TravelSeconds = 0.6f;
        private const float CelebrateSeconds = 1.3f;
        private const float CoverSeconds = 0.3f;
        private const float SinkSeconds = 0.3f;
        private const float ChosenScale = 1.2f;
        private const float SinkDepth = 30f;

        private RectTransform slot;
        private RectTransform face;
        private Image back;
        private Image backGlow;
        private Image flash;
        private Image rays;
        private SparkleBurst sparkles;
        private SparkleBurstStyle steamHere;
        private SparkleBurstStyle steamMiddle;
        private SparkleBurstStyle chosenBurst;
        private Vector2 restingPosition;
        private Vector3 restingScale;
        private int order;
        private Phase phase = Phase.Waiting;
        private float age;
        private bool steamed;
        private bool burstPlayed;
        private bool sinking;

        public bool HasRevealed { get; private set; }
        public bool IsHidden => phase == Phase.Hidden;

        // face is the finished card, back the face-down card drawn over it, backGlow a soft light
        // behind the back, flash a burst of white light, rays the light rays behind a chosen card.
        // order is the card's place from the left; it sets when it flips.
        public void Setup(RectTransform face, Image back, Image backGlow, Image flash, Image rays,
            Sprite sparkleSprite, int order)
        {
            slot = (RectTransform)transform;
            this.face = face;
            this.back = back;
            this.backGlow = backGlow;
            this.flash = flash;
            this.rays = rays;
            this.order = order;
            restingPosition = slot.anchoredPosition;
            restingScale = slot.localScale;
            // The stars live beside the card, not on it, so they keep their shape while it spins.
            sparkles = new SparkleBurst((RectTransform)slot.parent, sparkleSprite);
            Vector2 cardSize = slot.rect.size;
            steamHere = Steam(restingPosition, cardSize);
            steamMiddle = Steam(Vector2.zero, cardSize * ChosenScale);
            chosenBurst = new SparkleBurstStyle(26, Vector2.zero, cardSize * 1.2f, new Vector2(30f, 75f), 280f,
                new Vector2(80f, 260f), new Vector2(0.7f, 1.4f), 0.2f);
            phase = Phase.Revealing;
            age = 0f;
            Draw();
        }

        // A soft puff of small warm sparkles rising like steam from a dish.
        private static SparkleBurstStyle Steam(Vector2 at, Vector2 cardSize)
        {
            return new SparkleBurstStyle(16, at + new Vector2(0f, cardSize.y * 0.1f), new Vector2(cardSize.x * 0.8f, cardSize.y * 0.4f),
                new Vector2(20f, 46f), 0f, new Vector2(40f, 120f), new Vector2(0.45f, 0.85f), 0.12f);
        }

        // The chosen card's show; it hides itself when done.
        public void PlayChosen()
        {
            if (phase == Phase.Hidden || phase == Phase.Chosen) return;
            if (slot == null)
            {
                phase = Phase.Hidden;
                return;
            }

            Settle();
            // Drawn over the other cards while it travels to the middle.
            slot.SetAsLastSibling();
            phase = Phase.Chosen;
            age = 0f;
        }

        public void Hide()
        {
            if (phase == Phase.Hidden || phase == Phase.Leaving || phase == Phase.Chosen) return;
            // A card that was never set up has nothing to animate.
            if (slot == null)
            {
                phase = Phase.Hidden;
                return;
            }

            Settle();
            phase = Phase.Leaving;
            age = 0f;
        }

        private void Update()
        {
            if (phase == Phase.Waiting) return;
            float deltaTime = Time.unscaledDeltaTime;
            // Stars already thrown keep drifting after the card itself has gone.
            sparkles.Tick(deltaTime);
            if (phase == Phase.Hidden) return;
            age += deltaTime;
            Draw();
        }

        private void Draw()
        {
            switch (phase)
            {
                case Phase.Revealing: DrawReveal(); break;
                case Phase.Idle: DrawIdle(); break;
                case Phase.Chosen: DrawChosen(); break;
                case Phase.Leaving: DrawCloseAndSink(age, 1f, steamHere); break;
            }
        }

        // Face down at the middle, fading in, sliding out to its place, a puff of steam, then the flip.
        private void DrawReveal()
        {
            float appear = Ease(Mathf.Clamp01((age - AppearAt) / AppearSeconds));
            float slide = Ease(Mathf.Clamp01((age - AppearAt - AppearSeconds) / SlideSeconds));
            slot.anchoredPosition = new Vector2(Mathf.Lerp(0f, restingPosition.x, slide), restingPosition.y);
            float grow = Mathf.Lerp(0.7f, 1f, appear);

            float flipStart = FlipAt + order * FlipStagger;
            if (age >= flipStart - SteamLeadSeconds && !steamed)
            {
                steamed = true;
                sparkles.Play(steamHere);
            }

            float flip = Mathf.Clamp01((age - flipStart) / FlipSeconds);
            ShowFace(flip >= 0.5f);
            float turn = Mathf.Abs(Mathf.Cos(flip * Mathf.PI));
            slot.localScale = new Vector3(restingScale.x * grow * turn, restingScale.y * grow, restingScale.z);

            SetAlpha(back, appear);
            // The back glows brightest while it slides out, then dims before it steams.
            float glow = appear * (1f - Mathf.Clamp01((age - FlipAt + 0.4f) / 0.5f));
            SetAlpha(backGlow, flip >= 0.5f ? 0f : glow);
            SetAlpha(flash, 0f);
            SetAlpha(rays, 0f);

            if (flip < 1f) return;
            HasRevealed = true;
            phase = Phase.Idle;
            age = 0f;
        }

        // A small drift so neighbouring cards are out of step.
        private void DrawIdle()
        {
            float fade = Mathf.Clamp01(age / 0.6f);
            float bob = Mathf.Sin(age * 1.35f + order * 0.85f) * 3.5f * fade;
            slot.anchoredPosition = restingPosition + new Vector2(0f, bob);
        }

        // Wobble, spin across to the middle, burst, show off, then close and sink there.
        private void DrawChosen()
        {
            float t = age;
            float arriveAt = WobbleSeconds + TravelSeconds;
            float closeAt = arriveAt + CelebrateSeconds;
            if (t >= closeAt)
            {
                DrawCloseAndSink(t - closeAt, ChosenScale, steamMiddle);
                SetAlpha(rays, 0.85f * (1f - Mathf.Clamp01((t - closeAt) / CoverSeconds)));
                return;
            }

            float rotation = 0f;
            float turn = 1f;
            float travel = 0f;
            if (t < WobbleSeconds)
            {
                rotation = Mathf.Sin(t / WobbleSeconds * Mathf.PI * 3f) * 6f * (1f - t / WobbleSeconds);
            }
            else
            {
                travel = Ease(Mathf.Clamp01((t - WobbleSeconds) / TravelSeconds));
                turn = Mathf.Cos(travel * Mathf.PI * 2f);
            }

            if (t >= arriveAt && !burstPlayed)
            {
                burstPlayed = true;
                sparkles.Play(chosenBurst);
            }

            float grow = Mathf.Lerp(1f, ChosenScale, travel);
            slot.anchoredPosition = Vector2.Lerp(restingPosition, Vector2.zero, travel);
            slot.localRotation = Quaternion.Euler(0f, 0f, rotation);
            // The dome side faces out while the card is turned away mid-spin.
            ShowFace(turn >= 0f);
            SetAlpha(back, 1f);
            slot.localScale = new Vector3(restingScale.x * grow * Mathf.Abs(turn), restingScale.y * grow, restingScale.z);

            float sinceBurst = t - arriveAt;
            SetAlpha(flash, sinceBurst < 0f ? 0f : Mathf.Clamp01(1f - sinceBurst / 0.45f));
            SetAlpha(rays, Mathf.Clamp01(sinceBurst / 0.3f) * 0.85f);
            if (rays != null) rays.rectTransform.localRotation = Quaternion.Euler(0f, 0f, t * 25f);
        }

        // Flips over to the dome side, then sinks and shrinks away in a puff of steam.
        private void DrawCloseAndSink(float t, float scale, SparkleBurstStyle steam)
        {
            float cover = Mathf.Clamp01(t / CoverSeconds);
            float sink = Ease(Mathf.Clamp01((t - CoverSeconds) / SinkSeconds));
            if (cover >= 1f && !sinking)
            {
                sinking = true;
                sparkles.Play(steam);
            }

            ShowFace(cover < 0.5f);
            SetAlpha(back, 1f);
            float turn = Mathf.Abs(Mathf.Cos(cover * Mathf.PI));
            float size = scale * (1f - sink);
            Vector2 at = phase == Phase.Chosen ? Vector2.zero : restingPosition;
            slot.anchoredPosition = at + Vector2.down * SinkDepth * sink;
            slot.localRotation = Quaternion.identity;
            slot.localScale = new Vector3(restingScale.x * size * turn, restingScale.y * size, restingScale.z);
            SetAlpha(flash, 0f);
            if (sink >= 1f) Finish();
        }

        private void ShowFace(bool showFace)
        {
            face.gameObject.SetActive(showFace);
            back.gameObject.SetActive(!showFace);
        }

        // Puts the card at its finished face-up state before a closing move.
        private void Settle()
        {
            HasRevealed = true;
            ShowFace(true);
            SetAlpha(backGlow, 0f);
            slot.anchoredPosition = restingPosition;
            slot.localScale = restingScale;
        }

        private void Finish()
        {
            phase = Phase.Hidden;
            slot.localScale = Vector3.zero;
            slot.localRotation = Quaternion.identity;
            SetAlpha(flash, 0f);
            SetAlpha(rays, 0f);
        }

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            if (graphic == null) return;
            Color colour = graphic.color;
            colour.a = alpha;
            graphic.color = colour;
        }

        private static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
