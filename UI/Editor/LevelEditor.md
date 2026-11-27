# Standalone Campaign Level Editor

Open `UI/Editor/LevelEditor.tscn` in Godot .NET and press **F6**. Choose **Skirmishes**, **Missions**, or **Fortress Defense** first. Skirmishes and Missions then offer **Clash**, **Offense**, and **Defense**, with separate level lists and save destinations. Every registered level in the selected campaign appears as its own button in a wrapping grid; click a level to open it directly, or click **+ New Level** to create one. The picker does not use a level dropdown or scroll container. Hover a numbered level to see its custom name, if one is set. **Game Editor** returns to the hub, and **Return to current level** resumes the active map. The editor saves source assets when running from the editor build.

- Use **Map Settings** for the title, map size, opening camera and mode rules. Fortress Defense uses the existing wave definitions and spawn settings. Enter one wave per line as comma-separated `unit_id=count` pairs.
- Drag rock, water, forest and deployment brushes across the map. Right-drag to erase the selected paint type. Use the placement tools for hills, walls, soldier formations and Fortress structures. Select an object to move, delete or edit its available fields. Use the toolbar or Ctrl+Z and Ctrl+Shift+Z to undo and redo.
- Use **Preview Layout** to check the opening view and authored formations. **Test Fortress** runs a temporary copy of the current map with the normal Fortress gameplay systems. Return from Test restores the editing session.
- **Test AutoBattler** opens a AutoBattler playtest on a copy of the current map. Paint Blue troops inside the authored deployment area with no unit-count allowance, then select **Start painted battle**. The Red army and defenses come from the map. **Edit Blue deployment** resets the fight, and **Return from Test** goes back to the original map without saving test placements.
- **Save As** offers Level001 through Level100 for the current campaign. It creates the level folder, scene and metadata resource. Existing slots require overwrite confirmation. The campaign lists saved numbered levels in numeric order, skipping empty slots. For testing, a profile named `debug` can access every enabled level. Other profiles can access Level001 only. Selecting a level opens its briefing before Deploy loads the scene.
- Overwrites keep recovery copies and a target-path manifest under `.godot/level-editor/`. If an interruption leaves a new level unregistered, inspect that folder before retrying. Keep a copy of recovery files elsewhere before clearing Godot's project cache.

The scene stores shared map nodes and the existing world paint, terrain, placement and wave resources. A AutoBattler additionally stores `AutoBattlerRules`, which defines the Blue deployment cells, count cap and eligible units. An empty allowed-unit list permits all registered units. The AutoBattler scene provides an opening layout preview ahead of CAMPAIGN1. Player deployments, combat progression and rewards belong to later gameplay work.

Fortress Defense uses the existing command scene structure and the shared movement and combat simulation. Loading an older authored scene creates an in-memory editing session. Changes reach the project only through Save. Terrain and paint outside reduced map bounds are retained.

### Deployment layout

- Arrow keys pan the map while the editing canvas is active. **Map Size…** opens the width and depth setting at the top of the dialog. Select **Apply settings** to resize the map.
- In AutoBattler, choose **Set deployment line**, select X or Z and click the map to place the split. **Flip Blue** reverses which side Blue can use. New maps start with a centered Z split. Existing maps without a saved line retain their painted zones until a split is enabled. Painted Blue zones can further narrow the legal Blue side.
- Under **Unit placement**, click **Place units** and choose **Rectangle fill**. Drag to fill and right-drag to erase. A lower density scale packs soldiers closer, with minimum spacing set by the unit footprint. Each fill is one undoable edit. The top bar shows authored soldier totals and their estimated strength using Battle Rating. Preset Blue strength depends on the army the player eventually deploys.
- Choose a building from the **Defenses** dropdown, then click the map to place it. Options come from the building catalogue. Fortress Defense starts with the selected faction's towers in combat. AutoBattler saves and previews Red towers for the future campaign battle loop.

**Show grid** displays the same 2-unit cells used by grid snapping and painting, with a heavier line every eight cells. It defaults to on and can be toggled independently of snapping. Layout previews hide the grid.


## Draw bridge

Pick **Draw bridge**, then drag a rectangle and release to place it. The longer side sets the direction of the deck and its rail lights. You can paint the rectangle anywhere within the map. Right-click a bridge while using **Draw bridge** to remove it, or right-click during a drag to cancel. **Erase** also removes a bridge with a left-click. Placement and removal support undo and redo and save with the map.

The deck follows the terrain and opens only the painted water beneath it. Walls, rocks and structures remain solid. Fortress Defense and the Level Editor's AutoBattler test cache crossing approaches per unit footprint when their navigation graphs are built. A bridge's clear width must exceed the unit's footprint diameter. Oversized units need a wider route or a way around the water.


## Paint elevation

Select **Paint elevation** and choose a brush radius.

- **Raise / Lower** uses left-click or drag to raise the surface and right-click or drag to lower it. Strength can be as small as 0.1.
- **Set Height** accepts an absolute height from −80 to 80 in 0.1-unit steps. Zero is the flat map baseline. Left-click to level the brush interior to that height. The surrounding terrain blends back into the existing surface, using a wider transition for larger height differences. The transition stops at the map edge and is capped at 128 world units.

Each stroke or Set Height click can be undone or redone as one edit and saves with the map. The brush works on the combined terrain surface, including authored hills.

The ground grid and map edge follow the sculpted terrain. Both playtest modes use the saved height surface for grounding.

AutoBattler authoring uses the bottom placement bar. **Units** and **Defenses** show the authored Red roster and tower choices with base HP and damage. **Terrain** contains Rocks, Forest, Water, Elevation, Bridge, and Industrial Walls. Paint Industrial Walls one grid cell at a time, drag a straight Line, or drag a Rectangle Fill. Right-drag to erase painted wall cells. Closed loops stay hollow. Use Rectangle Fill to paint a solid block. **Setup** contains Blue Deployment Zone, Deployment Line, and Opening Camera. Select, Move, and Erase stay available on every tab. Settings for the active tool appear on the left.

Use **Red staging** in the AutoBattler palette to paint a reserve formation on a separate 256-unit grid. Paint or erase with the standard unit tools. The highlighted south edge is the front of the wave. **Back to battlefield** returns to the map. Use **Red entry preview** and click near an edge to position the entry. Cyan lanes are clear, red lanes overlap a blocker or leave the legal entry area, and the pale line shows the largest unit's handoff depth. The timer is under **Simulation → Reinforcement settings**.

Choose **Base Terrain** beside **+ New Level**. Industrial is the default and Steel Slate provides the earlier look. To switch an existing level, open **Map Size…**, choose a base terrain, and save the level.

During a tactical test battle, click a tower to show its health, kills, and range. Click empty ground to clear the selection.
