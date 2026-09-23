using FoodIsekaiZ.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // Supplies the current meal artwork to food displays and refreshes station indicators.
    public sealed class MealFoodDisplay : MonoBehaviour
    {
        [SerializeField] private FoodIsekaiZGameManager gameManager;
        [SerializeField] private MealMenuTransition menuTransition;
        [SerializeField] private Sprite[] breakfastSprites;
        [SerializeField] private Sprite[] lunchSprites;
        [SerializeField] private Sprite[] dinnerSprites;
        [SerializeField] private Image[] stationImages;
        [SerializeField] private MealFoodSwapEffect[] stationTransitions;
        [SerializeField] private Image bankPurse;
        [SerializeField] private MealFoodSwapEffect bankTransition;
        private int displayedMeal = -1;
        private bool stationsHidden;

        private bool IsWaitingForFirstMeal => gameManager != null && (gameManager.IsWaitingForStartup ||
            (gameManager.UsesMealWaves && gameManager.CurrentMealWavePhase == MealWavePhase.NotStarted));

        private bool IsBreakOrResults => gameManager != null && gameManager.UsesMealWaves &&
            (gameManager.CurrentMealWavePhase == MealWavePhase.Intermission ||
             gameManager.CurrentMealWavePhase == MealWavePhase.Completed);

        public int MealIndex => gameManager != null && gameManager.UsesMealWaves
            ? Mathf.Clamp(gameManager.CurrentWaveNumber - 1, 0, 2) : 0;

        // Food1 through Food5 map to meat, seafood, starter, dessert, and drink.
        public Sprite GetSprite(FoodType food)
        {
            int index = (int)food - (int)FoodType.Food1;
            Sprite[] sprites = MealIndex == 2 ? dinnerSprites : MealIndex == 1 ? lunchSprites : breakfastSprites;
            return sprites != null && index >= 0 && index < sprites.Length ? sprites[index] : null;
        }

        // Prepare hidden food after the guide; the bounce waits for the menu page to leave.
        public void RevealAfterGuide()
        {
            // A covered menu will prepare all visuals through its phase reveal event.
            if (menuTransition != null && menuTransition.IsVisible) return;
            RevealStations();
        }

        private void RevealStations()
        {
            if (IsWaitingForFirstMeal)
            {
                RefreshStations();
                return;
            }
            if (IsBreakOrResults)
            {
                HideStations();
                return;
            }
            RefreshStations();
            if (bankTransition != null && bankPurse != null)
                bankTransition.PlayReveal(bankPurse.sprite, menuTransition);
            if (stationTransitions == null) return;
            for (int i = 0; i < stationTransitions.Length; i++)
            {
                if (stationTransitions[i] != null)
                    stationTransitions[i].PlayReveal(GetSprite((FoodType)(i + (int)FoodType.Food1)), menuTransition);
            }
        }

        private void OnEnable()
        {
            displayedMeal = -1;
            stationsHidden = false;
            if (menuTransition != null) menuTransition.RevealStarting += RevealStations;
            if (gameManager != null) gameManager.MealWaveDisplayChanged += RefreshStations;
            RefreshStations();
        }

        private void OnDisable()
        {
            if (menuTransition != null) menuTransition.RevealStarting -= RevealStations;
            if (gameManager != null) gameManager.MealWaveDisplayChanged -= RefreshStations;
        }

        private void RefreshStations()
        {
            // Scene initialization precedes the first covered meal. Never present full-size
            // menu art in this interval, even if the station hierarchy becomes active first.
            if (IsWaitingForFirstMeal)
            {
                stationsHidden = true;
                if (bankTransition != null && bankPurse != null) bankTransition.PrepareHidden(bankPurse.sprite);
                if (stationTransitions != null)
                {
                    for (int i = 0; i < stationTransitions.Length; i++)
                        stationTransitions[i]?.PrepareHidden(GetSprite((FoodType)(i + (int)FoodType.Food1)));
                }
                return;
            }
            if (IsBreakOrResults)
            {
                HideStations();
                return;
            }
            if (stationImages == null) return;
            int meal = MealIndex;
            if (displayedMeal == meal && !stationsHidden) return;
            bool reveal = stationsHidden;
            stationsHidden = false;
            bool animate = displayedMeal >= 0;
            displayedMeal = meal;
            if (reveal && bankTransition != null && bankPurse != null)
                bankTransition.PlayReveal(bankPurse.sprite, menuTransition);
            else if (reveal && bankPurse != null) bankPurse.enabled = true;
            for (int i = 0; i < stationImages.Length; i++)
            {
                Sprite sprite = GetSprite((FoodType)(i + (int)FoodType.Food1));
                MealFoodSwapEffect transition = stationTransitions != null && i < stationTransitions.Length
                    ? stationTransitions[i] : null;
                if (transition != null)
                {
                    if (reveal || (menuTransition != null && menuTransition.IsVisible))
                        transition.PlayReveal(sprite, menuTransition);
                    else if (animate && (menuTransition == null || !menuTransition.IsVisible)) transition.Play(sprite);
                    else transition.ShowImmediately(sprite);
                }
                else if (stationImages[i] != null)
                {
                    stationImages[i].sprite = sprite;
                    stationImages[i].enabled = true;
                }
            }
        }

        private void HideStations()
        {
            if (stationsHidden) return;
            stationsHidden = true;
            if (bankTransition != null) bankTransition.PlayHide(menuTransition);
            else if (bankPurse != null) bankPurse.enabled = false;
            if (stationImages == null) return;
            for (int i = 0; i < stationImages.Length; i++)
            {
                MealFoodSwapEffect transition = stationTransitions != null && i < stationTransitions.Length
                    ? stationTransitions[i] : null;
                if (transition != null) transition.PlayHide(menuTransition);
                else if (stationImages[i] != null) stationImages[i].enabled = false;
            }
        }
    }
}
