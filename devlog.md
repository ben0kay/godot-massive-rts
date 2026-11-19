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
