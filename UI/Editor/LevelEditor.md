# Standalone Campaign Level Editor

Open `UI/Editor/LevelEditor.tscn` in Godot .NET and press **F6**. Choose a campaign and open a registered map or create a new one. The editor saves source assets when running from the editor build.

- Use **Map Settings** for the title, map size, opening camera and mode rules. Fortress Defense uses the existing wave definitions and spawn settings. Enter one wave per line as comma-separated `unit_id=count` pairs.
- Drag rock, water and deployment brushes across the map. Right-drag to erase the selected paint type. Use the placement tools for hills, walls, soldier formations and Fortress structures. Select an object to move, delete or edit its available fields. Use the toolbar or Ctrl+Z and Ctrl+Shift+Z to undo and redo.
- Use **Preview Layout** to check the opening view and authored formations. **Test Fortress** runs a temporary copy of the current map with the normal Fortress gameplay systems. Return from Test restores the editing session.
- **Save As** offers Level001 through Level100 for the current campaign. It creates the level folder, scene and metadata resource. Existing slots require overwrite confirmation. The campaign lists saved numbered levels in numeric order, skipping empty slots. For testing, a profile named `debug` can access every enabled level. Other profiles can access Level001 only. Selecting a level opens its briefing before Deploy loads the scene.
- Overwrites keep recovery copies and a target-path manifest under `.godot/level-editor/`. If an interruption leaves a new level unregistered, inspect that folder before retrying. Keep a copy of recovery files elsewhere before clearing Godot's project cache.

The scene stores shared map nodes and the existing world paint, terrain, placement and wave resources. A Preset Battle additionally stores `PresetBattleRules`, which defines the Blue deployment cells, count cap and eligible units. An empty allowed-unit list permits all registered units. The Preset Battles scene provides an opening layout preview ahead of CAMPAIGN1. Player deployments, combat progression and rewards belong to later gameplay work.

Fortress Defense uses the existing command scene structure and the shared movement and combat simulation. Loading an older authored scene creates an in-memory editing session. Changes reach the project only through Save. Terrain and paint outside reduced map bounds are retained.

### Deployment layout

- Arrow keys pan the map while the editing canvas is active. **Map Size…** opens the width and depth setting at the top of the dialog. Select **Apply settings** to resize the map.
- In Preset Battles, choose **Set deployment line**, select X or Z and click the map to place the split. **Flip Blue** reverses which side Blue can use. New maps start with a centered Z split. Existing maps without a saved line retain their painted zones until a split is enabled. Painted Blue zones can further narrow the legal Blue side.
- Under **Unit placement**, click **Place units** and choose **Rectangle fill**. Drag to fill and right-drag to erase. A lower density scale packs soldiers closer, with minimum spacing set by the unit footprint. Each fill is one undoable edit. The top bar shows authored soldier totals and their estimated strength using Battle Rating. Preset Blue strength depends on the army the player eventually deploys.
- Choose a building from the **Defenses** dropdown, then click the map to place it. Options come from the building catalogue. Fortress Defense starts with the selected faction's towers in combat. Preset Battles saves and previews Red towers for the future campaign battle loop.

**Show grid** displays the same 2-unit cells used by grid snapping and painting, with a heavier line every eight cells. It defaults to on and can be toggled independently of snapping. Layout previews hide the grid.
