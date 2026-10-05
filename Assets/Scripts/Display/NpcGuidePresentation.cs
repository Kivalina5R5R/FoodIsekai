using System.Collections;
using FoodIsekaiZ.Audio;
using UnityEngine;

namespace FoodIsekaiZ.Display
{
    // Moves the guide while preserving the prefab's authored body and dialogue styling.
    public sealed class NpcGuidePresentation : MonoBehaviour
    {
        [SerializeField] private GameObject dialogue;
        [SerializeField] private GameObject perkDialogue;
        [SerializeField] private RectTransform visualBody;
        [SerializeField] private NpcIdleBreathing breathing;
        [SerializeField] private NpcGuidePoseBlend poseBlend;
        [SerializeField, Min(0.01f)] private float walkingSpeedCanvasMultiplier = 0.264f;
        [SerializeField, Min(0.05f)] private float arrivalStoppingSeconds = 0.28f;
        [SerializeField, Min(0f)] private float walkingBobHeight = 12f;
        [SerializeField, Min(0.1f)] private float walkingBobFrequency = 2.2f;
        // Footsteps play each time the walking bob lands; the clips alternate between feet.
        // The same source also plays the pop when the speech bubble opens.
        [SerializeField] private AudioSource footstepSource;
        [SerializeField] private AudioClip[] footstepClips;
        [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.6f;
        // Steps far from her standing spot (off screen) play at this fraction of the full volume,
        // so walking in grows louder and walking out fades away.
        [SerializeField, Range(0f, 1f)] private float farFootstepVolume = 0.15f;
        [SerializeField] private AudioClip bubbleClip;
        [SerializeField, Range(0f, 1f)] private float bubbleVolume = 0.6f;
        private int footstepIndex;
        private Coroutine entrance;
        private Vector2 groundedPosition;
        private float speed;
        private RectTransform dialogueRect;
        private Vector3 dialogueScale;
        private GameObject introDialogue;
        private Vector3 entranceScale;
        private Quaternion entranceRotation;
        private AudioSource dialogueVoice;
        private bool duckingMusic;
        private Coroutine voiceFade;
        // Each voice line keeps its own authored level, so a fade always returns to that level rather than to full volume.
        private readonly System.Collections.Generic.Dictionary<AudioSource, float> voiceVolumes =
            new System.Collections.Generic.Dictionary<AudioSource, float>();
        // A voice line that is cut short fades out over this time; it finishes before the bubble closes.
        private const float VoiceFadeSeconds = 0.3f;

        public bool HasExited { get; private set; }
        public bool HasOpenedDialogue { get; private set; }
        public bool HasFinishedSpeaking => HasOpenedDialogue &&
            (dialogueVoice == null || !dialogueVoice.isPlaying);

        private void Awake()
        {
            entranceScale = transform.localScale;
            entranceRotation = transform.localRotation;
            // Resolve renamed children in prefab instances that still carry older scene references.
            Transform intro = transform.Find("TextIntro");
            if (intro != null) dialogue = intro.gameObject;
            if (perkDialogue == null) perkDialogue = transform.Find("TextPerk")?.gameObject;
            introDialogue = dialogue;
            dialogueRect = dialogue != null ? dialogue.transform as RectTransform : null;
            if (dialogueRect != null) dialogueScale = dialogueRect.localScale;
            if (dialogue != null) dialogue.SetActive(false);
            if (breathing != null) breathing.Initialize(visualBody, 3.6f, 0.008f, 0.0025f, 0.45f, 0f, dialogueRect);
            if (poseBlend != null) poseBlend.ShowWalking();
        }

        // Dips the music while this guide's voice line is audible.
        private void Update()
        {
            bool speaking = dialogueVoice != null && dialogueVoice.isPlaying;
            if (speaking == duckingMusic) return;
            duckingMusic = speaking;
            FoodIsekaiZBgmPlayer.SetVoiceSpeaking(speaking);
        }

        private void OnDisable()
        {
            // Disabling stops the fade coroutine, so put the voice back at its authored level.
            if (voiceFade != null && dialogueVoice != null) dialogueVoice.volume = AuthoredVolume(dialogueVoice);
            voiceFade = null;
            if (!duckingMusic) return;
            duckingMusic = false;
            FoodIsekaiZBgmPlayer.SetVoiceSpeaking(false);
        }

        private void OnEnable()
        {
            // The selected dialogue opens only after the guide reaches her standing position.
            if (dialogue != null) dialogue.SetActive(false);
            if (perkDialogue != null) perkDialogue.SetActive(false);
            if (introDialogue != null) introDialogue.SetActive(false);
        }

        // Select authored dialogue before starting the matching guide entrance.
        public void UsePerkDialogue(bool usePerk)
        {
            if (dialogue != null)
            {
                dialogue.SetActive(false);
                if (dialogueRect != null) dialogueRect.localScale = dialogueScale;
            }
            if (introDialogue != null) introDialogue.SetActive(false);
            if (perkDialogue != null) perkDialogue.SetActive(false);
            dialogue = usePerk ? perkDialogue : introDialogue;
            dialogueRect = dialogue != null ? dialogue.transform as RectTransform : null;
            if (dialogueRect != null) dialogueScale = dialogueRect.localScale;
            if (breathing != null) breathing.SetVerticalFollower(dialogueRect);
        }

        public void WalkIn(Vector2 start, Vector2 destination, float canvasWidth)
        {
            if (entrance != null) StopCoroutine(entrance);
            // A reused shop guide must undo the previous exit turn before walking in again.
            transform.localScale = entranceScale;
            transform.localRotation = entranceRotation;
            if (perkDialogue != null) perkDialogue.SetActive(false);
            if (dialogue != null) dialogue.SetActive(false);
            speed = Mathf.Max(1f, canvasWidth * walkingSpeedCanvasMultiplier);
            HasExited = false;
            HasOpenedDialogue = false;
            groundedPosition = start;
            if (breathing != null) breathing.EndIdle();
            if (poseBlend != null)
            {
                poseBlend.ShowWalking();
                // Prepare the hidden standing clip during the walk so it is ready on arrival.
                poseBlend.WarmStandingVideo();
            }
            entrance = StartCoroutine(Walk(start, destination, false));
        }

        public void WalkOut(Vector2 destination)
        {
            FadeOutVoice();
            if (entrance != null) StopCoroutine(entrance);
            entrance = StartCoroutine(TurnAndExit(destination));
        }

        private IEnumerator TurnAndExit(Vector2 destination)
        {
            yield return AnimateDialogue(false);
            if (breathing != null) breathing.EndIdle();
            var rect = (RectTransform)transform;
            Vector3 startScale = rect.localScale;
            Vector3 exitScale = new Vector3(-startScale.x, startScale.y, startScale.z);
            Quaternion startRotation = rect.localRotation;
            float direction = Mathf.Sign(destination.x - groundedPosition.x);
            float startWeight = poseBlend != null ? poseBlend.WalkingWeight : 1f;
            float elapsed = 0f;
            float turnSeconds = Mathf.Max(0.3f, poseBlend != null ? poseBlend.FadeSeconds + 0.15f : 0.3f);
            // Cross-dissolve from the inward standing pose to a walking pose that already faces the exit,
            // with a light squash and lean, so no frame shows a hard mirror flip.
            if (poseBlend != null) poseBlend.MirrorWalkingImage(true);
            else rect.localScale = exitScale;
            while (elapsed < turnSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / turnSeconds);
                float collapse = Mathf.Sin(t * Mathf.PI);
                float easedCollapse = Mathf.SmoothStep(0f, 1f, collapse);
                Vector3 scale = poseBlend != null ? startScale : exitScale;
                scale.x *= Mathf.Lerp(1f, 0.92f, easedCollapse);
                scale.y *= 1f + 0.01f * easedCollapse;
                rect.localScale = scale;
                rect.localRotation = startRotation * Quaternion.Euler(0f, 0f, collapse * 2f * direction);
                rect.anchoredPosition = groundedPosition + Vector2.up * (collapse * 2f);
                if (poseBlend != null)
                    poseBlend.SetWalkingWeight(Mathf.Lerp(startWeight, 1f, Mathf.SmoothStep(0f, 1f, t)));
                yield return null;
            }
            // The standing pose is now invisible, so flipping the root while un-mirroring the walking image
            // leaves the frame unchanged.
            if (poseBlend != null)
            {
                poseBlend.SetWalkingWeight(1f);
                poseBlend.MirrorWalkingImage(false);
            }
            rect.localScale = exitScale;
            rect.localRotation = startRotation;
            rect.localRotation = startRotation;
            rect.anchoredPosition = groundedPosition;
            yield return Walk(groundedPosition, destination, true);
        }

        private IEnumerator Walk(Vector2 start, Vector2 destination, bool exiting)
        {
            var rect = (RectTransform)transform;
            float distance = Vector2.Distance(start, destination);
            float arrivalSeconds = Mathf.Max(0.05f, arrivalStoppingSeconds);
            float stopDistance = exiting ? 0f : Mathf.Min(distance, .5f * speed * arrivalSeconds);
            float cruiseSeconds = (distance - stopDistance) / speed;
            float stopSeconds = stopDistance * 2f / speed;
            float duration = cruiseSeconds + stopSeconds;
            float elapsed = 0f;
            rect.anchoredPosition = start;
            int stride = Mathf.FloorToInt(-distance / speed * walkingBobFrequency);
            // The standing spot is where she is closest; the off-screen end of the walk is farthest.
            Vector2 standingSpot = exiting ? start : destination;
            while (elapsed < duration)
            {
                elapsed = Mathf.Min(duration, elapsed + Time.unscaledDeltaTime);
                float travelled;
                if (elapsed <= cruiseSeconds || stopSeconds <= 0f)
                    travelled = Mathf.Min(distance, speed * elapsed);
                else
                {
                    float remaining = 1f - Mathf.Clamp01((elapsed - cruiseSeconds) / stopSeconds);
                    travelled = distance - stopDistance * remaining * remaining;
                }
                groundedPosition = Vector2.Lerp(start, destination, distance > 0f ? travelled / distance : 1f);
                // Match customer stride: remaining distance grounds the final step at the destination.
                float phase = -(distance - travelled) / speed * walkingBobFrequency * Mathf.PI * 2f;
                float bob = (.5f - .5f * Mathf.Cos(phase)) * walkingBobHeight;
                // Ease into the first stride instead of jumping up immediately after the turn.
                bob *= Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 0.2f));
                if (!exiting && elapsed >= cruiseSeconds)
                {
                    float settle = Mathf.SmoothStep(0f, 1f,
                        stopSeconds > 0f ? (elapsed - cruiseSeconds) / stopSeconds : 1f);
                    bob *= 1f - settle;
                }
                rect.anchoredPosition = groundedPosition + Vector2.up * bob;
                // Each completed bob cycle is a foot touching the ground, including the final arrival step.
                int landedStride = Mathf.FloorToInt(phase / (Mathf.PI * 2f));
                if (landedStride > stride)
                {
                    stride = landedStride;
                    float away = distance > 0f ? Vector2.Distance(groundedPosition, standingSpot) / distance : 0f;
                    PlayFootstep(Mathf.Lerp(1f, farFootstepVolume, Mathf.SmoothStep(0f, 1f, away)));
                }
                yield return null;
            }
            rect.anchoredPosition = destination;
            groundedPosition = destination;
            if (exiting)
            {
                HasExited = true;
                gameObject.SetActive(false);
                yield break;
            }
            yield return WaitForStandingVideo();
            yield return BlendPose(0f);
            if (breathing != null) breathing.BeginIdle();
            // Let the standing video play at least one visible frame before the dialogue opens.
            yield return null;
            yield return AnimateDialogue(true);
            HasOpenedDialogue = true;
            entrance = null;
        }

        // Distance scales the authored footstep volume for this step.
        private void PlayFootstep(float distanceVolume)
        {
            if (footstepSource == null || footstepClips == null || footstepClips.Length == 0) return;
            AudioClip clip = footstepClips[footstepIndex % footstepClips.Length];
            footstepIndex++;
            if (clip != null) footstepSource.PlayOneShot(clip, footstepVolume * distanceVolume);
        }

        private void FadeOutVoice()
        {
            if (dialogueVoice == null || !dialogueVoice.isPlaying || voiceFade != null) return;
            AuthoredVolume(dialogueVoice);
            voiceFade = StartCoroutine(FadeVoice(dialogueVoice));
        }

        private IEnumerator FadeVoice(AudioSource voice)
        {
            float start = voice.volume;
            float elapsed = 0f;
            while (elapsed < VoiceFadeSeconds && voice != null && voice.isPlaying)
            {
                elapsed += Time.unscaledDeltaTime;
                voice.volume = start * (1f - Mathf.SmoothStep(0f, 1f, elapsed / VoiceFadeSeconds));
                yield return null;
            }
            if (voice != null)
            {
                voice.Stop();
                voice.volume = AuthoredVolume(voice);
            }
            voiceFade = null;
        }

        private float AuthoredVolume(AudioSource voice)
        {
            if (!voiceVolumes.TryGetValue(voice, out float volume))
            {
                volume = voice.volume;
                voiceVolumes[voice] = volume;
            }
            return volume;
        }

        private void StopVoiceNow()
        {
            if (voiceFade != null)
            {
                StopCoroutine(voiceFade);
                voiceFade = null;
                if (dialogueVoice != null) dialogueVoice.volume = AuthoredVolume(dialogueVoice);
            }
            if (dialogueVoice != null) dialogueVoice.Stop();
        }

        // Waits briefly for the standing clip's first frame so the arrival fades straight into video.
        private IEnumerator WaitForStandingVideo()
        {
            if (poseBlend == null) yield break;
            poseBlend.WarmStandingVideo();
            float waited = 0f;
            while (!poseBlend.IsStandingVideoReady && waited < 3f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        // Finish changing pose at the grounded position before starting the next action.
        private IEnumerator BlendPose(float targetWeight)
        {
            if (poseBlend == null) yield break;
            float from = poseBlend.WalkingWeight;
            if (Mathf.Approximately(from, targetWeight)) yield break;
            float elapsed = 0f;
            float duration = poseBlend.FadeSeconds;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                poseBlend.SetWalkingWeight(Mathf.Lerp(from, targetWeight, progress));
                yield return null;
            }
            poseBlend.SetWalkingWeight(targetWeight);
        }

        private IEnumerator AnimateDialogue(bool show)
        {
            if (dialogueRect == null) yield break;
            if (!show && !dialogue.activeSelf) yield break;
            if (!show) FadeOutVoice();
            if (show)
            {
                StopVoiceNow();
                dialogueRect.localScale = dialogueScale * 0.05f;
                dialogue.SetActive(true);
                if (footstepSource != null && bubbleClip != null) footstepSource.PlayOneShot(bubbleClip, bubbleVolume);
                // Play the bubble's authored voice exactly when its reveal begins.
                dialogueVoice = dialogue.GetComponent<AudioSource>();
                if (dialogueVoice != null && dialogueVoice.clip != null) dialogueVoice.Play();
            }
            Vector3 from = dialogueRect.localScale;
            Vector3 peak = dialogueScale * 1.12f;
            yield return ScaleDialogue(from, peak, show ? 0.22f : 0.12f);
            yield return ScaleDialogue(peak, show ? dialogueScale : Vector3.zero, show ? 0.16f : 0.2f);
            if (!show)
            {
                dialogue.SetActive(false);
                dialogueRect.localScale = dialogueScale;
            }
        }

        // Only scale is animated here; NpcIdleBreathing owns the dialogue's vertical following.
        private IEnumerator ScaleDialogue(Vector3 from, Vector3 to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                dialogueRect.localScale = Vector3.LerpUnclamped(from, to, t);
                yield return null;
            }
            dialogueRect.localScale = to;
        }
    }
}
