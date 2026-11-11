# TERRAIN — Unified Painted and Procedural Terrain

**Status: TERR1 planning-only; battlefields are still flat.**

See [TERR1 implementation plan](TERR1_IMPLEMENTATION_PLAN.md). Both future game modes use one authoritative chunked heightfield. Army Painter creates it with brushes for flat plateaus and smooth ramps. Open RTS creates it with deterministic low-frequency noise and flat building regions. Both share ground queries, rendering, navigation, projectile LOS and optional high-ground combat bonuses.

Planned milestones: **TERR1-A** common heightfield, static ramp/plateau demo, grounded soldiers; **TERR1-B** paint tools and persistence; **TERR1-C** procedural seeded generator; **TERR1-D** slope navigation, height combat advantage, terrain LOS and projectile collision. A future hybrid may paint overrides onto a procedural base.

OBST1 is a separate plan; when implemented, static obstacles and elevation must contribute to shared navigation, not competing soldier pathfinders. Create proposed code subfolders only as tasks are actually implemented.
