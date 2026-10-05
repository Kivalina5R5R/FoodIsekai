using System.IO;
using UnityEngine;

namespace FoodIsekaiZ.Configuration
{
    // Round timing that operators can change without rebuilding, read from
    // StreamingAssets/GameFlowConfig.json. A missing or broken file falls back to the defaults below.
    [System.Serializable]
    public sealed class GameFlowConfig
    {
        public const string FileName = "GameFlowConfig.json";

        // Seconds the result screen stays up before the game restarts for the next group.
        // Zero or a negative value turns the automatic restart off.
        public float resultRestartSeconds = 25f;

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
