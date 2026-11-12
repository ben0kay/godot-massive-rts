# TERRAIN — Unified Painted and Procedural Terrain

**TERR1-A is source implemented. Awaiting owner validation in Godot.**

Choose **Preset terrain → Test plateau**, then **Spawn / Reset selected**. The default plateau rises 4 world units and has a 20-unit smooth shoulder. Red starts on or near the raised area in Opposing Lines. Use Movement mode to watch the ranks climb and descend. Rise and shoulder are editable in BenchmarkConfig. The shoulder is kept at least three times as wide as the rise.

The arena builds static 32×32-cell chunks at 2.1-unit sample spacing. The mesh and grounding use the same triangle interpolation. Each displaced soldier is grounded once after movement, trample, and separation. The overlay reports chunk count, setup times, grounding samples and grounding time. Flat presets and painted deployments still use the flat arena. The optional preset wall moves onto the plain in Plateau mode.

The visual stomp ring remains level at its source, so part of it can intersect a slope. TERR1-A uses the current combat distance rules. Terrain editing and persistence (TERR1-B), seeded generation (TERR1-C), and terrain-aware slope routing, LOS, projectile contact and high-ground bonuses (TERR1-D) remain planned. Future routing should build on the existing OBST1 system.

See the [TERR1 implementation plan](TERR1_IMPLEMENTATION_PLAN.md).
