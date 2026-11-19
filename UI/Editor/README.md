# Game Editor

Open `UI/Editor/GameEditor.tscn` and press **F6** in Godot .NET.

- **Level Editor** opens the [campaign map editor](LevelEditor.md). **Load / New → Game Editor** returns to the hub.
- **Entity Editor** opens unit drafts from the default catalogue. Pick a unit, drag to orbit its mesh, scroll to zoom, and adjust its authored fields. Unit IDs and linked projectile resources are preserved.
- **Test Battlefield** opens the existing benchmark painter with all current draft stats. Pick units and paint both sides, then select **Start painted battle**. Use **Edit Unit** to tune more stats, then **Test Battlefield** to reset to the same deployment with the updated values. If a new footprint makes the deployment invalid, adjust the layout in the previous test first.
- **Save to Project** reviews and writes the selected unit's draft to its original `.tres`. The editor validates a staged resource before replacing the original and checks for external file changes. Save edits the working tree for review and commit in Git.

The first pass covers units. The existing entity plugin still provides the defense and projectile resource tools.
