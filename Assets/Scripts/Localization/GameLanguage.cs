using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FoodIsekaiZ.Localization
{
    public enum GameLanguageId
    {
        English = 0,
        Thai = 1
    }

    // Holds the selected display language and remembers it between game launches.
    // Pressing T anywhere in the game switches between English and Thai.
    public static class GameLanguage
    {
        private const string PreferenceKey = "FoodIsekaiZ.Language";
        private static bool loaded;
        private static GameLanguageId current;

        public static event Action<GameLanguageId> Changed;

        public static GameLanguageId Current
        {
            get
            {
                Load();
                return current;
            }
        }

        public static bool IsThai => Current == GameLanguageId.Thai;

        // Runtime-written labels choose their wording here; an empty Thai string falls back to English.
        public static string Pick(string english, string thai)
        {
            return IsThai && !string.IsNullOrEmpty(thai) ? thai : english;
        }

        // Saves the choice immediately so a crash or forced quit still keeps it.
        public static void Set(GameLanguageId language)
        {
            Load();
            if (current == language) return;
            current = language;
            PlayerPrefs.SetInt(PreferenceKey, (int)language);
            PlayerPrefs.Save();
            Changed?.Invoke(language);
        }

        public static void Toggle()
        {
            Set(Current == GameLanguageId.Thai ? GameLanguageId.English : GameLanguageId.Thai);
        }

        private static void Load()
        {
            if (loaded) return;
            loaded = true;
            int saved = PlayerPrefs.GetInt(PreferenceKey, (int)GameLanguageId.English);
            current = Enum.IsDefined(typeof(GameLanguageId), saved) ? (GameLanguageId)saved : GameLanguageId.English;
        }

        // Domain reload can be disabled in the editor, so clear static state before each play session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            loaded = false;
            Changed = null;
        }

        // Creates the persistent key listener so no scene needs an authored object for it.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateToggleListener()
        {
            var listener = new GameObject("GameLanguageToggle");
            listener.hideFlags = HideFlags.HideInHierarchy;
            UnityEngine.Object.DontDestroyOnLoad(listener);
            listener.AddComponent<GameLanguageToggle>();
        }
    }

    public sealed class GameLanguageToggle : MonoBehaviour
    {
        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tKey.wasPressedThisFrame) GameLanguage.Toggle();
        }
    }
}
