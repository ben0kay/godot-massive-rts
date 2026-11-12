# LEVELS — Campaign Maps

`Level001/Level001.tscn` is the blank first campaign map. It contains a single Node3D. On campaign launch, the launcher adds the return controls at runtime. Running the scene directly in the editor shows only the blank map.

To add a level, author its scene and a `LevelDefinition` resource with a stable ID, display name, unique positive order, and scene path. Add the resource to the campaign's `Levels` array. Add campaigns to `Catalog/DefaultCampaignCatalog.tres`. The campaign menu orders levels by their authored order. The first level is unlocked, and each later level requires completion of its immediate predecessor.

`Runtime/LevelLauncher.cs` validates the launch and attaches the level session controls. **Return to Campaign** leaves progress as-is. **Mark Test Level Complete (Dev)** records completion for the active profile and returns to the campaign. This control requires both a debug build and `AllowDevelopmentCompletion` on the catalog. Actual battle objectives and victory rules are future work.

For exports that use a selected-resource filter, include the menu scenes, UI config, campaign catalog and the level scenes referenced by path.

## Planned game modes and map ownership (not yet implemented)

See [FRONT1 — Campaign mode structure](../UI/CAMPAIGN_MODE_STRUCTURE_PLAN.md) and [endgoal.md](../endgoal.md). The current `Campaign001` ID and existing blank `Level001` are proposed to become **Preset Battles** without breaking profile progress. **Open RTS** will have a separate campaign ID and its own new blank `LEVELS/OpenRTS/Level001/Level001.tscn` for **manual owner authoring in Godot**. Both are displayed under Campaign via the shared catalog. Neither the new Open RTS scene nor the revised menu exists yet; no gameplay mode has been implemented by these documents.
