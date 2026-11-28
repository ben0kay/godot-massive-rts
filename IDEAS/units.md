# Units

Infantry, vehicles, aircraft, roles and balance proposals. See [structures and defenses](structures-and-defenses.md) for stationary buildings.

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

**Status:** Source implemented (Tank definition and shell present); retain original design intent below for reference. Check live turret, weapons and balance before proposing changes.

- **Proposal** — A heavy tracked assault vehicle with a rotating single main cannon, substantial health and slow movement/turning. Fires powerful, deliberate single shells with a modest splash radius; useful against medium/heavy troops and, where eligible, structures. Intended as a conventional frontline combat vehicle between existing smaller siege units and the multi-weapon Dreadnaught, not a replacement for dedicated artillery.
- **Goal** — Add a recognizable heavy direct-fire vehicle with high staying power and a clear weakness to flanking, swarms and dedicated anti-heavy weapons.
- **Notes** — Proposed visual: broad rectangular faction-painted armored hull, dark tracks, layered glacis, rotating gunmetal turret, long cannon and recoil. Reuse existing unit catalogue, compiled Tier 4 footprint, shared movement/separation, turret/muzzle conventions, projectile pool and optional radial damage. Use direct-fire cover/LOS checks rather than artillery arcs. Keep one weapon and avoid per-unit scene nodes, physical tracks, separate physics bodies or unbounded AoE work. Author editable HP, range, shell damage, splash, reload and movement/turn rates; exact balance and structure eligibility remain open.

### Sniper — Tier 2

- **Proposal** — A lightweight long-range marksman with an exceptionally precise, high-damage single-target shot, slow fire rate and low health. Strong at picking off individual high-value units from behind a frontline; inefficient against massed cheap infantry. No splash, piercing beam or multi-target attack by default.
- **Goal** — Add a distinct precision specialist at Tier 2 rather than a large elite railgun vehicle; distinguish it from the existing Long Triangle Marksman by much slower cadence and greater damage per shot, not simply superior overall DPS.
- **Notes** — Proposed visual: narrow faction-painted infantry silhouette, slim long-barreled precision rifle and small scope/emissive sight. Reuse existing ranged acquisition, direct-fire LOS/obstacle checks, pooled projectile and hit/recoil feedback; no new targeting engine. Prefer a normal valid target or bounded high-value preference within the existing query budget, never a full enemy-army scan per shot. Keep range, HP, damage and attack interval editable and compare with live Archer/Marksman/Prism stats before choosing final numbers.


## Shared implementation guidance

These entries preserve design proposals, not implementation authorization. Entries marked source implemented are retained for context, not tasks to repeat. Before implementing an approved idea, read root README, CodePreferences, endgoal, VISUALS/authoring.md, the relevant unit/building documentation and live code; for structures also read WORLD/BUILDINGS/TOWER_IMPLEMENTATION_STANDARD.md. Reuse stable catalog IDs, typed definitions, fixed-step simulation, spatial indexes, existing damage/status and pooled/batched visual paths; preserve existing content and unrelated stats. Integrate with applicable editor placement, previews, save/load, lifecycle and battle modes. Do not introduce per-soldier scene nodes, unbounded scans or duplicate combat engines. All suggested mechanics and unspecified balance values are subject to owner review and measured gameplay testing.
