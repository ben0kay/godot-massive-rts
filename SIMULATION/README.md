# SIMULATION

`UnitState.cs` owns stable-ID, world-space soldier arrays and the flat-ground height seam. `MovementSystem.cs` performs shared marching; `SpatialGrid.cs` reuses linked-cell storage with faction-separated heads and bounded queries. BENCHMARK configuration supplies limits. Dead IDs remain allocated until reset, but are excluded from simulation/indexing and removed from rendering. Positions are authoritative; there are no per-unit scene nodes. Terrain elevation uses a flat placeholder.

## SEP1 — Soldier separation

The [SEP1 plan](SEPARATION/SEP1_IMPLEMENTATION_PLAN.md) is source implemented and awaiting owner validation. After movement, a shared XZ grid indexes living soldiers from both teams. Each pass caches four neighbouring bucket heads per occupied cell and visits each eligible pair once. The solver accumulates pushes, damps corrections with multiple contacts, then applies the results together. Normal uses one pass. Strong rebuilds the grid for its second pass and shares the same per-tick push cap. Off bypasses the solver.

The defaults are a 0.05-unit gap, 32 examined entries per soldier per pass, and a 0.35-unit push cap per tick. Melee holds near the edge of its existing attack reach to avoid walking into packed bodies each tick. Projectile queries pad the pre-movement grid lookup by the maximum ordinary movement plus the separation cap. The candidate budget and displacement cap can leave overlap in crowded fights. SEP1 resolves local overlap and uses direct pursuit on the flat battlefield.
