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

- **2026-11-12 | Wall sliding | Source implemented.** Clipped movement and separation now preserve a safe wall-parallel remainder. Awaiting funnel gameplay validation.

- **2026-11-12 | Wall corner routing | Source implemented.** Reverted wall sliding and applied wider corner clearance, bounded look-ahead, and previous-corner recovery. Awaiting funnel testing.

- **2026-11-12 | Visual ranged recoil | Source implemented; awaiting owner validation.** Triangle Archers kick back 0.12 world units and recover over 0.18 seconds on successful ranged shots. Added compact optional recoil fields in the full-screen Unit Editor, shared per-type settings and active-only feedback updates; no gameplay displacement or extra soldier nodes.

- 2026-11-12 — BENCH0: Added live targeting timings for local enemy searches, long-range pursuit queries and lazy pursuit tree builds. Awaiting owner profiling in Godot.

- 2026-11-12 — BENCH0: Added a live toggle to halve scheduled long-range refresh frequency for living targets while preserving local search and lost-target replacement cadence. Awaiting owner profiling.

- **2026-11-12 | Benchmark camera yaw control | Source implemented; awaiting owner visual validation.** Added a live 0–360° rotation slider and 45° reset in the left Camera section. 0° aligns the view with battlefield axes, 45° matches the previous diagonal, and WASD/arrow pan axes follow the chosen angle without changing pitch/zoom or simulating new work.

- **2026-11-12 | BENCH tactical formations | Source implemented; awaiting owner testing.** Added five seeded layouts (layered ranks, cavalry pincer, wedge vs shield wall, encirclement, and Blue/Red/Green clash), using all six unit types including Hexagon for mixed modes. Ranged archers occupy rear ranks and Melee stays melee-only. Unit footprints set safe spacing; kept original three presets for historical benchmarks and adjusted camera framing for larger formations.

- 2026-11-12 — FRONT0: Added the local profile picker, campaign progress, display options, and blank Level001. The benchmark remains available directly via F6. Source implemented, awaiting owner validation.

- 2026-11-12 — Unit Editor: Clean roster selections open directly, and switching with an unsaved draft now prompts to save, discard, or cancel. Source implemented, awaiting editor validation.

- 2026-11-12 — FRONT0: Fixed the Options menu build errors by initializing both buttons before wiring their callbacks. Updated the Unit Editor split setting to the Godot 4.7 API.

- 2026-11-12 — ART1: Registered Heavy Artillery with a shared chevron-and-barrel mesh, height-aware indirect shells, shockwave radial damage and rings, and visual recoil. Source checks passed, awaiting owner playtest.

- **2026-11-12 | Open RTS theme / FRONT1 campaign hierarchy | Design documented only.** Updated `endgoal.md` to a futuristic geometric machine-swarm theme with Carbon as the proposed first resource, and reserved a hand-authored Open RTS Level 001 separate from Preset Battles Level 001. Added `UI/CAMPAIGN_MODE_STRUCTURE_PLAN.md` with a Codex-ready menu/catalogue implementation plan. No gameplay/menu code or scenes changed.

- 2026-11-12 — FRONT1: Added catalogue-driven Open RTS and Preset Battles campaign menus and a blank Open RTS Level 001. Preset Battles retains its existing scene and progress IDs.

- 2026-11-12 — FRONT1: Fixed the C# array conversion for the campaign level return shortcut.

- 2026-11-12 — Pursuit: Added conservative occupied-cell pruning and a live benchmark comparison toggle. Documented follow-up diagnostics and conditional shared replacements.

- 2026-11-12 — DEF1: Added catalogued static laser defenses to the Benchmark Army Painter, including saved placements, shared obstacle indexing, hitscan targeting and batched visuals. Awaiting owner validation.

- 2026-11-12 — SIM60: Defaulted the core simulation to 60 Hz with bounded five-step catch-up. Preserved 30 Hz acquisition and rendered-frame route budgets, scaled separation corrections, and added achieved tick rate to the HUD.

- 2026-11-12 — Entity Editor: Renamed the editor tab and added catalogue-driven Units and Defenses categories with shared draft controls and editable laser tower stats.

- 2026-11-12 — Benchmark: Split pursuit diagnostics by search reason and added F1 side-panel visibility while keeping the compact battle summary on screen.

- 2026-11-12 — DEF1-A: Extended the Laser Tower's shared barrel past its square base, aligned beam emission with the new tip and added a pooled, fading cyan/white muzzle starburst per shot. Rendering only; gameplay and collision footprints unchanged. Awaiting owner visual validation.
- 2026-11-12 — BENCH0 pursuit profiling: Split scheduled searches by target history, unit type, and distance at query time. Report extra replacement budget pressure and nearby global results. Awaiting owner benchmark validation.

- 2026-11-12 — SHADOW1: Added batched soldier ground shadows and static battle obstacle shadows with a live benchmark toggle. Awaiting owner visual and performance validation.

- 2026-11-12 — BENCH visual: Lightened the flat benchmark map and plateau ground to a neutral cool grey. No changes to terrain geometry, obstacles, collision or simulation. Awaiting owner visual validation.

- 2026-11-12 — BENCH0 pursuit experiments: Added independent live toggles for a distant melee retarget delay and a shared long-range search cap, with HUD deferral counts. Awaiting owner benchmark validation.

- 2026-11-13 — BENCH0 movement profiling: Added a CPU overlay toggle for sampled melee sidestep and contact-hold costs, direct cavalry run-up timing, and estimated remaining movement. Awaiting owner validation.

- 2026-11-13 — BENCH0: Added painted battle orders for all-auto combat or one holding faction, with local-only acquisition and no voluntary movement for holders. Awaiting owner validation.

- 2026-11-13 — BENCH0: Added configurable painted guard areas with stay, return to post, and one-way counterattack orders. Awaiting owner validation.

- 2026-11-13 — BENCH0: Split guard detection and retention ranges. Melee detects at half the guard radius and ranged at half its weapon range.

- 2026-11-13 — BENCH0: Added independent orders for Blue, Red, and Green in painted battles. Counterattack promotes only its own faction.

- **2026-11-13 | UI Army balance bar | Source implemented; awaiting owner visual validation.** Added a shared, top-centered gameplay HUD under UI/GameplayHUD. Blue/Red surviving-count proportions animate after casualties; an optional Green middle segment supports three-faction matches. The arena supplies existing constant-time FactionAlive counters every 0.3 seconds. Hidden during incremental spawn and deployment editing; no simulation scans, collision or target changes.

- **2026-11-13 | Painted soldier brush sizes | Source implemented; owner validation pending.** Added a soldier-only dropdown for 1×1, 3×3 (default), 5×5 and 10×10 cell stamps. Left-click/drag paints and right-click/drag erases using the selected brush; obstacle rectangles, defense footprints and deployment save format remain unchanged.

- 2026-11-13 — DEF1-B: Replaced flat laser tower parts with shared beveled layered chassis, a larger faceted triangular head and thicker beveled barrel with muzzle sleeve. Preserved MultiMesh batching, beam/flash endpoints and gameplay; owner visual validation pending.

- 2026-11-13 — BENCH0: Melee guards now detect enemies at the full configured guard radius.

- **2026-11-14 | Deployment placement grid | Source implemented; awaiting owner Godot validation.** Added a toggleable, semitransparent 2.1-unit world-space grid to the painted benchmark editor. One unshaded shader plane avoids per-cell meshes and draws only while editing; the grid follows obstacle, defense and soldier snapping, stays aligned through camera movement, and fades when zoomed out. Preserved simulation and deployment save format.

- **2026-11-14 | BENCH left controls readability | Source implemented; awaiting owner visual validation.** Widened the left benchmark panel from ~310 to ~440 px, switched editable fields to compact label/control rows (including per-team orders and camera yaw), added contrasting category headers/inset section bodies and distinct muted help text. Added F2 to independently toggle only the left panel; F1 still toggles both and F4 controls only expanded right-hand diagnostics. No combat, simulation or deployment data changes.

- 2026-11-14 — BENCH0: Added opt-in reuse of verified route waypoints and a benchmark reuse counter.

- 2026-11-14 — BENCH0: Added opt-in preventive crowd separation for voluntary movement in Normal mode.

- 2026-11-14 — UNIT balance: Set Circle to 50 HP/5 damage, Square to 85/12, Triangle to 25/9, Diamond to 105/14, Pentagon to 285/22 (0.9× size), and Hexagon to 600/36. Other stats and Chevron Artillery unchanged; updated roster documentation. Awaiting owner gameplay validation.

- 2026-11-14 — BENCH0: Reduced preventive crowd separation broad-phase scans with a finer grid and tighter swept-step bounds.

- 2026-11-14 — BENCH0: Removed preventive crowd stacking and streamlined separation candidate scans while preserving candidate order and correction rules.

- 2026-11-14 — BENCH Paint UI: Replaced the Paint tool dropdown with mutually exclusive radio-style buttons, moved Paint faction above Soldier type and removed the redundant soldier drag/erase help line. Painting behavior and deployment saves unchanged; awaiting owner UI check.

- 2026-11-14 — DEATH1: Added bounded soldier shrink and puff feedback with a live toggle, plus footprint-aware painted soldier spacing and compatible deployment loading.

- 2026-11-14 — BENCH0: Added a filled soldier rectangle tool using the obstacle drag interaction and footprint-aware paint density.

- 2026-11-14 — SHADOW1-B: Doubled fake ground-shadow opacity, reduced soldier shadow sizes (especially Pentagon/Hexagon), and shifted them to a slight right/down cast; compacted and aligned static obstacle/tower shadows. Existing batching and gameplay preserved; awaiting owner visual validation.

- 2026-11-14 — DEATH1-B: Increased dark-red death blobs from 3 to 4 per soldier and enlarged their shared geometry scale for readability, retaining the 0.28-second lifetime and pooled MultiMesh rendering. Source implemented; awaiting owner visual validation.

- 2026-11-14 — BENCH0: Added named local presets for replaying painted battles with their original placements, orders, guard radius, and seed.

- 2026-11-14 — BENCH0: Added live width and length arrows to painted rectangle previews.

- 2026-11-14 — BENCH0: New deployments use 2-world-unit cells. Version 5 saves retain each layout’s grid size and legacy battles keep their original geometry.

- **2026-11-14 | Fortress Defense Command Sandbox | Source implemented.** Added a standalone 20-soldier squad with box selection, right-click movement, and a measured right-drag formation preview. Awaiting Godot .NET playtesting.

- **2026-11-14 | Shared benchmark HUD | Source implemented.** Shared the compact FPS panel between both benchmarks and added the army balance and selection health bars to the Command Sandbox. Awaiting Godot visual validation.

- **2026-11-14 | Project layout.** Grouped features under GAMEPLAY, WORLD and VISUALS. Kept LEVELS at the root and grouped maps by campaign mode while preserving campaign and level IDs. Updated Godot resource references.

- **2026-11-14 | FAB1.** Added Carbon, a prebuilt Citadel, Fabricator placement and construction, editable batch production with blocked-exit retries, and rally orders in the Command Sandbox. Dynamic soldiers share simulation state and grow their renderer batches on demand. Awaiting Godot playtesting.

- **2026-11-14 | FAB1 exit tuning | Source implemented; awaiting owner validation.** Halved the Fabricator release interval to 0.075 seconds, applied a 1.5× move-speed boost only along the gate-to-exit leg, and allowed follow-on releases once the hatch itself is clear. Kept blocked-spawn retries and normal movement speed after exit.

- **2026-11-14 | Fortress Defense waves.** Added three editable waves, Citadel-priority combat, countdown and end states, and a world-space Fabricator production bar in the Command Sandbox. Awaiting Godot playtesting.

- **2026-11-14 | Fortress Defense melee interception.** Idle Blue melee soldiers now detect and close on Red attackers before contact. Player movement and Fabricator exit orders retain priority.

- **2026-11-14 | Fortress Defense.** Moved the Red spawn marker from Z=24 to Z=120 for a longer opening approach.

- **2026-11-14 | Command Sandbox.** Added one-pass Normal separation with toggle and timing stats, plus K + left-click attack-move with destination resumption.

- 2026-11-14 — DMG1: Added the configurable damage matrix, H Breacher, and footprint-based Citadel and Fabricator combat in the Command Sandbox. Benchmark towers use typed outgoing damage. Source implemented, awaiting owner gameplay and performance validation.

- **2026-11-14 | BENCH0 default toggles.** Enabled **Delay distant melee retargeting**, **Cap long-range searches (512/step)**, and **Reuse route waypoints** by default in the benchmark controls. All three remain independently toggleable; their algorithms, caps, and saved benchmark workloads were not changed. Source-only review; awaiting owner Godot validation.

- 2026-11-14 — DMG1 balance: Set percentage armour to 0% across the shipped roster and kept the damage-type matchup classes.

- 2026-11-14 — DEATH1-C: Recolored soldier death puffs to the defeated faction and added small, pooled/batched ground splats fading after 3 seconds, with an independent 1,024-mark cap. Combat, damage and lifetimes of existing puffs unchanged; awaiting owner visual validation.

- 2026-11-14 — DMG1 balance: H Breacher deals 20 damage to structures and 50% to units through editable category modifiers. Citadels, Fabricators and benchmark towers default to 1,000 HP and are removed on destruction.

- 2026-11-14 — DEATH1-D: Lengthened faction-colored death-blob lifetime from 0.28s to 0.42s, slowing the existing outward motion and fade; increased travel distance about 14%. Four-blob batching and 3s residual ground splats unchanged; awaiting owner visual validation.

- 2026-11-14 — Fixed painted benchmark Auto Attack pursuit so structure-capable units can reach hostile towers outside local detection range when their soldier target is gone.

- 2026-11-14 — Benchmark Auto Attack now compares nearest enemy soldiers and attackable tower edges by distance for structure-capable units.

- 2026-11-15 — Benchmark tower comparisons reuse the shared soldier acquisition result and its search budget.

- 2026-11-15 — VIS2-B: Darkened soldier side faces by 25% and bevels by 12.5%, retaining bright tops, faction colors and batching. Shared building bevels are unchanged. Source reviewed; awaiting owner visual validation.

- 2026-11-15 — PROJ-LASER1: Replaced Triangle Archer's arrow visual with a thin, short pale-cyan unshaded/emissive laser-bolt BoxMesh via its dedicated projectile resource. Preserved projectile damage, speed, shallow arc, collision and MultiMesh batching; retained BasicArrow for other use. Source reviewed; awaiting owner visual validation.

- 2026-11-15 — PROJ-LASER2: Made Triangle laser bolts subtly follow the firing faction colour (blue/red/green) via opt-in per-projectile MultiMesh instance tint and one shared unshaded shader. Existing bolt dimensions, damage, speed, arc and collision unchanged; other projectile types remain untinted. Source reviewed; awaiting owner Godot visual validation.

- 2026-11-15 — Command Sandbox: Added a visible border at the command bounds and editor-authored box walls that participate in routing and placement.

- **2026-11-15 | RTS freeform formation line.** Added Right Shift + right-mouse drag to sketch curved formation lines; previews and commits cached destinations placed along arc length, with extra ranks behind local tangents. The measurement is still straight from start to finish. Reuses ordinary move-order goal validity and keeps right-drag, K attack-move, rally and command simulation intact. 512 bounded stroke samples; owner Godot visual validation pending.

- 2026-11-15 — Command maps can author the opening camera, troop spawn markers and a placeable Citadel scene in the 3D editor.

- **2026-11-15 | RTS attack-move formation drag.** K + left-click retains the centered attack-move order but now issues on mouse release, allowing K + left-drag to preview and commit the existing straight formation geometry (columns, ghost footprints and distance measurement) with attack-move enabled. Normal left selection, right move drag, Right Shift freeform move and K one-shot arming remain unchanged. Source-only review; awaiting owner Godot validation.

- 2026-11-15 — Saved the Citadel model and flat command ground as scene meshes visible in the 3D editor.

- 2026-11-15 — Fortress waves enter from one configurable map edge. Added an optional Wave Editor panel for level wave composition, delay and edge.

- 2026-11-15 — Added the missing span namespace to squad input and migrated Wave Editor to the current editor dock API.

- 2026-11-15 — Command Wall prefab grounds its base on the flat terrain when placed or moved in the 3D editor.

- 2026-11-15 — Added authored command hills and starting unit placements, sharing terrain height across preview, commands, troop movement and structure placement.

- 2026-11-15 — UNIT4 / PROJ-LASER3: Reduced Triangle laser-bolt dimensions by 25% and added Long Triangle Marksman (2× length isosceles silhouette, 65 HP/3.2 speed/13 damage/1.7s rate/48 range) with longer faction-tinted bolt, stable catalogue entry and Fabricator recipe. Existing unit indices, gameplay systems and benchmark preset rosters preserved. Source reviewed; awaiting owner Godot validation.

- 2026-11-15 — Entity Editor now edits shared projectile flight and explosion settings, with a shortcut from ranged units.

- 2026-11-15 — Fixed Godot C# AddChild calls for internal terrain and unit previews.

- 2026-11-15 — Command selection now treats starting and produced troops individually, regardless of spawn order.

- 2026-11-15 — Level 001 now uses a 256-unit square, with matching runtime border, wave entry, command and placement bounds.

- 2026-11-15 — Restored Level 001 to full map size and added Home for a centered command-map overview.

- 2026-11-15 — Set Fortress Defense Level 001 to 1,000 × 1,000 and added editor warnings for reduced map bounds.

- 2026-11-15 — Applied editable per-unit turn rates to movement and combat across both modes, with shared tick coefficients and a Circle fast path.

- 2026-11-15 — Added editable Scatter and Rows wave entry patterns and enabled Scatter for Level 001.

- 2026-11-15 — Matched the embedded window override to 1920 × 820 and replaced the Fabricator dropdown with a scrollable two-column unit list.

- 2026-11-15 — Removed the campaign gameplay overlay and kept campaign exit available through Escape.

- 2026-11-15 — Allocated wave IDs separately from starting and produced troops, removed the raised Level 001 ground override, and enabled shared soldier shadows in command levels.

- 2026-11-15 — CARBON1 / WORKER1 / HUD1: Added authored Carbon deposits and a paid Citadel Worker queue, and unified command selection and build controls in one bottom HUD.

- 2026-11-15 — WAVE spawn rate: Added editable 1–60 units/sec Stream/Scatter spawn frequency to CommandSandbox and Wave Editor (default 6.25/s matching previous 0.16s interval). Retained fractional tick timing for high rates, capped to one attempt/tick, kept Rows/row delay unchanged; source checked, awaiting Godot owner validation.

- 2026-11-16 — WORKER1: Shared Fabricator rally controls with the Citadel and gave the Worker a beveled rectangular chassis with a pointed front drill. Source checked, awaiting Godot owner validation.

- 2026-11-16 — Fortress Defense: Moved map size into the per-level resource and enlarged the Worker chassis by 50% with twice the drill projection. Source checked, awaiting Godot owner validation.

- 2026-11-16 — Entity Editor: Bound roster and category selection to named Godot methods and surfaced skipped entries and unrecognized unit shapes. Catalogue checked from source, awaiting editor validation.

- 2026-11-16 — Wall authoring: Made each placed or duplicated CommandWall box mesh independently resizable while sharing the material and retaining terrain snap. Verified mesh isolation in a headless Godot editor check.

- 2026-11-16 — Fortress Defense: Added Laser Tower 001 to the Ctrl build menu, using shared tower rendering and combat, terrain-aware placement, and normal structure health and obstacle cleanup. Source checked, awaiting Godot playtest.

- 2026-11-16 — Wall authoring: Persisted per-wall red-handle dimensions in level scenes and verified separate sizes survive an editor save and runtime reload.

- 2026-11-16 — Wall authoring: Exposed per-wall dimensions as Wall Size in the Inspector. Verified both Inspector and mesh edits save and reload independently.

- 2026-11-16 — Fortress Defense: Restored Level 001 map settings at 256 × 256 and added a matching-resource fallback when a level scene loses its settings assignment. Source checked, awaiting Godot playtest.

- 2026-11-16 — CARBON2: Added worker right-click construction orders on Carbon deposits, 4-second repairable/resumable extractor builds, automatic 5 Carbon/s from finite deposits to the existing wallet, and H-targetable destructible extractor structures. Shared bottom HUD reports deposit/extractor status. No hauling or worker mining AI. Source implementation pending Godot gameplay validation.

- 2026-11-16 — CARBON2 review: Fixed unfinished extractor retargeting, released workers when rigs are destroyed, and kept extractor footprints and Fabricator limits independent. Increased Worker size 2.5× with speed and health unchanged. Source reviewed, awaiting Godot playtest.

- 2026-11-16 — Fortress Defense: Wired in shared hit and death feedback and used exact authored wall mesh bounds for gameplay obstacles. Source reviewed, awaiting Godot playtest.

- 2026-11-16 — CARBON2: Moved Extractors into the Ctrl build tray. Placement targets eligible Carbon Deposits and dispatches a Worker; ordinary right-click movement no longer begins construction.

- 2026-11-17 — WORLDPAINT1: Added the Godot 3D World Painter to Fortress Defense Level 001. Painted rock blocks movement and supplies cover, while water blocks movement and building placement and permits fire across it. Source reviewed with a bounded geometry check, awaiting Godot .NET playtest.

- 2026-11-17 — Pikeman: Added a spear-tipped T-shaped Strong Infantry counterpart to the roster and Fabricator. Its charge resistance stops stock cavalry tramples in the benchmark, and its regular melee strike gains a bonus against the Diamond unit ID. Source checked, awaiting Godot playtest.

- 2026-11-17 — PROJ-LASER4: Increased only the Long Triangle Marksman's dedicated laser projectile authored speed from 45 to 67.5 (+50%), preserving regular Triangle lasers, damage, range, firing interval, visuals and arc. Source reviewed; Godot playtest pending.

- 2026-11-17 — Widened Siege Infantry cleave from 100° to 150° and Elite Infantry cleave from 70° to 85°.

- **2026-11-17 | SKIRM1 planning only.** Added `GAMEPLAY/UNITS/SKIRM1_IMPLEMENTATION_PLAN.md`: fragile mobile ranged Skirmisher, closest-valid-enemy shooting (ignoring ranged variety), staggered/bounded 360-degree threat checks, retreat hysteresis, normal separation/obstacle rules, and future benchmark plus Fortress Defense integrations. **No implementation performed**; Codex should review/adapt before separately authorized coding.

- 2026-11-18 — Added a benchmark attack range overlay for hovered units, including cleave geometry and shockwave radius.

- 2026-11-18 — Moved cleave into Primary attack as an area hit pattern and added configurable extra cleave reach. Combat, movement, and both previews use the new setting.

- 2026-11-18 — Fixed Entity Editor selection and reload dispatch, refreshed linked resources from disk, and enabled catalogue refresh while retaining unsaved drafts.

- 2026-11-18 — Labelled melee range input Base detection range under Targeting and ranged input Firing range under Primary attack in Entity Editor.

- 2026-11-18 — Fixed World Painter activation by registering and opening its right-side dock after editor setup and balancing the paint resource subscriptions.

- 2026-11-18 — World Painter selects its paint node when armed and shows viewport feedback for valid cells, map bounds and camera angle.

- 2026-11-18 — Moved Wave Editor into the right-side dock beside World Painter and stacked its controls to fit.

- 2026-11-18 — Fortress Defense waves now sample cached clear openings along a configurable spawn width. Nearby-unit checks reuse the battle grid.

- 2026-11-18 — Allowed overlapping Fabricator launches and shared rally destinations so troop congestion no longer holds the release queue.

- 2026-11-18 — WATER2 adds animated painted-water shading and terrain-following banks inside the existing blocked water cells.

- 2026-11-18 — Fixed Fabricator rally handoff under separation, distributed rally arrivals, and added clear enemy approach alternatives and tight routing corners.

- 2026-11-18 — BENCH-POWER1 adds unit Battle Ratings and a painted-deployment strength bar with incremental faction totals.

- 2026-11-18 — Added Yellow and a shared faction catalogue for painted battles, unit colours and both army bars. Older deployments keep their faction IDs and battle orders.

- 2026-11-18 — UNIT-TIER1 adds editable physical Size Tier metadata to unit definitions, the Entity Editor and the compiled unit catalogue.

- **2026-11-19 | LEVEL-EDITOR1 | Source implemented.** Added an F6 campaign level editor with shared map authoring, Preset Battles deployment rules, Fortress testing and staged campaign saves with overwrite recovery. Awaiting Godot .NET validation.

- **2026-11-19 | Level editor deployment tools | Source implemented.** Reserved arrow keys for camera panning and added a reversible Preset Battles deployment split, benchmark-style rectangle fills, live soldier strength, and authored laser towers. Awaiting Godot .NET validation.

- **2026-11-19 | Level editor visible grid | Source implemented.** Added a default-on, zoom-aware placement grid with an independent visibility toggle.

- **2026-11-19 | Level editor placement controls | Source implemented.** Split unit and defense placement into sidebar sections, populated defenses from the building catalogue, and surfaced map size at the top of its settings dialog.

- **2026-11-19 | Numbered campaign saves | Source implemented.** Save As offers 100 numbered slots per campaign, and campaign menus and unlocks follow numeric level order.

- **2026-11-19 | Campaign briefing | Source implemented.** Added debug-profile level access and pre-deployment briefings with authored map previews, troop counts, and a future recruitment entry.

- **2026-11-19 | Level editor placement fix.** Read authored wall footprints without grounding walls or accessing the battle simulation during placement. Placement errors now include a stack trace in Godot's output.

- **2026-11-19 | DREAD1 plan only.** Added `GAMEPLAY/UNITS/DREAD1_IMPLEMENTATION_PLAN.md` for a Tier 5 Dreadnaught: one slow auto-pursuing pointed rectangular hull, four independent miniguns with per-turret nearest-target queries and minimum/maximum ranges, tiny fast pooled projectiles, and **moving front-only crushing**. Covers large-hull collision/separation, rarity, editor/level/production integration, performance budgets and validation. No gameplay code was added; implementation requires separate owner approval.

- **2026-11-19 | DREAD1 planning only.** Added `GAMEPLAY/UNITS/DREAD1_IMPLEMENTATION_PLAN.md`: rare Tier 5 Dreadnaught with a wedge-front rectangular hull, independent turret-local nearest/min-max-range rapid-fire guns, slow hull pursuit, front-only contact damage, and explicit large-footprint/20k separation concerns. Indexed in unit README. No feature code implemented; future Codex review/adaptation and owner authorization required.

- **2026-11-19 | DREAD1 first pass | Source implemented.** Added the Tier 5 Dreadnaught to the benchmark painter with a slow-turning wedge hull, four independently aimed pooled-projectile miniguns, and a damaging prow. Benchmark playtest and performance measurements are pending.

- **2026-11-19 | Dreadnaught visual refinement | Source implemented; awaiting owner validation.** Applied 0.9 SizeMultiplier to the Tier 5 Dreadnaught so its hull, collision outline, turret mounts and height shrink together by 10% without changing authored combat stats or SizeTier. Added baked faceted deck armor, prow keel, shallow deck rails/vents and side armor details to the shared hull mesh. Four independent minigun visuals now use a layered mount and twin small barrels; muzzle origin scales with the visual turret. Combat targeting, turret ranges, movement, contact damage and the batched infantry renderer remain unchanged. Godot appearance/build validation pending.

- **2026-11-19 | Game Editor first pass | Source implemented.** Added an F6 authoring hub, unit draft inspector and orbit preview, benchmark tests with unsaved stats, and reviewed saves to the original unit resources.

- **2026-11-19 | Forest visual jitter | Source implemented.** ForestTree instances now use stable per-cell X/Z offsets up to ±0.3 world units while preserving the paint grid, forest gameplay boundaries, trunk/crown alignment and terrain grounding. No additional instances, collisions or simulation loops. Godot visual validation pending.

- **2026-11-20 | Forest visibility | Source implemented.** Fade only trees with live units beneath their individual canopies and blend opacity without dithering. Godot visual validation pending.

- **2026-11-20 | Game Editor | Source implemented.** Added Benchmark to the F6 Game Editor launcher.

- **2026-11-20 | Unit visuals | Source implemented.** Added the pre-launch enhanced mesh switch and a recessed, rimmed Circle Infantry mesh on the shared renderer.

- **2026-11-20 | ROCKET1 | Planned only.** Added `GAMEPLAY/UNITS/ROCKET1_IMPLEMENTATION_PLAN.md` as a Codex handoff for a wider, abstract deep-fork rocket launcher with one tube per prong, alternating shared-target rocket shots, smaller arcing `AreaDamage.Radial` explosions than Chevron Artillery, performance and editor/game-mode integration notes. No gameplay implementation authorized or changed.

- **2026-11-20 | ROCKET1 | Source implemented.** Added the Twin Rocket Launcher, alternating pooled rockets, and editable splash settings. Registered it for painting and Fortress production. Godot gameplay and visual validation pending.

- **2026-11-20 | ROCKET1 tuning | Source implemented.** Halved firing cadence, increased launcher and rocket mesh linear scale, and added optional batched rocket and artillery trails and flashes.

- **2026-11-20 | Dreadnaught minigun cadence | Source implemented.** Set the shared per-gun `TurretShotInterval` override to 0.1128 s (from 0.15 s), increasing each of the four miniguns' nominal firing rate by ~33%; left hull contact interval, projectile velocity, damage and targeting untouched. Godot playtest pending.

- **2026-11-20 | DREAD1 deck cannon | Source implemented.** Added an optional center-mounted rocket cannon with its own target and cadence in the existing Benchmark Dreadnaught system.

- **2026-11-20 | Rocket mounts | Source implemented.** Migrated Twin Rocket Launcher and Dreadnaught cannon to configurable mounts with projectile scale and splash overrides.

- **2026-11-20 | Benchmark terrain | Source implemented.** Added a world-space steel slate material with subtle stone relief and sheen to the flat Benchmark ground.

- **2026-11-20 | Muzzle smoke | Source implemented.** Added batched, lingering artillery and rocket muzzle smoke with eastward default drift and a per-level wind setting.

- **2026-11-20 | Rocket mounts | Build fix.** Corrected the unit catalogue to populate its indexed definition lookup rather than indexing the constructor input.

- **2026-11-20 | Benchmark and DREAD1 | Source implemented.** Added opt-in hard footprint separation and migrated the Dreadnaught corner miniguns to configurable weapon mounts.

- **2026-11-20 | Explosive impacts | Source implemented.** Added batched flash and sparks sized by the resolved projectile splash radius.

- **2026-11-20 | Benchmark hard separation | Source implemented.** Added bounded sideways yielding for allied ranged units and local sidesteps under hard footprint checks.

- **2026-11-20 | FX2 | Source implemented.** Added progressive cracks using the existing instance stride, terrain-grounded pooled fragments with per-tick and active caps, and an FX2 toggle in UnitVisualConfig. Awaiting owner visual and FPS validation.

- **2026-11-20 | VIS-IDEAS1 | Planning only.** Added a Codex review backlog for eight optional unit and battlefield visual polish ideas under `VISUALS/FEEDBACK/`, indexed in visual-feedback and unit READMEs. Existing FX2 remains source-implemented; no visual or gameplay code changed.

- **2026-11-20 | FX2 tuning | Source implemented.** Thickened and darkened cracks, and tuned crumble deaths to emit six smaller pieces with slightly wider spread under the existing caps.

- **2026-11-20 | Bridges | Source implemented.** Added saved grid-aligned bridge drawing to the Level Editor and cached footprint-aware crossings for Fortress Defense and Preset Battle playtests. Awaiting owner gameplay validation.

- **2026-11-20 | Painted rock formations | Source implemented.** Replaced the flat rock mesh with deterministic, faceted columns on a shared rubble shelf. Kept painted-cell blocking and updated the height bounds used for airborne impacts.

- **2026-11-20 | Elevation brush | Source implemented.** Replaced Place hill with signed elevation strokes, preserving legacy hills and sharing the ground shader between sculpted terrain and flat areas. Added save and undo support.

- **2026-11-20 | Strong separation | Source implemented.** Added a live 0–10% overlap slider that relaxes Strong pair clearance while retaining its original behavior at 0%.

- **2026-11-20 | Preset playtest UI | Source implemented.** Shortened the bottom placement panel and arranged compact silhouette cards in two rows.

- **2026-11-20 | Hard separation | Source implemented.** Moved the live 0–10% allowance to Hard pair validation and restored Strong clearance. Tightening the allowance repacks transactionally.

- **2026-11-20 | ROCKET-TOWER1 | Source implemented; awaiting owner testing.** Added heavy 3×3 Rocket Tower with stepped beveled base, rotating two-barrel launcher and alternating pooled arcing rockets. Reused TwinRocket at 1.8× visual scale and 2× splash radius (2.5→5.0) through existing AreaDamage/trajectory/impact systems. Added benchmark/editor and Fortress Defense placement, Ctrl build menu at 1450 Carbon, and rocket stepping in melee-only benchmark compositions. Laser Tower remains unchanged. Source-only validation; owner to check visuals/FPS in Godot.

- **2026-11-20 | Projectile visuals | Source implemented.** Used faction-catalogue colours to tint projectile trails and explosive impacts through the existing shared batches.

- **2026-11-20 | Railgun Tower | Source implemented.** Converted the legacy laser tower to a three-second sweeping, piercing beam and added weapon-specific recoil for the rail barrel and individual rocket tubes. Preserved the tower resource path and save ID.

- **2026-11-20 | Defense catalogue compile fix.** Passed the draft projectile catalogue when restoring Entity Editor test layouts and reused the compiled building catalogue on startup.

- **2026-11-20 | Chevron Heavy Artillery Tier 3 | Source implemented, owner testing pending.** Increased size multiplier to 1.25×, changed physical SizeTier 2 → 3, reduced authored speed 1.5 → 1.2, raised HP 350 → 500. Added editable Chevron-only artillery barrel length multiplier set to 2.0×, compiled into shared UnitOutline so the beveled barrel, real projectile muzzle, editor preview and circular clearance all follow the same extended tip. Other artillery targeting, shell splash, recoil, damage and cadence unchanged. Updated Entity Editor and unit/rendering docs; no Godot runtime or FPS testing performed.

- **2026-11-20 | Slope movement | Source implemented.** Added directional grade speed scaling, Size Tier uphill caps and a 10% downhill cap across ordinary movement, guards, commands and charges.

- **2026-11-20 | Default level terrain.** Shared the sculptable ground resource across Preset Battles, Fortress Defense and the Level Editor template, and initialized the empty Preset Battle Level 001 with that terrain.

- **2026-11-20 | PRISM1 | Source implemented.** Added the Tier 2 Prism Lance, batched charge and discharge visuals, a nearest-first piercing shot with a per-tick budget, and Fabricator production. Awaiting owner gameplay and FPS validation.

- **2026-11-20 | Hexagon armour mesh | Source implemented, owner visual/FPS validation pending.** Added optional six-panel raised armour and a dark central inset to the shared Hexagon mesh, independent of Circle's enhancement toggle. Preserved MultiMesh rendering, previews, FX2 cracks, unit stats and collision footprint; no runtime Godot or FPS tests performed.

- **2026-11-20 | Battlefield weather | Source implemented.** Added live mist density, drift speed and haze controls to Benchmark and Preset Battle playtests. The weather uses one optional depth-aware camera pass.

- **2026-11-21 | Battlefield weather | Source implemented.** Blended drifting cloud layers and added feathered river mist from the compiled water footprint within the existing camera pass.

- 2026-11-21 — SHIELD1 — Implemented shared ally shields, the buildable Shield Generator and trainable Mobile Generator, batched shield-hit shells, and playtest controls. Catalogue references checked; Godot gameplay and performance await owner validation.

- 2026-11-21 — SHIELD1 — Enabled standard melee targeting and light attacks for the Mobile Generator, and reduced its base movement speed from 2.4 to 1.8. Source reviewed.

- 2026-11-21 — SHIELD1 — Enlarged shield shells slightly and increased cyan opacity at low charge so shields read at battlefield zoom. Source reviewed.

- 2026-11-21 — SHIELD1 — Tinted shield shells and impact flashes from the faction palette and brightened the shell edges. Source reviewed.

- 2026-11-21 — SHIELD1 — Replaced faceted shells with fitted round plasma bubbles in one shared MultiMesh. Added drifting faction-tinted rims. Source reviewed.

- 2026-11-21 — LEVEL-EDITOR — Added absolute Set Height painting with adaptive curved transitions, finer manual strength steps, and per-click undo. Source and falloff reviewed.

- 2026-11-21 — BENCH0 — Added playtest orthographic tilt and perspective free roam with right-drag look and keyboard flight. Source reviewed.

- 2026-11-21 — NAV — Use movement obstacles for melee contact across painted water so units can pursue a route around the bank. Projectile sight retains its existing rules. Source reviewed.

- 2026-11-21 — NAV — Fall back to a budgeted individual route when both shared route resolutions are unusable, avoiding permanent shared-cache stalls at obstacle corners. Source reviewed.

- 2026-11-21 — Terrain — Apply the Benchmark steel-slate material to campaign ground and painted elevation, with the map outline and editor grid. Source reviewed.

- 2026-11-21 — NAV — Hold for a verified detour instead of advancing into a blocked corridor. Added playtest counters for route waits and cached stalls, plus individual route failure details. Source reviewed.

- 2026-11-21 — NAV — Sample eight alternate attack bearings on failed combat route builds and report the number of valid attack points on individual failures. Revalidate cached attack endpoints against the current attack distance.

- 2026-11-21 — TOWER-STD2 — Shared tower initialization and explicit weapon dispatch. Authored walls now pass their world-space vertical bounds to existing airborne collision. Updated the tower standard with audit findings and runtime checks.

- 2026-11-21 — NAV — Allow outward movement from initial obstacle-clearance overlap and probe short, full-footprint escape moves after failed individual routes. Existing route budget retained.

- 2026-11-21 — NAV — Handle near-tangent obstacle contact in route recovery and report whether an exit was found. Normal corridor clearance is unchanged.

- 2026-11-21 — EDITOR-LEVEL-PICKER — Replaced campaign and level dropdowns with two mode tabs and a non-scrollable responsive grid of direct-open level buttons. Preserved new-level, current-map return and unsaved-session confirmation. Source reviewed; awaiting owner validation.

- 2026-11-22 — UNIT-BALANCE — Doubled Prism Lance visual/collision scale (1.0→2.0), advanced physical Size Tier (2→3), and doubled its pooled beam flash (0.18→0.36 s) without changing one-tick damage. Renamed Chevron Heavy Artillery to Light Artillery, preserving stable ID and all other stats, and halved its Artillery Shell blast radius (7→3.5) while retaining the 31.25% edge-damage fraction. Updated relevant current documentation; owner Godot validation pending.
- 2026-11-22 · Broadside Artillery Tower — Renamed the Rocket Tower, set its targeting range to 18–72 units, increased its projectile arc by 50%, and pitched both barrels up 15° with matching muzzle and recoil transforms. Source implemented, awaiting Godot playtest.

- 2026-11-22 · CAMPAIGN1-A — Added Preset Battles Headquarters, profile-specific 500-Credit saves, permanent roster recruitment and legacy completion migration. Source implemented, awaiting owner validation.

- 2026-11-22 · CAMPAIGN1-B — Connected owned-roster Blue deployment to authored Red forces and the shared battle simulation, with results and retry. Source implemented, awaiting owner validation.

- 2026-11-22 · CAMPAIGN1 — Fixed the deployment toggle signal binding and qualified the save-lock file access type to resolve C# build errors.

- 2026-11-22 · AutoBattler — Renamed the campaign, updated scene and editor references, and corrected the duplicate level order that blocked the Campaign menu.

- 2026-11-22 · AutoBattler — Replaced recruitment rows with compact responsive cards and fixed quantity toolbar wrapping.

- 2026-11-23 · AutoBattler — Improved recruitment readability with available and locked sections, aligned price and ownership, and compact recruit actions.

- 2026-11-24 · AutoBattler — Reset authored maps to blank editable scenes with centered deployment dividers. Original maps are backed up on `backup/autobattler-maps-2026-11-24`.

- 2026-11-24 — Obliterator. Added a Tier 2 single-target melee unit and a reusable first-hit multiplier per attacker and defender. Source implemented, awaiting owner validation.

- 2026-11-24 — Enabled structure targeting for Twin Rocket Launcher and Light Artillery at their existing weapon ranges. Reuses shared structure acquisition and projectile damage. Source implemented, awaiting owner validation.

- **2026-11-24 | Artillery tuning | Source implemented.** Reduced only Broadside Artillery Tower's projectile arc multiplier from 1.5 to 1.1 (18 → 13.2 maximum arc rise). Light Artillery's existing Chevron barrel now elevates 30° via an editable unit property, with matching shared mesh, projected preview and actual raised launch origin. Preserved full-length separation clearance, projectile definitions, targeting, attack stats and other units. Godot visual/runtime validation pending.

- 2026-11-24 — Added budgeted static-tower approach checks and temporary per-soldier tower skips. Moved Benchmark faction selection and defense brushes into the placement bar and added a Game Editor return button. Source implemented, awaiting owner validation.

- 2026-11-24 — Added Benchmark faction color buttons, tinted silhouettes, and compact placement cards with base HP and damage. Source implemented, awaiting owner validation.

- 2026-11-24 — Added the AutoBattler Level Editor placement palette with Units, Defenses, Terrain, and Setup tabs. Shares Benchmark placement card styling and preserves authored Red placement. Source implemented, awaiting owner validation.

- 2026-11-24 — Added optional independent AutoBattler Blue reserves and Red authored reinforcements, with battle-clock arrivals and edge placement validation. Source implemented, awaiting owner validation.

- 2026-11-24 — Added Red reinforcement staging-grid painting, validated battlefield entry previews, and ordered off-map marches with footprint-based combat handoff. Source implemented, awaiting owner validation.

- 2026-11-24 — Batched Level Editor paint previews to reduce Godot render-resource churn during drag strokes, skipped no-op brush updates, and flushed pending paint before recording undo. Crash mitigation, awaiting owner validation.

- 2026-11-24 — Kept forest preview nodes, meshes and materials alive across brush updates to address repeated Godot .NET disposal crashes. Windows verification pending.

- 2026-11-24 — Added a top-right elapsed simulation clock to AutoBattler campaign battles and Level Editor playtests. Source implemented, awaiting owner validation.

- 2026-11-24 — Moved AutoBattler reinforcement settings and authoring actions into the bottom Simulation tab.

- 2026-11-24 — Sized ranged recoil for all battle slots including reserves, and clipped Red ingress meshes at the battlefield edge. Runtime validation pending.

- 2026-11-24 — Slowed painted reinforcements to formation pace and gave all ranks a common combat handoff line to reduce bunching. Runtime validation pending.

- 2026-11-24 — Moved deployment brush controls beside unit selection and reused Fortress Defense footprint outlines for brush and rectangle previews. Runtime validation pending.

- 2026-11-24 — Reused Level Editor unit and tower preview batches across placement edits and skipped unrelated scenery rebuilds on ordinary placement. Windows crash validation pending.

- 2026-11-24 — Added opt-in Benchmark directional lighting and real-shadow comparisons with adjustable shadow distance, shared unit materials, and shadow-receiving ground. GPU performance and visual validation pending.

- 2026-11-24 — Added per-level base terrain, industrial walls with solid closed-loop fill, and updated water, boulder and pine visuals. Runtime validation pending.

- 2026-11-24 — Enabled Light Artillery-style visual firing recoil on Twin Rocket Launcher (0.35-unit kick, 0.4-second recovery). Uses existing shared recoil system; combat stats and simulation positions unchanged. Runtime validation pending.

- 2026-11-24 — Moved Industrial Walls to Terrain with single-cell, line, and rectangle brushes. Removed the rubble shelf, varied the boulder meshes, and added batched directional scenery contact shadows. Godot .NET runtime validation pending.

- 2026-11-24 — Removed automatic Industrial Wall loop fill from editor previews and battles. Rectangle Fill still paints solid blocks. Godot .NET runtime validation pending.

- 2026-11-24 — Added shaded steel panel bevels and amber wall lights, plus beveled basin rims, cyan retaining-wall lights, and water caustics. Godot .NET visual validation pending.

- 2026-11-24 — Broadside visual pass: added shared chamfered charcoal armor, faction-colored plating and trim, reinforced rotating head and twin barrels using existing MultiMesh batches. Preserved gameplay, footprint, muzzle alignment and independent recoil; Godot visual validation pending.

- 2026-11-24 — Made amber wall lights sparse and deterministic, deepened industrial ground shading, and added baked ground shadows along exposed wall edges. Godot .NET visual validation pending.

- 2026-11-24 — Broadside visual pass 2: strengthened corner armor, turret silhouette and barrel housings; added faction-emissive power rails and status strips using the same shared batches. No weapon, footprint or global glow changes; Godot visual validation pending.

- 2026-11-24 — Applied the shared directional sun and lit unit materials in AutoBattler and Fortress Defense, keeping fake ground shadows. Added Offense as an empty campaign menu entry. Godot .NET runtime validation pending.

- 2026-11-24 — Added directional shading to shared defense tower armor, including Broadside, while retaining emissive trim and fake ground shadows. Godot .NET visual validation pending.

- 2026-11-24 — Reduced Obliterator size multiplier from 1.0 to 0.9 and Prism Lance from 2.0 to 1.6 (10% and 20% smaller, respectively). Existing size tiers and authored combat stats unchanged. Configuration-only source review; in-game validation pending.

- 2026-11-24 — Added battlefield sun shading to painted rocks, trees, and industrial walls. Preserved fake shadows, canopy fading, and wall lamps. Godot .NET visual validation pending.

- 2026-11-24 — Added live sun strength, direction and material ambient fill controls to AutoBattler playtests. Fake shadows remain enabled. Godot .NET runtime validation pending.

- 2026-11-24 — Lifted shaded forest foliage and Broadside neutral steel at reduced ambient fill. Godot .NET visual validation pending.

- 2026-11-24 — Tuned pine crowns for greener sun-facing facets while restoring the previous shade fill. Godot .NET visual validation pending.

- 2026-11-24 — Shaped fake scenery shadows using a shared pine silhouette and terrain-following projected boulder outlines. Godot .NET visual validation pending.

- 2026-11-24 — Railgun visual pass: added a two-tone compact armored chassis, vented rotating carriage and emissive split-rail accelerator using the existing batches and muzzle/recoil transforms. Shared Broadside geometry helpers/material and added WORLD/BUILDINGS/authoring.md; Godot visual validation pending.

- 2026-11-25 — Added batched warm flight glare and lightly faction-tinted additive trails to rockets and artillery shells.

- 2026-11-25 — Strengthened team color on rocket and shell trails. Gave Broadside a dedicated faceted warhead and short batched flight smoke, retaining its flight and impact settings.

- 2026-11-25 — Light Artillery visual experiment: layered neutral-metal chevron hull, faction armor and emissive barrel accents behind the existing enhanced-mesh option. Shared UV mask preserves instance color/fracture buffers, lighting and reinforcement clip; gameplay and muzzle unchanged. Godot validation pending.

- 2026-11-25 — Added a pulsing faction hologram over painted deployment zones, softened the placement grid, and animated freshly painted units from small to a 110% overshoot before settling.

- 2026-11-25 — Shield Generator visual pass: shared two-tone radial reactor model and faint faction-colored coverage dome using compiled radius, a lazy compact batch, existing tower lifecycle and independent Benchmark visibility control. No shield mechanic or balance changes; Godot visual/GPU validation pending.

- 2026-11-25 — Light Artillery color refinement: faction-painted main chevron hull and barrel receiver with neutral-metal mechanical details retained. No geometry, material pipeline, other unit or gameplay changes; owner visual validation pending.

- 2026-11-25 — Reused explosion and shockwave effects for explosive structure hits, added batched armor chips and progressive defense cracks using the unit crack mask, and increased Broadside minimum range from 18 to 27. Light Artillery now uses a smaller version of the same warhead mesh with its own flight and damage settings. Godot .NET visual validation pending.

- 2026-11-25 — Made freshly painted unit previews pop faster with a 122% overshoot and a small rebound. Godot .NET visual validation pending.

- 2026-11-25 — Moved faction visual authoring to VISUALS/authoring.md and expanded it with faction-dominant mobile unit rules, separate unit/tower material and damage-feedback contracts, and mass-army performance guidance. Updated live documentation links; documentation only.

- 2026-11-25 — Staggered rectangle placement previews outward from the drag origin and reduced placement bounce speed to 75%. Godot .NET visual validation pending.

- 2026-11-25 — Set battlefield lighting defaults and AutoBattler lighting reset to 350% sun and 40% ambient fill.

- 2026-11-25 — Twin Rocket Launcher enhanced visual pass: layered faction-painted deep fork, gunmetal underframe, stepped rear deck and recessed twin launcher collars. Reuses the unit UV armor pipeline, authored mount poses and existing shared batches; gameplay unchanged. Owner validation pending.

- 2026-11-25 — Rebuilt bridges with terrain-following steel decks, beveled rails and amber markers. Replaced endpoint and width selection with a single rectangle drag. Bridge footprints still save as rectangles. Godot .NET visual validation pending.

- 2026-11-25 — Added terrain-following dirt and grass beneath painted trees, with continuous adjoining cells and irregular feathered forest edges. Godot .NET visual validation pending.

- 2026-11-25 — Enabled right-click bridge removal in Draw Bridge mode, with undo and redo and right-click cancellation during a placement drag.

- 2026-11-25 — Blended melee hit flashes 60% toward white to preserve faction readability. Godot .NET visual validation pending.

- 2026-11-25 — Added grounded attack scale pulses, a 35% melee hit tint, and a capped shared batch for clash sparks, directional cleave slashes, and cavalry charge dust. Godot .NET visual and performance validation pending.

- 2026-11-25 — Increased unit attack pulses to 15% and Diamond Cavalry top speed from 9.0 to 10.8. Cavalry moving in or out of combat now leaves batched dust that grows with actual speed.

- 2026-11-25 — Grouped lighting and fog controls in the left Rendering panel and revised painted water with moving ripple normals, viewing-angle reflections and directional glints. Godot .NET visual validation pending.

- 2026-11-25 — Railgun directed-energy visual refinement: tapered pedestal, narrow cradle, aft capacitor bank, four emissive induction coils and recessed focusing muzzle. Preserved shared armor material, footprint, exact beam origin and recoil transforms; no gameplay or extra batches. Owner visual validation pending.

- 2026-11-25 — Refined the enhanced Twin Rocket Launcher silhouette with tapered fork tips, swept shoulders and prong armor, a deeper visual notch, compact rear deck and tapered muzzle collars. Retained both authored mount poses, original collision outline, material colors, triangle topology and all gameplay. Owner visual validation pending.

- 2026-11-25 — Added a white-hot Railgun beam with faction glow, sustained muzzle glare and flickering impact sparks at observed contacts. Effects use capped shared batches. Godot .NET visual validation pending.

- 2026-11-25 — VIS-CIRCLE — Added an independent, default-on static Circle robot mesh trial with faction paint, visor, compact limbs and energy blade; visual-only heading uses existing movement/target data. Previous Circle mesh remains selectable; stats, footprint and other units unchanged. Source review only; owner visual/FPS validation pending.

- 2026-11-25 — VIS-CIRCLE-SWING — Optional 0.16-second procedural sword/forearm swing for the Circle robot, driven by existing attack feedback and tagged mesh vertices in shared lit/unlit shaders. Preserves fracture data and instance stride; no skeletal animation or combat changes. Awaiting owner visual/FPS validation.

- 2026-11-25 — Used each selected unit’s hull outline for deployment ghosts and halved both Twin Rocket Launcher mounts’ flight arcs. Godot .NET visual validation pending.

- 2026-11-25 — VIS-CIRCLE-WALK — Circle robot sword swing slowed to half speed (0.32s, original attack pulse unchanged); independent distance-driven alternating boot gait and restrained torso bounce with stable per-unit phase offsets and settling. Extra custom data is limited to opted-in Circle batches; shield shell indexing follows each batch stride. No combat or footprint changes. Source review only; owner visual/FPS validation pending.

- 2026-11-25 — Circle Infantry's robot mesh now uses turn-limited combat facing at 240°/s. It faces its target before swinging and follows deliberate movement while marching. Separation nudges no longer directly steer the renderer. Godot .NET visual validation pending.

- 2026-11-25 — Restored the Circle gait update omitted during the facing refactor.

- 2026-11-25 — Added a mipmapped forest-floor detail texture, continuous industrial wall tops, occasional loose stones in rock patches and batched amber wall-lamp ground spill. Godot .NET visual validation pending.

- 2026-11-25 — Replaced Strong with a live enemy separation toggle using the same pair scan. Allied contacts keep Normal separation and cavalry stops at surviving enemies after any resistance-based shove while the toggle is on. Godot .NET validation pending.

- 2026-11-25 — Removed the stray enemy correction from the allied yield helper to restore compilation.

- 2026-11-25 — Added AutoBattler deployment selection, editable attack-move routes, timed holds, and a local detection preview in the runtime Entity Editor. Godot .NET validation pending.

- 2026-11-25 — Preserved deployment formation offsets along opening routes, moved orders to a selection popup, added direct paint mode buttons, shortened Marksman, and added ranged attack rings to the Entity Editor. Godot .NET validation pending.

- 2026-11-25 — Opening attack-move curves now use pass-through waypoints and preserve marching speed between samples. Final arrival allows a small margin so rounding cannot prevent an order from completing. Godot .NET validation pending.

- 2026-11-25 — Added a live animated/classic Circle Infantry mesh switch to AutoBattler and the Benchmark. The switch refreshes the army renderer and materials while preserving the running simulation. Godot .NET validation pending.

- 2026-11-25 — Renamed the AutoBattler campaign display to Clash. The Level Editor can box-select starting Red soldiers and save opening Move and Hold orders that run in campaign battles and editor tests.

- 2026-11-25 — VIS-SQUARE — Added independently toggleable heavy Square infantry mesh (faction armor, shield and hammer), 0.40s procedural strike and distance-driven staggered heavy gait with smaller bounce. Shared Circle shader, attack feedback and per-batch gait data extended to Square; legacy mesh, Circle tuning and gameplay stats retained. Source review only; owner shader/visual/FPS validation pending.

- 2026-11-25 — VIS-HEXAGON — Added independent tier-3 siege robot mesh and walking/attack toggles. Central rotary blade sweeps alongside existing cleave contact effect; real radial stomp event drives separate bracing pose. Shared infantry animation data/armor pipeline reused; both older Hexagon meshes, damage cone, shockwave, cooldown, stats and footprint retained. Source review only; owner shader/visual/FPS validation pending.

- 2026-11-25 — VIS-TRIANGLE — Added independently toggleable light Triangle Archer robot mesh, 0.24s linear carbine recoil and distance-driven staggered gait; reused shared infantry animation/armor pipeline. Legacy mesh, long Triangle, projectile launch, aim spread, forest deflection, stats and gameplay retained. Source review only; owner shader/visual/FPS validation pending.

- 2026-11-25 — VIS-PENTAGON — Added independently toggleable tier-2 Pentagon Elite robot mesh, 0.46s shoulder-pivot energy axe cleave motion and distance-driven staggered heavy gait. Reuses shared infantry animation/armor pipeline; original mesh, existing cleave effect, damage cone, timing and gameplay stats retained. Source review only; owner shader/visual/FPS validation pending.

- 2026-11-25 — Redesigned Prefab Shelters into three compact 2×2 habitation variants with taller walls, airlocks, windows and distinct roofs. Stable grid-based variant selection, lazy per-design batches and preserved damage/erase lifecycle. Existing placements use the smaller footprint; no save schema or combat changes. Owner validation pending.

- 2026-11-25 — VIS-DIAMOND — Added independently toggleable Diamond Cavalry mechanical lancer mesh, +X four-leg distance-driven staggered gallop and 0.22s lance thrust on existing melee/trample feedback. Shared infantry animation/armor pipeline reused; original diamond, charge, separation, dust, contact effects and gameplay stats unchanged. Source review only; owner shader/visual/FPS validation pending.

- 2026-11-25 — Added a polygon Cliff Painter to the Level Editor. Closed outlines create a flat raised rock cap, rounded cliff corners, and height-aware projectile cover along the blocked rim. Undo and right-click removal restore the underlying terrain.

- 2026-11-25 — VIS-H-BREACHER — Added independently toggleable H-frame demolition walker mesh with twin synchronized 0.48s hydraulic ram animation on existing unit/structure attacks and distance-driven staggered four-pad gait. Reused shared armor and animation pipeline. Legacy mesh, body lunge, structure targeting, damage and gameplay footprint unchanged. Source review only; owner shader/visual/FPS validation pending.

- 2026-11-25 — VIS-MARKSMAN-PIKE — Added independently toggleable Long Triangle precision rifle robot and T-shaped Pikeman spear robot, 0.34s firing recoil and 0.30s melee thrust/bracing animation, with individual distance-driven staggered gaits. Shared infantry armor/animation pipeline reused; legacy meshes, original shot origin, recoil, anti-cavalry multiplier, stats and gameplay footprints retained. Source review only; owner shader/visual/FPS validation pending.

- 2026-11-25 — VIS-OBLITERATOR-PRISM — Added independently toggleable Obliterator shock infantry and Prism beam walker models, 0.36s powered blade thrust and 0.42s rail recoil, actual Prism charge-driven rail convergence, shared staggered gait. Preserved original meshes, size, first-hit/lunge, beam origin, charge, flash, damage and gameplay. Siege Ram untouched. Source review only; owner shader/visual/FPS validation pending.

- 2026-11-25 — Entity Editor: narrowed the unit picker and added temporary mesh previews on hover or keyboard focus. Source implemented, awaiting Godot .NET validation.

- 2026-11-26 — OFFENSE-HUD — Added a separate enemy Citadel HP strip beneath the army balance HUD, bound to actual objective health and refreshed at result. Resolve Offense victory/defeat at the tick boundary before destruction cleanup/navigation rebuild, stop remaining frame ticks and block accidental resume of a finished battle; reset on retry. Guard objective slot bounds. Screenshot showed repeated ArgumentOutOfRangeException without expanded stack; exact exception source not confirmed. Source review only; owner reproduction pending.

- 2026-11-26 — VIS-OBLITERATOR-RAM — Refined optional Obliterator assault mesh into a narrower upright biped (18% torso width reduction, shallower chest, slimmer shoulders, single spine pack, narrow pointed dagger), tighter gait and shorter cosmetic thrust; same height, attack timing and legacy toggle. Extended Siege Ram chassis/tracks 12% toward the rear, preserving front contact and piston motion. No stats, collision or catalogue changes; source review only, owner visual/clearance validation pending.

- 2026-11-26 — VIS-TRIANGLE-BOLT — Reduced basic Triangle Archer LaserBolt visual mesh from 0.0675 × 0.0675 × 0.51 to 0.045 × 0.045 × 0.44 units (one-third thinner, approximately 14% shorter). Long Marksman bolt, faction tint, flight, hit detection and damage unchanged. Resource/source review only; owner visual check pending.

- 2026-11-26 — AIR1-A: added authored Clash V-wing flights, separate air grid and fixed-step dogfights, variable altitude, and batched wingtip wisps. Source implemented, awaiting Godot playtest.

- 2026-11-26 — AIR1-A authoring: moved V-wing placement into the Clash Units roster, with faction selection, painted and erasable starting positions, preview, and undo/redo. Old flight counts remain readable. Awaiting Godot playtest.

- 2026-11-26 — Clash Units roster: added distinct Blue and Red V-wing cards with faction-tinted aircraft icons.

- 2026-11-26 — AIR1: shared unit definitions now drive aircraft cards, typed map starts and air combat stats. Added Blue aircraft painting to Test Clash deployment with faction colors and test-only placement.

- 2026-11-26 — Added capped, terrain-following ground glow to Triangle Archer laser bolts. Awaiting in-game visual validation.

- 2026-11-26 — Benchmark now defaults to a tier-weighted mixed roster with a single Dreadnaught and a small aircraft flight per side.

- 2026-11-26 — Added the per-unit Pulse on attack option with Dreadnaught disabled, plus the Clash Heavy Bomber, ground-only auto-fight, and editable opening bombing runs.

- 2026-11-26 — Heavy Bomber commits to a ground position before each pass, uses wider turns, and exposes a Bomb order in Clash authoring and test deployment.

- 2026-11-26 — Clash Level Editor authors Red aircraft only. Blue aircraft placement stays in Test Clash deployment.

- 2026-11-26 — AIR-VIS1 — Added independently toggleable faceted V-wing interceptor and Heavy Bomber airframes with shared faction armor material, cockpit/gun/exhaust detailing and bomber engine nacelles/payload-bay inset. Cached meshes shared by editor/deployment and battle; original wingtip/tracer anchors, flight and combat untouched. Legacy/custom meshes retained. Source review only; owner shader/visual validation pending.

- 2026-11-26 — Offense Blue attackers now use local defender acquisition so the default movement goal remains the Red Citadel between fights. Opening orders retain priority.

- 2026-11-26 — Added standalone Skirmishes menus and authoring destinations, preset rosters, and short Defense encounters with a Blue Citadel. Campaign retains recruitment and Fortress Defense has its own menu.

- 2026-11-26 — Level Editor now opens to Skirmishes and offers Fortress Defense as its other authoring mode.

- 2026-11-26 — SIEGE-RAM-SIZE — Owner-requested Tier-3 Ram enlargement: SizeMultiplier 1.5 and SiegeRam outline length proportion ×17/15, yielding +50% width/height and +70% total length versus the previous model, including existing rear extension. Compiled circular footprint radius 1.68 → 2.856. No change to tier, health, damage, raw reach, speed or attack timing; footprint-aware reach and navigation now follow the larger outline. Source review only; owner placement/bridge/structure-contact validation pending.

- 2026-11-26 — Army balance HUD now includes living aircraft in faction counts and bar shares, including air-only battles.

- 2026-11-26 — Aircraft cast offset ground silhouettes whose separation grows with flight height.

- 2026-11-26 — AIR-VIS2 — Upgraded default Heavy Bomber payload to a dark faceted finned model; added independently toggleable twin emissive flames and capped 1024-puff world-space dark smoke batches aligned with the existing nacelles. Preserved latest aircraft shadows, original payload, flight, targeting and blast settings. Source review only; owner Godot visual/shader/FPS validation pending.

- 2026-11-26 — Added Twin Minigun Tower: compact two-tone twin rotary clusters, shared direct-fire targeting and pooled alternating minigun rounds, source-footprint exclusion and terrain clipping, catalogue/editor/Fortress Defense registration. Provisional balance; owner Godot validation pending.

- 2026-11-26 — Added one short V-Wing engine flame to the shared aircraft exhaust batch. Both aircraft types now have hot cores and faction-tinted outer plumes. Engine emission ends on death and existing neutral bomber smoke fades in place.

- 2026-11-26 — Restored Red aircraft cards and placement previews in the Skirmish Level Editor by recognizing the tactical campaign IDs used by Skirmishes.

- 2026-11-26 — Added Tier 2 Interceptor Infantry with Strong Infantry melee, a rear AA launcher, and pooled guided missiles with faction-tinted exhaust and a six-second airburst fuse.

- 2026-11-26 — Added subtle faction tint and existing travelling ground glow to shared minigun rounds, enlarged Railgun/Twin Minigun flashes, and added a larger Broadside muzzle starburst plus capped faction-colored ground halo on successful launch. Visual-only; owner Godot validation pending.

- 2026-11-26 — Strengthened Railgun beam faction tint and added a terrain-sampled additive ground streak along the live clipped beam, sharing the existing dense beam lifecycle with a bounded lazy batch. No combat changes; owner Godot validation pending.

- 2026-11-26 — Integrated aircraft crashes with the shared lethal-hit path for dogfights and Interceptor missiles. V-Wings and Heavy Bombers fall with fire and smoke, burst on impact, and deal enemy ground splash damage before battle results resolve.

- 2026-11-26 — Shortened normal Heavy Bomber exhaust to a compact, translucent haze near the engine flames. Crash smoke remains separate.

- 2026-11-26 — Added click-to-inspect towers in tactical editor tests. Selection shows a footprint highlight, health bar, kills, and a lightly filled range circle. Kill credit follows tower beams and pooled projectiles, including splash.

- 2026-11-26 — Increased Twin Minigun Tower maximum range from 26 to 39 (1.5×) and Railgun Tower maximum range from 36 to 50.4 (1.4×). Minimum ranges and all other authored stats unchanged. Configuration-only source review; gameplay validation pending.

- 2026-11-26 — Softened the Railgun ground illumination with a rounded capsule falloff at both ends, retaining central brightness, width, faction tint, terrain fade and existing batch layout. Shader-only visual change; owner validation pending.

- 2026-11-26 — Added per-unit blocking wrecks for Tier 3 and above, enabled on Hexagon Siege. The dark, tilted hull settles into the terrain and joins the ground navigation field. Wreck rendering and route updates are batched and capped for initial testing.

- 2026-11-26 — Added temporary ground scorch marks for explosive projectile impacts and aircraft crashes, using a capped MultiMesh with slow fade updates and terrain sampling at impact.

- 2026-11-26 — Added Missile Barrage Tower (34 range, 12-round magazine, 6 shots/s, 8 s reload) and Long-Range AA Tower (95 range, heavy 2.2× missile, 6 s interval), sharing existing guided Interceptor missile pool and air grid. Added faction-armored vertical launch meshes, catalog/placement/inspection/Entity Editor and Fortress build registration. Aircraft combat runs in existing AirSquadron-enabled battles; Fortress air waves not implemented. Godot validation pending.

- 2026-11-26 — Shared the Railgun ground glow with Prism Lance and softened both beam tips. Reduced the Prism firing cycle from 9 to 6 seconds by shortening recovery. Increased Siege Ram speed from 1.4 to 1.54.

- 2026-11-26 — Ground units now route through strictly lower-tier wrecks and remove them on physical contact. Same-tier and heavier wrecks still block movement.

- 2026-11-26 — Explicitly classified V-Wing as Size Tier 1 and Heavy Bomber as Size Tier 2.

- 2026-11-26 — Anti-air towers now emit artillery-style smoke from the launch socket on successful guided missile launches. Barrage puffs use 0.55× scale and the long-range plume uses 1.60×, sharing the existing muzzle-smoke batch.

- 2026-11-27 — Added Conquest and Missions under Campaign on the profile menu. Conquest has a placeholder page, Missions opens the existing campaign entries, and Skirmishes remains directly accessible.

- 2026-11-27 — Added Skirmishes and Missions selectors above the Clash, Offense, and Defense tabs in the Level Editor. Both use their existing separate save destinations. Fortress Defense retains its own level list.

- 2026-11-27 — Added the Tier 2 Rally Commander with a tall pennant and an area pulse that temporarily boosts nearby allied ground troops. The pulse shares the soldier grid and shockwave batch.

- 2026-11-27 — Added live Barrage magazine indicators, white Railgun cooldown charging, faint faction shield waves, low-health dark building smoke, sparks and tower base light flicker, pooled visual-only building destruction bursts/chips/scorch/smoke, and optional Siege Ram tread marks (Entity Editor flag). Existing gameplay and tower batches preserved; owner visual/FPS validation pending.

- 2026-11-27 — Rally Commander tuning: size multiplier 0.9 → 1.08 (+20% linear scale and footprint), rally radius 18 → 14.4 (−20%), successful pulse cooldown 12 → 8 seconds (+50% frequency). Shared pulse ring follows the new radius. Five-second non-stacking buff, 15% boost, initial delay, empty-target retry and other stats unchanged. Source review only; owner Godot validation pending.

- 2026-11-27 — MAPGEN1-A/B: Added Blank and Procedural new-level choices, seeded map previews, editable elevation/scenery, and footprint-checked River Divide bridges. Generation uses the normal editor history and save flow. Owner Godot validation pending.

- 2026-11-27 — MAPGEN1 scenery: Added whole-map forest targets, Scattered Groves and Large Forest patterns, more varied rock outcrops and rolling terrain, and actual coverage in the preview. Owner Godot validation pending.

- 2026-11-27 — MAPGEN1: Allowed Large Forest to grow through the passable center lane on dry maps, removing the straight clearing. Bridge approaches and the rock clearance checks remain in place.

- 2026-11-27 — Changed the forest maximum range penalty for ranged ground units from 50% to 20% and applied it to Dreadnaught turrets. The shared range checks use the attacker's current forest cell.

- 2026-11-27 — Increased the forest maximum attack range penalty for ranged ground units from 20% to 35%.

- 2026-11-27 — Added direct projectile body-impact sparks and cosmetic shield ricochets for Triangle Archer bolts/minigun rounds. Reuses swept contacts and actual health/shield outcomes, shared contact spark pool and one bounded reflected-tracer batch. Resource opt-in, no combat or collision changes; owner validation pending.

- 2026-11-27 — MAPGEN1: Removed the full-length rock clearance exclusion from both forest patterns on all layouts. Trees still respect water, bridges and protected areas.

- 2026-11-27 — MAPGEN1: Removed map-spanning scenery clearance strips and straight-route validation from procedural layouts. Kept local clearance for bridge decks and the starting fortress.

- 2026-11-27 — Enabled cosmetic shield ricochets for the Marksman's LongLaserBolt via the existing projectile opt-in. No gameplay or rendering-system changes; owner validation pending.

- 2026-11-27 — MAPGEN1: Smoothed River Divide’s banks along the meander and removed auto-generated bridges. Crossings can be drawn manually in the editor.

- 2026-11-27 — Added a paintable marsh surface with brush and rectangle editing, undo and saves, batched terrain mesh rendering, and tier-based ground movement penalties in Skirmish and Fortress Defense playtests. Owner Godot validation pending.

- 2026-11-27 — Added opt-in shell casing ejection and size to unit/defense definitions and Entity Editor, enabled only on Broadside Artillery. Shared lazy 256-case visual pool with brass mesh, tumble, terrain bounce and fade; successful ground projectile/Dreadnaught minigun hooks. No gameplay changes; owner validation pending.

- 2026-11-27 — Upgraded Commander rally markers to pulsing overhead faction chevrons tied to buff expiry; added 30-second burnt building husks to the existing capped wreck field, with tier-4 movement collision, higher-tier crush and placement protection. Shared rubble mesh, existing destruction effects retained; owner validation pending.

- 2026-11-27 — Restored the LINQ import required by the procedural level picker’s `Single` call.

- 2026-11-27 — Tuned procedural elevation levels below 9 for gentler relief and reduced small-scale variation. Level 0 is flat and level 1 uses one ninth of its previous relief amplitude.

- 2026-11-27 — MAPGEN1: Added a Cliffs layout with a density slider, native closed Cliff Painter outlines, preview and undo. Open Plains starts with flat ground and lets authors add elevation and scenery independently.

- 2026-11-27 — Wreck obstacle refreshes now defer costly unit-type route graphs until requested and build at most one per route update. Collision uses the refreshed obstacle field immediately. Owner performance validation pending.

- 2026-11-27 — Marsh paint now lowers the shared terrain surface by up to 0.32 world units with feathered edges, and renders with streaked muddy water and subtle ripples.

- 2026-11-27 — Shield Generator buildings now charge a shared interception dome in place of unit shield coverage. Projectile definitions select Light, Standard or Heavy contact behavior, with friendly pass-through and localized shield ripples. Source review only; Godot validation pending.

- 2026-11-27 — Increased the Shield Generator dome radius from 21 to 24.15 world units and made the dome active whenever it has positive energy.

- 2026-11-27 — RUBBLE1 / ELEV2: Added three neutral rubble paint variants with batched husks, ground slowdown and structural cover. Added configurable seven-level elevation painting on the shared heightfield, with brush, rectangle and undo. Godot owner validation pending.

- 2026-11-27 — BARRICADE1: Added faction-painted Light Barricades with fixed-step traversal damage, heavy crush, height-aware projectile interception and static batching. Navigation and targeting sight use their existing fields. Owner tuning and validation pending.

- 2026-11-27 — ROAD1: Added saved road paint with brush and rectangle editing, terrain-following industrial pavement, and a configurable 20% ground movement bonus. Pathfinding remains unchanged. Owner Godot validation pending.

- 2026-11-27 — GEO1: Added neutral, placeable geothermal vents with configurable cycles, pooled steam visuals and tier-scaled ground-unit launches. Flight uses shared terrain and obstacle checks. Owner Godot validation pending.

- 2026-11-27 — TRENCH1: Added two-click trench authoring, heightfield excavation and end ramps, movement-only tier restrictions, and ground targeting concealment with a firing reveal. Source review only; owner Godot validation pending.

- 2026-11-27 — HUD1 — Added the tactical combat HUD for Skirmishes and Missions, including fixed-step time and speed controls, Citadel health, cached faction statistics and an interactive terrain/density minimap. Source implemented pending Godot visual and large-battle validation.

- 2026-11-27 — TRENCH1 — Fixed a compile error by passing the concealment flag through recursive pursuit searches.

- 2026-11-27 — Fixed trench placement on mouse release and added a Scenery palette for persistent, crushable destroyed buildings using the shared wreck mesh and navigation system.

- 2026-11-27 — CLIFF2: Increased deterministic cliff-face relief and broad irregular buttresses; added varied stone strata and plateau surface detail. Authored rim, plateau height and navigation unchanged. Source review only; owner visual validation pending.

- 2026-11-27 — CLIFF2 — Prevented painted ground-cover triangles from bridging cliff tops and lower terrain.

- 2026-11-27 — Narrowed and lined trenches with steel and asphalt, moved their entries to opposite sides, and added a Tier 4 direct-fire tank with a tracked chassis and firing recoil.

- 2026-11-27 — Fixed editor ground material updates overwriting steel and asphalt trench lining, and narrowed the side ramp openings.

- 2026-11-27 — Gave the tank a recoiling barrel, hull pitch, track smoke and a 3-unit shell blast, and disabled its firing size pulse.

- 2026-11-27 — Enforced a two-tier shove limit in crowd separation, allied yielding and cavalry charges.

- 2026-11-27 — TANK-VIS1 — Added optional sleek faceted Tier 4 tank hull/turret, layered track skirts, rear vents and segmented recoiling gun with original muzzle/footprint. Enabled shared shell casing ejection at 0.8× from hull-facing turret port on successful shots. Original model selectable; recoil, chassis pitch, smoke, tread marks and combat unchanged. Source review only; owner Godot visual and casing alignment checks pending.

- 2026-11-27 — TURRET1 — Added independent primary turret aiming, authored Tank mount and batched turret visuals, Entity Editor mount controls, and append-only Dreadnaught reinforcement state. Awaiting Godot validation.

- 2026-11-28 — Increased stationary Shield Generator radius by 20% (24.15 to 28.98 world units), keeping dome visuals and gameplay coverage/interception aligned. Mobile Generator and other shield stats unchanged.

- 2026-11-28 — TREE visual polish: Added deterministic taller pines, varied crown proportions and yaw, and irregular staggered branch tiers using the existing batches. Preserved forest gameplay, canopy fade and world-direction sway. Source review only; owner visual validation pending.

- 2026-11-28 — Reduced Tank size multiplier by 10% (1.55 to 1.395) and shifted enhanced hull, turret housing and track skirts to gunmetal with retained faction glacis, roof and central skirt accents. Existing turret scaling, Tier 4, combat stats and other units unchanged. Source review only; owner validation pending.

- 2026-11-28 — Added an independently aimed short-range roof minigun to the Tank, with a separate cooldown and shared projectile and rendering systems.

- 2026-11-28 — Added unlimited airstrike and repair pod calls to the Level Editor Clash sandbox. Support craft and drones are untargetable visual objects.

- 2026-11-28 — UNDERGROWTH1 — Added Grassland, Rocky and Marsh paint presets with chunked decorative meshes, terrain and building masking, save data and editor stroke history.

- 2026-11-28 — EDITOR-UNDO1 — Added compact toolbar history buttons with shortcut tooltips and live enabled states using the existing editor history.

- 2026-11-28 — Added UNIT-STD1, a mandatory unit creation standard grounded in the current ground/air roster, compiled catalogue, combat/mount paths, visual opt-ins, editor and mode integration. Linked from root guidance, unit README and visual authoring guide. Documentation only; no runtime or balance changes.

- 2026-11-28 — Added provisional Tank Fabricator recipe (1 / 220 Carbon / 22s) and allowed intentionally absent recipes to remain unavailable instead of preventing catalogue compilation. Upgraded Mobile Generator to two-tone radial reactor carrier, sharing animated faceted energy core with stationary Shield Generator through existing mesh/material batches. No shield/combat changes; owner validation pending.

- 2026-11-28 — IMPACT1 — Added configurable directional Heavy-projectile rocking for surviving, unshielded Tier 2+ units using the existing damage and render passes.
