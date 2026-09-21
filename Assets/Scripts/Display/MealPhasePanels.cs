using System;
using System.Collections;
using System.Collections.Generic;
using FoodIsekaiZ.Gameplay;
using FoodIsekaiZ.Players;
using TMPro;
using UnityEngine;

namespace FoodIsekaiZ.Display
{
    // Updates live text and visibility on the authored intermission and final report panels.
    public sealed class MealPhasePanels : MonoBehaviour
    {
        [SerializeField] private FoodIsekaiZGameManager gameManager;
        [SerializeField] private GameObject breakPanel;
        [SerializeField] private GameObject resultsPanel;
        [SerializeField] private UWBPlayerSpawner playerSpawner;
        [SerializeField] private TopHudVisibility topHudVisibility;
        [SerializeField] private MealMenuTransition menuTransition;
        [SerializeField] private TMP_Text breakScoreText;
        [SerializeField] private TMP_Text breakMvpText;
        [SerializeField] private TMP_Text breakCountdownText;
        [SerializeField] private TMP_Text breakNextMealText;
        private TMP_Text resultScoreText;
        private readonly TMP_Text[] resultNames = new TMP_Text[4];
        private readonly TMP_Text[] resultScores = new TMP_Text[4];
        private readonly GameObject[] resultRows = new GameObject[4];
        private readonly List<int> rankedPlayerIds = new List<int>();

        private readonly Dictionary<GameObject, bool> previousVisibility = new Dictionary<GameObject, bool>();
        private GameObject exclusivePanel;
        private GameObject requestedPanel;
        private Coroutine transition;
        private Coroutine pendingCover;
        private Action coveredPhaseChange;
        private MealWavePhase previousPhase = MealWavePhase.NotStarted;
        private int previousWave = -1;

        private void Awake()
        {
            // Resolve an already-authored Result instance, including scenes opened before references were wired.
            Transform result = transform.Find("Result");
            if (result != null) resultsPanel = result.gameObject;
            if (playerSpawner == null) playerSpawner = FindAnyObjectByType<UWBPlayerSpawner>();
            breakPanel?.SetActive(false);
            resultsPanel?.SetActive(false);
            if (resultsPanel == null) return;

            Transform resultContent = resultsPanel.transform.Find("Content") ?? resultsPanel.transform;
            resultScoreText = resultContent.Find("TotalScore/Text_Score")?.GetComponent<TMP_Text>();
            for (int i = 0; i < resultRows.Length; i++)
            {
                Transform row = resultContent.Find($"Player{i + 1}");
                if (row == null) continue;
                resultRows[i] = row.gameObject;
                resultNames[i] = row.Find($"Playr{i + 1}")?.GetComponent<TMP_Text>();
                resultScores[i] = row.Find($"Playr{i + 1}_Score")?.GetComponent<TMP_Text>();
            }
        }

        private void LateUpdate()
        {
            if (exclusivePanel != null) HideOtherUi(exclusivePanel);
        }

        private void HideOtherUi(GameObject panel)
        {
            Canvas canvas = panel.GetComponentInParent<Canvas>();
            if (canvas == null) return;
            Transform background = canvas.transform.Find("Background");

            // Keep the panel's ancestor chain running, including this event listener.
            Transform branch = panel.transform;
            while (branch != canvas.transform && branch.parent != null)
            {
                Transform parent = branch.parent;
                for (int i = 0; i < parent.childCount; i++)
                {
                    GameObject sibling = parent.GetChild(i).gameObject;
                    if (sibling == branch.gameObject) continue;
                    // Preserve the wall background's authored visibility during either report.
                    if (sibling.transform == background) continue;
                    if (topHudVisibility != null && topHudVisibility.Owns(sibling)) continue;
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
                if (menuTransition != null) gameManager.MealTransitionRequested += CoverBeforePhaseChange;
                gameManager.TeamScoreChanged += ScoreChanged;
                gameManager.PlayerScoreChanged += PlayerChanged;
                gameManager.PlayerMoneyDeposited += PlayerChanged;
            }
            Refresh();
        }

        private void OnDisable()
        {
            if (pendingCover != null) StopCoroutine(pendingCover);
            pendingCover = null;
            if (transition != null) StopCoroutine(transition);
            transition = null;
            menuTransition?.Hide();
            gameManager?.SetPhasePresentationPaused(false);
            previousPhase = MealWavePhase.NotStarted;
            previousWave = -1;
            requestedPanel = null;
            exclusivePanel = null;
            topHudVisibility?.SetReportVisible(false);
            RestoreOtherUi();
            if (gameManager != null)
            {
                gameManager.MealWaveDisplayChanged -= Refresh;
                gameManager.MealTransitionRequested -= CoverBeforePhaseChange;
                gameManager.TeamScoreChanged -= ScoreChanged;
                gameManager.PlayerScoreChanged -= PlayerChanged;
                gameManager.PlayerMoneyDeposited -= PlayerChanged;
            }
            breakPanel?.SetActive(false);
            resultsPanel?.SetActive(false);
            Action finishPendingChange = coveredPhaseChange;
            coveredPhaseChange = null;
            finishPendingChange?.Invoke();
        }

        private void CoverBeforePhaseChange(string heading, Action changePhase)
        {
            if (transition != null) StopCoroutine(transition);
            transition = null;
            coveredPhaseChange = changePhase;
            pendingCover = StartCoroutine(CoverPendingPhase(heading));
        }

        private IEnumerator CoverPendingPhase(string heading)
        {
            if (menuTransition != null) yield return menuTransition.Cover(heading);
            Action changePhase = coveredPhaseChange;
            coveredPhaseChange = null;
            pendingCover = null;
            changePhase?.Invoke();
        }

        private void ScoreChanged(int value) => Refresh();
        private void PlayerChanged(int playerId, int value) => Refresh();

        public void Refresh()
        {
            bool enabledWaves = gameManager != null && gameManager.UsesMealWaves;
            bool intermission = enabledWaves && gameManager.CurrentMealWavePhase == MealWavePhase.Intermission;
            bool complete = enabledWaves && gameManager.CurrentMealWavePhase == MealWavePhase.Completed;
            bool active = enabledWaves && gameManager.CurrentMealWavePhase == MealWavePhase.Active;
            bool phaseChanged = enabledWaves && (previousPhase != gameManager.CurrentMealWavePhase ||
                previousWave != gameManager.CurrentWaveNumber);
            bool showMenu = menuTransition != null && phaseChanged && (active || intermission || complete);
            if (enabledWaves)
            {
                previousPhase = gameManager.CurrentMealWavePhase;
                previousWave = gameManager.CurrentWaveNumber;
            }
            GameObject nextExclusivePanel = complete ? resultsPanel : intermission ? breakPanel : null;
            if (intermission)
            {
                if (breakScoreText != null) breakScoreText.text = gameManager.TeamScore.ToString();
                if (breakMvpText != null)
                    breakMvpText.text = gameManager.TryGetMvp(out int mvpPlayerId, out _)
                        ? $"MVP : Player{mvpPlayerId}" : "MVP : --";
                if (breakCountdownText != null)
                    breakCountdownText.text = Mathf.Max(0, Mathf.CeilToInt(gameManager.MealPhaseRemainingSeconds)).ToString("00");
                if (breakNextMealText != null) breakNextMealText.text = $"NEXT {gameManager.NextWaveName}";
            }
            if (exclusivePanel != null) HideOtherUi(exclusivePanel);
            if (complete) RefreshResults();
            if (requestedPanel == nextExclusivePanel && !showMenu) return;
            requestedPanel = nextExclusivePanel;
            if (transition != null) StopCoroutine(transition);
            string heading = complete ? "SERVICE RESULTS" : intermission ? "SERVICE BREAK" :
                gameManager != null ? gameManager.CurrentWaveName : string.Empty;
            transition = StartCoroutine(TransitionTo(nextExclusivePanel, showMenu, heading));
        }

        private IEnumerator TransitionTo(GameObject nextPanel, bool showMenu, string heading)
        {
            gameManager?.SetPhasePresentationPaused(showMenu);
            if (showMenu) yield return menuTransition.Cover(heading);
            if (exclusivePanel != null && exclusivePanel != nextPanel)
            {
                yield return FadePanel(exclusivePanel, false);
                RestoreOtherUi();
                exclusivePanel = null;
            }
            topHudVisibility?.SetReportVisible(nextPanel != null);
            if (nextPanel != null)
            {
                exclusivePanel = nextPanel;
                HideOtherUi(nextPanel);
                yield return FadePanel(nextPanel, true);
            }
            if (showMenu) yield return menuTransition.Reveal();
            else menuTransition?.Hide();
            gameManager?.SetPhasePresentationPaused(false);
            transition = null;
        }

        private static IEnumerator FadePanel(GameObject panel, bool visible)
        {
            ReportPanelTransition animation = panel.GetComponent<ReportPanelTransition>();
            if (animation != null) yield return animation.Fade(visible);
            else panel.SetActive(visible);
        }

        private void RefreshResults()
        {
            if (resultScoreText != null) resultScoreText.text = gameManager.TeamScore.ToString();
            rankedPlayerIds.Clear();
            if (playerSpawner != null)
            {
                // Offline players and players with zero points still belong in the final standings.
                foreach (UWBPlayerController player in playerSpawner.SpawnedPlayers)
                {
                    if (player != null && !rankedPlayerIds.Contains(player.PlayerId))
                        rankedPlayerIds.Add(player.PlayerId);
                }
            }
            rankedPlayerIds.Sort(ComparePlayerScores);
            for (int i = 0; i < resultRows.Length; i++)
            {
                bool hasPlayer = i < rankedPlayerIds.Count;
                if (resultRows[i] != null) resultRows[i].SetActive(hasPlayer);
                if (!hasPlayer) continue;
                int playerId = rankedPlayerIds[i];
                if (resultNames[i] != null) resultNames[i].text = $"Player{playerId}";
                if (resultScores[i] != null)
                    resultScores[i].text = gameManager.GetPlayerScore(playerId).ToString();
            }
        }

        private int ComparePlayerScores(int firstPlayerId, int secondPlayerId)
        {
            int scoreOrder = gameManager.GetPlayerScore(secondPlayerId).CompareTo(gameManager.GetPlayerScore(firstPlayerId));
            return scoreOrder != 0 ? scoreOrder : firstPlayerId.CompareTo(secondPlayerId);
        }
    }
}
