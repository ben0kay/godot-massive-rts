# TERRAIN — Unified Painted and Procedural Terrain

**TERR1-A is source implemented. Awaiting owner validation in Godot.**

Choose **Preset terrain → Test plateau**, then **Spawn / Reset selected**. The default plateau rises 4 world units and has a 20-unit smooth shoulder. Red starts on or near the raised area in Opposing Lines. Use Movement mode to watch the ranks climb and descend. Rise and shoulder are editable in BenchmarkConfig. The shoulder is kept at least three times as wide as the rise.

The arena builds static 32×32-cell chunks at 2.1-unit sample spacing. The mesh and grounding use the same triangle interpolation. Each displaced soldier is grounded once after movement, trample, and separation. The overlay reports chunk count, setup times, grounding samples and grounding time. Flat presets and painted deployments still use the flat arena. The optional preset wall moves onto the plain in Plateau mode.

The visual stomp ring remains level at its source, so part of it can intersect a slope. TERR1-A uses the current combat distance rules. Terrain editing and persistence (TERR1-B), seeded generation (TERR1-C), and terrain-aware slope routing, LOS, projectile contact and high-ground bonuses (TERR1-D) remain planned. Future routing should build on the existing OBST1 system.

See the [TERR1 implementation plan](TERR1_IMPLEMENTATION_PLAN.md).

## Command-level authoring

Command levels compile only the chunks around authored hills. The shared surface drives the visible terrain, pointer projection, troop grounding and building placement. [Level setup instructions](../../LEVELS/README.md) cover hill and unit placement scenes. Hills use radius and rise controls in this first pass. The command map still uses XZ obstacle routing on slopes.

## Procedural level authoring

`PROCEDURAL/ProceduralBattlefield.cs` generates seeded elevation samples and `WorldPaintData` for the Level Editor. River Divide keeps the banks level for crossings placed manually with Draw bridge. Units find routes through the generated terrain during play. The editor checks the candidate against the existing world-paint compiler before applying it. Generated maps save the editable output and use the existing terrain rendering and navigation systems in play.

## Tiered elevation

The Tiered Elevation tool writes absolute plateau heights with blended edges to the same samples as Organic Elevation. The default levels are 0, 3, 6, 9, 12, 15 and 18 world units. Edit `CommandTerrain.ElevationTierHeights` to customize the brush targets. Saved terrain and gameplay use the sampled surface.

## Geothermal vents

Vents are neutral terrain objects. Their default cycle is 8 seconds dormant, 2 seconds warning, and 1 second erupting. The eruption launches nearby ground units below Tier 4 once. Launched units temporarily suspend their ground movement and attacks while retaining their orders. Landing uses the shared terrain and movement blockers. Damage defaults to zero. Vents use the soldier spatial grid and track only active launches, with up to 32 vents per map.

## Defensive trenches

Trenches cut the compiled heightfield without baking over authored elevation samples. A trench has a floor and two ramps. Movement-only side blockers direct infantry to the ramps. Tier 1 units can use the interior while larger units route around it. Trench membership is spatially indexed, and direct projectile sweeps near the excavation check the sampled terrain. Tier 1 concealment ends on ramps and is suppressed for two seconds after firing. Ground enemies in the trench can detect its occupants. Overhead attacks still use their normal damage rules. Up to 16 trenches may be authored per map.
