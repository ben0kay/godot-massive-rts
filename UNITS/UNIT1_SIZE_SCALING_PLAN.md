# UNIT1 — Uniform Soldier Size Scaling

**Repository:** `ben0kay/godot-massive-rts` · **Branch:** `main`
**Status:** Source implemented. Awaiting owner validation in Godot.

## Objective

Add **one editable SizeMultiplier per soldier type** to its existing `UNITS/DEFINITIONS/*.tres` resource. A larger unit must become physically larger in the **shared mesh, gameplay footprint and unit-owned melee/area contact radius**, not merely look bigger. The user wants **Diamond Cavalry 25% larger** by default. Preserve its elongated ~1.9:1 rhombus silhouette, short height, smooth facing, 2-second acceleration, charge/trample and existing 3-faction battles.

**Initial settings:** all other types = **1.0**; `DiamondCavalry.tres` = **1.25**.

No per-soldier Godot physics nodes, instances, components or dynamic per-frame size calculations. Compile effective per-type dimensions once in `RuntimeUnitCatalog` at battle/scene initialization.

## Current facts to check on live main

The original type definitions expose `Width`, `Height`, `FootprintRadius`, `Reach`, and trample/cleave authoring. Their current numbers are **base inputs**.

| Type | Base width | Base height | Base footprint radius | Base reach |
| --- | ---: | ---: | ---: | ---: |
| Circle infantry | 0.80 | 0.4125 | 0.57 | 1.60 |
| Square infantry | 0.80 | 0.4125 | 0.57 | 1.80 |
| Triangle archer | 0.80 | 0.4125 | 0.57 | 32.00 ranged |
| Diamond cavalry | **0.64** | **0.30** | **0.63** | **1.90 melee** |
| Pentagon elite | 1.60 | 0.825 | 1.14 | 2.30 melee |

`RENDERING/FacetedSoldierMeshes.cs` generates one shared static mesh per type using compiled `UnitStats.Width`, `Height` and, for Diamond, a long point derived from `UnitStats.Footprint * 0.96f`. `RENDERING/ArmyRenderer.cs` uses compiled height to ground instances, interpolates heading for noncircles, and multiplies the per-instance transform by the temporary **FB0 hit pulse scale**. Do **not** confuse permanent size with this transient 1.06× feedback pulse.

`UNITS/CATALOG/UnitCatalog.cs` compiles authored resources into readonly `UnitStats`, currently applies `Footprint = Max(Width * 0.707107, FootprintRadius)`, computes `MaxFootprint`, and defines `AttackReach(defender)` as `Ranged ? Reach : Reach + defender.Footprint`.

`COMBAT/AreaDamage.cs`:
- `Cleave` uses `type.Reach + units.Catalog.MaxFootprint + movementPadding` for broadphase and `type.AttackReach(defender)` per candidate, so the **Pentagon's swipe radius is already tied to Reach and Footprint**.
- `Radial` is a reusable generic circle **taking a caller-provided radius**; it does **not** magically know which soldier authored it.

`COMBAT/TRAMPLE/TrampleSystem.cs`: charge contact sweep, surviving contact clearance and query padding all read the compiled `Footprint`. Charge run-up and charge travel distance are *movement distances* and should **not** automatically scale merely because the soldier's body grew. Keep momentum, efficiency, damage and max hits unchanged.

`SIMULATION/SEPARATION/SeparationSystem.cs` and SEP2 allied yielding use compiled per-type `Footprint` and `MaxFootprint`. `DEPLOYMENT/DeploymentLayout.cs` checks painter spacing using those radii, and `BENCHMARK/SCENARIOS/DeploymentFactory.cs` derives default line spacing from `MaxFootprint`. `DEPLOYMENT/DeploymentPreview.cs` uses the shared mesh and type height. `PROJECTILES/Shared/ProjectileSystem.cs` uses unit `Height` to locate shooter/target centre.

Recent commits include Circle rotation bypass, interpolated motion, 3 factions, Pentagon cleave, cavalry trample and FB0 hit pulses. **Read all actual latest files before coding and preserve this work.**

## Ownership / files

Use existing `UNITS/` hierarchy; **do not add a redundant global size manager**:

```text
UNITS/
    UNIT1_SIZE_SCALING_PLAN.md         # This implementation brief
    DEFINITIONS/
        UnitDefinition.cs             # Add SizeMultiplier export
        CircleInfantry.tres           # SizeMultiplier = 1.0 or default
        SquareInfantry.tres           # 1.0
        TriangleArcher.tres           # 1.0
        DiamondCavalry.tres           # SizeMultiplier = 1.25
        PentagonElite.tres            # 1.0
        STARTING_ROSTER.md            # Document effective vs base size
    CATALOG/
        UnitCatalog.cs                # Cache compiled effective values in UnitStats
```

Only touch other systems where a genuine size-dependent value is currently bypassing the compiled catalogue.

### A. One Inspector knob

In `UnitDefinition.cs`, add a serialized float:

```csharp
[ExportGroup("Size")]
[Export(PropertyHint.Range, "0.5,3.0,0.05")]
public float SizeMultiplier { get; set; } = 1.0f;
```

The exact legal range can be chosen conservatively (e.g. 0.5–3.0) and must be enforced at catalogue compilation. Validate NaN/Infinity/zero/negative values using fallback 1.0. Keep `Width`, `Height`, `FootprintRadius` and `Reach` as existing authored **base** values; do not rename their serialized properties, which could break old `.tres` files. The size multiplier must be independent of temporary hit-pulse scaling, and not vary per soldier. Each type's value is shared across factions.

Document clearly that 1.25 means **25% larger in each linear dimension**, NOT 25% added area/volume. The diamond must remain elongated by the same aspect ratio.

### B. Compile effective sizes once, not every tick

In `RuntimeUnitCatalog` cache derived `UnitStats`:

```csharp
float size = SafeClamp(d.SizeMultiplier, 0.5f, 3f, 1f);
float baseWidth = Positive(d.Width, 0.8f);
float baseHeight = Positive(d.Height, 1.1f);
float baseFootprint = Mathf.Max(baseWidth * 0.707107f,
                               Positive(d.FootprintRadius, 0.57f));
float width = baseWidth * size;
float height = baseHeight * size;
float footprint = baseFootprint * size;
float reach = d.Style == AttackStyle.Melee
    ? Positive(d.Reach, 1.6f) * size
    : Positive(d.Reach, 1.6f);
```

The exact code may differ but this is the intended contract. **Never scale the same field twice.** Preserve fallback and positive-value semantics of the existing catalogue. Keep `MaxFootprint` computed from the effective values, because combat queries, separation and placement need the largest physical soldier.

Add `SizeMultiplier` to `UnitStats` only if needed as an explicit cached owner scale for other size-relative AoEs; do not force every existing hot loop to multiply it again. Prefer compiled effective floats in the existing fields to minimize changes.

**Melee attack logic:** existing `AttackReach(defender)` uses the attacker's effective melee `Reach` plus the defender's effective `Footprint`. **Do not also add the attacker's footprint separately** or it would double-expand contact range. Ranged `Reach` should remain the authored projectile shooting range by default; simply becoming a larger archer should not make arrows fly 25% farther unless separately requested.

### C. Cavalry initial 1.25× result

Author `SizeMultiplier = 1.25` in **DiamondCavalry.tres**. Keep its original base `Width = 0.64`, `Height = 0.30`, `FootprintRadius = 0.63`, `Reach = 1.90` as editable base definitions.

Compiled effective Cavalry at 1.25:

| Property | Current 1.0× | Desired 1.25× |
| --- | ---: | ---: |
| Mesh narrow width | 0.64 | **0.80** |
| Mesh height | 0.30 | **0.375** |
| Approx elongated diamond length | ~1.21 | **~1.51** |
| Gameplay footprint radius | 0.63 | **0.7875** |
| Base melee reach | 1.90 | **2.375** |
| Max speed | 9.0 | **9.0 unchanged** |
| Acceleration time | 2.0 seconds | **2.0 seconds unchanged** |
| HP / damage / attack interval | current | **unchanged** |

Do not increase its actual trample run-up distance, charge distance, resistance, trample efficiency, damage multiplier or shove cap: physical contact width grows automatically through effective `Footprint`; gameplay ability tuning stays separate.

### D. Area/swipe radii and ability correctness

**Pentagon cleave is essential**:
- Use effective `Reach` everywhere `Cleave` compares distances AND in candidate/broadphase query radius. Its fixed 100° cone angle, maximum secondary targets and damage percentages stay unchanged.
- A 1.5× Pentagon should have 1.5× its authored body dimensions/footprint/melee reach. The cone must not hit outside its broadphase or stop short of its actual reach.

**Trample is essential**:
- Use effective attacker's and defender's footprints for contact sweep/penetration and broadphase padding. A 1.25× cavalry must collide with contacts sooner/farther from its centre, while run-up/charge travel length stay the same.
- Preserve current charge targeting, momentum, shove caps, ally handling, input heading and separation bypass semantics.

**Future area effects**:
- For truly *unit-authored* area effect radii (e.g. a future radial stomp based on the caster's size), define a simple explicit owner-size rule, such as `effectiveRadius = baseAbilityRadius * owner.SizeMultiplier`, compiled in its ability definition or supplied at the call site.
- `AreaDamage.Radial` currently takes a free `radius` argument and may represent explosion/projectile-specific geometry. **Do not globally multiply every generic radial radius by a random nearby unit's size**; preserve projectile-defined impact radii unless an ability is explicitly size-relative.
- Do not scale damage, attack rate, weapon projectile speed, area falloff fraction, projectile hitbox or shot range by this generic body-size knob.

### E. Renderer, deployment and simulation consistency

- Mesh is generated from the **effective** dimensions once per type. Do **not** apply a permanent 1.25× scale again to each MultiMesh instance, since that would double-scale visuals and break visual/physical agreement.
- `ArmyRenderer` uses effective height for grounded centres, preserves the existing 1.06× FB0 temporary hit expansion, smooth position/rotation for noncircles and Circle facing skip. Both sparse and dense upload paths must still render correctly.
- Deployment previews and painted battles must use the same effective mesh and Y centre. `DeploymentLayout.TryPaint` must enforce scaled radius against neighbours and level bounds; placing a bigger pentagon may legitimately require more space.
- SEP1/SEP2 must reuse cached effective footprints. Keep the same 50% allied ranged clearance rule, which should compute from each pair's new radii rather than hard-coded original sizes.
- Enemy targeting, melee stopping distances, projectile collision, projectile spawn/target Y and trample sweep should reflect effective physical size through existing catalogue references. Ensure all broadphase upper bounds derived from `MaxFootprint` remain conservative; keep query caps and projectile padding correct.
- Preserve all factions, type IDs, legacy migration, current JSON deployment save format and benchmark presets. No need to serialize the scale into each soldier or saved placement: the stable type definition determines the scale on load.
- Treat size editing as a **restart/new-arena setting**, not live rescaling thousands of active units. When the resource is edited, the next battle/scenario initialization recompiles the catalogue. Existing battle instances need not resize on the fly.

### F. Performance requirements

- **No new per-soldier nodes, physics bodies, scene-tree queries, shaders, materials, dictionaries, reflection or runtime resource accesses** in hot loops.
- No per-tick size computations or per-soldier extra allocations; `UnitStats` keeps effective floats at type level. `MaxFootprint` continues to drive the separation cell size and broadphase.
- Shared mesh generation happens once per unit type and already generates static low-poly meshes; visuals remain batched via MultiMesh and its existing sparse/dense update paths.
- A physical size change can make large crowds more congested and increase neighbour/contact checks. This is an expected gameplay cost, **not proof that the multiplier calculation is slow**. Keep existing performance overlays.
- Maintain practical target **10k vs 10k (20k total)**; larger benchmarks remain available. Do not claim measured FPS without owner profiling.

## Owner acceptance / test matrix

1. Open `UNITS/DEFINITIONS/DiamondCavalry.tres` in Godot Inspector: `SizeMultiplier = 1.25`. After restarting the battle, all cavalry are recognizably 25% larger while retaining the long rhombus silhouette and short profile.
2. Set Cavalry to 1.0 then 1.25 and compare identical seed/layout; confirm **visual and gameplay clearance** change together, including painted preview, spawn spacing, SEP1/SEP2 and swept trample contact.
3. Check Cavalry movement still reaches 9 units/s with the same 2-second acceleration; trample run-up 2 and charge travel 5.5 remain unchanged (while radius of physical contact increases).
4. Temporarily set Pentagon `SizeMultiplier=1.5`: its mesh and radius increase; its 100° multi-target swipe reaches more distant enemies and combat broadphase does not miss targets. Reset to 1.0 afterward.
5. Temporarily set Circle or Square to 0.75 and 1.5. Confirm width, height, placement clearance and melee reach all change consistently, and Circle still avoids heading/rotation work.
6. Temporarily resize an Archer. Its physical size, projectile spawn/target height and hit/contact footprint change, while its authored ranged shooting distance remains unchanged.
7. Confirm FB0 white flash and 6% pulse still work and do **not** permanently modify radius or multiply size twice. Verify sparse and dense MultiMesh uploads, damage/death slot swap and between-tick interpolation.
8. Check two- and three-team battles, saved painted layout compatibility, reset/reload and benchmark runs at 1k, 10k, 20k, including CPU and render counters. Build/test lightly under CodePreferences; owner will do extensive Godot testing.
9. Invalid `SizeMultiplier` values (negative, zero, NaN/Infinity, extreme numbers) safely fall back/clamp and do not crash or produce impossible broadphase radii.

## Codex implementation and delivery

1. Read `README.md`, `NOTES/CodePreferences.md`, `UNITS/README.md`, `COMBAT/README.md`, `COMBAT/TRAMPLE/README.md`, this plan, and the latest listed source.
2. Implement `SizeMultiplier` in `UnitDefinition`, catalogue compilation, authored Cavalry resource at 1.25, and any minimal fixes required where effects/ranges ignore the compiled size.
3. Update UNIT1 documentation, `UNITS/DEFINITIONS/STARTING_ROSTER.md`, and relevant COMBAT/RENDERING notes. Append **one** concise `UNIT1 source implemented; awaiting owner validation` entry to `devlog.md` in the implementation commit.
4. Check modified diff and perform **at most one readily available `dotnet build`** per CodePreferences; no expensive benchmarks. Do not add unrelated gameplay systems or alter damage/health/speeds.
5. Make **one focused commit to `main`**, push and report SHA, exactly what became size-responsive, explicit non-scaled attributes and remaining owner test steps.
