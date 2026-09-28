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
        private Vector3 restingPosition;
        private float idleTime;
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

        public void Hide()
        {
            if (hiding || !isActiveAndEnabled) return;
            hiding = true;
            if (motion != null) StopCoroutine(motion);
            motion = StartCoroutine(Conceal());
        }

        private void LateUpdate()
        {
            if (!idleMotion || hiding || motion != null) return;
            idleTime += Time.unscaledDeltaTime;
            float fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(idleTime / .6f));
            float phase = transform.GetSiblingIndex() * .85f;
            // A small vertical drift keeps text size stable and neighboring cards out of sync.
            transform.localPosition = restingPosition + Vector3.up *
                (Mathf.Sin(idleTime * 1.35f + phase) * 3.5f * fade);
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
            transform.localScale = restingScale;
            transform.localPosition = restingPosition;
            IsHidden = true;
            HasRevealed = false;
        }
    }
}
