# UNITS — Soldier Catalogue

For unit appearance and shared faction coloring, read [Faction Visual Authoring Guide](../../VISUALS/authoring.md) before modifying meshes or materials.

**Status:** UNIT0–UNIT3 source implemented. Awaiting owner validation.

This **UPPERCASE major folder** owns the roster, unit definitions and catalogue lookup. `GAMEPLAY/SIMULATION/` owns compact runtime soldier arrays; `GAMEPLAY/COMBAT/` owns generic attacks and damage; `VISUALS/RENDERING/` owns faceted meshes and shared MultiMesh batches; `GAMEPLAY/DEPLOYMENT/` owns painting and saved placements; `BENCHMARK/` owns test scenarios and controls.

- **[UNIT0 implementation plan](UNIT0_IMPLEMENTATION_PLAN.md)** — direct Codex instructions, architecture, migration, checks and performance requirements.
- **[UNIT1 size scaling plan](UNIT1_SIZE_SCALING_PLAN.md)** — per-type size tuning and owner checks.
- **[SKIRM1 Skirmisher implementation plan](SKIRM1_IMPLEMENTATION_PLAN.md)** — **planned only**: closest-enemy ranged targeting, bounded all-direction retreat AI, and shared benchmark/Command Sandbox integration.
- **[ROCKET1 Wide Deep-Fork Twin-Rocket Launcher plan](ROCKET1_IMPLEMENTATION_PLAN.md)** — **source implemented**: wide fork silhouette, two prong-mounted launchers firing alternating arcing rockets, small splash via existing artillery code.
- **[DREAD1 Dreadnaught implementation plan](DREAD1_IMPLEMENTATION_PLAN.md)** — **planned only**: Tier 5 super-heavy hull with 4 independent miniguns, nearest-target pursuit and front-only crush damage; includes large-footprint separation safeguards.
- **[DREAD1 Dreadnaught implementation plan](DREAD1_IMPLEMENTATION_PLAN.md)** — **planned only**: Tier 5 pointed super-heavy hull, four independent nearest-target miniguns with individual min/max ranges, and front-only crush damage.
- **[Visual ideas backlog](../../VISUALS/FEEDBACK/UNIT_VISUAL_IDEAS_REVIEW.md)** — Codex review only: optional future unit and battlefield feedback effects; nothing implemented from this plan.
- **[Starting roster and stats](DEFINITIONS/STARTING_ROSTER.md)** — the initial circle, square, triangle, diamond and pentagon with roles and tuning values.

## Folder structure

```text
UNITS/
  README.md
  UNIT0_IMPLEMENTATION_PLAN.md
  DEFINITIONS/
    STARTING_ROSTER.md
    UnitDefinition.cs
    CircleInfantry.tres
    SquareInfantry.tres
    Pikeman.tres
    TriangleArcher.tres
    LongTriangleMarksman.tres
    DiamondCavalry.tres
    PentagonElite.tres
    HexagonSiege.tres
    ChevronArtillery.tres
    HBreacher.tres
  CATALOG/
    UnitCatalog.cs
    DefaultUnitCatalog.tres
```

The arena loads the catalogue once and builds a shared stat lookup and mesh set. Runtime byte indices are explicitly Circle 0, Square 1, Triangle 2, Diamond 3, Pentagon 4, Hexagon 5, Chevron 6, H Breacher 7 Long Triangle Marksman 8, Worker 9, and Pikeman 10. Deployment saves use version 1 and stable textual IDs. Legacy arrays map kind 0 to Circle and kind 1 to Triangle.

Open a definition resource to tune its silhouette, dimensions, speed, health, armour, reach, damage or attack interval. The arena reads changes on restart. Both factions use the same definitions. Normal melee attacks require body contact based on both footprints. Authored melee reach still feeds acquisition and trample launch distance. Armour multiplies damage by the fraction left after reduction for both melee and projectiles.

**Size Multiplier** in each definition scales width, length, height, footprint radius and melee reach. At 1.25, each linear dimension is 25% larger. Diamond Cavalry defaults to 1.35, Pentagon Elite to 0.9, Hexagon Siege Infantry to 2.0, and all other types to 1.0. Positive values are clamped to 0.5–3.0. Zero, negative and nonfinite values fall back to 1.0. Restart the arena after changing a definition so the catalogue and meshes rebuild together.

The catalogue fits each circular collision footprint around the scaled mesh outline. Polygon faces may sit inside that circle. The legacy Diamond `FootprintRadius` property only controls the length of its silhouette. The painter checks the resulting circular clearance against neighbours and battlefield edges. SEP1/SEP2 use the same compiled footprints. Saved layouts still use stable type IDs and the existing format. Loading a layout after increasing a type’s size can reject placements that no longer fit.

## Enhanced unit meshes

Open [UnitVisualConfig.tres](UnitVisualConfig.tres) in the Godot Inspector, set **Use Enhanced Meshes**, and save before running a scene. The switch ships enabled for the Circle Infantry trial. Circle keeps its footprint and height and gets a bright faction-coloured rim around a shallow recessed cap. The shared renderer uses the setting for battles and placement previews. Turn it off and relaunch to compare against the original meshes.

## Circle Infantry robot mesh trial

Enable **Use Circle Robot Mesh** in [UnitVisualConfig.tres](UnitVisualConfig.tres) (on by default), save and relaunch. It replaces Circle Infantry's visual with a compact static low-poly robot: round faction-painted helmet and torso, emissive visor, shoulder plates, boots and short energy blade. All geometry is baked once into the existing shared unit mesh and uses the two-tone unit armor shader. The original radius, height, grounded origin, combat stats and rotationally symmetric simulation are unchanged. The renderer derives a cosmetic heading from existing motion or an acquired target; no skeleton. With **Animate Circle Sword** enabled, the right forearm and blade swing around the shoulder in a 0.32-second visual slash (half the original speed), synchronized to actual attack feedback. The existing 0.16-second attack pulse is unchanged. Disable this separate option for a stationary sword arm. Existing whole-body pulses, flash and fracture paths remain in use.

Disable **Use Circle Robot Mesh** to restore the previous Circle appearance: recessed-rim Circle when **Use Enhanced Meshes** is enabled, plain Circle otherwise. The new switch is independent of artillery, rocket and Hexagon visuals. Sword animation reuses the existing attack timer and instance color alpha: low nibble fracture, high nibble swing phase for opted-in infantry attack materials only. **Animate Circle Walk** independently enables alternating forward/back boot steps, a small alternating foot lift and a subtle upper-body bounce. Gait advances with actual rendered XZ distance and has a stable per-ID phase offset; it eases to rest when movement stops. Tiny jitter and teleports do not drive steps. Only opted-in infantry walking batches (and Hexagon attack animation batches) add a four-float custom-data channel; other batches keep their original stride. No extra mesh batch, skeleton, new simulation work or per-soldier nodes. Owner visual/FPS comparison is pending.

## Square Infantry heavy robot trial

In [UnitVisualConfig.tres](UnitVisualConfig.tres), **Use Square Heavy Mesh**, **Animate Square Attack**, and **Animate Square Walk** ship enabled independently. Save and relaunch after changing them. Disabling the mesh restores the original beveled square regardless of the animation settings.

The model has a square faction-painted helmet and chest, broad armor shoulders, heavy boots, a left shield, and a right-hand short impact hammer. The shield and hammer are cosmetic: no shield points, blocking, knockback, damage, reach, size or collision changes. All parts share the existing masked mesh surface; no individual skeleton, nodes or extra draw calls. The resting geometry preserves the original square outline and height.

A 0.40-second hammer swing uses the same attack-feedback phase and shader as Circle, with a longer wind-up and quick inward impact. The standard 0.16-second whole-body pulse and actual attack cooldown are unchanged. Walking uses the shared distance-driven, ID-staggered gait with a longer stride cycle, smaller foot lift and a peak torso rise of 2.5% of height (Circle 4.5%). The old Circle appearance and animation tuning are unchanged. Lit/unlit battle materials share the settings; placement previews remain static. Owner visual, shader compilation and FPS validation pending.

## Triangle Archer robot trial

**Use Triangle Archer Mesh**, **Animate Triangle Fire**, and **Animate Triangle Walk** ship enabled in [UnitVisualConfig.tres](UnitVisualConfig.tres). Disabling the mesh restores the original beveled triangle; animation toggles apply only to the new model. The long Triangle Marksman has its own independent mesh and animation toggles (see below).

A light triangular faction shell, pointed helmet, compact power pack, thin mechanical boots and centerline laser carbine preserve the original triangular silhouette and collision size. The barrel and breech slide back on the existing successful-shot feedback event, then recover over 0.24 seconds. The existing 0.16-second attack pulse and whole-body recoil remain; the muzzle is visually aligned with the original center-height projectile axis, but actual projectiles still launch from their original position (inside the hull). No change to bolt, launch effects, aim spread, forest deflection, damage, reach, cooldown or stats.

The shared distance-driven gait has a shorter cycle, staggered per-unit phase, 9.5% peak foot lift and 3% peak torso bounce. Rest geometry is baked once, using one masked surface; the existing infantry shader, packed attack phase and optional walking custom data are reused. No extra draw calls, timers or per-unit nodes. Check in-game firing, visible bolt exit, gait and old mesh fallback, plus lit/unlit and FPS.

## Diamond Cavalry mechanical lancer trial

**Use Diamond Cavalry Mesh**, **Animate Diamond Attack**, and **Animate Diamond Gallop** ship enabled in [UnitVisualConfig.tres](UnitVisualConfig.tres). Disable the mesh to restore the original elongated diamond; the animation toggles apply only to the new model.

The low four-legged mechanical mount retains Diamond's long +X forward axis, with a faction-painted rhombus hull, pointed rider pod, four piston legs, rear propulsion pack and central energy lance. Tagged lance geometry thrusts forward briefly on the existing successful trample-contact or ordinary melee attack feedback event, then retracts (0.22s total). The standard 0.16s body pulse remains unchanged. No change to charge entry, acceleration, momentum, penetration, resistance, separation, damage, dust or contact effects.

The shared distance-driven, ID-staggered gait drives four offset legs along local +X with 14% peak foot lift and 4.5% peak body bounce. This is a visual gallop at all movement speeds, not an independent charge-state timer. It settles to rest without wall-clock animation. Reuses existing packed attack alpha, optional walking data and masked surface; no extra nodes, draw calls or effects. Owner validation: charge contacts, facing, gait at high and low speed, legacy fallback and FPS.

## Pentagon Elite robot trial

**Use Pentagon Elite Mesh**, **Animate Pentagon Attack**, and **Animate Pentagon Walk** ship enabled in [UnitVisualConfig.tres](UnitVisualConfig.tres). Disable the mesh to restore the original beveled pentagon; the animation toggles apply only to the new robot.

The tier-2 elite assault robot has a layered five-sided faction-painted cuirass, pointed helmet, glowing visor, angular shoulder armor, mechanical boots and a compact energy axe. The weapon arm, haft and head share one shoulder pivot. The attack-feedback phase drives an approximately 85-degree sweeping axe strike in the initial 0.17-second contact-effect window, followed by recovery (0.46 seconds total). The existing cleave crescent, 85-degree damage cone, damage timing, 0.16-second body pulse and all gameplay stats are unchanged. No new effect emitters.

The distance-driven, ID-staggered gait has a restrained 2.2% peak torso bounce and 6.5% peak foot lift, between Square and Hexagon. Reuses the single shared armor surface, existing infantry shader, attack alpha encoding and optional walking data; no new per-unit nodes, timers or draw calls. Owner validation: mesh silhouette, swing/cleave visual alignment, grounded stance, legacy fallback, cracks, lit/unlit, shield shells and FPS.

## Enhanced Hexagon Siege mesh

**Use Enhanced Hexagon Mesh** in [UnitVisualConfig.tres](UnitVisualConfig.tres) is a separate, default-on toggle from Circle's **Use Enhanced Meshes**. It adds six raised faction-coloured armour wedges, narrow dark radial seams and a recessed central hexagon to the Hexagon Siege Infantry. The details are built into one shared MultiMesh-compatible mesh, including deployment and Entity Editor previews. No combat size, circular clearance, balance or AI parameters change; FX2 cracks and white hit flashes still work. Turn the toggle off and relaunch the battle/editor preview for the original beveled hexagon. The additional triangles warrant a like-for-like high-count FPS comparison.

## Hexagon Siege robot trial

**Use Hexagon Siege Robot Mesh**, **Animate Hexagon Attack**, and **Animate Hexagon Walk** ship enabled in UnitVisualConfig.tres. Disable the new mesh to return to the prior enhanced six-plate Hexagon; turn off Use Enhanced Hexagon Mesh as well for the original prism. Animation toggles apply only to the new robot.

The tier-3 siege robot uses a broad hexagonal faction-painted torso, piston boots, low armored head, power pack, and large centrally mounted rotary energy blade. The existing one-surface armor mask and infantry animation shader are reused. Cleaves sweep through roughly 149 degrees during the initial 0.17s contact effect window, then recover (0.58s total). The actual 180-degree cleave damage cone and original capped contact crescent are unchanged. Each actual radial stomp attack sends the existing attack feedback a cosmetic stomp flag, so the blade stays at rest and the torso braces downward instead; the existing radial ring and damage remain unchanged. The stance has a slower distance-driven staggered gait, 5.5% peak boot lift and 1.8% peak torso bounce. No new per-soldier nodes, draw calls or timers; the existing optional instance data stores gait phase/weight and, on Hexagon only, stomp mode. The 0.16s attack pulse, stats, footprint and cooldown are unchanged. Owner checks: both attack types, legacy toggle, lit/unlit visuals, damage/cracks/shield shells and FPS.

## H Breacher demolition walker trial

**Use H Breacher Mesh**, **Animate H Breacher Attack**, and **Animate H Breacher Walk** ship enabled in [UnitVisualConfig.tres](UnitVisualConfig.tres). Disable the new mesh to restore the original H-frame. Save and relaunch to change settings.

The twin-ram siege walker uses two faction-painted armored side housings, a low crossbeam and central command housing, rear power cells, hydraulic casings and four ground pads. Both ends of the H remain open. Tagged shafts and ram faces extend together along local -Z on the existing attack feedback event (both unit melee and StructureCombat attacks), followed by slow retraction over 0.48 seconds. This is one existing damage event, not two attacks. The existing 0.16s attack pulse and authored whole-body lunge are retained. No change to targeting, structure preference, damage multipliers, reach, cooldown, navigation or collision.

Distance-driven staggered diagonal pad pairs provide a heavy gait with 4.5% peak foot lift and 1.2% peak hull bounce. Reuses the one-surface armor mask, shared infantry shader, packed attack phase and optional walking custom data. No extra per-unit nodes, timers, effects or draw calls. Owner validation: both target kinds (including shelters), ram alignment, H silhouette, grounded motion, lit/unlit, legacy fallback and FPS.

## Long Triangle Marksman precision robot trial

**Use Long Marksman Mesh**, **Animate Long Marksman Fire**, and **Animate Long Marksman Walk** ship enabled in [UnitVisualConfig.tres](UnitVisualConfig.tres). Disable the mesh to restore the original elongated triangle. Save and relaunch after changing settings.

Rear-weighted wedge armor, an optical sight, twin cooling strips, thin mechanical legs and an elongated laser rifle distinguish this unit from the basic carbine Archer. The accepted-shot feedback drives sliding barrel recoil over 0.34 seconds; the original whole-body recoil and 0.16-second pulse remain. The barrel follows the original center-height shot axis; the real bolt still starts at the original center of the unit, not its new visual muzzle. Existing LongLaserBolt resource, targeting, damage, reach, cadence and collision are untouched. Shared ID-staggered walking uses 7% foot lift and 2% peak body bounce.

## Pikeman anti-cavalry robot trial

**Use Pikeman Mesh**, **Animate Pikeman Attack**, and **Animate Pikeman Walk** ship enabled in UnitVisualConfig.tres. Disable the mesh to restore the original T-shaped pike.

Broad rear armor, reinforced shoulders and boots, a compact helmet and an energized central spear preserve the T-shaped silhouette. The tagged spear extends along local -Z on the existing single-target melee event and recovers over 0.30 seconds. A small 3.5% torso dip gives the impact a braced appearance; there is no independent brace mechanic. The standard 0.16-second attack pulse, anti-Diamond 1.5x damage multiplier, charge resistance, targeting, reach and all stats are unchanged. The shared distance-driven, ID-staggered gait uses 8% foot lift and 2.4% peak body bounce.

Both models use one baked masked mesh, the shared infantry shader, packed attack phase and existing optional walking data. No extra nodes, draw calls, timers or emitters. Owner checks: old meshes, firing/bolt exit, spear thrust, cavalry interaction, lit/unlit, shield/crack feedback and FPS.

## Unit fracture

In [UnitVisualConfig.tres](UnitVisualConfig.tres), **Use Unit Fracture** enables health-dependent cracks and crumbling deaths. Save and relaunch the scene after changing it. [Visual feedback](../../VISUALS/FEEDBACK/README.md) describes the crack thresholds and fragment budgets.

## Tuning units

Edit the existing definition in the Inspector or enable the [full-screen Entity Editor](../../addons/unit_editor/README.md). UNIT2 exposes the cleave target cap, ordinary melee facing angle and shockwave outer damage. The editor writes the selected unit resource and keeps its stable ID read-only.

Light Artillery remains **Size Tier 3** (formerly 2). Its authored 1.92-unit width and 0.99-unit height are compiled with a **1.25× Size Multiplier**, giving a 2.4-unit-wide, 1.2375-unit-tall body. It has **500 HP** (was 350) and authored **speed 1.2** (was 1.5). **Artillery Barrel Length Multiplier = 2.0** doubles the barrel length relative to its scaled base geometry; its shared mesh, projectile muzzle and circular separation footprint all follow the extended barrel tip (compiled clearance radius ~2.333 units). Recoil, 12-unit minimum range, 70-unit maximum range, 80 centre damage and fire interval are unchanged. The Artillery Shell blast radius is now **3.5 world units** (was 7), with unchanged linear splash falloff to **31.25%** at the edge. The barrel is elevated **30°** above horizontal, with its actual muzzle raised to the visible tip. Elevation is adjustable in the unit Inspector or Entity Editor; the full-length clearance remains unchanged. Tune barrel length separately. Existing benchmark preset compositions remain unchanged; save layouts may need more space for the larger footprint.

DMG1 adds `h_breacher`, a slow 110-HP melee unit with Medium armour class and a twin-ram lunge. It deals 20 per strike to structures and 10 to units with the shipped zero percentage armour. The target percentages are editable under Primary attack in Entity Editor. It prefers hostile structures within 12 units while retaining soldier combat. Paint it through the benchmark unit picker, or select it in a Fabricator. The stock benchmark preset compositions retain their existing rosters.

## UNIT4 — Long Triangle Marksman

The new `long_triangle_marksman` (`LongTriangleMarksman.tres`) uses the same 0.8-unit rear-edge width as the basic Triangle, but a new, *twice as long* isosceles triangular outline pointing forward. Its circular clearance footprint follows the longer nose, while the soldier rendering and dynamic picker/Entity Editor use the existing catalogue pipeline. Stats: **65 HP, speed 3.2, 13 damage, 1.7s interval, 48-unit reach**, versus basic Triangle's 25 HP, 4.5 speed, 9 damage, 1.25s interval and 32-unit reach. It shoots its own longer faction-tinted laser bolt and has a Fabricator recipe of 8 for 210 Carbon in 14 seconds. Existing benchmark preset troop compositions were deliberately left unchanged; paint it or produce it from the Fabricator.

**Turn rate** in Entity Editor → Movement controls how quickly a directional unit rotates on the ground and aims in combat. It applies in both the benchmark and Fortress Defense. Circles skip heading updates. Older resources with a zero turn rate use 300°/s for directional shapes.

Worker (`worker`, index 9) is a compact rectangular utility unit with a front drill produced by the Citadel. Its allowed targets are **None**, so it can move and take damage without initiating combat. Its first milestone covers production and movement.

## Pikeman

The Pikeman (`pikeman`, index 10) uses Strong Infantry's base stats and a T silhouette with the spear tip pointing forward. Its regular melee strike deals 1.5× damage against the `diamond_cavalry` unit ID. The target ID and multiplier are authored in the Pikeman resource and resolved when the catalogue loads. Charge resistance is 1.10 versus Strong Infantry's 0.45. In the benchmark, that stops a stock cavalry charge with starting momentum 1.0 and reduces the contact damage. Fortress Defense currently uses ordinary cavalry melee. Select Pikemen in the deployment painter or produce a batch of 10 for 150 Carbon in 10 seconds at a Fabricator.

## UNIT-TIER1 — Size tier

Edit **Size tier** under **Appearance and size** in Entity Editor or the unit resource Inspector. It is an authored physical size class, starting at 1 in half-tier increments and allowing values beyond 5. The catalogue exposes it as `UnitStats.SizeTier` for each unit type. Older resources default to Tier 1.

Size multiplier still controls visual and collision scaling. Size tier currently has no gameplay effects. The Worker starts at Tier 3 because its enlarged drill gives it a 2.205-unit collision radius, compared with 1.6 units for Hexagon Siege. The Worker's Battle Rating remains zero.

## ROCKET1 — Twin Rocket Launcher

Select **Twin Rocket Launcher** in the Benchmark painter or Level Editor, or queue four at the Fortress Defense Fabricator for 220 Carbon and 15 seconds. The Tier 2 fork has two visible launchers and fires one rocket every 1.5 seconds, alternating sides on successful shots with the normal ranged target and cooldown. The 5–35 unit weapon reaches over obstacles using standard artillery flight and deals 12 damage at the center of a 2.5-unit splash. Edit `DEFINITIONS/TwinRocketLauncher.tres` for unit stats and `../PROJECTILES/Types/TwinRocket/TwinRocket.tres` for flight and blast settings. Preset army compositions retain their existing rosters. The fork's circular clearance encloses the whole hull, including the open notch. Each successful rocket shot also triggers the same visual hull recoil as Light Artillery (0.35-unit kick, 0.4-second recovery), without displacing the simulation footprint.

In `UnitVisualConfig.tres`, set **Use Projectile Launch Effects** before launching a scene. This enables thin golden trails and brief muzzle flashes for Twin Rocket Launcher and Light Artillery. The rocket mesh and Twin Rocket Launcher linear dimensions are 25% larger than in the initial ROCKET1 release.

## Dreadnaught turret cadence

The Dreadnaught's four independent miniguns use `TurretShotInterval` from `DEFINITIONS/Dreadnaught.tres`, currently **0.1128 s** per gun (formerly the `UnitDefinition` default of 0.15 s). This is approximately **33% more shots per second** per gun. Its ordinary `Interval` controls hull contact damage and remains unchanged; bullet speed, damage, range, and targeting are unchanged. This interval is shared by all four guns, each with its own targeting and cooldown.

The optional enhanced Twin Rocket Launcher model (`UseEnhancedMeshes`) retains the wide, open fork with subtly tapered tips, swept shoulders and a deeper visual notch. It has a dark underframe, faction-painted hull, swept prong armor, compact stepped rear deck and two segmented launcher assemblies with tapered recessed muzzle collars. The visual outline is separate from the original compiled collision outline. It uses the shared two-tone unit armor mask in all existing preview/battle paths. Fixed launchers are built from each authored mount pose, with the muzzle endpoint unchanged; the plain mesh remains available. No stats, footprint, cooldown, projectile or recoil changes. Owner visual/performance checks pending.

### Rocket mounts

Open a unit definition in the Godot Inspector and expand **Rocket Mounts**. Each entry has a hull-local position (relative to the mesh center), yaw, barrel dimensions, damage and projectile. Local forward is −Z. The unit's Size Multiplier also scales the mount. **Projectile Scale** only scales the projectile model. Use the splash radius and edge fraction overrides to tune one mount without editing the shared projectile. Negative override values inherit defaults.

Fixed mounts on a ranged unit fire in array order using one primary target and cooldown. They default to the unit's range, interval, damage and projectile. The first fixed mount can override the group's range and interval. Use the same obstacle-fire mode for all mounts in a fixed group. Independent mounts acquire their own targets and use their own range and interval in the Benchmark. The Dreadnaught's center-deck cannon is an independent mount and uses Twin Rocket flight, splash and launch effects. Remove that entry from Rocket Mounts to remove the cannon.

In `Dreadnaught.tres`, the **Weapon Mounts** group contains four independent twin minigun mounts and the center rocket cannon. Each minigun can change position, model scale, range, projectile, damage and interval. Its default damage, interval and projectile come from the Dreadnaught's shared minigun settings. Enable **Use Legacy Miniguns** only for definitions that still use the original fixed four mounts.


## PRISM1 — Prism Lance

Prism Lance is a Tier 3 ranged unit with a narrow six-sided hull, twin rails and a cyan charging lens. Its Size Multiplier is now **1.6×** (20% smaller than its previous 2.0× size), including its mesh dimensions and collision footprint. It charges for 3 seconds, deals 65 damage to up to 24 hostile soldiers in a straight line, then recovers for 6 seconds. Its starting range is 8–58 units. It uses the existing nearest-target search and fires through its target until it meets cover, terrain or the range limit. Damage and recovery use the unit's ordinary Damage and Interval fields. The **visible beam flash** lasts **0.36 seconds** (was 0.18 seconds); piercing damage still resolves on the firing tick. The **Prism Beam** section in Entity Editor exposes the remaining weapon settings.

Paint it in the Benchmark or Level Editor. In Fortress Defense the Fabricator produces two for 240 Carbon in 18 seconds. A plain move order interrupts a charge. Attack-move can hold to fire and resumes the order when no target remains.

## Obliterator

The Obliterator is a Tier 2 melee assault unit with a tapered hull and a raised center ridge. Its Size Multiplier is **0.9×**, reducing its mesh dimensions and collision footprint by 10%. It uses Elite Infantry's 285 HP and 22 base damage with a higher base speed of 3.5. Its single-target opener deals 33 raw damage per new enemy, then 22 on repeat hits. It is available in the unit catalogue, AutoBattler recruitment for 40 credits, and the Fabricator in batches of four for 220 Carbon.

## Obliterator and Prism Lance visual trials

**Use Obliterator Assault Mesh**, **Animate Obliterator Attack**, **Animate Obliterator Walk**, **Use Prism Lance Mesh**, **Animate Prism Weapon**, and **Animate Prism Walk** ship enabled in UnitVisualConfig.tres. Disable each mesh independently to restore its old model. Save and relaunch after changing settings.

Obliterator is the fast tier-2 single-target assault unit, not the separate Siege Ram. Its narrower upright five-sided chest, compact waist, close shoulders, visible biped legs, vertical spine pack and narrow pointed dagger distinguish it from the low four-legged Diamond Cavalry and Pentagon cleaver. The torso visual width is reduced by 18%, with a shallower chest and taller torso within the original height. The blade thrusts on actual melee feedback and recovers over 0.36 seconds. The authored body lunge, 1.5x first-hit bonus per new target, normal attack pulse, 0.9x size multiplier and gameplay stats remain unchanged. No new cleave or demolition mechanics. Shared staggered gait: 8.5% peak foot lift, tighter stride travel and 2.6% peak body bounce. The 0.36s attack timing is unchanged; the blade travel is slightly shorter.

Prism Lance is a low six-sided four-pad beam walker with twin accelerator rails, rear power housing and a fixed central emitter. The lens centerline is aligned to the existing nominal beam muzzle (-0.92 radiusZ, ground height 0.85 height); the existing charge orb is unchanged. Tagged rails converge laterally as actual charge progresses, reset on interruption and recoil on the existing discharge event over 0.42 seconds. Body recoil and 0.16s attack pulse remain; the fixed lens is fixed relative to the hull, so the original world-space beam may momentarily diverge during whole-body recoil. No changes to beam origin, damage, range, charge duration, firing budget, flash duration, size multiplier or hit limits. Shared staggered diagonal pad gait: 4% peak foot lift and 0.8% hull bounce.

Both use one shared masked surface per type, existing packed attack phase and optional gait data. Prism weapon animation reuses custom Z for normalized real charge (Hexagon uses the same channel in its own batches for stomp). No extra per-unit nodes, timers, lights, draw calls or emitters. The separate Siege Ram's chassis and tracks are now stretched 12% rearward from their original front edges (roughly 11% longer total rest silhouette); front guide, moving piston, contact face, attack animation and gameplay footprint remain unchanged. Check rear clearance visually in dense formations. Owner validation: weapon/beam alignment during recoil, charge interruption, first and repeat melee contacts, gait, legacy toggles, cracks, shield shells, lit/unlit and FPS.

## Light Artillery visual experiment

With `UseEnhancedMeshes` enabled in `UnitVisualConfig.tres`, Light Artillery (`chevron_artillery`) uses a low layered faction-painted chevron hull, colored shoulder plates and barrel receiver, dark gunmetal mechanical housings and barrel, and restrained emissive strips. Mobile units intentionally use more visible faction paint than neutral-metal buildings. Disabling the existing option restores the plain model. The existing instance color/alpha buffer remains unchanged: enhanced mesh UVs encode steel/paint/trim and baked brightness, while instance RGB/alpha retain faction color, hit flash and fracture state. Lit and unlit battle rendering, 3D previews and reinforcement entry clipping share the mask logic. No new batches or per-unit nodes. Gameplay dimensions, muzzle, elevation, recoil, stats and all other unit meshes are unchanged. Owner visual and performance validation pending.
