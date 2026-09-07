using UnityEngine;

namespace FoodIsekaiZ.Display
{
    /// <summary>Blends a gentle, grounded breathing pose onto an NPC's visual child.</summary>
    /// <remarks>The spawner supplies the visual child; its movement root remains untouched.</remarks>
    [DisallowMultipleComponent]
    public sealed class NpcIdleBreathing : MonoBehaviour
    {
        private const float FullCycleRadians = Mathf.PI * 2f;
        private const float SettleDurationSeconds = 0.3f;
        private const float SettleCompression = 0.004f;

        private RectTransform body;
        private Vector3 authoredPosition;
        private Vector3 authoredScale;
        private Quaternion authoredRotation;
        private Vector3 groundedPoint;
        private float cycleSeconds;
        private float heightAmount;
        private float widthAmount;
        private float blendSeconds;
        private float cyclePhase;
        private float blendWeight;
        private float blendStartWeight;
        private float blendElapsedSeconds;
        private float settleElapsedSeconds;
        private bool idleRequested;
        private bool initialized;

        /// <summary>Caches the visual child's authored pose and configures its temporary idle motion once.</summary>
        /// <param name="visualBody">The NPC's image child; a missing child leaves this component inactive.</param>
        /// <param name="breathCycleSeconds">Duration of one complete inhale and exhale in scaled seconds.</param>
        /// <param name="breathHeightAmount">Maximum fractional height expansion during an inhale.</param>
        /// <param name="breathWidthAmount">Maximum fractional width expansion during an inhale.</param>
        /// <param name="transitionSeconds">Duration of the fade into or out of the breathing pose.</param>
        /// <param name="phaseOffset">Initial cycle offset in normalized cycles, wrapped into the zero-to-one range.</param>
        public void Initialize(RectTransform visualBody, float breathCycleSeconds, float breathHeightAmount,
            float breathWidthAmount, float transitionSeconds, float phaseOffset)
        {
            if (initialized || visualBody == null)
            {
                return;
            }

            body = visualBody;
            authoredPosition = body.anchoredPosition3D;
            authoredScale = body.localScale;
            authoredRotation = body.localRotation;
            Rect bodyRect = body.rect;
            groundedPoint = new Vector3(bodyRect.center.x,
                authoredScale.y < 0f ? bodyRect.yMax : bodyRect.yMin, 0f);
            cycleSeconds = Mathf.Max(0.2f, breathCycleSeconds);
            heightAmount = Mathf.Clamp(breathHeightAmount, 0f, 0.03f);
            widthAmount = Mathf.Clamp(breathWidthAmount, 0f, 0.02f);
            blendSeconds = Mathf.Max(0.01f, transitionSeconds);
            cyclePhase = Mathf.Repeat(phaseOffset, 1f);
            initialized = true;
        }

        /// <summary>Gently settles the visual body and fades in its repeating breathing motion.</summary>
        public void BeginIdle()
        {
            if (!initialized || idleRequested)
            {
                return;
            }

            idleRequested = true;
            blendStartWeight = blendWeight;
            blendElapsedSeconds = 0f;
            settleElapsedSeconds = 0f;
        }

        /// <summary>Fades the visual body back to its exact authored pose while its root may keep moving.</summary>
        public void EndIdle()
        {
            if (!initialized || !idleRequested)
            {
                return;
            }

            idleRequested = false;
            blendStartWeight = blendWeight;
            blendElapsedSeconds = 0f;
        }

        private void Update()
        {
            if (!initialized || body == null || (!idleRequested && blendWeight <= 0f))
            {
                return;
            }

            float deltaSeconds = Time.deltaTime;
            if (deltaSeconds <= 0f)
            {
                return;
            }

            cyclePhase = Mathf.Repeat(cyclePhase + deltaSeconds / cycleSeconds, 1f);
            blendElapsedSeconds = Mathf.Min(blendElapsedSeconds + deltaSeconds, blendSeconds);
            float blendProgress = Mathf.SmoothStep(0f, 1f, blendElapsedSeconds / blendSeconds);
            blendWeight = Mathf.Lerp(blendStartWeight, idleRequested ? 1f : 0f, blendProgress);
            if (!idleRequested && blendElapsedSeconds >= blendSeconds)
            {
                blendWeight = 0f;
                RestoreAuthoredPose();
                return;
            }

            settleElapsedSeconds = Mathf.Min(settleElapsedSeconds + deltaSeconds, SettleDurationSeconds);
            float settleProgress = settleElapsedSeconds / SettleDurationSeconds;
            float settle = SettleCompression * Mathf.Sin(Mathf.PI * settleProgress)
                * (1f - settleProgress);
            float inhale = 0.5f - 0.5f * Mathf.Cos(cyclePhase * FullCycleRadians);
            Vector3 animatedScale = authoredScale;
            animatedScale.x *= 1f + (widthAmount * inhale + settle * 0.5f) * blendWeight;
            animatedScale.y *= 1f + (heightAmount * inhale - settle) * blendWeight;

            // Compensate around the actual image's lower edge, including its authored rotation
            // and signed scale. All offsets come from the cached pose, so they cannot accumulate.
            Vector3 groundCompensation = authoredRotation
                * Vector3.Scale(groundedPoint, authoredScale - animatedScale);
            body.localScale = animatedScale;
            body.anchoredPosition3D = authoredPosition + groundCompensation;
        }

        private void OnDisable()
        {
            idleRequested = false;
            blendWeight = 0f;
            blendStartWeight = 0f;
            blendElapsedSeconds = 0f;
            RestoreAuthoredPose();
        }

        private void RestoreAuthoredPose()
        {
            if (!initialized || body == null)
            {
                return;
            }

            body.anchoredPosition3D = authoredPosition;
            body.localScale = authoredScale;
            body.localRotation = authoredRotation;
        }
    }
}
