# RENDERING

`ArmyRenderer.cs` creates up to ten MultiMesh batches (two factions × five unit types), allocates exact deployment capacities and packs live slots on death. Dirty entries update reusable CPU buffers. Dense changes upload one buffer per affected batch. Sparse changes update individual slots. Projectile drawing lives in [PROJECTILES](../PROJECTILES/README.md). Soldier meshes are shared between factions and authored by `FacetedSoldierMeshes.cs` with brighter top faces and directionally darker sides (baked per-vertex grayscale tints). The existing MultiMesh blue/red instance colours multiply those tints; white damage flashes remain visible with darker side facets. Materials remain unshaded, requiring no lights or dynamic shadows; geometry and shading are shared per unit type. Shadows are disabled. Render sync runs on the main thread and is measured separately from simulation. Whole-batch culling, no interpolation/LOD; no per-soldier nodes. BENCHMARK owns renderer creation/disposal.

## FB0 — Future hit-feedback ownership

[Hit feedback](../FEEDBACK/README.md) supplies flash and scale to the shared packed transforms. Both upload paths use the same scale and ground-anchored centre offset.
