# Development Log — Godot Massive RTS

Concise, chronological record of completed implementation tasks. **Append new entries at the bottom**; oldest first. This is a history of source changes, not proof of in-game acceptance. See `NOTES/CodePreferences.md` for the logging rule.

## Entries

- **2026-11-10 | BENCH0 | Source implemented; awaiting owner validation.** Added standalone 3D isometric benchmark arena, batched geometric armies (up to 100k selectable), movement, benchmark melee/ranged workloads, projectiles, deployment presets, camera and performance overlay. No Godot/.NET runtime validation in Codex environment. Commit: `834a383a`.

- **2026-11-10 | BENCH0 optimization | Source implemented.** Added bulk soldier buffer uploads for dense updates, simplified straight lane movement, and exposed simulation cost per tick and ticks per frame. Owner performance testing pending.

- **2026-11-10 | BENCH0 targeting and projectiles | Source implemented.** Skip target searches beyond opposing faction bounds, report grid rebuild and targeting separately, and remove projectiles on their first enemy hit. Owner testing pending.


- **2026-11-10 | VIS1 (faceted units) | Source implemented; awaiting owner validation.** Added reusable low-poly prism/box/hex meshes with lighter top faces and darker fixed-direction sides via vertex colours, preserving MultiMesh batching, faction tints and white damage flashes; no dynamic lights or shadows. Owner visual/FPS testing pending.

- **2026-11-10 | Grid and painted deployment | Source implemented.** Added direct combat cell indexing with overflow handling, a paint/erase deployment editor with local save and load, and a 1920×1080 default window. Owner testing pending.

- **2026-11-11 | BENCH0 survivor pursuit | Source implemented.** Combat soldiers that finish marching without a local target pursue a living opposing soldier using one shared reference per faction. Owner testing pending.

- **2026-11-11 | BENCH0 nearest-enemy pursuit | Source implemented.** Combat soldiers pursue live targets from deployment and use staggered nearest-enemy searches across occupied cells beyond local range. Owner testing pending.

- **2026-11-11 | UNIT0 | Source implemented.** Added five editable soldier definitions with distinct faceted shapes and combat stats, updated seeded benchmark rosters and painted deployments, and migrated legacy saves. Awaiting owner validation.

- **2026-11-11 | Pursuit continuity | Source implemented.** Prepare combat targets before the first movement tick, prioritize bounded replacement searches after target deaths, and keep movement pointed at the last known position between searches. Owner testing pending.

- **2026-11-11 | UNIT0 balance/height adjustment | Source implemented; awaiting owner validation.** Reduced all five soldier mesh heights by 25% (width/footprint unchanged); set Circle damage to 3, Square HP to 45 and Triangle HP to 15. Updated authored roster; no simulation or renderer code changes.

- **2026-11-11 | Basic arrow projectiles | Source implemented.** Moved pooled projectile code into PROJECTILES/Shared, added a reusable Basic Arrow type, and aligned its short mesh to an editable shallow flight arc. Owner testing pending.

- **2026-11-11 | SEP1 soldier separation | Source implemented.** Added bounded footprint pushes for both factions, live Off/Normal/Strong modes, separation diagnostics, and projectile padding that accounts for the tick-wide push cap. Owner testing pending.

- **2026-11-11 | SEP1 performance and stability | Source implemented.** Cached a half-neighbour stencil per occupied cell, damped packed corrections, and made engaged melee hold inside attack reach. Added separation cost per tick. Owner testing pending.

- **2026-11-11 | SEP1 benchmark baseline | Source implemented.** Added worst simulation tick, render sync per tick, MultiMesh upload timing and counts, and viewport CPU/GPU timings. Owner measurement pending.

- **2026-11-11 | SEP1 dense grid | Source implemented.** Replaced linked neighbour traversal with contiguous cell ranges, bounded sparse-map grid scanning, and sampled penetration/candidate counters. Owner performance and jitter measurements pending.
