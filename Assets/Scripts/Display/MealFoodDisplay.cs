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
            if (stationImages == null) return;
            int meal = MealIndex;
            if (displayedMeal == meal) return;
            bool animate = displayedMeal >= 0;
            displayedMeal = meal;
            for (int i = 0; i < stationImages.Length; i++)
            {
                Sprite sprite = GetSprite((FoodType)(i + (int)FoodType.Food1));
                MealFoodSwapEffect transition = stationTransitions != null && i < stationTransitions.Length
                    ? stationTransitions[i] : null;
                if (transition != null)
                {
                    if (animate && (menuTransition == null || !menuTransition.IsVisible)) transition.Play(sprite);
                    else transition.ShowImmediately(sprite);
                }
                else if (stationImages[i] != null)
                    stationImages[i].sprite = sprite;
            }
        }
    }
}
