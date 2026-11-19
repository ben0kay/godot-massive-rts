# DREAD1 — Tier 5 Dreadnaught super-heavy assault platform

**Status: Planned only — Codex implementation handoff.** This documentation commit does **not** implement gameplay or authorize implementation.  
**Repository:** `ben0kay/godot-massive-rts` · **Branch:** `main` · **Engine:** Godot 4 / C#  
**Official name / stable ID:** **Dreadnaught** / `dreadnaught` (preserve the owner's spelling)  
**SizeTier:** **5**; Hexagon is already Tier 3. Intended scarcity: very costly and generally **2–3 per level**.

## 0. Owner requirements (do not reinterpret)

1. A huge **long rectangular armoured hull with a prominent triangular pointed front**. Low-poly beveled geometry and faction colouring consistent with the game's minimal geometric style.
2. **Four independent minigun turrets:** front-left, front-right, back-left and back-right.
3. Every turret independently **targets the closest valid enemy relative to its own mount/muzzle**, not hull center, and has **separately configurable minimum and maximum firing ranges**. Different turrets can shoot different enemies or converge on the same one naturally.
4. Miniguns shoot **very small, very fast, frequent real projectiles** (not fake hitscan). Every turret has independent cooldown, aiming and rotation.
5. The **hull itself pursues the closest enemy** via ordinary auto-acquisition. It moves **very slowly** and **turns very slowly**. Its travel/target selection must **not be overridden by a turret target** and it must **not stop just because a minigun is in range**.
6. **Only its forward triangular prow hurts enemies on contact while advancing.** Sides and rear deal **NO contact damage** in DREAD1. Side/rear collisions still block or physically push when appropriate, without health damage.
7. It is **one unit**, with one faction, health pool, movement/order ownership, hit/death effects, and 4 independent weapon state slots — **not five linked soldiers**.
8. Tier 5 is a **physical size class**, not a direct visual scale multiplier or arbitrary strength rating. Ensure massive physical proportions through actual mesh dimensions, not by coupling `SizeTier` to `SizeMultiplier`.
9. No user request for restrictive gun arcs: default to **full 360° turret acquisition**, subject to valid firing distances, world/obstacle LOS, occlusion by the Dreadnaught's own hull, and aiming/rotation. Do not silently limit turrets to quadrants. Optional arc settings can be considered later.

## 1. Read code and validate architecture before implementation

Read current `README.md`, `CodePreferences.md`, this plan and relevant local READMEs. Read `AGENTS.md` if present. Inspect the current versions of:

- `GAMEPLAY/UNITS/DEFINITIONS/UnitDefinition.cs`, `HexagonSiege.tres`, `ChevronArtillery.tres`
- `GAMEPLAY/UNITS/CATALOG/UnitCatalog.cs`, `DefaultUnitCatalog.tres`
- `GAMEPLAY/SIMULATION/SpatialGrid.cs`, `MovementSystem.cs`, `UnitState.cs`, `SEPARATION/SeparationSystem.cs`, `COMMANDS/CommandMovement.cs`
- `GAMEPLAY/COMBAT/CombatSystem.cs`, `GAMEPLAY/COMBAT/TRAMPLE/TrampleSystem.cs`
- `GAMEPLAY/PROJECTILES/Shared/{ProjectileSystem,ProjectileRenderer,ProjectileDefinition,ProjectileCatalog}.cs`
- `VISUALS/RENDERING/{FacetedSoldierMeshes,ArmyRenderer,GroundShadowMeshes}.cs`
- `BENCHMARK/BenchmarkArena.cs`, `GAMEPLAY/WAVES/FortressWaves.cs`
- `GAMEPLAY/PRODUCTION/ProductionRecipes.cs`, `GAMEPLAY/PRODUCTION/FabricatorRecipes.tres`
- Deployment painting, level editor, Entity Editor, campaign saves, production/fabrication UI and any newer game-mode owners.

**Verified architectural risks at plan creation (2026-11-19):**

- `UnitDefinition.SizeTier` already exists; Hexagon's authored resource uses **3.0**, and current catalogue enumerates Circle 0 through Pikeman 10. **Append new unit ID; never renumber existing ones.**
- Existing `CombatSystem` and `ProjectileSystem.Fire(shooter, victim, units)` assume **one target, cooldown and central muzzle per soldier**. Dreadnaught requires an opt-in 4-turret controller and a compatible explicitly-originated pooled projectile launch.
- Generic ranged `MovementSystem` stops at weapon reach, which is **wrong for this hull**. Don't disguise it as a normal stationary ranged attacker; the hull should still seek contact.
- `TrampleSystem` is short-duration cavalry charging; it doesn't model ongoing **prow-only** moving contact.
- `SeparationSystem` uses a **circular footprint per unit** and derives spatial cell width from `units.MaxFootprint`. Setting a huge Dreadnaught collision radius could unexpectedly make **all 10k–20k soldiers' separation much more expensive**. A tiny radius would let soldiers stand inside the visible hull. This must be designed deliberately.

This is a plan, not a demand to copy any exact class/file path. Codex may refine minimal integration after inspecting latest code, especially to protect performance.

## 2. Shape, mounts and visuals

Top-down layout (local forward direction is -Z):

```text
                     FRONT
                      /\
                     /  \
              ______/____\______
             |                  |
             |  FL          FR  |
             |  []          []  |
             |                  |
             |                  |
             |  []          []  |
             |  BL          BR  |
             |__________________|
                      REAR
```

- Broad **beveled rectangular prism**, triangular pointed wedge at nose, chunky low flat silhouette, darkened side faces, faction tinted body and larger proportional ground shadow.
- Four identical simple, small geometric rotating mount + thin barrel assemblies. Allow each to aim independently while the main hull very slowly turns.
- Proposed *visual starting range only*: ~7–9 world units wide and 12–16 long, with substantial thickness; adjust after comparing against Hexagon. These are **not** final approved dimensions. Expose independent hull **width, length, height** in authored data. The existing `SizeMultiplier` clamp is 0.5–3; do not force `SizeTier=5` into it.
- One canonical hull-local outline and four mount offsets should drive visuals, collision, prow contacts, muzzle transforms and painting clearance. Don't let visually large side walls have tiny invisible collisions.
- Ensure the wedge's top cap is valid: current mesh generation's polygon-fan assumptions can be unsuitable for arbitrary concave outlines. A dedicated small reusable Dreadnaught mesh constructor is acceptable; avoid per-soldier procedural mesh creation.
- Reuse faction MultiMesh for hull where feasible. Because only ~2–3 are expected per level, a dozen small turret render nodes/instances across the rare platforms may be acceptable; **never** create separate simulation soldiers or per-projectile nodes.

## 3. Normal nearest-enemy hull pursuit, independent turret combat

- The central Dreadnaught unit uses **ordinary nearest-valid-enemy** seeking/retargeting on the normal existing schedule, choosing a live opposing soldier as the main movement goal.
- It advances very slowly; candidate starting **authored Speed 0.9**, **hull turn rate 40°/s**, low acceleration (balance later).
- The hull does not use turret range as stopping distance. It keeps trying to approach the target into contact, while obeying map bounds, obstacles, navigation, large-body collision and turns.
- Avoid accidentally adding a generic fifth melee swing or generic ranged shot on top of its **four guns plus prow damage**. Use a narrowly scoped compiled unit capability/flag rather than changing normal units' core behaviour.
- When nearest enemy dies, hull retargets according to shared targeting budgets. Independent miniguns continue firing at their own targets while the hull changes heading.
- Normal manual move and K attack-move orders must continue to apply to the **one hull**, and gun tracking should not discard player goals.
- If a nearby valid enemy is behind the hull, its **main body turns gradually** to pursue, but turret barrels may independently rotate to engage threats while the hull turns.

## 4. Four independent turret targeting / firing rules

Each turret slot needs compact immutable authored config plus runtime data: local mount offset, yaw/facing, current target ID, cooldown, min/max range, aim/turn speed, projectile type, damage and shot cadence.

**Target acquisition contract:**

- Search from the **turret's current world position**, not `units.Position[dreadId]` or the Dreadnaught's movement target.
- Choose the **closest valid living enemy whose distance lies between that turret's minimum and maximum range**. A soldier **inside** minimum range is not targetable by that gun; do not merely pick that closest soldier and give up. The turret can find the next closest *eligible* enemy.
- Respect other factions, dead units, LOS/obstacles, muzzle line and self-occlusion by the Dreadnaught's own body. Guns should not fire through the hull to the other side.
- A turret should keep its current live/valid target between bounded acquisitions but eventually switch to a closer target when normal refresh occurs. Reacquire on death/out-of-range with a bounded urgent refresh.
- Turret targets can all be different, all the same, or any combination; do **not** force target distribution.
- **Full 360° acquisition by default**. Barrel aim angle and rotation are independent of hull facing. Require barrel alignment before firing; don't snap instantly across 180° unless explicitly tuned that way.
- Expose **min and max independently per turret**. Shared defaults can initialize all 4, but each must be editable/overrideable. Validate `0 <= min < max`.
- Default firing should continue while moving, provided gun alignment, target validity and physics allow it; no need for hull to stop.

**Spatial query integration:**

`SpatialGrid.FindEnemy` currently centers local search on a **soldier**. Don't modify hull position or temporarily fake a unit to query a turret. Add a small arbitrary-origin nearest-valid local query (support annulus min/max and faction) or a dedicated bounded turret query reusing `SpatialGrid` cells. Do not enumerate every enemy in the map. Look near-first where practicable, with candidate caps and deterministic tie-breaking. If a capped query is truncated, the selected target is closest **among examined valid candidates**, not guaranteed globally closest — expose budget-hit diagnostics and use a sensible limit.

Initially stagger acquisitions ~0.1–0.25 s per turret rather than 4 searches/tick. Cached target validity checks can be cheap. Avoid eight searches for four turrets just to rank possible directions.

## 5. Tiny, fast, frequent projectile model

- Add a dedicated tiny minigun round projectile resource (small rectangular line/bolt), optionally using the existing subtle faction tint shader.
- Suggested provisional stats: **speed 140 world units/s**, **damage 3**, **interval 0.15 s per turret**, **no explosion**, approximately straight travel. Rebalance after gameplay test.
- 4 turrets × 6.67 shots/s = ~**26.7 projectiles/s** per Dreadnaught; 3 platforms ≈**80/s**, excluding all other ranged units. Don't claim that's free: check pooled projectile count and collision cost.
- Introduce a narrow `ProjectileSystem` launch overload taking **explicit world muzzle origin, desired aim, shooter/faction, compiled projectile type, damage and damage type**, while reusing the fixed-capacity projectile pool, spatial swept collision, obstacle blocking, shadow/no-shadow and existing `ProjectileRenderer` MultiMesh. Keep ordinary Triangle/Marksman/Artillery launch logic unchanged.
- Compute muzzle origin from rotated hull + current independent turret yaw/mount and barrel length. Don't fire four projectiles from hull center.
- Extremely fast rounds **must use swept collisions** so they cannot tunnel through light infantry between 60 Hz ticks. Avoid Area3D per bullet, dynamic light per shot or spawning a node per round.
- Ensure correct faction and armour/damage-matrix rules; no friendly fire unless existing projectile rules demand it. Respect projectile capacity, lifetime and dropped-shot counters; don't silently lose most gun output in large battles.
- A subtle flash at each muzzle can be implemented with a bounded pooled/simple visual, never per-shot heavy object allocation.

## 6. Frontal crushing: triangular prow ONLY

Use a true **forward-facing local triangular wedge collision region**, moved/rotated with the hull. The *swept* prow zone in the current movement step hits nearby opposing bodies. Do **not** use a circular radial damage zone or all-around trample.

- Trigger crush **only when the hull actually advances forward** into enemies beyond a minimum real forward speed. No damage while stationary, spinning in place, reversing or merely sliding sideways from separation.
- Use current and previous actual prow positions, hull angle and nearby defender footprint to avoid tunneling; detect fronts accurately during slow turns as well.
- Give heavy **contact damage** to small infantry: proposed first pass **60 damage per qualifying hit/contact**, so a 50-HP Circle can be crushed. This value is editable; use normal `UnitState.Damage` so hit flash/death effects work.
- Add a controlled per-victim hit cooldown / fixed contact cadence (e.g. ~0.4–0.6s); don't apply full 60 damage every 1/60 second for prolonged overlap. Avoid per-hit allocations and arbitrary missed contacts when hundreds of victims line the prow.
- **Sides and rear deal exactly zero damage**, even when other soldiers rub against the hull, when turning, or when being pushed. They may physically separate/shove/block.
- Higher SizeTier units can be harder to shove or more resistant to the crush if defined in the Dreadnaught's tuning rules. Tier classification should not silently rewrite every other unit's health/damage.
- A heavy enemy or wall can physically halt the hull. Don't let it ghost through Hexagons, structures, walls or another Dreadnaught.
- Existing `TrampleSystem` contains useful swept-local-contact techniques but assumes short cavalry charges; implement a **specialized sustained front-contact capability** rather than a copied cavalry charge loop.
- After death, immediately stop guns, movement and contact damage.

## 7. CRITICAL — collision, large footprint, 20k separation performance

**Do not skip this investigation:** The standard `SeparationSystem` computes spatial cell width from **`units.MaxFootprint`**, and ordinary soldiers use circular footprints. A huge circle enclosing a Tier 5 rectangular hull could make separation scans dramatically more expensive for *all 20,000 units*. A small circle could let enemies intersect its side walls.

Before finalizing collision architecture, Codex must compare:

**Approach A — conservative large circle:** Minimal changes but oversized avoidance and potentially substantial global separation cost. Accept only if benchmark data and collision fidelity are reasonable.

**Approach B — rare-large-hull special case (preferred if needed):** Keep normal infantry separation cells stable; represent the Dreadnaught as an oriented rectangular/hull body tested against nearby small circles with a **bounded local broadphase**. Handle body collision separately from **damaging prow sweep**, reuse existing spatial data where safe, and preserve standard capped pushes for everyone else.

Do not blindly rewrite `SeparationSystem` globally. Keep size tier metadata independent. Evaluate sharp turns, front corners, map edge, water, walls, obstruction paths, deployment and navigation clearance. Don't let the long nose rotate through static structures. Heavy platforms shouldn't be shoved aside by one Circle, but shouldn't become invulnerable to massed enemies either.

If there isn't a clean, safe choice in one focused implementation, report the specific alternatives and trade-offs to owner before proceeding.

## 8. Provisional tuning and economics (NOT final balance)

| Property | Proposal |
| --- | --- |
| SizeTier | **5** |
| Hull dimensions | ~8 wide × 14 long, independent of SizeTier |
| Hull HP | ~3,000 |
| Hull speed | 0.9 authored units/s |
| Hull turn speed | ~40°/s |
| Four turrets | Front-left, front-right, back-left, back-right |
| Per-turret minimum range | 3 units initially; editable independently |
| Per-turret maximum range | 30 units initially; editable independently |
| Per-turret fire interval | 0.15 seconds |
| Minigun projectile damage | 3 |
| Minigun projectile speed | 140 units/s |
| Prow contact damage | 60 per qualifying contact, with cooldown |
| Normal player availability | Configurable cap, e.g. 3 per level/faction |
| Production | Very expensive, e.g. 5,000+ Carbon per single unit |
| Production duration | Long, e.g. 90 seconds |

The values above are **implementation starting points**. This platform is a formidable anti-light/siege presence, but sufficiently slow and expensive to be killed by focus fire, concentrated artillery or heavy opposition. The owner sets final costs and balance. Add an appropriate editable `BattleRating` for the army-strength painting estimator; physical Tier 5 is not its BattleRating.

## 9. Production, placement, editor and save compatibility

- Append the unit type `dreadnaught` to `RuntimeUnitCatalog` and `DefaultUnitCatalog.tres` **without renumbering existing byte IDs**. Preserve older deployment/preset/save formats and all existing unit stats. Implement consistent stable-ID resolution.
- Update `UnitDefinition`, unit editor, painter, campaign/level editor and previews for the new hull as necessary. Painter placement/selection must respect actual huge footprint and clear walls/obstacles.
- **Scarcity is configurable** per level/mode (typical 2–3 simultaneously present or queued per faction), not a hard-coded limit in benchmark test mode. Count queued production plus alive units as appropriate; block invalid over-cap purchases *before* Carbon is spent, with clear UI feedback.
- `GAMEPLAY/PRODUCTION/FabricatorRecipes.tres` uses `Vector3i(batchSize, carbonCost, productionSeconds)`. Dreadnaught can be batch size 1, but current standard Fabricator **may have a doorway far too small for a Tier 5 hull**. Do NOT spawn it through a hatch where it overlaps the factory or gets permanently stuck. Check for safe production/deployment alternatives; initial phase can allow editor/benchmark placement while super-heavy production remains gated until validated.
- Keep regular right-click move, straight drag, Right Shift freeform drag, K attack-move, selections/rally behaviour and team ownership. A single hull gets the movement order; turrets remain independently automatic.
- Make both benchmark battles and Fortress Defense use shared Dreadnaught firing/crush logic without duplicating entire systems. Ensure all new fields remain compatible with current entity-definition editing and save/load.

## 10. Performance requirements

- Typical runtime count 0–3 per faction. Use at most **four fixed, independent turret slots per hull**, no extra soldier objects per gun and no node per projectile.
- Reuse shared `SpatialGrid`; stagger nearest turret searches, cap candidates and profile worst-case density. Do not implement full-map scans every 60 Hz step.
- Localize crush scans to the swept prow region. No scan of all soldiers per contact update. Avoid per-shot/per-contact managed allocations, LINQ and individual timers.
- Reuse existing projectile pool and MultiMesh. Track dropped projectiles, active bullets and their update cost. One new projectile definition should not cause unnecessary new texture/shader/material per shot.
- **Do not let 1–3 huge soldiers balloon the ordinary infantry separation grid**. Compare 0 vs 1 vs 3 Dreadnaughts with similar 10k and 20k scenarios; inspect separation ms, candidate pairs, targeting ms, projectile ms, route cost and worst tick.
- Add optional diagnostic counters in the profiler (per-turret targeting queries, limited searches, shots, pooled drop counts, front-crush contacts and milliseconds) without a noisy per-frame UI.
- Per owner preferences, Codex should do source review + at most a basic practical build; user performs heavy Godot/FPS/visual tests unless separately requested.

## 11. Implementation stages when separately approved

1. **Audit latest repo** and confirm data ownership, main hull pursue semantics, 4-mount targeting origin API and collision approach. Flag any architectural issue before significant implementation.
2. **Shape/data:** Dreadnaught resources, stable ID, Tier 5, editable hull dimensions, beveled wedge mesh and four turret visuals.
3. **Hull movement/collision:** normal nearest-target pursuit, slow turn, no weapon-range stopping, safe map/obstacle and large-body separation behaviour.
4. **Turrets/projectiles:** four independent ranges/targets/cooldowns and independently rotating barrels, pooled tiny fast muzzle-origin projectiles and LOS.
5. **Prow:** moving-only swept triangular frontal damage, hit-cooldown, no side/rear damage, low-HP runover, heavy/wall blocking.
6. **Game-mode integration:** benchmark painter, level editor, Fortress Defense commands, configurable scarcity and safe production (no illegal Fabricator hatch spawning).
7. **Review/docs:** update relevant READMEs and `devlog.md`; focused validation and owner test checklist. Avoid unrelated rewrites.

## 12. Acceptance checks (owner in Godot)

- [ ] Huge visually distinct Tier 5 beveled rectangle/wedge hull with exactly four distinct rotating corner miniguns and clear faction tint.
- [ ] Hull seeks **closest valid unit**, keeps slowly approaching, turns slowly, and **does not stop at turret range**.
- [ ] Each turret selects the **nearest eligible target measured from its own mount**, honors its own min/max range and LOS; simultaneous different targets are possible.
- [ ] Rear turret can acquire behind hull independently while body continues toward target ahead; turret barrels aim smoothly without dragging hull heading.
- [ ] Bullets originate from the proper gun, are visibly tiny/rapid, fly very fast with **swept collisions**, and respect projectile pool limits.
- [ ] Only moving **front wedge** damages/crushes enemies; side/rear/stationary/front-turn-only touches do **zero** contact damage.
- [ ] Light Circles can be run over; Hexagons/heavy units and buildings can slow/stop the hull. No sliding sideways through enemies, terrain, water or walls.
- [ ] Global separation stays practical with 20k units and 0/1/3 Dreadnaught comparisons; no runaway spatial-grid width from huge footprint.
- [ ] Dreadnaught is selectable/paintable/saveable and works in relevant battles; existing units, faction palettes, saves, hit/death feedback and combat remain unchanged.
- [ ] Future gameplay scarcity/production cannot take Carbon for an unavailable/capped unit and does not spawn a colossal hull through a tiny Fabricator door.
- [ ] Original move/right-drag/freeform/K attack-move commands still work; new gun targets don't override hull movement goal.

## 13. Exclusions

No side/rear damage, railguns, shields, homing missiles, manually aimed turrets, per-turret HP, building targeting unless separately requested, attack arc restrictions, full tech-tree, individual-bullet nodes or unnecessary new AI. Main behaviour remains **automatic enemy pursuit plus automatic turret fire**.

**Codex handoff:** Treat as a design proposal that may be adapted to current code. The owner must separately authorize implementation. For collision architecture especially, prefer an explicit explanation of alternatives over a silently unsafe shortcut.
