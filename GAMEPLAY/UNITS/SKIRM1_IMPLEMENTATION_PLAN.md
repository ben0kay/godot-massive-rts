# SKIRM1 — Skirmisher: mobile ranged unit with budgeted retreat AI

**Status:** Planned — proposal for Codex to inspect, refine and implement **only when separately authorized**.  
**Repository:** `ben0kay/godot-massive-rts` · **Branch:** `main`  
**Owner intent:** Player-selectable, fragile, evasive alternative to Triangle Archer. **Always shoots the closest valid enemy**, and automatically retreats from threats that get too close, including threats approaching from behind or the sides. Uses a distinctive non-letter geometric silhouette.

## 1. Review first; do not implement from this documentation commit

Read root `README.md`, `CodePreferences.md`, current `GAMEPLAY/UNITS/README.md` and active source; read `AGENTS.md` if present. This plan is a handoff and starting architecture, **not a frozen specification or approval to code**. Before implementation, tell the owner if current design differs, suggest any better performance-friendly integration and get separate implementation approval. Preserve existing mechanics, other units, benchmark baselines, saves and performance instrumentation. When subsequently approved, follow owner preference for a focused single commit and lightweight source/build validation.

### Verified code as of 2026-11-17

- `GAMEPLAY/UNITS/DEFINITIONS/TriangleArcher.tres` is authoritative: **Health 25, authored Speed 4.5, Damage 9, attack Interval 1.25 s, Reach 32**, 300°/s turn rate, shared LaserBolt projectile.
- `GAMEPLAY/UNITS/DEFINITIONS/UnitDefinition.cs`: authored stats and `UnitShape`; `GAMEPLAY/UNITS/CATALOG/UnitCatalog.cs`: compiled per-type `UnitStats`, ordered type IDs; currently **Pikeman at index 10**. Append Skirmisher, do not renumber existing runtime IDs. Deployment files store stable type strings.
- `GAMEPLAY/COMBAT/CombatSystem.cs`: `Acquire` picks targets on a staggered cadence; the benchmark's **Ranged target variety** deliberately gives some existing ranged units 2nd/3rd rank targets. `Step` handles aim, live target, range, line-of-sight, cooldown and projectile firing.
- `GAMEPLAY/SIMULATION/SpatialGrid.cs`: `FindEnemy(... rank: 1)` provides candidate-bounded local nearest-enemy queries; `GetCandidates(position, radius, faction)` provides an allocation-free enemy cursor. **Nearest is approximate if a query candidate budget is reached**.
- `GAMEPLAY/SIMULATION/MovementSystem.cs`: ranged units approach until they have a clear shot and normally stop; moves use speed/acceleration, facing, obstacle clipping, ground marking and voluntary-movement flags.
- `GAMEPLAY/SIMULATION/SEPARATION/SeparationSystem.cs`: independent footprint-weighted capped separation pass **after movement**, including allied melee-versus-ranged yielding that checks whether a ranged unit is moving.
- `BENCHMARK/BenchmarkArena.cs` uses combat acquisition → movement → separation → attacks, on its existing update cadence.
- `GAMEPLAY/SIMULATION/COMMANDS/CommandMovement.cs` and `GAMEPLAY/WAVES/FortressWaves.cs` own command orders and fighting for Fortress Defense. **This is not the same movement path as the benchmark**, so plan both integrations.
- `VISUALS/RENDERING/FacetedSoldierMeshes.cs` batches prebuilt meshes per type; reuse it.

## 2. Unit gameplay and starter balance (tentative, all editable)

| Attribute | Existing Triangle Archer | Proposed Skirmisher |
|---|---:|---:|
| Health | 25 | **18** |
| Authored speed | 4.5 | **7.0** |
| Damage per hit | 9 | **6** |
| Attack interval | 1.25 s | **1.8 s** |
| Maximum firing range | 32 world units | **26 world units** |
| Start retreat when enemy closer than | — | **8 world units** |
| Stop retreat when no enemy within | — | **12 world units** |
| Fire during active retreat | — | **No** |
| Armour class | Light | **Light** |

Author-defined movement speed still uses the project's compiled movement multiplier. Skirmisher's theoretical uninterrupted damage is about **3.33 DPS**, versus Archer's **7.2 DPS**, even before actual retreat downtime. This unit should be faster and survive by mobility, **not** out-DPS an Archer. Existing Diamond cavalry's authored speed 9 exceeds proposed Skirmisher speed 7; kiting must not grant guaranteed invulnerability.

Prefer `MinimumFireRange = 0` initially so a cornered Skirmisher can shoot at close enemies when not actively retreating. **MinimumFireRange is not the retreat threshold.**

## 3. Behaviour and closest-enemy targeting contract

**Firing target:** Always use the closest valid **living opposing soldier** in firing reach, respecting line-of-sight and the current battle's target validity/order constraints. **Explicitly exempt Skirmisher from benchmark Ranged target variety**, whether that option is on or off. Leave Archer and other ranged units' target-variety behaviour intact. Use the existing staggered acquisition timing and bounded candidate search, not a per-tick global nearest scan. Do not assert global nearest correctness when a candidate cap truncates; document or report budget hits.

**Retreat threat:** Separate from the firing target. Inspect local enemies from **all 360 degrees** in the retreat safety area, regardless of who is being shot or which way the soldier faces. Examples:
- Enemy in front only: retreat away.
- Enemy behind only, while firing at another enemy in front: retreat away from rear threat; when safe, aim at and shoot the closest valid opponent.
- Enemies in front and behind, or on multiple sides: choose a locally safer sideways/diagonal escape if one exists. Don't blindly run directly away from the current firing target.
- Completely surrounded: unit may be trapped and killed, as intended.

**Modes:**
1. **Approach** current nearest firing target when outside firing range and not threatened.
2. **Hold/fire** once in a clear firing position, using normal projectile and facing rules.
3. **Retreat** when any credible enemy is within 8 world units, moving toward a locally safer position. Suppress firing while retreating; cooldown continues counting down. Leave retreat only when safely beyond the 12-unit threshold; reorient/aim and then shoot.

Use **hysteresis** (8 start / 12 stop) to prevent rapid on/off oscillation. Avoid constant orbiting and infinite running when there is no danger. If all threats are dead/disappear, eventually clear retreat and return to ordinary combat movement.

Target acquisition is **independent** of threat selection: do not change `units.Target[id]` merely because a different enemy forced retreat. When acquisition next refreshes, the shot target can change to the closest enemy, including a new threat behind.

## 4. Performance design: 20k-soldier goal

Propose a small `GAMEPLAY/SIMULATION/KITING/SkirmishSystem.cs` (or another existing appropriate owner) to **assess local threats and cache retreat state**, not a replacement physics/pathfinding system.

- Kiting is **opt-in** via editable `UnitDefinition` values such as `SkirmishEnabled`, `RetreatStartRange`, `RetreatStopRange`, `ThreatRefreshSeconds`, `ThreatCandidateLimit`, and `CanFireWhileRetreating` (false for SKIRM1). Compile per-type immutable stats into `UnitStats`. Avoid unit ID string comparisons in hot loops.
- Per-soldier runtime state should use small indexed arrays (retreat-active byte/flag, cached XZ retreat direction, next reassessment tick), preallocated/lazy-allocated **once**, not Godot nodes or managed objects per soldier.
- **Threat queries around 5 Hz (0.2 s)** by default, staggered by stable soldier ID; continue movement at the existing 60 Hz step using cached direction. If that feels sluggish around fast cavalry, test an optional faster reaction or cheap target-distance urgency path before simply multiplying scans.
- Reuse `SpatialGrid.GetCandidates` or a more appropriate bounded local nearest-first query. Initial candidate budget **24–32**. Important: raw cell-cursor iteration order can exhaust a cap before reaching the *nearest* threat in dense fights. Audit ordering and choose a cheap bounded closest-first or urgently-close strategy; do not accidentally let dangerous enemies go unnoticed merely because their IDs appear later in the cursor. Record truncation/budget hits.
- Compute safe heading with cheap weighted separation from threatening enemies or **up to eight fixed heading scores evaluated from the same candidate data**. Do not perform eight new enemy searches or eight route calculations.
- Only check local map bounds/obstacle clearance for promising directions; pick deterministic tie-breaking and reuse the currently safe heading when reasonable to avoid jitter.
- No full-army enemy scans, full pathfinding per unit/tick, LINQ, temporary lists per update, allocation per soldier per tick, additional spatial index, or per-soldier Timer/Node.
- For **2,000 skirmishers × 5 decisions/s × 32 candidates** the maximum examined-candidate count is about **320,000/s**, excluding grid traversal, evaluation and any overflow handling. This is a **work estimate, not a benchmark result**. Include optional profiler counters (decisions, active retreats, inspected candidates, budget hits, Skirmish CPU ms) in existing performance UI.

## 5. Movement, obstacles, separation, and firing

- Feed retreat **direction/state** into the current movement pass, **before** its normal `SeparationSystem.Step`. Do **not** directly move positions in an extra pass after separation. Existing movement speed, acceleration, facing/turning, wall clipping, map bounds, terrain grounding and `Dirty` render updates must remain authoritative.
- Normal/Strong separation still apply. Skirmishers use their compiled circular footprint. Mark a retreating Skirmisher as **voluntarily moving ranged** so allied yielding doesn't treat it as a stationary Archer. Friendly soldiers can block escape; never phase through crowds or structures.
- On very tight corners, blocked walls or surrounded positions, stop or make a minimal safe adjustment and retry later. Don't teleport, repeatedly slam into a wall or build global escape routes. Keep existing navigation/obstacle rules for normal approach.
- Suppress ranged firing **only for actively retreating Skirmishers** inside `CombatSystem.Step`, with no effect on Archer, Marksman and artillery. Continue advancing cooldown normally; after retreat ends, existing `SoldierFacing.Aim` must realign before firing. Avoid facing tug-of-war between retreat heading and shooting.
- Ensure threat assessment and movement still run when there is no valid shooting target; a Skirmisher shouldn't stand still while an enemy sneaks behind it just because the cached fire target is invalid. Respect the existing `MovementSystem` early-return conditions.

**Fortress Defense / Command Sandbox integration:** `CommandMovement` handles explicit unit orders, while `FortressWaves` handles acquisition and threats. Reuse the same threat-assessment policy while letting that mode's movement owner apply retreat. Proposed priority: **explicit normal move commands and Fabricator gate/exit movements outrank kiting**; during autonomous combat or **attack-move**, threat retreat temporarily takes precedence, then resume the original commanded destination after safety returns. Do not replace the persistent command goal with the temporary retreat goal; don't break K + left-drag attack-move, ordinary right-click/right-drag, Right Shift freeform formations, rallies or selection. If a conflict with current order semantics is discovered, surface it for owner approval.

## 6. Visuals, data, production and compatibility

- Give Skirmisher a **recognizable minimal geometric silhouette**, not a letter or detailed humanoid. A swept/dart-like polygon is favored, but Codex may adapt. Audit the existing `UnitOutline` and mesh-cap construction: a truly concave crescent can create incorrect top-face triangulation with the current single-fan method. Prefer a safe convex wedge or simple stylized polygon if necessary; consult owner about large visual deviations.
- Reuse prebuilt faceted/beveled per-type meshes, faction MultiMesh batching and existing LaserBolt projectile at first. No extra per-instance mesh/node/shader mechanics required.
- Create stable `skirmisher` ID and `Skirmisher.tres`; **append** `UnitShape` and catalogue registration/index. Make painter, Entity Editor and Fabricator dynamic catalogue/recipe sources aware of the new unit; avoid inventing a parallel registry. Give it an editable Fabricator recipe, subject to owner economy balancing.
- Preserve authored IDs, save formats and legacy deploy/preset loading. Keep generated benchmark preset compositions unchanged by default so current benchmark comparisons remain comparable.

## 7. Staged implementation when the owner authorizes it

1. Re-check latest files; present any suggested plan refinements and unresolved corner cases to owner.
2. Add silhouette, `Skirmisher.tres`, catalogue/definition data and existing painter/editor/production availability **without changing old type indices**.
3. Build optional cached, staggered, bounded threat assessment and retreat-state mechanism.
4. Integrate **benchmark** kiting into `MovementSystem`, normal separation, nearest shooting and fire suppression. Add diagnostics if useful.
5. Integrate **Fortress Defense / Command Sandbox**, following current command priority and attack-move behaviour.
6. Update relevant docs, one focused commit, and lightweight source/build validation. Let owner do full visual/FPS testing.

### Acceptance checks (owner validation)

- [ ] Triangle Archer unchanged: range, DPS, stationary firing and target variety.
- [ ] Skirmisher shoots **closest valid enemy** even with Ranged target variety ON; respects LOS, cooldown, faction and target death.
- [ ] Threats behind, to side and front are noticed independently of shooting target; a pincer prompts sideways avoidance when space allows.
- [ ] Start at 8 u, release at 12 u: no constant threshold flicker; no shooting during active retreat; turning works after retreat.
- [ ] Normal/Strong separation, allied melee yielding, obstacle collision, terrain grounding and map boundaries work, including dense crowds/traps.
- [ ] Fast cavalry can catch Skirmishers; no guaranteed escape or infinite range.
- [ ] Benchmark and Fortress Defense paths both work; K attack-move resumes its goal, ordinary move orders and Fabricator exits are not hijacked.
- [ ] Painter, Entity Editor, Fabricator and saved deployments/presets handle stable `skirmisher` ID, without disturbing existing units.
- [ ] Compare no Skirmishers to 500/2,000/5,000 Skirmishers with comparable 10k/20k army setups. Inspect CPU targeting, Skirmish ms, separation, movement, candidate budget hits and worst frames. **Do not claim a measured FPS improvement without a controlled benchmark.**

## 8. Exclusions for first version

No moving-shots/strafe fire; no squad-level coordinated retreats, morale, stamina, special anti-cavalry modifiers, flocking or perfect crowd escapes. No unbounded worldwide safety query, no per-soldier Godot nodes, and no redesign of the Triangle Archer or other range units.

**Codex must treat this file as a proposal:** inspect/refine architecture as needed, explain trade-offs, and await an explicit request to implement.
