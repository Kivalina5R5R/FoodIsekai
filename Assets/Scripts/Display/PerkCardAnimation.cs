using System.Collections;
using UnityEngine;

namespace FoodIsekaiZ.Display
{
    // Animates relative to the slot's authored scale, keeping its layout intact.
    public sealed class PerkCardAnimation : MonoBehaviour
    {
        [SerializeField] private PerkCardSparkles sparkles;
        [SerializeField] private bool idleMotion = true;
        [SerializeField] private CustomerPanelSuccessParticles vanishParticles;
        private Vector3 restingScale;
        private const float ShakeSeconds = .45f;
        private Vector3 restingPosition;
        private float idleTime;
        private float shakeAge = ShakeSeconds;
        private bool occupied;
        private float occupiedAge;
        private float swell = 1f;
        private float leaveFrom = 1f;
        private float leaveAge = 1f;
        private Coroutine motion;
        private bool hiding;
        public bool IsHidden { get; private set; }
        public bool HasRevealed { get; private set; }

        private void Awake()
        {
            restingScale = transform.localScale;
            restingPosition = transform.localPosition;
        }

        private void OnEnable()
        {
            hiding = false;
            IsHidden = false;
            HasRevealed = false;
            idleTime = 0f;
            if (vanishParticles != null) vanishParticles.Stop();
            motion = StartCoroutine(Reveal());
        }

        private IEnumerator Reveal()
        {
            transform.localScale = Vector3.zero;
            float delay = Mathf.Min(transform.GetSiblingIndex(), 3) * .12f;
            while (delay > 0f)
            {
                delay -= Time.unscaledDeltaTime;
                yield return null;
            }
            yield return ScaleTo(1.08f, .55f, true);
            yield return ScaleTo(1f, .4f);
            HasRevealed = true;
            motion = null;
        }

        // A quick decaying side-to-side "no" shake, used when players cannot afford this card.
        public void Shake()
        {
            if (hiding || !isActiveAndEnabled) return;
            shakeAge = 0f;
        }

        // Matches the player-selection cards: swell on entry, then a gentle breathing pulse while occupied,
        // and ease back to the resting size on leave. Safe to call every frame; only changes restart motion.
        public void SetOccupied(bool value)
        {
            if (value == occupied) return;
            occupied = value;
            occupiedAge = 0f;
            leaveFrom = swell;
            leaveAge = 0f;
        }

        public void Hide()
        {
            if (hiding || !isActiveAndEnabled) return;
            hiding = true;
            if (motion != null) StopCoroutine(motion);
            motion = StartCoroutine(Conceal());
        }

        private void LateUpdate()
        {
            bool shaking = shakeAge < ShakeSeconds;
            bool swelling = occupied || leaveAge < .16f;
            if (hiding || motion != null || (!idleMotion && !shaking && !swelling)) return;
            Vector3 offset = Vector3.zero;
            if (idleMotion)
            {
                idleTime += Time.unscaledDeltaTime;
                float fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(idleTime / .6f));
                float phase = transform.GetSiblingIndex() * .85f;
                // A small vertical drift keeps text size stable and neighboring cards out of sync.
                offset.y = Mathf.Sin(idleTime * 1.35f + phase) * 3.5f * fade;
            }
            if (shaking)
            {
                shakeAge = Mathf.Min(ShakeSeconds, shakeAge + Time.unscaledDeltaTime);
                float decay = 1f - shakeAge / ShakeSeconds;
                offset.x = Mathf.Sin(shakeAge * 55f) * 10f * decay * decay;
            }
            swell = OccupiedSwell(Time.unscaledDeltaTime);
            // The offset and swell always resolve back to the authored resting position and scale.
            transform.localPosition = restingPosition + offset;
            transform.localScale = restingScale * swell;
        }

        // Same timings as ReadyCardConfirmation: 1.09 in .14s, settle to 1.035 in .18s, then ±1.2% breathing.
        private float OccupiedSwell(float deltaTime)
        {
            if (occupied)
            {
                occupiedAge += deltaTime;
                if (occupiedAge < .14f) return Mathf.Lerp(leaveFrom, 1.09f, Smooth(occupiedAge / .14f));
                if (occupiedAge < .32f) return Mathf.Lerp(1.09f, 1.035f, Smooth((occupiedAge - .14f) / .18f));
                return 1.035f + Mathf.Sin((occupiedAge - .32f) * 5f) * .012f;
            }
            leaveAge = Mathf.Min(.16f, leaveAge + deltaTime);
            return Mathf.Lerp(leaveFrom, 1f, Smooth(leaveAge / .16f));
        }

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private IEnumerator Conceal()
        {
            if (sparkles != null) sparkles.StopEmitting();
            yield return ScaleTo(1.08f, .35f);
            yield return ScaleTo(0f, .58f);
            if (vanishParticles != null)
            {
                vanishParticles.Play();
                while (vanishParticles.IsPlaying) yield return null;
            }
            IsHidden = true;
            motion = null;
        }

        private IEnumerator ScaleTo(float target, float seconds, bool burstDuringGrowth = false)
        {
            Vector3 from = transform.localScale;
            float elapsed = 0f;
            bool burstPlayed = false;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / seconds);
                // Match the menu's zero-velocity, zero-acceleration easing at both ends.
                float eased = t * t * t * (t * (6f * t - 15f) + 10f);
                transform.localScale = Vector3.LerpUnclamped(from, restingScale * target, eased);
                if (burstDuringGrowth && !burstPlayed && t >= .65f)
                {
                    burstPlayed = true;
                    if (sparkles != null) sparkles.Burst();
                }
                yield return null;
            }
            transform.localScale = restingScale * target;
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            if (vanishParticles != null) vanishParticles.Stop();
            motion = null;
            shakeAge = ShakeSeconds;
            occupied = false;
            swell = 1f;
            leaveAge = 1f;
            transform.localScale = restingScale;
            transform.localPosition = restingPosition;
            IsHidden = true;
            HasRevealed = false;
        }
    }
}
