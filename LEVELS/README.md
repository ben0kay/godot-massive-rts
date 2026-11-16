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

## Authoring playable command maps

The Command Sandbox uses a 5,000 × 5,000 world-unit square centered on the origin. The Ground node is a saved flat plane visible in the editor. Its light outline marks X/Z ±2,500, with dark ground beyond. Zoom out and pan toward the edge to see it.

1. For a playable map, create a new Godot scene inherited from `BENCHMARK/RTS/CommandSandbox.tscn` and save it in your level folder. The current Fortress Defense Level 001 is an empty template. Use the inherited scene as its replacement when you want the command game to run there.
2. Drag `WORLD/OBSTACLES/CommandWall.tscn` into the scene as a child of `Obstacles`. Move or scale the wall in the editor with no rotation. Both X/Z edges must land on the 2-unit grid. The default 4 × 4 footprint fits when its center is on an even X and Z coordinate.
3. Keep walls clear of the starting squad, the Citadel and the spawn markers. Press **F6** with your level open to test unit routing and Fabricator placement. Walls are registered once at startup.
4. Set the level resource's `ScenePath` to your new scene. For Level 002 or 003, add each new level resource to `FortressDefense/OpenRTS.tres` with its own ID and order.

The inherited scene exposes the opening layout in the 3D viewport:

- **CameraStart** sets the point the camera looks at and its Y rotation sets the opening yaw. Set Opening Camera Size on the level root to change the opening zoom.
- **BlueSpawn** marks the middle of the starting squad's front row. Its Y rotation turns the formation. Set **Enemy Spawn Edge** on the level root or in the Wave Editor panel to choose the single boundary where waves enter.
- **Citadel** is an instance of `WORLD/BUILDINGS/CITADEL/Citadel.tscn`. Move it in X/Z and keep the default scale and rotation. The 10 × 10 footprint snaps to the two-unit grid on play, with centers on odd X/Z coordinates. Its new position is also the objective for enemy waves.

### Starting units and elevation

- Drag `GAMEPLAY/DEPLOYMENT/COMMAND/CommandUnitPlacement.tscn` under **StartingUnits**. Set **Unit**, **Faction**, and **Count** on each placement. Use count 1 for a single soldier. Larger counts form ranks behind the marker, and its Y rotation controls their direction. The editor shows one representative model and a count. Red placements are present at load and join the attack when Wave 1 starts. Disable **Spawn Default Blue Squad** on the level root to use just your own placements.
- Drag `WORLD/TERRAIN/AUTHORING/TerrainHill.tscn` under **Terrain**. Move it in X/Z and set **Radius** and **Rise**. The editor previews the sampled surface. Commands, starting units, waves, moving troops and prop bases use that same surface. Leave Terrain empty for a flat map.
- Place upright props on level sites. The Fabricator rejects sites that vary by more than 0.5 world units across its footprint. Authored walls and Citadels warn about uneven sites and preview at the highest sampled point.

**Map Size** on the command level root is the playable width and depth in world units. Fortress Defense Level 001 uses 1,000 × 1,000. Change Map Size in the level root Inspector and save the scene. Reducing the size shows a warning on the root. Authored nodes stay in the scene even if they fall outside the new playable bounds. Press **Home** in a running command level to center the camera and zoom out to the map overview. The selected wave edge follows the playable boundary.
