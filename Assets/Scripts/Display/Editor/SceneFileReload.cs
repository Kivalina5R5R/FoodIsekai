using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace FoodIsekaiZ.Display.Editor
{
    // Picks up scene files that were changed on disk while the same scene stayed open in the editor.
    // Without this, Play keeps running the older in-memory scene and an external edit never appears.
    // A scene with no unsaved changes is reloaded straight away; a scene with unsaved changes is only
    // reloaded after confirmation, so nothing authored in the editor is discarded silently.
    [InitializeOnLoad]
    internal static class SceneFileReload
    {
        private const string Key = "FoodIsekaiZ.SceneFileTime.";

        static SceneFileReload()
        {
            EditorSceneManager.sceneOpened += (scene, mode) => Remember(scene.path);
            EditorSceneManager.sceneSaved += scene => Remember(scene.path);
            EditorApplication.focusChanged += focused => { if (focused) EditorApplication.delayCall += Check; };
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += Check;
            };
            EditorApplication.delayCall += Check;
        }

        private static void Remember(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            SessionState.SetString(Key + path, File.GetLastWriteTimeUtc(path).Ticks.ToString());
        }

        // The time the editor last read or wrote this scene; before the first record, the editor's start time.
        private static DateTime KnownTime(string path)
        {
            string stored = SessionState.GetString(Key + path, string.Empty);
            if (long.TryParse(stored, out long ticks)) return new DateTime(ticks, DateTimeKind.Utc);
            return Process.GetCurrentProcess().StartTime.ToUniversalTime();
        }

        private static void Check()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                string path = scene.path;
                if (!scene.isLoaded || string.IsNullOrEmpty(path) || !File.Exists(path)) continue;
                DateTime onDisk = File.GetLastWriteTimeUtc(path);
                if (onDisk <= KnownTime(path).AddSeconds(1)) continue;
                Remember(path);
                if (scene.isDirty && !EditorUtility.DisplayDialog("Scene changed on disk",
                        $"{path} was changed outside the editor, but the open copy has unsaved changes.\n\n" +
                        "Reload it from disk (unsaved editor changes in this scene are discarded), or keep the open copy?",
                        "Reload from disk", "Keep open copy"))
                {
                    Debug.LogWarning($"[SceneFileReload] Kept the open copy of {path}; the newer file on disk is not loaded. Saving now overwrites it.");
                    continue;
                }
                OpenSceneMode mode = SceneManager.sceneCount == 1 ? OpenSceneMode.Single : OpenSceneMode.Additive;
                bool active = SceneManager.GetActiveScene() == scene;
                if (mode == OpenSceneMode.Additive) EditorSceneManager.CloseScene(scene, true);
                Scene reloaded = EditorSceneManager.OpenScene(path, mode);
                if (active) SceneManager.SetActiveScene(reloaded);
                Debug.Log($"[SceneFileReload] Reloaded {path} from disk so the editor uses the latest file.");
                return;
            }
        }
    }
}
