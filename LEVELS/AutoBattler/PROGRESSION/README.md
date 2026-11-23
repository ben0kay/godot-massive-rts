# AutoBattler economy

**CAMPAIGN1-A is source implemented and awaiting owner validation.**

Each selected profile starts with 500 Credits and zero soldiers the first time it opens the AutoBattler Headquarters. `DefaultAutoBattlerRecruitment.tres` maps stable unit IDs to prices and controls which units are available initially. Names, roles, size tiers and Battle Ratings come from the main Unit Catalog. Worker is a Fortress construction unit, and Mobile Generator is reserved for a future support unlock policy. Neither has a recruitment offer. Other campaign units display their price and locked status until their availability is configured.

`preset_battles.json` lives under `user://Profiles/{profileId}/` and stores the permanent roster by stable unit ID. AutoBattler retains the original filename and Campaign001 ID for existing profiles. The store serializes recruitment transactions, reloads the current balance, and atomically saves the debit and roster increase together using `AtomicJsonFile`. If the primary fails validation, the store attempts recovery from its backup. Preserve both files if recovery fails. On first open, existing Campaign001 completions are copied and marked as having claimed their first-clear rewards, with no retroactive payout. The existing generic progress file stays in place, including its development completion records.

Army strength estimates sum owned quantities multiplied by Battle Rating. Research and battle rewards are later stages.

## CAMPAIGN1-B deployment

Open a AutoBattler briefing from Headquarters and select **Deploy**. The painter reads owned quantities for the active profile and shows how many of each eligible type are deployed and available. It uses the authored Blue region and any authored unit whitelist. Left-click paints and right-click erases. Use Rectangle Fill to place or erase larger formations. Enemy placements come from the authored Red forces. The placement capacity applies to both armies and reflects the arena's configured simulation size.

**Fight** runs the shared simulation. A Blue wipe is a defeat, and eliminating the Red army and defenses is a victory. Withdraw ends the attempt immediately. An unresolved battle ends in stalemate after five minutes of simulation time. **Retry Deployment** restores the painted starting positions and resets both armies. Finishing or exiting a battle leaves the saved roster and Credits intact.

Level 001 currently has no authored Red forces. The existing development profile named `debug` can open Level 002, which has authored enemies, for a full campaign smoke test.

## Where to find the code

- Authored maps, campaign metadata and deployment rules live in `LEVELS/AutoBattler/`.
- Campaign deployment, results and retry are in `LEVELS/AutoBattler/BenchmarkArena.Campaign.cs`.
- Headquarters and recruitment UI live in `UI/Menus/AutoBattler/`. The deployment and results HUD lives in `UI/GameplayHUD/AutoBattlerHud.cs`.
- The shared battlefield setup lives in `BENCHMARK/BenchmarkArena.AutoBattlerPlaytest.cs`.
