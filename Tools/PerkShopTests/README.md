# Perk checks

Run the standalone shop, ownership and inventory tests:

```powershell
dotnet run --project Tools/PerkShopTests/PerkShopTests.csproj
```

In Unity, run **Food Isekai > Verify Perk Gameplay** to exercise the actual game components in a temporary preview scene. Results are written to `Temp/PerkGameplayVerification.txt`. **Verify Perk Visuals** renders the saved scene and player prefab to `Temp/PerkOrdersPreview.png` and `Temp/PerkFloorPreview.png`. Neither check saves or modifies the open scene.

Gameplay values live in `Assets/Scripts/Gameplay/PerkDefinitions.cs`, keyed by stable catalog IDs. Card text is presentation only. `FoodIsekaiZGameManager.Perks` owns the shared purchase history for the game, including the buyer and purchase meal; it survives shop closures and meal changes, and resets for a new game.

Select **System > PerkManager** in the scene hierarchy. Its Inspector shows **Active Perks (Team)**, the current food capacity, each perk ID, buyer, price and activation wave. The monitor reads the actual game manager and refreshes during Play Mode; it does not keep a separate copy of ownership.

`PlayerPlate.prefab` stores the compact multi-dish layout in **Plate Canvas > Held Food Left / Right / Bottom**. Two dishes use left/right; three also use bottom. A single dish uses the original full-size artwork. Runtime switches sprites and visibility without writing layout transforms.

- Small perks apply their food-score multiplier, payment multiplier, patience multiplier, eating speed, penalty reduction or capacity to the entire team.
- Happiness requires Tasty and occupies one of the four offers in the following break. The team can skip it or buy another Big perk under the existing one-Big-purchase rule.
- Plates hold one of each menu in pickup order. Correct dishes can be delivered from any slot; a completely wrong delivery discards only the oldest dish. A full perk plate rejects a new pickup. Money pickup and meal breaks clear the plate as before.
- Pairs has a 50% chance of a two-dish order, including matching dishes. Each delivered dish credits its sender; eating starts after both arrive, and payment is doubled.
- Home credits automatic drink service to the team. Bank credits automatic deposits and the existing deposit bonus to the team.
- Omakase appears on 60% of orders and accepts any of the five menus. A question-mark graphic identifies it; the actual served dish determines food-specific score bonuses.
- Sky chooses one available menu for the meal. That menu is requested on 30% of orders, and every request for it is special: unlimited patience, double service score and double payment. Its pickup station has a rainbow frame. At closing, unserved special customers leave without an anger penalty.
- Percentage payments round to the nearest coin. Cross retains fractional penalty reductions across customers so a 5% reduction is effective even with the five-point base penalty. Eating 5% faster divides the normal duration by 1.05.
