using UnityEngine;
using System.IO;

namespace FoodIsekaiZ.Configuration
{
    public abstract class BaseConfigManager<TManager, TConfig> : MonoBehaviour
        where TManager : BaseConfigManager<TManager, TConfig>
        where TConfig : class, new()
    {
        public static TManager Instance { get; private set; }
        public string fileName;
        public bool debugMode = false;

        public TConfig Config;
        private string filePath;

        protected abstract string DefaultFileName { get; }

        protected virtual void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = (TManager)this;
            DontDestroyOnLoad(gameObject);

            if (string.IsNullOrEmpty(fileName))
                fileName = DefaultFileName;

            filePath = Path.Combine(Application.persistentDataPath, $"{fileName}.json");
            LoadConfig();
        }

        protected virtual void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void SaveConfig()
        {
            string json = JsonUtility.ToJson(Config, true);
            File.WriteAllText(filePath, json);
            if (debugMode)
                Debug.Log($"{fileName} Saved to: {filePath}");
        }

        public void LoadConfig()
        {
            if (debugMode)
                Debug.Log($"Load {fileName} from: {filePath}");

            if (File.Exists(filePath))
            {
                string json = File.ReadAllText(filePath);
                Config = JsonUtility.FromJson<TConfig>(json);
            }
            else
            {
                if (debugMode)
                    Debug.Log($"No {fileName} file exists, creating default...");

                Config = new TConfig();
                SaveConfig();
            }
        }
    }
}
