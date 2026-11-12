# Claude’s obstacle and separation optimization notes

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
