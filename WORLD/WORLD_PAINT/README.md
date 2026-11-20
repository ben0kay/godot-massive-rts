# WORLDPAINT1

Open `UI/Editor/GameEditor.tscn` in Godot .NET and press F6. Choose **Level Editor**, open a map, then select **Paint rocks**, **Paint water**, or **Paint forest**. Use **Erase** or Ctrl+Z to remove paint and save the map to keep your changes. The `WorldPaint` node’s **Data** resource remains available in the Inspector for appearance settings.

Each cell is 2 × 2 world units and aligns to the build grid. Preview and gameplay meshes sample terrain separately. Rock prevents movement and construction and provides solid cover. Water prevents movement and construction and leaves shots and sight clear. Water follows terrain slopes. The painted cell footprint defines the shoreline.

The level owns a paint resource containing its cells and appearance. Meshes are generated on load and in the editor. For another command level, add a level-owned Node3D named `WorldPaint` with the `WorldPaint.cs` script. The first stroke creates its embedded data. Manual Command Walls remain under `Obstacles`.

The painter accepts up to 16,384 in-bounds occupied cells and 128 merged rectangles across both layers. The rectangle limit bounds the input to the existing dense corner visibility graph, which also includes authored walls and buildings. Winding or fragmented shorelines consume more of that budget than straight banks. The painter rejects an entire stroke if its resulting layout exceeds the budget. Out-of-bounds cells after a map resize are retained in the scene and skipped until they fit again.

`CommandBuildings` composes painted footprints with existing obstacles and updates the movement and cover fields when structure footprints change. The two fields share one instance on maps without painted water. Painted rock tops set terrain-relative shell impact bounds per footprint. On steep terrain those bounds conservatively enclose the rock mesh.

## Rock formations

**Paint rocks** builds clusters of tall, low-poly columns with chipped caps. Cell coordinates seed each column's shape, width, orientation, height and shade, so reopening a map reproduces the same rocks. Adjacent strokes join into one rubble shelf with no wall at the shared edge. Columns near a join gain a little height as they become part of the larger formation.

The full painted footprint remains impassable. Existing rock paint automatically uses the new mesh, and **WorldPaint → Data → Rock Height / Rock Color** still control the formation's scale and palette.

## WATER2 appearance

Select **WorldPaint → Data → Water appearance**. Adjust the surface, deep-water and shoreline colours, the width and height of the visible bank, and the ripple strength and speed. Setting ripple strength to zero gives still water. Paint strokes and Undo/Redo retain the appearance settings.

The water follows the terrain at its existing surface offset. Along exposed cell edges, a raised visual bank slopes inward to that surface. Shore distance darkens deeper-looking areas and shades the bank. The bank and surface share one indexed mesh and one opaque material. The complete visible shoreline sits inside the same painted cells that block movement and construction. Terrain heights, pathfinding, line of sight and shots use the existing gameplay geometry.

## Forest

Choose **Paint forest** in the in-game Level Editor. Forests share the paint grid with rock and water and are stored as level-local cells. Adjacent cells share an outline with no internal seams. Trees have a deterministic mix of heights and widths. Each tree's visual centre is also offset up to 0.3 world units on X and Z from its painted cell centre, using a stable per-cell hash; this makes groves less grid-aligned without changing painted forest boundaries or gameplay effects. Trunks and crowns share the same offset and resample the terrain at their actual visual positions. They are instanced scenery with no physics colliders.

In playable Fortress Defense maps, ranged units standing in forest have half their maximum attack range and units authored as exactly Size Tier 2 move at 75% speed. Forest does not block movement, line of sight or projectiles. A tree becomes translucent while a live unit's ground position is inside its canopy footprint. The check follows the individual tree’s offset and size.
