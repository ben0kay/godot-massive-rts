# SIMULATION

`UnitState.cs` owns stable-ID, world-space soldier arrays and the flat-ground height seam. `MovementSystem.cs` performs shared marching; `SpatialGrid.cs` reuses linked-cell storage with faction-separated heads and bounded queries. BENCHMARK configuration supplies limits. Dead IDs remain allocated until reset, but are excluded from simulation/indexing and removed from rendering. Positions are authoritative; there are no per-unit scene nodes. Terrain elevation uses a flat placeholder.

## SEP1 — Soldier separation

The [SEP1 plan](SEPARATION/SEP1_IMPLEMENTATION_PLAN.md) is source implemented and awaiting owner validation. After movement, a shared XZ grid indexes living soldiers from both teams. Each pass counting-sorts living soldiers into padded cells and reads neighbouring candidates from two contiguous ranges. Cells align to world coordinates at a given width. Sparse spreads widen the cells to bound the grid scan. Over-budget searches sample a deterministic rotating window. The solver accumulates pushes, damps corrections with multiple contacts, then applies the results together. Normal uses one pass. Strong rebuilds the grid for its second pass and shares the same per-tick push cap. Off bypasses the solver.

The defaults are a 0.05-unit gap, 32 examined entries per soldier per pass, and a 0.35-unit push cap per tick. Melee holds near the edge of its existing attack reach to avoid walking into packed bodies each tick. Projectile queries pad the pre-movement grid lookup by the maximum ordinary movement plus the separation cap. The candidate budget and displacement cap can leave overlap in crowded fights. SEP1 resolves local overlap and uses direct pursuit on the flat battlefield.

## SEP2 — Allied crowd yielding (planned)

[SEP2 plan](SEPARATION/SEP2_IMPLEMENTATION_PLAN.md) proposes a narrow **allies-only** exception to SEP1: advancing melee (including basic Circle infantry) can move through **stationary allied ranged** soldiers with 50% of the normal combined footprint clearance, while standing archers yield only a tiny amount. All enemy interactions and other soldier pairs retain SEP1 separation. SEP2 reuses SEP1's existing neighbour grid and remains **planned, not implemented**.

## SEP2 — Allied archer yielding

When enabled alongside Normal or Strong, advancing melee can approach stationary allied ranged soldiers to half the sum of their footprint radii. When closer, a small correction is split 70% to melee and 30% to ranged. Other allied melee–ranged pairs return to full clearance with a soft correction cap. Opposing pairs and same-role pairs use SEP1. Both corrections share SEP1's overall displacement cap. A dense allied wall can still slow or stall units.

Combat movement ramps to the authored top speed over 0.15 seconds for melee and 0.32 seconds for ranged, with braking near the existing stop distance. The benchmark resource exposes both ramp durations. A zero duration gives immediate movement for that role. Movement-only keeps its constant-speed lanes.

Stationary ranged soldiers hold their position while a living target is in exact attack range and wait for a replacement target after a kill. An advancing ranged soldier settles up to 0.25 world units inside range to absorb small shifts in target distance. Firing still uses the authored range.

Blue, Red and Green treat every other team as hostile. Local targeting picks the nearest examined enemy within its existing candidate budget. Long-range pursuit compares the nearest live candidates across opposing team trees. Area attacks and projectile sweeps use the same faction filter, and SEP2 compares the full team ID for ally yielding.
