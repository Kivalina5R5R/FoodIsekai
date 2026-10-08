using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using FoodIsekaiZ.Gameplay;
using FoodIsekaiZ.Players;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FoodIsekaiZ.Display.Editor
{
    // Exercises the real result prefab and score binding without saving or changing the open scene.
    [InitializeOnLoad]
    public static class ResultScoreVerification
    {
        private const string RequestPath = "Temp/ResultScoreVerification.request";
        private const string ReportPath = "Logs/ResultScoreVerification.txt";
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        static ResultScoreVerification() => EditorApplication.update += Poll;

        private static void Poll()
        {
            if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(RequestPath);
            Verify();
        }

        [MenuItem("Food Isekai/Verify Result Scores")]
        public static void Verify()
        {
            var report = new List<string>();
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                foreach (MealPhasePanels live in Resources.FindObjectsOfTypeAll<MealPhasePanels>())
                {
                    if (!live.gameObject.scene.IsValid() || live.gameObject.scene == preview) continue;
                    report.Add($"Loaded scene: {live.gameObject.scene.path}; dirty={live.gameObject.scene.isDirty}");
                    Transform result = live.transform.Find("Result");
                    if (result == null) continue;
                    foreach (TMP_Text label in result.GetComponentsInChildren<TMP_Text>(true))
                        report.Add($"Loaded label {label.name}: {label.text}");
                }

                var host = new GameObject("Result score verification");
                SceneManager.MoveGameObjectToScene(host, preview);
                host.SetActive(false);
                var game = host.AddComponent<FoodIsekaiZGameManager>();
                var spawner = host.AddComponent<UWBPlayerSpawner>();
                var panels = host.AddComponent<MealPhasePanels>();
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/UI/Result.prefab");
                GameObject instance = UnityEngine.Object.Instantiate(prefab, host.transform);
                instance.name = "Result";
                Set(panels, "gameManager", game);
                Set(panels, "playerSpawner", spawner);
                Invoke(panels, "Awake");
                var players = (List<UWBPlayerController>)Get(spawner, "spawnedPlayers");
                int[] scores = { 35, 420, 0, 200, 75, 200 };
                int[] rankedIds = { 2, 4, 6, 5, 1, 3 };
                for (int i = 0; i < scores.Length; i++)
                {
                    var player = new GameObject($"Result test player {i + 1}");
                    player.transform.SetParent(host.transform);
                    var controller = player.AddComponent<UWBPlayerController>();
                    Set(controller, "playerId", i + 1);
                    players.Add(controller);
                    Invoke(game, "AddPlayerAndTeamScore", i + 1, 200);
                }
                Invoke(panels, "RefreshResults");
                for (int i = 0; i < scores.Length; i++)
                    Invoke(game, "AddPlayerAndTeamScore", i + 1, scores[i] - 200);
                Invoke(game, "ApplyEscapedCustomerPenalty");
                Invoke(game, "AddTeamOnlyScore", 10);
                Set(game, "totalBankedMoney", 125);
                Invoke(game, "ConvertRemainingMoneyToScore");
                Check(game.PlayerScoreTotal == 930 && game.TeamPenalty == 5 && game.TeamOnlyScore == 10 &&
                    game.FinalScoreBeforeBonus == 935 && game.FinalLeftoverBonus == 60 && game.TeamScore == 995,
                    "Player earnings + team-only earnings - one team penalty + coins reconcile exactly.", report);
                Invoke(game, "ConvertRemainingMoneyToScore");
                Check(game.TeamScore == 995 && game.FinalLeftoverBonus == 60,
                    "Repeated final settlement does not add or erase the coin bonus.", report);
                Invoke(panels, "RefreshResults");
                Transform content = instance.transform.Find("Content");
                var reveal = content.GetComponent<ResultRevealSequence>();
                Set(reveal, "elapsed", 20f);
                Invoke(reveal, "Apply");
                for (int i = 0; i < rankedIds.Length; i++)
                {
                    Transform row = content.Find($"Player{i + 1}");
                    string name = row.Find($"Playr{i + 1}").GetComponent<TMP_Text>().text;
                    string score = row.Find($"Playr{i + 1}_Score").GetComponent<TMP_Text>().text;
                    Check(name == $"Player{rankedIds[i]}" && score == scores[rankedIds[i] - 1].ToString(),
                        $"Row {i + 1}: {name} = {score}, expected Player{rankedIds[i]} = {scores[rankedIds[i] - 1]}", report);
                }
                Check(content.Find("TotalScore/Text_Score").GetComponent<TMP_Text>().text == game.TeamScore.ToString(),
                    "Result total matches the game manager including leftover bonus.", report);
                Check(content.Find("TotalScore/Text_Bonus").GetComponent<TMP_Text>().text ==
                    "PLAYERS 930   +10 TEAM   -5 PENALTY   +60 COINS",
                    "Compact result breakdown shows the player sum, shared earnings, penalty and coin bonus.", report);

                // A later score event can reorder rows after the first snapshot was captured.
                Invoke(game, "AddPlayerAndTeamScore", 3, 700);
                Invoke(panels, "RefreshResults");
                Invoke(reveal, "Apply");
                Transform leader = content.Find("Player1");
                Check(leader.Find("Playr1").GetComponent<TMP_Text>().text == "Player3" &&
                    leader.Find("Playr1_Score").GetComponent<TMP_Text>().text == "700",
                    "Refreshing completed results keeps the new leader and score together.", report);
                Check(content.Find("TotalScore/Text_Score").GetComponent<TMP_Text>().text == game.TeamScore.ToString(),
                    "Refreshing completed results updates the team total.", report);
                Check((float)Get(reveal, "elapsed") == 20f,
                    "A score refresh does not restart the reveal animation.", report);

                reveal.enabled = false;
                Invoke(game, "AddPlayerAndTeamScore", 3, 25);
                Invoke(panels, "RefreshResults");
                Check(leader.Find("Playr1_Score").GetComponent<TMP_Text>().text == "725" &&
                    content.Find("TotalScore/Text_Score").GetComponent<TMP_Text>().text == game.TeamScore.ToString(),
                    "A disabled reveal still displays current player and team scores.", report);
                Check(content.Find("TotalScore/Text_Bonus").GetComponent<TMP_Text>().text ==
                    "PLAYERS 1,655   +10 TEAM   -5 PENALTY   +60 COINS",
                    "Disabled animation uses the same updated breakdown.", report);

                var penaltyGame = host.AddComponent<FoodIsekaiZGameManager>();
                Invoke(penaltyGame, "ApplyEscapedCustomerPenalty");
                Invoke(penaltyGame, "ApplyEscapedCustomerPenalty");
                Check(penaltyGame.TeamScore == 0 && penaltyGame.TeamPenalty == 10,
                    "Penalties at zero remain recorded while the live display stays nonnegative.", report);
                Invoke(penaltyGame, "AddPlayerAndTeamScore", 1, 5);
                Set(penaltyGame, "totalBankedMoney", 20);
                Invoke(penaltyGame, "ConvertRemainingMoneyToScore");
                Check(penaltyGame.GetPlayerScore(1) == 5 && penaltyGame.FinalScoreBeforeBonus == -5 &&
                    penaltyGame.FinalLeftoverBonus == 10 && penaltyGame.TeamScore == 5,
                    "Early penalties reduce the final coin bonus without changing personal earnings.", report);

                var emptyGame = host.AddComponent<FoodIsekaiZGameManager>();
                Invoke(emptyGame, "ApplyEscapedCustomerPenalty");
                Invoke(emptyGame, "ConvertRemainingMoneyToScore");
                Check(emptyGame.FinalScoreBeforeBonus == -5 && emptyGame.TeamScore == 0,
                    "Final totals below zero display zero even with no coins.", report);

                TMP_Text breakdown = content.Find("TotalScore/Text_Bonus").GetComponent<TMP_Text>();
                foreach (string sample in new[] {
                    ResultScoreText.Format(0, 0, 0),
                    ResultScoreText.Format(1000, 25, 60),
                    ResultScoreText.Format(99999, 9999, 9999, 9999) })
                {
                    breakdown.text = sample;
                    breakdown.ForceMeshUpdate(true, true);
                    // Preferred height reserves line spacing; visible glyph bounds determine overlap here.
                    Bounds ink = breakdown.textBounds;
                    Rect area = breakdown.rectTransform.rect;
                    Check(ink.min.x >= area.xMin - 0.1f && ink.max.x <= area.xMax + 0.1f &&
                        ink.min.y >= area.yMin - 0.1f && ink.max.y <= area.yMax + 0.1f &&
                        breakdown.textInfo.lineCount <= 1,
                        $"Breakdown fits its authored single-line rectangle: {sample}", report);
                }
            }
            catch (Exception error)
            {
                report.Add("FAIL: " + error);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
                File.WriteAllLines(ReportPath, report);
            }
        }

        private static object Get(object target, string field) =>
            target.GetType().GetField(field, PrivateInstance).GetValue(target);

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, PrivateInstance).SetValue(target, value);

        private static void Invoke(object target, string method, params object[] values) =>
            target.GetType().GetMethod(method, PrivateInstance).Invoke(target, values);

        private static void Check(bool passed, string message, List<string> report)
        {
            if (!passed) throw new InvalidOperationException(message);
            report.Add("PASS: " + message);
        }
    }
}
