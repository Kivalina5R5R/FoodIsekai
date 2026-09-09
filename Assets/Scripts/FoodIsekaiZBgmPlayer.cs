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

        [Tooltip("เปิดเพื่อวนเพลงซ้ำ ปิดเพื่อเล่นเพลงครั้งเดียว")]
        [SerializeField] private bool loop = true;

        [SerializeField, Range(0f, 1f)] private float volume = 0.18f;

        [SerializeField] private AudioSource audioSource;

        private void Awake()
        {
            ResolveAudioSource();
            if (audioSource == null)
            {
                Debug.LogError("Assign the dedicated BGM AudioSource; effect voices must remain independent.", this);
                return;
            }

            ConfigureAudioSource();

            if (playOnStart && bgmClip != null)
            {
                audioSource.Play();
            }
        }

        private void OnValidate()
        {
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
            audioSource.volume = Mathf.Clamp01(volume);
            audioSource.spatialBlend = 0f;
        }
    }
}
