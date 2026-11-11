# TERR1 — Painted and Procedural Elevation (Unified Heightfield)

**Repository:** ben0kay/godot-massive-rts · **Branch:** main
**Status:** PLAN ONLY. This document does not mean elevation code is implemented.

## Goal and visual direction

Add terrain with large *genuinely flat* regions, broad gentle slopes, curved hill transitions, and flat-topped raised plateaus. The terrain must support strategic high-ground fighting without requiring noisy, rocky or detailed landscapes. Target flat battles should remain visually and computationally simple. A representative initial test is a plateau 4 world units above a surrounding flat plain, with a 20-world-unit-wide smooth slope up to it (average grade roughly 11 degrees).

The game in endgoal.md has two game modes, but they MUST share one terrain runtime:
- **Army Painter:** author-specific levels using terrain painting, flatten, raise/lower, plateau and ramp brushes.
- **Open RTS:** deterministic seeded generation with low-frequency noise, broad flat start/build zones, optional plateaus and soft blending. Do not apply noise to every flat area.
- **Possible hybrid later:** procedural base terrain with explicit painted height overrides/masks for strategic areas and perfectly level building platforms.
- **Benchmark:** flat at Y=0 unless a terrain test scenario is intentionally selected.

Two producers feed one compiled, immutable (during battle) TerrainHeightField. Rendering, soldier grounding, navigation, direct-fire LOS and combat advantage query the SAME height source. Do not create separate terrain renderers or movement engines for each game mode.

## Existing source and compatibility

Read the live root README, NOTES/CodePreferences.md, endgoal.md, BENCHMARK/README.md, and the current versions of:
- SIMULATION/UnitState.cs: BattlefieldHeight.At(x,z) is currently a hard-coded Y=0 static compatibility seam.
- SIMULATION/MovementSystem.cs: XZ pursuit and movement-only Goal movement, acceleration, role handling.
- COMBAT/TRAMPLE/TrampleSystem.cs: current charge/shove placement samples BattlefieldHeight.At.
- SIMULATION/SEPARATION/SeparationSystem.cs: SEP1/SEP2 can adjust XZ positions and require final ground reprojection.
- BENCHMARK/SCENARIOS/DeploymentFactory.cs: spawns set ground Y.
- RENDERING/ArmyRenderer.cs and DEPLOYMENT/DeploymentPreview.cs: instances center above authoritative ground position; preserve FB0 scale pulse.
- DEPLOYMENT/DeploymentPainter.cs: maps mouse camera ray to Y=0, which must eventually become a terrain-surface hit.
- DEPLOYMENT/DeploymentLayout.cs: soldiers are painted on 2.1-world-unit editor cells.
- COMBAT/CombatSystem.cs, COMBAT/AreaDamage.cs: attacks currently use 3D distance and existing cleave.
- PROJECTILES/Shared/ProjectileSystem.cs: shallow arcs from shooter to target, currently no surface collision.
- UNITS/CATALOG/UnitCatalog.cs: UNIT1 scaled footprints, reaches and dimensions.
- OBSTACLES/OBST1_IMPLEMENTATION_PLAN.md: **currently planned**, not available as an implementation. Share future traversal rules instead of duplicating systems.

Preserve 30 Hz fixed-step simulation, 10k vs 10k practical benchmark target, large benchmark presets, UNIT1, SEP1/SEP2, three factions, cavalry charge/acceleration, damage feedback, and MultiMesh rendering. Never add a terrain physics body, raycast or NavigationAgent3D per soldier.

## Proposed hierarchy (create files only as each phase is implemented)

TERRAIN/
- README.md, TERR1_IMPLEMENTATION_PLAN.md
- SHARED/TerrainHeightField.cs — immutable, fast static height/slope lookups from chunked samples
- SHARED/TerrainSurfaceQueries.cs — optional normals/slope/ray intersection helpers
- SHARED/TerrainCompiler.cs — compile either source into one terrain snapshot
- SHARED/TerrainSettings.cs — common Inspector settings
- PAINTED/PaintedTerrainData.cs — authored height samples/masks and versioned save data
- PAINTED/TerrainBrushes.cs — raise/lower, flatten, plateau, ramp, smooth
- PROCEDURAL/ProceduralTerrainSettings.cs — seed, low-frequency noise, flat masks, plateau parameters
- PROCEDURAL/ProceduralTerrainGenerator.cs — seed-dependent source, NOT per-frame noise
- RENDERING/TerrainRenderer.cs — stable terrain mesh chunks and simple materials

Existing DEPLOYMENT/ owns the editor-mode UI, mouse picking and map save integration. NAVIGATION/ (when implemented) owns shared routes and terrain traversal, and COMBAT/ and PROJECTILES/ own attack policy integration. Do not put all code into BenchmarkArena.

## Shared heightfield and mesh agreement

**Authoritative contract:** height at any world XZ coordinate returns the exact ground Y. Use a per-arena terrain provider, not a mutable process-wide static singleton. Flat default is height 0, and no terrain object/render chunks need processing in normal flat benchmarks.

Suggested initial terrain sample spacing: **2.1 world units**; sample resolution can be independent of painter placement cells. Suggested chunk size: **32×32 terrain cells**, with consistent shared corner/edge samples. Store only nonflat/active chunks for painted terrains; procedural terrain may compile only finite active/battlefield chunks. Do not allocate and rebuild a world-sized 5000×5000 dense terrain mesh just because the deployment grid has large bounds.

HeightAt must be allocation-free, use a few cached grid samples, and avoid FastNoiseLite calls during soldier simulation. Match CPU ground interpolation to the actual rendered triangle mesh: same cell triangle split and barycentric interpolation, OR deliberately use the same bilinear surface representation in both. Otherwise feet will float or clip at diagonal triangles.

Build mesh chunks at terrain compile time or on edited-chunk changes only; shared terrain material, low/simple color variation, appropriate culling, no one Node per height sample. Cache chunk min/max Y, optional slope/traversability mask, and a terrain revision number. Keep flat-unchanged chunks free of per-tick costs.

**Y semantics:** soldier Position.Y is ground-contact Y, not visual center. Ground reconnection happens at spawn, during final position changes, after cavalry shove/charges and after SEP1/SEP2 displacements. Avoid multiple height samples on one soldier in the same fixed tick: reproject once after all XZ correction where possible. Rendered Y center remains groundY + effectiveUnitHeight/2 times visual pulse scale. Dirty flag includes color flashes, so do not use Dirty alone to decide whether ground height must be resampled.

Smooth display interpolation between ticks can normally interpolate existing Position.Y. If steep slopes cause visible gaps, optionally sample blended XZ ground Y in renderer under a measured budget; do not force that extra height sample on every high-frame-rate render by default.

## Painted source (priority for Army Painter)

Add terrain editing as a separate editor tool mode, without breaking 3×3 soldier painting, future obstacle rectangles, faction picker, or save/replay.

Brushes:
1. Raise/lower with configurable radius and falloff.
2. Flatten to exact target height in inner region (true flat, not approximate noisy flatten).
3. Plateau: adjustable inner radius/rectangle, target elevation, and **separately controlled outer slope width** so a broad flat top transitions smoothly to lower terrain. Use smoothstep-like shoulder with zero derivative at each end.
4. Ramp: direct accessible uphill corridor between height levels, with smooth edges and adjustable width.
5. Smooth (limited/local) and Reset to flat.
6. Visible brush outline, target height, slope preview; update only touched terrain chunks when a stroke commits.

Prefer final height samples or a compact bounded authoring representation, with optional per-stroke touched-cell undo later. Avoid saving an unbounded list that must reapply thousands of strokes on every battle load. Keep authored flat cores exempt from later noise.

Current painter projects mouse to Y=0. Use one **editor-only ray-to-heightfield intersection** method for unit painting and terrain editing (e.g., coarse bounded traversal + refinement, or nearby terrain mesh intersection), not a per-soldier Godot physics raycast. Existing XZ placement coordinates can remain saved while Y is recomputed from terrain at load/spawn. Previews follow surface height.

**Save compatibility:** Deployment currently uses user://deployment.json version 1; OBST1 has a proposed version 2. Do not overwrite either without an explicit migration. For initial terrain testing, version a separate user://terrain.json linked to the chosen battlefield, or define a versioned higher-level BattlefieldDefinition when both features are ready. Old saves without terrain load as flat. Validate input size/finite heights, clip strokes and preserve stable soldier IDs.

## Procedural source (priority for Open RTS)

Use a ProceduralTerrainSettings.tres with deterministic Seed, finite bounds, sample spacing, large-scale frequency/amplitude, optional tiny fine-noise amplitude default 0, plateau/ridge number/size/height/shoulder, start-area flat radius, slope cap and map variation knobs.

Compile FastNoiseLite or another deterministic coherent noise source into the SAME chunk samples as painted maps. Low-frequency broad variations only. Explicit flat masks override noise for base-building and battle lines. One seed + identical settings must recreate the same terrain. No per-soldier noise evaluation. Keep hills wide enough for thousands of troops to move and fight.

For a later hybrid, apply protected painted absolute override and a tunable blend mask AFTER procedural generation. Do not simply sum noise over a deliberately flattened plateau. Saving the procedural source should store seed/settings plus only edited chunks/overrides, not duplicate untouched procedural height samples.

No tree spawning, biome system, buildings, economy, caves, materials or world-streaming mechanics are part of this plan.

## Slope navigation and obstacle compatibility

A raised region is not an obstacle. A gentle slope is traversable; an extremely steep rise/cliff can be marked impassable. Suggested initial **max walking slope 30°**, configurable by locomotion type later. A 4-unit rise over a 20-unit slope should be fully walkable.

Navigation must query combined conditions: **terrain slope/passability + static obstacles (from OBST1 when implemented) + compiled UNIT1 soldier radius**. Route work is cached/shared by unit mobility class or radius and destination groups, not independent A* for each of 20,000 soldiers. Full map route work is precomputed or work-budgeted, not done every tick.

Collisions and slope safeguards are correctness checks: even when no route exists, units must not teleport up cliffs or move through walls. A large Pentagon may need wider viable ramps than a Circle. Keep combat pursuit target separate from intermediate waypoint, preserve cavalry momentum and SEP2 archer yielding.

OBST1 is presently a plan. Implement interfaces compatible with later static obstacle navigation, but do **not** write a second incompatible pathfinder merely to complete initial TERR1 height work.

## High-ground fighting advantage

The first combat effect should be small, easily configurable and cheap. Suggested settings in a TerrainCombatSettings.tres:
- EnableHighGroundAdvantage: true (optional per benchmark scenario)
- MinHeightDifference: **1.0 world unit**
- FullBonusHeightDifference: **4.0 world units**
- MaximumBonusOutgoingDamage: **+10%**
- UphillSpeedPenalty: **0% initially**, optional later
- Separately optional flags for melee/cleave/trample/ranged.

Apply one damage bonus when the **attacker's actual ground Y exceeds the defender's ground Y** by more than the threshold. Smoothly ramp bonus from 0 to +10%; equal-height engagements are unchanged. Attacking uphill gets no bonus, rather than also receiving a stacked arbitrary damage penalty. A clear, slight advantage for a plateau defender is enough to start with.

Compute the height difference **at actual attack/hit resolution** (using cached ground Y), not in every target scan or frame. Ensure projectile attacks, Pentagon multi-target cleave and cavalry trample do not multiply an already-adjusted damage value again; ideally centralize one shared hit modifier at the final damage application context. Preserve armor formula and existing damage balance unless explicitly changed.

This is an original gameplay rule inspired by high-ground tactics, not an assertion of Age of Empires' exact damage values.

## Visibility and projectile behaviour on elevation

Direct-fire LOS and actual projectile collision are different:
- For direct weapons, a distant ridge that crosses the shooter-to-target sight line blocks a shot. Test **only plausible, in-range / ready-to-fire** targets using bounded terrain line samples; keep staggered acquisition. No LOS ray every tick for every soldier.
- Projectiles follow actual 3D origin/target Y and existing shallow arc; the arc does **not** automatically clear an intervening hill. Compare earliest swept terrain hit, earliest static obstacle hit, and earliest soldier hit. Prevent tunnelling through slopes.
- OBST1's proposed initial dark-gray rectangles are explicitly opaque in 2D for direct shots. Keep this policy independent of terrain ridges until future height-aware wall geometry is implemented.
- Future **indirect artillery** can ignore direct LOS but still collide with the terrain along its trajectory; do not treat artillery permission as universal terrain pass-through.

Existing CombatSystem currently evaluates full 3D distance. Define melee/ranged distance semantics deliberately when Y varies: horizontal attack reach plus a bounded vertical tolerance, or a true 3D reach with adequate handling of ramps. Never let melee hit someone many metres above or below merely because XZ coincides. Keep all chosen rules consistent with slope geometry and UNIT1 dimensions.

## Rendering & performance diagnostics

Terrain mesh built once per active chunk. No player or soldier individual physics body. Runtime static terrain not regenerated every tick. Prefer normal interpolation/vertex color shading over costly dynamic noise shaders; show contours/debug slopes only when asked.

Metrics to capture:
- one-off terrain compilation and mesh chunk generation time;
- total terrain ground queries during a tick and optional sampled CPU cost;
- navigation route/slope checks and cache misses after navigation phase;
- LOS checks, terrain blocks and projectile-ground hits after combat phase;
- normal simulation tick, movement, separation, army buffer updates, render CPU/GPU.

Compare identical layouts/seeds with **flat baseline**, **single ramp + plateau**, and **gentle procedural variation** at 1k, 10k and 20k soldiers. No performance/FPS claim without measured results on the owner's machine. No noise on flat maps. Prefer zero-cost fast-path when the heightfield is flat.

## Staged Codex implementation

**TERR1-A — Foundation (do this FIRST):** per-arena height provider with flat default, chunked static heightfield and shared terrain renderer; one simple deterministic test plateau with wide slope; spawn/movement/trample/SEP1/SEP2 reproject Y correctly, preserve flat benchmark performance, expose tiny config and counters. Do NOT build full painter/procedural/LOS yet.

**TERR1-B — Painted maps:** separate height paint tool with Raise/Lower, Flatten, Plateau/ramp and Smooth; height-aware editor picking/preview; affected-chunk rebuild; versioned persistence and old-save flat compatibility. Leave current soldier and obstacle painting controls intact.

**TERR1-C — Procedural maps:** seed + settings; smooth low-frequency hills, protected flat base/battle areas, optional broad plateaus, same heightfield and renderer; optional painted override later. No separate procedural simulation engine.

**TERR1-D — Terrain mechanics:** shared slope navigation integrated with obstacle topology when OBST1 exists; +10% configurable high-ground damage advantage; direct terrain LOS and projectile-ground swept collisions. Avoid attacking through a ridge; keep artillery indirect-fire policies extensible, not implemented.

Implement/review/commit **one phase per authorized task**; update docs and append to devlog.md for source changes only. Only one basic build if convenient; owner will test visually and benchmark. Never claim TERR1-A includes features reserved for later phases.

## Owner checks

- Flat BENCH0 still identical when terrain is disabled.
- Painted plateau has exactly flat top and broad curved ramps; no jagged hills on the flats or seams between chunks.
- Soldiers of all five types and all factions stay grounded while moving, separated, trampled or flashed; no double Y-offset.
- Big cavalry/Pentagon correctly obey UNIT1 footprint while traversing ramps, not cliffs.
- Procedural same seed/settings reproduce same landscape; generated build/start zones stay flat.
- No route through impassable cliffs; shared pathing remains budgeted as with OBST1.
- High-to-low damage bonus reaches 10% at configured height delta; flat/equal heights receive no bonus, no double modifiers.
- Archers cannot shoot through ridge terrain and arrows stop on solid ground, while indirect-fire framework remains separate.
- Compare 1k/10k/20k-unit tick timings and CPU/GPU with identical flat vs elevated scenarios.

**TERR1 is a design plan until a Codex implementation task is explicitly authorized.**
