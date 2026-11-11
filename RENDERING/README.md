# RENDERING

`ArmyRenderer.cs` creates up to fifteen MultiMesh batches (three factions × five unit types), allocates exact deployment capacities and packs live slots on death. Dirty entries update reusable CPU buffers. Dense changes upload one buffer per affected batch. Sparse changes update individual slots. Projectile drawing lives in [PROJECTILES](../PROJECTILES/README.md). Soldier meshes are shared between factions and authored by `FacetedSoldierMeshes.cs` with brighter top faces and directionally darker sides (baked per-vertex grayscale tints). The existing MultiMesh blue/red/green instance colours multiply those tints; white damage flashes remain visible with darker side facets. Materials remain unshaded, requiring no lights or dynamic shadows; geometry and shading are shared per unit type. Shadows are disabled. Render sync runs on the main thread and is measured separately from simulation. Whole-batch culling, no interpolation/LOD; no per-soldier nodes. BENCHMARK owns renderer creation/disposal.

## FB0 — Future hit-feedback ownership

[Hit feedback](../FEEDBACK/README.md) supplies flash and scale to the shared packed transforms. Both upload paths use the same scale and ground-anchored centre offset.

## VIS2 — Refined soldier silhouettes

`FacetedSoldierMeshes.cs` now generates a narrow sloped top-edge bevel with an intermediate baked lightness for each shape. Circle infantry has 16 radial sides instead of 12. The diamond cavalry is about 1.9:1 in XZ silhouette, elongated along the default X-axis marching direction (~1.21 long × 0.64 wide) while remaining inside its unchanged 0.63 gameplay footprint radius. Geometry is created once per unit type using one unshaded material, with the existing faction/type MultiMesh batches, packed transforms and feedback effects unchanged. Slightly more triangles per soldier can increase GPU geometry throughput, so compare FPS at 10k–20k units.
