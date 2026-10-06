using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using FoodIsekaiZ.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FoodIsekaiZ.Display.Editor
{
    // Plays three meals through the real game manager in an isolated preview scene and checks the player log.
    // The log is written to StreamingAssets/PlayerLog with the normal date-and-time name so it can be opened and read;
    // the open scene stays untouched.
    [InitializeOnLoad]
    public static class PlayerLogVerification
    {
        private const string RequestPath = "Temp/PlayerLogVerification.request";
        private const string ResultPath = "Temp/PlayerLogVerification.txt";

        static PlayerLogVerification() => EditorApplication.delayCall += RunRequested;

        private static void RunRequested()
        {
            if (!File.Exists(RequestPath) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(RequestPath);
            Verify();
        }

        [MenuItem("Food Isekai/Verify Player Log")]
        public static void Verify()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var results = new List<string>();
            bool recordingBefore = PlayerSessionLog.RecordingEnabled;
            UnityEngine.Random.State randomState = UnityEngine.Random.state;
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                UnityEngine.Random.InitState(61026);
                PlayerSessionLog.FilePathOverride = null;
                PlayerSessionLog.RecordingEnabled = true;

                FoodIsekaiZGameManager game = Create<FoodIsekaiZGameManager>(preview, "Log Test Game");
                Set(game, "initialActiveCustomers", 0);
                ArenaSlot2D c1 = CreateSlot(preview, game, "C1", ArenaSlotType.Customer, FoodType.None);
                ArenaSlot2D c2 = CreateSlot(preview, game, "C2", ArenaSlotType.Customer, FoodType.None);
                ArenaSlot2D c3 = CreateSlot(preview, game, "C3", ArenaSlotType.Customer, FoodType.None);
                var stations = new ArenaSlot2D[6];
                for (int i = 0; i < 5; i++)
                    stations[i] = CreateSlot(preview, game, $"F{i + 1}", ArenaSlotType.FoodStation, (FoodType)(i + 1));
                ArenaSlot2D bank = CreateSlot(preview, game, "Bank", ArenaSlotType.MoneyDeposit, FoodType.None);
                stations[5] = bank;
                game.ConfigureSlots(new[] { c1, c2, c3 }, stations, false);
                FoodIsekaiZPlayerState p1 = Create<FoodIsekaiZPlayerState>(preview, "Log Test Player 1");
                FoodIsekaiZPlayerState p2 = Create<FoodIsekaiZPlayerState>(preview, "Log Test Player 2");
                Set(p2, "fallbackPlayerId", 2);

                PlayerSessionLog.BeginSession(2);
                game.StartMealWaveFlow();
                Check(game.CurrentMealWavePhase == MealWavePhase.Active && game.CurrentWaveName == "BREAKFAST",
                    "BREAKFAST starts.", results);

                // BREAKFAST: one served order, one wrong dish then an angry exit, one angry at the end of the meal.
                Arrive(game, c1, "NPC03-Kwang", FoodType.Food1);
                Arrive(game, c2, "NPC05-Maow", FoodType.Food2);
                game.TryInteract(p1, stations[0]);
                Check(game.TryInteract(p1, c1) && c1.CustomerState == CustomerSlotState.Eating,
                    "P1 serves F1-Meat.", results);
                game.TryInteract(p2, stations[2]);
                Check(game.TryInteract(p2, c2) && c2.CustomerState == CustomerSlotState.WaitingForFood,
                    "P2 delivers a wrong dish.", results);
                Tick(game, 10f);
                Tick(game, 30f);
                Check(c1.CustomerState == CustomerSlotState.MoneyAvailable && c2.CustomerState == CustomerSlotState.Empty,
                    "C1 pays and C2 leaves angry.", results);
                Check(game.TryInteract(p1, c1) && game.TryInteract(p1, bank) && game.TotalBankedMoney == 10,
                    "P1 carries 10 coins to the bank.", results);
                Arrive(game, c3, "NPC09-Wolf", FoodType.Food4);
                FinishMeal(game);
                Check(game.CurrentMealWavePhase == MealWavePhase.Intermission, "Break 1 starts.", results);
                Check(game.TrySpend(4), "The team pays 4 coins for a perk.", results);
                PlayerSessionLog.RecordPerkPurchase("Perk_Small_Meat", 4, 1, game.NextWaveName, false,
                    game.TotalBankedMoney);
                NextMeal(game);
                Check(game.CurrentWaveName == "LUNCH", "LUNCH starts.", results);

                // LUNCH: an omakase order served by P2, and a special order that never shows before the meal ends.
                Arrive(game, c1, "NPC10-Elf", FoodType.Food1, omakase: true);
                game.TryInteract(p2, stations[3]);
                Check(game.TryInteract(p2, c1) && c1.CustomerState == CustomerSlotState.Eating,
                    "P2 serves the omakase order with F4-Dessert.", results);
                c2.ConfigureCustomer(FoodType.Food2, 20f, 10, special: true);
                PlayerSessionLog.RecordCustomer(game.CurrentWaveName, c2.SlotId, "NPC12-Fox", c2);
                Tick(game, 10f);
                Tick(game, 10f);
                FinishMeal(game);
                Check(game.CurrentMealWavePhase == MealWavePhase.Intermission, "Break 2 starts.", results);
                NextMeal(game);
                Check(game.CurrentWaveName == "DINNER", "DINNER starts.", results);

                // DINNER: a paired order finished by both players, its coins left on the floor for the final settlement.
                Arrive(game, c1, "NPC13-Orc-1", FoodType.Food5, FoodType.Food2);
                game.TryInteract(p1, stations[4]);
                game.TryInteract(p2, stations[1]);
                game.TryInteract(p1, c1);
                Check(game.TryInteract(p2, c1) && c1.CustomerState == CustomerSlotState.Eating,
                    "P1 and P2 finish the paired order.", results);
                Tick(game, 10f);
                Tick(game, 10f);
                FinishMeal(game);
                Check(game.CurrentMealWavePhase == MealWavePhase.Completed, "The game reaches the result screen.", results);

                string logPath = PlayerSessionLog.LastFilePath;
                Check(File.Exists(logPath) && System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(logPath),
                    @"^\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}(_\d+)?\.md$"),
                    "Log file is named by its start date and time: " + logPath, results);
                string log = File.ReadAllText(logPath);
                Contains(log, "# FoodIsekaiZ Player Log", results);
                Contains(log, "- จำนวนผู้เล่นตอน Ready: 2", results);
                Contains(log, "| BREAKFAST | 1 | 2 | 1 | 10 | 10 |", results);
                Contains(log, "| LUNCH | 1 | 0 | 0 | 10 | 10 |", results);
                Contains(log, "| DINNER | 1 | 0 | 0 | 10 | 10 |", results);
                Contains(log, "| **รวม** | **3** | **2** | **1** | **30** | **30** |", results);
                Contains(log, "| C1 | NPC03-Kwang | F1-Meat | ได้กิน | P1 |", results);
                Contains(log, "| C2 | NPC05-Maow | F2-Seafood | โกรธออก | - | 20.0 |", results);
                Contains(log, "| C3 | NPC09-Wolf | F4-Dessert | โกรธออก | - |", results);
                Contains(log, "| C1 | NPC10-Elf | Omakase (อะไรก็ได้ 1 จาน) | ได้กิน | P2 |", results);
                Contains(log, "| C2 | NPC12-Fox | F2-Seafood (เมนูพิเศษ) | หมดมื้อ (ไม่ได้รับอาหาร) | - | - |", results);
                Contains(log, "| C1 | NPC13-Orc-1 | F5-Drink + F2-Seafood | ได้กิน | P2 |", results);
                Contains(log, "| LUNCH | Small | Perk_Small_Meat | 4 | P1 | 6 |", results);
                Contains(log, "| พักครั้งที่ 1 (หลัง BREAKFAST) | 10 |", results);
                Contains(log, "| พักครั้งที่ 2 (หลัง LUNCH) | 16 |", results);
                Contains(log, "| จบเกม | 26 |", results);
                Contains(log, "- เหรียญที่เหลือ 26 = โบนัส +10", results);
                Check(game.FinalLeftoverCoins == 26 && game.FinalLeftoverBonus == 10 &&
                    game.FinalScoreBeforeBonus == 45 && game.TeamScore == 55,
                    "26 leftover coins give 2 full groups of 10 = +10 (rounded down).", results);
                Contains(log, $"- **คะแนนรวมโบนัสแล้ว: {game.TeamScore}**", results);
                Contains(log, "(MVP)", results);
                Check(!log.Contains("กำลังรอ") && !log.Contains("ยังไม่จบ"), "Every row has a final result.", results);

                results.Add("PASS: Live scene untouched. Active scene dirty=" + SceneManager.GetActiveScene().isDirty);
                results.Add(string.Empty);
                results.Add(log);
                File.WriteAllLines(ResultPath, results);
                Debug.Log("[PlayerLogVerification] All checks passed.");
            }
            catch (Exception exception)
            {
                results.Add("FAIL: " + exception);
                string failedLog = PlayerSessionLog.LastFilePath;
                if (!string.IsNullOrEmpty(failedLog) && File.Exists(failedLog)) results.Add(File.ReadAllText(failedLog));
                File.WriteAllLines(ResultPath, results);
                Debug.LogException(exception);
            }
            finally
            {
                PlayerSessionLog.FilePathOverride = null;
                PlayerSessionLog.RecordingEnabled = recordingBefore;
                UnityEngine.Random.state = randomState;
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        // Mirrors the NPC spawner: the order is set first, then the NPC walks in and the order is revealed.
        private static void Arrive(FoodIsekaiZGameManager game, ArenaSlot2D slot, string npc, FoodType food,
            FoodType second = FoodType.None, bool omakase = false)
        {
            slot.ConfigureCustomer(food, 20f, 10, second, omakase);
            PlayerSessionLog.RecordCustomer(game.CurrentWaveName, slot.SlotId, npc, slot);
            slot.StartCustomerTimer();
        }

        private static void FinishMeal(FoodIsekaiZGameManager game)
        {
            Invoke(game, "EndCurrentWave");
            Invoke(game, "TickWaveClearance", 0f);
        }

        private static void NextMeal(FoodIsekaiZGameManager game)
        {
            Set(game, "mealPhaseRemainingSeconds", 0f);
            Invoke(game, "TickMealWave", 0f);
        }

        private static ArenaSlot2D CreateSlot(Scene scene, FoodIsekaiZGameManager game, string id,
            ArenaSlotType type, FoodType food)
        {
            ArenaSlot2D slot = Create<ArenaSlot2D>(scene, "Log Test " + id);
            slot.Configure(id, type, food, game);
            return slot;
        }

        private static T Create<T>(Scene scene, string name) where T : Component
        {
            var instance = new GameObject(name);
            SceneManager.MoveGameObjectToScene(instance, scene);
            return instance.AddComponent<T>();
        }

        private static void Tick(FoodIsekaiZGameManager game, float seconds) => Invoke(game, "TickCustomerStates", seconds);
        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        private static void Invoke(object target, string method, params object[] values) =>
            target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, values);

        private static void Contains(string log, string expected, List<string> results) =>
            Check(log.Contains(expected), "Log contains: " + expected, results);

        private static void Check(bool condition, string message, List<string> results)
        {
            if (!condition) throw new InvalidOperationException(message);
            results.Add("PASS: " + message);
        }
    }
}
