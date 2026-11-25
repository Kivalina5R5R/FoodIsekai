using Fortal.Arena;
using FoodIsekaiZ.Localization;
using FoodIsekaiZ.Players;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace FoodIsekaiZ.Display
{
    // Connects the Fortal web launcher's UDP commands (FortalArenaRemote, the same package Bounty
    // Hunter uses) to the game: its TH/EN button sets GameLanguage, and its Tutorial button opens
    // the HOW TO PLAY board on the wall (WallHowToPlay). The board may open only during the ready
    // phase, before every player has picked a number, and it drops away when the game starts.
    // H or L on the keyboard opens and closes the board too, without the launcher.
    // It adds itself every time a scene loads (a new round reloads the scene), so no scene set-up
    // can be left out of a build.
    // Console lines start with [ArenaRemoteLink].
    public sealed class ArenaRemoteLink : MonoBehaviour
    {
        private const string EnglishFontName = "Poppins-Bold SDF";
        private const string ThaiFontName = "NotoSansThai_Condensed-Medium SDF";

        // The link of the scene loaded now; an older one being destroyed must not clear its callbacks.
        private static ArenaRemoteLink current;

        private WallHowToPlay board;
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
            // The language works whatever is in the scene.
            FortalArenaRemote.GetLanguage = () => GameLanguage.IsThai ? "th" : "en";
            FortalArenaRemote.SetLanguage = SetLanguage;
            Debug.Log("[ArenaRemoteLink] The launcher's TH/EN switches the game's language");

            SceneManager.sceneLoaded += OnSceneLoaded;
            ConnectScene();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ConnectScene();
        }

        private static void ConnectScene()
        {
            if (current != null) return;

            RectTransform wall = DisplayManager.FindWallCanvas();
            if (wall == null)
            {
                Debug.LogWarning("[ArenaRemoteLink] No wall canvas found: the launcher cannot open the tutorial");
                return;
            }

            var host = new GameObject("Arena Remote Link");
            current = host.AddComponent<ArenaRemoteLink>();
            current.Connect(wall);
        }

        // code: "th" or "en"; anything else is ignored.
        private static void SetLanguage(string code)
        {
            if (code == "th") GameLanguage.Set(GameLanguageId.Thai);
            else if (code == "en") GameLanguage.Set(GameLanguageId.English);
        }

        private void Connect(RectTransform wall)
        {
            readySelection = FindFirstObjectByType<PlayerReadySelection>(FindObjectsInactive.Include);

            var boardObject = new GameObject("HOW TO PLAY Board", typeof(RectTransform));
            board = boardObject.AddComponent<WallHowToPlay>();
            board.Initialise(wall, FindFont(EnglishFontName), FindFont(ThaiFontName));

            FortalArenaRemote.IsTutorialOpen = () => board != null && board.IsOpen;
            FortalArenaRemote.CanOpenTutorial = CanOpen;
            FortalArenaRemote.OpenTutorial = OpenBoard;
            FortalArenaRemote.CloseTutorial = CloseBoard;
            // The launcher's BACK / NEXT turn the board's pages (kit 3.1).
            FortalArenaRemote.GetTutorialPage = () => board != null ? board.PageNumber : 1;
            FortalArenaRemote.GetTutorialPageCount = () => board != null ? board.PageCount : 0;
            FortalArenaRemote.ShowTutorialPage = page => { if (board != null) board.GoToPage(page); };
            Debug.Log("[ArenaRemoteLink] The launcher can open the tutorial during the ready phase", this);
        }

        // Only while the players pick their numbers: from the end of the intro until the game starts.
        // It never opens by itself.
        private bool CanOpen()
        {
            return board != null && (readySelection == null || readySelection.HasBegun);
        }

        private void OpenBoard()
        {
            if (CanOpen()) board.Open();
        }

        private void CloseBoard()
        {
            if (board == null) return;
            board.Close();
        }

        private void Update()
        {
            if (board == null) return;

            // The board drops away as the game starts.
            if (board.IsOpen && !CanOpen()) board.Close();

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !(keyboard.hKey.wasPressedThisFrame || keyboard.lKey.wasPressedThisFrame)) return;
            if (board.IsOpen) board.Close();
            else OpenBoard();
        }

        private void OnDestroy()
        {
            if (current != this) return;

            current = null;
            FortalArenaRemote.IsTutorialOpen = null;
            FortalArenaRemote.CanOpenTutorial = null;
            FortalArenaRemote.OpenTutorial = null;
            FortalArenaRemote.CloseTutorial = null;
            FortalArenaRemote.GetTutorialPage = null;
            FortalArenaRemote.GetTutorialPageCount = null;
            FortalArenaRemote.ShowTutorialPage = null;
        }

        // A font asset the scene has already loaded, by name; null when it is not loaded.
        private static TMP_FontAsset FindFont(string fontName)
        {
            foreach (TMP_FontAsset font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            {
                if (font != null && font.name == fontName) return font;
            }

            return null;
        }
    }
}
