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
            if (gameManager == null || slot == null || burst == null)
            {
                return;
            }

            gameManager.FoodPickedUp += HandleInteraction;
            gameManager.FoodServed += HandleInteraction;
            gameManager.WrongFoodDiscarded += HandleWrongFood;
            gameManager.PlayerMoneyCollectionBlocked += HandleWalletFull;
        }

        private void OnDisable()
        {
            if (gameManager != null)
            {
                gameManager.FoodPickedUp -= HandleInteraction;
                gameManager.FoodServed -= HandleInteraction;
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
