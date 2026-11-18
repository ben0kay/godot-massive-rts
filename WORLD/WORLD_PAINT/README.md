# WORLDPAINT1

Open Fortress Defense Level 001 in the Godot .NET editor and switch to the 3D viewport. Numpad 7 selects the top view. Open the **World Painter** tab beside Inspector, enable **Paint in viewport**, and choose Rock / Cliff or Deep Water. Left-drag with a brush or filled rectangle. Use **Erase** to remove the selected layer, Ctrl+Z to undo a stroke, and Escape to leave paint mode. Save the level scene to keep the paint. Select the `WorldPaint` node and expand **Data** to edit rock height and layer colours.

Each cell is 2 × 2 world units and aligns to the build grid. Preview and gameplay meshes sample terrain separately. Rock prevents movement and construction and provides solid cover. Water prevents movement and construction and leaves shots and sight clear. Water follows terrain slopes. The painted cell footprint defines the shoreline.

The level owns a paint resource containing its cells and appearance. Meshes are generated on load and in the editor. For another command level, add a level-owned Node3D named `WorldPaint` with the `WorldPaint.cs` script. The first stroke creates its embedded data. Manual Command Walls remain under `Obstacles`.

The painter accepts up to 16,384 in-bounds occupied cells and 128 merged rectangles across both layers. The rectangle limit bounds the input to the existing dense corner visibility graph, which also includes authored walls and buildings. Winding or fragmented shorelines consume more of that budget than straight banks. The painter rejects an entire stroke if its resulting layout exceeds the budget. Out-of-bounds cells after a map resize are retained in the scene and skipped until they fit again.

`CommandBuildings` composes painted footprints with existing obstacles and updates the movement and cover fields when structure footprints change. The two fields share one instance on maps without painted water. Painted rock tops set terrain-relative shell impact bounds per footprint. On steep terrain those bounds conservatively enclose the rock mesh.

## WATER2 appearance

Select **WorldPaint → Data → Water appearance**. Adjust the surface, deep-water and shoreline colours, the width and height of the visible bank, and the ripple strength and speed. Setting ripple strength to zero gives still water. Paint strokes and Undo/Redo retain the appearance settings.

The water follows the terrain at its existing surface offset. Along exposed cell edges, a raised visual bank slopes inward to that surface. Shore distance darkens deeper-looking areas and shades the bank. The bank and surface share one indexed mesh and one opaque material. The complete visible shoreline sits inside the same painted cells that block movement and construction. Terrain heights, pathfinding, line of sight and shots use the existing gameplay geometry.
