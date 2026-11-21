# Tower Implementation Standard (TOWER-STD1)

> **Repository:** `ben0kay/godot-massive-rts` · **Branch:** `main` · **Reviewed:** 2026-11-21  
> **Status:** Documentation / design guardrail. **This document does not authorize a framework refactor or implement a new tower.**  
> **Applies to:** buildable and authored stationary defense towers (Railgun, Rocket, Shield Generator, and future towers). Citadels, Fabricators, Carbon Extractors, and mobile support units have distinct responsibilities and are not automatically weapon towers.

## 0. Read first (mandatory for AI agents)

1. Read root `README.md`, root `CodePreferences.md`, and `endgoal.md`. Read applicable `AGENTS.md`, if any.
2. Read `WORLD/BUILDINGS/README.md` and the **live** files below. Plans and this document can become stale.
3. Identify whether the request concerns **visuals**, **weapon behaviour**, **support-field effects**, **placement**, or **stats**. Change only required owners.
4. Follow the existing light validation policy: small source/diff review and at most one practical build for C# changes; the owner performs extensive Godot and FPS testing.
5. **Do not refactor all towers merely because this document proposes a cleaner future design.** Separate cleanup from new-feature work unless explicitly authorized.

## 1. What is shared today

| Responsibility | Current owner / entry point | Established behaviour |
| --- | --- | --- |
| Authoring values | `WORLD/BUILDINGS/DEFINITIONS/BuildingDefinition.cs` and `DEFINITIONS/*.tres` | Inspector-editable stats, stable textual `Id`, footprint, health, weapon and shield fields |
| Registry and compiled stats | `WORLD/BUILDINGS/CATALOG/DefaultBuildingCatalog.tres`, `BuildingCatalog.cs` | Shared runtime `BuildingStats` indexed through `RuntimeBuildingCatalog.TryResolve(id, out type)` |
| Authored level marker | `WORLD/BUILDINGS/DEFENSE/CommandLaserPlacement.cs` | Legacy class name; `Definition` can point to **any** registered defense. Handles faction, snapped footprint and level placement |
| Benchmark painter | `GAMEPLAY/DEPLOYMENT/DeploymentLayout.cs`, `DeploymentPainter.cs`, `BENCHMARK/UI/BenchmarkPanel.cs` | Catalog-driven list and placement; maximum authored defense count is 64 in `DeploymentLayout` |
| Defense runtime | `WORLD/BUILDINGS/DEFENSE/DefenseSystem.cs` | Bounded tower slots, structure health, acquisition, firing and destruction |
| Damage and health | `WORLD/BUILDINGS/StructureHealth.cs`, `GAMEPLAY/SIMULATION/UnitState.cs`, `GAMEPLAY/COMBAT/DamageMatrix.cs` | Shared structure HP/armour and unit damage; towers use central damage pipeline |
| Rocket projectiles / splash | `GAMEPLAY/PROJECTILES/` and `GAMEPLAY/COMBAT/AreaDamage.cs` | Rocket Tower reuses Twin Rocket resource / projectile path and impact processing |
| Shield support | `GAMEPLAY/SHIELDS/ShieldSystem.cs`; `UnitState` integration | Registered tower emitter recharges friendly soldier shields; no attack/target needed |
| Tower meshes/effects | `WORLD/BUILDINGS/RENDERING/DefenseRenderer.cs` | Shared faceted meshes and MultiMesh batches; Railgun / Rocket / Shield variants |
| Fortress Defense building | `WORLD/BUILDINGS/FABRICATION/CommandBuildings.cs` | Cost, snap, validation, creation, removal; shared tower limit of 32 in command mode |
| HUD menu | `UI/GameplayHUD/CommandBottomHud.cs` | Explicit build buttons / `CommandBuildType` |
| Level editor / preview / saving | `UI/Editor/LevelEditor*`, `UI/Editor/SAVING/LevelEditorSave.cs`, `LEVELS/Runtime/LevelLayoutPreview.cs` | Starting defenses use `CommandLaserPlacement`; definitions and the renderer determine visuals |

**Important:** Tower _assets and capabilities_ are shared, but not every tower type is automatically registered with every system just by adding a `.tres`.

### Current tower examples

| Tower | Definition path | Stable ID | Mechanic | Renderer |
| --- | --- | --- | --- | --- |
| Railgun | `DEFINITIONS/LaserTower001.tres` | `laser_tower_001` | Piercing sustained beam; `Projectile == null` with attack enabled | Railgun base, rotating head and recoil barrel |
| Rocket | `DEFINITIONS/RocketTower001.tres` | `rocket_tower_001` | Twin-rocket projectile path; alternates launcher sides | Rocket-specific base/head/two barrels |
| Shield Generator | `DEFINITIONS/ShieldGeneratorTower.tres` | `shield_generator` | `AttacksSoldiers = false`, `ShieldEmitterEnabled = true` | Separate static shield tower mesh |

The Shield Generator is already in the live building catalog and Fortress Defense build menu. It is a **support tower**, not a no-damage railgun. Do not copy attack/target logic into it.

## 2. Required standards for adding a tower

### A. Definition first; stable IDs forever

- Create one `BuildingDefinition` resource in `WORLD/BUILDINGS/DEFINITIONS/`.
- Give it a unique, lowercase, stable textual ID (e.g. `slow_field_tower`) and clear display name.
- Add it to `CATALOG/DefaultBuildingCatalog.tres`. Ensure `RuntimeBuildingCatalog` can resolve the ID.
- Store tunable footprint, HP, armour, ranges, fire rate, damage, shield/support settings and projectile references in resources rather than duplicating values in runtime code.
- **Never rename an existing ID/resource path casually.** Railgun intentionally retains `laser_tower_001` and `LaserTower001.tres` for saved-map compatibility.
- Keep authoring units explicit: `Width` / `Depth` are **grid cells**, ranges are world units, rates use seconds or per-second values as named. Respect existing clamping and fallbacks.

### B. Keep gameplay and visuals separate

- `BuildingDefinition` / compiled `BuildingStats` own data, not scene logic.
- `DefenseSystem` owns active weapon behaviour, targeting, attack scheduling and tower lifetime.
- `ProjectileSystem` owns projectiles; `AreaDamage` owns AoE application. Reuse them, do not fork bespoke versions for every new turret.
- `GAMEPLAY/SHIELDS/` owns shields and their gameplay; support effects should not masquerade as attacks.
- `DefenseRenderer` owns mesh construction, aiming transforms, recoil and presentation effects. Presentation does not determine damage or collision.
- Keep shared faction targeting/armour rules, obstacle/terrain occlusion, pause/reset/cleanup and stable ID semantics.

### C. Use shared structure placement and lifecycle

- Register the definition in the catalog, then resolve by textual ID.
- Reuse `BuildingPlacement` for benchmark placement and `CommandLaserPlacement` for currently authored level defenses. The latter name is historical; do not create a duplicate marker for a new tower.
- Reuse `CommandBuildings.CanPlace` / `Snap` / `Place` and their map, footprint, terrain, obstacle, overlap and Carbon checks in Fortress Defense.
- Register with `DefenseSystem` and `StructureHealth` so damage, destruction and obstruction rebuilds remain shared.
- When a tower is destroyed or a battle resets, disable its targeting/beam/projectiles/field and hide its render slot. Do not leave active support auras or visual effects behind.
- Ensure a source placed in a map scene is equivalent to one built during play.

### D. Reuse batching

- Build and cache **one shared mesh per tower appearance**, not per placed tower.
- Use `MultiMesh` batches and the project's baked faceted materials/faction colouring, where appropriate.
- Preserve the real muzzle/beam/projectile origins when visual barrel geometry changes.
- Never add a dedicated `_Process`, `PhysicsBody3D`, `Timer`, light or unique material per tower/shot without an explicitly reviewed small-count exception.
- Pool/budget flashes, trails, smoke, beams and transient effects. When disabled, expensive visuals should go dormant.
- Do not overwrite or reinitialize render batches for other tower types just to show one new type.

### E. Add to every relevant workflow

- **Benchmark:** Catalog selection, paint, erase, preview, battle runtime, debug, reset.
- **Level Editor / Preset Battles:** Defense list, placement marker, saved level, runtime and preview.
- **Fortress Defense:** `CommandBuildType`, cost, build button, snap/placement and damage/selection/lifecycle.
- **Entity Editor:** Relevant definition fields grouped and editable; only show controls applicable to this attack/support style.
- **Documentation:** Update `WORLD/BUILDINGS/README.md`, and append one concise `devlog.md` entry when source code is implemented.

Benchmark deployment JSON stores defenses by **building ID** (`SavedDefense.BuildingId`), not catalog index. Keep that stable-ID save contract.

## 3. Known inconsistencies — do not treat these as the ideal template

1. **Behaviour inferred from fields rather than an explicit role:** Current attack logic uses `AttacksSoldiers`, `ProjectileType >= 0`, and `ShieldEmitter.Enabled` to distinguish beam, rocket and support. A fourth weapon behaviour cannot safely be introduced just by dropping in a new projectile/null resource.
2. **Renderer variant selected with booleans:** `DefenseRenderer.ShowTower(..., rocket, shield)` selects hardcoded geometry; `Aim` knows Railgun vs Rocket. A new visible tower type needs an explicit rendering integration and muzzle/animation contract.
3. **Hardcoded Fortress Defense build registration:** `CommandBuildType`, definition lookups, `Snap`, `Cost`, `Place`, and `CommandBottomHud` enumerate tower kinds directly.
4. **Costs are inconsistent:** Railgun and Rocket use constants in `CommandBuildings`; Shield Generator reads `BuildingDefinition.BuildCost`. Do not assume all costs are currently data-driven.
5. **Historical names remain:** `LaserTower001.tres`, `laser_tower_001`, `LaserTowerCost`, and `CommandLaserPlacement` still represent the renamed Railgun or generic authored defense markers. Preserve compatibility; plan aliases/refactoring separately.
6. **Different capacity limits:** Benchmark authored layout allows up to 64 defenses (`DeploymentLayout.MaxBuildings`); Fortress Defense and starting-map defense slots currently use `CommandSandbox.MaxStartingTowers = 32`. Do not assume a single shared cap.
7. **Special-case visuals and abilities:** Shield Tower is separately rendered and registered as an emitter; Railgun beam and Rocket launch/recoil are individually implemented. These are legitimate specializations but should not cause a parallel generic defense engine.

**Recommendation, not implemented:** Once several more tower types exist, a small explicit `DefenseRole` or `WeaponMode` field plus a typed tower-render variant/catalog may remove fragile field inference and long `if`/ternary chains. First audit save compatibility, previews, editor fields and all existing tower behaviour. Do **not** launch a broad refactor as part of adding one tower unless the owner approves.

## 4. Performance requirements for every new tower

- Know the active count: a few dozen towers differ from 10k soldiers, but projectile/area effects can still become expensive.
- Build resources/meshes **once**; no per-shot allocation of entire projectile scenes or materials.
- Use bounded `SpatialGrid` queries, staggered acquisition if necessary, and no O(towers × all units) unconditional scans.
- Work must be quiet when the tower has no target, is disabled/destroyed, or the battle is paused.
- Cache/unify collision and area queries where practical. Use XZ ground distance consistently and terrain Y where a hit/sight calculation needs it.
- Effects should have caps and shut off cleanly when full, with authoritative gameplay unaffected.
- Confirm a new type's renderer doesn't accidentally display another type's mesh or keep stale transforms after pooled slot reuse.
- Benchmark **render-only**, movement, active combat, mass volleys and mass tower destruction separately when performance work is explicitly requested.
- Report actual CPU/GPU/frame timing only when measured; successful compile is not proof of FPS.

## 5. AI implementation checklist

Before calling a new tower complete, confirm:

- [ ] Read root guidelines and live related code; identify the closest existing tower.
- [ ] Decide **beam**, **projectile**, **support field**, or **new explicitly approved behaviour**.
- [ ] Create `.tres` with stable ID and register it in the default building catalog.
- [ ] Compile and validate its settings in the existing `BuildingStats` path.
- [ ] Reuse `DefenseSystem`, `StructureHealth`, damage rules, projectile/support subsystems.
- [ ] Add one properly batched tower visual and preserve true muzzle/collision positions.
- [ ] Add Benchmark, Level Editor and Fortress Defense integrations as appropriate.
- [ ] Check Carbon price, build limit, placement/terrain, friendly/enemy faction, saved IDs and old-map compatibility.
- [ ] Check destruction, reset, pool-slot reuse, runtime construction and authored map placement.
- [ ] Confirm UI tuning in Entity Editor and any necessary debug range overlays.
- [ ] Sanity-check source and optionally one build; leave longer Godot/FPS tests to owner.
- [ ] Update feature docs and add a `devlog.md` implementation entry for actual code changes.
- [ ] Report exact changed files, commit SHA and unverified owner tests.

## 6. Example prompt for Codex

> Read `CodePreferences.md`, `README.md`, `endgoal.md`, and `WORLD/BUILDINGS/TOWER_IMPLEMENTATION_STANDARD.md` first. Inspect the current Railgun, Rocket and Shield Generator Towers and reuse their shared definition, catalog, health, placement, renderer and projectile/support systems. Implement only the requested tower. Preserve existing stable IDs and compatibility; do not introduce a separate tower engine or per-tower scene-tree processing. Document any place where existing hardcoded type selection must be extended, keep it focused, and report files changed, a single commit, lightweight validation, and owner-side Godot tests.
