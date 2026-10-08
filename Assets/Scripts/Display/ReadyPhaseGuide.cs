using FoodIsekaiZ.Audio;
using FoodIsekaiZ.Players;
using UnityEngine;

namespace FoodIsekaiZ.Display
{
    // The ready-phase guide lives on a separately authored wall-display layer.
    public sealed class ReadyPhaseGuide : MonoBehaviour
    {
        [SerializeField] private PlayerReadySelection selection;
        [SerializeField] private MealMenuTransition menuTransition;
        [InspectorName("NPC Guide Player Prefab")]
        [SerializeField] private NpcGuidePresentation guidePrefab;
        [SerializeField, Range(0f, 1f)] private float standingWidthFraction = 0.55f;
        // The intro music fades out once Lunar has walked off.
        [SerializeField] private FoodIsekaiZBgmPlayer bgmPlayer;
        // Where Lunar says "spot" (English) and "จุด" (Thai) in her intro voice line; the floor number
        // spots appear at that word. Measured from Lunar_Eng_Intro and Lunar_Thai_Intro.
        [SerializeField, Min(0f)] private float englishSpotSeconds = 6.6f;
        [SerializeField, Min(0f)] private float thaiSpotSeconds = 7.2f;
        private NpcGuidePresentation instance;
        private bool spawned;
        private bool exitRequested;
        private bool coverRequested;
        private Vector2 exitPosition;
        public bool HasStartedExit => exitRequested;

        // The floor number spots appear when Lunar says "spot" / "จุด", or once she has finished speaking.
        public bool IsReadyForSelection => guidePrefab == null ||
            (instance != null && (instance.HasFinishedSpeaking || HasReachedSpotWord()));

        private bool HasReachedSpotWord()
        {
            float cue = FoodIsekaiZ.Localization.GameLanguage.IsThai ? thaiSpotSeconds : englishSpotSeconds;
            return instance.VoiceSeconds >= cue;
        }

        // Ready selection waits for this departure before releasing gameplay.
        public bool TryFinish()
        {
            if (!exitRequested)
            {
                // Let Lunar finish her voiced intro; walking out would cut the line off.
                if (instance != null && instance.gameObject.activeInHierarchy && !instance.HasFinishedSpeaking)
                    return false;
                exitRequested = true;
                if (instance != null) instance.WalkOut(exitPosition);
            }
            bool departed = instance == null || !instance.gameObject.activeInHierarchy || instance.HasExited;
            if (!departed) return false;
            if (!coverRequested)
            {
                coverRequested = true;
                // Lunar is fully off screen; the music fades while the menu page covers the wall.
                if (bgmPlayer != null) bgmPlayer.EndIntroMusic();
                if (menuTransition != null) StartCoroutine(menuTransition.Cover("BREAKFAST"));
            }
            return menuTransition == null || menuTransition.IsCovered;
        }

        private void LateUpdate()
        {
            if (selection == null) return;
            if (selection.IsCompleted)
            {
                if (instance != null && instance.gameObject.activeSelf) instance.gameObject.SetActive(false);
                return;
            }
            if (spawned || !selection.IsSelecting || guidePrefab == null) return;
            spawned = true;
            instance = Instantiate(guidePrefab, transform, false);
            Rect area = ((RectTransform)transform).rect;
            exitPosition = new Vector2(area.xMax + area.width * .5f, area.yMin);
            Vector2 destination = new Vector2(Mathf.Lerp(area.xMin, area.xMax, standingWidthFraction), area.yMin);
            instance.WalkIn(exitPosition, destination, area.width);
        }

        private void OnDisable()
        {
            if (instance != null) instance.gameObject.SetActive(false);
        }
    }
}
