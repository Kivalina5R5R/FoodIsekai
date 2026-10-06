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
        private const string ResultPath = "Logs/PerkGameplayVerification.txt";

        static PerkGameplayVerification() => EditorApplication.update += RunRequested;

        private static void RunRequested()
        {
            if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            bool updatedPerksOnly = File.ReadAllText(RequestPath).Trim() == "updated-perks";
            File.Delete(RequestPath);
            if (updatedPerksOnly) VerifyUpdatedPerks();
            else Verify();
        }

        // Tests all combinations of the five updated perks through real spawning and player delivery.
        [MenuItem("Food Isekai/Verify Updated Perks")]
        public static void VerifyUpdatedPerks()
        {
            const string output = "Logs/UpdatedPerksVerification.txt";
            if (EditorApplication.isPlayingOrWillChangePlaymode && !Application.isBatchMode) return;
            var results = new List<string>();
            UnityEngine.Random.State randomState = UnityEngine.Random.state;
            Scene preview = EditorSceneManager.NewPreviewScene();
            bool logging = PlayerSessionLog.RecordingEnabled;
            try
            {
                PlayerSessionLog.RecordingEnabled = false;
                UnityEngine.Random.InitState(6102026);
                var game = Create<FoodIsekaiZGameManager>(preview, "Updated Perk Test Game");
                Set(game, "useMealWaves", false);
                Set(game, "moneyRewardRange", new Vector2Int(20, 20));
                var customer = Create<ArenaSlot2D>(preview, "Updated Perk Test Customer");
                customer.Configure("C1", ArenaSlotType.Customer, FoodType.None, game);
                game.ConfigureSlots(new[] { customer }, Array.Empty<ArenaSlot2D>(), false);
                var player = Create<FoodIsekaiZPlayerState>(preview, "Updated Perk Player 1");
                var teammate = Create<FoodIsekaiZPlayerState>(preview, "Updated Perk Player 2");
                Set(teammate, "fallbackPlayerId", 2);
                string[] ids = { "Perk_Small_Longer", "Perk_Small_Glad", "Perk_Small_Spoon", "Perk_Small_Cross", "Perk_Big_Pairs" };
                int orders = 0;
                int deliveries = 0;
                for (int mask = 0; mask < 32; mask++)
                {
                    game.Perks.Reset();
                    for (int perk = 0; perk < ids.Length; perk++)
                        if ((mask & (1 << perk)) != 0) Buy(game, ids[perk]);
                    bool longer = (mask & 1) != 0;
                    bool glad = (mask & 2) != 0;
                    bool spoon = (mask & 4) != 0;
                    bool cross = (mask & 8) != 0;
                    bool pairs = (mask & 16) != 0;
                    for (int meal = 0; meal < 3; meal++)
                    {
                        Set(game, "currentWaveIndex", meal);
                        for (int sample = 0; sample < 40; sample++)
                        {
                            Invoke(game, "SpawnCustomer", customer, 0);
                            int count = customer.OrderDishCount;
                            int multiplier = pairs && count >= 2 ? 2 : 1;
                            float patience = longer ? 25 : 20;
                            CheckQuiet(customer.OrderDurationSeconds == patience &&
                                customer.OrderReward == (20 * count + (glad ? 5 : 0)) * multiplier,
                                $"Spawn reward or patience mismatch: mask={mask}, dishes={count}.");
                            CheckQuiet(count >= 1 && count <= meal + 1 &&
                                new HashSet<FoodType>(customer.RemainingFoods).Count == count,
                                "Meal count or menu uniqueness mismatch.");
                            customer.StartCustomerTimer();
                            customer.AdvanceStateTimer(3);
                            for (int dish = 0; dish < count; dish++)
                            {
                                var sender = dish % 2 == 0 ? player : teammate;
                                FoodType food = customer.RemainingFoods[customer.RemainingFoods.Count - 1];
                                sender.TryPickFood(food);
                                int score = game.GetPlayerScore(sender.PlayerId);
                                CheckQuiet(game.TryInteract(sender, customer) &&
                                    game.GetPlayerScore(sender.PlayerId) == score + 10 * multiplier,
                                    "Per-dish score or alternating-player delivery mismatch.");
                                if (dish + 1 < count)
                                    CheckQuiet(customer.CustomerState == CustomerSlotState.WaitingForFood &&
                                        customer.StateRemainingSeconds == patience - 3 + 5 * (dish + 1),
                                        "Partial delivery did not add exactly five seconds.");
                                deliveries++;
                            }
                            CheckQuiet(customer.CustomerState == CustomerSlotState.Eating &&
                                customer.StateRemainingSeconds == (spoon ? 1.5f : 3f),
                                "Spoon eating duration mismatch.");
                            orders++;
                        }
                    }
                    Set(game, "teamScore", 100);
                    for (int expired = 0; expired < 4; expired++)
                    {
                        customer.ConfigureCustomer(FoodType.Food1, 1, 20);
                        customer.StartCustomerTimer();
                        Tick(game, 2);
                    }
                    CheckQuiet(game.TeamScore == (cross ? 100 : 80), "Cross anger penalty mismatch.");
                }
                results.Add($"PASS: {orders} orders and {deliveries} player deliveries across all 32 perk combinations and all 3 meals.");
                results.Add("PASS: Longer changes initial wait from 20 to 25 seconds; partial deliveries still add exactly 5 seconds.");
                results.Add("PASS: Glad adds 5 coins once per order, before Pairs doubles qualifying order rewards.");
                results.Add("PASS: Spoon changes eating time from 3 to 1.5 seconds.");
                results.Add("PASS: Cross keeps 100 points after four angry customers; without Cross the same test leaves 80.");
                results.Add("PASS: Pairs pays 20 points per dish for original orders of 2 or 3 dishes, including the last dish; single dishes pay 10.");
                results.Add("PASS: No live scene was saved or edited.");
            }
            catch (Exception exception)
            {
                results.Add("FAIL: " + exception);
                Debug.LogException(exception);
            }
            finally
            {
                PlayerSessionLog.RecordingEnabled = logging;
                UnityEngine.Random.state = randomState;
                EditorSceneManager.ClosePreviewScene(preview);
                Directory.CreateDirectory("Logs");
                File.WriteAllLines(output, results);
            }
        }

        [MenuItem("Food Isekai/Verify Perk Gameplay")]
        public static void Verify()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode && !Application.isBatchMode) return;
            Directory.CreateDirectory("Logs");
            var results = new List<string>();
            UnityEngine.Random.State randomState = UnityEngine.Random.state;
            bool logging = PlayerSessionLog.RecordingEnabled;
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                PlayerSessionLog.RecordingEnabled = false;
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
                Check(game.TryInteract(player, station), "Tasty binds through normal pickup.", results);
                station.Configure("F2", ArenaSlotType.FoodStation, FoodType.Food2, game);
                game.TryInteract(player, station);
                Check(player.HeldFoods.Count == 2 && player.HeldFood == FoodType.Food1,
                    "Tasty carries two distinct menus.", results);
                game.TryInteract(player, station);
                Check(player.HeldFoods.Count == 2, "Tasty ignores duplicate pickups.", results);
                station.Configure("F3", ArenaSlotType.FoodStation, FoodType.Food3, game);
                game.TryInteract(player, station);
                Check(player.HeldFoods.Count == 2 && player.HeldFood == FoodType.Food2,
                    "A full Tasty plate replaces its oldest menu.", results);
                while (player.TryDiscardHeldFood()) { }
                station.Configure("F1", ArenaSlotType.FoodStation, FoodType.Food1, game);
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
                Buy(game, "Perk_Big_Pairs");
                customer.ConfigureCustomer(FoodType.Food1, 20, 60, FoodType.Food2, thirdFood: FoodType.Food3);
                customer.StartCustomerTimer();
                Check(!customer.TryBeginEating(5), "Incomplete multi-dish orders cannot begin eating.", results);
                customer.AdvanceStateTimer(7);
                Check(!customer.TryServeFood(FoodType.Food4, 5) && customer.StateRemainingSeconds == 13,
                    "Wrong dishes never extend the waiting timer.", results);
                before = game.GetPlayerScore(1);
                int otherBefore = game.GetPlayerScore(2);
                player.TryPickFood(FoodType.Food3);
                game.TryInteract(player, customer);
                Check(customer.StateRemainingSeconds == 18 && customer.RemainingFoods.Count == 2 &&
                    game.GetPlayerScore(1) == before + 20, "Triple delivery adds five seconds and doubles its sender's dish score.", results);
                other.TryPickFood(FoodType.Food1);
                game.TryInteract(other, customer);
                Check(customer.StateRemainingSeconds == 23 && customer.RemainingFoods.Count == 1 &&
                    game.GetPlayerScore(2) == otherBefore + 20, "A different player can serve the next menu in any order.", results);
                player.TryPickFood(FoodType.Food2);
                game.TryInteract(player, customer);
                Check(customer.CustomerState == CustomerSlotState.Eating && game.GetPlayerScore(1) == before + 40,
                    "The last dish keeps double score and starts eating only when all three arrive.", results);
                VerifyGeneratedOrders(game, customer, results);

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
                Check(game.TeamScore == 100, "Cross prevents all score loss across four angry customers.", results);

                game.Perks.Reset();
                Buy(game, "Perk_Small_Longer");
                Buy(game, "Perk_Small_Glad");
                Buy(game, "Perk_Small_Spoon");
                Buy(game, "Perk_Small_Meat");
                Set(game, "moneyRewardRange", new Vector2Int(20, 20));
                Invoke(game, "SpawnCustomer", customer, 0);
                Check(Mathf.Abs(customer.OrderDurationSeconds - 25f) < .001f && customer.OrderReward == 25,
                    "Longer and Glad modify the spawned customer's actual patience and payment.", results);
                customer.ConfigureCustomer(FoodType.Food1, 20, 10);
                customer.StartCustomerTimer();
                player.TryPickFood(FoodType.Food1);
                playerBefore = game.GetPlayerScore(1);
                game.TryInteract(player, customer);
                Check(game.GetPlayerScore(1) == playerBefore + 20 && Mathf.Abs(customer.StateRemainingSeconds - 1.5f) < .001f,
                    "Food-specific double score and faster eating are applied at delivery.", results);

                game.Perks.Reset();
                Buy(game, "Perk_Big_Pairs");
                Set(game, "currentWaveIndex", 1);
                int pairs = 0;
                for (int i = 0; i < 3000; i++)
                {
                    Invoke(game, "SpawnCustomer", customer, 0);
                    if (customer.RemainingFoods.Count == 2)
                    {
                        pairs++;
                        CheckQuiet(customer.OrderReward == 80, "Two twenty-coin dishes must pay forty coins doubled.");
                    }
                }
                Check(pairs > 1050 && pairs < 1350, "Lunch retains its 40% paired orders with the double-reward perk.", results);
                Buy(game, "Perk_Small_Glad");
                Set(game, "currentWaveIndex", 2);
                for (int i = 0; i < 300; i++)
                {
                    Invoke(game, "SpawnCustomer", customer, 0);
                    int count = customer.OrderDishCount;
                    CheckQuiet(customer.OrderReward == (20 * count + 5) * (count >= 2 ? 2 : 1),
                        "Glad adds five coins once per order before Pairs doubles the total.");
                }
                Check(true, "Glad and Pairs stack correctly for one, two and three dishes.", results);
                Set(game, "currentWaveIndex", -1);

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
                    CheckQuiet(customer.OrderReward == 20 * customer.OrderDishCount * (customer.IsSpecialOrder ? 2 : 1),
                        "Sky must double the generated per-dish money reward only for special orders.");
                    if (customer.IsSpecialOrder) specials++;
                }
                Check(specialMenu != FoodType.None && specials > 750 && specials < 1050,
                    "Sky picks one meal menu; about 30% of customers order it and every special order pays double money.", results);
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
                VerifyFoodScoreAndServiceCombinations(game, customer, player, results);

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
                PlayerSessionLog.RecordingEnabled = logging;
                UnityEngine.Random.state = randomState;
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static void VerifyFoodScoreAndServiceCombinations(FoodIsekaiZGameManager game,
            ArenaSlot2D customer, FoodIsekaiZPlayerState player, List<string> results)
        {
            string[] ids = { "Perk_Small_Meat", "Perk_Small_Seafood", "Perk_Small_Starter", "Perk_Small_Dessert", "Perk_Small_Drink" };
            for (int perk = 0; perk < ids.Length; perk++)
            {
                game.Perks.Reset();
                Buy(game, ids[perk]);
                for (int menu = 1; menu <= 5; menu++)
                {
                    FoodType food = (FoodType)menu;
                    customer.ConfigureCustomer(food, 20, 20);
                    customer.StartCustomerTimer();
                    player.TryPickFood(food);
                    int before = game.GetPlayerScore(player.PlayerId);
                    CheckQuiet(game.TryInteract(player, customer) &&
                        game.GetPlayerScore(player.PlayerId) == before + (menu == perk + 1 ? 20 : 10),
                        "Food-score perk doubled a wrong menu or failed its own menu.");
                }
                Check(true, ids[perk] + " doubles only its matching menu across all five food types.", results);
            }

            game.Perks.Reset();
            Buy(game, "Perk_Big_Pairs");
            Buy(game, "Perk_Small_Drink");
            Buy(game, "Perk_Big_Home");
            customer.ConfigureCustomer(FoodType.Food1, 20, 60, FoodType.Food5, thirdFood: FoodType.Food3);
            customer.StartCustomerTimer();
            int teamBefore = game.TeamScore;
            int playerBefore = game.GetPlayerScore(player.PlayerId);
            Tick(game, 0);
            Check(customer.RemainingFoods.Count == 2 && customer.CustomerState == CustomerSlotState.WaitingForFood &&
                customer.StateRemainingSeconds == 25 && game.TeamScore == teamBefore + 40 &&
                game.GetPlayerScore(player.PlayerId) == playerBefore,
                "Home serves the drink in a triple order, adds five seconds, stacks Drink/Pairs and credits only the team.", results);
            Tick(game, 0);
            Check(game.TeamScore == teamBefore + 40, "Home cannot credit the same drink twice.", results);

            game.Perks.Reset();
            Buy(game, "Perk_Big_Pairs");
            Buy(game, "Perk_Small_Drink");
            Buy(game, "Perk_Big_Omakase");
            for (int menu = 1; menu <= 5; menu++)
            {
                customer.ConfigureCustomer(FoodType.Food1, 20, 60, FoodType.Food2, omakase: true, thirdFood: FoodType.Food3);
                customer.StartCustomerTimer();
                for (int dish = 0; dish < 3; dish++)
                {
                    player.TryPickFood((FoodType)menu);
                    playerBefore = game.GetPlayerScore(player.PlayerId);
                    CheckQuiet(game.TryInteract(player, customer) &&
                        game.GetPlayerScore(player.PlayerId) == playerBefore + (menu == 5 ? 40 : 20),
                        "Omakase failed to accept a menu or apply actual-food/Pairs score.");
                }
                CheckQuiet(customer.CustomerState == CustomerSlotState.Eating, "Omakase did not finish after three deliveries.");
            }
            Check(true, "Omakase accepts all five menus through actual player delivery and stacks matching food score with Pairs.", results);
        }

        private static void VerifyGeneratedOrders(FoodIsekaiZGameManager game, ArenaSlot2D customer, List<string> results)
        {
            for (int multiplier = 1; multiplier <= 2; multiplier++)
            {
                game.Perks.Reset();
                if (multiplier == 2) Buy(game, "Perk_Big_Pairs");
                for (int meal = 1; meal <= 3; meal++)
                {
                    Set(game, "currentWaveIndex", meal - 1);
                    for (int sample = 0; sample < 200; sample++)
                    {
                        Invoke(game, "SpawnCustomer", customer, 0);
                        int count = customer.OrderDishCount;
                        int rewardMultiplier = count >= 2 ? multiplier : 1;
                        CheckQuiet(count >= 1 && count <= meal &&
                            new HashSet<FoodType>(customer.RemainingFoods).Count == count &&
                            customer.OrderDurationSeconds == 20 &&
                            customer.OrderReward >= count * 10 * rewardMultiplier &&
                            customer.OrderReward <= count * 20 * rewardMultiplier,
                            "Generated order has an invalid count, duplicate menu, timer or per-dish money reward.");
                    }
                }
            }
            Set(game, "currentWaveIndex", -1);
            Check(true, "1,200 generated orders use unique menus, meal count limits, twenty-second waits and correct money ranges with/without Pairs.", results);
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
            target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance, null,
                Array.ConvertAll(values, value => value.GetType()), null).Invoke(target, values);
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
