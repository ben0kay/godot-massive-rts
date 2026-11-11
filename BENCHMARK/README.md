# BENCH0 — Source Implemented / Awaiting Owner Validation

Standalone 3D army workload arena. No performance target is claimed. No campaign, economy, research, final combat rules or advanced formation system is included.

## Launch and controls

1. Import root `project.godot` in **Godot 4.7 .NET** with its required .NET SDK installed. The standard non-.NET Godot editor cannot compile C#.
2. Build the C# project, then press **F6** with `BENCHMARK/BenchmarkArena.tscn` open, or **F5** (it is the temporary main scene).
3. Initial deployment is 1,000 render-only soldiers. Select total count, mode, layout and seed, then click **Spawn / Reset selected**. The button always rebuilds from the current controls, resetting deaths, projectiles, timings and simulation ticks.
4. **WASD / arrows** pan; **wheel** zooms; **Space** or the button pauses simulation; **F4** or the checkbox toggles the upper-right overlay. Camera remains active while paused. Click the battlefield to release keyboard focus from text/spin controls if necessary.
5. **Clear all** releases the current workload. Firing rate is live; other deployment settings take effect on spawn/reset. Pause does not interrupt a deployment already being prepared.

Supported total counts: 1k, 5k, 10k, 25k, 50k, 100k, split across blue/red. Status distinguishes requested count, prepared count and final spawned count. The overlay distinguishes spawned from currently living soldiers.

| Mode | Work performed |
| --- | --- |
| A — Rendering only | Stationary primitive MultiMeshes. No movement, grid, combat, projectile or recurring instance-sync pass. |
| B — Movement | Fixed-step marching between mirrored endpoints in separate lanes, continuously reversing at endpoints. No targeting or combat. |
| C — Melee | Grid rebuilding, staggered local acquisition, marching, health, attack cooldowns, damage flashes and deaths. |
| D — Ranged | Mixed melee/ranged force plus pooled, moving projectiles with swept intended-target hit checks and lifetime expiry. |

Layouts: **Opposing lines** uses rectangular opposing blocks; **Dense clusters** decreases spacing; **Distributed battlefield** uses seeded offsets across a wider battlefield. All remain reproducible with the same count, mode, composition, resource settings and seed. Heavy hexagonal soldiers are visual-only and behave like melee soldiers. Ranged soldiers are cubes; melee are triangular prisms.

## Configuration and ownership

`CONFIG/DefaultBenchmark.tres`, assigned to the arena's exported `Config`, is the single master tuning resource. Defaults and fields live in `CONFIG/BenchmarkConfig.cs`:

- 30 Hz simulation, at most 3 steps per rendered frame. Excess elapsed ticks are discarded and counted, avoiding unlimited catch-up spirals.
- Maximum 100,000 soldiers; prepare 4,000 array entries per frame before initializing visuals. GPU buffers and initial renderer upload are still a single finalization pass, which may briefly hitch at large counts.
- Speed 5 world units/sec; spatial cell 6; targets refreshed every 6 ticks, staggered by ID; maximum 96 enemy candidates/query.
- Melee reach 1.7, ranged reach 35; health 100, damage 25; melee interval 0.8 sec; flash 0.12 sec.
- Projectile cap 30,000; speed 45; lifetime 4 sec; hit radius 0.8. Full pools drop new shots and count drops. UI firing rate is shots/sec per ranged soldier, bounded by the fixed tick; composition is percent of all spawned soldiers.

Only necessary major folders exist. `SCENARIOS/` owns deterministic deployment; `UI/` owns controls and overlay. `BenchmarkArena.cs` handles lifecycle and orchestration. `SIMULATION/` owns compact arrays, movement and spatial indexing. `COMBAT/` owns benchmark damage/attacks and projectiles. `RENDERING/` owns instance slots and geometry. `CAMERA/` owns independent pan/zoom.

Flow: prepare unit arrays → allocate six mesh/faction batches → fixed-step spatial/targeting/movement/combat/projectile passes → dirty render sync → throttled overlay. No soldier or projectile has its own Node, processing callback, scene, timer or physics body. Flat ground uses a shared height-query seam; elevation-aware combat is future work.

## Measurements

Frame average and worst use up to the latest **120 rendered frames**, based on `_Process(delta)`. UI refreshes at approximately 0.3 sec. FPS is Godot's engine counter. These include engine/presentation scheduling, unlike scoped game CPU timers.

`Stopwatch` scopes measure simulation total, movement, grid + targeting, combat, projectile updates and renderer sync separately. Displayed CPU values are **accumulated milliseconds divided by rendered frames in that UI reporting window**, including zero-step frames. Simulation total includes its child scopes; do not add them together. Renderer sync measures C# update/submission cost, not completed GPU execution. Spawn preparation and initial GPU upload are outside those steady-state scopes. Simulation per tick and average/peak ticks per frame use the same reporting window. Samples reset on clear/spawn.

Draw calls and rendered objects are whole-viewport Godot counters, including the interface and ground; they are not soldier counts. Soldier instances count live, visible batch entries. Memory reports Godot static allocations and the managed heap separately; neither is total process or GPU memory. Godot static allocation tracking may be unavailable in some builds (shown as N/A). GPU frame time and GPU utilization are N/A; there is no fabricated utilization estimate.

Skipped ticks, capped local queries (latest completed combat tick) and dropped shots expose workload limiting. Fixed simulation time slows relative to wall time when ticks are skipped. Pause/finished combat still renders the viewport, but does not imply meaningful combat workload. Do not compare an army after mass deaths with a newly spawned army.

## Simplifications and fair comparison

- Candidate-bounded queries search cells center-out and may miss better targets in dense cells. The grid indexes factions separately so friendly crowds cannot exhaust enemy-query budgets. This is approximate targeting, not guaranteed global nearest selection.
- Movement follows lanes until local targets are found. No collision avoidance, navigation, separation solver or formation orders; combat can bunch at the front. Units march to fixed mirrored endpoints when no enemy is acquired. The benchmark does real movement and combat, but is not a complete battle AI.
- Projectiles fly straight at the target's launch-time position. Swept tests use the current intended target position; they can miss moving targets and do not hit intervening soldiers. No ballistics, splash or physics. Projectile visuals have fixed orientation. Heavy units have no unique combat stats.
- Fixed steps have no visual interpolation. At low FPS they may visibly step. MultiMesh batches have broad aggregate bounds, without spatial chunk culling or per-instance LOD. The dirty pass scans IDs and stages changed slots in reusable buffers. Batches with more than 64 staged writes upload their buffer once, while smaller changes use individual setters. Deaths pack active slots using swap removal. Render-only skips the pass entirely.
- Array allocation, deployment finalization, clear and GPU allocation can hitch. Spawning is bounded but not a background loading pipeline. UI allocations are throttled; dictionary storage is reused after capacity grows. No multithreading.
- State determinism assumes identical settings and simulation tick counts, not identical wall-clock results across different FPS or live rate edits. Melee applies damage in stable ID order; it is a workload, not a fairness/balance model.

For a fair owner test, keep hardware, renderer, window size, camera zoom, seed, config and composition identical. Reset before each run; exclude preparation, use the same number of simulation ticks/army stage, and record alive counts and limiting counters alongside FPS. Compare A→B→C→D and layouts at increasing counts. Test pause, clear, repeated respawn, mass casualties and projectile caps. Confirm distinct geometry/factions, white flashes and visible projectiles at an appropriate zoom. No owner-hardware FPS or visual acceptance has been performed here.

## Validation

Implementation validation is limited to source/diff review. No .NET executable is available in the implementation environment, so no C# build ran. The available Godot binary is 4.6.3, below this project's 4.7 target; no editor or headless simulation was run. Owner build, visuals and measurements remain required.
