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
        [SerializeField] private GameObject[] hideDuringBreak;
        [SerializeField, Min(0.01f)] private float breakSlideDuration = 0.45f;
        [SerializeField, Min(0f)] private float breakSlideDistance = 3f;
        [SerializeField] private GameObject resultsPanel;
        [SerializeField] private UWBPlayerSpawner playerSpawner;
        [SerializeField] private TopHudVisibility topHudVisibility;
        [SerializeField] private MealMenuTransition menuTransition;
        private TMP_Text resultScoreText;
        private readonly TMP_Text[] resultNames = new TMP_Text[4];
        private readonly TMP_Text[] resultScores = new TMP_Text[4];
        private readonly GameObject[] resultRows = new GameObject[4];
        private readonly List<int> rankedPlayerIds = new List<int>();

        private readonly Dictionary<GameObject, bool> previousVisibility = new Dictionary<GameObject, bool>();
        private readonly Dictionary<Transform, Vector3> floorSlideOrigins = new Dictionary<Transform, Vector3>();
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
            // Restore each floor object's prior visibility when leaving the break.
            if (panel == breakPanel && hideDuringBreak != null)
            {
                foreach (GameObject target in hideDuringBreak)
                {
                    if (target == null) continue;
                    if (!previousVisibility.ContainsKey(target))
                        previousVisibility.Add(target, target.activeSelf);
                    if (target.activeSelf) target.SetActive(false);
                }
            }

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
            // Restore the authored positions before enabling the floor groups again.
            foreach (KeyValuePair<Transform, Vector3> entry in floorSlideOrigins)
                if (entry.Key != null) entry.Key.position = entry.Value;
            floorSlideOrigins.Clear();
            foreach (KeyValuePair<GameObject, bool> entry in previousVisibility)
            {
                if (entry.Key != null) entry.Key.SetActive(entry.Value);
            }
            previousVisibility.Clear();
        }

        private IEnumerator SlideFloorOut()
        {
            if (hideDuringBreak == null) yield break;
            foreach (GameObject target in hideDuringBreak)
            {
                if (target == null || !target.activeInHierarchy ||
                    (target.name != "CustomerSlots" && target.name != "FoodStationSlots" &&
                     target.name != "BackgroundT1" && target.name != "BackgroundT2")) continue;
                if (!previousVisibility.ContainsKey(target))
                    previousVisibility.Add(target, target.activeSelf);
                if (!floorSlideOrigins.ContainsKey(target.transform))
                    floorSlideOrigins.Add(target.transform, target.transform.position);
            }
            if (floorSlideOrigins.Count == 0) yield break;

            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, breakSlideDuration);
            while (elapsed < duration)
            {
                elapsed = Mathf.Min(duration, elapsed + Time.unscaledDeltaTime);
                float progress = elapsed / duration;
                float eased = progress * progress * (3f - 2f * progress);
                foreach (KeyValuePair<Transform, Vector3> entry in floorSlideOrigins)
                {
                    if (entry.Key == null) continue;
                    float direction = entry.Key.name == "CustomerSlots" || entry.Key.name == "BackgroundT2"
                        ? 1f : -1f;
                    entry.Key.position = entry.Value + Vector3.forward *
                        (direction * Mathf.Max(0f, breakSlideDistance) * eased);
                }
                yield return null;
            }
        }

        private IEnumerator SlideFloorIn()
        {
            if (floorSlideOrigins.Count == 0) yield break;
            var startPositions = new Dictionary<Transform, Vector3>();
            foreach (KeyValuePair<Transform, Vector3> entry in floorSlideOrigins)
            {
                Transform target = entry.Key;
                if (target == null) continue;
                startPositions.Add(target, target.position);
                if (previousVisibility.TryGetValue(target.gameObject, out bool wasVisible))
                    target.gameObject.SetActive(wasVisible);
                // Startup fades wait for the wall page and would make this slide invisible.
                foreach (FloorTableFade fade in target.GetComponentsInChildren<FloorTableFade>())
                    fade.CompleteReveal();
                foreach (CustomerSlotsFade fade in target.GetComponentsInChildren<CustomerSlotsFade>())
                    fade.CompleteReveal();
                foreach (FloorDecorFade fade in target.GetComponentsInChildren<FloorDecorFade>())
                    fade.CompleteReveal();
            }

            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, breakSlideDuration);
            while (elapsed < duration)
            {
                elapsed = Mathf.Min(duration, elapsed + Time.unscaledDeltaTime);
                float progress = elapsed / duration;
                float eased = progress * progress * (3f - 2f * progress);
                foreach (KeyValuePair<Transform, Vector3> entry in startPositions)
                    if (entry.Key != null)
                        entry.Key.position = Vector3.Lerp(entry.Value, floorSlideOrigins[entry.Key], eased);
                yield return null;
            }
        }

        private void OnEnable()
        {
            ResolveBreakFloorObjects();
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

        // Open scenes can still have the old serialized component without the floor references.
        // Resolve only existing Arena children in this scene, including currently hidden objects.
        private void ResolveBreakFloorObjects()
        {
            var targets = new List<GameObject>();
            if (hideDuringBreak != null)
                foreach (GameObject target in hideDuringBreak)
                    if (target != null && !targets.Contains(target)) targets.Add(target);

            string[] names = { "BackgroundT1", "BackgroundT2", "CustomerSlots", "FoodStationSlots" };
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
                {
                    if (candidate.name != "Arena") continue;
                    foreach (string childName in names)
                    {
                        Transform child = candidate.Find(childName);
                        if (child != null && !targets.Contains(child.gameObject))
                            targets.Add(child.gameObject);
                    }
                }
            }
            hideDuringBreak = targets.ToArray();
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
            bool returnFloor = nextPanel == null && gameManager != null &&
                gameManager.CurrentMealWavePhase == MealWavePhase.Active && floorSlideOrigins.Count > 0;
            gameManager?.SetPhasePresentationPaused(showMenu || returnFloor);
            if (showMenu) yield return menuTransition.Cover(heading);
            if (exclusivePanel != nextPanel && previousVisibility.Count > 0)
            {
                if (exclusivePanel != null) yield return FadePanel(exclusivePanel, false);
                exclusivePanel = null;
                // Keep the groups at their offscreen positions until their return animation finishes.
                if (returnFloor) yield return SlideFloorIn();
                RestoreOtherUi();
            }
            topHudVisibility?.SetReportVisible(nextPanel != null);
            if (nextPanel != null)
            {
                if (nextPanel == breakPanel) yield return SlideFloorOut();
                exclusivePanel = nextPanel;
                HideOtherUi(nextPanel);
                yield return FadePanel(nextPanel, true);
            }
            if (showMenu) yield return menuTransition.Reveal(returnFloor);
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
                // Ready selection leaves unselected tags at PlayerId 0; they have no result row.
                // Assigned players remain in the standings even when offline or on zero points.
                foreach (UWBPlayerController player in playerSpawner.SpawnedPlayers)
                {
                    if (player != null && player.PlayerId > 0 && !rankedPlayerIds.Contains(player.PlayerId))
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
