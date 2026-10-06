using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace FoodIsekaiZ.Gameplay
{
    // Writes each play session to its own Markdown file in StreamingAssets/PlayerLog, named by the date and time
    // the session started (for example 2026-10-06_10-41-20.md). The file is rewritten after every recorded moment,
    // so an interrupted session still leaves its record.
    public static class PlayerSessionLog
    {
        public const string FolderName = "PlayerLog";
        private const string FileNameFormat = "yyyy-MM-dd_HH-mm-ss";
        private const int FoodCount = 5;

        private sealed class MealRecord
        {
            public string name;
            public int successfulOrders;
            public int angryCustomers;
            public int wrongFood;
            public int customerPayments;
            public int bankDeposits;
        }

        private sealed class CustomerRecord
        {
            public DateTime arrivedAt;
            public string mealName;
            public string slotId;
            public string npcName;
            public string order;
            // Identifies the customer while it is still at its slot, so its result lands on the right row.
            public ArenaSlot2D slot;
            public int generation;
            public string outcome;
            public string servedBy;
            public float waitedSeconds = -1f;
        }

        private sealed class MoneyRecord
        {
            public string moment;
            public int money;
        }

        private sealed class PerkRecord
        {
            public string beforeMeal;
            public string perkId;
            public bool bigShop;
            public int price;
            public int playerId;
            public int moneyLeft;
        }

        private sealed class PlayerRecord
        {
            public int dishesServed;
            public int ordersCompleted;
            public int wrongFood;
            public int perksBought;
            public int score;
        }

        private sealed class Session
        {
            public DateTime startedAt;
            public DateTime? endedAt;
            public int readyPlayerCount;
            public string filePath;
            public readonly List<MealRecord> meals = new List<MealRecord>();
            public readonly List<MoneyRecord> money = new List<MoneyRecord>();
            public readonly List<CustomerRecord> customers = new List<CustomerRecord>();
            public readonly List<PerkRecord> perks = new List<PerkRecord>();
            public readonly SortedDictionary<int, PlayerRecord> players = new SortedDictionary<int, PlayerRecord>();
            public readonly int[] foodOrdered = new int[FoodCount];
            public readonly int[] foodServed = new int[FoodCount];
            // Running totals for the meal in progress; RecordMeal moves them into that meal's row.
            public int mealWrongFood;
            public int mealCustomerPayments;
            public int mealBankDeposits;
            public bool hasFinalScore;
            public int scoreBeforeBonus;
            public int leftoverMoneyBonus;
            public int finalScore;
            public int mvpPlayerId;
        }

        private static Session current;

        // Off only for simulation tests that have not opted in; real builds always record.
        public static bool RecordingEnabled { get; set; } = true;

        // Lets an editor check write to a scratch file instead of the operators' PlayerLog.md.
        public static string FilePathOverride { get; set; }

        // The file of the most recently started session, kept after the session ends.
        public static string LastFilePath { get; private set; }

        // Clears a session left over from an earlier play when the editor keeps static state between plays.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            current = null;
            RecordingEnabled = true;
            FilePathOverride = null;
        }

        // Starts a new log once the ready phase has confirmed every participant.
        public static void BeginSession(int readyPlayerCount)
        {
            if (!RecordingEnabled)
            {
                current = null;
                return;
            }

            DateTime now = DateTime.Now;
            string filePath = FilePathOverride;
            if (string.IsNullOrEmpty(filePath))
            {
                string folder = Path.Combine(Application.streamingAssetsPath, FolderName);
                string name = now.ToString(FileNameFormat, CultureInfo.InvariantCulture);
                filePath = Path.Combine(folder, name + ".md");
                // Two sessions started within the same second keep separate files.
                for (int copy = 2; File.Exists(filePath); copy++)
                    filePath = Path.Combine(folder, $"{name}_{copy}.md");
            }

            current = new Session
            {
                startedAt = now,
                readyPlayerCount = Mathf.Max(0, readyPlayerCount),
                filePath = filePath
            };
            LastFilePath = filePath;
            Write();
        }

        // Used when a round starts without a ready phase; the count comes from the players in the scene.
        public static void EnsureSession(int fallbackPlayerCount)
        {
            if (current == null) BeginSession(fallbackPlayerCount);
        }

        // Successful orders are fully delivered orders; angry customers left before receiving their food.
        // Customers still without a result when the meal closes are marked as left at the end of the meal.
        public static void RecordMeal(string mealName, int successfulOrders, int angryCustomers)
        {
            if (current == null) return;
            current.meals.Add(new MealRecord
            {
                name = string.IsNullOrWhiteSpace(mealName) ? $"MEAL {current.meals.Count + 1}" : mealName,
                successfulOrders = Mathf.Max(0, successfulOrders),
                angryCustomers = Mathf.Max(0, angryCustomers),
                wrongFood = current.mealWrongFood,
                customerPayments = current.mealCustomerPayments,
                bankDeposits = current.mealBankDeposits
            });
            current.mealWrongFood = 0;
            current.mealCustomerPayments = 0;
            current.mealBankDeposits = 0;
            foreach (CustomerRecord customer in current.customers)
            {
                if (customer.outcome != null) continue;
                customer.outcome = "หมดมื้อ (ไม่ได้รับอาหาร)";
                customer.slot = null;
            }
            Write();
        }

        // One row per NPC that walks into the restaurant, with the dishes its customer slot ordered.
        public static void RecordCustomer(string mealName, string slotId, string npcName, ArenaSlot2D slot)
        {
            if (current == null) return;
            current.customers.Add(new CustomerRecord
            {
                arrivedAt = DateTime.Now,
                mealName = string.IsNullOrWhiteSpace(mealName) ? "-" : mealName,
                slotId = string.IsNullOrWhiteSpace(slotId) ? "-" : slotId,
                npcName = string.IsNullOrWhiteSpace(npcName) ? "-" : npcName,
                order = DescribeOrder(slot),
                slot = slot,
                generation = slot != null ? slot.CustomerGeneration : 0
            });
            // Omakase accepts any dish, so only named menus count toward how often a menu was ordered.
            if (slot != null && !slot.IsOmakase)
                foreach (FoodType food in slot.RemainingFoods)
                    if (food >= FoodType.Food1 && food <= FoodType.Food5) current.foodOrdered[(int)food - 1]++;
            Write();
        }

        // Called for every accepted dish; a player ID of zero means an automatic perk served it.
        public static void RecordDishServed(int playerId, FoodType food)
        {
            if (current == null) return;
            if (food >= FoodType.Food1 && food <= FoodType.Food5) current.foodServed[(int)food - 1]++;
            if (playerId > 0) GetPlayer(playerId).dishesServed++;
        }

        // Called once the customer's whole order has been delivered and it starts eating.
        public static void RecordCustomerServed(ArenaSlot2D slot, int playerId, float waitedSeconds)
        {
            if (current == null) return;
            if (playerId > 0) GetPlayer(playerId).ordersCompleted++;
            CustomerRecord customer = FindOpenCustomer(slot);
            if (customer != null)
            {
                customer.outcome = "ได้กิน";
                customer.servedBy = playerId > 0 ? $"P{playerId}" : "อัตโนมัติ";
                customer.waitedSeconds = Mathf.Max(0f, waitedSeconds);
                customer.slot = null;
            }
            Write();
        }

        // Called when a customer leaves angry, either from running out of patience or at the end of the meal.
        public static void RecordCustomerAngry(ArenaSlot2D slot, float waitedSeconds)
        {
            if (current == null) return;
            CustomerRecord customer = FindOpenCustomer(slot);
            if (customer == null) return;
            customer.outcome = "โกรธออก";
            customer.waitedSeconds = Mathf.Max(0f, waitedSeconds);
            customer.slot = null;
            Write();
        }

        // A dish handed to a customer who did not order it, which the game throws away.
        public static void RecordWrongFood(int playerId)
        {
            if (current == null) return;
            current.mealWrongFood++;
            if (playerId > 0) GetPlayer(playerId).wrongFood++;
        }

        // Money a customer leaves after eating, before anyone carries it to the bank.
        public static void RecordCustomerPayment(int amount)
        {
            if (current != null && amount > 0) current.mealCustomerPayments += amount;
        }

        // Money that reaches the team bank, by a player, an automatic perk or the break settlement.
        public static void RecordBankDeposit(int amount)
        {
            if (current != null && amount > 0) current.mealBankDeposits += amount;
        }

        // A successful purchase in the break shop, with the team money left after paying for it.
        public static void RecordPerkPurchase(string perkId, int price, int playerId, string nextMealName,
            bool bigShop, int moneyLeft)
        {
            if (current == null) return;
            current.perks.Add(new PerkRecord
            {
                beforeMeal = string.IsNullOrWhiteSpace(nextMealName) ? "-" : nextMealName,
                perkId = string.IsNullOrWhiteSpace(perkId) ? "-" : perkId,
                bigShop = bigShop,
                price = price,
                playerId = playerId,
                moneyLeft = moneyLeft
            });
            if (playerId > 0) GetPlayer(playerId).perksBought++;
            Write();
        }

        // Names match the food prefabs F1-Meat through F5-Drink so the log reads like the menu.
        private static string GetFoodLogName(FoodType food)
        {
            switch (food)
            {
                case FoodType.Food1: return "F1-Meat";
                case FoodType.Food2: return "F2-Seafood";
                case FoodType.Food3: return "F3-Hors d'oeuvre";
                case FoodType.Food4: return "F4-Dessert";
                case FoodType.Food5: return "F5-Drink";
                default: return "-";
            }
        }

        // An omakase order accepts any dish, so it is written by dish count instead of by menu.
        private static string DescribeOrder(ArenaSlot2D slot)
        {
            if (slot == null || slot.RemainingFoods.Count == 0) return "-";
            string dishes;
            if (slot.IsOmakase)
            {
                dishes = $"Omakase (อะไรก็ได้ {slot.RemainingFoods.Count} จาน)";
            }
            else
            {
                var names = new List<string>(slot.RemainingFoods.Count);
                foreach (FoodType food in slot.RemainingFoods) names.Add(GetFoodLogName(food));
                dishes = string.Join(" + ", names);
            }
            return slot.IsSpecialOrder ? $"{dishes} (เมนูพิเศษ)" : dishes;
        }

        private static CustomerRecord FindOpenCustomer(ArenaSlot2D slot)
        {
            if (slot == null) return null;
            for (int i = current.customers.Count - 1; i >= 0; i--)
            {
                CustomerRecord customer = current.customers[i];
                if (customer.outcome == null && customer.slot == slot &&
                    customer.generation == slot.CustomerGeneration) return customer;
            }
            return null;
        }

        private static PlayerRecord GetPlayer(int playerId)
        {
            if (!current.players.TryGetValue(playerId, out PlayerRecord player))
            {
                player = new PlayerRecord();
                current.players.Add(playerId, player);
            }
            return player;
        }

        // Team money after the finished meal's outstanding coins were settled at the start of the break.
        public static void RecordBreakMoney(string finishedMealName, int teamMoney)
        {
            if (current == null) return;
            int breakNumber = 1;
            foreach (MoneyRecord record in current.money)
                if (record.moment.StartsWith("พัก", StringComparison.Ordinal)) breakNumber++;
            string after = string.IsNullOrWhiteSpace(finishedMealName) ? string.Empty : $" (หลัง {finishedMealName})";
            current.money.Add(new MoneyRecord { moment = $"พักครั้งที่ {breakNumber}{after}", money = teamMoney });
            Write();
        }

        // Each player's own score at the end of the game; the MVP ID is zero when nobody scored.
        public static void RecordPlayerScore(int playerId, int score)
        {
            if (current == null || playerId <= 0) return;
            GetPlayer(playerId).score = score;
        }

        // Team money is taken before the leftover coins are converted into the final score bonus.
        public static void RecordGameEnd(int teamMoney, int scoreBeforeBonus, int leftoverMoneyBonus, int finalScore,
            int mvpPlayerId)
        {
            if (current == null) return;
            current.money.Add(new MoneyRecord { moment = "จบเกม", money = teamMoney });
            current.hasFinalScore = true;
            current.scoreBeforeBonus = scoreBeforeBonus;
            current.leftoverMoneyBonus = leftoverMoneyBonus;
            current.finalScore = finalScore;
            current.mvpPlayerId = mvpPlayerId;
            current.endedAt = DateTime.Now;
            Write();
            current = null;
        }

        private static void Write()
        {
            Session session = current;
            if (session == null) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(session.filePath));
                File.WriteAllText(session.filePath, BuildMarkdown(session), new UTF8Encoding(false));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[PlayerSessionLog] Could not write {session.filePath}. {exception.Message}");
            }
        }

        private static string BuildMarkdown(Session session)
        {
            const string timeFormat = "yyyy-MM-dd HH:mm:ss";
            var text = new StringBuilder();
            text.AppendLine("# FoodIsekaiZ Player Log");
            text.AppendLine();
            text.AppendLine($"- เริ่มเล่น: {session.startedAt.ToString(timeFormat, CultureInfo.InvariantCulture)}");
            text.AppendLine(session.endedAt.HasValue
                ? $"- จบเกม: {session.endedAt.Value.ToString(timeFormat, CultureInfo.InvariantCulture)}"
                : "- จบเกม: ยังไม่จบ");
            text.AppendLine($"- จำนวนผู้เล่นตอน Ready: {session.readyPlayerCount}");
            text.AppendLine();

            text.AppendLine("## สรุปแยกตามมื้อ");
            text.AppendLine();
            text.AppendLine("| มื้อ | ออเดอร์สำเร็จ | ลูกค้าโกรธออก | ส่งอาหารผิด | เงินที่ลูกค้าจ่าย | เงินเข้าธนาคาร |");
            text.AppendLine("|---|---:|---:|---:|---:|---:|");
            var total = new MealRecord();
            foreach (MealRecord meal in session.meals)
            {
                text.AppendLine($"| {meal.name} | {meal.successfulOrders} | {meal.angryCustomers} | {meal.wrongFood} | " +
                    $"{meal.customerPayments} | {meal.bankDeposits} |");
                total.successfulOrders += meal.successfulOrders;
                total.angryCustomers += meal.angryCustomers;
                total.wrongFood += meal.wrongFood;
                total.customerPayments += meal.customerPayments;
                total.bankDeposits += meal.bankDeposits;
            }
            text.AppendLine($"| **รวม** | **{total.successfulOrders}** | **{total.angryCustomers}** | " +
                $"**{total.wrongFood}** | **{total.customerPayments}** | **{total.bankDeposits}** |");
            text.AppendLine();

            text.AppendLine("## ลูกค้าที่เข้าร้าน");
            text.AppendLine();
            text.AppendLine("| # | เวลา | มื้อ | ช่อง | NPC | สั่ง | ผล | เสิร์ฟโดย | รอ (วิ) |");
            text.AppendLine("|---:|---|---|---|---|---|---|---|---:|");
            for (int i = 0; i < session.customers.Count; i++)
            {
                CustomerRecord customer = session.customers[i];
                string time = customer.arrivedAt.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
                string waited = customer.waitedSeconds >= 0f
                    ? customer.waitedSeconds.ToString("0.0", CultureInfo.InvariantCulture)
                    : "-";
                text.AppendLine($"| {i + 1} | {time} | {customer.mealName} | {customer.slotId} | {customer.npcName} | " +
                    $"{customer.order} | {customer.outcome ?? "กำลังรอ"} | {customer.servedBy ?? "-"} | {waited} |");
            }
            text.AppendLine();

            text.AppendLine("## เมนูยอดนิยม");
            text.AppendLine();
            text.AppendLine("| อันดับ | เมนู | ถูกสั่ง | เสิร์ฟสำเร็จ (จาน) |");
            text.AppendLine("|---:|---|---:|---:|");
            var foodOrder = new List<int> { 0, 1, 2, 3, 4 };
            foodOrder.Sort((left, right) =>
            {
                int byOrdered = session.foodOrdered[right].CompareTo(session.foodOrdered[left]);
                if (byOrdered != 0) return byOrdered;
                int byServed = session.foodServed[right].CompareTo(session.foodServed[left]);
                return byServed != 0 ? byServed : left.CompareTo(right);
            });
            for (int rank = 0; rank < foodOrder.Count; rank++)
            {
                int index = foodOrder[rank];
                text.AppendLine($"| {rank + 1} | {GetFoodLogName((FoodType)(index + 1))} | " +
                    $"{session.foodOrdered[index]} | {session.foodServed[index]} |");
            }
            text.AppendLine();

            text.AppendLine("## Perk ที่ซื้อตอนพัก");
            text.AppendLine();
            if (session.perks.Count == 0)
            {
                text.AppendLine("- ไม่ได้ซื้อ Perk");
            }
            else
            {
                text.AppendLine("| ก่อนมื้อ | ร้าน | Perk | ราคา | ผู้ซื้อ | เงินทีมที่เหลือ |");
                text.AppendLine("|---|---|---|---:|---|---:|");
                foreach (PerkRecord perk in session.perks)
                {
                    string buyer = perk.playerId > 0 ? $"P{perk.playerId}" : "-";
                    text.AppendLine($"| {perk.beforeMeal} | {(perk.bigShop ? "Big" : "Small")} | {perk.perkId} | " +
                        $"{perk.price} | {buyer} | {perk.moneyLeft} |");
                }
            }
            text.AppendLine();

            text.AppendLine("## เงินทีม");
            text.AppendLine();
            text.AppendLine("| ช่วงเวลา | เงินทีม |");
            text.AppendLine("|---|---:|");
            foreach (MoneyRecord record in session.money)
                text.AppendLine($"| {record.moment} | {record.money} |");
            text.AppendLine();

            text.AppendLine("## ผู้เล่น");
            text.AppendLine();
            if (session.players.Count == 0)
            {
                text.AppendLine("- ยังไม่มีข้อมูลผู้เล่น");
            }
            else
            {
                text.AppendLine("| ผู้เล่น | คะแนน | ออเดอร์ที่ส่งครบ | จานที่เสิร์ฟ | ส่งอาหารผิด | Perk ที่ซื้อ |");
                text.AppendLine("|---|---:|---:|---:|---:|---:|");
                foreach (KeyValuePair<int, PlayerRecord> entry in session.players)
                {
                    PlayerRecord player = entry.Value;
                    string name = entry.Key == session.mvpPlayerId ? $"**P{entry.Key} (MVP)**" : $"P{entry.Key}";
                    string score = session.hasFinalScore ? player.score.ToString(CultureInfo.InvariantCulture) : "-";
                    text.AppendLine($"| {name} | {score} | {player.ordersCompleted} | {player.dishesServed} | " +
                        $"{player.wrongFood} | {player.perksBought} |");
                }
            }
            text.AppendLine();

            text.AppendLine("## คะแนนตอนจบเกม");
            text.AppendLine();
            if (session.hasFinalScore)
            {
                text.AppendLine($"- คะแนนก่อนโบนัส: {session.scoreBeforeBonus}");
                text.AppendLine($"- เหรียญที่เหลือ {session.money[session.money.Count - 1].money} = โบนัส +{session.leftoverMoneyBonus}");
                text.AppendLine($"- **คะแนนรวมโบนัสแล้ว: {session.finalScore}**");
                text.AppendLine(session.mvpPlayerId > 0 ? $"- MVP: P{session.mvpPlayerId}" : "- MVP: -");
            }
            else
            {
                text.AppendLine("- ยังไม่จบเกม");
            }
            return text.ToString();
        }
    }
}
