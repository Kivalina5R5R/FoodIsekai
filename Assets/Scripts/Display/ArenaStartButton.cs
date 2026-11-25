using FoodIsekaiZ.Players;
using Fortal.Arena;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FoodIsekaiZ.Display
{
    // The launcher's START button (kit 3.2): shown until the game starts. Lunar walks out and speaks
    // first; the floor number cards come up only once START has been pressed (the launcher has taken
    // the players' credit), so a round, including the one after the result's restart, cannot be
    // played without paying. In the Editor, press START from Tools > Fortal > Arena Remote Tester.
    // It adds itself every time a scene loads (a new round reloads the scene). Console lines start
    // with [ArenaStartButton].
    public sealed class ArenaStartButton : MonoBehaviour
    {
        // The button of the scene loaded now; an older one being destroyed must not remove START.
        private static ArenaStartButton current;

        private PlayerReadySelection readySelection;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            current = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            Attach();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Attach();
        }

        private static void Attach()
        {
            if (FindFirstObjectByType<ArenaStartButton>() != null) return;

            var selection = FindFirstObjectByType<PlayerReadySelection>(FindObjectsInactive.Include);
            if (selection == null)
            {
                Debug.LogWarning("[ArenaStartButton] No ready phase: the launcher has no START");
                return;
            }

            current = new GameObject("Arena Start Button").AddComponent<ArenaStartButton>();
            current.Connect(selection);
        }

        private void Connect(PlayerReadySelection selection)
        {
            readySelection = selection;
            // Simulation mode has no launcher to press START, so the number spots come up as soon as
            // Lunar reaches them in her line.
            var uwbManager = FindAnyObjectByType<Fortal.UWB.UWBManager>();
            if (uwbManager != null && uwbManager.IsSimulationMode)
            {
                Debug.Log("[ArenaStartButton] Simulation mode: the number spots show without START", this);
                return;
            }

            FortalArenaButtons.AddStart(Press, () => readySelection != null && readySelection.IsWaitingForStart);
            readySelection.WaitForStartPress();
            Debug.Log("[ArenaStartButton] The number cards wait for the launcher's START", this);
        }

        private void Press()
        {
            Debug.Log("[ArenaStartButton] START from the launcher: the number cards come up", this);
            readySelection.StartPressed();
        }

        private void OnDestroy()
        {
            if (current != this) return;
            current = null;
            FortalArenaButtons.Remove("start");
        }
    }
}
