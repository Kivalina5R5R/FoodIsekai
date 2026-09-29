using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace FoodIsekaiZ.Display
{
    // Crossfades the two authored body images without changing their sprites or proportions.
    public sealed class NpcGuidePoseBlend : MonoBehaviour
    {
        [SerializeField] private Image standingImage;
        [SerializeField] private Image walkingImage;
        // Optional looping clip shown in place of the standing image while the guide stands and speaks.
        // The standing image stays visible until the clip has delivered its first frame.
        [SerializeField] private RawImage standingVideoImage;
        [SerializeField] private VideoClip standingVideoClip;
        [SerializeField, Min(0.01f)] private float fadeSeconds = 0.35f;
        private VideoPlayer standingVideo;
        private RenderTexture standingVideoTexture;
        public float FadeSeconds => Mathf.Max(0.01f, fadeSeconds);
        public float WalkingWeight { get; private set; }
        // True when there is no standing clip, or the clip is playing with a frame ready to show.
        public bool IsStandingVideoReady => standingVideo == null ||
            (standingVideo.isPlaying && standingVideo.frame >= 0);
        private bool standingVideoWarm;
        private RectTransform walkingRect;
        private Vector2 walkingPosition;
        private Vector3 walkingScale;

        private void Awake()
        {
            walkingRect = walkingImage != null ? walkingImage.rectTransform : null;
            if (walkingRect != null)
            {
                walkingPosition = walkingRect.anchoredPosition;
                walkingScale = walkingRect.localScale;
            }
            EnsureStandingVideo();
            ApplyWeight();
        }

        private void Update()
        {
            if (standingVideo == null) return;
            // Keep the loop running whenever any part of the standing pose is visible.
            if (WalkingWeight < 1f || standingVideoWarm)
            {
                if (!standingVideo.isPlaying) standingVideo.Play();
            }
            else if (standingVideo.isPlaying)
            {
                standingVideo.Pause();
            }

            ApplyWeight();
        }

        private void OnDestroy()
        {
            if (standingVideoTexture == null) return;
            standingVideoTexture.Release();
            Destroy(standingVideoTexture);
        }

        public void ShowWalking()
        {
            standingVideoWarm = false;
            // An interrupted exit can leave the walking image mirrored; restore its authored facing.
            MirrorWalkingImage(false);
            SetWalkingWeight(1f);
        }

        // Mirrors the walking image around the guide root, matching a flip of the whole guide,
        // so it can fade in already facing the exit while the standing pose still faces inward.
        public void MirrorWalkingImage(bool mirrored)
        {
            if (walkingRect == null) return;
            walkingRect.anchoredPosition = mirrored
                ? new Vector2(-walkingPosition.x, walkingPosition.y)
                : walkingPosition;
            walkingRect.localScale = mirrored
                ? new Vector3(-walkingScale.x, walkingScale.y, walkingScale.z)
                : walkingScale;
        }

        // Starts the standing clip before it becomes visible so the pose change can fade straight into video.
        public void WarmStandingVideo()
        {
            standingVideoWarm = true;
            if (standingVideo != null && !standingVideo.isPlaying) standingVideo.Play();
        }

        // The presentation supplies an eased weight while the guide is stationary.
        public void SetWalkingWeight(float weight)
        {
            WalkingWeight = Mathf.Clamp01(weight);
            ApplyWeight();
        }

        private void ApplyWeight()
        {
            float standingWeight = 1f - WalkingWeight;
            bool videoReady = standingVideo != null && standingVideo.isPlaying && standingVideo.frame >= 0;
            if (standingImage != null) standingImage.canvasRenderer.SetAlpha(videoReady ? 0f : standingWeight);
            if (standingVideoImage != null)
            {
                standingVideoImage.enabled = videoReady;
                standingVideoImage.canvasRenderer.SetAlpha(standingWeight);
            }
            if (walkingImage != null) walkingImage.canvasRenderer.SetAlpha(WalkingWeight);
        }

        private void EnsureStandingVideo()
        {
            if (standingVideo != null || standingVideoImage == null || standingVideoClip == null) return;
            standingVideoTexture = new RenderTexture(
                (int)standingVideoClip.width, (int)standingVideoClip.height, 0, RenderTextureFormat.ARGB32);
            standingVideoTexture.Create();
            standingVideo = gameObject.AddComponent<VideoPlayer>();
            standingVideo.playOnAwake = false;
            standingVideo.isLooping = true;
            standingVideo.clip = standingVideoClip;
            standingVideo.renderMode = VideoRenderMode.RenderTexture;
            standingVideo.targetTexture = standingVideoTexture;
            standingVideo.audioOutputMode = VideoAudioOutputMode.None;
            // The guide animates on unscaled time, so the loop keeps playing during paused presentations.
            standingVideo.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            // The silent idle loop needs no clock sync, so show every frame instead of skipping when the game is busy.
            standingVideo.skipOnDrop = false;
            // A failed clip falls back to the standing image, so report why in the Console.
            standingVideo.errorReceived += (source, message) =>
                Debug.LogWarning($"[{nameof(NpcGuidePoseBlend)}] Standing video failed: {message}", this);
            standingVideo.Prepare();
            standingVideoImage.texture = standingVideoTexture;
            standingVideoImage.enabled = false;
        }
    }
}
