using UnityEngine;

namespace FoodIsekaiZ.Audio
{
    // Plays authored cues through a bounded pool so simultaneous players cannot flood the speakers.
    public sealed class GameSoundPlayer : MonoBehaviour
    {
        [SerializeField] private AudioSource[] voices;
        [SerializeField] private AudioClip[] clips;
        [SerializeField] private float[] cooldowns;
        [SerializeField, Range(0f, 1f)] private float masterVolume = 0.65f;
        private readonly float[] nextAllowedTime = new float[System.Enum.GetValues(typeof(GameSoundCue)).Length];
        private int nextVoice;

        public bool TryPlay(GameSoundCue cue, bool priority = false)
        {
            int index = (int)cue;
            if (!isActiveAndEnabled || masterVolume <= 0f || clips == null || index < 0 ||
                index >= clips.Length || index >= nextAllowedTime.Length || clips[index] == null ||
                voices == null || voices.Length == 0 || Time.unscaledTime < nextAllowedTime[index]) return false;
            AudioSource voice = null;
            for (int offset = 0; offset < voices.Length; offset++)
            {
                int candidate = (nextVoice + offset) % voices.Length;
                if (voices[candidate] == null || !voices[candidate].isActiveAndEnabled || voices[candidate].isPlaying) continue;
                voice = voices[candidate];
                nextVoice = (candidate + 1) % voices.Length;
                break;
            }
            if (voice == null && priority)
            {
                voice = voices[nextVoice];
                nextVoice = (nextVoice + 1) % voices.Length;
            }
            if (voice == null || !voice.isActiveAndEnabled) return false;
            float cooldown = cooldowns != null && index < cooldowns.Length ? cooldowns[index] : 0.12f;
            nextAllowedTime[index] = Time.unscaledTime + Mathf.Max(0.05f, cooldown);
            voice.Stop();
            voice.PlayOneShot(clips[index], masterVolume);
            return true;
        }

        private void OnDisable()
        {
            if (voices != null)
                foreach (AudioSource voice in voices) if (voice != null) voice.Stop();
            System.Array.Clear(nextAllowedTime, 0, nextAllowedTime.Length);
        }
    }
}
