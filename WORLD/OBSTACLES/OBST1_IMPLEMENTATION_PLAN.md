# OBST1 — Painted Static Rectangles, Shared Navigation and Selective Line of Sight

**Repository:** `ben0kay/godot-massive-rts` · **Branch:** `main`
**Status:** Source implemented. Awaiting owner validation in Godot.
**Primary scale:** about 10,000 vs 10,000 soldiers in actual battles. Higher BENCH0 presets remain for comparative stress testing.

## Objective and boundaries

Implement static, **dark-grayish rectangular obstacles** that can be painted, erased, saved, loaded and replayed in the existing deployment editor. Obstacles:
1. Occupy XZ ground space and physically block **all ground soldiers of all factions**, including cavalry and separation pushes.
2. Require ground units to **route around them**, not repeatedly walk into the wall.
3. Block **direct-fire line of sight** and stop direct projectiles before they hit troops behind cover.
4. Provide explicit policy hooks for **future indirect artillery/air units**, which may fire across the obstacle without ordinary direct LOS. **Do not build artillery or flying movement in OBST1.**
5. Remain **static for the lifetime of a battle**. Recompute obstacle topology on edit/save/load/battle preparation, not on every tick.

The goal is a lightweight, mass-army solution **without** per-soldier Godot collision nodes, `NavigationAgent3D`, physics raycasts, per-unit `AStar3D`, moving obstacles, destructible obstacles, dynamic lighting, or full RTS group orders. Do not refactor unrelated gameplay.

## Read the actual latest source first

The repository is evolving. Read `README.md`, `NOTES/CodePreferences.md`, `endgoal.md`, `BENCHMARK/README.md`, and:

- `GAMEPLAY/DEPLOYMENT/DeploymentLayout.cs`: 2.1-world-unit painter grid, currently soldier placements only; checks per-type footprint clearance.
- `GAMEPLAY/DEPLOYMENT/DeploymentPainter.cs`: left drag paints soldiers in a 3×3 brush, right drag erases; `user://deployment.json` stores version 1 with stable string unit IDs; old legacy placement arrays also load.
- `GAMEPLAY/DEPLOYMENT/DeploymentPreview.cs`: MultiMesh instances for static editor previews.
- `BENCHMARK/UI/BenchmarkPanel.cs`: painter mode and controls; keep UI useful and uncluttered.
- `BENCHMARK/BenchmarkArena.cs`: initialization, painted vs seeded battles and fixed-step scheduling; currently `SpatialGrid.Rebuild → targeting → movement → trample → separation → combat → projectiles → rendering`, with the latest scheduling variations.
- `GAMEPLAY/SIMULATION/MovementSystem.cs`, `GAMEPLAY/SIMULATION/UnitState.cs`: direct movement toward living target or `Goal`, acceleration, ranged standoff, headings.
- `GAMEPLAY/SIMULATION/SpatialGrid.cs`: combat targeting/proximity, NOT obstacle awareness.
- `GAMEPLAY/SIMULATION/SEPARATION/SeparationSystem.cs`: cached soldier radii with SEP1/SEP2 corrections.
- `GAMEPLAY/UNITS/CATALOG/UnitCatalog.cs`: compiled effective `Footprint`, `MaxFootprint`, `Reach`; latest UNIT1 size multiplier must be respected.
- `GAMEPLAY/COMBAT/CombatSystem.cs`, `GAMEPLAY/COMBAT/AreaDamage.cs`, `GAMEPLAY/COMBAT/TRAMPLE/TrampleSystem.cs`: attack range, targeting, swipe and charge.
- `GAMEPLAY/PROJECTILES/Shared/ProjectileDefinition.cs`, `ProjectileCatalog.cs`, `ProjectileSystem.cs`: pooled arrows with a swept first-unit test and a visible shallow arc.
- `VISUALS/RENDERING/ArmyRenderer.cs`: packed MultiMesh rendering, smoothing and hit-feedback transforms; preserve it.
- `VISUALS/CAMERA/IsometricCamera.cs`: painting projection to XZ ground.

Do **not** undo work from SEP2, FB0, three-team deployment, cavalry trample, soldier facing, smooth motion or UNIT1. Use existing CAPITALIZED major-root folder convention.

## Folder ownership and suggested source files

Create actual code files only when needed. Keep shallow, domain-owned folders:

```text
OBSTACLES/
  README.md
  OBST1_IMPLEMENTATION_PLAN.md
  ObstacleLayout.cs         # authored painted cells/rectangles, static data, validation
  ObstacleField.cs          # compiled static AABBs, broadphase and obstacle revisions
  ObstacleRenderer.cs       # shared dark-gray box or rectangle MultiMesh
  ObstacleVisibility.cs     # reusable direct LOS / swept obstacle queries
NAVIGATION/
  README.md
  ObstacleNavigation.cs     # shared obstacle-aware route lookup/caching and movement safety
DEPLOYMENT/
  DeploymentPainter.cs     # obstacle vs soldier painting mode, drag preview
  DeploymentLayout.cs      # soldier placement interaction/clearance
BENCHMARK/
  UI/BenchmarkPanel.cs     # obstacle brush and navigation/LOS diagnostics
  CONFIG/BenchmarkConfig.cs
  BenchmarkArena.cs
COMBAT/
  CombatSystem.cs          # optional/lazy direct LOS gate and ranged repositioning
PROJECTILES/
  Shared/ProjectileDefinition.cs
  Shared/ProjectileCatalog.cs
  Shared/ProjectileSystem.cs
```

Do not put the entire system in `BenchmarkArena.cs`. OBSTACLES owns static obstacle data/queries; NAVIGATION owns path choice; COMBAT owns when a weapon needs a LOS test; PROJECTILES owns physical hit order; DEPLOYMENT owns editing; BENCHMARK wires them into tests. If a small helper belongs in fewer files, consolidate; avoid empty or redundant folders.

## 1. Paintable obstacles and save compatibility

**Visual target:** simple solid **dark gray rectangular blocks** on the flat battlefield, easy to see from the isometric camera. Initial neutral obstacle type, no team ownership, damage, destructibility or moving nodes. Use one shared simple box material/mesh or a merged rectangle mesh; prefer **one MultiMesh batch** / few draws rather than one Node3D per painted tile. Initial uniform height (suggest 1.0 world unit) is presentation and future occlusion metadata; the initial direct LOS policy is deliberately 2D and conservative.

In painter UI:
- Add a clear selector: **Paint tool: Soldiers / Obstacles**. Soldier brush keeps all existing types/factions and original 3×3 drag behavior.
- Add **Rectangle obstacle** drag: left press starts rectangle, mouse drag previews bounds, release commits inclusive cells; right drag erases matching occupied cells. Optional single-cell/brush painting only if straightforward.
- Obstacles snap to **same 2.1-world-unit XZ editor grid**, allowing quick painting of big blockades.
- Reject obstacles that overlap currently painted soldiers' *full circle footprint* (or offer a consistent atomic cancel/error); likewise reject new soldier placement whose footprint intersects painted obstacle geometry. Check **continuous circle-vs-AABB**, not just same cell, especially with larger Pentagon/UNIT1.
- A bulk rectangle operation must be transactional and bounded, with a practical edit cap and informative status; don't run repeated expensive scene rebuilds on every mouse move. Rebuild merged static geometry on drag release/paint finalize, not on every pixel of movement.
- Preserve **Clear all**, **Save**, **Load**, **Start painted battle** and layout editing lifecycle. A painted battle snapshots both obstacles and soldiers. Don't allow editor changes to mutate an active static battle's nav/LOS world until reset.
- Saved `user://deployment.json`: **introduce version 2** containing `Placements` (unchanged stable unit IDs) plus `Obstacles` (e.g. compact grid-cell rectangles or runs). Load **v1** as zero obstacles; load old legacy array as soldiers + zero obstacles; load v2 with strict dimensions, counts, bounds, overlap validation, no duplicate/invalid cells, and safe size limit. Avoid changing unit IDs/faction IDs. Saving a loaded old layout should produce valid v2.
- Store painted obstacle cells sparsely (set/runs); merge contiguous cells into world-space **axis-aligned rectangles** for rendering and obstacle queries. Merging must preserve openings, concavities and holes: a bounding box around an irregular painted shape is **not** equivalent. Avoid duplicating overlapping internal faces when simple greedy rectangle merging suffices.

Seeded BENCH0 modes without edited obstacles remain unchanged by default, except for an optional obstacle scenario toggle if needed for comparable benchmarking. **RenderingOnly** must not pay for navigation/LOS work. **Movement** mode should route around obstacles between its existing `Goal` endpoints; Melee/Ranged should pursue enemies around them.

## 2. Single compiled static obstacle representation

During battle preparation, compile painted cells into immutable, compact XZ AABBs, their editor-grid cell coverage and a **fast static spatial lookup**. Suggested interfaces:

```csharp
bool IntersectsCircle(Vector2 center, float soldierRadius);
bool SweepCircle(Vector2 from, Vector2 to, float radius,
                 out float firstHitT, out Vector2 normal);
bool BlocksSegment(Vector2 from, Vector2 to, ObstacleQueryPolicy policy);
bool SweepProjectile(Vector3 from, Vector3 to, ObstacleQueryPolicy policy,
                     out float firstHitT);
```

These are **illustrative signatures**, not required API spellings. Avoid `Godot.PhysicsDirectSpaceState` queries. Preallocate/reuse memory; no per-soldier allocated list or path node.

- For moving circle bodies, use **Minkowski expansion**: sweep the soldier's centre against obstacle AABBs expanded by `effectiveFootprint + tinyClearance`. Static overlap resolution must use exact circle-vs-rectangle near corners (not simply expanded AABB) to avoid artificial corner blocking; a proper swept circle/AABB may need a broadphase expanded AABB followed by exact rounded-corner contact.
- Broadphase candidates via editor-grid lookup, compact cell/range indexing, or coarse hash populated **once per obstacle revision**; avoid O(soldiers × all rectangles) scans.
- No soldier may enter an obstacle even if navigation is pending, SEP1 Off, SEP1 Strong, allied yielding, a high-speed cavalry charge, projectile shove or initial spawn. A **final safety/placement pass** after movement and separation is necessary; resolve against walls with bounded correction, or validate and clamp each movement contributor and final pose. The nonpenetration invariant cannot rely solely on navigation.
- Treat obstacle contact as independent of troop separation. SEP2 ally pass-through **never** permits crossing obstacles.
- Unit **size classes**: compiled UNIT1 `Footprint` may change. Cache clearance/path data per distinct quantized footprint radius or per authored type if only five; do not treat all soldiers as circles with the smallest radius. Big pentagons should not fit gaps meant for infantry.
- Consider out-of-map boundary and units starting embedded in an obstacle: reject painter overlaps, and for corrupted old saves/seeded setups reject invalid positions or place at nearest legal point *once* at preparation, not silently jitter each tick.

## 3. Navigation: share heavy work, never per-unit A*

**Key rule:** do not run independent `AStarGrid2D`, `NavigationAgent3D`, `GetSimplePath`, full shortest path, or LOS-driven corner scans for each of 20,000 soldiers each simulation tick.

A practical recommended approach for this **static rectangle** milestone is **shared obstacle-aware routing with cached spatial sectors**:

1. **Fast direct path:** when the intended straight XZ movement corridor to the current goal is unobstructed for the soldier's radius, use the existing direct movement and acceleration. Do not force every unit into a navigation graph when there are no relevant obstacles.
2. **Static obstacle topology:** during preparation, build a sparse waypoint / corner-visibility graph around the **inflated corners** of merged AABBs for each effective radius class. Include visibility-tested connections, no shortcuts through solids, no diagonal corner cutting. All graph building occurs once per obstacle revision, *not per unit*. A graph is efficient for a modest number of rectangular obstacles on a 5,000-wide map: do **not** allocate a giant full-map 2.1-grid flow array.
3. **Share routes by quantized origin/destination sectors**, effective radius class and obstacle revision, not individual soldier ID. Source and destination sectors can be ~8–16 world units to start. Store cached **route/next-waypoint indices** in a bounded dictionary/LRU, and small per-soldier cached route references only where required. Many nearby soldiers pursuing the same enemy cluster should share routes.
4. **Bound route requests:** on a cache miss queue the request. Prepare at most a small configurable number of shared sector routes / fixed milliseconds per frame, staggered. Until ready, soldiers use a deterministic local wall-slide/corner-approach fallback or stop safely **outside** the obstacle. Never cross it to satisfy a route deadline. Expose queue depth and latency.
5. **Replan only when necessary:** when the current goal changes substantially or the chosen next waypoint becomes invalid, or when a route sector is entered. Do not redo pathfinding because a target moved slightly, a soldier was separated or its render facing changed.
6. **Maintain reliable target pursuit:** direct `units.Target` can remain an enemy reference; navigation supplies a temporary steering waypoint when straight pursuit is blocked. Do not replace the authoritative combat target with a navigation node. In movement-only mode the waypoint leads toward `units.Goal`. A unit may continue to fight en route.
7. **Dynamic enemy targets:** don't create one unique route for each moving target. Quantize goals to enemy **clusters/sector** and aggressively share. Stagger goal updates; if a target dies, existing combat targeting can reacquire, after which route validity is checked. Include a route cooldown/hysteresis around corners to avoid left/right oscillation.
8. **Unreachable goals:** no infinite loops, no snapping across walls, no tunnelling and no path through a sealed enclosure. Detect missing connectivity and hold/reacquire safely rather than burning CPU on repeated failed searches.
9. **Large maps/too many obstacles:** keep graph memory and preprocessing bounded. If rectangle graph size becomes high, either use a sparse chunked route alternative or report a clear limitation; do **not** allocate grid data over the entire 5,000×5,000 world just to cover a few wall tiles.

**Correctness caveat:** a sector cache is only a *hint*. Two soldiers on opposite sides of a wall can share a nominal sector. Validate the **actual** soldier→first waypoint segment against its inflated obstacles and fall back to a local safe route if needed. A cached route cannot authorize collision bypass.

The overall **query/collision budget should be separate from route-build budget**: essential nonpenetration checks run immediately; expensive route computation is staggerable. Initial benchmark targets can be around 1k, 5k, 10k, **20k total**. Profile before raising cache size or rebuilding graph each frame.

## 4. Integrate navigation into existing movement and charge

Suggested conceptual ordering (adapt to actual live arena):

```text
Static obstacle snapshot / graph prepared once
Combat target refresh (existing, staggered)
Movement desire (enemy or Goal) → direct or shared safe waypoint
Swept obstacle collision / slide (movement-authoritative)
Cavalry trample/charge movement, clipped by solid obstacles
SEP1/SEP2 soldier separation
Final bounded obstacle depenetration / valid pose correction
Combat attack decisions + direct LOS policy
Projectile sweep: compare first obstacle and first soldier hit
Existing render sync and overlays
```

- Preserve current movement acceleration, braking, unit facing (Circle bypass), smooth interpolation and stop-distance semantics.
- **Cavalry charges must stop at solid obstacles**, even if momentum would trample enemy soldiers. A trample penetration exception is for **enemy soldiers only**, never for terrain. Trample sweep should process first contact in correct order (wall vs enemy); do not allow damaging an enemy located behind an obstacle by passing through the wall.
- No obstacle should be an enemy target or damageable in OBST1. Pentagon cleave must not hit enemies through a wall: filter its secondary targets using the same direct-cover policy if cleave is not explicitly wall-penetrating.
- Later right-click group commands should be able to use the same shared routes. Do **not** implement selection/orders in OBST1.

## 5. LOS: separate weapon policy from geometric query

Add a shared LOS query in OBSTACLES and a small **authorable policy on attack/projectile definitions**, not an all-units LOS ray every tick.

Recommended policy enum:

```csharp
public enum ObstacleFireMode
{
    RequiresDirectLineOfSight,   // current Triangle Archer / arrows
    IndirectFire                 // future artillery; may target over cover
}
```

**OBST1 defaults:** existing Triangle Archers require direct LOS. Melee attack and Pentagon cleave should also respect opaque solid obstacles for victims across a wall. Future artillery can set `IndirectFire`, but its projectile flight/height and physical obstacle-interaction rules must still be explicitly defined. **Do not implement artillery now.**

Important distinction:
- **Target acquisition / pursuit:** a target behind a wall can remain a *navigation goal* so troops know whom to route toward. For firing, a direct weapon needs a clear view from attacker to victim.
- **Firing:** check LOS **only for a plausible target in weapon range and ready to attack**; validate at firing time, because a cached visible result may have become stale as actors moved. If blocked, choose a nearby visible alternative during staggered acquisition where possible. Don't burn a cooldown or spawn a projectile that would unreasonably pass through the wall.
- **Stationary archers:** current `MovementSystem` stops ranged soldiers when a target is in range. Update this to hold only when **both in range AND directly visible**. If target is in range but blocked, use shared navigation toward a firing position outside cover. Prevent the archer from oscillating between standstill and blocked pursuit; a small stable goal/LOS retry is fine.
- **Line test:** use a cheap 2D XZ segment versus obstacle AABBs through the static spatial lookup, early-out on first blocker. No physics raycast. Exact geometry checks in the hot path should not allocate.
- **Projectile sweep:** direct arrows must be blocked **before** they reach a soldier behind the obstacle. Extend pooled projectile sweep to test the segment's earliest obstacle `t` against first unit `t`; apply the **earlier collision**. Current `SpatialGrid.FirstProjectileHit` may return only unit ID, so refactor minimally to expose hit fraction if needed. Avoid checking only projectile endpoint, because fast arrows tunnel through thin obstacles.
- **2D occlusion for OBST1:** dark-gray blocks are treated as tall enough to stop ordinary direct fire, irrespective of the existing cosmetic shallow arrow arc; this ensures targeting and projectile contact are consistent. This is a deliberate first-pass rule, not a general 3D height model.
- **Future artillery:** do not treat `IndirectFire` as meaning all launched artillery magically passes through arbitrary-height geometry. Later add height-aware ballistic arc intersection or artillery's explicit terrain collision policy, using obstacle elevation/height and projectile trajectory. Keep LOS permission and projectile collision **separate concepts**, so artillery can fire without direct LOS but still be blocked by a sufficiently tall obstacle.
- **Other future types:** low walls, transparent glass, flying units and wall-penetrating weapons should have independent flags/policies. OBST1 supports architectural extension, not their full implementation.

### Compact obstacle flags (initial/future-facing)

A single obstacle definition may carry:
- `BlocksGroundMovement = true`
- `BlocksDirectLineOfSight = true`
- `BlocksDirectProjectiles = true`
- `Height = 1.0f` (for visuals/future height-aware interaction)

Avoid extra complex material/entity classes before needed. No per-rectangle Godot physics shape.

## 6. Important costs, guards and diagnostics

`BENCHMARK` should show measured **obstacle navigation CPU ms/tick**, **LOS queries and blocks**, **route cache hits/misses**, **new routes built/queued**, **solid collision corrections**, plus active merged-obstacle count. Avoid instrumenting by doing a new full-unit scan. Retain all existing movement, separation, projectile, trample, render and GPU timings.

Suggested Inspector settings in `BENCHMARK/CONFIG/BenchmarkConfig.cs` or a modest `ObstacleSettings.tres`:
- Obstacle height and dark-gray tint (editor visuals).
- Route sector size (e.g. 12 world units), maximum cached routes, cache/graph build budget per frame.
- Optional debug overlay: obstacle AABBs, route waypoints and LOS blocked indicator for a selected/sampled soldier.
- **Nav enabled vs disabled for benchmark A/B** may be included, but obstacle **physical collision** must never be disabled in an active obstacle scenario. No-obstacle scenes retain baseline CPU cost with an early return.

Specific pitfalls to guard:
- Units with different UNIT1 sizes and trample collisions; no footprint shrink.
- Separated units pushed through a wall; cavalry trample passing through a wall.
- Ranged archers freezing in range behind an opaque rectangle.
- Weapons consuming cooldown when LOS was blocked.
- Projectiles visually/arbitrarily crossing a wall while targeting refuses the shot.
- A dense block of archers/allies obstructing melee on top of obstacle routing: SEP2 and obstacle collision need separate priorities.
- Disconnected regions, concave shapes, narrow corridors and wall-corner snagging.
- Cached goal sector spanning a wall or outdated position; always test actual waypoint clearance.
- Pause, clear, painted save/load, reset and repeated scenario launches.
- Thread safety: static path prep may be budgeted on main thread; don't introduce background Godot-API work.

Do **not** promise a specific FPS. A fully engaged obstacle scenario may cost more than unblocked benchmark scenes. Reuse small arrays, compact graph nodes and cached route data so the cost depends on **obstacles/route groups**, not the number of units × number of obstacles. The editor's large world boundary is not a reason to preallocate an enormous full-map grid.

## 7. Phased implementation sequence (all required for OBST1)

**Phase A — Painter + solid geometry**
- Obstacle authoring brush, drag rectangle preview, sparse/merged geometry, save v2 migration, no soldier/obstacle overlap, single shared dark-gray renderer.
- Static obstacle index and swept circle collision. Ground movement and SEP1/SEP2/trample never pass through solids.

**Phase B — Shared navigation**
- Obstacle-radius-specific topology, cached/staggered sector routes, direct-path fast lane, safe no-route fallback.
- Connect to Movement/Goal and three-faction combat pursuit. Ranged units reposition for shots behind cover.

**Phase C — Line of sight + pooled projectile collision**
- Explicit direct/indirect policy for future use, current archers and melee direct policy, range+LOS firing gate.
- Early swept obstacle vs soldier hit ordering for direct arrows; future artillery mode merely an API hook.

**Phase D — Benchmarks + documentation**
- Stats/CPU counters, no-obstacle baseline and a reproducible painted or seeded big-rectangle test.
- Update `WORLD/OBSTACLES/README.md`, create `GAMEPLAY/NAVIGATION/README.md` on implementation, update `GAMEPLAY/DEPLOYMENT/` and `BENCHMARK/` docs and root `README.md`.
- Append **one concise entry** in `devlog.md` **only when OBST1 code actually exists**.

**Keep it in one focused implementation task**, but stop and report a real blocker if a necessary cross-system API can't be made correct in minimal scope. Prefer correctness over claiming successful routing for all topologies.

## Owner testing checklist

1. Paint a long dark-gray rectangular blockade between **Blue and Red** (and repeat with Green), paint troops on either side, save/load v2, and start the battle. Obstacles persist; previous v1/legacy deployment loads with no obstacles.
2. Frontline Circle/Square troops, cavalry and Pentagon visibly walk around both ends of the rectangle, then engage instead of running indefinitely into the wall.
3. Larger UNIT1 Pentagon/cavalry respects its own radius. In a narrow doorway, large units cannot cut a corner or pass when smaller soldiers can.
4. SEP1 Off/Normal/Strong and SEP2 allied yield never push an actor through the wall.
5. Cavalry charge/trample cannot phase through the obstacle, regardless of momentum.
6. Triangle archers **cannot shoot through** the rectangle; pooled arrows stop at the obstacle if fired into it. Archers can move to an exposed firing lane instead of remaining frozen.
7. Pentagon cleave cannot splash damage through a wall. `AreaDamage.Radial` defaults remain unchanged unless a future authorable area attack explicitly uses cover/occlusion.
8. A 2- or 3-rectangle detour, short gap, sealed enclosure, mixed-unit collision and changing target after a death do not produce looping, teleportation or permanently wedged path requests.
9. With zero obstacles, compare against baseline BENCH0 and confirm no material hot-path regression. Compare the same seed/count/stage at **1k, 5k, 10k, 20k total**, especially navigation/LOS and separation CPU time, queue depth and late frame spikes.
10. Test pause, clear/reset, painted edit save/load, 3 factions and repeated arena launches. Ensure normal soldier facing, smoothing, FB0 flashes, 1.25× cavalry size and current trample work.

## Codex delivery rules

- First read `NOTES/CodePreferences.md` and all named source. Use actual live `main` as source of truth; `endgoal.md` is aspirational, not existing code.
- Implement **OBST1 only**; do not add full RTS group selection, destructible walls, terrain elevation, economy, artillery, flying units or general navmesh.
- Keep arrays/queries reusable and branch away when no obstacles exist. One basic `dotnet build` if practical; don't run extensive benchmarks. User performs full in-game and FPS acceptance.
- After code changes, update the relevant READMEs and append one short implementation entry to `devlog.md`.
- Commit and push to `main` once, then report SHA, how routing and LOS are handled, measured results only if actually measured, and owner test steps.
