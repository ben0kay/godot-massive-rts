# BENCH0 — Source Implemented / Awaiting Owner Validation

Standalone 3D army workload arena. No performance target is claimed. No campaign, economy, research, final combat rules or advanced formation system is included.

## Launch and controls

1. Import root `project.godot` in **Godot 4.7 .NET** with its required .NET SDK installed. The standard non-.NET Godot editor cannot compile C#.
2. Build the C# project, then press **F6** with `BENCHMARK/BenchmarkArena.tscn` open. This direct launch needs no profile. **F5** opens the project’s profile picker.
3. Initial deployment is 1,000 render-only soldiers. Select total count, mode, layout and seed, then click **Spawn / Reset selected**. The button always rebuilds from the current controls, resetting deaths, projectiles, timings and simulation ticks.
4. **WASD / arrows** pan; **wheel** zooms; **Space** or the button pauses simulation. **F1** hides both left controls and the entire right diagnostics HUD, then shows both again. **F2** toggles only the left control panel and leaves the right diagnostics unchanged. **F4** or the checkbox toggles only the expanded right-hand performance details; the compact top-right metrics remain. Camera remains active while paused.
   - Open the left-side **Camera** section: **Rotation** is a live 0–360° slider (1° steps). **0°** aligns the view with the battlefield axes; **45°** restores the original diagonal. **Reset camera rotation (45°)** restores the default. WASD/arrow panning stays relative to the current view; zoom and pitch are unchanged. Click the battlefield to release keyboard focus from text/spin controls if necessary.
5. **Clear all** releases the current workload. Firing rate is live; other deployment settings take effect on spawn/reset. Pause does not interrupt a deployment already being prepared.

Supported total counts: 1k, 5k, 10k, 25k, 50k, 100k, split across blue/red. Status distinguishes requested count, prepared count and final spawned count. The overlay distinguishes spawned from currently living soldiers.

| Mode | Work performed |
| --- | --- |
| A — Rendering only | Stationary primitive MultiMeshes. No movement, grid, combat, projectile or recurring instance-sync pass. |
| B — Movement | Fixed-step marching between mirrored endpoints in separate lanes, continuously reversing at endpoints. No targeting or combat. |
| C — Melee | Grid rebuilding, staggered local acquisition, nearest-enemy pursuit, health, attack cooldowns, damage flashes and deaths. |
| D — Ranged | Mixed melee/ranged force plus pooled, moving projectiles with swept first-enemy hit checks and lifetime expiry. |

Layouts: **Opposing lines** uses rectangular opposing blocks; **Dense clusters** decreases spacing; **Distributed battlefield** uses seeded offsets across a wider battlefield. The five tactical layouts below add full-roster formations. All remain reproducible with the same count, mode, composition, resource settings and seed. Hexagon Siege Infantry is available in painted battles. Seeded presets use the original five types.

## Wider benchmark controls

The left benchmark interface is approximately **440 px wide**, up from about 310 px. Most editable settings (soldier count, mode, layout, seed, camera yaw, ranged %, paint tool, soldier type/size, faction, each team's battle order, guard radius and separation settings) now show their labels and controls side-by-side. Collapsible category headers have a contrasting blue-grey accent and their contents have recessed backgrounds to distinguish category titles from individual parameters. Long dropdown options truncate visually without expanding the entire panel; open the dropdown to read the full option. Mouse input, debug controls and actual benchmark values are unchanged.

**F1** toggles both panels as a pair. **F2** toggles the left control panel only, without changing right-side compact statistics or the optional F4 expanded diagnostics. No simulation or rendering changes are involved.

## Army balance HUD

A thin top-centre bar from [UI/GameplayHUD](../UI/GameplayHUD/README.md) reflects the current **relative number of living soldiers**. Blue occupies the left, Red the right; Green takes a middle segment in three-faction battles. Counts appear alongside the bar, and the colored proportions ease smoothly as soldiers die. It reads only existing faction counters during the 0.3-second UI update. It appears after the army has fully spawned and disappears while clearing or editing; no soldier-level scans, additional damage calculations or benchmark metric changes are involved.

## Placement grid in deployment editor

**Edit painted deployment → Paint tools → Show placement grid** displays a faint, ground-aligned placement grid (on by default), behind soldiers, obstacles and defenses. The same cells are used for placement snapping. The one-surface shader grid fades out at distant zoom, follows world coordinates under camera rotation/panning, and hides outside edit mode. The checkbox affects only presentation; save files and placement validity remain unchanged. See [DEPLOYMENT](../DEPLOYMENT/README.md) for the rendering details and flat-terrain limitation.

## Painted battle orders

Under **Edit painted deployment → Paint tools → Battle orders**, choose **Auto Attack** or a Hold Ground variant independently for Blue, Red, and Green. Set the shared melee guard radius (default 12 world units). Melee detects and retains enemies within the full guard radius. Ranged first detects enemies within half its attack range and can retain them out to its full attack range. Both distances are measured from the painted post. The melee guard radius also bounds voluntary movement for all guards. When its area clears, plain **Hold Ground** stops in place and **Return to Position** walks back to the post. **Hold Ground – Auto Attack** switches only that faction once it has detected an enemy, gone a full local scan cycle without contact, and enemies remain alive. Guards skip cavalry charges until promoted to Auto Attack. Separation and enemy charge impacts can still displace guards. The arena reads these settings at battle start. Presets use Auto Attack. Orders are session settings.

## Movement diagnostics

Enable **CPU and separation → Profile movement** for a per-tick breakdown. Melee sidestep and contact-hold timings are estimates from a rotating 1-in-32 soldier sample. The calls and blocked counts show the actual sample, including sidestep calls that return no movement during their wait period. Cavalry run-up is timed directly. Remaining movement is the estimated residual and includes the rest of the soldier loop and charge movement. Separation and terrain grounding have their own existing measurements. Switch profiling off for baseline FPS comparisons.

## Pursuit diagnostics

In the CPU overlay, **Scheduled acquisition causes** splits global searches by whether the soldier had a previous target. Loss rows use the distance to the retained dead target **at search time**, compared with that soldier's local search radius. Rows show searches per real second and CPU ms per simulation tick.

Budget and nearby-result counts cover the current HUD sample. An exhausted step used all 128 extra replacement queries. Deferred replacements count eligible attempts blocked by the cap, so a soldier can count again on a later step. Nearby results measure distance only. A local search can still reject those enemies due to cover, minimum range, or its candidate limit.

**Live combat** has two independent comparison toggles, both off by default. **Delay distant melee retargeting** skips scheduled and extra pursuit for about 0.4 simulation seconds after a melee soldier first observes the target loss. Local acquisition continues, and getting within local search range of the old waypoint makes pursuit eligible early. **Cap long-range searches (512/step)** shares 512 queries across all live pursuit reasons per acquisition step (normally 30 Hz), with a rotating starting unit ID. The 128-query extra replacement cap still applies. Skipped searches retry on existing slots. The CPU overlay reports deferred attempts for each toggle over its current sample.

## Configuration and ownership

`CONFIG/DefaultBenchmark.tres`, assigned to the arena's exported `Config`, controls benchmark workloads. Defaults and fields live in `CONFIG/BenchmarkConfig.cs`:

- 60 Hz core simulation, at most five steps per rendered frame. Excess elapsed ticks are discarded and counted. The CPU overlay shows the achieved and configured tick rates.
- Maximum 100,000 soldiers; prepare 4,000 array entries per frame before initializing visuals. Combat modes prepare initial targets in groups of 512 per rendered frame before the battle starts. GPU buffers and initial renderer upload are still a single finalization pass, which may briefly hitch at large counts.
- Spatial cell 6; targets refreshed every 6 ticks, staggered by ID; maximum 96 enemy candidates/query.
- Unit speed, health, damage, reach and interval come from the [roster](../UNITS/DEFINITIONS/STARTING_ROSTER.md). The shared [hit feedback settings](../FEEDBACK/README.md) control the 0.12-second flash and 0.18-second scale pulse. Disable Pulse Enabled and respawn for a white-only comparison.
- Projectile cap 30,000, lifetime 4 sec, minimum hit radius 0.8. Full pools drop new shots and count drops. The live ranged fire-rate multiplier defaults to 1× and scales the authored attack rate.

Only necessary major folders exist. `SCENARIOS/` owns deterministic deployment; `UI/` owns controls and overlay. `BenchmarkArena.cs` handles lifecycle and orchestration. `SIMULATION/` owns compact arrays, movement and spatial indexing. `COMBAT/` owns benchmark damage/attacks and projectiles. `RENDERING/` owns instance slots and geometry. `CAMERA/` owns independent pan/zoom.

Flow: prepare unit arrays → allocate up to ten mesh/faction batches → fixed-step spatial/targeting/movement/combat/projectile passes → dirty render sync → throttled overlay. No soldier or projectile has its own Node, processing callback, scene, timer or physics body. Flat ground uses a shared height-query seam; elevation-aware combat is future work. Benchmark flat ground and the plateau terrain now use a lighter, neutral cool-grey palette for improved readability against the red/blue units and dark obstacle shadows.

Target acquisition rejects queries outside the opposing faction’s bounds before scanning cells. Under **CPU and separation**, **Targeting** shows the full acquisition pass. **Local enemy search**, **Long-range pursuit**, and **Pursuit tree build** show its query and lazy index build costs in milliseconds per rendered frame over the same HUD interval. The total also includes acquisition bookkeeping such as target validation and refresh scheduling. The breakdown excludes initial deployment target preparation. **Live combat → Prune pursuit cells** is on by default. Turn it off to compare the original leaf scan, keeping the retargeting cadence fixed. The checkbox takes effect on the next simulation tick.

Preset types are seeded. The **original three layouts remain unchanged for performance baselines**: Melee-only picks Circle, Square, Diamond and Pentagon in approximate proportions 60/25/10/5; Render-only and Movement use about 20% ranged; Ranged uses the selected ranged percentage.

**Five new tactical formation layouts** use the **entire six-unit roster**, including Hexagon Siege Infantry, in mixed Rendering Only, Movement and Ranged modes. Melee mode intentionally omits Triangle Archers, keeping it melee-only. Ranged % of each force is allocated in rear ranks (default 20% for Movement and Rendering Only). The five new layouts are deterministic, generated incrementally on spawn, and reserve clearance using the largest type footprint including the scaled Hexagon. They do not introduce live formation AI or per-soldier scene nodes.

| Layout | Formation and stress-test purpose |
| --- | --- |
| **Layered ranks — full roster** | Square infantry front, Circle infantry mid-ranks, scattered Pentagon/Hexagon anchors, Triangle Archers behind, Diamond Cavalry on the sides |
| **Cavalry pincer — flanking wings** | Separate forward Diamond wings flanking heavier mixed infantry centres and archers |
| **Wedge assault vs shield wall** | Blue forms a pointed wedge with heavy central support; Red forms a flat shield-heavy defence |
| **Encirclement — defenders vs ring** | Blue holds the centre square while Red deploys in spaced concentric rings around it |
| **Three-way clash — Blue / Red / Green** | Three full-roster armies spawn 120° apart and converge toward the centre |

The original three layouts retain their historical composition and spacing so before/after FPS comparisons remain valid. On new formations the initial camera framing is expanded; zoom and rotation remain live. Terrain and wall preset toggles work as before. Owner visual and performance testing is still required.

## Painted deployments

The **Paint tools** selector uses visible, mutually exclusive radio buttons for **Soldiers**, **Obstacles** and **Defenses** instead of a dropdown. **Paint faction** appears above **Soldier type** and stays available for soldiers and defenses, while the redundant soldier drag/erase hint has been removed. Tool selection still changes the existing painter mode; brush size, placement grid, paint behavior and saved deployment formats are unchanged.

Click **Edit painted deployment** to clear the live battle and open the placement tools. Pick a faction and one of the six unit brushes. Larger bodies need extra clearance. Left drag paints a 3×3 area and right drag erases. Blue, Red and Green can be placed anywhere across the battlefield within footprint clearance and the map boundary. Painted armies have no troop-count cap. The count selector applies to seeded presets. **Clear all** clears soldiers and walls from the draft while editing.

**Save deployment** and **Load deployment** use one local slot at `user://deployment.json`. Version 5 saves record the grid size alongside fine-grid soldier coordinates, walls, and defenses. Version 2 saves include wall rectangles and stable unit IDs. Version 1 and legacy layouts load with no walls. Older melee and ranged placements load as Circle and Triangle. **Start painted battle** requires at least two different teams and uses melee or ranged combat based on the placed units. Reopen the editor to revise or replay the same draft. The preset spawn button still runs the selected Blue vs Red benchmark layout. Existing saves keep Blue and Red IDs, and Green uses ID 2.

New deployment cells are 2 world units wide and independent of the combat grid. Legacy deployments keep their 2.1-unit cells. The combat index uses direct cell lookups within its central array, a dictionary outside it, and clears only occupied array entries each tick. Both paths retain the same soldier order within each cell.

## Soldier separation

Choose **Off**, **Normal** (default), or **Strong** in the live Separation control. Normal runs one correction pass and Strong runs two, with a fresh separation grid between passes. The solver runs in Movement, Melee and Ranged. Compare identical respawns at 1k and 20k soldiers and inspect contact fronts for stacking or jitter.


**Separation** is CPU milliseconds per rendered frame in the current reporting window. Compare **Separation per tick** across runs with different numbers of ticks per frame. **Separation pairs / caps** sums overlap pairs that produced a correction and neighbour queries cut short in the latest simulation tick, including both Strong passes. A pair can be counted again on the second pass. Penetration measures only sampled overlaps before correction. Checks per soldier per pass show how much of the candidate budget is used.

Compare the instrumentation-only revision and the dense solver with VSync disabled, fixed seed/layout/zoom, and 20,000 soldiers. Test A, B Off/Normal, and C Off/Normal. Compare equivalent battle stages and living counts across three runs. Record FPS, worst simulation tick, separation per tick, render sync, skipped ticks and viewport GPU milliseconds when reported. Upload timings cover the army upload loop, and write counts count staged writes rather than unique instances. Tunables are in the benchmark resource under Separation.

**Allied archer yielding** is a live comparison toggle, enabled by default. It applies only alongside Normal or Strong. SEP2's skip, push, and exit counts report relaxed overlaps accepted, yield corrections applied, and soft-exit corrections capped in the latest tick. Tunables are under **Allied crowd yielding** on the benchmark resource.

## Measurements

Frame average and worst use up to the latest **120 rendered frames**, based on `_Process(delta)`. UI refreshes at approximately 0.3 sec. FPS is Godot's engine counter. These include engine/presentation scheduling, unlike scoped game CPU timers.

`Stopwatch` scopes measure simulation total, movement, grid rebuild, targeting, combat, projectile updates and renderer sync separately. Displayed CPU values are **accumulated milliseconds divided by rendered frames in that UI reporting window**, including zero-step frames. Simulation total includes its child scopes; do not add them together. Renderer sync measures C# update/submission cost, not completed GPU execution. Spawn preparation and initial GPU upload are outside those steady-state scopes. Simulation per tick and average/peak ticks per frame use the same reporting window. Samples reset on clear/spawn.

Draw calls and rendered objects are whole-viewport Godot counters, including the interface and ground; they are not soldier counts. Soldier instances count live, visible batch entries. Memory reports Godot static allocations and the managed heap separately; neither is total process or GPU memory. Godot static allocation tracking may be unavailable in some builds (shown as N/A). GPU frame time and GPU utilization are N/A; there is no fabricated utilization estimate.

Skipped ticks, capped local queries (latest completed combat tick) and dropped shots expose workload limiting. Fixed simulation time slows relative to wall time when ticks are skipped. Pause/finished combat still renders the viewport, but does not imply meaningful combat workload. Do not compare an army after mass deaths with a newly spawned army.

## Simplifications and fair comparison

- Candidate-bounded queries search cells center-out and may miss better targets in dense cells. The grid indexes factions separately so friendly crowds cannot exhaust enemy-query budgets. This is approximate targeting, not guaranteed global nearest selection.
- Combat soldiers pursue living targets directly. Local searches use the existing candidate cap. After a target dies, a soldier can continue toward its last position while waiting for a replacement. Up to 128 extra long-range searches per tick can replace lost targets on the local refresh schedule. Long-range nearest-enemy searches use a shared tree over occupied cells. **Live combat → Slower long-range retargeting** is on by default and doubles the staggered refresh interval for soldiers pursuing a living target outside local range (roughly 1.6 simulation seconds at default settings). Soldiers without a living target keep the original roughly 0.8-second schedule and the existing extra replacement budget. Turn the toggle off to compare the original cadence during the same battle. Movement-only mode keeps mirrored march lanes. Combat can bunch at the front and will need obstacle navigation for maps with impassable terrain.
- Basic arrows follow a shallow arc to the target's launch-time position. They use swept hits and disappear on their first enemy impact, arrival, or lifetime expiry. The arrow mesh faces along the flight path. Tune the arc in the [projectile definition](../PROJECTILES/Types/BasicArrow/BasicArrow.tres).
- Smooth soldier motion interpolates positions and turns between fixed steps. Disable it for tick-only benchmark uploads. MultiMesh batches have broad aggregate bounds, without spatial chunk culling or per-instance LOD. The dirty pass scans IDs and stages changed slots in reusable buffers. Batches with more than 64 staged writes upload their buffer once, while smaller changes use individual setters. Deaths pack active slots using swap removal. Render-only skips the pass entirely.
- Array allocation, deployment finalization, clear and GPU allocation can hitch. Spawning is bounded but not a background loading pipeline. UI allocations are throttled; dictionary storage is reused after capacity grows. No multithreading.
- State determinism assumes identical settings and simulation tick counts, not identical wall-clock results across different FPS or live rate edits. Melee applies damage in stable ID order; it is a workload, not a fairness/balance model.

For a fair owner test, keep hardware, renderer, window size, camera zoom, seed, config and composition identical. Reset before each run; exclude preparation, use the same number of simulation ticks/army stage, and record alive counts and limiting counters alongside FPS. Compare A→B→C→D and layouts at increasing counts. Test pause, clear, repeated respawn, mass casualties and projectile caps. Confirm distinct geometry/factions, white flashes and visible projectiles at an appropriate zoom. No owner-hardware FPS or visual acceptance has been performed here.

## Validation

Implementation validation is limited to source/diff review. No .NET executable is available in the implementation environment, so no C# build ran. The available Godot binary is 4.6.3, below this project's 4.7 target; no editor or headless simulation was run. Owner build, visuals and measurements remain required.

## OBST1 — Obstacle benchmark

In **Edit painted deployment**, choose **Paint tool → Obstacles**. Drag left to draw a rectangle and drag right to erase a rectangular area. Release to commit the preview. Switch back to **Soldiers** to paint the teams and click **Start painted battle**. Painted walls persist through Save and Load. See [DEPLOYMENT](../DEPLOYMENT/README.md) for clearance and edit limits.

For a reproducible obstacle comparison, leave the layout, seed, and soldier count fixed and toggle **Test wall in preset** before spawning. The preset wall crosses the middle lanes. Movement and combat modes use it for routing and collision. Rendering Only shows the mesh with no route or LOS work. The overlay shows wall count, LOS checks and blocks, clipped movements, steering time per tick, route build time per rendered frame, cache activity, queue depth and max route wait in frames for the current sample.

## Live melee comparisons

- **Melee sidestep** checks for room on either side when a pursuing soldier repeatedly loses forward progress. A soldier waits briefly if both sides are crowded. Requires Normal or Strong separation.
- **Hold melee contact** gives engaged soldiers a small release margin so they can hold through tiny changes in distance. Separation can still move them.
- **Footprint-weighted separation** switches ordinary overlap correction between footprint weighting and equal shares. The Allied archer yielding toggle remains separate.

All three are on by default. Toggle them during the same battle, or restart the painted deployment for a comparison from identical starting positions. Turning all three off restores the previous melee steering with equal-weight ordinary separation. To reproduce the previous weighted baseline, leave the weight toggle on.

## TERR1-A terrain preset

Select **Test plateau** under **Preset terrain**, then reset. Opposing Lines puts Red near the raised top and Blue on the plain. Movement mode makes both sides traverse the shoulder. Compare with **Flat ground** using the same count and seed. The overlay reports terrain compilation, mesh setup, and grounding cost. Painted deployments use flat ground. See [Terrain](../TERRAIN/README.md).

The left panel scrolls independently of the battlefield. Click a section header to fold it. **Live combat → Ranged target variety** is on by default. Approximately 80% of ranged soldiers prefer the closest target, 15% prefer the second, and 5% prefer the third, based on the battle seed and soldier ID. Alternate picks come from the existing bounded local query and must be visible for direct-fire weapons, within range, and within 10% plus 0.5 units of the nearest valid candidate. Toggle off to restore the original closest-target selection on subsequent refreshes.

The right overlay keeps the battle summary visible and folds CPU, obstacle, and rendering details into independently scrollable sections. Enable **Obstacles and terrain → Sample obstacle queries** during a wall scenario to see calls per HUD interval and approximate microseconds per call. Timing samples one in 64 calls per category. Clip and LOS calls are counted separately from standalone sweeps and corridors. Route building still reports milliseconds per rendered frame.

**Reuse corridor checks** (on by default) reuses direct-approach results while the endpoints match or stay within a verified half-unit margin. Inset wall crossings prove a blocked approach and enlarged clear corridors prove an open approach. Wall clipping, route work budgets and corner selection still use the existing checks. Compare with the toggle off in the same wall layout and watch **Corridor results reused**, corridor calls, and simulation time per tick.

## DEF1 — Static laser defense

Open **Edit painted deployment → Paint tools → Defenses** to place Laser Tower 001. Defenses support Blue, Red and Green. A painted battle can start with a tower faction opposing a soldier faction. Save and load preserve defense type, faction and footprint position. See [BUILDINGS](../BUILDINGS/README.md) for configuration and limits.

## SIM60 — Core and acquisition clocks

Movement, separation, terrain grounding, charge contacts, cooldowns, and projectile sweeps use the 60 Hz fixed step. The combat spatial grid is rebuilt every core tick for current positions. Expensive acquisition uses a separate 30 Hz schedule. With default settings, local soldier searches and tower reacquisition stagger over 0.2 seconds. Scheduled pursuit keeps its 0.8-second cadence, and slower live pursuit remains at 1.6 seconds. The extra replacement cap is 128 per acquisition step. Pursuit trees are built on demand on those steps.

Navigation retains its existing rendered-frame route budget and retry clock. **Display and controls → Smooth soldier motion** remains enabled by default. Projectile meshes use the latest simulated position when a frame advances the simulation.

### Pursuit diagnostics and panel shortcuts

Under **CPU and separation**, the pursuit breakdown shows live refresh, scheduled acquisition without a living target, and extra replacement. Each row reports searches per wall-clock second, CPU milliseconds per simulation tick, and average microseconds per search. Live refresh also reports the percentage that changed targets. Tree construction remains in its own timing, and initial deployment preparation is excluded.

**F1** hides or restores the left controls and right performance details while keeping the compact battle summary visible. **F4** controls the right-hand details independently.

**Reuse route waypoints** (off by default) keeps the current waypoint while the soldier stays inside its verified corridor. Waypoint arrival, leaving that corridor or losing the route restores normal waypoint selection. Wall clipping still runs on each movement step. Compare **Route waypoints reused**, corridor calls and movement time in the same obstacle layout.

### Named painted battles

In **Edit painted deployment → Paint tools**, enter a **Preset name** and click **Save as preset**. The preset stores the painted soldier positions, types and teams, walls, defenses, team orders, guard radius, and seed in `user://benchmark_presets`. Names are unique. Choose it under **Preset battle → Saved battle** and click **Spawn / Reset selected** to replay the deployment, including after restarting Godot. Choose **Generated battle** to use the standard benchmark controls. Saved battles use the current unit definitions and live tuning settings. The existing single deployment slot still loads into the painter.
