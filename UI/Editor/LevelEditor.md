# Standalone Campaign Level Editor

Open `UI/Editor/LevelEditor.tscn` in Godot .NET and press **F6**. Choose a campaign and open a registered map or create a new one. The editor saves source assets when running from the editor build.

- Use **Map Settings** for the title, map size, opening camera and mode rules. Fortress Defense uses the existing wave definitions and spawn settings. Enter one wave per line as comma-separated `unit_id=count` pairs.
- Drag rock, water, forest and deployment brushes across the map. Right-drag to erase the selected paint type. Use the placement tools for hills, walls, soldier formations and Fortress structures. Select an object to move, delete or edit its available fields. Use the toolbar or Ctrl+Z and Ctrl+Shift+Z to undo and redo.
- Use **Preview Layout** to check the opening view and authored formations. **Test Fortress** runs a temporary copy of the current map with the normal Fortress gameplay systems. Return from Test restores the editing session.
- **Test Auto Battler** opens a Preset Battles playtest on a copy of the current map. Paint Blue troops inside the authored deployment area with no unit-count allowance, then select **Start painted battle**. The Red army and defenses come from the map. **Edit Blue deployment** resets the fight, and **Return from Test** goes back to the original map without saving test placements.
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


## Draw bridge

Pick **Draw bridge**, click the first bank, then click the opposite bank. The span snaps to the dominant X or Z axis on the paint grid. Scroll to set the width in grid steps, then left-click to commit, or right-click to cancel. Extend both ends onto dry land so units can enter and leave. **Erase** removes a bridge with a left-click. Placement and removal support undo and redo and save with the map.

The deck follows the terrain and opens only the painted water beneath it. Walls, rocks and structures remain solid. Fortress Defense and the Level Editor's Preset Battle test cache crossing approaches per unit footprint when their navigation graphs are built. A bridge's clear width must exceed the unit's footprint diameter. Oversized units need a wider route or a way around the water.


## Paint elevation

Select **Paint elevation** and choose a brush radius.

- **Raise / Lower** uses left-click or drag to raise the surface and right-click or drag to lower it. Strength can be as small as 0.1.
- **Set Height** accepts an absolute height from −80 to 80 in 0.1-unit steps. Zero is the flat map baseline. Left-click to level the brush interior to that height. The surrounding terrain blends back into the existing surface, using a wider transition for larger height differences. The transition stops at the map edge and is capped at 128 world units.

Each stroke or Set Height click can be undone or redone as one edit and saves with the map. The brush works on the combined terrain surface, including authored hills.

The ground grid and map edge follow the sculpted terrain. Both playtest modes use the saved height surface for grounding.
