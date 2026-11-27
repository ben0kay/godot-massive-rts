# Units and defenses

Unit, aircraft, and tower concepts, roles, and balance ideas.

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

## Ground units

### Flame Trooper

- **Proposal** — A fragile short-range anti-infantry unit (suggested Tier 2) that projects a continuous fan/cone of fire toward enemies. Damages several hostile units inside the cone at bounded intervals rather than firing individual bullets. Deliberately **low health for its tier** to compensate for strong crowd damage; vulnerable to focused ranged fire and heavy units before getting into range.
- **Goal** — Provide an infantry-clearing weapon that rewards positioning and protects the balance between massed infantry and specialist counters.
- **Notes** — Proposed visual: compact faction-painted infantry hull, dark short flamethrower nozzle and rear fuel/energy pack; a readable short-lived fire cone. Reuse existing cone/cleave spatial queries and damage pipeline, configurable angle/range/damage/tick interval/victim cap, and a bounded batched visual effect. Do not spawn a projectile, particle node or physics query per flame or target per frame. Start with direct cone damage, no persistent burning-ground or damage-over-time system. Exact HP/damage are provisional and must be tuned against live infantry definitions; low HP is a requirement.

### Disruptor

- **Proposal** — A specialist anti-shield ranged ground unit (suggested Tier 3). Fires an EMP-like bolt that is particularly effective against shields and temporarily suppresses shield regeneration on the struck enemy. Has modest ordinary HP damage so it is not a universal upgrade over conventional ranged units.
- **Goal** — Counter shield-generator-heavy formations and make combined arms more useful.
- **Notes** — Proposed visual: faction-painted angular hull, paired capacitor banks and a distinctive compact emitter. Reuse the existing projectile, damage and shield systems; apply a configurable bonus to shield damage only and a short non-stacking regeneration suppression status on confirmed hits. Clarify shield overflow and application to already-unshielded targets when implementing. Share status logic with the proposed Disruption Tower where practical. Use existing target eligibility and cadence; no additional global shield scans or per-unit timers.

### Tank — Tier 4

- **Proposal** — A heavy tracked assault vehicle with a rotating single main cannon, substantial health and slow movement/turning. Fires powerful, deliberate single shells with a modest splash radius; useful against medium/heavy troops and, where eligible, structures. Intended as a conventional frontline combat vehicle between existing smaller siege units and the multi-weapon Dreadnaught, not a replacement for dedicated artillery.
- **Goal** — Add a recognizable heavy direct-fire vehicle with high staying power and a clear weakness to flanking, swarms and dedicated anti-heavy weapons.
- **Notes** — Proposed visual: broad rectangular faction-painted armored hull, dark tracks, layered glacis, rotating gunmetal turret, long cannon and recoil. Reuse existing unit catalogue, compiled Tier 4 footprint, shared movement/separation, turret/muzzle conventions, projectile pool and optional radial damage. Use direct-fire cover/LOS checks rather than artillery arcs. Keep one weapon and avoid per-unit scene nodes, physical tracks, separate physics bodies or unbounded AoE work. Author editable HP, range, shell damage, splash, reload and movement/turn rates; exact balance and structure eligibility remain open.

### Sniper — Tier 2

- **Proposal** — A lightweight long-range marksman with an exceptionally precise, high-damage single-target shot, slow fire rate and low health. Strong at picking off individual high-value units from behind a frontline; inefficient against massed cheap infantry. No splash, piercing beam or multi-target attack by default.
- **Goal** — Add a distinct precision specialist at Tier 2 rather than a large elite railgun vehicle; distinguish it from the existing Long Triangle Marksman by much slower cadence and greater damage per shot, not simply superior overall DPS.
- **Notes** — Proposed visual: narrow faction-painted infantry silhouette, slim long-barreled precision rifle and small scope/emissive sight. Reuse existing ranged acquisition, direct-fire LOS/obstacle checks, pooled projectile and hit/recoil feedback; no new targeting engine. Prefer a normal valid target or bounded high-value preference within the existing query budget, never a full enemy-army scan per shot. Keep range, HP, damage and attack interval editable and compare with live Archer/Marksman/Prism stats before choosing final numbers.

## Shared implementation guidance

These seven entries are future design proposals, not implementation authorization. Before implementing an approved idea, read root README, CodePreferences, endgoal, VISUALS/authoring.md, the relevant unit/building documentation and live code; for structures also read WORLD/BUILDINGS/TOWER_IMPLEMENTATION_STANDARD.md. Reuse stable catalog IDs, typed definitions, fixed-step simulation, spatial indexes, existing damage/status and pooled/batched visual paths; preserve existing content and unrelated stats. Integrate with applicable editor placement, previews, save/load, lifecycle and battle modes. Do not introduce per-soldier scene nodes, unbounded scans or duplicate combat engines. All suggested mechanics and unspecified balance values are subject to owner review and measured gameplay testing.
