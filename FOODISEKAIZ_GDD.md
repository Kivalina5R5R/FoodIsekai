# FoodIsekai — Game Design Document

Project: FoodIsekaiZ (Unity 6000.3.8f1, product name `FoodIsekai`, version 0.1.0)
Status: playable prototype, as of 2026-09-29
This document describes the game as it is currently built. Systems still in development are listed in section 12, open questions in section 13.

---

## 1. Overview

**FoodIsekai** is a cooperative, location-based party game for 1–4 players. Players physically walk around a floor display while wearing UWB (ultra-wideband) tracking tags. Their positions drive on-screen avatars in a fantasy tavern. Together the team serves food to fantasy-race customers across three meal services (Breakfast, Lunch, Dinner), earning score and coins, and spends coins on perks during service breaks.

| Item | Value |
|---|---|
| Genre | Co-op physical party / restaurant service |
| Players | 1–4, simultaneous, one team |
| Session length | About 6 minutes (3 × 90 s service + 2 × 30 s break + intro and results) |
| Input | UWB tags (NoopLoop), one per player; keyboard in simulation mode |
| Output | Two displays: floor (2816×1280, players walk on it) and wall (8192×2160) |
| Perspective | Floor: top-down orthographic; Wall: 2D side-view tavern |
| Setting | Isekai fantasy guild tavern run by head chef Lunar |

## 2. Player experience goals

- Physical, shouting-and-running co-op: players move their bodies to play. No buttons.
- Simple rules that can be taught by the in-game guide in under a minute.
- Shared success: one team score, one shared coin wallet, one MVP for bragging rights.
- Clear feedback on two screens: the floor tells you where to go, the wall tells you how the customers feel.

## 3. Game flow

1. **Intro** — The wall plays an intro video behind a tavern curtain. When it finishes, the curtain closes and the Ready phase is built behind it.
2. **Ready / number selection** — Four numbered floor cards (1–4) appear on the floor, centered for the number of online tags. Each participant stands alone on a card for 2 seconds to claim that player number. A card that is empty, contested by two players, or whose tag drops offline resets its hold. Lunar, the head chef, greets players on the wall and explains the rules. Play starts once every participant has a number and Lunar has walked off.
3. **Service 1: BREAKFAST** — 90 seconds of serving customers.
4. **Service Break 1** — 30 seconds. Lunar walks in and opens the perk shop with four small perks (80 coins each).
5. **Service 2: LUNCH** — 90 seconds.
6. **Service Break 2** — 30 seconds. Perk shop with four big perks (150 coins each, one purchase only).
7. **Service 3: DINNER** — 90 seconds.
8. **Service Results** — "TIME'S UP" panel with total score, per-player standings, and the MVP.

A parchment "meal menu" page sweeps over the wall between phases so state changes happen behind cover. The wall background shifts between morning, day, and night per meal, and food artwork changes per meal (morning, lunch, and dinner variants of each dish).

### Wave end and clearing

When the 90-second timer hits zero, new customers stop arriving. Customers still waiting for food leave immediately (angry, no penalty). Customers already eating finish their meal and pay. Players can still collect and bank money during this clearing period, but can no longer pick up or deliver food. The break begins once all NPCs have left the restaurant.

## 4. Play space (floor display)

The floor is an 11 × 5 world-unit arena mapped 1:1 to the physical tracked area.

```
 Top row      [T1] [T2] [T3] [T4] [T5] [T6]        6 customer tables (C1–C6)

                    (players walk here)

 Bottom row  [F1 MEAT] [F2 SEAFOOD] [F3 STARTERS] [F4 DESSERT] [F5 DRINKS] [GUILD BANK]
```

- **Customer tables (6)** — A customer's order icon and a countdown appear here. Walk in to deliver food or collect money.
- **Food stations (5)** — Meat, Seafood, Starters (hors d'oeuvre), Dessert, Drinks. Walk onto a station for a short hold (0.12 s) to pick up that dish.
- **Guild Bank (1)** — Walk in to deposit all carried coins.

Each player is represented by a numbered plate that shows the held dish, a crescent money meter (carried coins out of 50), and a ribbon arrow that always points to the nearest valid destination for what the player is holding.

## 5. Core loop

```
Pick up dish  →  Deliver matching order  →  Customer eats (3 s)  →  Money pile appears
     ↑                                                                     │
     └──────────  Deposit at Guild Bank  ←  Collect money at the table  ←──┘
```

### 5.1 Carrying rules

- A player carries either **one dish** or **coins**, never both.
- Walking onto a different food station swaps the held dish (the old one is discarded). Walking onto the station of the dish already held does nothing.
- A player carrying coins cannot pick up food until they bank.
- Collecting a money pile replaces any held food.
- Wallet cap is **50 coins**. If a pile does not fit entirely, collection is blocked, the pile stays for another player, and a reminder cue plays.

### 5.2 Delivery rules

- Delivering the **correct** dish: the customer starts eating, the player and team gain score, a success burst plays.
- Delivering the **wrong** dish: the dish is thrown away, the customer shows the "wrong food" emoji, no score change. The player must fetch a new dish.
- Arriving at a table with nothing in hand does nothing.
- Delivery only counts once the order is revealed on the wall (after the NPC has walked in and sat down).

### 5.3 Customer lifecycle

| State | What happens |
|---|---|
| Arriving | NPC walks in from the side of the wall to its table (1–2 per batch, random delay). |
| Waiting for food | Order icon and 20 s timer shown. The floor tile blinks in the last 25 %. A time-warning sound plays at 10 s and 5 s. |
| Eating | 3 s after a correct delivery. Timer palette changes. |
| Completing | Success particles on the wall. |
| Money available | A coin pile (10–20 coins, random) sits on the table until a player collects it. A rattle reminder plays periodically. |
| Expired | Timer ran out. Customer turns angry, leaves, team loses score. |

After a table empties, a new customer is scheduled after a random 2–5 s delay. Up to 6 customers can be active; all 6 tables start occupied at the beginning of each service.

**Order generation** avoids assigning the same dish to two adjacent tables and never assigns the same dish more than twice in a row, falling back only when no other option exists.

**NPC roster** — 14 fantasy customers, each with a normal and an angry pose: Dragon, Rabbit, Kwang (deer), Lama, Maow (cat), Mhee (bear), Raven, White Tiger, Wolf, Elf, Fairy, Fox, Orc, Lizardman. Within one service each prefab is drawn without replacement; across services lower-"Power" NPCs are picked first. Each NPC shows a mood bubble (Normal, Smile, or Fun by default; Love on success; Angry on expiry; a special face on wrong delivery), idle breathing, and heart particles when pleased.

## 6. Scoring and economy

Two separate resources exist:

| Resource | Earned by | Used for |
|---|---|---|
| **Score** (team + per-player) | Serving and banking | Winning, MVP, results screen |
| **Coins** (shared team wallet) | Money piles banked at the Guild Bank | Buying perks during breaks |

### Score values

| Event | Player score | Team score |
|---|---|---|
| Correct delivery | +10 | +10 |
| Bank deposit (any amount > 0) | +5 | +5 |
| Customer leaves angry (timeout) | — | −5 |

Team score never goes below 0.

### Coins

- Each completed order pays 10–20 coins into a pile at the table.
- Coins only count once banked. Carried coins are shown on the player's plate meter.
- At the start of every break, all carried coins are auto-deposited (credited to the carrying player, with the +5 bank bonus) and uncollected piles are swept into the team wallet (team gets the +5 bonus, no player credit). Any held food is discarded.
- The wallet is shown on the wall as **TEAM COINS** and on the Guild Bank as a purse that swells with balance.

### MVP

The MVP is the player with the highest individual score. Ties go to the lower player number. The MVP is shown live on the wall HUD and on the break and results panels.

## 7. Perk shop (service breaks)

During each 30-second break Lunar walks onto the wall, delivers a voiced line (English and Thai voice clips exist), and four perk cards flip open on the wall with four matching zones on the floor. The break countdown does not start until Lunar finishes speaking.

- To buy: a numbered player stands **alone** in a floor zone for **1.5 s**. A gauge fills; contested zones (two players) or a wallet too small to afford the card reset the gauge.
- Zones show **PURCHASED** / **NOT PURCHASED**.
- **Break 1 (after Breakfast): Small perks**, 80 coins each, four random cards from the pool of 10. Multiple different cards can be bought if coins allow.
- **Break 2 (after Lunch): Big perks**, 150 coins each, four random cards from the pool of 6. The shop **locks after one purchase**.
- Coins spent never affect score.

### Small perks (80 coins)

| Card | Effect (as written on the card) |
|---|---|
| Don't Be Cross with Me | Lose 5 % fewer points when customers become angry. |
| A Little Sweeter Today | ×2 multiplier on desserts. |
| Have a Sip Before You Go | ×2 multiplier on drinks. |
| So Glad You Came | Customers pay 5 % more. |
| Stay a Little Longer | Customers wait 2 % longer for their food. |
| All Your Favorites Today | Double points from meat dishes. |
| The Sea Is Feeling Generous | Double points from seafood dishes. |
| Too Good to Put the Spoon Down | Customers eat 5 % faster. |
| Start with Something Lovely | Double points from starter dishes. |
| Bringing You Something Tasty | Carry up to 2 food items at a time. |

### Big perks (150 coins)

| Card | Effect (as written on the card) |
|---|---|
| I'll Take Care of the Rest | Bank automatically collects money when customers pay. |
| An Armful of Happiness | Serve up to 3 dishes at once. |
| Make Yourself at Home | Customers collect their own drinks. |
| Let Me Pick for You Today | Customers may order Omakase; any dish satisfies them. |
| Good Things Come in Pairs | Customers can order up to 2 dishes at once; double points and money. |
| The Sky Is Clear Today | Unlock a special menu item whose customers wait indefinitely. |

See section 12.2: perk effects are in development. Purchases are recorded but not yet applied to gameplay.

## 8. Presentation

### Wall display (side view)
- Tavern interior with time-of-day backgrounds (morning, day, night) and ambient light.
- Six customer panels above the tables: NPC body, mood bubble, order icon on a parchment "BG Order", engraved brass patience/eating timer.
- Top HUD: **Score**, **Time** (mm:ss), **MVP**.
- Guild Bank purse and **TEAM COINS** readout.
- Lunar (NPC00) as guide with dialogue box for the tutorial and perk shop.
- Transitions: tavern curtain (intro), parchment meal menu (between phases) with titles BREAKFAST / LUNCH / DINNER / SERVICE BREAK / SERVICE RESULTS.
- Break panel: INTERMISSION, current score, MVP, "NEXT LUNCH/DINNER".
- Results panel: TIME'S UP, TOTAL SCORE, up to four player rows, MVP icon.

### Floor display (top-down)
- Grass/wood arena, six NPC tables (T1–T6), five food tables with dish names, Guild Bank.
- Ready cards with numbers 1–4 and a fill gauge.
- Player plate: number, held dish, coin meter, delivery arrow, pickup "flight" animation from station to plate.
- Feedback: floor bursts on pickup/delivery, coin bursts on payment/collection/deposit, table and decor fades during transitions, blinking tile for near-timeout orders, ambient sparkles.

### Audio
- Original synthesized SFX set (17 cues): food pickup, food served, wrong food, order arrived/dismissed, emoji bubble, success pop, money paid/collected, bank deposit, money reminder, customer expired, time warning, wave start, wave break, service complete.
- One looping BGM track.
- Lunar voice lines for the perk shop (English and Thai).

## 9. Tuning values (current scene)

| Parameter | Value |
|---|---|
| Services | 3 (BREAKFAST, LUNCH, DINNER) |
| Service duration | 90 s |
| Break duration | 30 s |
| Customer tables / max active | 6 / 6 |
| Initial customers per service | 6 |
| Order time limit | 20 s |
| New customer delay | 2–5 s (random) |
| Eating time | 3 s |
| Reward per order | 10–20 coins |
| Correct serve score | +10 |
| Bank deposit score | +5 |
| Angry customer penalty | −5 (team) |
| Wallet cap | 50 coins |
| Food pickup hold | 0.12 s |
| Ready card hold | 2 s |
| Perk zone hold | 1.5 s |
| Small perk price / Big perk price | 80 / 150 coins |

## 10. Controls and input

- **Live play**: one NoopLoop UWB tag per player. Tag positions arrive over serial (COM, 921600 baud) or UDP, are filtered and calibrated in `UWBConfig.json`, and mapped onto the 11 × 5 floor. Players interact purely by walking into trigger zones.
- **Player identity**: tags have no player number until the Ready phase assigns 1–4. Offline tags are hidden; the roster locks when selection starts.
- **Simulation mode** (no hardware): players spawn on the center line; tag 6 can be moved with the keyboard, others auto-drift. Debug keys: `B` adds 1000 test coins, `M` skips to the break, `N` advances to the next meal or restarts after results.

## 11. Technical summary

- Unity 6 (6000.3.8f1), C#, TextMesh Pro, Input System, Video Player.
- Dual-display standalone Windows build: Display 1 = floor (2816×1280), Display 2 = wall (8192×2160, UI authored at 1536×435 reference).
- Gameplay core (`FoodIsekaiZGameManager`, `ArenaSlot2D`, `FoodIsekaiZPlayerState`, `FoodOrderGenerator`, perk session classes) is event-driven and independent of presentation. Presentation scripts subscribe to events and only write live values (text, timers, sprites, visibility); layout and styling are authored in the scene.
- See `FOODISEKAIZ_ARCHITECTURE.md` for UWB calibration and scene setup.

## 12. Systems in development

The following systems are designed and partially built. They are actively being worked on and are not yet complete in the current build.

### 12.1 End-of-game coin bonus
- **Design intent**: Lunar's tutorial tells players to "save a few [coins] for bonus points at the end!" Coins left in the team wallet after Dinner are meant to convert into bonus score on the Service Results screen, so spending on perks is a trade-off against the final score.
- **Current state**: The dialogue is authored and the shared wallet persists through all three services, but no conversion happens at results. Total score currently equals earned service score only.
- **Remaining work**: Define the coin-to-score rate, apply it when the results phase begins, and show the bonus as its own line on the results panel.

### 12.2 Perk effects
- **Design intent**: Each purchased perk changes gameplay for the following services, as described on its card (multi-dish carrying, double orders, Omakase, special menu item, auto-bank, self-serve drinks, score/coin multipliers, timer modifiers).
- **Current state**: The shop UI, floor selection, pricing, catalog, tiering, and purchase recording are complete. Purchases are stored in the shop session with the perk ID, buyer, and the wave they apply from, but no gameplay system reads them yet. Buying a perk currently only spends coins.
- **Remaining work**: Add a perk-effect layer that reads the purchase list at the start of each service and applies modifiers to the game manager (scoring, rewards, timers) and player state (carry capacity). Some cards need new customer behavior (double orders, Omakase, special menu item, self-serve drinks).

### 12.3 Penalty for unserved customers at service end
- **Design intent**: Customers still waiting for food when the service timer ends should count as failed orders and apply the angry-customer penalty, consistent with orders that time out during play.
- **Current state**: When a service ends, waiting customers are cleared and their NPCs leave angry on the wall, but no score penalty is applied and they are not counted as expired orders.
- **Remaining work**: Route service-end clearing through the same expiry path as timeouts (count the order, subtract the penalty, fire the expiry event) or define a distinct end-of-service penalty.

## 13. Open design questions

- Perk shop uses the wave number to pick the tier, so with the default three services Break 1 is always small and Break 2 always big. Adding services would need a tier rule.
- Guide dialogue for the Ready phase is partially authored ("Hi! I'm Lunar, the head chef. I'll show you how to play! When the game starts, stand…").
- No difficulty ramp between Breakfast, Lunch, and Dinner; all three use identical timings.
- Product settings still use `DefaultCompany`.
