using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // Fades the picture an NPC video showed a moment ago out over the new one, so a change of clip or
    // the jump back to the idle's resting pose blends instead of popping. The old picture is copied
    // into its own texture and shown on a runtime overlay that follows the authored frame image.
    public sealed class VideoFrameCrossfade : System.IDisposable
    {
        private readonly RawImage target;
        private readonly float defaultFadeSeconds;
        private float fadeSeconds;
        private RawImage overlay;
        private RenderTexture snapshot;
        private float holdRemaining;
        private float fadeRemaining;

        // The target is the frame image that shows the video. Fade seconds is how long the old
        // picture takes to disappear; zero turns the cross-fade off.
        public VideoFrameCrossfade(RawImage target, float fadeSeconds)
        {
            this.target = target;
            defaultFadeSeconds = Mathf.Max(0f, fadeSeconds);
        }

        // Copies what the frame image shows right now and fades that copy out. The copy stays fully
        // visible for the hold time first, which covers a seek that has not drawn its frame yet.
        public void Begin(float holdSeconds)
        {
            Begin(holdSeconds, defaultFadeSeconds);
        }

        // The same, with its own fade length for this one change.
        public void Begin(float holdSeconds, float seconds)
        {
            fadeSeconds = Mathf.Max(0f, seconds);
            if (fadeSeconds <= 0f || target == null || target.texture == null || !target.isActiveAndEnabled)
            {
                return;
            }

            EnsureOverlay(target.texture);
            RenderTexture previous = RenderTexture.active;
            Graphics.Blit(target.texture, snapshot);
            RenderTexture.active = previous;
            overlay.gameObject.SetActive(true);
            holdRemaining = Mathf.Max(0f, holdSeconds);
            fadeRemaining = fadeSeconds;
            Follow(1f);
        }

        // Advances the fade; call once per frame.
        public void Tick(float deltaTime)
        {
            if (overlay == null || !overlay.gameObject.activeSelf)
            {
                return;
            }

            if (holdRemaining > 0f)
            {
                holdRemaining -= deltaTime;
                Follow(1f);
                return;
            }

            fadeRemaining -= deltaTime;
            if (fadeRemaining <= 0f)
            {
                Stop();
                return;
            }
            Follow(Mathf.SmoothStep(0f, 1f, fadeRemaining / fadeSeconds));
        }

        // Hides the old picture at once.
        public void Stop()
        {
            holdRemaining = 0f;
            fadeRemaining = 0f;
            if (overlay != null)
            {
                overlay.gameObject.SetActive(false);
            }
        }

        public void Dispose()
        {
            if (overlay != null)
            {
                Object.Destroy(overlay.gameObject);
            }
            if (snapshot != null)
            {
                snapshot.Release();
                Object.Destroy(snapshot);
            }
        }

        private void EnsureOverlay(Texture source)
        {
            if (snapshot == null || snapshot.width != source.width || snapshot.height != source.height)
            {
                if (snapshot != null)
                {
                    snapshot.Release();
                    Object.Destroy(snapshot);
                }
                snapshot = new RenderTexture(source.width, source.height, 0, RenderTextureFormat.ARGB32);
                snapshot.useMipMap = true;
                snapshot.autoGenerateMips = true;
                snapshot.filterMode = FilterMode.Trilinear;
                snapshot.Create();
            }

            if (overlay != null)
            {
                return;
            }
            GameObject copy = new GameObject(target.name + " Fade", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(RawImage));
            copy.transform.SetParent(target.transform.parent, false);
            copy.transform.SetSiblingIndex(target.transform.GetSiblingIndex() + 1);
            overlay = copy.GetComponent<RawImage>();
            overlay.raycastTarget = false;
            overlay.texture = snapshot;
        }

        // Keeps the overlay exactly on top of the frame image, at the given share of its opacity.
        private void Follow(float opacity)
        {
            RectTransform from = target.rectTransform;
            RectTransform to = overlay.rectTransform;
            to.anchorMin = from.anchorMin;
            to.anchorMax = from.anchorMax;
            to.pivot = from.pivot;
            to.anchoredPosition = from.anchoredPosition;
            to.sizeDelta = from.sizeDelta;
            to.localRotation = from.localRotation;
            to.localScale = from.localScale;
            overlay.material = target.material;
            overlay.color = target.color;
            overlay.uvRect = target.uvRect;
            overlay.canvasRenderer.SetAlpha(opacity * target.canvasRenderer.GetAlpha());
        }
    }
}
