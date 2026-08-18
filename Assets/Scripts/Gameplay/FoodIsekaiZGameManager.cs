using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace FoodIsekaiZ.Gameplay
{

    public sealed class FoodIsekaiZGameManager : MonoBehaviour
    {
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

        [Header("Top Area - 4 Customer Slots")]
        [SerializeField] private ArenaSlot2D[] customerSlots = new ArenaSlot2D[4];

        [Header("Bottom Area - 5 Food + 1 Bank")]
        [SerializeField] private ArenaSlot2D[] stationSlots = new ArenaSlot2D[6];

        [Header("Meal Waves")]
        [SerializeField] private bool useMealWaves = true;
        [Tooltip("เวลาทำอาหารของแต่ละ Wave ใช้ค่าเดียวกันทั้ง BREAKFAST, LUNCH และ DINNER")]
        [InspectorName("Wave Duration (Seconds)")]
        [SerializeField, Min(1f)] private float waveDurationSeconds = 90f;
        [Tooltip("เวลาพักระหว่าง Wave ก่อนเริ่มมื้อถัดไป")]
        [InspectorName("Break Duration (Seconds)")]
        [SerializeField, Min(0f)] private float intermissionDurationSeconds = 10f;
        [Tooltip("ชื่อของแต่ละ Wave เรียงตามลำดับการเล่น")]
        [SerializeField] private MealWaveDefinition[] mealWaves =
        {
            new MealWaveDefinition("BREAKFAST"),
            new MealWaveDefinition("LUNCH"),
            new MealWaveDefinition("DINNER")
        };

        [Header("Customer Spawning")]
        [SerializeField] private bool startCustomersOnPlay = true;
        [SerializeField, Range(0, 4)] private int initialActiveCustomers = 4;
        [SerializeField, Range(1, 4)] private int maximumActiveCustomers = 4;

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
        [SerializeField, Min(0)] private int completedOrderCount;
        [SerializeField, Min(0)] private int expiredOrderCount;
        [SerializeField] private List<PlayerScoreRecord> playerScores = new List<PlayerScoreRecord>();
        [SerializeField] private MealWavePhase mealWavePhase = MealWavePhase.NotStarted;
        [SerializeField] private int currentWaveIndex = -1;
        [SerializeField, Min(0f)] private float mealPhaseRemainingSeconds;

        private float[] nextCustomerSpawnTimes = Array.Empty<float>();
        private bool customerFlowStarted;
        private bool mealWaveFlowStarted;
        private int lastNotifiedMealSecond = int.MinValue;

        public int TeamScore => teamScore;
        public int CompletedOrderCount => completedOrderCount;
        public int ExpiredOrderCount => expiredOrderCount;
        public IReadOnlyList<ArenaSlot2D> CustomerSlots => customerSlots;
        public bool UsesMealWaves => useMealWaves;
        public MealWavePhase CurrentMealWavePhase => mealWavePhase;
        public int CurrentWaveNumber => currentWaveIndex >= 0 ? currentWaveIndex + 1 : 0;
        public int TotalWaveCount => mealWaves != null ? mealWaves.Length : 0;
        public float MealPhaseRemainingSeconds => mealPhaseRemainingSeconds;
        public string CurrentWaveName => GetWaveDisplayName(currentWaveIndex);
        public string NextWaveName => GetWaveDisplayName(currentWaveIndex + 1);

        public event Action<int, int> PlayerMoneyDeposited;
        public event Action<int, int> PlayerScoreChanged;
        public event Action<int> TeamScoreChanged;
        public event Action<ArenaSlot2D, FoodType> CustomerRequestedFood;
        public event Action<ArenaSlot2D, int> CustomerMoneySpawned;
        public event Action<ArenaSlot2D> CustomerOrderExpired;
        public event Action MealWaveDisplayChanged;

        private void Start()
        {
            ValidateSlotLayout();
            if (!startCustomersOnPlay)
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

        public void EnsureCustomerFlowStarted()
        {
            if (!Application.isPlaying || !startCustomersOnPlay)
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
            if (useMealWaves && mealWaveFlowStarted)
            {
                TickMealWave(Time.deltaTime);
                if (mealWavePhase != MealWavePhase.Active)
                {
                    return;
                }
            }

            if (!customerFlowStarted)
            {
                return;
            }

            TickCustomerStates(Time.deltaTime);
            SpawnReadyCustomers();
        }

        [ContextMenu("Start / Restart 3 Meal Waves")]
        public void StartMealWaveFlow()
        {
            EnsureMealWaveConfiguration();
            mealWaveFlowStarted = true;
            currentWaveIndex = 0;
            BeginCurrentWave();
        }

        [ContextMenu("Start / Restart Customer Flow")]
        public void StartCustomerFlow()
        {
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
            int initialCount = Mathf.Min(initialActiveCustomers, maximumActiveCustomers, CountUsableCustomerSlots());
            for (int i = 0; i < initialCount; i++)
            {
                int slotIndex = PickRandomEmptySlotIndex();
                if (slotIndex >= 0)
                {
                    SpawnCustomer(customerSlots[slotIndex]);
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

        public bool TryInteract(FoodIsekaiZPlayerState player, ArenaSlot2D slot)
        {
            if (player == null || slot == null)
            {
                return false;
            }

            if (useMealWaves &&
                mealWavePhase != MealWavePhase.Active &&
                mealWavePhase != MealWavePhase.Intermission)
            {
                return false;
            }

            switch (slot.SlotType)
            {
                case ArenaSlotType.FoodStation:
                    return player.TryPickFood(slot.StationFood);

                case ArenaSlotType.MoneyDeposit:
                    return TryDepositMoney(player);

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

            mealWavePhase = MealWavePhase.Active;
            mealPhaseRemainingSeconds = Mathf.Max(1f, waveDurationSeconds);
            lastNotifiedMealSecond = int.MinValue;
            StartCustomerFlow();
            NotifyMealWaveDisplayIfNeeded(true);
        }

        private void EndCurrentWave()
        {
            if (currentWaveIndex >= TotalWaveCount - 1)
            {
                CompleteMealWaves();
                return;
            }

            PauseCustomerFlowAndKeepAvailableMoney();
            mealWavePhase = MealWavePhase.Intermission;
            mealPhaseRemainingSeconds = Mathf.Max(0f, intermissionDurationSeconds);
            lastNotifiedMealSecond = int.MinValue;
            NotifyMealWaveDisplayIfNeeded(true);
            if (mealPhaseRemainingSeconds <= 0f)
            {
                currentWaveIndex++;
                BeginCurrentWave();
            }
        }

        private void CompleteMealWaves()
        {
            StopCustomerFlowAndClearSlots();
            mealWavePhase = MealWavePhase.Completed;
            mealPhaseRemainingSeconds = 0f;
            lastNotifiedMealSecond = int.MinValue;
            NotifyMealWaveDisplayIfNeeded(true);
        }

        private void PauseCustomerFlowAndKeepAvailableMoney()
        {
            customerFlowStarted = false;
            if (customerSlots != null)
            {
                for (int i = 0; i < customerSlots.Length; i++)
                {
                    ArenaSlot2D slot = customerSlots[i];
                    if (slot != null && slot.CustomerState != CustomerSlotState.MoneyAvailable)
                    {
                        slot.ClearCustomer();
                    }
                }
            }

            nextCustomerSpawnTimes = Array.Empty<float>();
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
                if (slot == null || !slot.AdvanceStateTimer(deltaTime))
                {
                    continue;
                }

                if (slot.CustomerState == CustomerSlotState.WaitingForFood)
                {
                    expiredOrderCount++;
                    AddTeamScore(-escapedCustomerPenalty);
                    CustomerOrderExpired?.Invoke(slot);
                    slot.ClearCustomer();
                    ScheduleCustomer(i);
                }
                else if (slot.CustomerState == CustomerSlotState.Eating)
                {
                    int reward = slot.OrderReward;
                    slot.SpawnMoney(reward);
                    completedOrderCount++;
                    CustomerMoneySpawned?.Invoke(slot, reward);
                }
            }
        }

        private bool TryInteractWithCustomer(FoodIsekaiZPlayerState player, ArenaSlot2D slot)
        {
            if (slot.CustomerState == CustomerSlotState.WaitingForFood)
            {
                if (player.HeldFood == FoodType.None)
                {
                    return false;
                }

                if (player.HeldFood != slot.RequestedFood)
                {
                    return player.TryDiscardHeldFood();
                }

                if (!slot.TryBeginEating(eatingDurationSeconds) ||
                    !player.TryConsumeFood(slot.RequestedFood))
                {
                    return false;
                }

                AddPlayerAndTeamScore(player.PlayerId, correctServeScore);
                return true;
            }

            if (slot.CustomerState != CustomerSlotState.MoneyAvailable)
            {
                return false;
            }

            int collected = slot.CollectMoney();
            if (collected <= 0)
            {
                return false;
            }

            player.AddMoney(collected);
            ScheduleCustomer(IndexOfCustomerSlot(slot));
            return true;
        }

        private bool TryDepositMoney(FoodIsekaiZPlayerState player)
        {
            int deposited = player.DepositAllMoney();
            if (deposited <= 0)
            {
                return false;
            }

            AddPlayerAndTeamScore(player.PlayerId, bankDepositScore);
            PlayerMoneyDeposited?.Invoke(player.PlayerId, deposited);
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

                SpawnCustomer(slot);
                nextCustomerSpawnTimes[i] = float.PositiveInfinity;
                activeCustomers++;
            }
        }

        private void SpawnCustomer(ArenaSlot2D slot)
        {
            if (slot == null)
            {
                return;
            }

            FoodType food = PickRandomFood();
            int reward = UnityEngine.Random.Range(
                Mathf.Min(moneyRewardRange.x, moneyRewardRange.y),
                Mathf.Max(moneyRewardRange.x, moneyRewardRange.y) + 1);

            slot.ConfigureCustomer(food, orderTimeLimitSeconds, reward);
            CustomerRequestedFood?.Invoke(slot, food);
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

        private FoodType PickRandomFood()
        {
            int enabledCount = 0;
            if (foodOptions != null)
            {
                for (int i = 0; i < foodOptions.Length; i++)
                {
                    if (IsOrderable(foodOptions[i]))
                    {
                        enabledCount++;
                    }
                }
            }

            if (enabledCount == 0)
            {
                return (FoodType)UnityEngine.Random.Range((int)FoodType.Food1, (int)FoodType.Food5 + 1);
            }

            int selected = UnityEngine.Random.Range(0, enabledCount);
            for (int i = 0; i < foodOptions.Length; i++)
            {
                if (IsOrderable(foodOptions[i]) && selected-- == 0)
                {
                    return foodOptions[i].food;
                }
            }

            return FoodType.Food1;
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
            if (customerSlots == null || customerSlots.Length != 4)
            {
                Debug.LogWarning("[FoodIsekaiZ] Customer area should contain exactly C1-C4.", this);
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
            initialActiveCustomers = Mathf.Clamp(initialActiveCustomers, 0, 4);
            maximumActiveCustomers = Mathf.Clamp(maximumActiveCustomers, 1, 4);
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
