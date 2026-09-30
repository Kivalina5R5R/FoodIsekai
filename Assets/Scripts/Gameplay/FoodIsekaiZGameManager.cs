using System;
using System.Collections.Generic;
using Fortal.UWB;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace FoodIsekaiZ.Gameplay
{

    public sealed class FoodIsekaiZGameManager : MonoBehaviour, IPerkWallet
    {
        private const int CustomerSlotCapacity = 6;
        private const int MaximumConsecutiveSameFoodOrders = 2;

        [Serializable]
        private sealed class PlayerScoreRecord
        {
            [Min(1)] public int playerId;
            public int score;

            public PlayerScoreRecord(int playerId, int score)
            {
                this.playerId = playerId;
                this.score = score;
            }
        }

        [Serializable]
        public sealed class MealWaveDefinition
        {
            [InspectorName("Wave Name")]
            public string displayName = "MEAL";

            public MealWaveDefinition()
            {
            }

            public MealWaveDefinition(string displayName)
            {
                this.displayName = displayName;
            }
        }

        [Serializable]
        public sealed class FoodOption
        {
            public FoodType food = FoodType.Food1;
            public string displayName = "FOOD 1";
            public Color color = Color.white;
            public bool canBeOrdered = true;

            public FoodOption()
            {
            }

            public FoodOption(FoodType food, string displayName, Color color)
            {
                this.food = food;
                this.displayName = displayName;
                this.color = color;
            }
        }

        [Header("Top Area - 6 Customer Slots (C1-C6)")]
        [SerializeField] private ArenaSlot2D[] customerSlots = new ArenaSlot2D[CustomerSlotCapacity];

        [Header("Bottom Area - 5 Food + 1 Bank")]
        [SerializeField] private ArenaSlot2D[] stationSlots = new ArenaSlot2D[6];

        [Header("Meal Waves")]
        [SerializeField] private bool useMealWaves = true;
        [Tooltip("เวลาทำอาหารของแต่ละ Wave ใช้ค่าเดียวกันทั้ง BREAKFAST, LUNCH และ DINNER")]
        [InspectorName("Wave Duration (Seconds)")]
        [SerializeField, Min(1f)] private float waveDurationSeconds = 90f;
        [Tooltip("เวลาพักระหว่าง Wave ก่อนเริ่มมื้อถัดไป")]
        [InspectorName("Break Duration (Seconds)")]
        [SerializeField, Min(0f)] private float intermissionDurationSeconds = 30f;
        [Tooltip("ช่วงวินาทีสุดท้ายของ Wave ที่ลูกค้าคนใหม่จะไม่เดินเข้าช่องแล้ว เพื่อเตรียมพักเบรก")]
        [InspectorName("Last Call Before Break (Seconds)")]
        [SerializeField, Min(0f)] private float lastCallBeforeBreakSeconds = 7.5f;
        [Tooltip("ชื่อของแต่ละ Wave เรียงตามลำดับการเล่น")]
        [SerializeField] private MealWaveDefinition[] mealWaves =
        {
            new MealWaveDefinition("BREAKFAST"),
            new MealWaveDefinition("LUNCH"),
            new MealWaveDefinition("DINNER")
        };

        [Header("Customer Spawning")]
        [SerializeField] private bool startCustomersOnPlay = true;
        [Tooltip("Wait for the scene startup sequence before starting customers or accepting interactions.")]
        [SerializeField] private bool waitForStartup;
        [SerializeField, Range(0, CustomerSlotCapacity)] private int initialActiveCustomers = CustomerSlotCapacity;
        [SerializeField, Range(1, CustomerSlotCapacity)] private int maximumActiveCustomers = CustomerSlotCapacity;

        [Header("Customer Timing")]
        [Tooltip("เวลาที่ลูกค้ารอรับอาหารก่อนหนี")]
        [InspectorName("Wait For Food (Seconds)")]
        [SerializeField, Min(1f)] private float orderTimeLimitSeconds = 20f;
        [Tooltip("ช่วงเวลาสุ่ม Min/Max ก่อนลูกค้าคนใหม่เข้าช่อง C ที่ว่าง ใส่ค่าเท่ากันถ้าต้องการเวลาคงที่")]
        [InspectorName("New Customer Delay (Min / Max Seconds)")]
        [SerializeField] private Vector2 customerRespawnDelaySeconds = new Vector2(2f, 5f);
        [Tooltip("เวลาที่ลูกค้าใช้กินอาหารก่อนวางเงิน")]
        [InspectorName("Eating Time (Seconds)")]
        [SerializeField, Min(0.1f)] private float eatingDurationSeconds = 3f;

        [Header("Order Rewards")]
        [Tooltip("Inclusive random money reward for one completed order.")]
        [SerializeField] private Vector2Int moneyRewardRange = new Vector2Int(10, 20);

        [Header("Scoring")]
        [SerializeField, Min(0)] private int correctServeScore = 10;
        [SerializeField, Min(0)] private int bankDepositScore = 5;
        [SerializeField, Min(0)] private int escapedCustomerPenalty = 5;

        [Header("Food Order Pool")]
        [SerializeField] private FoodOption[] foodOptions =
        {
            new FoodOption(FoodType.Food1, "FOOD 1", new Color(0.35f, 1f, 0.45f, 1f)),
            new FoodOption(FoodType.Food2, "FOOD 2", new Color(0.25f, 0.85f, 1f, 1f)),
            new FoodOption(FoodType.Food3, "FOOD 3", new Color(1f, 0.75f, 0.25f, 1f)),
            new FoodOption(FoodType.Food4, "FOOD 4", new Color(1f, 0.4f, 0.35f, 1f)),
            new FoodOption(FoodType.Food5, "FOOD 5", new Color(0.8f, 0.4f, 1f, 1f))
        };

        [Header("Runtime (Read Only)")]
        [FormerlySerializedAs("teamBankedMoney")]
        [SerializeField] private int teamScore;
        [SerializeField, Min(0)] private int servedOrderCount;
        [SerializeField, Min(0)] private int completedOrderCount;
        [SerializeField, Min(0)] private int expiredOrderCount;
        [SerializeField] private List<PlayerScoreRecord> playerScores = new List<PlayerScoreRecord>();
        [SerializeField] private MealWavePhase mealWavePhase = MealWavePhase.NotStarted;
        [SerializeField] private int currentWaveIndex = -1;
        [SerializeField, Min(0f)] private float mealPhaseRemainingSeconds;

        private float[] nextCustomerSpawnTimes = Array.Empty<float>();
        private readonly FoodOrderGenerator foodOrderGenerator = new FoodOrderGenerator();
        private bool customerFlowStarted;
        private bool mealWaveFlowStarted;
        private bool startupReleased;
        private bool phasePresentationPaused;
        private bool phaseChangePending;
        public bool IsPhasePresentationPaused => phasePresentationPaused;

        // Holds gameplay timers and interactions while the authored phase transition covers the wall.
        public void SetPhasePresentationPaused(bool paused) => phasePresentationPaused = paused;
        private UWBManager simulationModeSource;
        public bool IsSimulationMode => simulationModeSource != null && simulationModeSource.IsSimulationMode;

        public bool IsWaitingForStartup => waitForStartup && !startupReleased;
        private int lastNotifiedMealSecond = int.MinValue;

        private int totalBankedMoney;
        private readonly PerkManager perks = new PerkManager(PerkDefinitions.All);
        private double teamScoreRemainder;
        public PerkManager Perks => perks;
        public FoodType SpecialMenuFood { get; private set; }
        private int specialMenuWave = int.MinValue;
        private IWaveDepartureStatus departureStatus;
        private IMealIntermissionGate intermissionGate;

        public int TotalBankedMoney => totalBankedMoney;
        public int Balance => totalBankedMoney;

        // Spending changes the shared wallet, never earned service scores.
        public bool TrySpend(int amount)
        {
            if (mealWavePhase != MealWavePhase.Intermission || amount <= 0 || amount > totalBankedMoney)
                return false;
            totalBankedMoney -= amount;
            BankedMoneyChanged?.Invoke(totalBankedMoney);
            return true;
        }

        public void RegisterIntermissionGate(IMealIntermissionGate gate) => intermissionGate = gate;

        public void ReleaseIntermissionGate(IMealIntermissionGate gate)
        {
            if (ReferenceEquals(intermissionGate, gate)) intermissionGate = null;
        }

        // Ends the break now; the registered gate still controls the guide's departure before the next meal.
        public void EndIntermissionEarly()
        {
            if (mealWavePhase != MealWavePhase.Intermission) return;
            mealPhaseRemainingSeconds = 0f;
            NotifyMealWaveDisplayIfNeeded(true);
        }
        public int TeamScore => teamScore;
        // Counts accepted deliveries, including NPCs still eating when a wave ends.
        public int ServedOrderCount => servedOrderCount;
        public int CompletedOrderCount => completedOrderCount;
        public int ExpiredOrderCount => expiredOrderCount;
        public IReadOnlyList<ArenaSlot2D> CustomerSlots => customerSlots;
        public bool UsesMealWaves => useMealWaves;
        public MealWavePhase CurrentMealWavePhase => mealWavePhase;
        public int CurrentWaveNumber => currentWaveIndex >= 0 ? currentWaveIndex + 1 : 0;
        public int TotalWaveCount => mealWaves != null ? mealWaves.Length : 0;
        public float MealPhaseRemainingSeconds => mealPhaseRemainingSeconds;
        // True during the final seconds of an active wave, when no new customer may enter a slot.
        public bool IsNewCustomerEntryClosed =>
            useMealWaves &&
            mealWavePhase == MealWavePhase.Active &&
            mealPhaseRemainingSeconds <= lastCallBeforeBreakSeconds;
        public string CurrentWaveName => GetWaveDisplayName(currentWaveIndex);
        public string NextWaveName => GetWaveDisplayName(currentWaveIndex + 1);

        public event Action<FoodIsekaiZPlayerState, ArenaSlot2D, int> PlayerMoneyCollected;
        // Raised when a positive money pile cannot fit in this player's wallet.
        public event Action<FoodIsekaiZPlayerState, ArenaSlot2D> PlayerMoneyCollectionBlocked;
        public event Action<int, int> PlayerMoneyDeposited;
        // Includes both manual deliveries and automatic deposits during the break.
        public event Action<int> BankedMoneyChanged;
        // Carries both transaction endpoints for directional bank feedback.
        public event Action<FoodIsekaiZPlayerState, ArenaSlot2D, int> PlayerMoneyDelivered;
        public event Action<int, int> PlayerScoreChanged;
        public event Action<int> TeamScoreChanged;
        // Raised only after a station has successfully placed food in the player's hands.
        public event Action<FoodIsekaiZPlayerState, ArenaSlot2D> FoodPickedUp;
        // Raised as soon as the requested food is accepted and the customer starts eating.
        public event Action<FoodIsekaiZPlayerState, ArenaSlot2D> FoodServed;
        public event Action<FoodIsekaiZPlayerState, ArenaSlot2D> WrongFoodDiscarded;
        public event Action<ArenaSlot2D, FoodType> CustomerRequestedFood;
        // Raised when eating ends, before the reward becomes visible or collectible.
        public event Action<ArenaSlot2D, int> CustomerFinishedEating;
        // Raised when a completed order's money is visible and available to collect.
        public event Action<ArenaSlot2D, int> CustomerMoneySpawned;
        // Bank has finished the money animation and removed the pile from the floor.
        public event Action<ArenaSlot2D, int> CustomerMoneyAutomaticallyCollected;
        public event Action<ArenaSlot2D> CustomerOrderExpired;
        public event Action MealWaveDisplayChanged;
        // The presentation calls the completion action once the previous screen is fully covered.
        public event Action<string, Action> MealTransitionRequested;

        private void ChangeMealBehindCover(string heading, Action changePhase)
        {
            if (phaseChangePending) return;
            if (MealTransitionRequested == null)
            {
                changePhase();
                return;
            }
            phaseChangePending = true;
            phasePresentationPaused = true;
            MealTransitionRequested.Invoke(heading, () =>
            {
                phaseChangePending = false;
                changePhase();
            });
        }

        // The display registers its departure status; gameplay also works without a display.
        public void RegisterDepartureStatus(IWaveDepartureStatus status) => departureStatus = status;

        public void ReleaseDepartureStatus(IWaveDepartureStatus status)
        {
            if (ReferenceEquals(departureStatus, status)) departureStatus = null;
        }

        private void Start()
        {
            simulationModeSource = FindAnyObjectByType<UWBManager>();
            ValidateSlotLayout();
            if (!startCustomersOnPlay || IsWaitingForStartup)
            {
                return;
            }

            if (useMealWaves)
            {
                if (!mealWaveFlowStarted)
                {
                    StartMealWaveFlow();
                }
            }
            else if (!customerFlowStarted)
            {
                StartCustomerFlow();
            }
        }

        // Called once the intro has fully revealed the game; repeated calls are harmless.
        public void ReleaseStartup()
        {
            if (startupReleased) return;
            startupReleased = true;
            EnsureCustomerFlowStarted();
        }

        public void EnsureCustomerFlowStarted()
        {
            if (!Application.isPlaying || !startCustomersOnPlay || IsWaitingForStartup)
            {
                return;
            }

            if (customerSlots == null)
            {
                customerSlots = Array.Empty<ArenaSlot2D>();
            }

            if (useMealWaves)
            {
                if (!mealWaveFlowStarted)
                {
                    StartMealWaveFlow();
                }

                return;
            }

            bool timingStateMissing = nextCustomerSpawnTimes == null ||
                nextCustomerSpawnTimes.Length != customerSlots.Length;
            bool stalledWithoutCustomers = !HasNonEmptyCustomerSlot() && !HasPendingCustomerSpawn();
            if (!customerFlowStarted || timingStateMissing || stalledWithoutCustomers)
            {
                StartCustomerFlow();
            }
        }

        private void Update()
        {
            HandleSimulationMoneyShortcut();
            if (IsWaitingForStartup) return;
            if (HandleSimulationShortcut()) return;
            if (phasePresentationPaused) return;

            if (useMealWaves && mealWaveFlowStarted)
            {
                TickMealWave(Time.deltaTime);
            }
            if (phasePresentationPaused) return;

            if (useMealWaves && mealWavePhase == MealWavePhase.Clearing)
            {
                TickWaveClearance(Time.deltaTime);
                return;
            }

            TickCompletedCustomerMoney();
            if (useMealWaves && mealWaveFlowStarted && mealWavePhase != MealWavePhase.Active)
            {
                return;
            }

            if (!customerFlowStarted)
            {
                return;
            }

            TickCustomerStates(Time.deltaTime);
            SpawnReadyCustomers();
        }

        private void HandleSimulationMoneyShortcut()
        {
            if (simulationModeSource == null || !simulationModeSource.IsSimulationMode ||
                Keyboard.current == null || !Keyboard.current.bKey.wasPressedThisFrame) return;

            // Test funds are available during startup, transitions and the perk shop without awarding score.
            totalBankedMoney = (int)Math.Min(int.MaxValue, (long)totalBankedMoney + 1000);
            BankedMoneyChanged?.Invoke(totalBankedMoney);
        }

        private bool HandleSimulationShortcut()
        {
            if (simulationModeSource == null || !simulationModeSource.IsSimulationMode ||
                !useMealWaves || phasePresentationPaused || phaseChangePending || Keyboard.current == null)
            {
                return false;
            }

            if (Keyboard.current.mKey.wasPressedThisFrame && mealWaveFlowStarted &&
                (mealWavePhase == MealWavePhase.Active || mealWavePhase == MealWavePhase.Clearing))
            {
                BeginIntermission();
                return true;
            }

            if (!Keyboard.current.nKey.wasPressedThisFrame) return false;

            if (mealWavePhase == MealWavePhase.Completed)
            {
                Scene restartScene = SceneManager.GetActiveScene();
                // Include a System object persisted by an earlier script version in the scene unload.
                if (gameObject.scene != restartScene)
                {
                    SceneManager.MoveGameObjectToScene(transform.root.gameObject, restartScene);
                }

                // Reload the whole round so scores, players, and the startup sequence reset together.
                SceneManager.LoadScene(restartScene.path);
                return true;
            }

            if (!mealWaveFlowStarted) return false;

            if (mealWavePhase == MealWavePhase.Intermission && intermissionGate != null)
            {
                mealPhaseRemainingSeconds = 0f;
                NotifyMealWaveDisplayIfNeeded(true);
                return true;
            }

            // Skip directly to the next meal while preserving scores and the normal transition.
            if (currentWaveIndex >= TotalWaveCount - 1)
            {
                CompleteMealWaves();
                return true;
            }

            currentWaveIndex++;
            BeginCurrentWave();
            return true;
        }

        [ContextMenu("Start / Restart 3 Meal Waves")]
        public void StartMealWaveFlow()
        {
            if (IsWaitingForStartup) return;

            EnsureMealWaveConfiguration();
            perks.Reset();
            teamScoreRemainder = 0;
            specialMenuWave = int.MinValue;
            SpecialMenuFood = FoodType.None;
            mealWaveFlowStarted = true;
            currentWaveIndex = 0;
            BeginCurrentWave();
        }

        [ContextMenu("Start / Restart Customer Flow")]
        public void StartCustomerFlow()
        {
            if (IsWaitingForStartup) return;

            if (customerSlots == null)
            {
                customerSlots = Array.Empty<ArenaSlot2D>();
            }

            nextCustomerSpawnTimes = new float[customerSlots.Length];
            for (int i = 0; i < customerSlots.Length; i++)
            {
                if (customerSlots[i] != null)
                {
                    customerSlots[i].ClearCustomer();
                }

                nextCustomerSpawnTimes[i] = float.PositiveInfinity;
            }

            customerFlowStarted = true;
            foodOrderGenerator.Reset();
            SelectSpecialMenu();
            int initialCount = Mathf.Min(initialActiveCustomers, maximumActiveCustomers, CountUsableCustomerSlots());
            for (int i = 0; i < initialCount; i++)
            {
                int slotIndex = PickRandomEmptySlotIndex();
                if (slotIndex >= 0)
                {
                    SpawnCustomer(customerSlots[slotIndex], slotIndex);
                }
            }

            for (int i = 0; i < customerSlots.Length; i++)
            {
                if (customerSlots[i] != null && customerSlots[i].CustomerState == CustomerSlotState.Empty)
                {
                    ScheduleCustomer(i);
                }
            }
        }

        // Finds the nearest currently valid destination for the player's inventory on the floor plane.
        public bool TryGetDeliveryTarget(FoodIsekaiZPlayerState player, out ArenaSlot2D target)
        {
            target = null;
            if (IsWaitingForStartup || player == null || player.PlayerId <= 0) return false;
            if (useMealWaves && mealWavePhase != MealWavePhase.Active &&
                mealWavePhase != MealWavePhase.Clearing)
                return false;

            bool deliveringMoney = player.CarriedMoney > 0;
            if (!deliveringMoney && (player.HeldFood == FoodType.None ||
                (useMealWaves && mealWavePhase == MealWavePhase.Clearing))) return false;

            ArenaSlot2D[] candidates = deliveringMoney ? stationSlots : customerSlots;
            if (candidates == null) return false;
            float nearestDistanceSquared = float.PositiveInfinity;
            foreach (ArenaSlot2D candidate in candidates)
            {
                if (candidate == null || !candidate.isActiveAndEnabled) continue;
                if (deliveringMoney)
                {
                    if (candidate.SlotType != ArenaSlotType.MoneyDeposit) continue;
                }
                else if (candidate.SlotType != ArenaSlotType.Customer ||
                    !candidate.IsOrderRevealed ||
                    !HasDeliverableFood(player, candidate)) continue;

                Vector3 offset = candidate.transform.position - player.transform.position;
                float distanceSquared = offset.x * offset.x + offset.z * offset.z;
                if (distanceSquared >= nearestDistanceSquared) continue;
                nearestDistanceSquared = distanceSquared;
                target = candidate;
            }
            return target != null;
        }

        public bool TryInteract(FoodIsekaiZPlayerState player, ArenaSlot2D slot)
        {
            if (IsWaitingForStartup || phasePresentationPaused || player == null || slot == null)
            {
                return false;
            }

            if (useMealWaves &&
                mealWavePhase != MealWavePhase.Active &&
                mealWavePhase != MealWavePhase.Clearing)
            {
                return false;
            }

            if (useMealWaves && mealWavePhase == MealWavePhase.Clearing &&
                slot.SlotType != ArenaSlotType.MoneyDeposit &&
                slot.CustomerState != CustomerSlotState.MoneyAvailable) return false;

            switch (slot.SlotType)
            {
                case ArenaSlotType.FoodStation:
                    player.SetFoodCapacity(perks.FoodCapacity);
                    if (!player.TryPickFood(slot.StationFood))
                    {
                        return false;
                    }

                    FoodPickedUp?.Invoke(player, slot);
                    return true;

                case ArenaSlotType.MoneyDeposit:
                    return TryDepositMoney(player, slot);

                case ArenaSlotType.Customer:
                    return TryInteractWithCustomer(player, slot);

                default:
                    return false;
            }
        }

        private void TickMealWave(float deltaTime)
        {
            if (mealWavePhase != MealWavePhase.Active && mealWavePhase != MealWavePhase.Intermission)
            {
                return;
            }

            if (mealWavePhase == MealWavePhase.Intermission && mealPhaseRemainingSeconds > 0f &&
                intermissionGate != null && !intermissionGate.CanCountDown) return;

            mealPhaseRemainingSeconds = Mathf.Max(
                0f,
                mealPhaseRemainingSeconds - Mathf.Max(0f, deltaTime));
            NotifyMealWaveDisplayIfNeeded();
            if (mealPhaseRemainingSeconds > 0f)
            {
                return;
            }

            if (mealWavePhase == MealWavePhase.Active)
            {
                EndCurrentWave();
            }
            else
            {
                if (intermissionGate != null && !intermissionGate.TryFinish()) return;
                currentWaveIndex++;
                BeginCurrentWave();
            }
        }

        private void BeginCurrentWave()
        {
            if (mealWaves == null || currentWaveIndex < 0 || currentWaveIndex >= mealWaves.Length)
            {
                CompleteMealWaves();
                return;
            }

            ChangeMealBehindCover(CurrentWaveName, () =>
            {
                mealWavePhase = MealWavePhase.Active;
                mealPhaseRemainingSeconds = Mathf.Max(1f, waveDurationSeconds);
                lastNotifiedMealSecond = int.MinValue;
                StartCustomerFlow();
                NotifyMealWaveDisplayIfNeeded(true);
            });
        }

        private void EndCurrentWave()
        {
            customerFlowStarted = false;
            nextCustomerSpawnTimes = Array.Empty<float>();
            mealWavePhase = MealWavePhase.Clearing;
            mealPhaseRemainingSeconds = 0f;
            lastNotifiedMealSecond = int.MinValue;
            // Let the display identify unfinished customers before their orders are cleared.
            NotifyMealWaveDisplayIfNeeded(true);
            if (customerSlots == null) return;
            foreach (ArenaSlot2D slot in customerSlots)
            {
                if (slot == null || slot.CustomerState != CustomerSlotState.WaitingForFood) continue;
                // A customer still waiting at its slot when the break starts counts as an expired order.
                if (slot.IsOrderRevealed && !slot.IsSpecialOrder)
                {
                    expiredOrderCount++;
                    ApplyEscapedCustomerPenalty();
                    CustomerOrderExpired?.Invoke(slot);
                }
                slot.ClearCustomer();
            }
        }

        private void TickWaveClearance(float deltaTime)
        {
            TickCustomerStates(deltaTime);
            TickCompletedCustomerMoney();
            if (customerSlots != null)
                foreach (ArenaSlot2D slot in customerSlots)
                    if (slot != null && (slot.CustomerState == CustomerSlotState.Eating ||
                        slot.CustomerState == CustomerSlotState.Completing || slot.IsAutomaticCollectionPending)) return;
            if (departureStatus != null && departureStatus.HasNpcsInRestaurant) return;

            if (currentWaveIndex >= TotalWaveCount - 1)
            {
                CompleteMealWaves();
                return;
            }

            if (intermissionDurationSeconds <= 0f)
            {
                currentWaveIndex++;
                BeginCurrentWave();
                return;
            }
            BeginIntermission();
        }

        // Both normal clearance and the simulation shortcut use the same break transition.
        private void BeginIntermission()
        {
            ChangeMealBehindCover("SERVICE BREAK", () =>
            {
                SettleOutstandingMoney();
                DiscardPlayerFood();
                StopCustomerFlowAndClearSlots();
                mealWavePhase = MealWavePhase.Intermission;
                mealPhaseRemainingSeconds = Mathf.Max(0f, intermissionDurationSeconds);
                lastNotifiedMealSecond = int.MinValue;
                NotifyMealWaveDisplayIfNeeded(true);
            });
        }

        private void DiscardPlayerFood()
        {
            foreach (FoodIsekaiZPlayerState player in FindObjectsByType<FoodIsekaiZPlayerState>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
                player.TryDiscardHeldFood();
        }

        private void CompleteMealWaves()
        {
            ChangeMealBehindCover("SERVICE RESULTS", () =>
            {
                SettleOutstandingMoney();
                ConvertRemainingMoneyToScore();
                StopCustomerFlowAndClearSlots();
                mealWavePhase = MealWavePhase.Completed;
                mealPhaseRemainingSeconds = 0f;
                lastNotifiedMealSecond = int.MinValue;
                NotifyMealWaveDisplayIfNeeded(true);
            });
        }

        // Unspent team coins count one point each on the final team score, so the wallet empties into the result.
        private void ConvertRemainingMoneyToScore()
        {
            if (totalBankedMoney <= 0) return;
            int remaining = totalBankedMoney;
            totalBankedMoney = 0;
            BankedMoneyChanged?.Invoke(totalBankedMoney);
            AddTeamScore(remaining);
        }

        private void StopCustomerFlowAndClearSlots()
        {
            customerFlowStarted = false;
            if (customerSlots != null)
            {
                for (int i = 0; i < customerSlots.Length; i++)
                {
                    customerSlots[i]?.ClearCustomer();
                }
            }

            nextCustomerSpawnTimes = Array.Empty<float>();
        }

        private void NotifyMealWaveDisplayIfNeeded(bool force = false)
        {
            int displayedSecond = Mathf.CeilToInt(mealPhaseRemainingSeconds);
            if (!force && displayedSecond == lastNotifiedMealSecond)
            {
                return;
            }

            lastNotifiedMealSecond = displayedSecond;
            MealWaveDisplayChanged?.Invoke();
        }

        private string GetWaveDisplayName(int waveIndex)
        {
            if (mealWaves == null || waveIndex < 0 || waveIndex >= mealWaves.Length ||
                mealWaves[waveIndex] == null || string.IsNullOrWhiteSpace(mealWaves[waveIndex].displayName))
            {
                return string.Empty;
            }

            return mealWaves[waveIndex].displayName.Trim();
        }

        private void EnsureMealWaveConfiguration()
        {
            if (mealWaves == null || mealWaves.Length == 0)
            {
                mealWaves = new[]
                {
                    new MealWaveDefinition("BREAKFAST"),
                    new MealWaveDefinition("LUNCH"),
                    new MealWaveDefinition("DINNER")
                };
            }

            for (int i = 0; i < mealWaves.Length; i++)
            {
                if (mealWaves[i] == null)
                {
                    mealWaves[i] = new MealWaveDefinition($"MEAL {i + 1}");
                }
            }

            waveDurationSeconds = Mathf.Max(1f, waveDurationSeconds);
            intermissionDurationSeconds = Mathf.Max(0f, intermissionDurationSeconds);
            lastCallBeforeBreakSeconds = Mathf.Max(0f, lastCallBeforeBreakSeconds);
        }

        public ArenaSlot2D GetCustomerSlot(int index)
        {
            return customerSlots != null && index >= 0 && index < customerSlots.Length
                ? customerSlots[index]
                : null;
        }

        public string GetFoodDisplayName(FoodType food)
        {
            FoodOption option = GetFoodOption(food);
            return option != null && !string.IsNullOrWhiteSpace(option.displayName)
                ? option.displayName
                : food == FoodType.None ? "NO ORDER" : $"FOOD {(int)food}";
        }

        public Color GetFoodColor(FoodType food)
        {
            FoodOption option = GetFoodOption(food);
            return option != null ? option.color : Color.white;
        }

        public int GetPlayerScore(int playerId)
        {
            PlayerScoreRecord record = FindPlayerScoreRecord(playerId);
            return record != null ? record.score : 0;
        }

        public bool TryGetMvp(out int playerId, out int score)
        {
            playerId = 0;
            score = 0;
            bool found = false;
            if (playerScores == null)
            {
                return false;
            }

            for (int i = 0; i < playerScores.Count; i++)
            {
                PlayerScoreRecord entry = playerScores[i];
                if (entry == null)
                {
                    continue;
                }

                if (found && entry.score < score ||
                    (found && entry.score == score && entry.playerId >= playerId))
                {
                    continue;
                }

                playerId = entry.playerId;
                score = entry.score;
                found = true;
            }

            return found;
        }

        public void ConfigureSlots(
            ArenaSlot2D[] newCustomerSlots,
            ArenaSlot2D[] newStationSlots,
            bool restartActiveCustomerFlow = true)
        {
            customerSlots = newCustomerSlots ?? Array.Empty<ArenaSlot2D>();
            stationSlots = newStationSlots ?? Array.Empty<ArenaSlot2D>();

            if (restartActiveCustomerFlow && Application.isPlaying && customerFlowStarted)
            {
                StartCustomerFlow();
            }
        }

        private void TickCustomerStates(float deltaTime)
        {
            for (int i = 0; i < customerSlots.Length; i++)
            {
                ArenaSlot2D slot = customerSlots[i];
                if (slot != null) ServeAutomaticDrinks(slot);
                if (slot == null || !slot.AdvanceStateTimer(deltaTime))
                {
                    continue;
                }

                if (slot.CustomerState == CustomerSlotState.WaitingForFood)
                {
                    expiredOrderCount++;
                    ApplyEscapedCustomerPenalty();
                    CustomerOrderExpired?.Invoke(slot);
                    slot.ClearCustomer();
                    ScheduleCustomer(i);
                }
                else if (slot.TryFinishEating())
                {
                    int generation = slot.CustomerGeneration;
                    int reward = slot.OrderReward;
                    completedOrderCount++;
                    CustomerFinishedEating?.Invoke(slot, reward);
                    // Without a presentation, the reward is ready in this same frame.
                    TrySpawnCompletedCustomerMoney(slot, generation);
                }
            }
        }

        private void TickCompletedCustomerMoney()
        {
            if (customerSlots == null)
            {
                return;
            }

            for (int i = 0; i < customerSlots.Length; i++)
            {
                ArenaSlot2D slot = customerSlots[i];
                if (slot != null)
                {
                    TrySpawnCompletedCustomerMoney(slot, slot.CustomerGeneration);
                    if (slot.IsAutomaticCollectionReady)
                    {
                        int collected = slot.CollectMoney();
                        if (CreditMoneyDeposit(0, collected))
                            CustomerMoneyAutomaticallyCollected?.Invoke(slot, collected);
                        ScheduleCustomer(i);
                    }
                }
            }
        }

        private void TrySpawnCompletedCustomerMoney(ArenaSlot2D slot, int generation)
        {
            if (slot == null || slot.CustomerGeneration != generation || !slot.IsReadyToSpawnMoney)
            {
                return;
            }

            // A presentation callback must not release a reward belonging to a replacement customer.
            if (slot.CustomerGeneration != generation || slot.CustomerState != CustomerSlotState.Completing)
            {
                return;
            }

            int reward = slot.OrderReward;
            slot.SpawnMoney(reward, perks.HasEffect(PerkEffect.AutomaticBank));
            CustomerMoneySpawned?.Invoke(slot, reward);
        }

        private static bool HasDeliverableFood(FoodIsekaiZPlayerState player, ArenaSlot2D slot)
        {
            foreach (FoodType food in player.HeldFoods)
                if (slot.AcceptsFood(food)) return true;
            return false;
        }

        private float EffectiveEatingSeconds => eatingDurationSeconds / (float)perks.GetAmount(PerkEffect.EatingSpeed);

        private int GetServeScore(FoodType food, ArenaSlot2D slot) =>
            (int)(correctServeScore * perks.GetAmount(PerkEffect.FoodScore, food: food) * (slot.IsSpecialOrder ? 2 : 1));

        private void ServeAutomaticDrinks(ArenaSlot2D slot)
        {
            if (!perks.HasEffect(PerkEffect.AutomaticDrinks)) return;
            while (slot.AcceptsFood(FoodType.Food5))
            {
                if (!slot.TryServeFood(FoodType.Food5, EffectiveEatingSeconds)) break;
                AddTeamScore(GetServeScore(FoodType.Food5, slot));
                if (slot.CustomerState == CustomerSlotState.Eating) servedOrderCount++;
                FoodServed?.Invoke(null, slot);
            }
        }

        private void ApplyEscapedCustomerPenalty()
        {
            // Keep fractional reductions so the five-point base penalty still benefits from Cross.
            double exact = -escapedCustomerPenalty * perks.GetAmount(PerkEffect.AngerPenalty) + teamScoreRemainder;
            int rounded = (int)Math.Round(exact, MidpointRounding.AwayFromZero);
            AddTeamScore(rounded);
            teamScoreRemainder = teamScore > 0 ? exact - rounded : 0;
        }

        private bool TryInteractWithCustomer(FoodIsekaiZPlayerState player, ArenaSlot2D slot)
        {
            if (slot.CustomerState == CustomerSlotState.WaitingForFood)
            {
                if (!slot.IsOrderRevealed) return false;
                if (player.HeldFood == FoodType.None)
                {
                    return false;
                }

                if (!HasDeliverableFood(player, slot))
                {
                    if (!player.TryConsumeFood(player.HeldFood)) return false;
                    WrongFoodDiscarded?.Invoke(player, slot);
                    return true;
                }

                bool delivered = false;
                for (int i = player.HeldFoods.Count - 1; i >= 0; i--)
                {
                    FoodType food = player.HeldFoods[i];
                    if (!slot.TryServeFood(food, EffectiveEatingSeconds)) continue;
                    player.TryConsumeFood(food);
                    AddPlayerAndTeamScore(player.PlayerId, GetServeScore(food, slot));
                    if (slot.CustomerState == CustomerSlotState.Eating) servedOrderCount++;
                    FoodServed?.Invoke(player, slot);
                    delivered = true;
                }
                return delivered;
            }

            if (slot.CustomerState != CustomerSlotState.MoneyAvailable || slot.IsAutomaticCollectionPending)
            {
                return false;
            }

            // Reserve the entire pile in the wallet before clearing it from the floor.
            // A full wallet leaves the money and customer slot available for another player.
            if (!player.TryAddMoney(slot.AvailableMoney))
            {
                if (slot.AvailableMoney > 0)
                {
                    PlayerMoneyCollectionBlocked?.Invoke(player, slot);
                }
                return false;
            }

            int collected = slot.AvailableMoney;
            slot.CollectMoney();
            PlayerMoneyCollected?.Invoke(player, slot, collected);
            ScheduleCustomer(IndexOfCustomerSlot(slot));
            return true;
        }

        private bool TryDepositMoney(FoodIsekaiZPlayerState player, ArenaSlot2D bank)
        {
            int deposited = player.DepositAllMoney();
            if (!CreditMoneyDeposit(player.PlayerId, deposited)) return false;

            PlayerMoneyDelivered?.Invoke(player, bank, deposited);
            return true;
        }

        // Settle once behind the break cover, before the intermission summary is shown.
        private void SettleOutstandingMoney()
        {
            FoodIsekaiZPlayerState[] players = FindObjectsByType<FoodIsekaiZPlayerState>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (FoodIsekaiZPlayerState player in players)
            {
                CreditMoneyDeposit(player.PlayerId, player.DepositAllMoney());
            }

            if (customerSlots == null) return;
            foreach (ArenaSlot2D slot in customerSlots)
            {
                if (slot == null || slot.CustomerState != CustomerSlotState.MoneyAvailable) continue;
                CreditMoneyDeposit(0, slot.CollectMoney());
            }
        }

        // Unclaimed NPC piles have no player owner, so their deposit bonus belongs only to the team.
        private bool CreditMoneyDeposit(int playerId, int amount)
        {
            if (amount <= 0) return false;
            totalBankedMoney += amount;
            BankedMoneyChanged?.Invoke(totalBankedMoney);
            if (playerId > 0)
            {
                AddPlayerAndTeamScore(playerId, bankDepositScore);
                PlayerMoneyDeposited?.Invoke(playerId, amount);
            }
            else
            {
                AddTeamScore(bankDepositScore);
            }
            return true;
        }

        private void AddPlayerAndTeamScore(int playerId, int amount)
        {
            if (amount == 0)
            {
                return;
            }

            int updatedPlayerScore = GetPlayerScore(playerId) + amount;
            PlayerScoreRecord record = FindPlayerScoreRecord(playerId);
            if (record == null)
            {
                record = new PlayerScoreRecord(playerId, updatedPlayerScore);
                if (playerScores == null)
                {
                    playerScores = new List<PlayerScoreRecord>();
                }

                playerScores.Add(record);
            }
            else
            {
                record.score = updatedPlayerScore;
            }

            PlayerScoreChanged?.Invoke(playerId, updatedPlayerScore);
            AddTeamScore(amount);
        }

        private PlayerScoreRecord FindPlayerScoreRecord(int playerId)
        {
            if (playerScores == null)
            {
                return null;
            }

            for (int i = 0; i < playerScores.Count; i++)
            {
                PlayerScoreRecord record = playerScores[i];
                if (record != null && record.playerId == playerId)
                {
                    return record;
                }
            }

            return null;
        }

        private void AddTeamScore(int amount)
        {
            if (amount == 0)
            {
                return;
            }

            teamScore = Mathf.Max(0, teamScore + amount);
            TeamScoreChanged?.Invoke(teamScore);
        }

        private void SpawnReadyCustomers()
        {
            // Keep the final seconds of the wave free of new arrivals before the break.
            if (IsNewCustomerEntryClosed)
            {
                return;
            }

            int activeCustomers = CountActiveCustomers();
            if (activeCustomers >= maximumActiveCustomers)
            {
                return;
            }

            for (int i = 0; i < customerSlots.Length && activeCustomers < maximumActiveCustomers; i++)
            {
                ArenaSlot2D slot = customerSlots[i];
                if (slot == null || slot.CustomerState != CustomerSlotState.Empty ||
                    i >= nextCustomerSpawnTimes.Length || Time.time < nextCustomerSpawnTimes[i])
                {
                    continue;
                }

                SpawnCustomer(slot, i);
                nextCustomerSpawnTimes[i] = float.PositiveInfinity;
                activeCustomers++;
            }
        }

        private void SpawnCustomer(ArenaSlot2D slot, int slotIndex)
        {
            if (slot == null)
            {
                return;
            }

            bool special = SpecialMenuFood != FoodType.None &&
                UnityEngine.Random.value < perks.GetAmount(PerkEffect.SpecialMenu, 0);
            FoodType food = special ? SpecialMenuFood : foodOrderGenerator.PickRandomFood(
                customerSlots,
                slotIndex,
                foodOptions, SpecialMenuFood);
            bool paired = UnityEngine.Random.value < perks.GetAmount(PerkEffect.PairedOrders, 0);
            FoodType secondFood = paired
                ? foodOrderGenerator.PickRandomFood(customerSlots, slotIndex, foodOptions) : FoodType.None;
            bool omakase = UnityEngine.Random.value < perks.GetAmount(PerkEffect.Omakase, 0);
            int reward = UnityEngine.Random.Range(
                Mathf.Min(moneyRewardRange.x, moneyRewardRange.y),
                Mathf.Max(moneyRewardRange.x, moneyRewardRange.y) + 1);

            reward = (int)Math.Round(reward * perks.GetAmount(PerkEffect.Payment) *
                (paired ? 2 : 1) * (special ? 2 : 1), MidpointRounding.AwayFromZero);
            slot.ConfigureCustomer(food, orderTimeLimitSeconds * (float)perks.GetAmount(PerkEffect.Patience),
                reward, secondFood, omakase, special);
            CustomerRequestedFood?.Invoke(slot, food);
        }

        private void SelectSpecialMenu()
        {
            if (specialMenuWave == CurrentWaveNumber && SpecialMenuFood != FoodType.None &&
                perks.HasEffect(PerkEffect.SpecialMenu)) return;
            SpecialMenuFood = FoodType.None;
            specialMenuWave = CurrentWaveNumber;
            if (!perks.HasEffect(PerkEffect.SpecialMenu)) return;
            var menus = new List<FoodType>();
            if (foodOptions == null) return;
            foreach (FoodOption option in foodOptions)
                if (IsOrderable(option) && !menus.Contains(option.food)) menus.Add(option.food);
            if (menus.Count > 0) SpecialMenuFood = menus[UnityEngine.Random.Range(0, menus.Count)];
        }

        private void ScheduleCustomer(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= nextCustomerSpawnTimes.Length)
            {
                return;
            }

            float min = Mathf.Max(0f, Mathf.Min(customerRespawnDelaySeconds.x, customerRespawnDelaySeconds.y));
            float max = Mathf.Max(min, Mathf.Max(customerRespawnDelaySeconds.x, customerRespawnDelaySeconds.y));
            nextCustomerSpawnTimes[slotIndex] = Time.time + UnityEngine.Random.Range(min, max);
        }

        private int PickRandomEmptySlotIndex()
        {
            int emptyCount = 0;
            for (int i = 0; i < customerSlots.Length; i++)
            {
                if (customerSlots[i] != null && customerSlots[i].CustomerState == CustomerSlotState.Empty)
                {
                    emptyCount++;
                }
            }

            if (emptyCount == 0)
            {
                return -1;
            }

            int selected = UnityEngine.Random.Range(0, emptyCount);
            for (int i = 0; i < customerSlots.Length; i++)
            {
                if (customerSlots[i] == null || customerSlots[i].CustomerState != CustomerSlotState.Empty)
                {
                    continue;
                }

                if (selected-- == 0)
                {
                    return i;
                }
            }

            return -1;
        }

        private FoodOption GetFoodOption(FoodType food)
        {
            if (foodOptions == null)
            {
                return null;
            }

            for (int i = 0; i < foodOptions.Length; i++)
            {
                if (foodOptions[i] != null && foodOptions[i].food == food)
                {
                    return foodOptions[i];
                }
            }

            return null;
        }

        private static bool IsOrderable(FoodOption option)
        {
            return option != null && option.canBeOrdered && option.food >= FoodType.Food1 && option.food <= FoodType.Food5;
        }

        private int CountActiveCustomers()
        {
            int count = 0;
            for (int i = 0; i < customerSlots.Length; i++)
            {
                if (customerSlots[i] != null && customerSlots[i].HasCustomer)
                {
                    count++;
                }
            }

            return count;
        }

        private bool HasNonEmptyCustomerSlot()
        {
            for (int i = 0; i < customerSlots.Length; i++)
            {
                if (customerSlots[i] != null && customerSlots[i].CustomerState != CustomerSlotState.Empty)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasPendingCustomerSpawn()
        {
            if (nextCustomerSpawnTimes == null || nextCustomerSpawnTimes.Length != customerSlots.Length)
            {
                return false;
            }

            for (int i = 0; i < nextCustomerSpawnTimes.Length; i++)
            {
                if (!float.IsInfinity(nextCustomerSpawnTimes[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private int CountUsableCustomerSlots()
        {
            int count = 0;
            for (int i = 0; i < customerSlots.Length; i++)
            {
                if (customerSlots[i] != null)
                {
                    count++;
                }
            }

            return count;
        }

        private int IndexOfCustomerSlot(ArenaSlot2D slot)
        {
            if (customerSlots == null)
            {
                return -1;
            }

            for (int i = 0; i < customerSlots.Length; i++)
            {
                if (customerSlots[i] == slot)
                {
                    return i;
                }
            }

            return -1;
        }

        [ContextMenu("Validate Slot Layout")]
        private void ValidateSlotLayout()
        {
            if (customerSlots == null || customerSlots.Length != CustomerSlotCapacity)
            {
                Debug.LogWarning("[FoodIsekaiZ] Customer area should contain exactly C1-C6.", this);
            }

            if (stationSlots == null || stationSlots.Length != 6)
            {
                Debug.LogWarning("[FoodIsekaiZ] Station area should contain F1-F5 and one Bank.", this);
            }

            ValidateSlotTypes(customerSlots, ArenaSlotType.Customer);
            if (stationSlots == null)
            {
                return;
            }

            int foodStationCount = 0;
            int depositCount = 0;
            for (int i = 0; i < stationSlots.Length; i++)
            {
                if (stationSlots[i] == null)
                {
                    continue;
                }

                if (stationSlots[i].SlotType == ArenaSlotType.FoodStation)
                {
                    foodStationCount++;
                }
                else if (stationSlots[i].SlotType == ArenaSlotType.MoneyDeposit)
                {
                    depositCount++;
                }
            }

            if (foodStationCount != 5 || depositCount != 1)
            {
                Debug.LogWarning($"[FoodIsekaiZ] Bottom layout requires Food 5 + Bank 1 (currently {foodStationCount} + {depositCount}).", this);
            }
        }

        private static void ValidateSlotTypes(ArenaSlot2D[] slots, ArenaSlotType expectedType)
        {
            if (slots == null)
            {
                return;
            }

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && slots[i].SlotType != expectedType)
                {
                    Debug.LogWarning($"[FoodIsekaiZ] Slot '{slots[i].SlotId}' should be {expectedType}.", slots[i]);
                }
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            initialActiveCustomers = Mathf.Clamp(initialActiveCustomers, 0, CustomerSlotCapacity);
            maximumActiveCustomers = Mathf.Clamp(maximumActiveCustomers, 1, CustomerSlotCapacity);
            initialActiveCustomers = Mathf.Min(initialActiveCustomers, maximumActiveCustomers);
            orderTimeLimitSeconds = Mathf.Max(1f, orderTimeLimitSeconds);
            customerRespawnDelaySeconds.x = Mathf.Max(0f, customerRespawnDelaySeconds.x);
            customerRespawnDelaySeconds.y = Mathf.Max(customerRespawnDelaySeconds.x, customerRespawnDelaySeconds.y);
            eatingDurationSeconds = Mathf.Max(0.1f, eatingDurationSeconds);
            moneyRewardRange.x = Mathf.Max(0, moneyRewardRange.x);
            moneyRewardRange.y = Mathf.Max(0, moneyRewardRange.y);
            correctServeScore = Mathf.Max(0, correctServeScore);
            bankDepositScore = Mathf.Max(0, bankDepositScore);
            escapedCustomerPenalty = Mathf.Max(0, escapedCustomerPenalty);
            EnsureMealWaveConfiguration();
        }
#endif
    }
}
