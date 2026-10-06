# Perk checks

Run the standalone shop, ownership and inventory tests:

```powershell
dotnet run --project Tools/PerkShopTests/PerkShopTests.csproj
```

In Unity, run **Food Isekai > Verify Perk Gameplay** to exercise the actual game components in a temporary preview scene. Results are written to `Temp/PerkGameplayVerification.txt`. **Verify Perk Visuals** renders the saved scene and player prefab to `Temp/PerkOrdersPreview.png` and `Temp/PerkFloorPreview.png`. Neither check saves or modifies the open scene.

Gameplay values live in `Assets/Scripts/Gameplay/PerkDefinitions.cs`, keyed by stable catalog IDs. Card text is presentation only. `FoodIsekaiZGameManager.Perks` owns the shared purchase history for the game, including the buyer and purchase meal; it survives shop closures and meal changes, and resets for a new game.

Select **System > PerkManager** in the scene hierarchy. Its Inspector shows **Active Perks (Team)**, the current food capacity, each perk ID, buyer, price and activation wave. The monitor reads the actual game manager and refreshes during Play Mode; it does not keep a separate copy of ownership.

`PlayerPlate.prefab` stores the compact multi-dish layout in **Plate Canvas > Held Food Left / Right / Bottom**. Two dishes use left/right; three also use bottom. A single dish uses the original full-size artwork. Runtime switches sprites and visibility without writing layout transforms.

- Small perks apply to the entire team: Longer adds five seconds to initial patience, Glad adds five coins once per order, Spoon halves eating duration, and Cross prevents angry-customer score loss. Food-score multipliers and inventory capacity retain their existing behavior.
- Happiness requires Tasty and occupies one of the four offers in the following break. The team can skip it or buy another Big perk under the existing one-Big-purchase rule.
- Plates hold one of each menu in pickup order. Correct dishes can be delivered from any slot; a completely wrong delivery discards only the oldest dish. A full perk plate rejects a new pickup. Money pickup and meal breaks clear the plate as before.
- Breakfast orders one dish; lunch orders two with 40% probability; dinner orders two with 40% and three with 20% probability. Menus within each order are distinct. Each correct dish adds five seconds to the initial twenty-second wait, credits its sender ten base points, and contributes ten to twenty base coins to the completed order. Eating starts only after all dishes arrive.
- Pairs doubles points and money for orders originally containing two or more dishes; it never changes dish-count probabilities. Three-dish orders use the authored top-one/bottom-two layout with one timer, within the existing single-card vertical bounds.
- Home credits automatic drink service to the team. Bank credits automatic deposits and the existing deposit bonus to the team.
- Omakase appears on 60% of orders and accepts any of the five menus. A question-mark graphic identifies it; the actual served dish determines food-specific score bonuses.
- Sky chooses one available menu for the meal. That menu is requested on 30% of orders, and every request for it is special: unlimited patience, double service score and double payment. Its pickup station has a rainbow frame. At closing, unserved special customers leave without an anger penalty.
- Glad's five-coin order bonus is added before Pairs and special-order payment multipliers. Without these perks, the base waiting time, payment, eating speed and anger penalty are unchanged.
