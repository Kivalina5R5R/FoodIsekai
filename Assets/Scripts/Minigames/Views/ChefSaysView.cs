using FoodIsekaiZ.Display;
using FoodIsekaiZ.Localization;
using UnityEngine;
using UnityEngine.Serialization;

namespace FoodIsekaiZ.Minigames.Views
{
    // Chef Says on the wall and floor (prefab Assets/Prefab/Minigame/ChefSays).
    // Wall: each call first floats the called direction up on that side, then the wolfgirl chef pops up
    // there from behind that side's countdown gauge, and ducks away between calls.
    // The gauges, words and both portraits keep their authored prefab layout.
    // Floor: one ChefSaysFloorMark per player, cloned from the template: its moving chevrons during a
    // call with a faint ingredient at their end, then that player's own right or wrong mark after it.
    // Wall bottom middle: the basket every collected ingredient flies into (ChefSaysBasket).
    public sealed class ChefSaysView : MinigameView
    {
        [SerializeField] private ChefSaysPopup leftChef;
        [SerializeField] private ChefSaysPopup rightChef;
        [SerializeField, FormerlySerializedAs("callGauge")] private ReadyConfirmationGauge leftGauge;
        [SerializeField] private ReadyConfirmationGauge rightGauge;
        [SerializeField] private ChefSaysCallWord leftWord;
        [SerializeField] private ChefSaysCallWord rightWord;
        // How long the called word floats on its own before the chef pops out under it.
        [SerializeField, Min(0f)] private float chefDelay = 0.25f;
        [SerializeField] private ChefSaysFloorMark markTemplate;
        // Fantasy ingredients waiting at the end of the arrows, and the basket on the wall they go into.
        [SerializeField] private Sprite[] ingredients = new Sprite[0];
        [SerializeField] private ChefSaysBasket basket;

        public int IngredientCount => ingredients.Length;

        private ReadyConfirmationGauge activeGauge;

        // Seconds from a call appearing until the chef has popped out and looked both ways; the
        // game times each call to this so the gauge runs out as she finishes.
        public float CallSeconds => chefDelay + Mathf.Max(leftChef.AppearanceSeconds, rightChef.AppearanceSeconds);

        protected override void OnBound()
        {
            leftChef.HideImmediately();
            rightChef.HideImmediately();
            ShowGauge(null);
            if (leftWord != null) leftWord.HideImmediately();
            if (rightWord != null) rightWord.HideImmediately();
        }

        // Only the called side enters, including when the same side is called twice.
        public void ShowCall(bool right)
        {
            (right ? leftChef : rightChef).HideImmediately();
            ChefSaysCallWord word = right ? rightWord : leftWord;
            if (word != null) word.Play(right ? GameLanguage.Pick("RIGHT!", "ขวา!") : GameLanguage.Pick("LEFT!", "ซ้าย!"));
            (right ? rightChef : leftChef).Show(chefDelay);
            ShowGauge(right ? rightGauge : leftGauge);
            SetBar(1f);
        }

        // The visible chef leaves while the players return to the middle.
        public void Quiet()
        {
            leftChef.Hide();
            rightChef.Hide();
            ShowGauge(null);
        }

        // share runs 1 -> 0 as the call runs out.
        public void SetBar(float share)
        {
            if (activeGauge != null) activeGauge.ShowProgress(share, false, false);
        }

        // Shows only the given side's gauge; null hides both.
        private void ShowGauge(ReadyConfirmationGauge gauge)
        {
            activeGauge = gauge;
            if (leftGauge != null) leftGauge.gameObject.SetActive(gauge == leftGauge);
            if (rightGauge != null) rightGauge.gameObject.SetActive(gauge == rightGauge);
        }

        // A floor mark for one player, with chevrons in that player's colour made for up to
        // maxReach metres.
        public ChefSaysFloorMark AddMark(float maxReach, Color playerColour)
        {
            ChefSaysFloorMark mark = Spawn(markTemplate);
            mark.Setup(maxReach * MinigameStage.UnitsPerMetre, playerColour);
            return mark;
        }

        // Moves mark to arena point here and points its chevrons the called way for reach metres.
        // Returns, in metres, how far out the last shown chevron reaches (at least one always shows).
        public float PointMark(ChefSaysFloorMark mark, Vector2 here, bool right, float reach)
        {
            PlaceOnFloor((RectTransform)mark.transform, here);
            return mark.Point(right, reach * MinigameStage.UnitsPerMetre) / MinigameStage.UnitsPerMetre;
        }

        // Shows ingredient number index faintly centre metres out on the called side of mark.
        public void ShowIngredient(ChefSaysFloorMark mark, int index, bool right, float centre)
        {
            mark.ShowIngredient(Ingredient(index), right, centre * MinigameStage.UnitsPerMetre);
        }

        public void CollectIngredient(ChefSaysFloorMark mark)
        {
            mark.CollectIngredient();
        }

        // Ingredient number index flies into the wall basket from the bottom edge on the given side.
        public void SendToBasket(int index, bool fromRight)
        {
            if (basket != null) basket.Add(Ingredient(index), fromRight);
        }

        private Sprite Ingredient(int index)
        {
            return index >= 0 && index < ingredients.Length ? ingredients[index] : null;
        }

        // Moves mark to where the player stands now (arena point here) and shows their result there.
        public void ShowResult(ChefSaysFloorMark mark, Vector2 here, bool correct)
        {
            PlaceOnFloor((RectTransform)mark.transform, here);
            mark.ShowResult(correct);
        }
    }
}
