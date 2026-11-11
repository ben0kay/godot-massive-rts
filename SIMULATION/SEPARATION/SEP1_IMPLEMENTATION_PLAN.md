# SEP1 — High-Count Soldier Separation

**Repository:** `ben0kay/godot-massive-rts` · **Branch:** `main` · **Status:** **Planned / Not Implemented**
**Target workload:** Up to roughly **10,000 vs 10,000 (20,000 live units total)** on owner hardware; keep larger BENCH0 sizes available for comparative stress tests, without promising FPS.

## Goal

Soldiers currently use the fast shared movement pass, but friendly and opposing units can occupy almost the same XZ position. Add **lightweight, deterministic, footprint-based crowd separation** so infantry front lines form, archers cannot stack inside other soldiers, cavalry does not ghost through crowded allies, and large pentagons occupy visibly more room.

**Do not add** per-soldier `Node3D`, `CharacterBody3D`, `Area3D`, `RigidBody3D`, collision shapes, Godot physics queries, RVO/NavigationAgent nodes, navmesh pathfinding, advanced formation AI, knockback, terrain systems, or multithreading. This task is SEP1 only, not a gameplay refactor.

## Relevant existing code — inspect latest files before editing

- Root `README.md`, `NOTES/CodePreferences.md`, any `AGENTS.md`.
- `SIMULATION/MovementSystem.cs`: currently moves units independently toward targets/lane goals.
- `SIMULATION/UnitState.cs`: compact arrays, stable IDs, living/dead state and `Catalog`.
- `SIMULATION/SpatialGrid.cs`: faction-specific targeting/projection grid (~6-unit cells), pursuit tree, pre-movement projectile broadphase. **Do not assume it is appropriate for close-range neighbour separation**.
- `UNITS/CATALOG/UnitCatalog.cs`: `UnitStats.Footprint` is a radius; circles/squares/triangles/diamonds/pentagons have different radii.
- `BENCHMARK/BenchmarkArena.cs`, `BENCHMARK/CONFIG/BenchmarkConfig.cs`, `BENCHMARK/UI/BenchmarkPanel.cs`: fixed-step orchestration, tuning, overlay.
- `COMBAT/CombatSystem.cs`, current `COMBAT/ProjectileSystem.cs`: ensure revised positions do not invalidate hit/target assumptions.
- `DEPLOYMENT/DeploymentLayout.cs`, `DEPLOYMENT/DeploymentPainter.cs`: editor-side placement already has footprint clearance; don't break painted saves or placement.

The repository has been changing rapidly. **Read the live branch**, not just this plan's description, and preserve all post-UNIT0 projectile/targeting work.

## Folder ownership

```text
SIMULATION/
    README.md
    MovementSystem.cs
    SpatialGrid.cs
    UnitState.cs
    SEPARATION/
        SEP1_IMPLEMENTATION_PLAN.md
        SeparationSystem.cs          # Proposed reusable allocation-bounded solver
        SeparationGrid.cs            # Optional, only if it clarifies the implementation
BENCHMARK/
    CONFIG/BenchmarkConfig.cs
    UI/BenchmarkPanel.cs
    BenchmarkArena.cs
```

Use the existing **CAPITALIZED major top-level directories**. A dedicated `SIMULATION/SEPARATION/` subsystem should own neighbour lookup and position correction. Keep its state reusable across ticks and explicit across reset. Do not create empty folders or extra abstraction layers.

## Observable behaviour and controls

Support three user-selectable settings in the **existing BENCH0 left control panel**, applying live when possible without respawning:

| Setting | Behaviour |
| --- | --- |
| **Off** | Exact current baseline: skip all separation grid building, neighbour queries, and pushes. |
| **Normal (default)** | One solver pass each simulation tick. Default recommendation for gameplay/performance. |
| **Strong** | Two solver passes each tick (rebuild neighbour grid between passes if corrected positions changed enough to require it). More robust in very dense crowds; higher CPU cost. |

- Enable separation for **both factions**, and across friendly/enemy pairs, in **Movement / Melee / Ranged** modes. Rendering-only remains untouched.
- Use a tiny tunable extra gap (suggest **0.05 world units**) and sensible per-tick maximum displacement/correction limits. Expose these through BENCH0 tuning resources (suggest `Gap = 0.05`, `NeighbourBudget = 32`, `SeparationStrength` or correction cap) with clamping and clear Inspector labels.
- An unavoidable crowded/jammed situation may retain slight overlap; **no claim of absolute collision-free movement**. The visible goal is substantially less stacking and a more believable battle line, without jitter or high counts grinding to a halt.
- Movement and separation should feel natural when one side wins: soldiers should keep pursuing enemies rather than freezing after their local neighbours fill the path.
- **Pentagons have a larger footprint** and should push/stand farther apart than circles or triangles. Use `Catalog[units.Type[id]].Footprint`, not mesh height, as the physical XZ radius.

## Algorithm — bounded, deterministic, batched

1. Preserve the existing fixed-step movement/pursuit and combat architecture. Calculate normal desired movement as today, then resolve local penetration before the tick's combat/projectile results use positions. **Do not mutate positions while querying a stale spatial index for other actors without addressing consistency.**
2. Build a **small-cell broadphase** over all living soldiers, **regardless of faction**, indexing XZ positions. Use a reusable uniform-grid bucket structure (linked-ID arrays, recyclable heads or equivalent) sized for the current deployment. Choose cell width relative to the largest unit footprints (e.g. around 2.5–3.0 units initially), but **query enough neighboring cells** to cover `own radius + maximum other radius + gap`. A fixed 3×3 scan is only correct when this bound fits the chosen cell size. Do not incur O(n²) checks or build an enormous dense array covering the entire map.
3. For each local pair only once (e.g. `otherId > id`), compute XZ squared distance. Minimum centre distance is **`rA + rB + gap`**. If below threshold, compute a bounded push along the separating direction, distributing it between both soldiers (equal push is acceptable initially; configurable mobility weighting only if necessary). Use **preallocated per-unit displacement arrays** and accumulate corrections, then apply them together in a separate pass to avoid sequential/order-biased mutation.
4. For exactly coincident positions, choose a **stable deterministic fallback direction from unit IDs**, not random per-frame angles. Clamp maximum per-unit correction and optionally use a small dead zone/slop so nearly-touching pairs don't vibrate. Preserve finite coordinates and do not introduce per-tick allocations.
5. Prefer one correction pass for Normal, at most two for Strong. Any candidate-budget limit (suggest ~**32 evaluated neighbours per unit**, tune after owner feedback) **must be counted and shown in instrumentation**. It is a safeguard, **not proof of complete collision avoidance**. Avoid systematic bias toward low-ID soldiers or one faction; use deterministic ordering/rotation where appropriate.
6. Dead units must never block others. Clear/reinitialize reusable arrays and grid state correctly on mass deaths, clear, repeat spawn, painter battles and scene teardown.
7. All correction positions are authoritative: mark changed soldiers `Dirty` so the existing MultiMesh renderer updates them. Update `Y` via `BattlefieldHeight.At(X,Z)` if required; do not modify orientation, health, attack order, speed, cooldown, or resource definitions.
8. **Combat/projectiles consistency:** current `SpatialGrid.Rebuild` may run **before** movement and supplies projectile broadphase. Separation adds displacement after movement. Either rebuild a necessary index at a safe point or conservatively extend existing search padding based on **maximum movement plus maximum separation push**, without adding unnecessary duplicate expensive rebuilds. Keep first-enemy projectile-hit and targeting correct after position corrections. Explicitly document the approach.
9. Avoid repeated `GetNode`/resource lookup, transient lists/dictionaries in hot loops, global all-pairs correction, per-soldier physics, and costly periodic scene redraws. Prefer contiguous arrays and flat loops.

### Suggested tick ordering (adapt to current code as necessary)

```text
Target/spatial-grid refresh (existing)
    ↓
Desired movement / pursuit (existing)
    ↓
Build separation grid from moved, living positions
    ↓
Resolve neighbour penetration → batched corrections (1 or 2 passes)
    ↓
Combat / projectile stepping (preserve accurate hit broadphase)
    ↓
Existing MultiMesh sync
```

If current ordering differs, make the smallest safe integration, retaining correct target selection and projectile interception. No need to separate armies during spawn preparation; applying the solver once battle begins is fine, but avoid an enormous start-of-battle jump if preset layouts initialize overlapping footprints.

## Performance and instrumentation

The **owner will run FPS testing**. The solver should have predictable per-tick work:

- No per-soldier nodes/physics and no allocations in steady state after initial capacity.
- O(living units + candidate checks), with explicit neighbour caps/counters; support 1k, 10k, and approximately 20k total soldiers as the practical comparison points.
- **Off** should return to the existing baseline with minimal overhead.
- Put one separated timing in the BENCH0 overlay: **`Separation CPU ms/frame`** (and include its work in overall simulation time). Show **overlapping pairs corrected**, **neighbour budget hits** / capped queries, and optionally **max observed penetration** gathered from the normal solver pass; do not add extra full-field scans just for stats.
- Track costs during movement and especially large head-on melee collisions. Don't cite or promise a specific FPS gain/loss before measurements.
- Use exactly the same seed, counts, layout, soldier mix, zoom and battle phase when comparing Off/Normal/Strong. Remember that soldiers die at different rates with separation, so compare living counts and stage.

## Edge cases and acceptance

The owner should be able to confirm in Godot:

- **1k–20k live units:** Off visibly overlaps, Normal forms denser-but-distinct crowds, Strong reduces overlaps further without obvious jitter.
- Soldiers of the **same team and opposite teams** do not routinely interpenetrate, even at contact fronts; some residual overlap under worst-case crowding is acceptable.
- Mixed circle/square/triangle/diamond/**large pentagon** correctly honours individual footprint radii; no common-radius approximation.
- Stationary/engaged soldiers also separate; they don't require a move command to push apart.
- Cavalry traverses crowds without routinely teleporting or passing straight through infantry; acknowledge full obstacle routing/flanking is a future task.
- No NaN/unstable positions from identical spawn points; no lingering obstruction from corpses; no unbounded velocity or jitter spikes.
- Ranged shots, melee range, target pursuit, saved/loaded painted deployments, spawn/clear/reset and death rendering still work.
- Rendering-only stays the untouched renderer baseline. The performance panel accurately reports solver time and limiting counters.
- 25k/50k/100k presets should **still exist**; performance at these higher workloads may be worse and is not a primary SEP1 acceptance target.

## Implementing and handing off

1. Add `SeparationSystem` (+ helper grid only if needed), modes and exposed knobs.
2. Integrate into movement/fixed-step scheduling without changing unit combat stats, save identities, current polygon geometry, render batching, existing pursuit or projectile rules.
3. Add BENCH0 UI mode control and diagnostic timing/counters.
4. Update `SIMULATION/README.md`, relevant BENCHMARK docs, and root milestone status; append **one short implementation note to `devlog.md`** when coding is actually complete. **Do not log this plan as an implemented feature.**
5. Inspect changed diff, run **at most one readily available `dotnet build`**, and skip extensive automated tests/benchmarks under `NOTES/CodePreferences.md`. Owner does extensive Godot FPS/visual testing.
6. Make **one focused commit and push to `main`**, then report the SHA, changes, limitations, and quick in-game verification steps. Mark **Source Implemented / Awaiting Owner Validation**, not Accepted.

**Explicit scope:** SEP1 separation system and minimal integration only. Do not implement a general crowd AI/navmesh, campaign or advanced formation engine.
