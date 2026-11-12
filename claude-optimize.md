# Claude’s obstacle, separation, and targeting optimization notes

Review of `ben0kay/godot-massive-rts`, with implementation status checked against `main` on 2026-11-12. Claude reviewed an earlier snapshot, before mesh-fitted footprints and the obstacle profiling changes. Its original cost rankings were estimates from source review. Keep timings from different battle layouts, hardware, and simulation stages separate.

## What the review found

Keep the static obstacle BVH, exact swept-circle tests, shared corner routes, and dense SEP1 grid for now. Queries reuse memory. The main performance question is how often the battle traverses the BVH.

| Suspected cost | Where to look |
| --- | --- |
| Long corridor queries | `ObstacleNavigation.Steer` and `CanApproach` can test several segments for each moving unit. |
| Movement and separation clips | Ordinary movement and each separation pass can sweep the same soldier against walls in one tick. |
| Line of sight | In-range movement checks and visible-target search can probe multiple candidates behind walls. The visible-target search keeps looking when a candidate is blocked. |
| Route misses and builds | Fine 0.5-unit sector keys can reduce sharing. Each build tests access to graph corners. Waiting units can repeat cache lookups and first-hit queries. |
| Startup | Each unit type builds its corner graph eagerly. Identical footprint radii could share a graph. |
| Crowd grid | A large deployed unit widens SEP1 cells for everyone. Watch candidates per soldier, budget hits, and penetration before changing the grid. |

A painted battle with 3,330 soldiers and six compiled wall rectangles showed about 45 FPS after two middle walls were added, versus the owner’s reported 60 FPS before. One HUD interval showed 64,269 corridor calls, 40,570 movement clips, and 11 route builds. The sample points to repeated queries as a place to investigate. It does not establish the cost of each subsystem in a controlled A/B run.

## Status on main

| Change | Status |
| --- | --- |
| Footprints fitted to meshes | `1e1108a`. A large deployed unit can still increase the separation grid’s cell width. |
| Query counters and timing | `11526bf`. **Obstacles and terrain → Sample obstacle queries** counts corridor, clip, sweep, sight, and overlap calls. Timing samples one in 64 calls per category. The old per-soldier steering stopwatch was removed. |
| Direct-approach corridor reuse | `bfb1efa`. **Reuse corridor checks** caches exact endpoint matches and results with a verified half-unit margin. This covers the direct-approach decision. Waypoint legs and physical clips still run their own checks. |
| Wall sliding | `f1ea1a3`. Clipped ordinary movement and separation try a safe axis-aligned remainder. Reverted after the owner still reproduced clumps at wall ends. The follow-up uses Claude’s bounded waypoint look-ahead, one-corner recovery, and 0.35-unit corner clearance. Gameplay validation is pending. |
| Tick-budgeted route builds | Pending. The queue and retry ages still use rendered frames. |
| Golden trace and fixed benchmark fixtures | Pending. |

## Suggested order

1. **Capture a baseline.** Use the same seed, painted layout, troop mix, zoom, and battle stage. Compare no walls, a single long wall, and a busy layout with multiple rectangles. Keep a corner funnel for crowd throughput. Record simulation and separation ms/tick, obstacle calls and sampled cost, route builds/queue/wait, candidate caps, and skipped ticks. Compare corridor reuse on and off under the same sampling settings.
2. **Make route scheduling tick-based.** Process a bounded number of requests or work units per simulation tick. Use tick-based retry age and wait metrics. A wall-clock budget would still let hardware change route availability. The new schedule intentionally changes when routes become available, so establish a fresh baseline afterward. Verify the same fixed-tick state across different render FPS caps.
3. **Add a conservative clearance shortcut if clips remain expensive.** Consider a sparse or bounded grid with roughly one-unit cells. Store a rounded-down lower bound on distance to the nearest wall or battlefield edge, accounting for the cell half-diagonal. A short move can bypass the BVH when its full swept disc fits within the certified clearance. Keep exact tests for close calls and outside the indexed area. The painter spans roughly 5,000 × 5,000 world units, so a dense one-byte grid would be about 25 MB before overhead. Claude’s smaller estimate assumed a roughly 540-unit arena.
4. **Remove a redundant movement sweep only when the exact step is certified.** A prefix of a verified corridor can reuse that proof. Keep boundary enforcement and sweep any step changed by sidestepping, waiting fallback, or another movement adjustment.
5. **Profile before expanding the work.** Try lazy or shared graphs if setup matters. If route builds dominate, measure fine-sector misses and consider lazy graph seeding. If LOS dominates, evaluate a conservative visibility cache first. A LOS probe cap changes targeting and should be tested as such. A type-pair separation-weight table is a small optimization compared with candidate volume. Leave all-pairs routing, parallel queries, and flow fields for measured needs. Flow fields are a better fit for future shared group destinations than for soldiers chasing individual moving targets.

## Wall funnels and crowd behavior

Wall clipping can cancel lateral movement when a push has an inward component. A pinned soldier also cannot yield its full share of the correction. Claude proposed wall-aware weighting and projecting the remaining push along the wall tangent. These change crowd behavior. Validate them with a funnel using throughput, clipped moves, and penetration, alongside the exact same wall layout for comparison.

The slide patch addressed one movement symptom. For the remaining stall, capture one stuck soldier’s target, next waypoint, route/cache outcome, and displacement before and after separation. This can distinguish a missing or unusable route from a usable route whose exit is crowded. Review `NAVIGATION/ObstacleNavigation.cs`, `OBSTACLES/ObstacleField.cs`, `SIMULATION/MovementSystem.cs`, `SIMULATION/SEPARATION/SeparationSystem.cs`, `SIMULATION/MeleeSteering.cs`, and `COMBAT/CombatSystem.cs`. Preserve the exact painted deployment for reproduction.

## Verification

For changes intended to preserve behavior, compare per-tick hashes of position, target, and health against the appropriate baseline. Exercise the accelerated obstacle queries against the original BVH with randomized queries and cases touching corners, walls, and the arena boundary. Keep crowd behavior changes separate from performance comparisons so a faster run can be interpreted alongside its battle outcome.

## Long-range pursuit follow-up — 2026-11-12

The laptop benchmark used 10,000 melee soldiers in Opposing lines on flat ground with the test wall off. At about 9,100 alive, with **Slower long-range retargeting** on, the HUD showed 31.4 ms per simulation tick and three ticks per rendered frame in that sample.

| CPU cost | ms per simulation tick |
| --- | ---: |
| Long-range pursuit queries | 8.28 |
| Pursuit tree build | 0.69 |
| Local enemy search | 4.86 |
| Movement | 7.95 |
| Separation | 5.45 |

Earlier screenshots showed about 8.5–8.7 ms/tick for pursuit, with no verified matched toggle comparison. The toggle doubles the scheduled interval for pursuing a living target from roughly 0.8 to 1.6 simulation seconds. The replacement schedule and extra budget are unchanged. The snapshots leave open how much work the toggle removes and how much time replacements consume. These measurements are separate from the wall-navigation results above.

### Measure query types first

Track counts and CPU time separately for these paths in `CombatSystem.Acquire` and `SpatialGrid.FindPursuitEnemy`. Keep tree-build time separate.

| Query type | When it runs |
| --- | --- |
| Live-target refresh | A soldier has a living target outside local range and its scheduled pursuit phase is due. The toggle can skip this refresh. |
| Scheduled acquisition | The pursuit phase is due and the soldier has no living target, including when its previous target died. |
| Extra replacement | A soldier has a dead target and local refresh failed to replace it. This path has a shared budget of 128 queries per tick and scans soldiers in ID order. |

Start with the three counts and timings in the HUD, plus extra-budget denials. Also useful are live refreshes eligible versus run versus skipped, budget use, and the number of soldiers still following dead waypoints. Frequent denials would warrant checking whether higher IDs wait longer. Compare at the same battle stage with the same seed and settings. Normalize by simulation ticks when rendered frames contain different numbers of ticks.

Claude also proposed a per-tick CSV and deeper counters in `OccupiedCellTree.Search` if the HUD split leaves questions open. Track nodes entered and pruned, leaves and cells scanned, soldiers checked, improvements to the best candidate, checks after the last improvement, and final distance buckets. A diagnostic counter could record how often a scanned cell could have been pruned by its distance bound.

### Candidate optimization — prune cells inside tree leaves

The current tree prunes nodes by their bounds. Once a leaf passes that test, its loop walks the soldiers in each occupied cell. Claude proposed skipping a cell when a conservative lower bound on its distance is at least the current best squared distance. Check that the bound covers the positions the query actually reads, uses the same distance metric, and preserves the existing equal-distance rule. Compare target IDs against the original search before measuring CPU time. The added bounds check is worthwhile only if it avoids enough soldier checks.

Claude described a Python replica using synthetic opposing blocks with 4,550 soldiers per side and 600 seekers. The message cut off before the results, so there is no reported speedup from that replica. The query split, deeper counters, and cell pruning are proposals.

### Additional pursuit diagnostics

- Count target changes per query reason, distinguishing live refreshes that keep their incumbent from those that switch. Track within-local-range and slow-refresh gate skips separately, units with no living target at the start of acquisition, deaths per tick, and ticks that consume all 128 extra replacements.
- Record maximum and p95 soldiers examined per query, split by reason. Use cell and soldier counts to distinguish avoidable scans from genuinely dense cells. The suggested 40% wasted-soldier share is a heuristic. The proposed 15–30 µs per query is an estimate, with no measured per-reason timings yet.
- For a diagnostic run, temporarily reduce `ExtraPursuitQueriesPerTick` from 128 to 0 and compare pursuit ms and query counts. This changes replacement timing and can change the battle, so keep it separate from the cell-pruning comparison.

### Implemented cell-pruning comparison

**Live combat → Prune pursuit cells** defaults to on and can be changed mid-battle. Each visited leaf compares the padded XZ bounds of each cell with the best squared distance so far. If even the closest point in the cell cannot beat the incumbent, the search skips that cell's soldier list. Cell order and strict closer-than tie handling are preserved. The checkbox is independent of the slower retargeting toggle.

For comparison, start the same seeded deployment twice and keep refresh cadence fixed. Compare **Long-range pursuit** over the same simulation tick window and check per-tick target hashes when available. Compare equivalent ticks per rendered frame when using the current HUD's per-frame CPU figures. No in-game speedup or replay match has been measured.

### Conditional proposal — share replacements for the same dead target

If replacement queries dominate, consider caching one replacement per dead target and tick. The first eligible orphan queries normally, and later orphans whose previous target is the same can reuse the result if it is still alive. Keep local acquisition first. Because the shared replacement can change who soldiers pursue, compare the median and p95 extra distance against each orphan's own nearest result. Roughly one cell is a proposed tolerance. Leave this pending until the per-reason measurements justify it.
