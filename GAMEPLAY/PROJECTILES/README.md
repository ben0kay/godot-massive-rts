# PROJECTILES

`Shared/` owns the projectile resource, catalogue, pooled flight and collision logic, and batched rendering. `Types/BasicArrow/` retains the reusable arrow definition and its shaft, arrowhead and fletching mesh. `Types/LaserBolt/` provides the Triangle Archer's thin, short, unshaded/emissive laser-bolt box mesh (now 0.045 × 0.045 × 0.44 units, about 33% thinner and 14% shorter than the previous bolt); its light base colour is subtly mixed with the firing faction's blue/red/green tint. A ranged unit selects its projectile in its unit definition. Several units can share the same asset. Attack damage still belongs to the firing unit.

Open `Types/LaserBolt/LaserBolt.tres` to tune the Triangle Archer's bright rectangular bolt (the shared shader uses a 40% faction tint while retaining a pale energy core) or `Types/BasicArrow/BasicArrow.tres` for the retained arrow type; each resource has its own speed and arc settings. At the default 0.8× movement multiplier, both types fly at 36 world units per second from an authored speed of 45. The lifetime cap scales inversely to preserve the distance a shot can travel before expiry. **Arc Height** caps the rise above the straight line between launch and aim. **Arc Height Per Distance** scales the rise down on short shots. Both types retain the existing 0.65 world-unit maximum and 0.08 units of rise per horizontal world unit, so switching Triangle to the bolt does not alter its flight or collision behavior. Set Arc Height to zero for straight flight. Restart the arena after editing the resource.

To add another projectile, create a ProjectileDefinition resource in its own directory under `Types/` and assign it to a ranged unit. Set Visual Mesh to use a custom model aligned along local −Z; where precise point alignment matters, put its tip at the origin. The Laser Bolt uses a centered 0.045 × 0.045 × 0.44 box and does not change collision geometry. Definitions with no mesh use the shared arrow mesh. `TintByFaction` is an optional, visual-only projectile setting (disabled on existing non-laser definitions). When enabled, the shared projectile batch uses `MultiMesh.UseColors` and receives each shot's existing source faction; a compatible shader such as Laser Bolt's can mix that faction tint into its appearance. The renderer still packs live instances by projectile definition. A shot follows its authored arc toward the aim position recorded at launch and tests for an enemy hit along each simulation segment.


## ART1 — Artillery Shell

The reusable `Types/ArtilleryShell/ArtilleryShell.tres` enables **Indirect Fire** and **Explodes on Impact**. The aim point is the target's ground position at launch. A shell can clear a wall if its swept segment is above the wall. It detonates on contact with an enemy, a blocking wall, or the terrain surface. Terrain tests follow the same heightfield triangles as the renderer. If an airborne shell expires before reaching its aim point, it is removed.

The explosion uses the same XZ enemy-only damage query as the Hexagon shockwave. The Artillery Shell's radius is now **3.5 units** (was 7); its edge fraction remains **31.25%** of the firing unit's damage. With Light Artillery's 80 damage, the linear falloff gives 80 at the centre, 52.5 at half-radius and 25 at the edge before armour. The blast can reach enemies across nearby cover. The brief ring reuses the shockwave batch and is drawn at ground height at the impact center.

## PROJ-GLOW1 — Planned low-flying projectile ground glow (not implemented)

[PROJ-GLOW1 implementation plan](PROJ-GLOW1_IMPLEMENTATION_PLAN.md) documents an optional additive radial quad below future **low-flying travelling laser/plasma projectiles**. It would use per-projectile `.tres` settings and shared MultiMesh batches, with terrain-relative height fading. **The existing high-arc artillery shell and ordinary arrows must remain unlit by this effect.** This is a future visual-only plan; no shader, settings or rendering code has been added yet.

## PROJ-LASER3 — Two laser-bolt sizes

Basic Triangle Archer originally received a 25% smaller bolt on every axis; its current bolt is 0.045 × 0.045 × 0.44 units after the subsequent visual refinement. The new Long Triangle Marksman uses `Types/LongLaserBolt/LongLaserBolt.tres` with the same existing 40%-faction-tint shader appearance, but a 0.07 × 0.07 × 0.64-unit bolt (about 45% longer than Triangle's current bolt). The basic Triangle retains its authored 45 projectile speed; the Long Triangle Marksman's dedicated bolt now has 67.5 authored speed (50% faster), while both retain the 0.65 arc-height cap, 0.08 arc-per-distance and standard hit/collision behavior. There are no new per-projectile nodes, simulated effects or lights.

## Rocket and artillery launch effects

Enable **Use Projectile Launch Effects** in `GAMEPLAY/UNITS/UnitVisualConfig.tres` before launch. Opted-in projectile resources draw a fading ribbon along the current flight arc and a flash at the recorded muzzle. The shared renderer uses two MultiMesh batches with no particles or lights. Only Twin Rocket and Artillery Shell opt in by default. These effects are visual only.

Artillery and rockets also leave a brief cloud at the muzzle after a successful launch. It billows, rises, drifts, and fades over about 2.4 seconds, independently of projectile impact. Disable **Use Muzzle Smoke** in `GAMEPLAY/UNITS/UnitVisualConfig.tres` before launch to hide it. Wind defaults to east (+X). To change it for an authored map, edit **Smoke Wind** in its level `.tres`. The components are world X and Z drift in units per second. Use `(0, 0)` for still air.

Rocket and artillery trails, explosive sparks and impact rings take a light tint from the firing faction’s colour in the shared faction catalogue. Explosion cores stay bright. Both scale with the resolved splash radius, including weapon-mount overrides. The shared visual pool remains capped at 256 effects.

Triangle Archer laser bolts opt into a faction-colored ground glow. The shared renderer batches up to 256 in-flight streaks and fades them with height above terrain.

## AIR-VIS2 — Heavy Bomber payload

The default `GAMEPLAY/COMBAT/AIR/BomberPayload.tres` retains all flight and splash settings and its original orange box fallback. **Use Enhanced Bomber Bomb Visuals** selects a cached dark-metal faceted render mesh with a tapered nose, contrasting collar and four swept fins, approximately 0.48 × 0.48 × 1.04 units including fins. Local -Z follows the existing flight tangent. Speed, collision, splash, damage, rocket and artillery models remain unchanged.

### Minigun rounds and defense firing feedback

The shared `MinigunRound.tres` now blends 28% of the source faction color into its pale warm tracer and opts into the same height-faded, capped 256-instance travelling ground glow as Triangle Archer bolts. This affects both Dreadnaught and Twin Minigun Tower shots. The shared glow shader retains its default 0.20 intensity; Broadside ground flashes reuse it with an independent material at 0.72 intensity. Neither effect changes projectile behavior.

Explosive projectile impacts and aircraft crashes leave temporary ground
scorch marks. Marks size to the blast, follow the local terrain slope, and
fade over the last four seconds of a nine-second lifetime. The shared renderer
retains up to 128 marks in one MultiMesh and clears them on battle reset.

### Direct impact sparks and shield ricochets

Non-explosive swept unit contacts that actually reduce health emit a small metallic spark burst at the existing hit fraction. Shield-only contacts do not emit body sparks. `ProjectileDefinition.ShieldRicochetEnabled` is a visual-only opt-in, enabled on `LaserBolt.tres` (Triangle Archer), `LongLaserBolt.tres` (Marksman) and `MinigunRound.tres` (Dreadnaught and Twin Minigun Tower). The compiled catalogue caches the opted-in mesh dimensions. A fully absorbed hit, including an exact shield break, spawns a cosmetic reflected tracer if enabled; shield overflow produces the normal shield flash and body sparks instead. The original projectile is removed normally. No damage, targeting, RNG, flight or collision rules change.

### Cosmetic shell casings

`UnitDefinition` and `BuildingDefinition` expose `EjectShellCasings` and `ShellCasingScale` (default off, size 0.4×). Broadside Artillery initially opted in at 1×; Tank now opts in at 0.8×, with a dedicated hull-facing receiver-side port. Successful ordinary ground projectile shots and Dreadnaught minigun shots have reusable source-side ejection hooks; the latter uses its own turret mount. Ground projectile towers use receiver-side anchors and alternate sides with the fired barrel. Aircraft and guided AA are not connected to casing ejection. `ProjectileRenderer` owns a lazy shared `ShellCasingEffects` pool: 256 brass cases, cosmetic ballistic tumble, terrain bounce and settle, 4.5-second lifetime with final 1-second fade. No rigid bodies, gameplay collisions, additional targeting, or simulation RNG. Terrain is sampled only while airborne. Cases can visually cross structures; no permanent clutter.

## Light Barricade contact

The shared projectile sweep tests hostile barricades at their physical height before resolving the next impact. A projectile that strikes a segment is consumed and damages its health pool. Friendly shots and arcs that clear the top pass through. Target acquisition can still see targets on the other side.

An independent primary weapon launches through the ordinary unit projectile path
with a rotated muzzle and casing port. Structure shots retain their structure
target reference. The Tank uses its existing shell, glow and track smoke.
