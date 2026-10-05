using System.Collections;
using UnityEngine;

namespace FoodIsekaiZ.Audio
{
    // Plays authored cues through a bounded pool so simultaneous players cannot flood the speakers.
    public sealed class GameSoundPlayer : MonoBehaviour
    {
        [SerializeField] private AudioSource[] voices;
        [SerializeField] private AudioClip[] clips;
        [SerializeField] private float[] cooldowns;
        [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
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
        [Header("Player Feedback (dB)")]
        // Responses to what a player just did in the perk shop or Ready selection must stay clearly above the music.
        [SerializeField, Range(-12f, 12f)] private float playerFeedbackGainDb = 3f;
        [Header("Scene Ambience (dB)")]
        // Scene dressing such as the intro curtain sits only just above the music so it never jumps out.
        [SerializeField, Range(-12f, 12f)] private float curtainGainDb = -6f;
        [Header("Stopping")]
        // Stopped cues fade out over this time instead of cutting off.
        [SerializeField, Min(0.01f)] private float stopFadeSeconds = 0.25f;
        private readonly float[] nextAllowedTime = new float[System.Enum.GetValues(typeof(GameSoundCue)).Length];
        private int nextVoice;
        private readonly System.Collections.Generic.HashSet<GameSoundCue> missingClips =
            new System.Collections.Generic.HashSet<GameSoundCue>();
        private readonly System.Collections.Generic.Dictionary<AudioSource, GameSoundCue> activeCues =
            new System.Collections.Generic.Dictionary<AudioSource, GameSoundCue>();
        private readonly System.Collections.Generic.Dictionary<AudioSource, Coroutine> fadingVoices =
            new System.Collections.Generic.Dictionary<AudioSource, Coroutine>();
        private readonly System.Collections.Generic.Dictionary<AudioSource, float> voiceVolumes =
            new System.Collections.Generic.Dictionary<AudioSource, float>();

        // Pitch lets a single authored clip step through a short rising sequence; other cues play at 1.
        public bool TryPlay(GameSoundCue cue, bool priority = false, float pitch = 1f)
        {
            int index = (int)cue;
            if (index >= 0 && (clips == null || index >= clips.Length || clips[index] == null))
            {
                if (missingClips.Add(cue))
                    Debug.LogWarning($"[GameSound] Missing clip for {cue} in scene '{gameObject.scene.name}'. In the editor, run Food Isekai > Repair Perk Audio Links, then save the scene.", this);
                return false;
            }
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
            CancelFade(voice);
            voice.Stop();
            voice.pitch = Mathf.Clamp(pitch, .5f, 2f);
            activeCues[voice] = cue;
            voice.PlayOneShot(clips[index], Mathf.Clamp01(masterVolume) * Mathf.Pow(10f, GetGainDb(cue) / 20f));
            return true;
        }

        // Stops only matching effect voices, leaving music and unrelated gameplay sounds alone.
        public void StopCue(GameSoundCue cue)
        {
            foreach (var entry in activeCues)
            {
                AudioSource voice = entry.Key;
                if (entry.Value != cue || voice == null || !voice.isPlaying || fadingVoices.ContainsKey(voice)) continue;
                if (!isActiveAndEnabled)
                {
                    voice.Stop();
                    continue;
                }
                if (!voiceVolumes.ContainsKey(voice)) voiceVolumes[voice] = voice.volume;
                fadingVoices[voice] = StartCoroutine(FadeOutVoice(voice));
            }
        }

        private IEnumerator FadeOutVoice(AudioSource voice)
        {
            float start = voice.volume;
            float elapsed = 0f;
            while (elapsed < stopFadeSeconds && voice != null)
            {
                elapsed += Time.unscaledDeltaTime;
                voice.volume = start * (1f - Mathf.SmoothStep(0f, 1f, elapsed / stopFadeSeconds));
                yield return null;
            }
            if (voice == null) yield break;
            voice.Stop();
            voice.volume = voiceVolumes[voice];
            fadingVoices.Remove(voice);
        }

        // A voice reused mid-fade starts again at its authored volume.
        private void CancelFade(AudioSource voice)
        {
            if (!fadingVoices.TryGetValue(voice, out Coroutine fade)) return;
            if (fade != null) StopCoroutine(fade);
            fadingVoices.Remove(voice);
            voice.volume = voiceVolumes[voice];
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
                case GameSoundCue.PerkFocus:
                case GameSoundCue.PerkHoldTick:
                case GameSoundCue.PerkCancel:
                case GameSoundCue.PerkUnavailable:
                case GameSoundCue.PerkPurchase:
                case GameSoundCue.SmallPerkFocus:
                case GameSoundCue.SmallPerkHoldTick:
                case GameSoundCue.SmallPerkCancel:
                case GameSoundCue.SmallPerkUnavailable:
                case GameSoundCue.SmallPerkPurchase:
                case GameSoundCue.ReadyStepOn:
                case GameSoundCue.ReadyStepOff:
                case GameSoundCue.ReadyHoldTick:
                case GameSoundCue.ReadyContested:
                case GameSoundCue.ReadyConfirm:
                    return playerFeedbackGainDb;
                case GameSoundCue.CurtainClose:
                case GameSoundCue.CurtainOpen:
                    return curtainGainDb;
                default: return 0f;
            }
        }

        private void OnDisable()
        {
            if (voices != null)
                foreach (AudioSource voice in voices) if (voice != null) voice.Stop();
            foreach (var entry in voiceVolumes) if (entry.Key != null) entry.Key.volume = entry.Value;
            fadingVoices.Clear();
            System.Array.Clear(nextAllowedTime, 0, nextAllowedTime.Length);
            activeCues.Clear();
        }
    }
}
