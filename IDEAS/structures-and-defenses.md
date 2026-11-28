# Structures and defenses

Stationary defenses, fortifications, utility buildings and strategic objects. All additions below are proposals unless marked otherwise.

## Structures and defenses

### Stealth Beacon

- **Proposal** — A stationary detection/support structure that reveals hostile stealth units within a configurable radius. Revealed units become eligible targets for friendly weapons that can otherwise legally attack them (air/ground eligibility, range and line of sight still apply). The beacon has no offensive weapon and does not itself increase weapon range.
- **Goal** — Provide a dedicated counter to future stealth units, protect vulnerable fortresses and create meaningful detection coverage gaps.
- **Notes** — Stealth is a prerequisite, not implemented or approved by this entry. Decide the exact reveal/visibility rules, whether aircraft can use stealth and whether overlapping beacons share faction-wide detection before implementing. Proposed visual: compact gunmetal pedestal, faceted radar/sensor array and restrained faction-colored scan trim. Reuse the BuildingDefinition/catalogue, support-field lifecycle and existing spatial indexes; query only relevant stealth candidates on staggered/bounded refreshes, cache reveal state and invalidate it on movement, death, beacon destruction or reset. Avoid per-soldier full-map scans, real lights and per-beacon processing nodes. Reveal state must be authoritative rather than driven by a visual effect.

### Disruption Tower

- **Proposal** — A non-projectile support defense that periodically emits an electromagnetic pulse within a limited radius. Temporarily slows enemy movement and prevents affected enemy shields from regenerating for a short duration; existing shield points are not automatically removed. Long cooldown between pulses gives enemies windows to advance.
- **Goal** — Counter fast assaults and shield-heavy armies while supporting normal defenses instead of replacing damage towers.
- **Notes** — Proposed visual: armored emitter pedestal with a raised concentric-ring generator and a brief faction-tinted expanding pulse. Reuse bounded radial/spatial queries, shield regeneration rules and existing pooled shockwave effects. Store compact timed status state, use non-stacking refresh rules, and apply the movement modifier without changing base authored speed. Expire statuses normally and clean them on death/reset; destruction stops future pulses. Keep range, slow factor, effect duration and pulse cooldown editable. Initial proposal targets ground units; air disruption and structure effects are separate design decisions.

### Fortress Gate

- **Proposal** — A wide destructible armored gateway for fortress walls that permits friendly ground units to pass while blocking hostile ground units. Friendly access is automatic; destroying the gate opens the passage to all factions. The gate is a structure, not a weapon.
- **Goal** — Create meaningful fortress entrances, defensive chokepoints and siege objectives without trapping the defending army inside its own base.
- **Notes** — Proposed visual: two heavy gunmetal side pillars, faction armor and an animated central segmented door. Reuse building health, damage, placement, terrain grounding, stable IDs and destruction lifecycle. The closed gate must be targetable by eligible siege/structure attackers. Friendly/hostile passage requires faction-aware navigation and separation/collision handling; do not merely hide the mesh or remove the global obstacle when allies approach. Invalidate affected cached routes on gate state changes/destruction, preferably locally and with existing budgets. Match opening width to compiled unit footprints. Keep projectile/LOS and open-door behavior coherent with authoritative state; exact animation, projectile passage and gate dimensions remain open. Avoid per-unit pathfinding spikes or per-frame global obstacle rebuilds.

## Additional defensive and support concepts

### Anti-heavy barriers
- **Proposal** — Dragon's-teeth-style obstacles: Tier 1 passes freely, heavy ground vehicles are substantially slowed. Prefer local traversal resistance over new global pathfinding rules; choose exact tier thresholds and destruction behavior later.
- **Goal** — Complement the existing Light Barricade, which delays light infantry and can be crushed by heavy units.

### Energy fence
- **Proposal** — Hostile units may pass through but receive bounded periodic damage; a linked destructible generator disables the fence when destroyed. Decide friendly passage, shielding and placement rules before implementation.
- **Goal** — Create a defensible perimeter without forcing expensive alternate routes.

### Infantry bunker
- **Proposal** — A small armored emplacement that can house Tier 1 ranged units, providing substantial protection while allowing legal fire outward. Define capacity, entry/exit, occupant targeting and behavior on destruction; reuse trench/cover concepts where appropriate.
- **Goal** — Protect fragile ranged formations at strategic positions.

### Power and communications stations
- **Proposal** — Destroy a power station to disable linked towers or shield generators; communications stations extend nearby friendly detection range, without necessarily extending attack range. Explicit links and bounded support updates; avoid global per-unit scans.
- **Goal** — Give Offense battles meaningful secondary targets.

### Field repair station
- **Proposal** — Slowly restore HP of nearby friendly mechanical units, capped and non-stacking. Decide mechanical eligibility, combat healing, range and rate.
- **Goal** — Sustain defensive vehicle groups without making healing universally available.

### Volatile storage tanks
- **Proposal** — Neutral destructible fuel/energy containers damage nearby units of either faction when destroyed, using existing bounded splash and pooled effects. Clarify whether units deliberately target them.
- **Goal** — Make industrial battlefields tactically interactive.

### Heavy proximity mines
- **Proposal** — A game-only defensive trap that triggers on Tier 3+ ground units, ignoring lighter infantry. Use existing trap lifecycle and bounded spatial queries; define cooldown/consumption and friendly behavior.
- **Goal** — Give light forces a relative advantage when opening heavy-vehicle routes.

### Reinforcement landing pad
- **Proposal** — Placeable arrival marker for scripted friendly or enemy reinforcements, linked to authored scenario events rather than a separate spawning system. See [maps and modes](maps-and-modes.md#scenario-events-and-objectives).
- **Goal** — Enable legible arrival locations and landing-zone scenarios.

### Teleportation gate
- **Proposal** — Linked structures transfer eligible troops between designated positions; use capacity and cooldown limits, validate exit clearance and avoid mass teleport spikes.
- **Goal** — Create unusual flanking routes and objectives. Later concept, not part of initial skirmish scope.

## Shared implementation guidance

These entries preserve design proposals, not implementation authorization. Entries marked source implemented are retained for context, not tasks to repeat. Before implementing an approved idea, read root README, CodePreferences, endgoal, VISUALS/authoring.md, the relevant unit/building documentation and live code; for structures also read WORLD/BUILDINGS/TOWER_IMPLEMENTATION_STANDARD.md. Reuse stable catalog IDs, typed definitions, fixed-step simulation, spatial indexes, existing damage/status and pooled/batched visual paths; preserve existing content and unrelated stats. Integrate with applicable editor placement, previews, save/load, lifecycle and battle modes. Do not introduce per-soldier scene nodes, unbounded scans or duplicate combat engines. All suggested mechanics and unspecified balance values are subject to owner review and measured gameplay testing.
