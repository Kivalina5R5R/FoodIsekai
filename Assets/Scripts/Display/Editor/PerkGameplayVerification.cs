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
    // Exercises real gameplay components in an isolated preview scene without running or saving the user's scene.
    [InitializeOnLoad]
    public static class PerkGameplayVerification
    {
        private const string RequestPath = "Temp/PerkGameplayVerification.request";
        private const string ResultPath = "Temp/PerkGameplayVerification.txt";

        static PerkGameplayVerification() => EditorApplication.delayCall += RunRequested;

        private static void RunRequested()
        {
            if (!File.Exists(RequestPath) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(RequestPath);
            Verify();
        }

        [MenuItem("Food Isekai/Verify Perk Gameplay")]
        public static void Verify()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var results = new List<string>();
            UnityEngine.Random.State randomState = UnityEngine.Random.state;
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                UnityEngine.Random.InitState(291026);
                FoodIsekaiZGameManager game = Create<FoodIsekaiZGameManager>(preview, "Perk Test Game");
                Set(game, "useMealWaves", false);
                Set(game, "initialActiveCustomers", 0);
                ArenaSlot2D customer = Create<ArenaSlot2D>(preview, "Perk Test Customer");
                customer.Configure("C1", ArenaSlotType.Customer, FoodType.None, game);
                ArenaSlot2D station = Create<ArenaSlot2D>(preview, "Perk Test Station");
                station.Configure("F1", ArenaSlotType.FoodStation, FoodType.Food1, game);
                game.ConfigureSlots(new[] { customer }, new[] { station }, false);
                FoodIsekaiZPlayerState player = Create<FoodIsekaiZPlayerState>(preview, "Perk Test Player 1");
                FoodIsekaiZPlayerState other = Create<FoodIsekaiZPlayerState>(preview, "Perk Test Player 2");
                Set(other, "fallbackPlayerId", 2);

                Buy(game, PerkDefinitions.Tasty);
                Buy(game, PerkDefinitions.Happiness);
                Check(game.TryInteract(player, station), "Tasty/Happiness bind through normal pickup.", results);
                station.Configure("F2", ArenaSlotType.FoodStation, FoodType.Food2, game);
                game.TryInteract(player, station);
                station.Configure("F3", ArenaSlotType.FoodStation, FoodType.Food3, game);
                game.TryInteract(player, station);
                customer.ConfigureCustomer(FoodType.Food3, 20, 10);
                Check(!game.TryInteract(player, customer) && player.HeldFoods.Count == 3,
                    "Unrevealed orders cannot consume or discard food.", results);
                customer.StartCustomerTimer();
                Check(game.TryInteract(player, customer) && player.HeldFoods.Count == 2 &&
                    player.HeldFood == FoodType.Food1 && game.GetPlayerScore(1) == 10,
                    "Slot-three delivery earns only its sender's score and preserves earlier dishes.", results);
                customer.ConfigureCustomer(FoodType.Food5, 20, 10);
                customer.StartCustomerTimer();
                Check(game.TryInteract(player, customer) && player.HeldFoods.Count == 1 && player.HeldFood == FoodType.Food2,
                    "A fully wrong inventory discards only slot one.", results);

                player.TryDiscardHeldFood();
                customer.ConfigureCustomer(FoodType.Food1, 20, 20, FoodType.Food2);
                customer.StartCustomerTimer();
                player.TryPickFood(FoodType.Food1);
                other.TryPickFood(FoodType.Food2);
                int before = game.GetPlayerScore(1);
                game.TryInteract(player, customer);
                Check(customer.CustomerState == CustomerSlotState.WaitingForFood &&
                    customer.RemainingFoods.Count == 1 && game.GetPlayerScore(1) == before + 10,
                    "First paired delivery earns one dish while the customer keeps waiting.", results);
                game.TryInteract(other, customer);
                Check(customer.CustomerState == CustomerSlotState.Eating && game.GetPlayerScore(2) == 10,
                    "A teammate can finish a pair and receive their own dish score.", results);

                game.Perks.Reset();
                Buy(game, "Perk_Big_Home");
                customer.ConfigureCustomer(FoodType.Food5, 20, 10);
                int teamBefore = game.TeamScore;
                int playerBefore = game.GetPlayerScore(1);
                Tick(game, 0);
                Check(customer.CustomerState == CustomerSlotState.WaitingForFood,
                    "Home waits for the visible order.", results);
                customer.StartCustomerTimer();
                Tick(game, 0);
                Check(customer.CustomerState == CustomerSlotState.Eating && game.TeamScore == teamBefore + 10 &&
                    game.GetPlayerScore(1) == playerBefore,
                    "Home serves revealed drinks automatically and credits the team only.", results);

                game.Perks.Reset();
                Buy(game, "Perk_Big_Bank");
                VerifyAutomaticMoneyVisual(preview, results);
                int automaticCollectionBursts = 0;
                game.CustomerMoneyAutomaticallyCollected += (paidSlot, amount) =>
                {
                    CheckQuiet(paidSlot.CustomerState == CustomerSlotState.Empty && amount == 10,
                        "The Bank burst must follow removal of the money pile.");
                    automaticCollectionBursts++;
                };
                teamBefore = game.TeamScore;
                Tick(game, 10);
                Tick(game, 10);
                Check(customer.CustomerState == CustomerSlotState.MoneyAvailable &&
                    customer.IsAutomaticCollectionPending && game.TotalBankedMoney == 0,
                    "Bank leaves the reward visible before collecting.", results);
                Set(customer, "automaticCollectionReadyAt", Time.time - 1f);
                Invoke(game, "TickCompletedCustomerMoney");
                Check(game.TotalBankedMoney == 0, "Bank cannot collect during its spawn frame.", results);
                Set(customer, "automaticCollectionSpawnFrame", Time.frameCount - 1);
                customer.WaitForAutomaticCollectionPresentation(() => false);
                Invoke(game, "TickCompletedCustomerMoney");
                Check(game.TotalBankedMoney == 0, "Bank waits for the money animation to finish.", results);
                customer.WaitForAutomaticCollectionPresentation(() => true);
                Invoke(game, "TickCompletedCustomerMoney");
                Invoke(game, "TickCompletedCustomerMoney");
                Check(automaticCollectionBursts == 1,
                    "Bank emits one gold burst after the money pile disappears.", results);
                Check(game.TotalBankedMoney == 10 && game.TeamScore == teamBefore + 5 &&
                    customer.CustomerState == CustomerSlotState.Empty && game.GetPlayerScore(1) == playerBefore,
                    "Bank deposits once, grants the team deposit score, and releases the customer slot.", results);

                game.Perks.Reset();
                Buy(game, "Perk_Small_Cross");
                Set(game, "teamScore", 100);
                for (int i = 0; i < 4; i++)
                {
                    customer.ConfigureCustomer(FoodType.Food1, 1, 10);
                    customer.StartCustomerTimer();
                    Tick(game, 2);
                }
                Check(game.TeamScore == 81, "Cross retains fractional reductions: four five-point penalties cost 19.", results);

                game.Perks.Reset();
                Buy(game, "Perk_Small_Longer");
                Buy(game, "Perk_Small_Glad");
                Buy(game, "Perk_Small_Spoon");
                Buy(game, "Perk_Small_Meat");
                Set(game, "moneyRewardRange", new Vector2Int(20, 20));
                Invoke(game, "SpawnCustomer", customer, 0);
                Check(Mathf.Abs(customer.OrderDurationSeconds - 20.4f) < .001f && customer.OrderReward == 21,
                    "Longer and Glad modify the spawned customer's actual patience and payment.", results);
                customer.ConfigureCustomer(FoodType.Food1, 20, 10);
                customer.StartCustomerTimer();
                player.TryPickFood(FoodType.Food1);
                playerBefore = game.GetPlayerScore(1);
                game.TryInteract(player, customer);
                Check(game.GetPlayerScore(1) == playerBefore + 20 && Mathf.Abs(customer.StateRemainingSeconds - 3f / 1.05f) < .001f,
                    "Food-specific double score and faster eating are applied at delivery.", results);

                game.Perks.Reset();
                Buy(game, "Perk_Big_Pairs");
                int pairs = 0;
                for (int i = 0; i < 3000; i++)
                {
                    Invoke(game, "SpawnCustomer", customer, 0);
                    if (customer.RemainingFoods.Count == 2)
                    {
                        pairs++;
                        CheckQuiet(customer.OrderReward == 40, "Pair reward must be doubled.");
                    }
                }
                Check(pairs > 1350 && pairs < 1650, "Pairs generates approximately 50% double orders with double payment.", results);

                game.Perks.Reset();
                Buy(game, "Perk_Big_Omakase");
                int omakase = 0;
                for (int i = 0; i < 3000; i++)
                {
                    Invoke(game, "SpawnCustomer", customer, 0);
                    if (customer.IsOmakase) omakase++;
                }
                Check(omakase > 1650 && omakase < 1950, "Omakase generation follows the configured 60% rate.", results);

                game.Perks.Reset();
                Buy(game, "Perk_Big_Sky");
                game.StartCustomerFlow();
                FoodType specialMenu = game.SpecialMenuFood;
                int specials = 0;
                for (int i = 0; i < 3000; i++)
                {
                    Invoke(game, "SpawnCustomer", customer, 0);
                    CheckQuiet((customer.RequestedFood == specialMenu) == customer.IsSpecialOrder,
                        "Every order for the chosen menu must be special.");
                    if (customer.IsSpecialOrder) specials++;
                }
                Check(specialMenu != FoodType.None && specials > 750 && specials < 1050,
                    "Sky picks one meal menu; about 30% of customers order it and every matching order is special.", results);
                customer.ConfigureCustomer(specialMenu, 20, 40, special: true);
                customer.StartCustomerTimer();
                Tick(game, 1000);
                Check(customer.CustomerState == CustomerSlotState.WaitingForFood && !customer.IsOrderNearTimeout,
                    "Sky customers never run out of patience.", results);
                player.TryDiscardHeldFood();
                player.TryPickFood(specialMenu);
                playerBefore = game.GetPlayerScore(1);
                game.TryInteract(player, customer);
                Check(game.GetPlayerScore(1) == playerBefore + 20 && customer.CustomerState == CustomerSlotState.Eating,
                    "Sky uses normal pickup/delivery and awards double dish score.", results);

                results.Add("PASS: Live scene untouched. Active scene dirty=" + SceneManager.GetActiveScene().isDirty);
                File.WriteAllLines(ResultPath, results);
                Debug.Log("[PerkVerification] " + results.Count + " checks passed.");
            }
            catch (Exception exception)
            {
                results.Add("FAIL: " + exception);
                File.WriteAllLines(ResultPath, results);
                Debug.LogException(exception);
            }
            finally
            {
                UnityEngine.Random.state = randomState;
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static void VerifyAutomaticMoneyVisual(Scene scene, List<string> results)
        {
            ArenaSlot2D slot = Create<ArenaSlot2D>(scene, "Bank Animation Slot");
            var money = new GameObject("Bank Animation Money", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(money, scene);
            money.transform.SetParent(slot.transform, false);
            money.SetActive(false);
            var reward = money.AddComponent<FloorAnimatedSprite>();
            Type motionType = typeof(FloorAnimatedSprite).GetField("motion", BindingFlags.NonPublic | BindingFlags.Instance).FieldType;
            Set(reward, "motion", Enum.ToObject(motionType, 2));
            Set(reward, "rewardSprite", AssetDatabase.LoadAssetAtPath<Sprite>(
                AssetDatabase.GUIDToAssetPath("975c67a090b641578f54b9c3da37213d")));
            slot.ConfigureVisuals(money, null);
            slot.SpawnMoney(10, true);
            Set(slot, "automaticCollectionReadyAt", Time.time - 1f);
            Set(slot, "automaticCollectionSpawnFrame", Time.frameCount - 1);
            Check(money.activeSelf && reward.IsAnimating && !slot.IsAutomaticCollectionReady,
                "Bank money starts its own animation without FloorMoneyFeedback.", results);
            Set(reward, "automaticCollectionFrame", Time.frameCount - 1);
            Invoke(reward, "Advance", 10f);
            Check(!slot.IsAutomaticCollectionReady, "Bank waits for the first visible money mesh.", results);
            using (var mesh = new UnityEngine.UI.VertexHelper())
            {
                Invoke(reward, "OnPopulateMesh", mesh);
                var vertex = new UIVertex();
                mesh.PopulateUIVertex(ref vertex, 4);
                Check(mesh.currentVertCount == 8 && vertex.color.a > 0,
                    "Bank's first money frame is opaque instead of fading from invisible.", results);
            }
            Set(reward, "firstAutomaticRenderFrame", Time.frameCount - 1);
            Invoke(reward, "Advance", 1f);
            Check(slot.IsAutomaticCollectionReady, "Bank collects only after the visible money animation completes.", results);
            slot.CollectMoney();
        }

        private static T Create<T>(Scene scene, string name) where T : Component
        {
            var instance = new GameObject(name);
            SceneManager.MoveGameObjectToScene(instance, scene);
            return instance.AddComponent<T>();
        }

        private static void Buy(FoodIsekaiZGameManager game, string id) =>
            game.Perks.RecordPurchase(new PerkPurchase(new PerkOffer(id, 80), 1, 2));
        private static void Tick(FoodIsekaiZGameManager game, float seconds) => Invoke(game, "TickCustomerStates", seconds);
        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        private static void Invoke(object target, string method, params object[] values) =>
            target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, values);
        private static void CheckQuiet(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        private static void Check(bool condition, string message, List<string> results)
        {
            CheckQuiet(condition, message);
            results.Add("PASS: " + message);
        }
    }
}
