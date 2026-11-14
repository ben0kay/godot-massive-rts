# DEPLOYMENT

Open **Edit painted deployment** in BENCH0 and choose **Soldiers** or **Obstacles**. Soldiers retain the faction and type selectors and now have a **Paint size** dropdown offering **1×1, 3×3, 5×5, and 10×10** cells. The default remains 3×3. Left mouse paints and right mouse erases using the selected size; dragging continues to work. Even-sized brushes are anchored consistently near the hovered cell (10×10 uses offsets -4 through +5). Brush size affects soldiers only; Obstacles retain their dragged rectangular footprint and Defenses retain their own authored footprints. In Obstacles mode, left-drag previews and commits a snapped rectangle. Right-drag previews and erases the selected area. Click for one cell. New deployments use cells 2 world units wide. There is no painted troop-count cap. Battlefield space and available memory determine the practical limit.

Obstacle edits are atomic. A rectangle that crosses the battlefield edge, overlaps a soldier's full footprint, or exceeds the 65,536 occupied cell or 64 merged rectangle limit is rejected. Soldier paint also respects wall clearance. Erasing can be rejected if it would fragment the remaining walls beyond the rectangle limit.

**Save deployment** writes version 2 to `user://deployment.json`. Version 1 and legacy arrays still load with no walls. Loads validate all cells and soldier positions before replacing the draft. **Clear all** clears both layers. A painted battle uses the wall geometry and troop placements captured when it starts.

The selected soldier brush size is an editor tool setting and is not part of `user://deployment.json`; saves continue to store resulting individual soldier cells with the existing version. Collision/footprint checks, per-cell validation, painting previews and faction selection are unchanged.

## Placement grid overlay

During **Edit painted deployment**, a **Show placement grid** checkbox (on by default) draws fine, semitransparent cell boundaries directly on the flat editor ground. Lines lie on the exact world-space multiples of `DeploymentLayout.CellSize` (2 units for new deployments), so obstacle rectangles and defense footprints correspond to visible cells. Soldier brushes retain the original cell alignment at default density. The overlay is shared across all paint tools and automatically disappears on battle launch or when leaving edit mode.

`PlacementGrid.gdshader` draws the grid on a **single 5000×5000 plane** slightly above the ordinary Y=0 surface; there are no per-cell nodes, CPU line generation, timers, physics bodies or saves. Anti-aliasing and a zoom-based fade reduce aliasing when distant; camera pan and rotation do not change grid alignment. The grid toggle changes visibility immediately and is a transient editor preference, not a deployment-save field. This initial flat-ground editing overlay does **not** project grid lines onto the separate elevated test-plateau terrain; raised terrain is currently available in preset benchmark mode, not as editable deployment terrain.

**Paint density**, below **Paint size**, adjusts spacing for new soldier stamps. The default is **1.0×** (2 units in new deployments where footprints fit). **Touching** spaces units by their footprint diameter, rounded up to the fine grid. Maximum spread is **3.0×** (6 units in new deployments, subject to the footprint minimum). The layout rejects overlapping footprints across stamps and teams. Version 5 records the grid size. Legacy saves and presets load at their original 2.1-unit scale and keep that scale when edited or resaved. Clear the painter to start a new 2-unit layout.

**Paint type → Filled rectangle** uses the same cell-snapped drag preview as obstacles. Left-drag and release to fill the selected area with the current soldier and faction at the chosen density. Right-drag to erase soldiers in the rectangle. A single fill can attempt up to 100,000 positions. **Brush** shows the stamp size control.

Soldier and obstacle rectangle drags show live width and length in world units alongside the snapped preview.
