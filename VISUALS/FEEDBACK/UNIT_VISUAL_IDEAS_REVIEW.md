# VIS-IDEAS1 — Unit and Battlefield Visual Ideas (Codex Review Backlog)

**Repository:** `ben0kay/godot-massive-rts` · **Branch:** `main`  
**Status:** Ideas only — **NOT implemented or approved for implementation**  
**Purpose:** Codex should inspect live code, assess feasibility/benefit/cost, then recommend what (if anything) to implement in separately authorized phases.

## Intent

Make dense battles between simple faceted 3D soldiers feel more physical and readable without abandoning the minimalist visual style or the 10,000–25,000-unit performance target. Prefer small, shared, pooled, batched, event-driven effects over individual nodes/physics. Make the battlefield react to impacts and movement without sacrificing performance or gameplay clarity.

**Existing foundations (verify current code):**
- `VISUALS/FEEDBACK/UNIT_HITS/`: white flash and optional size pulse.
- `VISUALS/FEEDBACK/UNIT_DEATHS/`: FX2 cracking/crumbling fragments and fading splats are already **source implemented**; do **not** reimplement or replace them.
- `VISUALS/FEEDBACK/SHOCKWAVES/ShockwaveRenderer.cs`: shared blast rings / explosion bursts.
- `VISUALS/FEEDBACK/UNIT_RECOIL/RangedRecoilSystem.cs`: ranged recoil is already presentation-only.
- `VISUALS/RENDERING/ArmyRenderer.cs`: dirty-updated shared MultiMesh soldier rendering.
- `GAMEPLAY/UNITS/UnitVisualConfig.cs`: optional visual feature flags/configuration.
- `WORLD/WORLD_PAINT/ForestCover.cs`: existing forest visuals and proximity-based tree visibility.
- Projectile systems already have some optional launch/smoke/trail feedback. Avoid duplicating effects.

These ideas are proposals, **not claims of existing implementation**.

## Ideas for evaluation

### 1. Melee contact sparks — high priority, likely low effort
At confirmed melee hits, emit a very short white/yellow glint plus a few warm orange sparks where weapons/bodies connect. Ordinary infantry clashes should be subtle; heavy hits (e.g., Hexagon striking Circle) should be more pronounced. Suggested lifetime **0.1–0.2 s**.

- Candidate approach: a small shared pooled MultiMesh with one emitted event per accepted hit, capped and optionally sampled during mass melee.
- Important: use actual resolved contact/hit events, not proximity scans or an effect on every target query.
- Check: visibility at normal isometric zoom, absence of a solid wall of sparks during 10k combat.
- Avoid: per-hit particle node, light, timer, or physics body.

### 2. Battlefield friction and dust — high priority, low–medium effort
Subtle grey dust along active melee frontlines and from movement/ground contact. Ordinary marching infantry should emit little or none. Dense pushing/clashes create occasional local wisps; cavalry charges and very heavy units leave more noticeable but short-lived dust.

- Candidate approach: sample a **bounded number** of movement/contact/charge events at reduced rate; pooled low-poly translucent or dithered ground-level puffs.
- Reuse existing movement or separation information if cheap; do not add a full-grid scan purely for FX.
- Make emission density depend on *visual activity*, not on the absolute number of nearby units, and cap it to avoid obscuring armies.
- Review terrain height, ground texture/colour and overdraw concerns.

### 3. Directional hit wobble — high priority, likely low effort
On a nonlethal hit, a soldier briefly leans/tilts **away from the impact**, then returns to upright. Proposed visual duration **0.1–0.2 s** and restrained angle (**a few degrees**); heavier/large Size Tiers react less.

- Candidate approach: presentation-only transform perturbation on an active-ID list, integrated with existing hit flash/pulse and `ArmyRenderer` dirty updates.
- Obtain impact direction from the damage event only where readily available; if direction tracking would require intrusive combat plumbing, suggest a simpler stable directional fallback.
- Do not move simulation positions, footprints, hitboxes, facing, targeting, LOS or projectile origins.
- Review interaction with melee lunge, recoil, interpolation, FX2 cracks and ground anchoring; no per-soldier node.

### 4. Heavy-unit ground disturbance — high visual impact, medium effort
When Tier 4–5 units, especially Dreadnaughts, *actually* crush/push soldiers or make heavy contact, emit small ground-level dust/debris ripples or compressed impact rings. Make enormous units feel weighty relative to Circle infantry.

- Candidate approach: hook into **already confirmed** shove/crush/contact events and reuse a compact pooled impact family.
- Prefer a short-lived visual dust impulse tied to movement rather than global proximity tracking.
- No new force, damage, knockback, separation, or collision rules; no extra spatial search just to make the visual.
- Scale by authored size/footprint within strict bounds, not by unbounded Tier multipliers.

### 5. Explosion scars and temporary battlefield wear — medium–high priority, low–medium effort
After artillery/rocket impacts, leave temporary muted ground scorch/crack marks. Stronger explosions make slightly bigger scars, while small rockets leave small marks. Over several minutes, the battlefield should visibly reflect combat.

- Candidate approach: shared pooled flat irregular meshes similar to current ground splats; deterministic rotation/scale, fade after about **10–30 s** (tunable).
- Only certain actual explosion/impact events create marks; do not place one for every projectile or damage recipient.
- Respect terrain Y, layer with existing death splats, avoid z-fighting/terrain penetration where possible.
- No terrain mesh deformation or permanent decals; strict global cap and optional low-frequency updates.
- Audit current `ShockwaveRenderer` and splash-damage visuals to avoid doubling the same event.

### 6. Projectile near-miss and ground-strike detail — medium priority, medium effort
Occasionally show small dust strikes or tiny glints where Dreadnaught minigun rounds and Marksman lasers miss or strike ground; near misses can add the sensation of intense suppressive fire.

- Prefer **ground impacts already detected** by the projectile system. A near-miss test that scans neighbouring soldiers for each bullet would likely be too expensive and should be rejected unless it can use existing collision results with negligible incremental cost.
- Sample only a low percentage of eligible projectile events, with a tight global emission limit.
- Preserve bullet trajectories, damage, hit validation and pooled projectile caps.
- Do not confuse visual glints with confirmed damage.

### 7. Injured armour wear/chipped facets — optional, medium cost/risk
Extend FX2's existing persistent crack look with slight local darkened/chipped-looking zones or reduced saturation at very low health. This should feel like fractured metal or ceramic under faction paint, not glowing lightning or a material swap.

- First evaluate whether the shipped FX2 shared crack mask already gives enough readability.
- If useful, prefer a **single lightweight shared material/shader enhancement** using the existing per-instance damage stage; avoid geometry changes/material instances per soldier.
- Preserve faction colours and white-hit flashes. Avoid introducing expensive new per-pixel noise, overdraw, or per-unit update streams.
- **Do not** rebuild FX2 or change its death-fragment system as part of this idea.

### 8. Forest canopy response to moving armies — optional, medium effort
A subtle lean/sway of nearby vegetation as troops push through, returning naturally afterward. Large units can cause a little more response than ordinary soldiers. No tree rigid bodies.

- Candidate owner: `WORLD/WORLD_PAINT/ForestCover.cs` or existing forest shader/batching, **not** the soldier renderer.
- Review how the current forest code samples nearby units and fades trees; avoid a second full scan of living soldiers.
- Keep deterministic low-amplitude sway; no gameplay effect on forest cover, move speed, visibility or collisions.
- Respect the existing forest bounds/instance approach and GPU overhead.

## Cross-cutting vision: responsive battlefield atmosphere

The strongest result would be a battlefield that starts visually quiet and gradually looks *used* as fighting intensifies: light dust at packed frontlines, brief melee sparks, chipped/damaged units, heavier contact effects for giant hulls, and temporary blast scars. These should **emerge from real combat events**, not rely on expensive ambient simulations.

Keep layering restrained: soldiers, aiming, projectiles and formations must remain readable at normal gameplay zoom. Do not make every attack visually spectacular.

## Initial prioritization (for review, not implementation)

| Idea | Visual benefit | Estimated effort | Recommended stance |
| --- | --- | --- | --- |
| Melee contact sparks | High | Low | Prototype first |
| Frontline/charge dust | High | Low–medium | Prototype early |
| Directional hit wobble | High | Low–medium | Prototype early |
| Large-unit ground disturbance | High | Medium | After dust/contact foundations |
| Explosion ground scars | Medium–high | Low–medium | Good independent pass |
| Projectile near misses | Medium | Medium/high under dense fire | Only if existing impact data supports cheaply |
| Extra armour wear | Medium | Medium | Compare with current FX2 first |
| Forest response | Medium | Medium | Later optional polish |

These are subjective estimates. Codex should replace them with source-grounded assessments; no FPS claims have been measured.

## Shared technical requirements

1. **Presentation only.** No combat balance, AI, target acquisition, projectile trajectories, navigation, collision or economy changes.
2. **MultiMesh and bounded pools by default.** No `Node3D`, `RigidBody3D`, light, timer or particle-system instance per soldier, hit, tree or projectile.
3. **Event-driven, sampled, and capped.** Minimize CPU writes and GPU overdraw, especially in 10k–25k unit battles. Drop cosmetic events cleanly when budget is full; never queue a backlog.
4. **Independent feature controls.** Design optional toggles/config carefully, ideally using existing `UnitVisualConfig` or established settings conventions, without bloating one resource with unrelated options.
5. **Mode consistency.** Shared combat effects should work in Benchmark, AutoBattler and Fortress Defense as appropriate; forest effects only where forests exist.
6. **Coexist with shipped effects.** Preserve FX2 cracks/crumbling, white hit flash/pulse, ranged recoil, shockwaves, splats, muzzle smoke, optional projectile visuals and ground shadows.
7. **Terrain-aware, zero-gameplay-impact transforms.** Any dust/scars/impacts should use cheap available terrain data; transient visual wobble must not affect authoritative units.
8. **Avoid unnecessary scans/allocations.** Use existing contact/hit/projectile events, active lists, preallocated buffers and deterministic seeds. No new all-unit pass per frame.

## Suggested review phases for Codex

- **Review A — Existing-effect inventory:** Identify overlapping features and exact event sources; explicitly note FX2 is already implemented.
- **Review B — Cheap visual prototypes:** Recommend whether melee sparks, directional hit wobble, and dust can be implemented as independent small opt-in passes.
- **Review C — Environmental polish:** Evaluate temporary explosion scars and heavy-unit ground disturbance using shared pooled effects.
- **Review D — Optional extras:** Only pursue projectile near-misses, extra armour wear, or forest motion if cost/benefit justifies them.

## What to return before implementation

For each idea, Codex should report:
- Whether any of it already exists.
- Exact files/event hooks it would use.
- Smallest viable rendering/pooling approach and approximate resource budget.
- Risk to 10k–25k battles (CPU, GPU, overdraw, dirty updates, allocations).
- User-facing tuning/toggle placement.
- A recommendation: **implement**, **prototype**, **defer**, or **reject**.

Then propose a ranked phased implementation plan with acceptance criteria and owner-visible A/B toggles. **Do not implement, edit combat logic, add empty folders or commit code as part of reviewing this document.** Individual implementations require separate owner approval. Follow `CodePreferences.md`.
