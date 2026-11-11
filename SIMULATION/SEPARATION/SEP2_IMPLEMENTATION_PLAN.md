# SEP2 — Allied Melee Pass-Through / Archer Yielding

**Repo:** `ben0kay/godot-massive-rts` · **Branch:** `main` · **Status:** Planned (not implemented)
**Owner:** `SIMULATION/SEPARATION/` · **Depends on:** implemented SEP1 and UNIT0

## Goal

Allow **any advancing allied melee soldier**, including the **basic Circle infantry**, to work through a stationary block of friendly ranged infantry (currently Triangle Archers) on the way to the enemy front line. A small amount of visual interpenetration is preferable to melee units getting stuck. **SEP2 MUST ONLY CHANGE SAME-FACTION INTERACTIONS.** Enemy-vs-enemy-faction overlaps, blocking and movement must behave exactly as SEP1 already does.

The owner explicitly accepts **50% overlap of nominal combined footprint clearance** for this one interaction and prefers that standing archers only move a tiny bit, rather than being shoved far away. SEP2 is not a full crowd-navigation or heavy-unit mass simulation.

### Exact pair rules (strict)

| Pair | Rule |
| --- | --- |
| Moving Circle (basic melee) + stationary **allied** Triangle (ranged) | SEP2 relaxed clearance + tiny archer yield |
| Moving Square (strong melee) + stationary allied Triangle | Same |
| Moving Diamond (melee cavalry) + stationary allied Triangle | Same |
| Moving Pentagon (elite melee) + stationary allied Triangle | Same |
| Moving melee + stationary **enemy** Triangle | **Unchanged SEP1**, full separation; no ghosting |
| Any other friendly pair (including melee–melee, ranged–ranged, moving archer, stopped melee) | **Unchanged SEP1** |
| All opposite-faction pairs | **Unchanged SEP1**, without exceptions |

Implement eligibility using **roles**, not just Triangle's hardcoded type index, so future ranged definitions can also yield. Require `units.Faction[idA] == units.Faction[idB]` **before** testing for the SEP2 exception. Make the eligibility condition simple, explicit, symmetric in pair visitation order and easily reviewable.

## Clarify the 50% setting

Use the runtime `UnitStats.Footprint` radii of the two soldiers:

- Existing SEP1 desired centre distance: `fullLimit = radiusA + radiusB + gap` (with current SEP1 slop as applicable).
- **SEP2 eligible pair** desired centre distance: `relaxedLimit = (radiusA + radiusB) * 0.50f`.
- This means the **minimum required centre-to-centre clearance is half** of the nominal radius sum. It does **not** mean exactly 50% area overlap between drawn polygons or a percent of their heights.
- Do not permanently shrink a soldier's stored `Footprint` or authored `.tres` value; other pairs, attacks, deployment clearance and projectile hits continue to use the original footprint.
- If a melee soldier stops advancing, the exception should switch back to normal SEP1 distance (smoothly limited by existing per-tick caps, never snap/teleport). Avoid oscillation between advancing and stopped states; a small hysteresis/grace if proven necessary is acceptable only if inexpensive and localized.

**Suggested starting knobs:** `AlliedRangedClearanceFraction = 0.50f` (clamp 0.1–1), `AlliedRangedYieldPairCap = 0.08f` world units of **total pair correction per solver pass**, `AlliedRangedYieldShare = 0.85f` (archer receives 85%, approaching melee receives 15%). These values are recommended initial tuning, **not** a requirement to add a new general per-unit mass/weight mechanic. If a tiny correction does not effectively unblock melee, adjust these knobs, but never alter enemy pair handling.

## Minimal implementation strategy

### 1. Capture genuine movement intent

`SIMULATION/MovementSystem.cs` currently moves toward a target and skips soldiers at stop distance. Add a **reused, compact per-soldier intent flag** (`bool[]`, `byte[]`, or equivalent) indicating that movement logic **actually attempted a positive melee travel this tick**. Clear/reset flags when the soldier is dead, has no target, is already in reach, or does not attempt to move. Do this inside the existing movement loop with **no new per-soldier object allocation**.

Do **not** infer intent from `UnitState.Dirty`: hits, white flashes, and SEP1 position corrections also make units dirty. Do **not** classify an archer nudged by the solver as actively walking; the flag represents voluntary movement before SEP1/SEP2 pushes.

The `Movement` benchmark may keep the same intent semantics, but the desired case must work in real Melee/Ranged **combat** with active pursuit and stationary shooting archers. Avoid changing rendering-only mode.

### 2. Reuse the current SEP1 counting-sort grid

`SIMULATION/SEPARATION/SeparationSystem.cs` already enumerates each candidate pair once using a cell-ordered forward stencil and shared buffers. **Do not create a second spatial grid, another per-soldier neighbour scan, a pathfinder or physics bodies.**

- Determine whether the pair is eligible for SEP2 based on cached flag, `Faction` and `UnitStats.Ranged` (one moving melee, one nonmoving allied ranged).
- Carry the small necessary information into the current slot-order arrays or lookup by `slotId`; no per-candidate resource loads or managed allocations.
- In the existing pair resolver only, substitute the **relaxed distance threshold** when eligible; otherwise run the **unchanged SEP1** threshold and equal-split correction logic.
- For eligible pairs only, bias the push toward the stationary archer and cap the total pair correction to a **small** `AlliedRangedYieldPairCap`. Example: correction 0.08 max, 85% to archer (~0.068), 15% to moving melee (~0.012), rather than 0.5/0.5. Allow remaining penetration up to the relaxed threshold to resolve over successive ticks.
- Preserve existing per-unit combined tick cap, 1-pass Normal / 2-pass Strong, damping, deterministic coincident fallback, grid padding, budget sampling and dead-unit exclusion.
- For the special pair, only use positive penetration relative to the **relaxed** limit; skip correction when within accepted 50% clearance. Consequently most slight archer/melee overlaps require **no push at all**, reducing actual corrections.
- Do not alter `UnitStats.AttackReach`, ally/enemy targeting, authoring dimensions, projectile hit footprints, or saved deployments.

### 3. Make tuning and optional comparison simple

Place the three small SEP2 tuning fields under an `Allied crowd yielding` subsection of existing `BENCHMARK/CONFIG/BenchmarkConfig.cs` or other appropriate shared config, with sensible Inspector ranges/clamps. Recommended default is **enabled whenever SEP1 is Normal or Strong**. SEP1 **Off** must remain exactly the old no-separation baseline.

Optionally add a single live checkbox **`Allied archer yielding`** to the existing BENCH0 panel for A/B comparison, provided it stays simple and does not change SEP1 Off/Normal/Strong. Do not introduce another separation mode or new big UI panel.

### 4. Instrument with negligible overhead

Keep SEP1's existing separation CPU milliseconds, candidate counts, overlap counts, budget hits and penetration summary. Optionally count **SEP2 eligible pairs** and **actual yielded corrections** while resolving pairs; do not do another O(N) measurement pass just to display statistics. The owner will measure real FPS and crowd behaviour.

## Critical behaviour and edge cases

- If melee moves into **friendly archer** ranks, it can occupy some of the archers' space while advancing; archers shift only a little. **No enemy soldier gets relaxed clearance under any circumstance.**
- Standing archers keep shooting and do not acquire melee movement intent merely because they were pushed sideways.
- Once melee stops voluntarily moving (e.g. reaches attack distance), return to regular SEP1 separation through the same limited solver passes. No abrupt leap, oscillatory push-back or repeated stop/restart if possible.
- If two archers overlap, or two melee overlap, unchanged SEP1 separation still applies. Two moving units do not automatically get pass-through; the exception is **moving melee versus stationary ranged** on the same team.
- An archers-only blockade may still slow moving soldiers where there are many layers; **this is a local crowd-yield system, not a guarantee of pathfinding through arbitrary impenetrable formations**. Avoid adding costly sideways steering or global paths without a separate task.
- Zero-distance pairs remain deterministic, all corrections finite, stationary dead soldiers do not block anyone, and mass reset / clear does not leak stale intent flags.
- Refresh only the existing renderer dirty soldiers as needed. Keep projectile-hit broadphase padding safe by **not exceeding the existing SEP1 maximum per-tick displacement cap**.
- Use the same behaviour for all five authored types, but do not add a generic `PushWeight` stat or other unrelated mass/charge mechanics. The larger Pentagon may still take more visual space through its original footprint, but SEP2's eligibility and percentage rule do not privilege its size.

## Files Codex should review or modify (minimal scope)

- `SIMULATION/SEPARATION/SeparationSystem.cs`: conditional same-faction relaxed pair resolution and reused intent state.
- `SIMULATION/MovementSystem.cs`: capture pre-separation voluntary movement.
- `BENCHMARK/BenchmarkArena.cs`: pass intent and tuning to solver; preserve movement → SEP1 separation → combat/projectile tick sequence.
- `BENCHMARK/CONFIG/BenchmarkConfig.cs`: simple exported tuning knobs.
- `BENCHMARK/UI/BenchmarkPanel.cs`: only if a live enable/disable comparison checkbox is worthwhile.
- `SIMULATION/README.md`, relevant BENCHMARK docs, root `README.md`: describe distinction between SEP1 and SEP2.
- `devlog.md`: **append one short entry only after SEP2 is actually implemented**, in the implementation commit, not the plan commit.

**Do not modify** `UNITS/DEFINITIONS/*.tres`, combat balance, projectiles, current unit geometry, pathfinding, deployment save format, or unrelated performance instrumentation.

## Owner acceptance checks

1. Painted **friendly Circle behind a wall of stationary friendly Triangle Archers**, with enemy troops ahead: Circle advances through the archers more easily than in SEP1, and archers are moved only subtly.
2. Repeat for Square, Diamond, Pentagon. Their original movement speeds remain unchanged.
3. Paint **enemy Archers** as the blockade and repeat: no special overlap/low-resistance rule; normal SEP1 contact applies.
4. Friendly archer–archer, melee–melee, and two moving soldiers still use normal SEP1 separation.
5. Compare SEP1 Off / Normal / Strong and optional SEP2 checkbox on same seed and deployment. Use 1k, 5k, 10k and up to **20k total**; inspect FPS, separation CPU, cap hits and movement. Larger BENCH0 counts remain available but are not FPS targets.
6. Archers still shoot, attackers still damage targets, projectiles hit, white flash/rendering remains correct; loading/clearing/redeploying remains correct; no errors/NaNs/dead-body obstruction.
7. Check units immediately after reaching their target: no visible jitter or dramatic snap when regular SEP1 clearance returns.

## Implementation/validation policy for Codex

Read `README.md`, `NOTES/CodePreferences.md`, this plan, SEP1 plan, and the **latest** touched code first. Keep the existing optimized SEP1 counting-sort grid and low-allocation loops. Implement SEP2 only. Run a brief source/diff review and **at most one readily available `dotnet build`**; leave repeated, high-count Godot testing to the owner. Update the relevant documentation and append one brief implementation entry to `devlog.md`. Make **one focused commit to `main`** and push, then report SHA, touched files, actual checks and remaining limits.

**Status after this documentation commit remains PLANNED, not source implemented.**
