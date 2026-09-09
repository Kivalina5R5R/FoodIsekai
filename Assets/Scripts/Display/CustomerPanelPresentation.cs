using UnityEngine;

namespace FoodIsekaiZ.Display
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public sealed class CustomerPanelPresentation : MonoBehaviour
    {
        // Raised when the success particle burst begins after the order collapse.
        public event System.Action SuccessParticlesPlayed;

        private enum MotionPhase { Hidden, Entering, Visible, Expanding, Collapsing }

        [Header("Scene References")]
        [SerializeField] private CanvasGroup panelGroup;
        [Tooltip("The order background defines the visual center without changing the panel pivot.")]
        [SerializeField] private RectTransform visualCenter;
        [SerializeField] private CustomerPanelSuccessParticles successParticlesPrefab;
        [SerializeField] private CustomerPanelAmbientSparkles ambientSparklesPrefab;

        [Header("Entrance")]
        [SerializeField, Min(0.05f)] private float entranceDurationSeconds = 0.42f;
        [SerializeField, Range(0.1f, 1f)] private float entranceScale = 0.72f;
        [SerializeField, Min(0f)] private float entranceLiftPixels = 18f;
        [SerializeField, Range(0f, 0.2f)] private float entranceOvershoot = 0.06f;

        [Header("Waiting For Food")]
        [SerializeField, Min(0.5f)] private float idleCycleSeconds = 3.6f;
        [SerializeField, Min(0f)] private float idleFloatPixels = 2f;
        [SerializeField, Range(0f, 0.02f)] private float idleScaleAmount = 0.004f;

        [Header("Successful Order")]
        [SerializeField, Min(0.05f)] private float expandDurationSeconds = 0.18f;
        [SerializeField, Range(1f, 1.5f)] private float successScale = 1.18f;
        [SerializeField, Min(0.05f)] private float collapseDurationSeconds = 0.24f;

        private RectTransform panelRect;
        private CustomerPanelSuccessParticles successParticles;
        private CustomerPanelAmbientSparkles ambientSparkles;
        private Vector3 authoredScale;
        private Vector3 authoredPosition;
        private Vector3 centerOffset;
        private float authoredAlpha;
        private float phaseElapsedSeconds;
        private float currentScale = 1f;
        private float currentLift;
        private float expandStartScale;
        private float expandStartLift;
        private float expandStartAlpha;
        private MotionPhase phase;
        private bool initialized;

        public bool IsCompleting => phase == MotionPhase.Expanding || phase == MotionPhase.Collapsing;

        public bool IsCelebrating => IsCompleting || (successParticles != null && successParticles.IsPlaying);

        private void Awake()
        {
            Initialize();
        }

        public void Show()
        {
            Initialize();
            EnsureAmbientSparkles();
            if (phase != MotionPhase.Hidden && gameObject.activeSelf)
            {
                return;
            }

            RestoreAuthoredAppearance();
            CacheVisualCenter();
            successParticles?.Stop();
            phase = MotionPhase.Entering;
            phaseElapsedSeconds = 0f;
            gameObject.SetActive(true);
            ApplyMotion(entranceScale, -entranceLiftPixels, 0f);
            ambientSparkles?.Play();
        }

        public void Complete()
        {
            if (!isActiveAndEnabled || IsCompleting || phase == MotionPhase.Hidden)
            {
                return;
            }

            expandStartScale = currentScale;
            ambientSparkles?.Stop();
            expandStartLift = currentLift;
            expandStartAlpha = panelGroup.alpha;
            phase = MotionPhase.Expanding;
            phaseElapsedSeconds = 0f;
        }

        public void Hide(bool force = false)
        {
            Initialize();
            if (!force && IsCompleting)
            {
                return;
            }

            if (force)
            {
                successParticles?.Stop();
            }

            if (phase == MotionPhase.Hidden && !gameObject.activeSelf)
            {
                return;
            }

            phase = MotionPhase.Hidden;
            phaseElapsedSeconds = 0f;
            gameObject.SetActive(false);
            RestoreAuthoredAppearance();
        }

        private void Update()
        {
            if (phase == MotionPhase.Hidden)
            {
                return;
            }

            phaseElapsedSeconds += Time.deltaTime;
            switch (phase)
            {
                case MotionPhase.Entering:
                    AnimateEntrance();
                    break;
                case MotionPhase.Visible:
                    AnimateIdle();
                    break;
                case MotionPhase.Expanding:
                    AnimateExpansion();
                    break;
                case MotionPhase.Collapsing:
                    AnimateCollapse();
                    break;
            }
        }

        private void AnimateEntrance()
        {
            float progress = Mathf.Clamp01(phaseElapsedSeconds / Mathf.Max(0.05f, entranceDurationSeconds));
            const float peakTime = 0.65f;
            float scale = progress < peakTime
                ? Mathf.Lerp(entranceScale, 1f + entranceOvershoot, EaseOutCubic(progress / peakTime))
                : Mathf.Lerp(1f + entranceOvershoot, 1f, Mathf.SmoothStep(0f, 1f, (progress - peakTime) / (1f - peakTime)));
            ApplyMotion(scale, -entranceLiftPixels * (1f - EaseOutCubic(progress)),
                authoredAlpha * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / 0.55f)));
            if (progress >= 1f)
            {
                phase = MotionPhase.Visible;
                phaseElapsedSeconds = 0f;
                RestoreAuthoredAppearance();
            }
        }

        private void AnimateIdle()
        {
            float cycle = phaseElapsedSeconds / Mathf.Max(0.5f, idleCycleSeconds) * Mathf.PI * 2f;
            float breath = (1f - Mathf.Cos(cycle)) * 0.5f;
            ApplyMotion(1f + breath * Mathf.Clamp(idleScaleAmount, 0f, 0.02f),
                breath * Mathf.Max(0f, idleFloatPixels), authoredAlpha);
        }

        private void AnimateExpansion()
        {
            float progress = Mathf.Clamp01(phaseElapsedSeconds / Mathf.Max(0.05f, expandDurationSeconds));
            float eased = EaseOutCubic(progress);
            ApplyMotion(Mathf.Lerp(expandStartScale, successScale, eased),
                Mathf.Lerp(expandStartLift, 0f, eased), Mathf.Lerp(expandStartAlpha, authoredAlpha, eased));
            if (progress >= 1f)
            {
                phase = MotionPhase.Collapsing;
                phaseElapsedSeconds = 0f;
            }
        }

        private void AnimateCollapse()
        {
            float progress = Mathf.Clamp01(phaseElapsedSeconds / Mathf.Max(0.05f, collapseDurationSeconds));
            float eased = progress * progress * progress;
            ApplyMotion(Mathf.Lerp(successScale, 0f, eased), 0f,
                authoredAlpha * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, progress))));
            if (progress < 1f)
            {
                return;
            }

            // The effect is a sibling, so hiding this panel cannot shrink or stop its burst.
            Vector3 burstPosition = panelRect.position;
            Hide(true);
            if (successParticles != null)
            {
                successParticles.transform.position = burstPosition;
                successParticles.Play();
                SuccessParticlesPlayed?.Invoke();
            }
        }

        private void Initialize()
        {
            if (initialized)
            {
                return;
            }

            panelRect = (RectTransform)transform;
            if (panelGroup == null)
            {
                panelGroup = GetComponent<CanvasGroup>();
            }

            authoredScale = panelRect.localScale;
            authoredPosition = panelRect.anchoredPosition3D;
            authoredAlpha = panelGroup.alpha;
            initialized = true;
            EnsureAmbientSparkles();

            if (successParticlesPrefab != null)
            {
                successParticles = Instantiate(successParticlesPrefab, panelRect.parent, false);
                successParticles.name = $"{name} Success Particles";
                successParticles.Stop();
            }
        }

        private void EnsureAmbientSparkles()
        {
            if (ambientSparkles != null || visualCenter == null)
            {
                return;
            }

            // An already-open scene can still have the old, empty prefab reference.
            // Resources also keeps this fallback available in player builds.
            CustomerPanelAmbientSparkles prefab = ambientSparklesPrefab;
            if (prefab == null)
            {
                GameObject prefabAsset = Resources.Load<GameObject>("UI/CustomerPanelAmbientSparkles");
                prefab = prefabAsset != null ? prefabAsset.GetComponent<CustomerPanelAmbientSparkles>() : null;
            }
            if (prefab == null)
            {
                return;
            }

            // Place only the new effect above the panel content; leave authored children in their order.
            ambientSparkles = Instantiate(prefab, panelRect, false);
            ambientSparkles.name = "Order Border Sparkles";
            ambientSparkles.Initialize(visualCenter.GetComponent<UnityEngine.UI.Image>());
        }

        private void CacheVisualCenter()
        {
            Vector3 worldCenter = visualCenter != null
                ? visualCenter.TransformPoint(visualCenter.rect.center)
                : panelRect.TransformPoint(panelRect.rect.center);
            centerOffset = panelRect.parent != null
                ? panelRect.parent.InverseTransformPoint(worldCenter) - panelRect.localPosition
                : worldCenter - panelRect.localPosition;
        }

        private void ApplyMotion(float scale, float lift, float alpha)
        {
            currentScale = scale;
            currentLift = lift;
            panelRect.localScale = authoredScale * scale;
            panelRect.anchoredPosition3D = authoredPosition + centerOffset * (1f - scale) + Vector3.up * lift;
            panelGroup.alpha = alpha;
        }

        private void RestoreAuthoredAppearance()
        {
            if (!initialized)
            {
                return;
            }

            panelRect.localScale = authoredScale;
            panelRect.anchoredPosition3D = authoredPosition;
            panelGroup.alpha = authoredAlpha;
            currentScale = 1f;
            currentLift = 0f;
        }

        private void OnDisable()
        {
            phase = MotionPhase.Hidden;
            phaseElapsedSeconds = 0f;
            successParticles?.Stop();
            ambientSparkles?.Stop();
            RestoreAuthoredAppearance();
        }

        private void OnDestroy()
        {
            if (ambientSparkles != null)
            {
                Destroy(ambientSparkles.gameObject);
            }

            if (successParticles != null)
            {
                Destroy(successParticles.gameObject);
            }
        }

        private static float EaseOutCubic(float progress)
        {
            float remaining = 1f - progress;
            return 1f - remaining * remaining * remaining;
        }
    }
}
