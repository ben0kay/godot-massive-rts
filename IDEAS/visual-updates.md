# Visual updates

Models, animation, lighting, terrain art, and battle effects.


## Natural terrain and scenery
- **Waterfalls:** Place a flowing cascade between elevations, with pooled mist and foam at the base. Decorative initially; no water physics.
- **Dead forests:** Bare and broken pine trunks, fallen branches, desaturated variants for volcanic or ruined maps. Reuse forest batching; decide separately whether they share forest combat modifiers.
- **Rock spires:** Place individual angular natural pillars with varied shapes. If intended as solid cover, reuse authored obstacle footprints and LOS; otherwise decorative.
- **Natural shorelines:** Optional alternative to industrial water banks: sloping rock/sand banks, pebbles, reeds and weathered edges. Traversable fords are a separate [mechanic](gameplay-mechanics.md#shallow-water-and-fords).
- **Cliff styles:** Optional sedimentary, basalt, granite and sandstone appearance presets sharing the existing procedural cliff generator. CLIFF2's organic relief and layered material are already source implemented; retain authored rim and plateau height. Optional sparse scree at the base, batched and non-blocking.
- **Forest refinement:** Organic varied pine silhouettes are source implemented. Future options include dead/frosted variants and restrained ambient wind; preserve existing sway, transparency and performance.

## Terrain materials and atmosphere
- **Surface palette:** Volcanic basalt, cracked dry earth, moss, worn concrete, snow and scorched earth. Paint cosmetic material masks with blended edges; keep independent of gameplay terrain types. Tool design in [level editor](level-editor.md#terrain-surface-painter).
- **Per-level atmosphere:** Sunset, night, fog, rain, volcanic ash/embers, sandstorms and snowstorms. Start as visuals only, with adjustable sun direction, ambient tint, fog density and weather intensity. Preserve faction readability and existing lighting.
- **Undergrowth:** Manual grassland, rocky and marsh presets are source implemented. Future automatic decoration belongs in [level editor](level-editor.md#automatic-undergrowth).

## Battle feedback and environmental detail
- **Wind and leaves:** Subtle shader-based grass/tree movement and occasional pooled ambient leaves; existing heavy-unit sway/leaf effects are not to be replaced.
- **Ground impacts:** Bounded artillery craters, scorch decals and persistent decorative debris after major explosions; no terrain deformation or new collision initially.
- **Movement dust:** Sparse batched dust from large army concentrations, with visibility and effect caps; no emitter per unit.
- **Shallow-water splashes:** Small pooled splash effects for units crossing future fords.
- **Cliff dust:** Occasional tiny rock fragments, visual only and capped.
- **Damaged structures:** Escalating smoke at low HP, using existing damage/FX state and budgets.
- **Aircraft shadows:** Review current shadow support and improve readability if needed; use cheap batched projections, not individual real-time shadow lights.

Keep effects subdued, distance-aware and pooled/batched. No per-unit processing nodes or expensive transparent layers; use static generation or shader effects wherever practical. All proposed visuals require owner approval and actual Godot review.
