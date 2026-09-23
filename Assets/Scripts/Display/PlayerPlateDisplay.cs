using FoodIsekaiZ.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // Displays inventory state on the authored player plate without changing its layout.
    public sealed class PlayerPlateDisplay : MonoBehaviour
    {
        [SerializeField] private FoodIsekaiZPlayerState playerState;
        [SerializeField] private GameObject[] playerPlates;
        [SerializeField] private TMP_Text playerName;
        [SerializeField] private Image foodImage;
        [SerializeField] private Image drinkImage;
        [SerializeField] private Image[] authoredFoodImages;
        [SerializeField] private Sprite[] foodSprites;
        [SerializeField] private MealFoodDisplay mealFoodDisplay;
        [SerializeField] private GameObject moneyVisual;
        [SerializeField] private PlayerMoneyMeter moneyMeter;
        [SerializeField] private InventoryAppearEffect foodAppearEffect;
        [SerializeField] private InventoryAppearEffect drinkAppearEffect;
        [SerializeField] private InventoryAppearEffect moneyAppearEffect;

        private int displayedPlayerId = -1;
        private FoodType displayedFood = (FoodType)(-1);
        private int displayedMoney = -1;
        private int displayedMeal = -1;
        private FloorAnimatedSprite pendingPickup;
        private int pendingPlaybackVersion;
        private FoodType pendingFood;

        // The spawner supplies the scene meal display to each prefab instance.
        public void BindMealDisplay(MealFoodDisplay display)
        {
            mealFoodDisplay = display;
            displayedMeal = -1;
        }

        // Hide the held illustration until this specific pickup flight finishes or is interrupted.
        public void WaitForPickup(FloorAnimatedSprite motion)
        {
            pendingPickup = motion;
            pendingPlaybackVersion = motion.PlaybackVersion;
            pendingFood = playerState.HeldFood;
            displayedFood = (FoodType)(-1);
            foodImage.enabled = false;
            if (drinkImage != null) drinkImage.enabled = false;
            HideAuthoredFood();
        }

        private void OnDisable()
        {
            pendingPickup = null;
        }

        private void OnEnable()
        {
            displayedPlayerId = -1;
            displayedFood = (FoodType)(-1);
            displayedMoney = -1;
        }

        private void LateUpdate()
        {
            if (playerState == null || playerState.PlayerId <= 0)
            {
                return;
            }

            if (displayedPlayerId != playerState.PlayerId)
            {
                displayedPlayerId = playerState.PlayerId;
                int plateIndex = Mathf.Clamp(displayedPlayerId - 1, 0, playerPlates.Length - 1);
                for (int i = 0; i < playerPlates.Length; i++)
                {
                    playerPlates[i].SetActive(i == plateIndex);
                }
                playerName.text = $"PLAYER {displayedPlayerId}";
            }

            bool waitingForPickup = pendingPickup != null && pendingPickup.isActiveAndEnabled &&
                pendingPickup.PlaybackVersion == pendingPlaybackVersion && pendingPickup.IsAnimating &&
                playerState.HeldFood == pendingFood;
            if (!waitingForPickup) pendingPickup = null;
            int meal = mealFoodDisplay != null ? mealFoodDisplay.MealIndex : 0;
            if (!waitingForPickup && (displayedFood != playerState.HeldFood || displayedMeal != meal))
            {
                displayedMeal = meal;
                displayedFood = playerState.HeldFood;
                RefreshFood();
            }

            if (displayedMoney != playerState.CarriedMoney)
            {
                bool receivedMoney = playerState.CarriedMoney > displayedMoney && playerState.CarriedMoney > 0;
                displayedMoney = playerState.CarriedMoney;
                moneyVisual.SetActive(displayedMoney > 0);
                moneyMeter?.SetAmount(displayedMoney, playerState.MaximumCarriedMoney);
                if (receivedMoney)
                {
                    moneyAppearEffect?.Play();
                }
            }
        }

        private void RefreshFood()
        {
            int foodIndex = (int)displayedFood - 1;
            Sprite sprite = mealFoodDisplay != null ? mealFoodDisplay.GetSprite(displayedFood)
                : foodSprites != null && foodIndex >= 0 && foodIndex < foodSprites.Length ? foodSprites[foodIndex] : null;
            bool hasFood = sprite != null;
            HideAuthoredFood();
            if (authoredFoodImages != null && authoredFoodImages.Length > 0)
            {
                foodImage.enabled = false;
                if (drinkImage != null) drinkImage.enabled = false;
                foreach (Image image in authoredFoodImages)
                {
                    if (image == null || !hasFood || image.sprite != sprite) continue;
                    image.enabled = true;
                    image.GetComponent<InventoryAppearEffect>()?.PlayGrow();
                    return;
                }
            }

            bool showDrink = hasFood && displayedFood == FoodType.Food5 && drinkImage != null;
            foodImage.enabled = hasFood && !showDrink;
            if (drinkImage != null)
            {
                drinkImage.enabled = showDrink;
                if (showDrink) drinkImage.sprite = sprite;
            }

            if (hasFood && !showDrink)
            {
                foodImage.sprite = sprite;
            }

            if (hasFood)
            {
                InventoryAppearEffect effect = showDrink ? drinkAppearEffect : foodAppearEffect;
                effect?.PlayGrow();
            }
        }

        private void HideAuthoredFood()
        {
            if (authoredFoodImages == null) return;
            foreach (Image image in authoredFoodImages)
            {
                if (image != null) image.enabled = false;
            }
        }
    }
}
