using FoodIsekaiZ.Gameplay;
using UnityEngine;

namespace FoodIsekaiZ.Display
{
    // Plays the scene-authored floor burst when this slot accepts a pickup or delivery.
    public sealed class FloorSlotFeedback : MonoBehaviour
    {
        [SerializeField] private FoodIsekaiZGameManager gameManager;
        [SerializeField] private ArenaSlot2D slot;
        [SerializeField] private CustomerPanelSuccessParticles burst;
        [SerializeField] private FloorAnimatedSprite foodMotion;

        private void OnEnable()
        {
            if (gameManager == null || slot == null || burst == null)
            {
                return;
            }

            gameManager.FoodPickedUp += HandleInteraction;
            gameManager.FoodServed += HandleInteraction;
        }

        private void OnDisable()
        {
            if (gameManager != null)
            {
                gameManager.FoodPickedUp -= HandleInteraction;
                gameManager.FoodServed -= HandleInteraction;
            }

            if (burst != null)
            {
                burst.Stop();
            }

            if (foodMotion != null)
            {
                foodMotion.Stop();
            }
        }

        private void HandleInteraction(FoodIsekaiZPlayerState player, ArenaSlot2D interactedSlot)
        {
            if (interactedSlot == slot && burst != null && burst.isActiveAndEnabled)
            {
                burst.Play();
                if (foodMotion != null && player != null)
                {
                    FoodType food = slot.SlotType == ArenaSlotType.FoodStation ? slot.StationFood : slot.RequestedFood;
                    foodMotion.PlayFood(food, player.transform.position);
                }
            }
        }
    }
}
