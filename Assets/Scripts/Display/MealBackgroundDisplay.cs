using FoodIsekaiZ.Gameplay;
using UnityEngine;

namespace FoodIsekaiZ.Display
{
    // Selects authored background layers without changing their artwork or lighting settings.
    public sealed class MealBackgroundDisplay : MonoBehaviour
    {
        [SerializeField] private FoodIsekaiZGameManager gameManager;
        [SerializeField] private GameObject nightBackground;
        [SerializeField] private GameObject morningLight;
        [SerializeField] private GameObject lunchLight;
        [SerializeField] private GameObject dinnerLight;

        private void OnEnable()
        {
            if (gameManager != null) gameManager.MealWaveDisplayChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (gameManager != null) gameManager.MealWaveDisplayChanged -= Refresh;
        }

        private void Refresh()
        {
            // Keep the current meal through breaks and results; a new game starts with morning.
            int wave = gameManager != null && gameManager.UsesMealWaves
                ? gameManager.CurrentWaveNumber : 0;
            bool lunch = wave == 2;
            bool dinner = wave >= 3;
            SetVisible(nightBackground, dinner);
            SetVisible(morningLight, !lunch && !dinner);
            SetVisible(lunchLight, lunch);
            SetVisible(dinnerLight, dinner);
        }

        private static void SetVisible(GameObject layer, bool visible)
        {
            if (layer != null && layer.activeSelf != visible) layer.SetActive(visible);
        }
    }
}
