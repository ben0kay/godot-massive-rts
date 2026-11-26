# Faction Visual Authoring Guide — Units, Defenses & Structures

> **Status:** Shared faction art direction established by owner feedback on Broadside and Light Artillery. Railgun, Shield Generator and the refined Light Artillery are source implemented; individual visual and FPS acceptance remains with the owner. This guide applies to future approved visual work, not permission to redesign the entire roster.
>
> **Read first:** root [README](../README.md), [CodePreferences](../CodePreferences.md), [endgoal](../endgoal.md), [battlefield visual plan](visual.md), [Tower Implementation Standard](../WORLD/BUILDINGS/TOWER_IMPLEMENTATION_STANDARD.md) for structures, and [unit catalogue guide](../GAMEPLAY/UNITS/README.md) for units. Those documents govern gameplay, placement and lifecycle; this guide governs faction appearance.

## 1. Shared art direction and faction identity

Build original, futuristic low-poly military machines with readable hard-surface geometry for the orthographic isometric camera. The accepted Broadside is the primary reference; Railgun is the compact precision-weapon interpretation. Aim for a premium geometric model, not a photorealistic prop. Soldiers remain simpler and the battlefield must stay readable with thousands of them. Mobile units must be visibly more faction-colored than static buildings; the first Light Artillery pass was too metallic and the owner requested more faction paint.

### Color hierarchy

| Asset category | Neutral gunmetal | Faction paint | Emissive trim |
| --- | ---: | ---: | ---: |
| Defenses and faction buildings | ~70% | ~25% | ~5% |
| Detailed mobile units | ~35% | ~60% | ~5% |

These are approximate visual composition targets, not measured surface-area quotas or shader blending ratios. Aim for faction color dominance on mobile units, including at normal battle zoom. Small infantry can be almost entirely faction-colored and need little or no metal or emission. Larger mobile machines may use more metal where the role needs it, but should still read as mobile units rather than buildings. Unowned terrain and scenery follow the separate battlefield visual plan, not this faction color split.

Use the shared palette from `GAMEPLAY/SIMULATION/FACTIONS/DefaultFactionCatalog.tres` through `Factions.Color` / `ArmyRenderer.FactionColor`. Current factions include Blue, Red, Green and Yellow. Never hardcode a color per mesh or assume only two factions; authored factions must work with the same geometry/materials.

### Buildings and defenses

- **Neutral chassis:** all factions share dark charcoal / gunmetal steel. Use several related shades for plinths, recessed seams, inner mechanisms and brighter bevels. Do not tint the entire tower by faction.
- **Faction armor:** select meaningful armor plates, corner caps, shoulder pods or weapon collars. Keep enough color visible from above and both sides to recognize ownership at normal gameplay zoom. As a starting visual heuristic, use roughly 65–75% neutral structure, 20–30% faction panels, and a small remainder of illuminated trim (not a measured or mandatory surface-area ratio).
- **Faction emission:** short power strips, status panels and a focal core, matching the owning faction. Use bright cores against dark recessed channels; avoid outlining every edge or making the whole model glow.
- **Shape language:** layered clipped rectangles, octagonal collars, chamfered slopes, overlapping armor and clean dark gaps. Use larger details that survive zoom; a few large vents are better than tiny bolts, noise or dense texture patterns.
- **Distinct roles:** Broadside is broad and heavy with two independently recoiling launch tubes. Railgun is a sleek directed-energy weapon, with a tapered pedestal, narrow cradle, rear capacitor bank, a single long coil-wrapped accelerator and recessed circular focusing aperture. Avoid bulky artillery-style armor pods or twin-tube silhouettes. Shield support can instead use a recognizable generator/ring silhouette. Do not copy one weapon silhouette onto every tower.
- **Lighting:** use shared battlefield directional lighting and restrained satin highlights. Baked bevel masks complement real light; do not add new global exposure, sun, ambient or glow settings just for a model.

## 2. Building implementation references

| File | Responsibility |
| --- | --- |
| `WORLD/BUILDINGS/RENDERING/BroadsideTowerMeshes.cs` | Accepted heavyweight geometry: normalized base, rotating head and two identical independent barrels |
| `WORLD/BUILDINGS/RENDERING/RailgunTowerMeshes.cs` | Tapered pedestal, compact rotating capacitor carriage and one recoiling/pitching coil accelerator |
| `WORLD/BUILDINGS/RENDERING/TowerArmorGeometry.cs` | Shared `Steel`, `Paint`, `Trim`, `Block`, `Plate` helpers |
| `WORLD/BUILDINGS/RENDERING/ShieldGeneratorMeshes.cs` | Static radial reactor crown, armored base and emitter pylons |
| `WORLD/BUILDINGS/RENDERING/PrefabShelterMeshes.cs` | Three compact habitation variants with square foundations, airlocks, windows and faction trim |
| `WORLD/BUILDINGS/RENDERING/BroadsideArmor.gdshader` | Shared two-tone mask shader (historical filename, used by all three towers) |
| `WORLD/BUILDINGS/RENDERING/ShieldFieldDomeRenderer.cs` / `ShieldFieldDome.gdshader` | Optional faint stationary coverage indicator; separate compact batch |
| `WORLD/BUILDINGS/RENDERING/TowerLighting.gdshaderinc` | Shared directional light and satin highlight |
| `WORLD/BUILDINGS/RENDERING/DefenseRenderer.cs` | Cached meshes, MultiMesh allocation, faction data, placement, aiming, hide/reset, beam/flash batches |
| `WORLD/BUILDINGS/RENDERING/TowerArmor.gdshader` | Legacy material for other tower visuals; do not silently switch unrelated structures |

The Shield Generator now shares the armor shader while retaining its static support silhouette. Command buildings still have their own render paths. Apply the language when their redesign is requested, adapting it to their function rather than duplicating the weapon renderer.

## 3. Building mesh and material conventions

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
- In `DefenseRenderer.Batch`, enable `UseCustomData` before setting instance count. Keep instance color white and supply the actual faction RGB via `SetInstanceCustomData` at placement/reuse, using `SetArmorTint`. Never multiply the mask by team color. **Custom alpha is reserved for progressive defense cracks**, initialized to zero and updated by `SetDamageCracks` at stage changes (stage / 3). Preserve the RGB while updating damage, and reset the alpha on slot reuse. The base/static generator receives the crack stage; do not assume rotating parts use it. The shared shader reuses `VISUALS/FEEDBACK/UNIT_HITS/SurfaceCracks.gdshaderinc`.
- Use the current material rather than creating a shader per faction or weapon. Preserve the current shadow policy. Emission is a material effect, not an OmniLight3D; visible bloom depends on the environment. Do not add point lights, particles or animated lights merely to decorate a tower.
- Preserve valid clockwise Godot winding, normals and face shading. Avoid coincident faces and intersecting decorative panels that hide the intended accent. Keep authored dimensions readable; use deliberate broad recess-like dark panels, not excessive boolean mesh detail.

## 4. Building coordinate and animation contracts

- XZ is ground, Y is up, and local forward is -Z. Stationary base is normalized around zero (approximately -0.5 to +0.5 in X/Y/Z), scaled to actual footprint and base height by the renderer. Grounding uses terrain height.
- Head geometry is authored in world units relative to `DefenseRenderer.Pivot`; it rotates around Y. The weapon barrel uses its existing local transform and recoil axis. Bake attached rails, caps and trim into the moving part, not into the base.
- Railgun barrel is centered at zero, length 3.8; muzzle at local z = -1.9. The renderer offsets its center forward 1.9 from pivot and adds recoil along the pitched axis. Beam and flash must still exit the actual central opening. The induction coils and recessed focusing aperture are cosmetic and baked into the pitching/recoiling barrel: one weapon, one beam.
- Broadside barrels are mounted at x = ±0.7, y = 0.9 from the pivot, tilted up 15°, with local muzzle z = -4.7. Each barrel recoils independently. Do not extend muzzle geometry past these endpoints without an explicitly approved coordinated transform change.
- Keep the authored footprint, collision, HP, range, timing, damage, projectile behavior and stable resource ID unchanged during visual-only tasks. Real collision and targeting come from the shared simulation, not decorative triangles.

## 5. Building performance and acceptance

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

## 6. Mobile unit visual authoring

### Art and silhouette

- Lead with faction-colored hulls, large plates or the original geometric body. Use dark steel as a secondary material for gun tubes, exposed mechanisms, seams, undercarriages and selected armor frames.
- Keep infantry shapes recognizable at normal gameplay zoom: circle, square, triangle, diamond, etc. Do not add machinery to every small soldier or turn every silhouette into a tank. Detail should increase selectively with size/role, not indiscriminately.
- Light Artillery is the first two-tone unit experiment and the current reference for compact mobile hardware: faction-painted chevron hull, colored shoulder armor and barrel receiver, dark barrel and mount, restrained emissive strips. The first charcoal-heavy version was intentionally revised to be more faction-colored. Retain its lightweight appearance and raised single gun; do not give it the Broadside's heavy stationary silhouette.
- Twin Rocket Launcher retains its wide angular fork, deep open center and two fixed prong-mounted launchers. Its faction-colored upper hull with tapered tips, swept shoulders and a deep visual notch, dark underframe, compact rear deck and tapered recessed muzzle collars distinguish it from Light Artillery. The enhanced render-only outline must not alter the compiled collision outline. Preserve authored `MountedRocket.LocalPose` and `MountedRocket.Muzzle` for both shots; no crossbar may close the notch.
- Use broad facets, simple layered plates and readable dark gaps. Prefer a small number of large details over lots of bolts, vents or tiny lights. Limit emissive accents to functional-looking details; no persistent decorative particles, real lights or strong bloom per soldier.

### Current unit implementation and references

| File (repository-relative) | Responsibility |
| --- | --- |
| `VISUALS/RENDERING/FacetedSoldierMeshes.cs` | Shared mesh per unit type; `AppendLightArtilleryArmor`, `AppendTwinRocketArmor`, `UsesTwoToneArmor`, `AppendPrism` and existing geometric unit builders |
| `VISUALS/RENDERING/UnitArmor.gdshaderinc` | Unit UV armor mask to faction/steel/trim colors |
| `VISUALS/RENDERING/ArmyRenderer.cs` | Existing faction/type MultiMesh batches, packed pose and color, sparse/dense updates, flashes and fracture state |
| `VISUALS/RENDERING/BattlefieldLighting.cs` / `BattlefieldUnitLit.gdshader` | Per-type shared lit material selection and directional shading |
| `VISUALS/FEEDBACK/UNIT_HITS/UnitSurface.gdshaderinc` / `UnitCracks.gdshader` | Shared lit/unlit armor and fracture rendering |
| `LEVELS/AutoBattler/ReserveEntryClip.gdshader` | Reinforcement entry clipping with shared unit armor shading |
| `GAMEPLAY/UNITS/UnitVisualConfig.cs` / `UnitVisualConfig.tres` | Optional enhanced meshes, independent Hexagon enhancement, fracture, shield shells and other visual settings |
| `GAMEPLAY/UNITS/CATALOG/UnitCatalog.cs` | Compiled `UnitOutline`, size, footprint, barrel mount/tip; visual geometry follows the authored contracts |

Enhanced Light Artillery and Twin Rocket Launcher opt into the two-tone unit armor mask. Existing simpler unit meshes continue using baked grayscale vertex colors multiplied by instance faction tint. Do not silently migrate the roster, or use a building shader on a soldier. Use stable unit IDs to scope new experiments; preserve the existing plain fallback and independent visual toggles.

### Material contracts — different from buildings

| Channel | Enhanced units | Two-tone towers |
| --- | --- | --- |
| Vertex RGB | White for masked faces; baked grayscale for legacy geometry | Baked steel/paint/trim weights |
| UV.x | 0 legacy; 1 steel; 2 paint; 3 trim | Not the unit armor mask |
| UV.y | Baked face brightness | Not the unit armor mask |
| Instance RGB | Faction tint or temporary white hit flash | White instance color; faction RGB in custom data |
| Instance alpha / custom alpha | Existing color alpha encodes fracture stage and orientation, not transparency | Custom alpha encodes base damage stage, not transparency |

For enhanced units, use the existing `AppendPrism(..., armorMask: true)` convention: the mesh builder supplies RGB material weights, then the triangle writer converts them to UV kind/brightness and leaves vertex color white. Legacy faces on a mixed mesh use UV zero and their ordinary vertex colors. Do not directly write the tower's RGB mask to the soldier surface. Keep the existing 16-float transform/color instance stride for ordinary units; do not add custom instance fields, materials per soldier, or extra batches merely to color panels. The optional Circle, Square, Triangle, Diamond, Pentagon, Hexagon, H Breacher, Long Marksman, Pikeman, Obliterator and Prism robot animation experiments are scoped exceptions: opted-in walking infantry batches, Hexagon attack batches and Prism weapon batches use 20 floats (additional custom RGBA for gait phase/weight; type-local Z is Hexagon stomp or Prism charge fraction); shield shell reads use the actual batch stride. Negative UV kinds tag the animated weapon parts (arm, blade or recoiling barrel), and +4 tags boots; the shared armor shader strips these tags before choosing colors. Attack phase occupies the high nibble of instance alpha only for opted-in attack materials; fracture occupies the low nibble. Gait uses actual rendered movement with a stable ID phase, not wall-clock time. Square reuses this pipeline with a broad square hull, cosmetic shield, heavy hammer and lower bounce. Keep its old square silhouette selectable and avoid gameplay changes. Hexagon uses the same animation system: a six-sided armored siege hull and central rotary blade, a low-bounce heavy gait, and distinct cleave versus stomp poses. For units with multiple actual attack types, pass the existing resolved combat event as visual metadata; never infer the attack type by counting independently or move damage to match an animation. Preserve existing capped melee crescents and shockwave rings, with no extra emitters. Keep both prior Hexagon mesh options selectable. Triangle Archer reuses the same attack phase for linear barrel recoil (rather than a melee rotation), driven by an accepted projectile launch. Preserve its center-height projectile axis, actual launch position, whole-body recoil and forest/spread mechanics. Keep ranged weapons compact and original role silhouettes readable; the Long Triangle Marksman is a separate unit with a longer rifle and its own recoil/gait tuning. Retain the original projectile origin and center-height axis. Pikeman uses an open T-shaped silhouette with a centerline spear and a short linear thrust on existing melee feedback; bracing is cosmetic only, never a new damage or charge-defense rule. Pentagon Elite uses a distinct five-sided armored silhouette and shoulder-pivot energy axe, with a tighter cleave motion than the siege Hexagon and intermediate gait weight. Match the existing contact-effect window approximately, without changing damage timing or adding a second emitter. Diamond Cavalry is a low mechanical quadruped with a central lance: preserve its +X forward axis for geometry, four staggered leg offsets and linear thrust. Use actual movement distance for gallop and existing melee/trample feedback for lance contact; never modify charge momentum, separation or damage to support animation. H Breacher retains its open H-frame with two hydraulic ram assemblies on local -Z, using tagged linear extension from existing unit and structure attack feedback. Both rams are one cosmetic motion, not separate damage events; retain body lunge and structure targeting. Slow diagonal pad motion uses the shared gait with minimal hull bounce. Obliterator retains the fast single-target role, original body lunge and first-hit bonus; its narrow upright biped profile, slim spine pack and pointed centerline dagger must read differently from the low Diamond quadruped. Its blade uses actual hit feedback for a short linear thrust. Siege Ram's chassis and tracks retain their rearward extension. The owner subsequently approved actual size growth: SizeMultiplier 1.5 and a 17/15 longitudinal proportion factor in UnitOutline give +50% width/height and +70% length versus the preceding model, including the larger compiled collision clearance. Keep its proportional hydraulic contact geometry and attack timing; check room in existing formations and navigation corridors. Do not apply this authorized size change to other units. Prism has a fixed lens aligned to the existing nominal beam axis, with tagged accelerator rails converging from authoritative charge progress and recoiling from the actual discharge event. Preserve the existing beam/charge effects and limits, including the original world-space origin and whole-body recoil. Its four pads use the shared heavy gait. Do not confuse Obliterator with the separate Siege Ram.

Set `use_two_tone_armor` consistently for both lit and unlit material creation. The placement preview uses the same enhanced builder and a preview material with `armor_tint`; normal battle instances use the actual faction color. Preserve white hit flashes, the FX2 fracture stage/orientation encoded in COLOR.a, and the shared crack mask. The current armor shader suppresses emissive trim during white hit flashes. Verify the reinforcement-entry clip shader still applies the same armor colors while retaining its map-edge discard. Do not interpret fracture alpha as transparent soldier opacity.

### Geometry and motion

- Build meshes once per type, with one surface for the main body and the existing independently animated mounts only where needed. Keep normal and enhanced variants aligned to the same authored size, collision footprint, height, forward axis and ground contact.
- For ordinary units, keep the hull centered vertically at zero (bottom at -height/2); ArmyRenderer applies the ground-anchored half-height offset. Preserve existing shape-specific forward axes rather than assuming all soldiers face -Z (Diamond is an existing exception).
- Light Artillery uses `UnitOutline.ArtilleryBarrelMount(height)` and `ArtilleryBarrelTip(height)`; preserve the authored elevation (currently 30 degrees), muzzle, length, and whole-body visual recoil. Do not move the muzzle for decorative purposes or modify combat/navigation dimensions.
- Keep independent weapon mounts, shield shell fitting, unit death fragments, hit pulses, projectiles and preview/runtime behavior compatible. Shield shell fitting uses the visible mesh vertices; excessive decoration can enlarge the shell even without gameplay changes.
- No stats, size tiers, attacks, range, movement, collision, targeting or saved identifiers should change in a visual-only pass.

### Unit performance and validation

For armies of thousands, prioritize triangle count and pixel cost much more aggressively than for a few dozen towers. Cache geometry and shared materials; do not add per-soldier nodes, lights, timers, textures per faction, new upload fields, or per-frame mesh generation. Use the existing enhanced-mesh switch for controlled before/after tests. More triangles and emissive shader work can still cost GPU time even if draw-call counts stay the same.

Review source and (if practical) one build under root CodePreferences. Owner checks should include close and battle zoom in multiple factions, current/lit/real-shadow modes, Benchmark and campaign previews, hit flash/crack stages, firing/recoil, shield shells and death feedback, and reinforcement clipping. Compare identical battles at representative unit counts before broad adoption; report measured FPS only if actually measured.

## 7. Future authoring workflow

Read this guide and the relevant feature docs, inspect the live renderer and a reference asset, then scope the smallest requested change. Choose the color hierarchy by asset category, preserve the original role silhouette and gameplay contracts, and reuse the appropriate existing pipeline. For new mechanics, use the existing tower/unit implementation standards separately. Update relevant feature README and append one devlog entry for implemented changes. Report the exact commit, any assumptions and pending owner checks.

### Habitation structures

Prefab Shelters should read as basic sci-fi living quarters, not turrets or flat bunkers: square full-footprint foundations, tall clean walls, doors, window panels, small vents, distinct roof shapes and restrained faction accents. Visual variants share the same gameplay definition, footprint and lifecycle. Use a stable placement-cell hash rather than a frame-dependent random choice; cache one mesh and one lazy batch per design. Preserve custom-data crack state and avoid per-building nodes. Any footprint or collision change requires an explicit decision and existing-layout compatibility review.

### Aircraft (AIR-VIS1)

Aircraft use their own existing AirSquadronVisuals renderer and shared AircraftPaint previews, not ground gait or fracture shaders. Build one cached closed faceted surface per default airframe with the same unit armor UV kinds (1 steel, 2 faction paint, 3 emissive trim) and baked shading; use the shared UnitArmor include with the aircraft's instance faction color. V-wing should remain a slim swept V with a distinct cockpit, paired guns and exhausts; bomber should remain a much broader flying wing with thicker armor, large engines and a visible underside payload bay. Airframe forward is **+Z**, unlike most infantry. Preserve wingtip trail anchors, interceptor tracer origins, authored footprint/flight stats, bomb release behavior and bank/pitch transforms. Use independent legacy mesh toggles; explicit custom AircraftMesh resources take precedence. Share material selection between editor and runtime, and avoid additional per-aircraft nodes, emitters or upload channels. Decorative bays/engines must not alter combat.

Heavy Bomber exhaust (AIR-VIS2) uses a shared cached flame mesh and capped billboard smoke pool. Keep engine anchors aligned to the +Z-forward airframe's rear nacelles; leave world-space puffs behind during banks, and do not assign guessed anchors to custom airframes. Default BomberPayload visual substitution is controlled by a dedicated toggle; preserve its original resource, flight, splash and legacy mesh. Payloads point local -Z along the existing projectile tangent. Existing aircraft shadows, wingtip wisps and tracers remain independent.

### Twin Minigun Tower

Use a square full-footprint foundation, squat armored rotating carriage, side ammunition housings and two short rotary-style clusters rather than long artillery tubes. Keep both muzzle positions aligned to `DefenseRenderer.MinigunMuzzle` (local X ±0.48, Z −1.65); both barrels share the same pitch transform and one mesh. Shared gunmetal/faction mask and pooled flash effects, no decorative spinning nodes or lights.
