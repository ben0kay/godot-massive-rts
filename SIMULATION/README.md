# SIMULATION

`UnitState.cs` owns stable-ID, world-space soldier arrays and the flat-ground height seam. `MovementSystem.cs` performs shared marching; `SpatialGrid.cs` reuses linked-cell storage with faction-separated heads and bounded queries. BENCHMARK configuration supplies limits. Dead IDs remain allocated until reset, but are excluded from simulation/indexing and removed from rendering. Positions are authoritative; there are no per-unit scene nodes. Terrain elevation uses a flat placeholder.

## SEP1 — Soldier separation

The [SEP1 plan](SEPARATION/SEP1_IMPLEMENTATION_PLAN.md) is source implemented and awaiting owner validation. After movement, a shared XZ grid indexes living soldiers from both teams. Each pass counting-sorts living soldiers into padded cells and reads neighbouring candidates from two contiguous ranges. Cells align to world coordinates at a given width. Sparse spreads widen the cells to bound the grid scan. Over-budget searches sample a deterministic rotating window. The solver accumulates pushes, damps corrections with multiple contacts, then applies the results together. Normal uses one pass. Strong rebuilds the grid for its second pass and shares the same per-tick push cap. Off bypasses the solver.

The defaults are a 0.05-unit gap, 32 examined entries per soldier per pass, and a 0.35-unit push cap per tick. Melee holds near the edge of its existing attack reach to avoid walking into packed bodies each tick. Projectile queries pad the pre-movement grid lookup by the maximum ordinary movement plus the separation cap. The candidate budget and displacement cap can leave overlap in crowded fights. SEP1 resolves local overlap and uses direct pursuit on the flat battlefield.

## SEP2 — Allied crowd yielding (planned)

[SEP2 plan](SEPARATION/SEP2_IMPLEMENTATION_PLAN.md) proposes a narrow **allies-only** exception to SEP1: advancing melee (including basic Circle infantry) can move through **stationary allied ranged** soldiers with 50% of the normal combined footprint clearance, while standing archers yield only a tiny amount. All enemy interactions and other soldier pairs retain SEP1 separation. SEP2 reuses SEP1's existing neighbour grid and remains **planned, not implemented**.
