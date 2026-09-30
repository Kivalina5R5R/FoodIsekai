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
        [SerializeField, Min(0.1f)] private float burnSeconds = 6f;
        [SerializeField, Min(0.1f)] private float emberSeconds = 1.2f;
        [SerializeField, Min(0.1f)] private float powerSeconds = 1.4f;

        private enum Phase { Flying, Settling, Burning, Embers }

        private sealed class PurchaseVisual
        {
            public RectTransform Root { get; }
            public PerkBurnGraphic[] Masks { get; }
            public PerkBurnGraphic[] Fires { get; }
            public PerkLandingGraphic Landing { get; }
            public GameObject Artwork { get; }
            public GameObject Back { get; }
            public Vector3 StartPosition { get; }
            public Vector3 StartScale { get; }
            public Quaternion StartRotation { get; }

            public PurchaseVisual(RectTransform root, PerkBurnGraphic[] masks, PerkBurnGraphic[] fires, PerkLandingGraphic landing,
                GameObject artwork, GameObject back)
            {
                Root = root;
                Masks = masks;
                Fires = fires;
                Landing = landing;
                Artwork = artwork;
                Back = back;
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
        private readonly System.Random visualRandom = new System.Random();

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

            var ignitions = new Vector2[visualRandom.Next(2, 4)];
            ignitions[0] = Vector2.one * .5f;
            float burnSeed = (float)visualRandom.NextDouble() * 100f;
            var masks = new PerkBurnGraphic[ignitions.Length];
            var fires = new PerkBurnGraphic[ignitions.Length];
            RectTransform maskParent = root;
            // Nested stencils keep paper only where none of the ignition holes have reached it.
            for (int i = 0; i < ignitions.Length; i++)
            {
                maskParent = CreateRect("Burn Mask " + i, maskParent, source.rect.size);
                masks[i] = maskParent.gameObject.AddComponent<PerkBurnGraphic>();
                masks[i].Configure(true, ignitions[i], burnSeed);
                maskParent.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            }
            Transform copy = slot.CopyPurchaseArtwork(maskParent);
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
            float edgeStart = (float)visualRandom.NextDouble();
            for (int i = 1; i < ignitions.Length; i++)
            {
                Vector2 edge = PerkFrameContour.Sample(bounds, edgeStart + (i - 1) * .5f);
                edge = Vector2.Lerp(edge, bounds.center, .006f);
                ignitions[i] = new Vector2((edge.x - source.rect.xMin) / source.rect.width,
                    (edge.y - source.rect.yMin) / source.rect.height);
            }
            for (int i = 0; i < masks.Length; i++) masks[i].Configure(true, ignitions[i], burnSeed);
            for (int i = 0; i < ignitions.Length; i++)
            {
                RectTransform fireRect = CreateRect("Burn Edge and Ash " + i, root, source.rect.size);
                fires[i] = fireRect.gameObject.AddComponent<PerkBurnGraphic>();
                fires[i].Configure(false, ignitions[i], burnSeed);
                fires[i].SetOtherIgnitions(ignitions, i);
                foreach (Image image in copy.GetComponentsInChildren<Image>(true))
                {
                    if (image.name != "BG") continue;
                    fires[i].SetFrame(image.rectTransform);
                    break;
                }
            }
            // The break UI hides siblings outside the shop whenever gameplay data refreshes.
            root.SetParent(transform, true);
            waiting.Enqueue(new PurchaseVisual(root, masks, fires, landing, copy.gameObject, back));
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
                    phase = Phase.Burning;
                    elapsed = 0f;
                    break;
                case Phase.Burning:
                    AnimateBurn();
                    break;
                case Phase.Embers:
                    foreach (PerkBurnGraphic fire in current.Fires)
                        fire.SetBurn(1f, elapsed / Mathf.Max(0.1f, emberSeconds));
                    if (powerPulse != null) powerPulse.SetPower(elapsed / Mathf.Max(.1f, powerSeconds));
                    if (elapsed >= Mathf.Max(emberSeconds, powerSeconds))
                    {
                        DestroyVisual(current.Root);
                        current = null;
                        powerPulse = null;
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

        private void AnimateBurn()
        {
            current.Landing.SetImpact(1f);
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, burnSeconds));
            foreach (PerkBurnGraphic mask in current.Masks) mask.SetBurn(progress);
            foreach (PerkBurnGraphic fire in current.Fires) fire.SetBurn(progress);
            if (progress < 1f) return;
            ShowPowerPulse();
            phase = Phase.Embers;
            elapsed = 0f;
        }

        private void ShowPowerPulse()
        {
            // The pulse belongs to the purchase, but its bounds match the entire wall canvas.
            var corners = new Vector3[4];
            ((RectTransform)wallCanvas.transform).GetWorldCorners(corners);
            Vector2 minimum = current.Root.InverseTransformPoint(corners[0]);
            Vector2 maximum = current.Root.InverseTransformPoint(corners[2]);
            RectTransform rect = CreateRect("Perk Power Acquired", current.Root, maximum - minimum);
            rect.anchoredPosition = (minimum + maximum) * .5f;
            powerPulse = rect.gameObject.AddComponent<PerkPowerGraphic>();
            powerPulse.SetPower(0f);
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
