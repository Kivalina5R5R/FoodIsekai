using System.Collections;
using UnityEngine;

namespace FoodIsekaiZ.Audio
{
    // เล่นเพลง BGM ของเกมเมื่อเริ่ม Play Mode
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class FoodIsekaiZBgmPlayer : MonoBehaviour
    {
        [Header("BGM")]
        [Tooltip("เพลงที่จะเล่นเมื่อเริ่มเกม")]
        [SerializeField] private AudioClip bgmClip;

        [Tooltip("เริ่มเล่นเพลงอัตโนมัติเมื่อกด Play")]
        [SerializeField] private bool playOnStart = true;
        [SerializeField] private bool waitForStartup;

        [Tooltip("เปิดเพื่อวนเพลงซ้ำ ปิดเพื่อเล่นเพลงครั้งเดียว")]
        [SerializeField] private bool loop = true;

        [SerializeField, Range(0f, 1f)] private float volume = 0.16f;

        [SerializeField] private AudioSource audioSource;

        [Header("Intro Music")]
        // Loops from scene start through the wall intro and Lunar's guide, then fades out once she has left.
        [SerializeField] private AudioClip introClip;
        [SerializeField, Range(0f, 1f)] private float introVolume = 0.18f;
        [SerializeField, Min(0f)] private float introFadeSeconds = 2f;
        // Both the intro music and the game BGM rise in smoothly instead of starting at full volume.
        [SerializeField, Min(0f)] private float musicFadeInSeconds = 2f;
        // The first play skips the soft wind opening and starts where the melody and bass come in.
        // Later loops still pass through the whole clip, whose seamless crossfade leads back to the start.
        [SerializeField, Min(0f)] private float introStartSeconds = 7.21f;

        [Header("Voice Ducking")]
        // Music dips under Lunar's voice lines so speech stays clear, then returns smoothly.
        [SerializeField, Range(0f, 1f)] private float voiceDuckLevel = 0.45f;
        [SerializeField, Min(0.01f)] private float voiceDuckSeconds = 0.35f;

        [Header("Scene Transitions")]
        // Music fades down while the menu page covers a scene change and fades back as it lifts.
        [SerializeField, Range(0f, 1f)] private float sceneTransitionLevel = 0.35f;
        [SerializeField, Min(0.01f)] private float sceneTransitionFadeSeconds = 0.5f;

        private static int speakingVoices;
        private static bool sceneTransition;
        private float transitionDip = 1f;
        private float baseVolume;
        private float fade = 1f;
        private float duck = 1f;
        private Coroutine introFade;
        private Coroutine fadeIn;

        // Guide voices call this when a spoken line starts and ends.
        public static void SetVoiceSpeaking(bool speaking)
        {
            speakingVoices = Mathf.Max(0, speakingVoices + (speaking ? 1 : -1));
        }

        // The menu transition calls this when its page starts covering and when it lifts again.
        public static void SetSceneTransition(bool covering) => sceneTransition = covering;

        private void Awake()
        {
            speakingVoices = 0;
            sceneTransition = false;
            // StreamingAssets/GameFlowConfig.json sets both music levels for this round.
            var config = FoodIsekaiZ.Configuration.GameFlowConfig.Load();
            volume = Mathf.Clamp01(config.bgmVolume);
            introVolume = Mathf.Clamp01(config.introMusicVolume);
            ResolveAudioSource();
            if (audioSource == null)
            {
                Debug.LogError("Assign the dedicated BGM AudioSource; effect voices must remain independent.", this);
                return;
            }

            ConfigureAudioSource();

            if (playOnStart && !waitForStartup && bgmClip != null)
            {
                PlayWithFadeIn();
            }
            else if (waitForStartup && introClip != null)
            {
                audioSource.clip = introClip;
                audioSource.loop = true;
                baseVolume = Mathf.Clamp01(introVolume);
                audioSource.time = Mathf.Min(introStartSeconds, Mathf.Max(0f, introClip.length - 0.1f));
                PlayWithFadeIn();
            }
        }

        private void PlayWithFadeIn()
        {
            fade = musicFadeInSeconds > 0f ? 0f : 1f;
            ApplyVolume();
            audioSource.Play();
            if (fadeIn != null) StopCoroutine(fadeIn);
            fadeIn = musicFadeInSeconds > 0f ? StartCoroutine(FadeIn()) : null;
        }

        private IEnumerator FadeIn()
        {
            float elapsed = 0f;
            while (elapsed < musicFadeInSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                fade = Mathf.SmoothStep(0f, 1f, elapsed / musicFadeInSeconds);
                yield return null;
            }
            fade = 1f;
            fadeIn = null;
        }

        private void Update()
        {
            if (audioSource == null) return;
            float target = speakingVoices > 0 ? voiceDuckLevel : 1f;
            duck = Mathf.MoveTowards(duck, target, Time.unscaledDeltaTime / voiceDuckSeconds);
            float dipTarget = sceneTransition ? sceneTransitionLevel : 1f;
            transitionDip = Mathf.MoveTowards(transitionDip, dipTarget, Time.unscaledDeltaTime / sceneTransitionFadeSeconds);
            ApplyVolume();
        }

        private void ApplyVolume() => audioSource.volume = baseVolume * fade * duck * transitionDip;

        // Fades the intro music out; the game BGM still waits for ReleaseStartup.
        public void EndIntroMusic()
        {
            if (!IsPlayingIntro || introFade != null) return;
            introFade = StartCoroutine(FadeOutIntro());
        }

        private bool IsPlayingIntro => audioSource != null && introClip != null &&
            audioSource.clip == introClip && audioSource.isPlaying;

        private IEnumerator FadeOutIntro()
        {
            // Continue from the current level so a fade-out that interrupts the fade-in never jumps.
            if (fadeIn != null) StopCoroutine(fadeIn);
            fadeIn = null;
            float start = fade;
            float elapsed = 0f;
            while (elapsed < introFadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                fade = start * (1f - Mathf.SmoothStep(0f, 1f, elapsed / Mathf.Max(0.01f, introFadeSeconds)));
                yield return null;
            }
            fade = 0f;
            audioSource.Stop();
            introFade = null;
        }

        // Connected to the scene intro completion event; the game BGM starts once the intro music has faded.
        public void ReleaseStartup()
        {
            if (audioSource == null || !isActiveAndEnabled) return;
            StartCoroutine(StartGameMusic());
        }

        private IEnumerator StartGameMusic()
        {
            if (IsPlayingIntro && introFade == null) EndIntroMusic();
            while (introFade != null) yield return null;
            if (introClip != null && audioSource.clip == introClip)
            {
                audioSource.Stop();
                ConfigureAudioSource();
            }
            if (playOnStart && bgmClip != null && !audioSource.isPlaying)
            {
                PlayWithFadeIn();
            }
        }

        private void OnValidate()
        {
            // Inspector edits during play must not swap the intro music for the game BGM.
            if (Application.isPlaying) return;
            ResolveAudioSource();

            ConfigureAudioSource();
        }

        private void ResolveAudioSource()
        {
            if (audioSource != null) return;
            AudioSource[] candidates = GetComponents<AudioSource>();
            if (candidates.Length == 1) audioSource = candidates[0];
        }

        private void ConfigureAudioSource()
        {
            if (audioSource == null)
            {
                return;
            }

            audioSource.clip = bgmClip;
            audioSource.playOnAwake = false;
            audioSource.loop = loop;
            baseVolume = Mathf.Clamp01(volume);
            audioSource.volume = baseVolume * fade * duck * transitionDip;
            audioSource.spatialBlend = 0f;
        }
    }
}
