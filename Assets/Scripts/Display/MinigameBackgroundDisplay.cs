using FoodIsekaiZ.Gameplay;
using UnityEngine;

namespace FoodIsekaiZ.Display
{
    // Shows the back-kitchen artwork while a minigame round runs and the tavern artwork otherwise.
    public sealed class MinigameBackgroundDisplay : MonoBehaviour
    {
        [SerializeField] private FoodIsekaiZGameManager gameManager;
        [SerializeField] private GameObject kitchenBackground;

        private void OnEnable()
        {
            if (gameManager != null) gameManager.MealWaveDisplayChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (gameManager != null) gameManager.MealWaveDisplayChanged -= Refresh;
        }

        // The phase changes behind the transition cover, so swapping at once is never seen.
        private void Refresh()
        {
            bool minigame = gameManager != null && gameManager.CurrentMealWavePhase == MealWavePhase.Minigame;
            if (kitchenBackground != null && kitchenBackground.activeSelf != minigame)
            {
                kitchenBackground.SetActive(minigame);
            }
        }
    }
}
