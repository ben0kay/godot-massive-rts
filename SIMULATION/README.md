# SIMULATION

`UnitState.cs` owns stable-ID, world-space soldier arrays and the flat-ground height seam. `MovementSystem.cs` performs shared marching; `SpatialGrid.cs` reuses linked-cell storage with faction-separated heads and bounded queries. BENCHMARK configuration supplies limits. Dead IDs remain allocated until reset, but are excluded from simulation/indexing and removed from rendering. Positions are authoritative; there are no per-unit scene nodes. Terrain elevation and collision avoidance are not implemented.

## SEP1 — Planned soldier separation

The next proposed subsystem is [SEP1: High-Count Soldier Separation](SEPARATION/SEP1_IMPLEMENTATION_PLAN.md). It adds an allocation-bounded, local XZ footprint solver with **Off / Normal / Strong** BENCH0 comparison modes, both-team separation and dedicated CPU/candidate instrumentation. **Plan only — not implemented yet.** See the plan for integration with the existing spatial-grid/projectile code and owner-side acceptance checks.
