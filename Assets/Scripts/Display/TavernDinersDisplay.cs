using System;
using FoodIsekaiZ.Gameplay;
using UnityEngine;
using UnityEngine.Video;

namespace FoodIsekaiZ.Display
{
    // Fills the background tables with eating customers as dishes are served during a meal.
    public sealed class TavernDinersDisplay : MonoBehaviour
    {
        [Serializable]
        private struct DinerTable
        {
            [SerializeField] private TavernDinerVideo video;
            // The table appears once this many dishes have been served in the current meal.
            [SerializeField] private int dishesToAppear;
            // This table's clip for each meal, breakfast first; each is painted for that meal's
            // lighting. Meals past the end use the last clip; an empty entry keeps the table empty.
            [SerializeField] private VideoClip[] mealClips;

            public TavernDinerVideo Video => video;
            public int DishesToAppear => dishesToAppear;

            // The clip for the given meal (1 = breakfast), or null for an empty table.
            public VideoClip ClipFor(int meal)
            {
                if (mealClips == null || mealClips.Length == 0) return null;
                return mealClips[Mathf.Clamp(meal - 1, 0, mealClips.Length - 1)];
            }
        }

        [SerializeField] private FoodIsekaiZGameManager gameManager;
        [SerializeField] private DinerTable[] tables = Array.Empty<DinerTable>();

        private int dishesServedThisMeal;
        private int trackedMeal = -1;

        private void OnEnable()
        {
            if (gameManager == null) return;
            gameManager.FoodServed += OnFoodServed;
            gameManager.MealWaveDisplayChanged += OnMealDisplayChanged;
            OnMealDisplayChanged();
        }

        private void OnDisable()
        {
            if (gameManager == null) return;
            gameManager.FoodServed -= OnFoodServed;
            gameManager.MealWaveDisplayChanged -= OnMealDisplayChanged;
        }

        private void OnFoodServed(FoodIsekaiZPlayerState player, ArenaSlot2D slot)
        {
            dishesServedThisMeal++;
            Refresh();
        }

        private void OnMealDisplayChanged()
        {
            int meal = gameManager.UsesMealWaves ? gameManager.CurrentWaveNumber : 1;
            if (meal != trackedMeal)
            {
                trackedMeal = meal;
                dishesServedThisMeal = 0;
                HideAllInstantly();
                UseMealClips();
                return;
            }

            // Minigames, breaks and results switch behind a cover, so the empty tavern returns at once.
            if (!IsServiceRunning()) HideAllInstantly();
        }

        // Diners belong to service only: the meal itself and its clearing while customers finish eating.
        private bool IsServiceRunning()
        {
            if (!gameManager.UsesMealWaves) return true;
            MealWavePhase phase = gameManager.CurrentMealWavePhase;
            return phase == MealWavePhase.Active || phase == MealWavePhase.Clearing;
        }

        private void HideAllInstantly()
        {
            foreach (DinerTable table in tables)
            {
                if (table.Video != null) table.Video.HideInstantly();
            }
        }

        // Each table loads the current meal's clip while it is hidden behind the meal transition.
        private void UseMealClips()
        {
            foreach (DinerTable table in tables)
            {
                if (table.Video != null) table.Video.UseClip(table.ClipFor(trackedMeal));
            }
        }

        private void Refresh()
        {
            bool serviceRunning = IsServiceRunning();
            foreach (DinerTable table in tables)
            {
                if (table.Video == null) continue;
                bool show = serviceRunning && table.ClipFor(trackedMeal) != null
                    && dishesServedThisMeal >= table.DishesToAppear;
                if (show) table.Video.Show();
                else table.Video.Hide();
            }
        }
    }
}
