# RENDERING

`ArmyRenderer.cs` creates up to eighteen MultiMesh batches (three factions × six unit types), allocates exact deployment capacities and packs live slots on death. Dirty entries update reusable CPU buffers. Dense changes upload one buffer per affected batch. Sparse changes update individual slots. Projectile drawing lives in [PROJECTILES](../PROJECTILES/README.md). Soldier meshes are shared between factions and authored by `FacetedSoldierMeshes.cs` with brighter top faces and directionally darker sides (baked per-vertex grayscale tints). The existing MultiMesh blue/red/green instance colours multiply those tints; white damage flashes remain visible with darker side facets. Materials remain unshaded, requiring no lights or dynamic shadows; geometry and shading are shared per unit type. Shadows are disabled. Render sync runs on the main thread and is measured separately from simulation. Whole-batch culling and no LOD; no per-soldier nodes. BENCHMARK owns renderer creation/disposal.

## FB0 — Future hit-feedback ownership

[Hit feedback](../FEEDBACK/README.md) supplies flash and scale to the shared packed transforms. Both upload paths apply the temporary pulse to the shared mesh and use the compiled height for the ground-anchored centre offset. UNIT1 applies permanent size when building the mesh.

## VIS2 — Refined soldier silhouettes

`FacetedSoldierMeshes.cs` now generates a narrow sloped top-edge bevel with an intermediate baked lightness for each shape. Circle infantry has 16 radial sides instead of 12. The diamond cavalry is about 1.9:1 in XZ silhouette, elongated along the default X-axis marching direction (about 1.51 long × 0.80 wide at the UNIT1 default of 1.25×) inside its 0.7875 gameplay footprint radius. Geometry is created once per unit type using one unshaded material, with the existing faction/type MultiMesh batches, packed transforms and feedback effects unchanged. Slightly more triangles per soldier can increase GPU geometry throughput, so compare FPS at 10k–20k units.

Soldiers turn toward deliberate movement, including charges, and keep their last heading while stationary. The packed transform contains yaw and hit-pulse scale, shared by the sparse and dense upload paths. Separation and shove corrections do not change facing.

Smooth soldier motion is on by default. It blends the latest two simulation poses and adds roughly one tick of visual latency. Extra uploads between ticks are limited to about 60 Hz. Projectile poses and hit feedback still update on simulation ticks. Disable smoothing for tick-only soldier uploads.

Circle Infantry bypasses visual yaw interpolation and rotation calculations because its shared mesh is rotationally symmetric. The renderer still interpolates Circle position and applies hit-pulse scaling, flash colour and MultiMesh uploads as before. Other unit shapes retain smooth turning.
