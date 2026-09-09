using FoodIsekaiZ.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace FoodIsekaiZ.Display
{
    // Updates live text and visibility on the authored intermission and final report panels.
    public sealed class MealPhasePanels : MonoBehaviour
    {
        [SerializeField] private FoodIsekaiZGameManager gameManager;
        [SerializeField] private GameObject breakPanel;
        [SerializeField] private GameObject resultsPanel;
        [SerializeField] private Text nextMealText;
        [SerializeField] private Text countdownText;
        [SerializeField] private Text resultScoreText;
        [SerializeField] private Text resultMvpText;
        [SerializeField] private Text servedText;
        [SerializeField] private Text missedText;
        [SerializeField] private Text bankedText;
        [SerializeField] private Text mealsText;

        private void OnEnable()
        {
            if (gameManager != null)
            {
                gameManager.MealWaveDisplayChanged += Refresh;
                gameManager.TeamScoreChanged += ScoreChanged;
                gameManager.PlayerScoreChanged += PlayerChanged;
                gameManager.PlayerMoneyDeposited += PlayerChanged;
            }
            Refresh();
        }

        private void OnDisable()
        {
            if (gameManager != null)
            {
                gameManager.MealWaveDisplayChanged -= Refresh;
                gameManager.TeamScoreChanged -= ScoreChanged;
                gameManager.PlayerScoreChanged -= PlayerChanged;
                gameManager.PlayerMoneyDeposited -= PlayerChanged;
            }
            breakPanel?.SetActive(false);
            resultsPanel?.SetActive(false);
        }

        private void ScoreChanged(int value) => Refresh();
        private void PlayerChanged(int playerId, int value) => Refresh();

        public void Refresh()
        {
            bool enabledWaves = gameManager != null && gameManager.UsesMealWaves;
            bool intermission = enabledWaves && gameManager.CurrentMealWavePhase == MealWavePhase.Intermission;
            bool complete = enabledWaves && gameManager.CurrentMealWavePhase == MealWavePhase.Completed;
            if (breakPanel != null && breakPanel.activeSelf != intermission) breakPanel.SetActive(intermission);
            if (resultsPanel != null && resultsPanel.activeSelf != complete) resultsPanel.SetActive(complete);
            if (intermission)
            {
                if (nextMealText != null) nextMealText.text = $"NEXT  {gameManager.NextWaveName}";
                if (countdownText != null) countdownText.text = Mathf.Max(0, Mathf.CeilToInt(gameManager.MealPhaseRemainingSeconds)).ToString("00");
            }
            if (!complete) return;
            if (resultScoreText != null) resultScoreText.text = gameManager.TeamScore.ToString("0000");
            if (resultMvpText != null)
                resultMvpText.text = gameManager.TryGetMvp(out int playerId, out int score) ? $"P{playerId}   {score:0000}" : "--   0000";
            if (servedText != null) servedText.text = gameManager.ServedOrderCount.ToString("00");
            if (missedText != null) missedText.text = gameManager.ExpiredOrderCount.ToString("00");
            if (bankedText != null) bankedText.text = gameManager.TotalBankedMoney.ToString("0000");
            if (mealsText != null) mealsText.text = $"MEALS  {gameManager.CurrentWaveNumber} / {gameManager.TotalWaveCount}";
        }
    }
}
