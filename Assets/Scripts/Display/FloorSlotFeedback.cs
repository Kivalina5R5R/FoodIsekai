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
        private float nextWalletReminderTime;

        private void OnEnable()
        {
            if (gameManager == null || slot == null)
            {
                return;
            }

            gameManager.FoodPickedUp += HandlePickup;
            gameManager.FoodServed += HandleDelivery;
            if (foodMotion != null) foodMotion.FoodArrived += HandleFoodArrived;
            gameManager.WrongFoodDiscarded += HandleWrongFood;
            gameManager.PlayerMoneyCollectionBlocked += HandleWalletFull;
        }

        private void OnDisable()
        {
            if (gameManager != null)
            {
                gameManager.FoodPickedUp -= HandlePickup;
                gameManager.FoodServed -= HandleDelivery;
                gameManager.WrongFoodDiscarded -= HandleWrongFood;
                gameManager.PlayerMoneyCollectionBlocked -= HandleWalletFull;
            }

            if (burst != null)
            {
                burst.Stop();
            }
            nextWalletReminderTime = 0f;

            if (foodMotion != null)
            {
                foodMotion.FoodArrived -= HandleFoodArrived;
                foodMotion.Stop();
            }
        }

        private void HandleWalletFull(FoodIsekaiZPlayerState player, ArenaSlot2D interactedSlot)
        {
            if (interactedSlot != slot || player == null)
            {
                return;
            }

            if (burst == null || !burst.isActiveAndEnabled || Time.time < nextWalletReminderTime)
            {
                return;
            }

            nextWalletReminderTime = Time.time + 2f;
            burst.PlayWalletFull();
        }

        private void HandleWrongFood(FoodIsekaiZPlayerState player, ArenaSlot2D interactedSlot)
        {
            if (interactedSlot != slot || burst == null || !burst.isActiveAndEnabled)
            {
                return;
            }

            burst.PlayRejection();
        }

        private void HandlePickup(FoodIsekaiZPlayerState player, ArenaSlot2D interactedSlot)
        {
            if (interactedSlot != slot || player == null) return;
            foodMotion?.PlayFood(slot.StationFood, player.transform, false);
        }

        private void HandleDelivery(FoodIsekaiZPlayerState player, ArenaSlot2D interactedSlot)
        {
            if (interactedSlot != slot || player == null) return;
            if (foodMotion != null && foodMotion.isActiveAndEnabled)
                foodMotion.PlayFood(slot.RequestedFood, player.transform, true);
            else
                HandleFoodArrived();
        }

        private void HandleFoodArrived()
        {
            // Only delivery celebrates at the destination; pickup travels into the player's plate.
            if (slot != null && slot.SlotType == ArenaSlotType.Customer && burst != null && burst.isActiveAndEnabled)
                burst.Play();
        }
    }
}
