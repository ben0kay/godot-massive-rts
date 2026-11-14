# RENDERING

`ArmyRenderer.cs` creates up to twenty-one MultiMesh batches (three factions × seven unit types), allocates exact deployment capacities and packs live slots on death. Dirty entries update reusable CPU buffers. Dense changes upload one buffer per affected batch. Sparse changes update individual slots. Projectile drawing lives in [PROJECTILES](../../GAMEPLAY/PROJECTILES/README.md). Soldier meshes are shared between factions and authored by `FacetedSoldierMeshes.cs` with brighter top faces and directionally darker sides (baked per-vertex grayscale tints). The existing MultiMesh blue/red/green instance colours multiply those tints; white damage flashes remain visible with darker side facets. Materials remain unshaded, requiring no lights or dynamic shadows; geometry and shading are shared per unit type. Dynamic shadow casting is disabled. Render sync runs on the main thread and is measured separately from simulation. Whole-batch culling and no LOD; no per-soldier nodes. BENCHMARK owns renderer creation/disposal.

## FB0 — Future hit-feedback ownership

[Hit feedback](../FEEDBACK/README.md) supplies flash and scale to the shared packed transforms. Both upload paths apply the temporary pulse to the shared mesh and use the compiled height for the ground-anchored centre offset. UNIT1 applies permanent size when building the mesh.

## VIS2 — Refined soldier silhouettes

`FacetedSoldierMeshes.cs` now generates a narrow sloped top-edge bevel with an intermediate baked lightness for each shape. Circle infantry has 16 radial sides instead of 12. The diamond cavalry is about 1.9:1 in XZ silhouette, elongated along the default X-axis marching direction (about 1.51 long × 0.80 wide at the UNIT1 default of 1.25×) with its long tips at the 0.756 gameplay footprint radius. Geometry is created once per unit type using one unshaded material, with the existing faction/type MultiMesh batches, packed transforms and feedback effects unchanged. Slightly more triangles per soldier can increase GPU geometry throughput, so compare FPS at 10k–20k units.

Soldiers turn toward deliberate movement, including charges, and keep their last heading while stationary. The packed transform contains yaw and hit-pulse scale, shared by the sparse and dense upload paths. Separation and shove corrections do not change facing.

Smooth soldier motion is on by default. It blends the latest two simulation poses and adds roughly one tick of visual latency. Extra uploads between ticks are limited to about 60 Hz. Projectile poses and hit feedback still update on simulation ticks. Disable smoothing for tick-only soldier uploads.

Circle Infantry bypasses visual yaw interpolation and rotation calculations because its shared mesh is rotationally symmetric. The renderer still interpolates Circle position and applies hit-pulse scaling, flash colour and MultiMesh uploads as before. Other unit shapes retain smooth turning.

Ranged recoil applies a visual-only XZ displacement to the packed MultiMesh transform after normal pose interpolation. An active-ID feedback list marks only recoiling soldiers dirty during recovery. No changes to simulation positions or projectile origins.

Heavy Artillery adds one shared beveled mesh containing the concave chevron and its centered barrel. Both pieces turn and recoil together in the same instance.

## SHADOW1 — Ground shadows

The benchmark draws optional translucent oval shadows beneath soldiers and offset rectangular shadows for walls and defense footprints. Soldier batches share one oval mesh and reuse the army renderer's live slots, dirty updates and interpolated ground positions. Recoil and hit pulses affect the soldier mesh only. Static obstacle shadows are built with the battle layout. **Display and controls → Ground shadows** is enabled by default and stops shadow drawing and soldier shadow uploads when switched off.

## SHADOW1-B — Darker, tighter, directional shadows

The shared unshaded ground-shadow material now uses **0.52 alpha instead of 0.26** (about twice the opacity). Soldier shadow ellipse sizes are **80% of their previous size**, except **Pentagon (70%)** and **Hexagon (58%)**, keeping the large units' shadows compact. Their world-space offsets are **+0.40 footprint in X** and **−0.16 footprint in Z**, casting slightly right/down at the default 45° camera view to imply sunlight from the upper-left. Static obstacle/tower shadows use the same general direction, with a centered **10% footprint-size reduction**. This is a shared material/transform/mesh-only visual change: no individual light or shadow nodes, gameplay positioning, collision, death feedback, interpolation, or shadow-toggle logic changed. Source-implemented; pending owner visual validation at normal gameplay zoom.
