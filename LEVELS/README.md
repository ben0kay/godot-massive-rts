# LEVELS — Campaign Maps

Both Level 001 scenes start as a single editable `Node3D`:

| Campaign mode | Stable campaign ID | Level 001 scene |
| --- | --- | --- |
| Fortress Defense | `OpenRTS` | `FortressDefense/Level001/Level001.tscn` |
| Preset Battles | `Campaign001` | `PresetBattles/Level001/Level001.tscn` |

Fortress Defense Level 001 is reserved for manual terrain and object placement in Godot. The launcher adds return controls at runtime on campaign launch. Both modes can use the same `Level001` ID because progress is keyed by profile and campaign. Existing `Campaign001/Level001` progress still belongs to Preset Battles.

To add a level, author its scene and a `LevelDefinition` resource with a stable ID, display name, unique positive order, and scene path. Add the resource to the campaign's `Levels` array. Add modes as `CampaignDefinition` resources in `Catalog/DefaultCampaignCatalog.tres` in the order they should appear. The campaign menu orders levels by their authored order. The first level is unlocked, and each later level requires completion of its immediate predecessor.

`Runtime/LevelLauncher.cs` validates the launch and attaches the level session controls. **Return to Campaign** and Escape return to the current mode’s level list and leave progress as-is. **Mark Test Level Complete (Dev)** records completion for the active profile and returns to the campaign. This control requires both a debug build and `AllowDevelopmentCompletion` on the catalog. Actual battle objectives and victory rules are future work.

For exports that use a selected-resource filter, include the menu scenes, UI config, campaign catalog and the level scenes referenced by path.
