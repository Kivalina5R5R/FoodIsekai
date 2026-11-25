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
        // One full walk cycle (two steps) for the walking image, played in step with the walk. The first
        // frame and the middle frame are the poses where a foot lands. Empty keeps the authored still.
        [SerializeField] private Sprite[] walkingFrames = new Sprite[0];
        // How long each walk frame stays up, relative to the others (for example how far the body moves
        // to the next frame), so uneven source frames still move at an even speed. Each step (half of
        // the frames) is spread over its own half of the cycle, keeping foot landings on the footsteps.
        // Leave empty, or with a different length than the frames, for equal times.
        [SerializeField] private float[] walkingFrameWeights = new float[0];
        // The walk frame that looks most like the standing pose. The entrance walk comes to rest on it,
        // so the fade into the standing pose starts from a matching pose instead of a wide stride.
        // A value outside the frames ends the walk on the first frame.
        [SerializeField] private int arrivalFrame = -1;
        private VideoPlayer standingVideo;
        private RenderTexture standingVideoTexture;
        public float FadeSeconds => Mathf.Max(0.01f, fadeSeconds);
        public float WalkingWeight { get; private set; }
        public RectTransform WalkingRect => walkingImage != null ? walkingImage.rectTransform : null;
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

        // Shows the walk frame for the given number of steps walked; it may be negative or fractional.
        public void SetWalkSteps(float steps)
        {
            if (walkingImage == null || walkingFrames == null || walkingFrames.Length == 0) return;
            float cycle = Mathf.Repeat(steps * 0.5f, 1f);
            Sprite sprite = walkingFrames[GetWalkFrame(cycle)];
            if (sprite != null && walkingImage.sprite != sprite) walkingImage.sprite = sprite;
        }

        // The number of steps (within one cycle) at the middle of the arrival frame, or 0 without one.
        // A walk whose steps reach this value at its end comes to rest on the arrival frame.
        public float ArrivalSteps
        {
            get
            {
                int count = walkingFrames != null ? walkingFrames.Length : 0;
                if (arrivalFrame < 0 || arrivalFrame >= count) return 0f;
                if (!HasWalkingFrameWeights(count)) return (arrivalFrame + 0.5f) / count * 2f;

                int half = count / 2;
                int first = arrivalFrame < half ? 0 : half;
                float total = HalfWeight(first, half);
                if (total <= 0f) return (arrivalFrame + 0.5f) / count * 2f;

                float before = 0f;
                for (int i = first; i < arrivalFrame; i++) before += Mathf.Max(0f, walkingFrameWeights[i]);
                float middle = (before + Mathf.Max(0f, walkingFrameWeights[arrivalFrame]) * 0.5f) / total;
                // Each half of the frames is one step.
                return (first == 0 ? 0f : 1f) + middle;
            }
        }

        private bool HasWalkingFrameWeights(int count)
        {
            return walkingFrameWeights != null && walkingFrameWeights.Length == count &&
                count >= 2 && count % 2 == 0;
        }

        private float HalfWeight(int first, int half)
        {
            float total = 0f;
            for (int i = first; i < first + half; i++) total += Mathf.Max(0f, walkingFrameWeights[i]);
            return total;
        }

        // The frame shown at a point of the walk cycle, from 0 up to but not including 1.
        private int GetWalkFrame(float cycle)
        {
            int count = walkingFrames.Length;
            if (!HasWalkingFrameWeights(count)) return Mathf.Min(count - 1, Mathf.FloorToInt(cycle * count));

            int half = count / 2;
            int first = cycle < 0.5f ? 0 : half;
            float position = Mathf.Repeat(cycle, 0.5f) * 2f;
            float total = HalfWeight(first, half);
            if (total <= 0f) return Mathf.Min(count - 1, Mathf.FloorToInt(cycle * count));

            float reached = 0f;
            for (int i = first; i < first + half; i++)
            {
                reached += Mathf.Max(0f, walkingFrameWeights[i]) / total;
                if (position < reached) return i;
            }
            return first + half - 1;
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
