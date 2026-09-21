using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.Video;

namespace FoodIsekaiZ.Display
{
    // Plays the authored wall intro, then releases scene startup through Inspector events.
    [DefaultExecutionOrder(-150)]
    public sealed class WallIntroVideoSequence : MonoBehaviour
    {
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private VideoClip introClip;
        [SerializeField] private RawImage videoImage;
        [SerializeField] private GameObject videoBackdrop;
        [SerializeField] private CanvasGroup overlay;
        [SerializeField] private WallBlockTransitionGraphic transition;
        [SerializeField, Min(1f)] private float prepareTimeoutSeconds = 15f;
        [Tooltip("Start covering the video this many seconds before its playback reaches the end.")]
        [SerializeField, Min(0f)] private float transitionLeadSeconds = 0.7f;
        [SerializeField] private GameObject floorBackground;
        [SerializeField] private GameObject introFloorBackground;
        [SerializeField] private UnityEvent onCovered = new UnityEvent();
        [SerializeField] private UnityEvent onFinished = new UnityEvent();

        private bool videoEnded;
        private bool videoFailed;
        private bool finished;
        private bool started;
        private bool quitting;
        private bool floorSwapped;
        private bool originalFloorActive;
        private bool coveredReleased;

        public bool IsFinished => finished;

        private void Awake()
        {
            if (floorBackground != null && introFloorBackground != null)
            {
                originalFloorActive = floorBackground.activeSelf;
                floorSwapped = true;
                floorBackground.SetActive(false);
                introFloorBackground.SetActive(true);
            }
            if (overlay != null) overlay.alpha = 1f;
            if (videoImage != null) videoImage.enabled = false;
            if (videoBackdrop != null) videoBackdrop.SetActive(true);
            if (transition != null) transition.Hide();
        }

        private IEnumerator Start()
        {
            started = true;
            if (videoPlayer == null || introClip == null || videoImage == null ||
                videoBackdrop == null || overlay == null || transition == null)
            {
                Debug.LogWarning("[WallIntroVideoSequence] Intro references are missing; starting the game.", this);
                Finish();
                yield break;
            }

            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = false;
            videoPlayer.waitForFirstFrame = true;
            videoPlayer.skipOnDrop = true;
            videoPlayer.renderMode = VideoRenderMode.APIOnly;
            videoPlayer.source = VideoSource.VideoClip;
            videoPlayer.clip = introClip;
            videoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
            videoPlayer.controlledAudioTrackCount = introClip.audioTrackCount;
            for (ushort track = 0; track < videoPlayer.controlledAudioTrackCount; track++)
            {
                videoPlayer.EnableAudioTrack(track, true);
                videoPlayer.SetDirectAudioMute(track, false);
                videoPlayer.SetDirectAudioVolume(track, 1f);
            }
            videoPlayer.loopPointReached += HandleVideoEnded;
            videoPlayer.errorReceived += HandleVideoError;
            videoPlayer.Prepare();

            double deadline = Time.realtimeSinceStartupAsDouble + prepareTimeoutSeconds;
            while (!videoPlayer.isPrepared && !videoFailed && Time.realtimeSinceStartupAsDouble < deadline)
            {
                yield return null;
            }

            if (!videoPlayer.isPrepared && !videoFailed)
            {
                FailVideo("Timed out while preparing the intro.");
            }

            Coroutine coverRoutine = null;
            if (!videoFailed)
            {
                videoPlayer.Play();
                double transitionStartTime = System.Math.Max(0d, introClip.length - transitionLeadSeconds);
                double playbackDeadline = Time.realtimeSinceStartupAsDouble +
                    introClip.length + prepareTimeoutSeconds;
                while (!videoEnded && !videoFailed)
                {
                    if (videoPlayer.texture != null)
                    {
                        videoImage.texture = videoPlayer.texture;
                        videoImage.enabled = true;
                    }
                    // Use the video's playback position so loading delays do not advance the transition.
                    if (coverRoutine == null && videoPlayer.frame >= 0 && videoPlayer.time >= transitionStartTime)
                    {
                        coverRoutine = StartCoroutine(transition.Cover());
                    }
                    if (Time.realtimeSinceStartupAsDouble >= playbackDeadline)
                    {
                        FailVideo("Intro playback stalled before reaching the end.");
                    }
                    yield return null;
                }
                // Keep the final decoded frame visible until the curtain fully covers it.
                videoPlayer.Pause();
            }

            if (coverRoutine != null)
            {
                yield return coverRoutine;
            }
            else
            {
                yield return transition.Cover();
            }
            videoBackdrop.SetActive(false);
            videoPlayer.Stop();
            videoImage.texture = null;
            ReleaseCovered();
            // Let Ready build behind the closed curtain before revealing it.
            yield return null;
            yield return transition.Reveal();
            Finish();
        }

        private void HandleVideoEnded(VideoPlayer source)
        {
            videoEnded = true;
            RestoreFloor();
        }

        private void HandleVideoError(VideoPlayer source, string message)
        {
            FailVideo(message);
        }

        private void FailVideo(string message)
        {
            videoFailed = true;
            RestoreFloor();
            Debug.LogWarning($"[WallIntroVideoSequence] {message} Continuing to the game.", this);
        }

        private void Finish()
        {
            RestoreFloor();
            if (finished) return;
            finished = true;
            ReleaseCovered();
            if (overlay != null) overlay.alpha = 0f;
            if (transition != null) transition.Hide();
            onFinished.Invoke();
        }

        private void ReleaseCovered()
        {
            if (coveredReleased) return;
            coveredReleased = true;
            onCovered.Invoke();
        }

        private void OnApplicationQuit()
        {
            quitting = true;
        }

        private void OnDisable()
        {
            RestoreFloor();
            StopAllCoroutines();
            if (videoPlayer != null)
            {
                videoPlayer.loopPointReached -= HandleVideoEnded;
                videoPlayer.errorReceived -= HandleVideoError;
                videoPlayer.Stop();
            }
            if (videoImage != null) videoImage.texture = null;
            if (started && !quitting) Finish();
        }

        private void RestoreFloor()
        {
            if (!floorSwapped) return;
            floorSwapped = false;
            if (introFloorBackground != null) introFloorBackground.SetActive(false);
            if (floorBackground != null) floorBackground.SetActive(originalFloorActive);
        }
    }
}
