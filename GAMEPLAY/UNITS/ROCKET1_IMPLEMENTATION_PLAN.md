# ROCKET1 — Wide Deep-Fork Twin-Rocket Launcher

**Status:** Source implemented. Godot gameplay and visual validation pending.
**Repository:** `ben0kay/godot-massive-rts`  
**Branch:** `main` · **Engine:** Godot 4 C#  
**Working name:** Twin Rocket Launcher (owner has not finalized the display name).  
**Suggested stable ID:** `twin_rocket_launcher` — confirm with owner before implementation if the name changes.  
**Suggested physical SizeTier:** 2 (provisional; 1.5 also acceptable). SizeTier is a label and must NOT automatically scale geometry.

## 0. Goal and owner-approved design

Add a new **standard auto-attacking ranged unit**, visually defined by an **abstract, extra-wide deep-fork silhouette**. It fires **two alternating high-arc rockets** with a noticeably **smaller splash radius than existing Chevron Artillery**.

Non-negotiable design:
- **Wide deep fork, not an H:** two long, thick, angular forward prongs separated by a **deep visible central notch**, joined by a broad solid rear base. All edges straight and faceted. Avoid rounded pods, vehicle-like bodywork, an H crossbar across the opening, a regular chevron, or a real-world launcher.
- **One small rocket gun mounted on each prong**, aligned with the forward direction; each barrel and launch flash visibly belongs to its own side.
- **ONE shared firing target and ONE attack cooldown for the unit.** Normal auto-acquire: seek/approach the **nearest valid enemy**, subject to current engine acquisition/budget/line-of-sight rules. **Do not build independent turret searches** (unlike Dreadnaught).
- **Alternating actual launches:** left → right → left → right, one rocket per normal attack interval. Not two simultaneous rockets and not two shots per cooldown.
- Projectile trajectories arc through the air like artillery, with **small, frequent local explosions**. Use the existing projectile pool, swept collision, terrain/obstacle impact, and **`AreaDamage.Radial`** splash code. Do not introduce another splash-damage algorithm.
- Existing Chevron Artillery, H Breacher and Dreadnaught are not redesigned; no general autonomous retreat, deploy/stance AI, or defensive mode.

This file authorizes planning/documentation only. **Codex must inspect the latest repository and adapt details as needed before implementing, and implement only on a separate explicit request from the owner.**

## 1. Read first and re-check the live architecture

Read `README.md`, `CodePreferences.md`, relevant feature READMEs and any `AGENTS.md` if present.

Review current source, especially:
- `GAMEPLAY/UNITS/DEFINITIONS/UnitDefinition.cs`, `ChevronArtillery.tres` and `HBreacher.tres`
- `GAMEPLAY/UNITS/CATALOG/UnitCatalog.cs`, `DefaultUnitCatalog.tres`
- `VISUALS/RENDERING/FacetedSoldierMeshes.cs`, `ArmyRenderer.cs` and placement-preview renderer
- `GAMEPLAY/COMBAT/CombatSystem.cs`
- `GAMEPLAY/SIMULATION/MovementSystem.cs`, `SpatialGrid.cs`, `UnitState.cs`, `SoldierFacing.cs`, `SEPARATION/SeparationSystem.cs`
- `GAMEPLAY/PROJECTILES/Shared/ProjectileDefinition.cs`, `ProjectileSystem.cs`, `ProjectileCatalog.cs`, `ProjectileRenderer.cs`
- `GAMEPLAY/PROJECTILES/Types/ArtilleryShell/ArtilleryShell.tres`
- `BENCHMARK/BenchmarkArena.cs`, `GAMEPLAY/WAVES/FortressWaves.cs`, `GAMEPLAY/SIMULATION/COMMANDS/CommandMovement.cs`
- Unit painter, Entity Editor, Level Editor, Fabricator recipes and mode-specific spawn / combat owners.

**Verified at plan creation on 2026-11-20:**
1. Chevron Artillery's authored stats: range 70, **minimum range 12**, attack interval 4.5 s, damage 80; `ArtilleryShell.tres` uses indirect fire, maximum arc height 24, and splash radius **7 world units** with 0.3125 edge damage fraction.
2. The existing `ProjectileSystem.LaunchFrom(muzzle, aim, faction, projectileType, damage, damageType)` already supports explicit shot origins and returns a bool indicating pool acceptance. It already calculates a curved flight and `ImpactShell` calls `AreaDamage.Radial` and the shockwave ring. **Use it** for two alternating muzzle positions.
3. `CombatSystem.Acquire` / `CombatSystem.Step` provide reusable normal ranged targeting, cooldown, facing, LOS and firing. `MovementSystem` handles ranged approach/stand-off. Reuse these; only minimal unit-specific alternate-origin logic should be new.
4. `UnitOutline` has some custom concave/compound shapes (`HFrame`, `Pike`). `FacetedSoldierMeshes.AppendPrism` currently uses a center-fan top/bottom triangulation for custom shapes: a **deep concave notch can be accidentally filled or triangulated incorrectly**. Do not just add fork vertices and rely on an invalid center fan.
5. The catalogue uses existing stable IDs in a dense byte mapping (currently Dreadnaught index 11). **Append any new ID/index; never renumber** existing types or change save identifiers. New unit's projectile definition needs registration as usual.
6. `UnitVisualConfig.tres` offers an optional enhanced-mesh switch; normal unit silhouette must work with the switch ON or OFF. Do not make this feature dependent on enhanced meshes.
7. The existing separation solver uses a **circular footprint** derived from `UnitOutline`. Be mindful that a U/fork silhouette includes empty space in its notch; choose a conservative footprint and don't globally expand grid cell size unnecessarily.

## 2. Shape — EXTRA-WIDE DEEP FORK

The distinctive geometry is the priority. This should look like an abstract symbol as bold as the H Breacher, **but not like an H**.

Top view, local forward = **-Z** (two prongs point forward/up):

```text
                     FRONT (-Z)
                  __       __
                 /  \     /  \
                /    |   |    \
               |     |   |     |
               |     |   |     |
               |     |   |     |
               |     |___|     |
                \             /
                 |___________|
                     REAR (+Z)
```

The diagram communicates the negative-space notch, not a literal pixel-perfect outline. Author a clean, symmetric polygon with **sharp corners, a broad rear bridge and two separate forward points**.

Proposed shape guidelines, adjustable after owner visual testing:
- Overall **width: ~1.3–1.5× the earlier narrow Deep Fork idea**, giving the two prongs more visual separation. This is a **relative art goal**, not an instruction to enlarge a pre-existing game unit.
- Aspect ratio: **wider than tall**, with width roughly 1.15–1.4× front-to-rear length.
- Deep central opening extends ~**50–65% of body length** from the forward edge toward the rear. Avoid a shallow V nick.
- Distinct two-prong silhouette at both orthographic and isometric zoom; prong thickness substantial enough to display compact individual guns.
- Rear connects the prongs through a **solid base**, leaving the front negative space truly open. No crossbar near the tips and no added front connection that turns it into an H.
- Beveled sides, slightly lighter top, darker side faces, faction color as with existing units. Optional very small line/panel accent on each prong, but no realistic rocket vehicle, wheels or tracks.
- No circular/rounded major shape. Avoid overlapping with the Chevron silhouette and Pike/H Breacher when zoomed out.
- **One discrete short launcher/barrel on each prong** rather than two tubes in the body center. Tubes can be simple rectangular/low-poly prisms embedded or raised slightly, with visible separation and no realistic machinery.

**Critical geometric requirement:** ensure all top/bottom/side faces are properly triangulated and consistently wound for the concave notch. Implement a suitable small custom triangulation for this outline (e.g., fixed safe triangles or a purpose-built convex decomposition) if the generic fan is invalid. Do not globally rewrite other units' mesh generation. Preserve placement-preview fidelity, footprint scaling, facing convention and fake shadow readability.

## 3. Alternating two rocket launch origins

The unit should still have **one current target (`units.Target[id]`) and one normal attack cooldown (`units.Cooldown[id]`)**.

At each eligible firing event:
1. Validate live target, distance within min/max range, LOS/indirect rules, aim/orientation, attack cooldown and any current attack-move restrictions using the shared ranged combat path.
2. Read a lightweight **next-side state** (left or right) for this unit ID; initial state is **left**. Use a byte array/bitset keyed by soldier ID, or another compact mechanism appropriate to current `UnitState`. Avoid a separate Node/Timer per unit.
3. Compute launcher muzzle in **local hull space** (e.g. `±right * forkWidthFraction` plus a forward offset at the tips and a small local Y height), then rotate/translate using the unit's **current facing**. Reuse the same offset for projectile visuals and gameplay origin.
4. Call `ProjectileSystem.LaunchFrom(...)` with the rocket's compiled projectile type, source faction, damage after normal category/damage-type rules, and the target's **ground-space position at launch**.
5. If the pool accepted the rocket, toggle `left ↔ right` and commit the normal attack cooldown. If full, **do not advance the next-side state**. Match the game's existing behavior for a failed projectile launch without causing rapid retry storms.
6. If target dies before firing, re-acquire normally. Do not toggle sides just because an attack was attempted.
7. Reset next-side state when units/battle are recreated; no stale state after death, new wave, map reload or restart.

**Both launchers fire at the same unit's currently acquired target.** They do not have independent targets/ranges/cooldowns and do not fire together. The main body faces its target as per existing ranged aim, rather than freely traversing two turrets.

When projectile firing is indirect, preserve current obstacle semantics: projectile arcs may pass *over* walls where the standard artillery flight does; do not let them tunnel through an obstacle at impact.

## 4. Rocket projectile and shared splash damage

Create a **new** `ProjectileDefinition` resource at a suitable path, e.g.:

`GAMEPLAY/PROJECTILES/Types/TwinRocket/TwinRocket.tres`

- Small crisp angular rocket / dart visual, faction-readable and visibly smaller than Chevron artillery shell. Avoid oversized orange sphere if it obscures the two distinct launch origins. Optional minimal launch flash if cheap; no particle nodes per projectile.
- High enough arc to be visibly indirect but **lower/shorter than Chevron's heavy artillery shell** at a comparable distance, subject to visual evaluation.
- **`ObstacleFire = IndirectFire`**, `ExplodesOnImpact = true`, appropriate `BlocksOnObstacles` for the shared collision rules.
- **ExplosionRadius = ~2.5 world units** as an initial proposal, much smaller than Chevron's 7; expose it on this rocket resource.
- Use the existing explosion edge falloff (initial ~0.30–0.40, tweakable) and existing shockwave/impact ring at the rocket radius, **without adding a second splash solver**.
- Keep normal target/category damage, faction filtering, armour and `UnitDamagePercent` behavior. Avoid double damage from direct projectile impact plus its area hit unless consistent with current artillery.
- Ensure 2D XZ radius testing, elevation/terrain interception, obstacles and lifetime handling are identical to standard explosive artillery.
- Do not mutate `ArtilleryShell.tres`; this new rocket owns its independently tunable splash radius and arc.

### Provisional starting values — NOT locked balance

| Property | Proposed value | Rationale |
| --- | ---: | --- |
| Physical SizeTier | **2** | Compact medium artillery; not Dreadnaught-scale |
| Health | **90** | Vulnerable if melee reaches it |
| Authored speed | **3.2** | Mobile compared with heavy artillery |
| Damage per rocket | **12** | Lighter than Chevron's 80 |
| One-rocket attack interval | **0.75 s** | Alternating left/right at 1.33 rockets/s total |
| Max attack range | **35 u** | Medium range |
| Minimum attack range | **5 u** | Vulnerable to close attackers |
| Rocket blast radius | **2.5 u** | Substantially less than Chevron's 7 |
| Rocket edge fraction | **0.35** | Same shared falloff mechanism |
| Rocket arc cap | **~12 u** | Lower than Chevron's 24; tune against distance |
| Turn speed | **~140°/s** | Modest |
| Suggested body dimensions | **~1.7×1.3 u** | Editable, fit painter/formation |
| Starting muzzle side | **Left** | Deterministic |

These are estimates for Codex to propose/validate, not current game values; leave appropriate fields editable in the unit/projectile resources.

## 5. Runtime design and performance

Keep everything consistent with high soldier counts (10k–25k goal):
- Ordinary spatial target acquisition, attack decisions, ranged movement, separation, collision and projectile stepping already exist. Reuse rather than duplicate.
- **No two new enemy searches** per rocket and no separate launcher AI. This is a **single-target, alternating-muzzle** ranged weapon, not a 2-turret Dreadnaught.
- Precompute immutable left/right local muzzle offsets per unit type or derive from compiled outline, then transform from facing only on shot. Allocate no new vectors/arrays/scene nodes per frame or per shot (value-type Godot `Vector3` operations are fine).
- Reuse the existing fixed-capacity `ProjectileSystem`, `ProjectileRenderer`, compiled projectile type, spatial collision queries, `AreaDamage.Radial`, and shared explosion ring. Do not add another pool, physics body or per-projectile Node.
- One rocket per cooldown means effective total rocket count is about `N / 0.75` per second at uninterrupted firing; verify pool capacity, `Dropped` counters and hit/arc costs under load.
- Do not increase the shared separation grid cell size or global max footprint just to represent empty notch space if it materially affects 20k-unit performance. If existing circular footprint makes a visible gameplay compromise, document it and propose a bounded mitigation.
- Keep standard faction projectile coloring, hit flashes, deaths and battle performance counters.

## 6. Catalogue, modes and editor/production integration

When implementation is separately approved:
- Add a named new `UnitShape` at the **end** of the enum (no renumbering), plus `UnitOutline` / mesh implementation and `TwinRocketLauncher.tres`.
- Append stable `twin_rocket_launcher` to the end of `RuntimeUnitCatalog`'s ordered indices; update default catalogue and backwards-compatible loading so old saved maps/campaign layouts still load. Keep Dreadnaught and existing unit IDs stable.
- Use standard `AttackStyle.Ranged`, projectile definition and authored `MinimumFireRange`; no secondary AI targeting system.
- Make selectable/paintable in **Game Editor benchmark painter**, **Entity Editor** (including shape preview) and any relevant level spawning/selection path. Inspect which are catalogue-driven; do not hand-code unnecessary duplicated lists.
- Add a balanced editable **Fabricator recipe** only if this unit should be available in Fortress Defense; inspect the current recipe required-for-every-combat-unit behavior. Do not silently change existing recipes.
- Ensure **Benchmark** and **Fortress Defense / Command Sandbox** both call the same alternating-origin fire logic. Different combat owners should not accidentally bypass it or fire from the center. Verify attack-move, normal auto-targeting, range visualization and obstacle handling.
- Preserve deterministic presets/baseline army composition for comparable performance. New unit should initially be paintable rather than replacing existing preset units.

## 7. Suggested implementation sequence

1. Re-check code and confirm the final display name and size category if the owner has made further decisions.
2. Add `TwinRocketLauncher.tres`, new projectile `TwinRocket.tres`, stable catalogue registration and editor-facing parameters.
3. Build the **wide deep-fork outline and correctly triangulated beveled mesh**. Show the placement preview from above before spending time on effects.
4. Add left/right muzzle-origin computation and tiny per-unit alternation state; integrate into current combat shot execution and the shared `LaunchFrom` API.
5. Confirm the existing artillery impact path provides the only splash resolution, small ring, terrain and LOS behavior.
6. Wire both game modes/painter/production as appropriate, document controls and tuning, record the implementation in `devlog.md`.
7. Follow `CodePreferences.md`'s one focused commit and lightweight validation policy; leave extended FPS/gameplay experiments to owner.

## 8. Owner test checklist

- [ ] Visible unit is a **WIDE, DEEP FORK**, not an H, chevron or ordinary vehicle; fork gap stays visibly open in 3D/isometric/placement preview.
- [ ] Each prong carries **exactly one launcher**, left and right; barrel origins match on-screen rocket spawns.
- [ ] One enemy: left rocket fires, **next cooldown** right rocket fires, then left, then right.
- [ ] Rocket launch attempts rejected by full projectile pool do not silently flip the side.
- [ ] Firing and moving only use ordinary target acquisition and ranged AI, with no new rocket-specific search system.
- [ ] Minimum range is respected: too-close enemies cannot be fired upon; nearest **eligible** alternative target is considered if available.
- [ ] Splash radius is **much smaller** than Chevron artillery and uses **the same area-damage and impact ring code**, with correct armor/damage faction filtering.
- [ ] Arcs land correctly over terrain and into walls, do not visually start from center, and handle dead targets and shot expiry.
- [ ] Separation, collision footprint, orientation, turn speed, attack cooldown and faction colors work for blue/red and other factions.
- [ ] Unit appears in painting/editor/eligible Fabricator catalogs and old saves still load, with existing byte indices unchanged.
- [ ] Compare against old Chevron alone, then mixed armies at 1k/10k/20k; review projectile pool drops, CPU projectile ms, collision counts, separation and targeting. Report **measured** costs only.

## 9. Explicit non-goals

No new splash solver; no homing missiles, lock-on tracking, manual aiming, twin independent targeting, per-tube cooldowns, two-at-once volleys, advanced turret articulation, rocket barrage special ability, defensive stances, or heavyweight render particles in ROCKET1.

**Codex instruction:** This is a design/implementation plan only. Inspect the current source, adapt where appropriate and obtain a separate explicit implementation request.
