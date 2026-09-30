using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // Owns the temporary purchase visuals and plays simultaneous purchases in order.
    public sealed class SmallPerkPurchasePresentation : MonoBehaviour
    {
        [SerializeField] private Canvas wallCanvas;
        [SerializeField, Min(0.1f)] private float flightSeconds = 1.65f;
        [SerializeField, Min(0.1f)] private float landingHoldSeconds = .85f;
        [SerializeField, Min(0.1f)] private float gatherSeconds = 2.6f;
        [SerializeField, Min(0.1f)] private float chargeSeconds = .8f;
        [SerializeField, Min(0.1f)] private float powerSeconds = 1.4f;

        private enum Phase { Flying, Settling, Gathering, Charging, Releasing }

        private sealed class PurchaseVisual
        {
            public RectTransform Root { get; }
            public RectTransform Absorption { get; }
            public CanvasGroup ArtworkOpacity { get; }
            public PerkLandingGraphic Landing { get; }
            public PerkChargedFrameGraphic ChargedFrame { get; }
            public GameObject Artwork { get; }
            public GameObject Back { get; }
            public Vector3 StartPosition { get; }
            public Vector3 StartScale { get; }
            public Quaternion StartRotation { get; }

            public PurchaseVisual(RectTransform root, RectTransform absorption, CanvasGroup artworkOpacity, PerkLandingGraphic landing,
                GameObject artwork, GameObject back, PerkChargedFrameGraphic chargedFrame)
            {
                Root = root;
                Absorption = absorption;
                ArtworkOpacity = artworkOpacity;
                Landing = landing;
                Artwork = artwork;
                Back = back;
                ChargedFrame = chargedFrame;
                StartPosition = root.localPosition;
                StartScale = root.localScale;
                StartRotation = root.localRotation;
            }
        }

        private readonly Queue<PurchaseVisual> waiting = new Queue<PurchaseVisual>();
        private PurchaseVisual current;
        private Phase phase;
        private float elapsed;
        private PerkPowerGraphic powerPulse;
        private PerkGatherGraphic gathering;
        private Image focusShade;

        public bool IsPlaying => current != null || waiting.Count > 0;

        // Gameplay has already committed the purchase; this method never changes money or perks.
        public void Play(PerkCardSlot slot)
        {
            if (slot == null || wallCanvas == null || !isActiveAndEnabled || !slot.gameObject.activeInHierarchy) return;
            RectTransform source = slot.transform as RectTransform;
            if (source == null) return;
            RectTransform canvasRect = wallCanvas.transform as RectTransform;
            RectTransform root = CreateRect("Small Perk Purchase", canvasRect, source.rect.size);
            var overlay = root.gameObject.AddComponent<Canvas>();
            overlay.overrideSorting = true;
            overlay.sortingLayerID = wallCanvas.sortingLayerID;
            overlay.sortingOrder = wallCanvas.sortingOrder + 20;
            root.position = source.position;
            root.rotation = source.rotation;
            Vector3 scale = canvasRect.InverseTransformVector(source.TransformVector(Vector3.right));
            Vector3 verticalScale = canvasRect.InverseTransformVector(source.TransformVector(Vector3.up));
            root.localScale = new Vector3(scale.magnitude, verticalScale.magnitude, 1f);
            var landing = CreateRect("Landing Burst", root, source.rect.size).gameObject.AddComponent<PerkLandingGraphic>();

            RectTransform absorption = CreateRect("Card Absorption", root, source.rect.size);
            var artworkOpacity = absorption.gameObject.AddComponent<CanvasGroup>();
            artworkOpacity.blocksRaycasts = false;
            Transform copy = slot.CopyPurchaseArtwork(absorption);
            if (copy == null)
            {
                DestroyVisual(root);
                return;
            }
            foreach (Graphic graphic in copy.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            Image frame = null;
            foreach (Image candidate in copy.GetComponentsInChildren<Image>(true))
                if (candidate.name == "BG") { frame = candidate; break; }
            Rect bounds = source.rect;
            GameObject back = null;
            if (frame != null)
            {
                var corners = new Vector3[4];
                frame.rectTransform.GetWorldCorners(corners);
                Vector2 minimum = root.InverseTransformPoint(corners[0]);
                Vector2 maximum = root.InverseTransformPoint(corners[2]);
                bounds = Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
                RectTransform backRect = CreateRect("Card Back", root, bounds.size);
                backRect.anchoredPosition = bounds.center;
                backRect.localRotation = Quaternion.Euler(0f, 180f, 0f);
                var backImage = backRect.gameObject.AddComponent<Image>();
                backImage.sprite = frame.sprite;
                backImage.color = frame.color;
                backImage.raycastTarget = false;
                back = backRect.gameObject;
                back.SetActive(false);
            }
            // The break UI hides siblings outside the shop whenever gameplay data refreshes.
            var chargedFrame = CreateRect("Charged Gold Frame", absorption, source.rect.size).gameObject.AddComponent<PerkChargedFrameGraphic>();
            chargedFrame.SetFrame(bounds);
            root.SetParent(transform, true);
            waiting.Enqueue(new PurchaseVisual(root, absorption, artworkOpacity, landing, copy.gameObject, back, chargedFrame));
            // Keep the authored slot available for the next shop; only its visibility changes.
            slot.gameObject.SetActive(false);
        }

        private void Update() => Advance(Time.unscaledDeltaTime);

        private void Advance(float deltaTime)
        {
            if (current == null)
            {
                if (waiting.Count == 0) return;
                current = waiting.Dequeue();
                powerPulse = null;
                gathering = null;
                focusShade = null;
                current.Root.SetAsLastSibling();
                phase = Phase.Flying;
                elapsed = 0f;
            }
            elapsed += Mathf.Max(0f, deltaTime);
            switch (phase)
            {
                case Phase.Flying:
                    AnimateFlight();
                    break;
                case Phase.Settling:
                    current.Landing.SetImpact((flightSeconds * .1f + elapsed) / .9f);
                    if (elapsed < landingHoldSeconds) break;
                    RectTransform shadeRect = CreateFullscreenRect("Power Focus Shade");
                    shadeRect.sizeDelta *= 1.25f;
                    shadeRect.SetAsFirstSibling();
                    focusShade = shadeRect.gameObject.AddComponent<Image>();
                    focusShade.raycastTarget = false;
                    focusShade.color = Color.clear;
                    gathering = CreateFullscreenRect("Gathered Perk Energy").gameObject.AddComponent<PerkGatherGraphic>();
                    gathering.SetEnergy(0f, 0f);
                    phase = Phase.Gathering;
                    elapsed = 0f;
                    break;
                case Phase.Gathering:
                    AnimateGathering();
                    break;
                case Phase.Charging:
                    gathering.SetEnergy(1f, elapsed / Mathf.Max(.1f, chargeSeconds));
                    AnimateChargedCard(1f, gatherSeconds + elapsed);
                    if (elapsed < chargeSeconds) break;
                    ShowPowerPulse();
                    phase = Phase.Releasing;
                    elapsed = 0f;
                    break;
                case Phase.Releasing:
                    gathering.SetEnergy(1f, 1f, 1f - Mathf.Clamp01(elapsed / .25f));
                    float remaining = 1f - Mathf.Clamp01(elapsed / .12f);
                    current.ArtworkOpacity.alpha = remaining;
                    current.ChargedFrame.SetCharge(remaining, gatherSeconds + chargeSeconds + elapsed);
                    powerPulse.SetPower(elapsed / Mathf.Max(.1f, powerSeconds));
                    focusShade.color = new Color(0f, 0f, 0f, .58f * (1f - Mathf.Clamp01(elapsed / powerSeconds)));
                    if (elapsed >= powerSeconds)
                    {
                        DestroyVisual(current.Root);
                        current = null;
                        powerPulse = null;
                        gathering = null;
                        focusShade = null;
                    }
                    break;
            }
        }

        private void AnimateFlight()
        {
            RectTransform canvasRect = (RectTransform)wallCanvas.transform;
            float screenHeight = transform.InverseTransformVector(canvasRect.TransformVector(Vector3.up * canvasRect.rect.height)).magnitude;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, flightSeconds));
            float flight = Mathf.Clamp01(t / .7f);
            float smooth = SmootherStep(flight);
            float enlargement = Mathf.Min(1.24f, screenHeight * 0.86f /
                Mathf.Max(1f, current.Root.rect.height * current.StartScale.y));
            float hoverEnlargement = screenHeight * 1.32f /
                Mathf.Max(1f, current.Root.rect.height * current.StartScale.y);
            Vector3 destination = transform.InverseTransformPoint(canvasRect.TransformPoint(canvasRect.rect.center));
            float direction = current.StartPosition.x <= destination.x ? 1f : -1f;
            float fall = SmootherStep(Mathf.Clamp01((t - .73f) / .17f));
            float drop = Mathf.Sin(smooth * Mathf.PI) * .06f + .045f * smooth * (1f - fall);
            current.Root.localPosition = Vector3.Lerp(current.StartPosition, destination, smooth) +
                Vector3.up * (drop * screenHeight);
            float growth = Mathf.Lerp(1f, hoverEnlargement, smooth);
            growth = Mathf.Lerp(growth, enlargement, fall);
            // The card stays rigid; impact is carried by the expanding wave alone.
            current.Root.localScale = current.StartScale * growth;
            current.Root.localRotation = Quaternion.Slerp(current.StartRotation, Quaternion.identity, smooth) *
                Quaternion.Euler(0f, direction * 360f * smooth, 0f);
            if (current.Back != null)
            {
                bool frontFacing = Mathf.Cos(smooth * Mathf.PI * 2f) >= 0f;
                current.Artwork.SetActive(frontFacing);
                current.Back.SetActive(!frontFacing);
            }
            current.Landing.SetImpact(t < .9f ? -1f : (elapsed - flightSeconds * .9f) / .9f);
            if (t < 1f) return;
            current.Root.localPosition = destination;
            current.Root.localRotation = Quaternion.identity;
            phase = Phase.Settling;
            elapsed = 0f;
        }

        private static float SmootherStep(float value) =>
            value * value * value * (value * (value * 6f - 15f) + 10f);

        private void AnimateGathering()
        {
            current.Landing.SetImpact(1f);
            float amount = Mathf.Clamp01(elapsed / Mathf.Max(.1f, gatherSeconds));
            gathering.SetEnergy(amount, 0f);
            focusShade.color = new Color(0f, 0f, 0f, Mathf.SmoothStep(0f, .58f, amount * 2f));
            AnimateChargedCard(amount, elapsed);
            if (amount < 1f) return;
            phase = Phase.Charging;
            elapsed = 0f;
        }

        private void AnimateChargedCard(float strength, float seconds)
        {
            current.ChargedFrame.SetCharge(strength, seconds);
            float shake = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((strength - .65f) / .35f)) * 3f;
            current.Absorption.anchoredPosition = new Vector2(Mathf.Sin(seconds * 73f), Mathf.Sin(seconds * 91f)) * shake;
        }

        private void ShowPowerPulse()
        {
            powerPulse = CreateFullscreenRect("Perk Power Acquired").gameObject.AddComponent<PerkPowerGraphic>();
            powerPulse.SetPower(0f);
        }

        private RectTransform CreateFullscreenRect(string objectName)
        {
            var corners = new Vector3[4];
            ((RectTransform)wallCanvas.transform).GetWorldCorners(corners);
            Vector2 minimum = current.Root.InverseTransformPoint(corners[0]);
            Vector2 maximum = current.Root.InverseTransformPoint(corners[2]);
            RectTransform rect = CreateRect(objectName, current.Root, maximum - minimum);
            rect.anchoredPosition = (minimum + maximum) * .5f;
            return rect;
        }
        private static RectTransform CreateRect(string objectName, RectTransform parent, Vector2 size)
        {
            var rect = (RectTransform)new GameObject(objectName, typeof(RectTransform)).transform;
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }

        private void OnDisable()
        {
            if (current != null) DestroyVisual(current.Root);
            current = null;
            powerPulse = null;
            gathering = null;
            focusShade = null;
            while (waiting.Count > 0) DestroyVisual(waiting.Dequeue().Root);
        }

        private static void DestroyVisual(RectTransform root)
        {
            if (root == null) return;
            if (Application.isPlaying) Destroy(root.gameObject);
            else DestroyImmediate(root.gameObject);
        }
    }
}
