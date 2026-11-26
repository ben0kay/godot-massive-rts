# AIR1 — Aircraft and anti-air defenses

**Status:** AIR1-A is source implemented for Clash and awaiting playtest. The remaining phases are planned.
**Repository:** `ben0kay/godot-massive-rts` · **Target branch:** `main`

## Goal

Add autonomous air combat above the existing mass ground battles. Aircraft should be readable as separate silhouettes at isometric battle zoom. Fighters bank into dogfights, bombers make visible runs over ground formations, and dedicated AA defenses create pockets of air cover.

| Piece | Shape | Behavior |
| --- | --- | --- |
| V-Wing Interceptor | Thin swept V, bright faction tips, twin guns | Air-only fighter. Banks toward an enemy aircraft, fires a short minigun burst when aligned, flies past and circles back. |
| Heavy Bomber | Wider, thicker flying wing with a visible payload bay | Ground-attack aircraft. Lines up a straight pass and drops three bombs in a short line across enemy ground formations. Vulnerable during the committed run. |
| AA Minigun | Fast-traversing twin-barrel mount with upward tracers | Short-range, air-only defense. Switches quickly between aircraft and gives nearby troops local cover. |
| AA Missile Battery | Tall vertical launch rack with a distinct launch trail | Longer-range, slower-firing air-only defense. Threatens bombers on approach and makes AA coverage a deployment consideration. |

Exact heights, ranges, speeds, turn rates, cooldowns and damage are playtest tuning. Mobiles with AA weapons are a possible later addition.

## Shared simulation and authoring

- Start with a nominal cruise height above terrain, forward movement and a minimum turn radius. Smooth height changes over slopes and tall scenery. Keep aircraft inside the playable boundary and give missed passes room to turn back.
- Compile air/ground target eligibility with existing unit and defense definitions. Apply it consistently to acquisition, retargeting, projectile contact and splash queries. Ordinary ground attacks default to ground-only. Interceptors and AA can attack aircraft. Bomber splash affects eligible ground targets. Building damage and bomber objective targeting can be considered separately.
- Give the air roster its own spatial index from AIR1-A. Keep airborne aircraft out of the ground index entirely. Ordinary ground attacks query the existing ground index. Interceptors and AA query only the air index. Bombers explicitly query the ground index for attack-run targets, and bomb splash uses the ground index at impact.
- Use a sparse XZ air grid with altitude-aware range checks on the returned candidates. Update membership on the fixed simulation tick, stagger acquisition, cap candidate work, and retain valid targets between searches. The air controller owns the grid and allocates or updates it only when aircraft are active.
- Host the air controller and MultiMesh batches in one shared aircraft scene per battle. Aircraft bypass ground pathfinding, ground crowd separation, forest slowdown, and ground-only traps. Use a lightweight aircraft spacing rule if test formations bunch up.
- Reuse compact unit state, batched visuals, existing projectile and damage infrastructure where suitable. Verify whether current projectile contact is planar before reusing it for air hits. Use altitude-aware contact for air fire and ground contact for bomb impacts. Keep aim and damage authoritative in the simulation and banking, tracers, trails and shadows in rendering.
- Keep air-specific work dormant in ground-only battles. Budget target refresh and visual effects, and avoid per-aircraft scene nodes, physics bodies or timers. Preserve stable unit IDs and existing authored maps and saves.
- Inspect the unit catalog and definitions, movement and spatial queries, shared combat/projectiles, DefenseSystem, and the benchmark and Fortress Defense battle loops. Follow the root Code Preferences and the faction visual authoring guide.

## Proposed phases

1. **AIR1-A · Clash flight and dogfights.** Add the V-Wing with a fixed-step flight loop, separate air spatial index and air-only bursts. Prove approach, firing, overshoot and re-entry with two small opposing flights. Add readable banks, ground-projected shadows and restrained trails. Check mixed ground/air targeting and verify ground-only runs keep their existing behavior.
2. **AIR1-B · Bombing runs and gun AA.** Add the Heavy Bomber and AA Minigun. The bomber commits to a short straight run, drops its payload at intervals, then exits before lining up again. Use pooled bomb visuals and the shared ground impact/splash path where appropriate. Check that AA protects a formation while aircraft can still threaten exposed ranks.
3. **AIR1-C · Missile AA and editor support.** Add the AA Missile Battery as a separate defense role with its own range and cadence. Expose aircraft and AA in the relevant Entity Editor and Level Editor lists, with placement previews, stable save IDs, and editable definitions.
4. **AIR1-D · Campaign rollout.** In Clash, make fighter cover and bomber composition deployment choices. In Offense, author AA-covered approaches to the Citadel while keeping objective targeting explicit. In Fortress Defense, introduce signaled air waves and buildable AA. Reuse the same combat rules across modes and check each mode's resolution, especially when only aircraft remain or neither side can hit the other.

## Later · V-wing landing

Consider allowing airborne V-wings to land once their team has no hostile aircraft left to engage. Use the ground directly beneath the aircraft when legal. Otherwise, search a bounded neighborhood for a spot that fits the ground variant’s footprint and respects map bounds, terrain, structures and unit occupancy. If no spot is available, the V-wing stays airborne.

At touchdown, transfer the same combatant to the ground V-wing variant and its ground spatial index. Preserve faction and damage taken, clear incompatible air orders and targets, and update battle counts once. A V-wing occupies one index at a time. Whether the grounded variant can take off again and how pending enemy aircraft reinforcements affect landing remain open design decisions.

## Try AIR1-A in Clash

Open a Clash level in the Level Editor, select **V-wing** in the bottom **Units** roster to author Red aircraft. Left-drag paints starts;
right-drag erases that faction’s starts. Try eight per side. Press **Test Clash**
and paint the Blue flight from the test roster, then press **Fight**, with or without ground troops. Save the map to keep the painted
starts. Older map-authored flight counts still spawn their original automatic
formation until the first painted start replaces that faction’s count.

Aircraft use normal UnitDefinition resources listed under Aircraft in the shared
UnitCatalog. Both placement bars build cards from those entries, using the
active faction color, authored icon, health and damage. In Test Clash, choose
V-wing in the Blue deployment bar and paint or erase test-only starts before
Fight. Returning from the test discards those edits. Map starts are saved by unit
ID and faction, with legacy V-wing starts still readable.

Aircraft use an independent spatial index. The V-wing opts into air targeting
through CanTargetAircraft. Ordinary ground weapons never query the air index.
Future AA weapons need their own explicit air target capability and targeting
path. Future bombers need a ground-attack flight behavior.

V-wings cruise about 12 world units above open ground. A stable aircraft-specific
offset of up to 1 unit, a gentle oscillation, and the approach and breakaway offsets
vary their height. The terrain lookahead and a conservative height floor above
authored obstacles keep the prototype above cover. The prototype uses air-only
hitscan bursts with visible tracers and two short white wingtip wisps.

## Heavy Bomber in Clash

Select Heavy Bomber in the bottom roster. It automatically seeks the nearest
enemy ground soldier, snapshots its location, and drops three bombs across that spot. It flies out before acquiring the next spot and turns at 36 degrees per second. Fighters
can engage it, and its bombs use the shared ground splash and impact effects.

During Level Editor authoring or Test Clash deployment, select the bomber card
and choose **Bomb**, then click a ground spot to assign that faction’s opening bombing run.
Shift-click remains a shortcut, and Shift-right-click clears the order. The marked run takes priority once, then the flight
resumes auto-fight. Orders authored in the Level Editor save with the map.
Test Clash orders apply to the test session.

## Playtest checks

- A fighter can miss a pass, bank away, reacquire and return without orbiting forever or snapping its heading.
- Check candidate counters in mixed battles. Ordinary ground queries inspect zero airborne IDs, and interceptor and AA queries inspect zero ground IDs. Bombers use ground candidates only for ground attacks.
- Fighters and AA hit aircraft at altitude. Ground-only melee, direct fire and splash behave correctly when an aircraft flies directly overhead.
- Bombs hit the intended ground lane, with readable warning and impact. A lost target, destroyed aircraft, or exhausted projectile pool leaves flight and cooldown state recoverable.
- Aircraft cross water and walls without blocking ground troops. At normal isometric zoom, fighters, bombers, air tracers and both AA types are distinguishable.
- Compare the same seeded ground battle with and without a representative air contingent. Check simulation time, air query counts, projectile counts and worst frame times before increasing aircraft density.
