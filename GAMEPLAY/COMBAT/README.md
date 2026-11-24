# COMBAT

Benchmark-only rules, not final combat gameplay. `CombatSystem.cs` acquires local targets on staggered ticks and performs cooldown attacks. Damage mutates SIMULATION state. Ranged attacks launch pooled shots through [PROJECTILES](../PROJECTILES/README.md).

UNIT0 reads damage, armour, attack cadence and range from the compiled unit catalogue. Projectiles carry their shooter’s damage and use defender footprint in collision checks.

Normal melee approaches to body contact. A swing requires both footprints plus the active separation gap and a 0.08-unit tolerance. Directional units turn toward the target before committing a forward attack. The Pentagon and Hexagon default to a 100° cleave centred on their current heading. The primary takes full damage and up to two other enemies in contact within the cone take 65% by default, with ties broken by soldier ID. A coincident primary receives the primary hit alone.

`AreaDamage` also provides a reusable XZ impact circle with linear falloff to a configurable edge multiplier. Both attacks query the existing enemy grid and apply damage through UnitState. Because that grid is built before movement, callers include maximum ordinary movement plus the separation push cap in the candidate search. Range and angle are checked against current positions. The caller supplies the radial impact radius and can pass an obstacle field to block hits across walls. Hexagon Siege Infantry reuses the Pentagon cleave and replaces every fifth eligible attack with an 8-unit stomp. Damage falls linearly from 25 at the centre to 15 at the edge (0.6 edge multiplier). The stomp counts completed attacks and uses normal melee cooldown and target validation. The unit definition exposes cadence, base radius, centre and outer damage, and cover; UNIT1 multiplies the base radius by body size. The expanding ring is independent of damage.

[Trample](TRAMPLE/README.md) is an opt-in charge ability. It runs after ordinary movement and before separation. Only successfully penetrated enemy pairs bypass separation for the charge tick. The later area and projectile queries pad for the maximum trample shove as well as normal separation.

UNIT2 exposes the ordinary melee facing angle, cleave secondary target cap (0–50), and direct shockwave outer damage in the unit definition. Older resources using a shockwave edge fraction still load.

## DMG1

Edit `DefaultDamageMatrix.tres` to tune damage types, armour classes and their effectiveness. Entries are row-major, with one row per damage type and one column per armour class. Keep Standard and Light first so old definitions have their default IDs. When changing the column list, update each row to the new width. Missing values default to 1×. Definition names resolve to integer indices when the arena starts. A blank or unknown name uses the first entry. Reload the arena after editing resources.

All hits use base damage × matrix effectiveness × (1 − defender percentage armour). Primary attacks, cleave, shockwave, trample, projectiles and tower beams use the attacker's configured damage type. Existing weapons default to Standard, which ships with a neutral row. Entity Editor exposes per-category damage percentages. A value of −1 uses the class matchup, and a nonnegative value replaces it for that category. H Breacher uses 50% against units and 100% against structures. Single-type selections show the attack and armour classes and derived matchup multipliers.

Structure targeting is a separate setting. In the command sandbox, eligible attackers refresh structure candidates on the staggered acquisition phase and route to a clear footprint edge through the command mover. A reachable soldier in melee contact can interrupt the approach to a preferred structure. Move orders allow swings within reach and attack-move can pursue. The legacy wave Citadel assault remains a scenario rule for the original roster. In painted benchmark battles, auto-attacking units with structure targeting can also engage enemy towers. Benchmark Auto Attack compares the distance to the live soldier selected by the shared targeting pass with the distance to the nearest attackable tower edge. The closer target wins, with soldiers winning ties. Destroyed towers stop firing and release their footprints.


## PRISM1 piercing discharge

Prism Lance shares segment-versus-footprint and terrain math with the Railgun Tower. Prism only scans the full segment once per shot. Its spatial query uses the existing enemy grid and movement padding. It keeps the nearest hits in a bounded stack buffer, using soldier ID to resolve equal entry distances. A 32-discharge limit per simulation tick spreads simultaneous salvos over several ticks. Fully charged units revalidate their target before using a later slot. The firing flash and charge lens use separate bounded MultiMesh batches. `UnitVisualConfig.MaxPrismBeamEffects` controls the flash cap independently of damage.

## First hit per enemy

Set **First hit per enemy** above 1× in the Entity Editor to boost a unit's first single-target melee strike against each enemy soldier. An attacker keeps its own record, so returning to a previous enemy uses regular damage. The ordinary attack interval still applies. The bonus is applied before armour and shields. Only attackers with the bonus allocate a hit record, which is discarded when the attacker dies or the battle resets.
