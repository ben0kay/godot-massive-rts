# DREAD1 — Tier 5 Dreadnaught Super-Heavy Assault Platform

**Status:** Planned — Codex implementation handoff; **this document does not implement gameplay code**.  
**Repository:** `ben0kay/godot-massive-rts` · **Branch:** `main` · **Engine:** Godot 4 .NET/C#  
**Unit display name:** **Dreadnaught** (preserve the owner's spelling) · **Stable unit ID:** `dreadnaught` · **Size Tier:** **5**

## 1. Objective and immutable gameplay rules

Create a rare, extremely expensive, very large geometric assault unit: a long rectangular hull with a pointed triangular front and four independent rapid-fire minigun turrets at **front-left, front-right, back-left and back-right**. The Dreadnaught feels like a slowly advancing land battleship in an auto-battler with thousands of smaller units.

**Owner decisions to preserve:**

1. **One unit:** one authoritative soldier ID, one main health pool, one faction, one hull position and heading. Four turrets are **weapons**, not four separate soldier entities.
2. **Hull movement:** uses ordinary auto-attack nearest-enemy seeking and pursuit, consistent with other units. **Slow movement and very slow hull turning.** It does **not** stop just because its miniguns are firing.
3. **Independent turrets:** each finds the **closest valid enemy measured from its own current muzzle/turret position**, with its **own minimum range and maximum range**, cooldown and aim direction. The four turrets can shoot different enemies—or the same closest enemy if individually appropriate. All-around targeting for DREAD1; do not impose arbitrary left/right firing quadrants.
4. **Projectiles:** each minigun spits very small, fast, frequent visible projectiles, with low individual damage and the standard pooled projectile collision rules.
5. **Frontal crushing:** the **pointed front** deals substantial physical/contact damage as the hull advances into enemies, especially small fragile Circle infantry. **The sides and rear do no contact damage** in DREAD1. A turret hitting someone at the side is ranged damage, not hull contact damage.
6. **Tier 5:** use existing `SizeTier = 5` (current Hexagon is Tier 3). Tier classifies physical scale; it must **not** silently change `SizeMultiplier` or the older unit meshes.
7. **Rarity:** intended maximum **2–3 Dreadnaughts per level**, at great production/deployment cost. Prefer an authored **per-level/per-faction cap** rather than hard-coding a universal magic limit that breaks editor/debug scenarios.
8. **Default auto-battle:** no manual weapon controls, stance system, special defensive AI, shields, or player micro required.

Review source and adjust technical design if necessary, **without weakening these behaviours**. Explain substantial trade-offs before implementation.

## 2. Read and audit first

Read `README.md`, `CodePreferences.md`, `GAMEPLAY/UNITS/README.md`, the relevant combat/simulation/rendering/projectile/production documentation, and `AGENTS.md` if it exists. Inspect the current main branch—source may have changed after this plan.

Primary source:

- `GAMEPLAY/UNITS/DEFINITIONS/UnitDefinition.cs` and `GAMEPLAY/UNITS/CATALOG/UnitCatalog.cs`
- `GAMEPLAY/UNITS/DEFINITIONS/HexagonSiege.tres`, `ChevronArtillery.tres` and `DefaultUnitCatalog.tres`
- `GAMEPLAY/SIMULATION/UnitState.cs`, `SpatialGrid.cs`, `MovementSystem.cs`, `SEPARATION/SeparationSystem.cs` and `SoldierFacing.cs`
- `GAMEPLAY/COMBAT/CombatSystem.cs`, `COMBAT/TRAMPLE/TrampleSystem.cs`, `AreaDamage.cs`
- `GAMEPLAY/PROJECTILES/Shared/ProjectileDefinition.cs`, `ProjectileCatalog.cs`, `ProjectileSystem.cs`, `ProjectileRenderer.cs`
- `VISUALS/RENDERING/FacetedSoldierMeshes.cs`, `ArmyRenderer.cs`, shadow/feedback code
- `BENCHMARK/BenchmarkArena.cs`, benchmark panel and metrics
- `GAMEPLAY/SIMULATION/COMMANDS/CommandMovement.cs`, `GAMEPLAY/WAVES/FortressWaves.cs`, player orders
- Deployment/painting, Entity Editor, Fabricator `GAMEPLAY/PRODUCTION` recipes, campaign/level editor save/unlock infrastructure.

**Relevant findings as of 2026-11-19:**

- `SizeTier` exists independently from `SizeMultiplier`; Hexagon is already authored Tier 3.
- Existing `UnitState.Target[id]`, `CombatSystem.Acquire` and `MovementSystem` are built around one primary combat target per soldier. The existing `CombatSystem.Step` ranged code supports a **single projectile origin per firing unit**. **Do not overwrite `units.Target[id]` with turret targets.**
- `SpatialGrid.FindEnemy` is a soldier-centred, candidate-budgeted local search, while `GetCandidates(center, radius, faction)` can search around an **arbitrary world-space point**. A turret-local query must measure from the *turret*, not from the hull centre. A raw cursor is not sorted by closeness, so an arbitrary 32-candidate cut-off may not truly pick the nearest.
- `ProjectileSystem` is pooled and `ProjectileRenderer` uses per-projectile-type MultiMesh batches; its normal `Fire(shooter, victim, units)` launches from a **single unit-centred origin** (apart from Chevron special-casing) and reads attack stats from the firing unit. This must be extended safely for per-turret muzzle position, damage/shot speed and aim, without disrupting regular arrows/artillery.
- `SeparationSystem` and existing `UnitOutline.Footprint` use **circular collision footprints**. A long, wide hull with an angular ram cannot be accurately represented by a circular footprint alone. Plan deliberate hull clearance and front crushing without changing the O(n) / bounded separation assumptions for everyone else.
- Cavalry `TrampleSystem` is a **run-up/charge mechanic** with hit limits and momentum; it does not directly implement steady moving front-only contact.
- The benchmark and Fortress Defense use different movement/order ownership. Source must work in both if Dreadnaught is available in both.

## 3. Hull geometry, size and visuals

### Main silhouette

- Low, broad, **elongated rectangle** viewed isometrically/top-down, with an integrated **pointed triangular prow on the front (-local Z)**.
- Substantial height and visible bevels, solid faction colours, darker sides, large fake ground shadow. No detailed realistic tank/wheel animations.
- **Four visibly distinct angular turrets** at front-left (FL), front-right (FR), back-left (BL) and back-right (BR), with thin short barrels.
- Turret base remains anchored to the moving/turning hull; turret body/barrel independently rotates around local vertical axis toward its target. When idle, hold last orientation or return to a stable default without jitter.
- Ensure projected placement outline, selection highlights, hit/death visual effects and terrain grounding read correctly.

### Geometry implementation

- Add new appended `UnitShape` variant and a lightweight custom `UnitOutline`/mesh path for rectangle + prow (or a similarly inexpensive compiled compound mesh).
- One cached hull mesh per unit type, not dynamic meshes per soldier.
- For the expected few Dreadnaughts, four light per-instance turret visuals can be acceptable, or use small batched turret meshes if simpler within existing code. **Do not create heavy scene graphs or new physics bodies for every ordinary soldier.**
- Do not let the standard `SizeMultiplier` hard cap (currently 3×) accidentally limit this design to a merely Hexagon-sized shape: use appropriate **base width/length proportions**, with Tier 5 metadata. Avoid enlarging unrelated units.
- Keep faction tint, low-poly faceting, optional fake shadow, white hit flash and dying effects consistent with the rest of the game.

### Important physical footprint audit

The circular separation radius may be far larger than the physical distance from hull to its **front nose**, preventing a prow from ever contacting a small enemy; or may be too small and allow soldiers to visibly occupy the Dreadnaught's side.

**Codex must choose and justify a bounded, size-aware plan:**
- Preserve existing fast circular broadphase for ordinary units.
- Use a Dreadnaught-specific oriented hull/ram **narrow-phase** for front impact.
- Ensure friendly/enemy soldiers, structures and walls cannot unintentionally pass through its hull.
- Avoid making its extra-wide circle create excessive crowd separation scans, cell sizes or route-clearance problems for 10k/20k soldiers. Audit `MaxFootprint` usage, navigation, deployment clearance, and solver grid sizing.
- Prefer a targeted heavy-unit footprint solution over rewriting every soldier into full polygon collision. If a simpler approximation is chosen initially, **document limitations** rather than letting frontal damage trigger at the sides.

## 4. Main hull target and movement

Use the ordinary target-acquisition/pursuit system to select the **closest enemy unit** for the hull's destination, subject to normal acquisition budgets and refresh cadence. Respect existing faction/health/obstacle/guard semantics.

- The Dreadnaught advances slowly toward the main target **even when all four guns are already in range**.
- Apply slow authored speed, acceleration and low `TurnDegreesPerSecond`. No instant snap-to-target.
- Treat main hull targeting as a **movement goal**, not a fifth minigun. Avoid accidentally applying the normal ranged stand-off/stop logic.
- If compiled as `AttackStyle.Melee`, prevent extra ordinary melee strikes from stacking with frontal crush; if compiled as `Ranged`, explicitly prevent the normal ranged halt and single-target auto-shot. Design a small explicit special attack/movement policy rather than relying on a conflicting style flag.
- Support target death and reacquisition. If the closest enemy is to the rear, slowly turn to approach; no backward teleporting or unbounded instantaneous rotation.
- Ordinary map boundary, obstacle blocking, movement orders and separation should remain in control. Avoid turning the Dreadnaught into a special unit that ignores route safety.
- Preserve commanded normal move and attack-move semantics in Fortress Defense (turrets can still auto-fire at nearby valid enemies where command/combat rules permit).

## 5. Four independent turret combat controllers

Implement a small **`DreadnaughtTurretSystem`** (suggested name/location `GAMEPLAY/COMBAT/DREADNAUGHT/`, adapt to current project organization) that operates on a *small indexed set of live Dreadnaught IDs*, not the entire army every time.

Each Dreadnaught has **four** compact turret states, e.g.:

| Turret index | Mount | Independent state |
| --- | --- | --- |
| 0 | Front left | Target ID, current local yaw, cooldown, next search tick |
| 1 | Front right | Target ID, current local yaw, cooldown, next search tick |
| 2 | Back left | Target ID, current local yaw, cooldown, next search tick |
| 3 | Back right | Target ID, current local yaw, cooldown, next search tick |

- Mount offsets are authored in **hull-local X/Z units**, compiled or derived from dimensions; rotate/translate them into world space each tick.
- Each turret has **editable minimum and maximum range**, measured from its own **world-space mount/muzzle**. Initially permit one common default with per-turret overrides; ensure the four can truly differ.
- Choose **nearest valid living enemy** within inclusive maximum and outside minimum range. Ignore allies, dead enemies and blocked shots where appropriate.
- Do not tie turret targets to `UnitState.Target[hullId]`, and do not use the benchmark `Ranged target variety` logic for turret target selection.
- If the closest enemy is inside the minimum range, choose the closest **eligible** enemy outside that radius (do not lock onto the invalid closer target).
- Each turret may choose the same target or separate targets; no enforced quadrant arcs in DREAD1. **360° rotation is allowed**; turret model should physically turn to face its target.
- Update targets using a **staggered, bounded** query cadence. Validate cached target health, range and sight cheaply between full reacquisitions; do not run four full searches per Dreadnaught per 60 Hz tick.
- Use `SpatialGrid.GetCandidates` around each **mount point**, or a new small query using existing spatial cells. **Correctness concern:** the raw cursor iterates cells, not nearest-distance order. Under a candidate cap choose a sensible closest-first cell search or explicitly report approximation and prioritize nearest cells. Preserve performance budgets.
- Use individual aiming yaw/turn speed, separate cooldowns, independent muzzle world positions, and clear/hold invalid targets sensibly. Handle death/despawn and reset state without dangling IDs.
- **All four turrets operate while the hull is moving.** They don't force hull rotation or stop the hull.
- Expose min/max range, projectile damage, firing cadence, turret aim speed, muzzle offsets and target refresh rate as editable data. A dedicated `DreadnaughtDefinition` / `TurretDefinition` resource may be cleaner than adding dozens of unrelated inspector fields to every soldier; choose the least invasive well-supported design. No per-soldier polymorphic object allocation in the hot loop.

## 6. Minigun projectiles and firing rate

- New lightweight `DreadnaughtMinigun` projectile resource or equivalent using current pooled projectile system.
- Very small, bright, fast **non-explosive** bolts, preferably using the existing rectangular laser styling / faction tint pattern. No new particle nodes per shot.
- Shots originate **at the actual turret muzzle**, not hull centre. Add a minimal pooled `FireFromMuzzle` / shot override API (or equivalent) accepting source soldier/faction, muzzle origin, target aim, damage and projectile-type reference. Preserve existing `Fire` and `FireAtStructure` behaviour; avoid hardcoding special origin logic inside the ordinary single-gun path.
- No unlimited allocations; respect existing projectile cap, swept-hit tests, obstacles and team damage rules. Track dropped shots so the 4-gun platform doesn't silently starve normal Archer/Marksman projectiles.
- Use **independent turret cooldowns** and stagger startup phases so all four don't always launch on the exact same tick unless that naturally happens.
- Independently target closest enemies but projectile collisions still hit the **first valid soldier** along the travelling segment, as existing hitscan/sweep rules dictate.
- Avoid cheating by applying hits directly at range if a real tiny projectile was requested. Fast bullets still require robust swept collision to prevent tunnelling.

**Starter numbers (illustrative; tune after owner tests):**

| Property | Initial proposal |
| --- | ---: |
| Hull Size Tier | **5** |
| Hull health | **3,000** |
| Authored speed | **1.1 u/s** |
| Hull turn rate | **35°/s** |
| Turrets | **4** |
| Turret minimum range | **3 u** |
| Turret maximum range | **24 u** |
| Individual minigun shot damage | **3** |
| Firing interval per turret | **0.15 s** |
| Projectile speed | **110 u/s** |
| Turret target refresh | **0.20–0.35 s**, staggered |
| Frontal crush per fresh victim/contact | **60** (tunable) |
| Initial per-level availability target | **2–3** |

The numbers are **not approved balance settings**, only safe first-pass starting points. At 0.15 s, all four turrets of one Dreadnaught can fire about **27 shots/sec**; three could produce about **80 shots/sec**, plus other battlefield projectiles. Confirm actual resource speed scaling and projectile lifetime/capacity before choosing values. Damage, firing interval and range must be tuneable without recompiling.

## 7. Front-only physical crushing

The Dreadnaught's **triangular pointed nose** must deal heavy damage to enemies when it physically moves into them. **No side or rear hull damage, even if close enough to overlap a broadphase circle.**

Do **not** implement the crush as a radial shockwave or generic proximity aura.

Suggested separate `DreadnaughtCrushSystem` (or integrated with the small Dreadnaught component):

1. Detect each Dreadnaught's **actual forward hull displacement** for the current simulation tick.
2. Build a **swept, forward-facing wedge / nose-zone** between previous and new hull poses (correct even during slow turning). The broadphase may use existing nearby enemy spatial cells.
3. Apply narrow-phase **directional contact geometry** at the pointed front only, using enemy footprints and movement padding. Do not count soldiers alongside the hull, behind it, or outside the swept nose.
4. Apply substantial per-contact damage using the existing damage matrix and `UnitState.Damage` so white-hit/death feedback works.
5. Prevent the same victim from taking crush damage **every 1/60th second merely because it remains touching the nose**. Track bounded per-victim contact/exit or cooldown state, resetting appropriately when the victim separates or dies. Distinguish a fresh contact from continued overlap.
6. Circle/other small weak infantry should be readily killed/cleared if struck; heavier soldiers should resist/require additional hits. This may be accomplished initially through hit damage vs ordinary HP and collision mass, or later with a *size-tier* modifier. **Do not automatically let a Tier 5 unit one-shot every Tier 1 unit without considering actual HP and balance.**
7. On blocked contact with an uncrushable/heavy unit, stop, push within the existing capped separation, or go around. Do **not** pass through surviving units without collision handling.
8. Use existing wall/boundary clipping and separation, including the fact that the unit can be stopped; do not allow crush damage to occur on the far side of an obstacle.
9. No damaging stationary side walls; **front crush only while actually moving forward**. Reverse/sideways lateral drift caused by separation is not a new crush attack.
10. Consider at most a modest number of **new** contacts per tick per Dreadnaught; use spatial broadphase and cap/record over-budget behaviour. Avoid n² soldier scans.

The existing `TrampleSystem` is purpose-built for **temporary momentum charges** and is not an exact fit. Reuse its contact/sweep math and bounded hit handling selectively, but **do not force the Dreadnaught to launch or dash like cavalry**.

## 8. Separation, obstacles and oversized hull routing

This is a high-risk integration area and must be explicitly designed rather than left for incidental testing.

- The current single circular footprint is usable as a **broadphase** but not a faithful exact collision boundary for a long rectangular hull and wedge.
- Audit how `MaxFootprint` affects separation-cell width, neighbour budget, spatial queries and navigation for **every other unit**. A single huge radius can inflate work for thousands of regular soldiers.
- Avoid global regressions from changing baseline separation cell dimensions, pair budgets or collision formulas.
- Prefer **special-cased heavy-unit local narrowphase** against nearby units and obstacles while keeping normal soldier separation unchanged. Evaluate whether bounded compound proxy circles, a capsule-like approximation or an oriented polygon/rectangle would be simplest. Reconcile this with UnitState's single authoritative position.
- Nearby infantry should be pushed aside/around the hull within existing caps, not sit halfway inside it indefinitely, teleport, or receive damage from passive side overlap.
- Never damage a soldier merely because standard separation moved it near a side. Never apply front crush because the entire unit's bounding radius overlaps.
- Verify painted defenses, rivers/impassable terrain, walls, map edges, chokepoints, target pursuit, grounding and spawn placement at realistic hull dimensions.
- A Dreadnaught may be unable to fit a narrow passage: safe blocking/rerouting is better than clipping.
- Heavy unit should remain reasonably stable when many tiny soldiers push against it; avoid hundreds of Circles visibly shoving Tier 5 away.

## 9. Both battle modes, catalog, saves and economy

### Benchmark / painted deployment

- Add `Dreadnaught.tres` with stable ID `dreadnaught`; **append** to catalogue ordered IDs and `UnitShape` enum to preserve all existing IDs/numeric values.
- Set `SizeTier = 5`; add suitable editable `BattleRating` for army strength estimator (proposed placeholder only).
- Make it discoverable/selectable in benchmark deployment painting, selection displays, unit editor and preview without changing seeded baseline benchmark compositions.
- Preserve existing saved deployment formats and old maps/presets. New saves store the stable ID; do not reinterpret prior indices.
- Validate painting footprint and warning/handling of oversized placements and normal shared collision clearance.

### Fortress Defense / Command Sandbox

- Support the same four-turret system and front crush with regular `FortressWaves` and `CommandMovement`, using the same behaviour rules.
- Normal move, attack-move, rally, Fabricator spawn/exit, user selection and battle orders should not erase turret state or inadvertently halt hull movement.
- Make it available through Fabricator production once the economy/recipe can support its cost. **Avoid assuming an ordinary single-unit-sized hatch can release the Dreadnaught.** Verify spawn clearance and larger exit area; if not feasible without a special production building, document a safe alternate production approach and ask owner before broad building changes.
- Set a very high configurable Carbon cost and long build time; suggested batch size **1**. Do not hardcode price until owner reviews economy. Do not alter other production recipes.
- Define a per-level/per-faction Dreadnaught population cap (initial default **3**, optionally **2** for curated maps). Apply to production/deployment as appropriate; define whether cap counts living units, queued units and preplaced enemies. Allow explicit editor/debug overrides where needed.
- Campaign/editor should show the Dreadnaught and its cost/rarity where relevant; avoid changing older level save semantics.

## 10. Instrumentation, performance and memory

Target remains the project's existing **10k–20k+ soldier** benchmark; Dreadnaughts are rare and should not cause widespread system overhead.

- Maintain compact arrays indexed by active Dreadnaught owner ID (4 turret slots each) and a small active-ID list. No nested per-tick allocations, LINQ, timers per gun, global pathfinding or full army scans.
- Reuse current spatial index, bounded searches and pooled projectile system. Do not rebuild spatial grids separately for four turrets.
- Stagger 4-gun target refreshes across ticks; when target dies/reaches invalid range, revalidate/reacquire within reasonable latency.
- Bounds checks, deterministic tie-breaking for equidistant targets, safe cleanup on unit death/reset, support for mid-battle spawning, no stale-target reuse after reset.
- Optional benchmark metrics: Dreadnaught count, 4-turret target queries and candidate caps, shots fired/dropped, front crush candidates/hits, Dreadnaught CPU milliseconds, projectile milliseconds, separation candidate/budget impact.
- Confirm turret rotation and muzzle transforms do not require expensive new render uploads for all existing soldier meshes.
- Keep ammunition visuals cheap: tiny geometric MultiMesh bullets, limited muzzle flashes, no particle explosion per bullet.
- Inspect worst-frame/99th percentile and density around Dreadnaughts; **owner performs heavy Godot/FPS acceptance tests**.

## 11. Suggested implementation sequence

When Codex is separately instructed to implement DREAD1:

1. **Audit & report** how the current standard melee/ranged target fields, projectile origin, collision footprint, navigation and production will interact with this unit; recommend the least invasive architecture and flag blockers.
2. **Hull data & shape:** append type ID/shape, Tier 5 resource, editable balance, low-poly hull and four turret mounts. Add painter/editor visibility.
3. **Movement:** keep main hull nearest-enemy seeking, very slow turns, continuous forward pursuit regardless of turret shots. Ensure no duplicate standard melee/ranged attack.
4. **Turret controller:** four independently cached nearest-valid target searches with independent min/max ranges, aiming, cooldowns and transforms.
5. **Projectile integration:** pooled fast tiny bolts from **per-turret muzzle positions**, with existing collision/LOS, source-faction and damage handling.
6. **Frontal crush:** swept nose contacts only while advancing; bounded, non-duplicated damage events and safe interactions with separation and walls.
7. **Integration:** benchmark, Fortress Defense/Command Sandbox, editor/paint/save/production and configurable scarcity. Fix large-hull passage and Fabricator-spawn issues without affecting normal soldiers.
8. **Docs & handoff:** update unit/projectile/combat docs and `devlog.md`, inspect diffs, run one basic build if readily available, then leave heavy performance and visual acceptance to owner. Prefer one focused implementation commit.

## 12. Acceptance checklist (owner to validate in Godot)

- [ ] Dreadnaught appears as **large pointed rectangular hull** with **four clearly distinct corner guns** and is visibly Tier 5 compared with Tier 3 Hexagon.
- [ ] Main hull acquires nearest enemy and **continues moving toward it while guns fire**; turning is deliberately slow.
- [ ] All four turrets have independent minimum/maximum range, targets, cooldowns, direction and muzzle origins; nearest valid is measured **from the respective turret**.
- [ ] Enemy too near one gun's minimum does not disable other guns; same enemy can be chosen independently by multiple guns.
- [ ] Threats on left, right, front and behind can all be shot by their nearest qualifying turret; turrets can fire simultaneously.
- [ ] Projectiles are visibly tiny, fast and frequent but pooled/batched; regular projectile collision, LOS and other units' firing remain unchanged.
- [ ] **Front-only** moving nose crush damages/kills Circle infantry effectively; **side and rear** overlap cause **no contact damage**.
- [ ] Stationary hull does not repeatedly deal front crush; each victim is not damaged 60 times per second while continuously overlapping.
- [ ] Heavy soldiers, walls and tight corridors provide realistic resistance; smaller infantry cannot shove the hull implausibly.
- [ ] Normal/Strong separation, target acquisition budgets, obstacles/grounding, death feedback and multi-faction rules work.
- [ ] Painter, saved levels, benchmark, Fortress Defense, player orders, production, spawn clearance and scarcity cap behave correctly.
- [ ] Compare 0, 1, 2 and 3 Dreadnaughts among 10k–20k soldiers; check projectiles, targeting, separation and worst-frame metrics without assuming perfect 60 FPS.

### Explicitly out of scope

No 5th central turret; no side/rear ram damage; no independent turret health; no explosive ammunition, ammo/reload inventory, shield systems, deployable cover, stances, directional armor, special boss AI, or player-controlled gun targeting. These are potential later iterations **only if the owner requests them**.

**Codex instruction:** Preserve the owner's gameplay decisions; you may adapt the technical architecture after source review. Do not implement this merely because the plan file was added to the repository.
