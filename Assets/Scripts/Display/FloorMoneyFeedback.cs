using FoodIsekaiZ.Gameplay;
using System.Globalization;
using TMPro;
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
        [SerializeField] private TMP_Text bankBalanceText;
        [SerializeField] private GameObject bankBalanceVisual;
        [SerializeField] private MealFoodSwapEffect bankTransition;

        private void OnEnable()
        {
            if (gameManager == null) return;
            gameManager.CustomerMoneySpawned += HandlePayout;
            gameManager.PlayerMoneyCollected += HandleCollection;
            gameManager.PlayerMoneyDelivered += HandleDeposit;
            gameManager.BankedMoneyChanged += HandleBalanceChanged;
            HandleBalanceChanged(gameManager.TotalBankedMoney);
        }

        private void OnDisable()
        {
            if (gameManager != null)
            {
                gameManager.CustomerMoneySpawned -= HandlePayout;
                gameManager.PlayerMoneyCollected -= HandleCollection;
                gameManager.PlayerMoneyDelivered -= HandleDeposit;
                gameManager.BankedMoneyChanged -= HandleBalanceChanged;
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

        private void HandleDeposit(FoodIsekaiZPlayerState player, ArenaSlot2D bank, int amount)
        {
            if (bank != slot || player == null || bankPurse == null || amount <= 0) return;
            bankPurse.SetBalance(gameManager.TotalBankedMoney);
            bankPurse.Pulse();
            coinBurst?.PlayTransfer(player.transform.position, bankPurse.transform.position);
        }

        private void HandleBalanceChanged(int balance)
        {
            bankPurse?.SetBalance(balance);
            if (bankBalanceText != null)
                bankBalanceText.text = balance.ToString("N0", CultureInfo.InvariantCulture);
        }

        private void LateUpdate()
        {
            if (bankBalanceVisual == null) return;
            bool visible = bankTransition == null || bankTransition.IsIconVisible;
            if (bankBalanceVisual.activeSelf != visible) bankBalanceVisual.SetActive(visible);
        }
    }
}
