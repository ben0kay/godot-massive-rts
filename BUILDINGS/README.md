# BUILDINGS — Static Defenses

DEF1 adds **Laser Tower 001** to the Benchmark Army Painter. Open **Paint tools → Defenses**, choose the faction and type, then left-click to place or right-click on the footprint to remove. Placement snaps to the painter grid. Soldier footprints, walls, other defenses and map edges constrain placement.

The default catalog registers `DEFINITIONS/LaserTower001.tres`. Its Inspector settings include footprint, minimum and maximum range, damage, shots per second, turret speed, pulse duration and line of sight. The default 2 × 2-cell tower fires 12 damage twice per second at 4–30 world units. It keeps a valid target and uses the shared enemy grid to find the nearest valid replacement. Walls and other towers block its line of sight. Soldier armour and hit feedback apply through the usual damage path.

The painter keeps up to 64 defenses independently of the wall allowance. Both compile into the existing obstacle index. Towers stay outside soldier targeting and are indestructible. The renderer batches bases, turret heads, barrels and laser pulses. A pulse is visual feedback for a hitscan attack. The simulation resolves at most one shot per tower per tick, and the shared pulse batch has a capacity of 256.

Deployment saves use version 3 with stable building IDs. The loader still accepts version 1, version 2 and legacy soldier arrays. Initial turret orientation is derived from the default heading and subsequent targeting.

Use **Entity Editor → Defenses** to edit registered towers with a footprint preview and the same draft controls as soldier definitions.

## DEF1-A — Longer barrel and laser muzzle flash

Laser Tower 001 now has a longer shared box barrel: its tip extends roughly 1.7 world units beyond the front edge of the default 2 × 2-cell base at its forward orientation. The beam starts at that exact tip. Each shot also emits a small white-core/cyan starburst at the muzzle, fading and shrinking over 0.11 seconds. Both the starburst and beam use fixed-capacity MultiMesh batches, with no per-shot nodes. The base footprint, combat targeting, damage, range, firing cadence and soldier obstruction are unchanged. Verify at your normal painter/battle zoom in Godot.
