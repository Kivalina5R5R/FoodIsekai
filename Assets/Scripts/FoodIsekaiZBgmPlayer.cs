using UnityEngine;

namespace FoodIsekaiZ.Audio
{
    /// <summary>เล่นเพลง BGM ของเกมเมื่อเริ่ม Play Mode</summary>
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

        [SerializeField, Range(0f, 1f)] private float volume = 1f;

        private AudioSource audioSource;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            ConfigureAudioSource();

            if (playOnStart && bgmClip != null)
            {
                audioSource.Play();
            }
        }

        private void OnValidate()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }

            ConfigureAudioSource();
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
