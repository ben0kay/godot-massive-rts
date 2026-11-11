# UNIT0 — Five-Shape Soldier Catalogue Implementation Plan

**Repository:** `ben0kay/godot-massive-rts` · **Branch:** `main` · **Status:** Source Implemented / Awaiting Owner Validation · **Owner:** `UNITS/`

## Objective and scope

Replace BENCH0's hardcoded triangle-melee / cube-ranged / hex-heavy kind assumptions with a small **data-driven catalogue of five visually distinct, playable unit types**. Keep the clean geometric, pseudo-3D art direction, red/blue factions, white damage feedback, mass-scale batching, and stress-test arena.

The five requested units, in **display order**, are:

1. **Circle — Basic Melee Infantry**: cheap general-purpose front line.
2. **Square — Stronger Melee Infantry**: sturdier, harder-hitting, slower.
3. **Equilateral Triangle — Basic Ranged Infantry**: shoots projectiles, fragile at close range.
4. **Diamond — Fast Cavalry**: mobile close-range flanker, lower staying power.
5. **Regular Pentagon — Elite Infantry**: very slow, tough, large (~2× the square's *linear visual dimensions*).

These refer to the unit **silhouettes seen from above**. They must remain short extruded **3D** solids, not flat 2D sprites. Reuse the current baked top-bright / side-dark vertex colours; no dynamic per-unit lighting, meshes, shadows or per-soldier nodes.

**Do not implement** economy, cost/recruitment UI, research, terrain elevation, advanced formations, new projectile effects, mounted character models or complicated charge/pathfinding mechanics in UNIT0. Values in the [starting roster](DEFINITIONS/STARTING_ROSTER.md) are **tunable starting defaults**, not approved final balance.

## Read before coding

- Root `README.md`, `NOTES/CodePreferences.md`, any `AGENTS.md`
- `BENCHMARK/BenchmarkArena.cs`, `BENCHMARK/CONFIG/BenchmarkConfig.cs`, `BENCHMARK/SCENARIOS/DeploymentFactory.cs`, `BENCHMARK/UI/BenchmarkPanel.cs`
- `SIMULATION/UnitState.cs`, `SIMULATION/MovementSystem.cs`
- `COMBAT/CombatSystem.cs`, `COMBAT/ProjectileSystem.cs`
- `RENDERING/ArmyRenderer.cs`, `RENDERING/FacetedSoldierMeshes.cs`
- `DEPLOYMENT/DeploymentLayout.cs`, `DEPLOYMENT/DeploymentPainter.cs`
- Relevant feature READMEs and the latest code on `main`, because new BENCH0 features may have landed since the plan was written.

## Required owner folder structure

Follow **UPPERCASE major root directories**, with narrow internal subsystems. This is proposed ownership; create folders when implementing actual files, and do not add generic root-level `Scripts/` or `Resources/`.

```text
UNITS/
    README.md
    UNIT0_IMPLEMENTATION_PLAN.md
    DEFINITIONS/
        STARTING_ROSTER.md
        UnitDefinition.cs                 # Planned typed Resource
        CircleInfantry.tres
        SquareInfantry.tres
        TriangleArcher.tres
        DiamondCavalry.tres
        PentagonElite.tres
    CATALOG/
        UnitCatalog.cs                    # Planned shared lookup/registry
        DefaultUnitCatalog.tres           # Planned catalogue resource
    VISUALS/
        ...                               # Only if unit-authored mesh descriptors are needed
SIMULATION/
    UnitState.cs                          # Compact per-soldier runtime arrays
    MovementSystem.cs                     # Movement by type's cached stats
COMBAT/
    CombatSystem.cs                       # Generic attack/range/armour rules
    ProjectileSystem.cs                   # Generic ranged attacks
RENDERING/
    ArmyRenderer.cs                       # MultiMesh groups by faction + shape
    FacetedSoldierMeshes.cs               # Low-poly mesh generation / baked-face tints
DEPLOYMENT/
    DeploymentLayout.cs                   # Unit ID in placements, validation, saves
    DeploymentPainter.cs                  # Five brushes per faction / footprint
BENCHMARK/
    BenchmarkArena.cs
    CONFIG/
    SCENARIOS/
    UI/
```

`UNITS/` owns **definitions and catalogue**; shared simulation/combat/render code stays in its existing major system folder. Do not duplicate those systems under `UNITS/`.

## Data model

Introduce `UnitDefinition : Resource` with at least:

- **Stable `Id`** (e.g. `circle_infantry`), display name, role/description; do not use presentation order as a persisted identity.
- **Visual shape** (circle, square, equilateral triangle, diamond, regular pentagon), dimensions / visual scale, optional facing/orientation.
- **Gameplay**: max health, speed (world units/s), damage, attack interval (seconds), attack reach, armour reduction (0–1), combat style (melee / ranged), physical footprint radius or dimensions, projectile speed where applicable.
- Designer-tunable values visible in the Inspector; guard against invalid negative stats, zero cooldown/range, null/duplicate IDs.
- Define **damage mitigation explicitly** (suggested: `appliedDamage = max(0, incomingDamage * (1 - armourFraction))`); keep it inexpensive and consistent for melee and ranged. Avoid adding multiple overlapping armour systems.
- The pentagon's ~2× dimensions are **relative to the square's physical mesh size**, and its gameplay footprint must be consistent with the larger body. Do not just increase the mesh without considering spacing/placement/reach.

Create `UnitCatalog` as a single owner for five authored `.tres` definitions and their stable IDs. Load/resolve resources **once at arena setup**, validate their IDs, and copy required stats into **small readonly runtime lookup arrays/structs**. A soldier stores only a small integral type index; **never dereference a `Resource`, create a Node, or allocate an object per soldier in hot loops**. Keep per-soldier HP, cooldown, faction, target, etc. as compact authoritative arrays. Do not overload `Kind` to mean a combat mode, a shape, and a brush index in different places.

Choose and document the runtime numeric mapping **explicitly** rather than relying on enum ordinal or UI dropdown ordering. Use the stable textual ID for new saved deployments (or a versioned stable numeric-ID mapping); the active build's dense index is strictly an internal performance detail.

## Five mesh silhouettes

- Circle: 10–12-sided near-circular low-poly **cylinder**, short and clearly round from above.
- Square: four equal-sided edges with corners aligned to battlefield X/Z axes; short **square prism**.
- Triangle: three equal-length sides (**equilateral**), triangular **prism** with readable pointing direction.
- Diamond: **four equal-sided edges** rotated 45° from the square's plan orientation (a square rotated in X/Z); low-profile diamond prism, distinct from the square even when both use four vertices.
- Pentagon: **five equal sides**, extruded regular pentagonal prism, approximately **2× the square's width/depth and height** initially; scale and body clearance should be tunable.

For all five, tops brighter and sides darker with existing fake directional facet tint; red/blue instance colour multiplied into the mesh colours, white damage flash retained. Shadows remain off. Use **one shared mesh per shape** and `MultiMeshInstance3D` batches grouped by team × mesh/compatible material; ~10 batches for the initial five types. Continue reusable CPU buffers/sparse-vs-dense uploads from `ArmyRenderer.cs`, proper swap-remove on deaths, no per-unit 3D nodes or physics bodies. Keep the 16-float packed instance stride correct if transforms/scales change.

## Combat and movement behavior

All five types must differ **in real simulation**, not just silhouette. Read values from the catalogue's cached runtime stats rather than BENCH0-wide `Health`, `Damage`, `Speed`, `MeleeReach`, `RangedReach`, or a hardcoded `Kind == 1` test. Bench config should retain general performance/workload controls (simulation ticks, spatial budgets, cap, seed, etc.) instead of owning each unit's inherent stats.

- **Circle:** baseline, normal movement, close-range attacks.
- **Square:** extra HP/damage/armour; slower than circle.
- **Triangle:** ranged target acquisition/projectile firing; fragile HP; keep existing bounded projectile pool and hit behavior. Attack interval belongs to the unit; any BENCH0 fire-rate control should be clearly labelled a **benchmark override/multiplier** rather than silently replacing all unit stats.
- **Diamond:** noticeably faster movement; melee attack. First pass needs no special charge state, collision/knockback or independent mounted entity. Strength = mobility, weakness = low durability.
- **Pentagon:** strongest and slowest frontline unit with high HP, armour and damage, larger footprint/reach. No per-unit physics; document any movement crowding limitation left for future formation/collision work.

Keep current spatial-grid performance budgets, target-eligibility checks, dead-unit cleanup and projectile correctness. Account for body size in melee stop/range / deployment spacing where cheap and appropriate, but **do not add all-to-all separation or navigation**. Keep both army factions symmetric in their access to the catalogue.

## Migration and benchmark preservation

The current code has `Kind = 0` melee triangle, `Kind = 1` ranged cube, `Kind = 2` visual heavy hex. The painted deployment editor currently supports only kinds 0/1, and its `user://deployment.json` is a raw array of serialized placements with numeric kinds.

**Do not blindly reinterpret existing saved `Kind` values** when changing type ordering.

- Accept the legacy deployment format and map **legacy 0 → Circle (melee)** and **legacy 1 → Triangle (ranged)**. Optionally map legacy 2 → Pentagon if it is encountered; the old painter did not expose it. Explicitly version new saves and store stable unit IDs so future catalogue reorderings do not corrupt layouts.
- Replace brush choice's hardcoded `Brush / 2`, `Brush % 2` logic with explicit `(faction, unitId)` choices. Support all **five types for each faction** (10 brush selections or clearly labelled faction + unit dropdowns).
- Update `DeploymentLayout.TryPaint` validation and painter previews; a large Pentagon must have appropriate **2× footprint/occupancy or sufficiently enforced non-overlap spacing** rather than being silently placed exactly on top of neighbors. Do not change the combat spatial grid's meaning to solve editor occupancy.
- Maintain Save, Load, paint, erase, and Start Painted Battle workflows. A painted battle with any mix of all five types must start with proper combat behavior. Replace the current `ranged |= placement.Kind == 1` shortcut with role lookup; no ranged-only hardcoded branches.
- Preserve preset BENCH0 **render-only, movement, melee and ranged** modes, unit counts up to 100k (subject to owner hardware), different spawn layouts, camera, timers and performance overlay. Presets should choose from the new 5-type catalogue with deterministic seeded compositions; expose or document their type distribution. Melee mode excludes ranged triangle units or defines an explicit behavior when included; ranged mode includes triangle.
- Avoid destroying old benchmark comparability: document that new unit types/mesh sizes/compositions affect performance measurements. Do not claim the former 700 FPS still applies.
- Avoid touching unrelated camera, save paths, terrain, renderer driver, app menus or debug HUD.

## Implementation sequence for Codex (UNIT0 only)

1. **Definitions:** Add typed `UnitDefinition` and registry, validate five `.tres` resources and small runtime stat records; document stable ID policy and stats.
2. **Geometry/rendering:** Replace three shapes with five correct, well-proportioned faceted solids; expand MultiMesh batch indexing safely, including empty groups, packed transforms, white flashes, death packing and 2× pentagon scaling.
3. **Simulation/combat:** Replace global per-type assumptions with cache-based values for spawn HP, per-type speed/reach, cooldown, damage, armour and ranged handling. Keep hot loops allocation-free and use existing spatial indexing.
4. **Deployment:** Make 10 brushes / five type selections per faction, painter placement/preview, saves + legacy load migration and painted battle activation work for all five.
5. **Bench integration:** Keep deterministic presets / all BENCH0 modes, provide a reasonable mixed distribution and ensure overlay and caps still work. Update relevant feature READMEs, root roadmap, and append one short `devlog.md` entry in the **same implementation commit**.

Do not build unrelated future systems or turn the small catalogue into a generalized plugin framework.

## Owner acceptance checklist (owner tests in Godot)

- All five **distinct from above**: circle round, axis-aligned square, equilateral triangle, 45° diamond, pentagon ~2× square.
- Blue/red colour is correct, facet lighting remains attractive without real-time light/shadow, damaged units flash white.
- In a same-distance test, diamond arrives first; pentagon visibly slower; square survives longer than circle; triangle fires at range; pentagon has tougher effective HP and larger physical silhouette.
- Unit types are available to **both teams** in painter; paint, erase, save, load, reset, and start battle work. A pre-UNIT0 deployment with legacy ranged kind 1 still reloads as ranged **triangle**, not melee **square**.
- All BENCH0 modes, deterministic counts, performance readout and 1k/10k/25k tests remain functional. Test high counts manually; CPU/memory remain bounded, no per-soldier nodes or light/shadow work.
- No errors at zero units, no missing Mesh instances, no erroneous type indices after mass deaths/reset.

## Validation and delivery

Follow `NOTES/CodePreferences.md` strictly: **lightweight source/diff review and at most one readily available `dotnet build`**, no extensive automated or repeated stress tests. The owner performs in-game validation. Update feature docs, append a concise `devlog.md` implementation entry, **commit and push one focused change to `main`**, report SHA, scope, limitations and owner checks. Mark status **Source Implemented / Awaiting Owner Validation**, never claim FPS targets without measurements.
