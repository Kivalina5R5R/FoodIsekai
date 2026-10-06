using System.IO;
using UnityEngine;

namespace FoodIsekaiZ.Configuration
{
    // Round timing, sound levels and the language key that operators can change without rebuilding,
    // read from StreamingAssets/GameFlowConfig.json. A missing or broken file falls back to the defaults below,
    // and a field left out of the file keeps its default.
    [System.Serializable]
    public sealed class GameFlowConfig
    {
        public const string FileName = "GameFlowConfig.json";

        // One sound effect's level in decibels, named exactly like a GameSoundCue (for example "MoneyCollected").
        // 0 is the clip's own level, negative is quieter and positive is louder.
        [System.Serializable]
        public sealed class SoundGain
        {
            public string sound;
            public float gainDb;
        }

        // 0 keeps the result screen open; 1 allows automatic restart after resultRestartSeconds.
        [SerializeField] private int resultRestartEnabled = 1;
        public bool IsResultRestartEnabled => resultRestartEnabled == 1;

        // Seconds the result screen stays up before the game restarts for the next group.
        // Zero or a negative value turns the automatic restart off.
        public float resultRestartSeconds = 25f;

        // Keyboard key that switches between English and Thai, named like the Input System Key enum
        // ("T", "L", "F1", "Digit1" and so on).
        public string languageToggleKey = "T";

        // Music levels from 0 to 1. The game BGM plays during service; the intro music plays from
        // the start of the scene until Lunar has finished the introduction.
        public float bgmVolume = 0.6f;
        public float introMusicVolume = 0.4f;

        // Multipliers from 0 to 1 applied on top of the scene's own levels:
        // every gameplay sound effect, and Lunar's spoken lines.
        public float soundEffectsVolume = 1f;
        public float lunarVoiceVolume = 1f;

        // Level from 0 to 1 of Lunar's footsteps at her standing spot; steps further away stay
        // proportionally quieter, as set on the guide.
        public float lunarFootstepVolume = 0.3f;

        // Per-effect levels that replace the scene's values for the listed sounds only.
        public SoundGain[] soundEffectGainsDb = new SoundGain[0];

        // Reads the file each time so an edit on the installation PC applies from the next round.
        public static GameFlowConfig Load()
        {
            string path = Path.Combine(Application.streamingAssetsPath, FileName);
            try
            {
                if (File.Exists(path))
                {
                    GameFlowConfig config = JsonUtility.FromJson<GameFlowConfig>(File.ReadAllText(path));
                    if (config != null) return config;
                }
                Debug.LogWarning($"[GameFlowConfig] {path} not found; using default values.");
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[GameFlowConfig] Could not read {path}; using default values. {exception.Message}");
            }
            return new GameFlowConfig();
        }
    }
}
