# DEPLOYMENT

Open **Edit painted deployment** in BENCH0 and choose **Soldiers** or **Obstacles**. Soldiers retain the faction and type selectors and the 3×3 paint brush. In Obstacles mode, left-drag previews and commits a snapped rectangle. Right-drag previews and erases the selected area. Click for one cell. Each cell spans 2.1 world units. There is no painted troop-count cap. Battlefield space and available memory determine the practical limit.

Obstacle edits are atomic. A rectangle that crosses the battlefield edge, overlaps a soldier's full footprint, or exceeds the 65,536 occupied cell or 64 merged rectangle limit is rejected. Soldier paint also respects wall clearance. Erasing can be rejected if it would fragment the remaining walls beyond the rectangle limit.

**Save deployment** writes version 2 to `user://deployment.json`. Version 1 and legacy arrays still load with no walls. Loads validate all cells and soldier positions before replacing the draft. **Clear all** clears both layers. A painted battle uses the wall geometry and troop placements captured when it starts.
