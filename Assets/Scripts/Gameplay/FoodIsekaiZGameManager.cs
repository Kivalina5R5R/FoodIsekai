using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FoodIsekaiZ.Gameplay
{
    /// <summary>
    /// กติกาหลักของ Arena: หยิบอาหาร -> ส่งลูกค้า -> รอกิน -> เก็บเงิน -> ฝากเงิน
    /// Slot และ Player เก็บ state ของตัวเอง ส่วน manager เป็นผู้ตัดสิน interaction
    /// </summary>
    public sealed class FoodIsekaiZGameManager : MonoBehaviour
    {
        [Header("Top Area - 4 Customer Slots")]
        [SerializeField] private ArenaSlot2D[] customerSlots = new ArenaSlot2D[4];

        [Header("Bottom Area - 5 Food + 1 Deposit")]
        [SerializeField] private ArenaSlot2D[] stationSlots = new ArenaSlot2D[6];

        [Header("Customer Rules")]
        [SerializeField, Min(0.1f)] private float eatingDurationSeconds = 3f;
        [SerializeField, Min(0)] private int moneyPerOrder = 10;

        [Header("Runtime (Read Only)")]
        [SerializeField, Min(0)] private int teamBankedMoney;

        private readonly Dictionary<int, int> bankedMoneyByPlayer = new Dictionary<int, int>();

        public int TeamBankedMoney => teamBankedMoney;

        public event Action<int, int> PlayerMoneyDeposited;
        public event Action<ArenaSlot2D, FoodType> CustomerRequestedFood;
        public event Action<ArenaSlot2D, int> CustomerMoneySpawned;

        private void Start()
        {
            ValidateSlotLayout();
            for (int i = 0; i < customerSlots.Length; i++)
            {
                if (customerSlots[i] != null)
                {
                    AssignNextCustomerOrder(customerSlots[i]);
                }
            }
        }

        /// <summary>จุดเข้าเดียวของทุก interaction เรียกได้ทั้งจาก Trigger และ proximity system ภายนอก</summary>
        public bool TryInteract(FoodIsekaiZPlayerState player, ArenaSlot2D slot)
        {
            if (player == null || slot == null)
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

        public int GetPlayerBankedMoney(int playerId)
        {
            return bankedMoneyByPlayer.TryGetValue(playerId, out int amount) ? amount : 0;
        }

        /// <summary>รับ Slot ที่ Arena Layout สร้าง เพื่อไม่ต้องลาก array 10 ช่องด้วยมือ</summary>
        public void ConfigureSlots(ArenaSlot2D[] newCustomerSlots, ArenaSlot2D[] newStationSlots)
        {
            customerSlots = newCustomerSlots ?? Array.Empty<ArenaSlot2D>();
            stationSlots = newStationSlots ?? Array.Empty<ArenaSlot2D>();
        }

        private bool TryInteractWithCustomer(FoodIsekaiZPlayerState player, ArenaSlot2D slot)
        {
            if (slot.CustomerState == CustomerSlotState.WaitingForFood)
            {
                if (!player.TryConsumeFood(slot.RequestedFood) || !slot.TryBeginEating())
                {
                    return false;
                }

                StartCoroutine(FinishEating(slot));
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
            AssignNextCustomerOrder(slot);
            return true;
        }

        private IEnumerator FinishEating(ArenaSlot2D slot)
        {
            yield return new WaitForSeconds(eatingDurationSeconds);
            if (slot == null || slot.CustomerState != CustomerSlotState.Eating)
            {
                yield break;
            }

            slot.SpawnMoney(moneyPerOrder);
            CustomerMoneySpawned?.Invoke(slot, moneyPerOrder);
        }

        private bool TryDepositMoney(FoodIsekaiZPlayerState player)
        {
            int deposited = player.DepositAllMoney();
            if (deposited <= 0)
            {
                return false;
            }

            teamBankedMoney += deposited;
            bankedMoneyByPlayer[player.PlayerId] = GetPlayerBankedMoney(player.PlayerId) + deposited;
            PlayerMoneyDeposited?.Invoke(player.PlayerId, deposited);
            return true;
        }

        private void AssignNextCustomerOrder(ArenaSlot2D slot)
        {
            FoodType food = (FoodType)UnityEngine.Random.Range((int)FoodType.Food1, (int)FoodType.Food5 + 1);
            slot.ConfigureCustomer(food);
            CustomerRequestedFood?.Invoke(slot, food);
        }

        [ContextMenu("Validate Slot Layout")]
        private void ValidateSlotLayout()
        {
            if (customerSlots == null || customerSlots.Length != 4)
            {
                Debug.LogWarning("[FoodIsekaiZ] ควรกำหนด Customer Slots ด้านบนให้ครบ 4 ช่อง", this);
            }

            if (stationSlots == null || stationSlots.Length != 6)
            {
                Debug.LogWarning("[FoodIsekaiZ] ควรกำหนด Station Slots ด้านล่างให้ครบ 6 ช่อง", this);
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
                Debug.LogWarning($"[FoodIsekaiZ] Bottom layout ต้องเป็น Food 5 + Deposit 1 (ปัจจุบัน {foodStationCount} + {depositCount})", this);
            }
        }

        private void ValidateSlotTypes(ArenaSlot2D[] slots, ArenaSlotType expectedType)
        {
            if (slots == null)
            {
                return;
            }

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && slots[i].SlotType != expectedType)
                {
                    Debug.LogWarning($"[FoodIsekaiZ] Slot '{slots[i].SlotId}' ควรเป็น {expectedType}", slots[i]);
                }
            }
        }
    }
}
