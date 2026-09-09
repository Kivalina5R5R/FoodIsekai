using FoodIsekaiZ.Gameplay;
using UnityEngine;

namespace FoodIsekaiZ.Display
{
    // Routes successful money transactions to the scene-authored visual effects.
    public sealed class FloorMoneyFeedback : MonoBehaviour
    {
        [SerializeField] private FoodIsekaiZGameManager gameManager;
        [SerializeField] private ArenaSlot2D slot;
        [SerializeField] private FloorCoinBurst coinBurst;
        [SerializeField] private BankPurseImage bankPurse;

        private void OnEnable()
        {
            if (gameManager == null) return;
            gameManager.CustomerMoneySpawned += HandlePayout;
            gameManager.PlayerMoneyCollected += HandleCollection;
            gameManager.PlayerMoneyDeposited += HandleDeposit;
            bankPurse?.SetBalance(gameManager.TotalBankedMoney);
        }

        private void OnDisable()
        {
            if (gameManager != null)
            {
                gameManager.CustomerMoneySpawned -= HandlePayout;
                gameManager.PlayerMoneyCollected -= HandleCollection;
                gameManager.PlayerMoneyDeposited -= HandleDeposit;
            }
            coinBurst?.Stop();
        }

        private void HandlePayout(ArenaSlot2D source, int amount)
        {
            if (source == slot && amount > 0) coinBurst?.Play(false, transform.position);
        }

        private void HandleCollection(FoodIsekaiZPlayerState player, ArenaSlot2D source, int amount)
        {
            if (source == slot && amount > 0) coinBurst?.Play(true, player.transform.position);
        }

        private void HandleDeposit(int playerId, int amount)
        {
            if (bankPurse == null || amount <= 0) return;
            bankPurse.SetBalance(gameManager.TotalBankedMoney);
            bankPurse.Pulse();
            coinBurst?.Play(false, bankPurse.transform.position);
        }
    }
}
