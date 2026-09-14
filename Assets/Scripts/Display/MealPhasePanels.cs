using System.Collections.Generic;
using FoodIsekaiZ.Gameplay;
using TMPro;
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
        [SerializeField] private TMP_Text breakScoreText;
        [SerializeField] private TMP_Text breakMvpText;
        [SerializeField] private TMP_Text breakCountdownText;
        [SerializeField] private TMP_Text breakNextMealText;
        [SerializeField] private Text resultScoreText;
        [SerializeField] private Text resultMvpText;
        [SerializeField] private Text servedText;
        [SerializeField] private Text missedText;
        [SerializeField] private Text bankedText;
        [SerializeField] private Text mealsText;

        private readonly Dictionary<GameObject, bool> previousVisibility = new Dictionary<GameObject, bool>();

        private void LateUpdate()
        {
            if (gameManager != null && gameManager.UsesMealWaves &&
                gameManager.CurrentMealWavePhase == MealWavePhase.Intermission && breakPanel != null)
            {
                HideOtherUi();
            }
        }

        private void HideOtherUi()
        {
            Canvas canvas = breakPanel.GetComponentInParent<Canvas>();
            if (canvas == null) return;
            Transform background = canvas.transform.Find("Background");

            // Keep the panel's ancestor chain running, including this event listener.
            Transform branch = breakPanel.transform;
            while (branch != canvas.transform && branch.parent != null)
            {
                Transform parent = branch.parent;
                for (int i = 0; i < parent.childCount; i++)
                {
                    GameObject sibling = parent.GetChild(i).gameObject;
                    if (sibling == branch.gameObject) continue;
                    // Preserve the wall background's authored visibility during the break.
                    if (sibling.transform == background) continue;
                    if (!previousVisibility.ContainsKey(sibling))
                        previousVisibility.Add(sibling, sibling.activeSelf);
                    if (sibling.activeSelf) sibling.SetActive(false);
                }
                branch = parent;
            }
        }

        private void RestoreOtherUi()
        {
            foreach (KeyValuePair<GameObject, bool> entry in previousVisibility)
            {
                if (entry.Key != null) entry.Key.SetActive(entry.Value);
            }
            previousVisibility.Clear();
        }

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
            RestoreOtherUi();
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
            if (!intermission) RestoreOtherUi();
            if (breakPanel != null && breakPanel.activeSelf != intermission) breakPanel.SetActive(intermission);
            if (resultsPanel != null && resultsPanel.activeSelf != complete) resultsPanel.SetActive(complete);
            if (intermission)
            {
                if (breakScoreText != null) breakScoreText.text = gameManager.TeamScore.ToString("0000");
                if (breakMvpText != null)
                    breakMvpText.text = gameManager.TryGetMvp(out int mvpPlayerId, out _)
                        ? $"MVP : Player{mvpPlayerId}" : "MVP : --";
                if (breakCountdownText != null)
                    breakCountdownText.text = Mathf.Max(0, Mathf.CeilToInt(gameManager.MealPhaseRemainingSeconds)).ToString("00");
                if (breakNextMealText != null) breakNextMealText.text = $"NEXT {gameManager.NextWaveName}";
                if (breakPanel != null) HideOtherUi();
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
