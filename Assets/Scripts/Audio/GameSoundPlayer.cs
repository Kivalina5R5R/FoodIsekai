using UnityEngine;

namespace FoodIsekaiZ.Audio
{
    // Plays authored cues through a bounded pool so simultaneous players cannot flood the speakers.
    public sealed class GameSoundPlayer : MonoBehaviour
    {
        [SerializeField] private AudioSource[] voices;
        [SerializeField] private AudioClip[] clips;
        [SerializeField] private float[] cooldowns;
        [SerializeField, Range(0f, 1f)] private float masterVolume = 0.85f;
        [Header("Action Mix (dB)")]
        [SerializeField, Range(-12f, 12f)] private float foodPickupGainDb = 0f;
        [SerializeField, Range(-12f, 12f)] private float foodServedGainDb = 0f;
        [SerializeField, Range(-12f, 12f)] private float wrongFoodGainDb = 1f;
        [SerializeField, Range(-12f, 12f)] private float moneyPaidGainDb = 2f;
        [SerializeField, Range(-12f, 12f)] private float moneyCollectedGainDb = 1f;
        [SerializeField, Range(-12f, 12f)] private float bankDepositGainDb = 1f;
        [Header("Announcements And Warnings (dB)")]
        [SerializeField, Range(-12f, 12f)] private float customerExpiredGainDb = 2f;
        [SerializeField, Range(-12f, 12f)] private float waveStartGainDb = 4f;
        [SerializeField, Range(-12f, 12f)] private float waveBreakGainDb = 4f;
        [SerializeField, Range(-12f, 12f)] private float serviceCompleteGainDb = -1f;
        [SerializeField, Range(-12f, 12f)] private float timeWarningGainDb = 7f;
        [Header("Supporting Cues (dB)")]
        [SerializeField, Range(-12f, 12f)] private float orderArrivedGainDb = 4f;
        [SerializeField, Range(-12f, 12f)] private float emojiBubbleGainDb = 0f;
        [SerializeField, Range(-12f, 12f)] private float moneyReminderGainDb = 3f;
        [SerializeField, Range(-12f, 12f)] private float successPopGainDb = 4f;
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
            voice.PlayOneShot(clips[index], Mathf.Clamp01(masterVolume) * Mathf.Pow(10f, GetGainDb(cue) / 20f));
            return true;
        }

        private float GetGainDb(GameSoundCue cue)
        {
            switch (cue)
            {
                case GameSoundCue.FoodPickup: return foodPickupGainDb;
                case GameSoundCue.FoodServed: return foodServedGainDb;
                case GameSoundCue.WrongFood: return wrongFoodGainDb;
                case GameSoundCue.OrderArrived: return orderArrivedGainDb;
                case GameSoundCue.MoneyPaid: return moneyPaidGainDb;
                case GameSoundCue.MoneyCollected: return moneyCollectedGainDb;
                case GameSoundCue.BankDeposit: return bankDepositGainDb;
                case GameSoundCue.CustomerExpired: return customerExpiredGainDb;
                case GameSoundCue.WaveStart: return waveStartGainDb;
                case GameSoundCue.WaveBreak: return waveBreakGainDb;
                case GameSoundCue.ServiceComplete: return serviceCompleteGainDb;
                case GameSoundCue.TimeWarning: return timeWarningGainDb;
                case GameSoundCue.EmojiBubble: return emojiBubbleGainDb;
                case GameSoundCue.MoneyReminder: return moneyReminderGainDb;
                case GameSoundCue.SuccessPop: return successPopGainDb;
                default: return 0f;
            }
        }

        private void OnDisable()
        {
            if (voices != null)
                foreach (AudioSource voice in voices) if (voice != null) voice.Stop();
            System.Array.Clear(nextAllowedTime, 0, nextAllowedTime.Length);
        }
    }
}
