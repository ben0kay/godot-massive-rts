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

- **2026-11-11 | UNIT0 shorter soldier silhouettes | Source implemented; awaiting owner visual validation.** Halved the existing mesh heights of all five soldiers again without changing width, footprint or combat balance; updated the roster.

- **2026-11-11 | SEP2 allied archer yielding | Source implemented.** Added role-based allied clearance and small capped pushes, a soft return to full spacing, and a live SEP1 comparison toggle. Owner testing pending.

- **2026-11-11 | FB0 soldier hit feedback | Source implemented.** Centralized the 0.12-second white flash and added a grounded 6% visual pulse over 0.18 seconds in both MultiMesh upload paths. Awaiting owner visual and performance validation.

- **2026-11-11 | Combat movement acceleration | Source implemented.** Added short role-based acceleration ramps and braking near attack position, with a gentler default for ranged units. Owner visual validation pending.

- **2026-11-11 | Area damage and Pentagon cleave | Source implemented.** Added a configurable three-target cleave and reusable radial falloff with shared grid queries. Owner testing pending.

- **2026-11-11 | Ranged firing-line movement | Source implemented.** Stationary ranged units hold for any target already in range, settle slightly inside range after advancing, and ease in over 0.32 seconds. Owner visual validation pending.

- **2026-11-11 | Three-team painted battles | Source implemented.** Added Green, removed paint-side restrictions, and updated targeting, projectiles, area damage and ally yielding for all opposing teams. Owner testing pending.

- **2026-11-11 | VIS2 refined soldier silhouettes | Source implemented; awaiting owner visual/FPS validation.** Changed cavalry from a square-like diamond to a ~1.9:1 long rhombus within its original footprint, increased Circle to 16 sides, and added top-edge bevels/intermediate baked shading to all shared soldier meshes. Preserved MultiMesh batches and game balance.

- **2026-11-11 | Cavalry trample | Source implemented.** Added a shared, configurable melee charge with real run-up, swept contacts, momentum resistance, bounded shove, and normal melee recovery. Owner testing pending.

- **2026-11-11 | Soldier movement facing | Source implemented.** Soldiers turn toward intentional travel and cavalry charges. Both MultiMesh upload paths preserve grounded hit pulses. Owner visual testing pending.

- **2026-11-11 | Smooth soldier motion | Source implemented.** Slowed visual turns and added optional between-tick interpolation for soldier positions and headings. Owner visual and performance testing pending.

- **2026-11-11 | Cavalry acceleration tuning | Source implemented.** Doubled the Diamond acceleration time while retaining its top speed and trample requirements.

- **2026-11-11 | Cavalry acceleration | Source implemented.** Set Diamond acceleration to 2 seconds at the existing 9-unit-per-second top speed.

- **2026-11-11 | Circle Infantry facing bypass | Source implemented; awaiting owner visual/performance validation.** Skipped voluntary-turn calculations and render-facing interpolation/rotation for circles, preserving position smoothing, MultiMesh transforms and hit-feedback scaling. Other soldier types still turn normally.

- **2026-11-11 | UNIT1 | Source implemented. Awaiting owner validation.** Compiled shared mesh dimensions, footprint and melee reach from the per-type Size Multiplier. Diamond Cavalry defaults to 1.25×.

- **2026-11-11 | Cavalry balance pass | Source values updated; awaiting owner in-game validation.** Reduced Diamond HP 95→65 and standard melee damage 25→14, set an explicit trample damage multiplier of 1.0× (was implicit 1.35×), and increased acceleration to full unchanged 9.0 speed from 2.0 to 3.0 seconds. Preserved 1.25× size, charge/trample mechanics, reach and other unit definitions.

- **2026-11-11 | OBST1 | Source implemented. Awaiting owner validation.** Added beveled rectangle painting and version 2 saves, shared footprint-aware routes, solid wall collision, direct-fire cover and benchmark counters.

- **2026-11-11 | Movement baseline | Source implemented.** Added a shared 0.8× movement speed multiplier for soldiers, cavalry charges and projectiles. Attack settings retain their authored values.

- **2026-11-11 | Footprint separation and obstacle pursuit | Source implemented.** Weighted ordinary contact by effective footprint radius and reduced detour pauses by allowing a safe approach while routes queue and carrying speed through waypoints. Awaiting owner validation.

- **2026-11-11 | Hexagon Siege Infantry | Source implemented.** Added a double-size melee unit with cleave and a reusable radial stomp/ring. Removed the preset troop-count cap from painting and saved deployments. Awaiting Godot validation.

- **2026-11-11 | Hexagon stomp balance | Source updated; awaiting owner validation.** Changed the Hexagon's shockwave to trigger every fifth eligible melee attack (previously tenth), with damage falling from 25 at the centre to 10 at the edge (was 50 to 25). Radius and ordinary melee attacks are unchanged.

- **2026-11-11 | Contact melee | Source implemented.** Normal melee closes to footprint contact, turns to face the target, and applies cleave only to forward contacts. Hexagon stomp keeps radial damage. Awaiting Godot validation.

- **2026-11-11 | Mesh footprints | Source implemented.** Compiled circular collision radii from the same outlines used to render soldiers, removing oversized default clearance. Awaiting Godot validation.

- **2026-11-11 | Hexagon stomp edge damage | Source tuned; awaiting owner validation.** Increased outer shockwave damage from 10 to 15 via edge multiplier 0.4→0.6; preserved 25 centre damage, every-fifth attack cadence, radius and regular melee cleave.

- **2026-11-11 | UNIT2 and UNIT3 | Source implemented.** Added per-unit melee tuning and a full-screen Unit Editor for existing resources, with isolated drafts, previews and guarded saves. Default combat tuning is preserved. Awaiting editor validation.

- **2026-11-11 | Live melee experiments | Source implemented.** Added live controls for stalled-melee sidesteps, contact hold and equal-versus-footprint separation. Awaiting mirrored-deployment testing in Godot.

- **2026-11-11 | Unit Editor ability panel visibility | Source implemented; awaiting owner editor validation.** Collapsed inactive Cleave, Shockwave and Trample sections to their enable checkboxes; enabled abilities expand immediately and preserve other authored settings. Hid the Ranged panel for melee units. No runtime combat changes.

- **2026-11-11 | Cleave target cap | Source updated; awaiting owner validation.** Raised configurable maximum additional cleave targets from 4 to 8 so the Unit Editor and Godot Inspector accept 6 (up to 7 total targets per swing). Existing defaults and attack behavior are unchanged; the existing allocation-free, bounded cleave selection uses the shared cap.

- **2026-11-11 | Cleave target cap | Source updated; awaiting owner validation.** Increased per-unit maximum additional cleave targets from 8 to 50 (up to 51 total including the primary). The Unit Editor uses the shared cap; kept the existing default of 2. Cleave's temporary stack buffers now size to each unit's configured limit rather than always allocating the maximum, to avoid unnecessary work for normal two-extra-target units.

- **2026-11-11 | TERR1-A | Source implemented.** Added the test plateau preset, static chunk meshes and shared height sampling after ground displacement. Awaiting Godot validation.

- **2026-11-11 | Ranged targeting and benchmark panel | Source implemented.** Added optional seeded ranged target variety and scrollable, collapsible benchmark controls. Awaiting Godot validation.

- **2026-11-11 | Obstacle profiling and debug overlay | Source implemented.** Narrowed and right-aligned the scrollable overlay, added optional obstacle query sampling, and removed per-soldier navigation timers. Awaiting Godot validation.

- **2026-11-12 | Obstacle corridor reuse | Source implemented.** Added conservative direct-approach memoization and a live benchmark comparison toggle. Awaiting Godot profiling.
