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
        [SerializeField] private UnityEvent onFinished = new UnityEvent();

        private bool videoEnded;
        private bool videoFailed;
        private bool finished;
        private bool started;
        private bool quitting;

        public bool IsFinished => finished;

        private void Awake()
        {
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

            if (!videoFailed)
            {
                videoPlayer.Play();
                double playbackDeadline = Time.realtimeSinceStartupAsDouble +
                    introClip.length + prepareTimeoutSeconds;
                while (!videoEnded && !videoFailed)
                {
                    if (videoPlayer.texture != null)
                    {
                        videoImage.texture = videoPlayer.texture;
                        videoImage.enabled = true;
                    }
                    if (Time.realtimeSinceStartupAsDouble >= playbackDeadline)
                    {
                        FailVideo("Intro playback stalled before reaching the end.");
                    }
                    yield return null;
                }
                // Keep the final decoded frame visible until all black tiles have covered it.
                videoPlayer.Pause();
            }

            yield return transition.Cover();
            videoBackdrop.SetActive(false);
            videoPlayer.Stop();
            videoImage.texture = null;
            yield return transition.Reveal();
            Finish();
        }

        private void HandleVideoEnded(VideoPlayer source)
        {
            videoEnded = true;
        }

        private void HandleVideoError(VideoPlayer source, string message)
        {
            FailVideo(message);
        }

        private void FailVideo(string message)
        {
            videoFailed = true;
            Debug.LogWarning($"[WallIntroVideoSequence] {message} Continuing to the game.", this);
        }

        private void Finish()
        {
            if (finished) return;
            finished = true;
            if (overlay != null) overlay.alpha = 0f;
            if (transition != null) transition.Hide();
            onFinished.Invoke();
        }

        private void OnApplicationQuit()
        {
            quitting = true;
        }

        private void OnDisable()
        {
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
    }
}
