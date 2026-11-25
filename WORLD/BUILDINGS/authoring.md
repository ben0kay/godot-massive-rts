# Defense & Structure Visual Authoring Guide

> **Status:** Broadside two-tone visual direction accepted by owner; Railgun source implemented, awaiting visual validation. This guide sets the visual language for future approved tower and structure work, not permission to redesign every building.
>
> **Read first:** root [README](../../README.md), [CodePreferences](../../CodePreferences.md), [endgoal](../../endgoal.md), and [Tower Implementation Standard](TOWER_IMPLEMENTATION_STANDARD.md). The latter governs gameplay, placement and lifecycle; this guide governs appearance.

## 1. Art direction

Build original, futuristic low-poly military machines with readable hard-surface geometry for the orthographic isometric camera. The accepted Broadside is the primary reference; Railgun is the compact precision-weapon interpretation. Aim for a premium geometric model, not a photorealistic prop. Soldiers remain simpler and the battlefield must stay readable with thousands of them.

- **Neutral chassis:** all factions share dark charcoal / gunmetal steel. Use several related shades for plinths, recessed seams, inner mechanisms and brighter bevels. Do not tint the entire tower by faction.
- **Faction armor:** select meaningful armor plates, corner caps, shoulder pods or weapon collars. Keep enough color visible from above and both sides to recognize ownership at normal gameplay zoom. As a starting visual heuristic, use roughly 65–75% neutral structure, 20–30% faction panels, and a small remainder of illuminated trim (not a measured or mandatory surface-area ratio).
- **Faction emission:** short power strips, status panels and a focal core, matching the owning faction. Use bright cores against dark recessed channels; avoid outlining every edge or making the whole model glow.
- **Shape language:** layered clipped rectangles, octagonal collars, chamfered slopes, overlapping armor and clean dark gaps. Use larger details that survive zoom; a few large vents are better than tiny bolts, noise or dense texture patterns.
- **Distinct roles:** Broadside is broad and heavy with two independently recoiling launch tubes. Railgun is narrower, with a compact mount and a single long split-rail accelerator. Shield support can instead use a recognizable generator/ring silhouette. Do not copy one weapon silhouette onto every tower.
- **Lighting:** use shared battlefield directional lighting and restrained satin highlights. Baked bevel masks complement real light; do not add new global exposure, sun, ambient or glow settings just for a model.

## 2. Existing implementation references

| File | Responsibility |
| --- | --- |
| `RENDERING/BroadsideTowerMeshes.cs` | Accepted heavyweight geometry: normalized base, rotating head and two identical independent barrels |
| `RENDERING/RailgunTowerMeshes.cs` | Compact base, separate rotating carriage and one recoiling/pitching accelerator assembly |
| `RENDERING/TowerArmorGeometry.cs` | Shared `Steel`, `Paint`, `Trim`, `Block`, `Plate` helpers |
| `RENDERING/ShieldGeneratorMeshes.cs` | Static radial reactor crown, armored base and emitter pylons |
| `RENDERING/BroadsideArmor.gdshader` | Shared two-tone mask shader (historical filename, used by all three towers) |
| `RENDERING/ShieldFieldDomeRenderer.cs` / `ShieldFieldDome.gdshader` | Optional faint stationary coverage indicator; separate compact batch |
| `RENDERING/TowerLighting.gdshaderinc` | Shared directional light and satin highlight |
| `RENDERING/DefenseRenderer.cs` | Cached meshes, MultiMesh allocation, faction data, placement, aiming, hide/reset, beam/flash batches |
| `RENDERING/TowerArmor.gdshader` | Legacy material for other tower visuals; do not silently switch unrelated structures |

The Shield Generator now shares the armor shader while retaining its static support silhouette. Command buildings still have their own render paths. Apply the language when their redesign is requested, adapting it to their function rather than duplicating the weapon renderer.

## 3. Mesh and material conventions

Create a small static mesh builder per tower visual; cache each mesh once in the owning renderer. Use one `SurfaceTool` triangle surface per independently animated part, and the shared geometry primitives for armor. Neutral chassis, paint and emissive details are baked into the same surface, not separate meshes/draw calls for each panel.

```csharp
using static MassiveRts.TowerArmorGeometry;
// RGB mask weights are not display colors.
Block(surface, new Vector3(0f, 0f, 0f), new Vector3(1f, 0.2f, 1f), Steel(0.9f));
Plate(surface, new Vector3(0f, 0.18f, 0f), new Vector3(0.7f, 0.2f, 0.7f),
    0.25f, 0.82f, Paint());
Block(surface, new Vector3(0f, 0.30f, 0f), new Vector3(0.3f, 0.04f, 0.08f), Trim());
```

- Red vertex channel = neutral steel weight, green = faction paint weight, blue = emissive trim weight. The helpers preserve face shading in these channels. Do not use normal RGB paint colors or white for the two-tone mask.
- The current shared shader uses neutral steel `vec3(0.12, 0.15, 0.19)`, faction data in `INSTANCE_CUSTOM.rgb`, and a lighter indicator plus faction emission for trim. It respects the existing battlefield ambient fill and directional light include.
- In `DefenseRenderer.Batch`, enable `UseCustomData` before setting instance count. Keep instance color white and supply the actual faction color via `SetInstanceCustomData` at placement/reuse, using `SetArmorTint`. Never multiply the mask by team color.
- Use the current material rather than creating a shader per faction or weapon. Preserve the current shadow policy. Emission is a material effect, not an OmniLight3D; visible bloom depends on the environment. Do not add point lights, particles or animated lights merely to decorate a tower.
- Preserve valid clockwise Godot winding, normals and face shading. Avoid coincident faces and intersecting decorative panels that hide the intended accent. Keep authored dimensions readable; use deliberate broad recess-like dark panels, not excessive boolean mesh detail.

## 4. Coordinate and animation contracts

- XZ is ground, Y is up, and local forward is -Z. Stationary base is normalized around zero (approximately -0.5 to +0.5 in X/Y/Z), scaled to actual footprint and base height by the renderer. Grounding uses terrain height.
- Head geometry is authored in world units relative to `DefenseRenderer.Pivot`; it rotates around Y. The weapon barrel uses its existing local transform and recoil axis. Bake attached rails, caps and trim into the moving part, not into the base.
- Railgun barrel is centered at zero, length 3.8; muzzle at local z = -1.9. The renderer offsets its center forward 1.9 from pivot and adds recoil along the pitched axis. Beam and flash must still exit the actual central opening. Split rails are cosmetic: one weapon, one beam.
- Broadside barrels are mounted at x = ±0.7, y = 0.9 from the pivot, tilted up 15°, with local muzzle z = -4.7. Each barrel recoils independently. Do not extend muzzle geometry past these endpoints without an explicitly approved coordinated transform change.
- Keep the authored footprint, collision, HP, range, timing, damage, projectile behavior and stable resource ID unchanged during visual-only tasks. Real collision and targeting come from the shared simulation, not decorative triangles.

## 5. Performance and acceptance

Use the existing base/head/barrel MultiMesh batches; add new variant batches only when necessary, not a node per decorative part or tower. No per-frame mesh generation, material clones, shader parameter churn, timers or new simulation queries. Extra triangles still cost GPU time; prefer modest silhouettes and a single surface per moving part. No unmeasured FPS claims.

For every new visual pass:
- Check original silhouette, faction readability at gameplay zoom and close inspection, in multiple faction colors and lighting conditions.
- Check placement/erase, save/reload, terrain grounding, preview vs runtime, and adjacent footprints.
- Fire/rotate/recoil, including rail beam slope and projectile muzzle alignment; check destroyed/hidden/reused slots and reset.
- Preserve untouched towers and effects; compare visuals in Benchmark, Level Editor and Fortress Defense as applicable.
- Follow root minimal validation policy: source review and one practical build if available; owner handles extended Godot and FPS validation.
- Update the buildings README and append one concise devlog entry. Report exact commit and any pending checks.

For entirely new tower mechanics or structures, also follow the Tower Implementation Standard and existing definition/catalog/placement contracts. This is an art/implementation guide, not a mandate to refactor all renderers.

### Shield coverage indicators

A dome visual represents the emitter's horizontal range, not a physical force field. Feed the compiled radius through the placement renderer, center it on the footprint, keep its face very transparent and its rim restrained. Reuse one compact MultiMesh, do not add per-generator simulation or per-recipient effects, and remove the dome on slot release/reset. The dome height is purely cosmetic; terrain may intersect the shell. No projectile blocking, collision, shield recharge changes or global glow changes belong in a visual pass.
