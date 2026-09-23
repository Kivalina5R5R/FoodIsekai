using UnityEngine;

namespace FoodIsekaiZ.Display
{
    // Crossfades the ready circle into the gameplay plate without moving either authored marker.
    [DisallowMultipleComponent]
    public sealed class PlayerMarkerTransition : MonoBehaviour
    {
        [SerializeField] private CanvasGroup plate;
        [SerializeField] private CanvasGroup selection;
        [SerializeField] private MarkerBlendMesh plateShape;
        [SerializeField] private MarkerBlendMesh selectionShape;
        [SerializeField, Min(0.05f)] private float duration = 0.68f;
        [SerializeField, Range(0.8f, 1f)] private float plateStartScale = 0.94f;
        [SerializeField, Range(1f, 1.2f)] private float selectionEndScale = 1.12f;

        private bool initialized;
        private bool targetPlate;
        private bool targetSelection;
        private bool playing;
        private float elapsed;

        // Repeated tracking updates retain the current blend; hiding either marker is immediate.
        public void SetVisible(bool showPlate, bool showSelection)
        {
            if (plate == null || selection == null) return;
            if (initialized && targetPlate == showPlate && targetSelection == showSelection) return;
            bool crossfade = initialized && targetSelection && !targetPlate && showPlate && !showSelection;
            targetPlate = showPlate;
            targetSelection = showSelection;
            initialized = true;
            playing = crossfade;
            elapsed = 0f;
            if (!crossfade)
            {
                ApplyFinalVisibility();
                return;
            }

            // Set opacity before activation so the first plate frame cannot flash at full opacity.
            plate.alpha = 0f;
            selection.alpha = 1f;
            plateShape?.SetBlendScale(plateStartScale);
            selectionShape?.SetBlendScale(1f);
            plate.gameObject.SetActive(true);
            selection.gameObject.SetActive(true);
        }

        private void Update()
        {
            if (!playing) return;
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.05f, duration));
            // A slight overlap avoids a hollow middle; both envelopes settle with zero velocity.
            float incoming = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / 0.9f));
            float outgoing = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((progress - 0.1f) / 0.9f));
            float motion = Mathf.SmoothStep(0f, 1f, progress);
            plate.alpha = incoming;
            selection.alpha = 1f - outgoing;
            plateShape?.SetBlendScale(Mathf.Lerp(plateStartScale, 1f, motion));
            selectionShape?.SetBlendScale(Mathf.Lerp(1f, selectionEndScale, motion));
            if (progress < 1f) return;
            playing = false;
            ApplyFinalVisibility();
        }

        private void OnDisable()
        {
            if (initialized && plate != null && selection != null) ApplyFinalVisibility();
            playing = false;
            initialized = false;
        }

        private void ApplyFinalVisibility()
        {
            plateShape?.SetBlendScale(1f);
            selectionShape?.SetBlendScale(1f);
            plate.alpha = targetPlate ? 1f : 0f;
            selection.alpha = targetSelection ? 1f : 0f;
            plate.gameObject.SetActive(targetPlate);
            selection.gameObject.SetActive(targetSelection);
        }
    }
}
