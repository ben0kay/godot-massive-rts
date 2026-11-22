# Preset Battles economy

**CAMPAIGN1-A is source implemented and awaiting owner validation.**

Each selected profile starts with 500 Credits and zero soldiers the first time it opens the Preset Battles Headquarters. `DefaultPresetRecruitment.tres` maps stable unit IDs to prices and controls which units are available initially. Names, roles, size tiers and Battle Ratings come from the main Unit Catalog. Worker is a Fortress construction unit, and Mobile Generator is reserved for a future support unlock policy. Neither has a recruitment offer. Other campaign units display their price and locked status until their availability is configured.

`preset_battles.json` lives under `user://Profiles/{profileId}/` and stores the permanent roster by stable unit ID. The store serializes recruitment transactions, reloads the current balance, and atomically saves the debit and roster increase together using `AtomicJsonFile`. If the primary fails validation, the store attempts recovery from its backup. Preserve both files if recovery fails. On first open, existing Campaign001 completions are copied and marked as having claimed their first-clear rewards, with no retroactive payout. The existing generic progress file stays in place, including its development completion records.

Army strength estimates sum owned quantities multiplied by Battle Rating. Research, battle rewards and linking the owned roster to deployment are later stages.
