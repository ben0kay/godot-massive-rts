# COMBAT

Benchmark-only rules, not final combat gameplay. `CombatSystem.cs` acquires local targets on staggered ticks and performs cooldown attacks. Damage mutates SIMULATION state. Ranged attacks launch pooled shots through [PROJECTILES](../PROJECTILES/README.md).

UNIT0 reads damage, armour, attack cadence and range from the compiled unit catalogue. Projectiles carry their shooter’s damage and use defender footprint in collision checks.

Normal melee approaches to body contact. A swing requires both footprints plus the active separation gap and a 0.08-unit tolerance. Directional units turn toward the target before committing a forward attack. The Pentagon and Hexagon default to a 100° cleave centred on their current heading. The primary takes full damage and up to two other enemies in contact within the cone take 65% by default, with ties broken by soldier ID. A coincident primary receives the primary hit alone.

`AreaDamage` also provides a reusable XZ impact circle with linear falloff to a configurable edge multiplier. Both attacks query the existing enemy grid and apply damage through UnitState. Because that grid is built before movement, callers include maximum ordinary movement plus the separation push cap in the candidate search. Range and angle are checked against current positions. The caller supplies the radial impact radius and can pass an obstacle field to block hits across walls. Hexagon Siege Infantry reuses the Pentagon cleave and replaces every fifth eligible attack with an 8-unit stomp. Damage falls linearly from 25 at the centre to 15 at the edge (0.6 edge multiplier). The stomp counts completed attacks and uses normal melee cooldown and target validation. The unit definition exposes cadence, base radius, centre and outer damage, and cover; UNIT1 multiplies the base radius by body size. The expanding ring is independent of damage.

[Trample](TRAMPLE/README.md) is an opt-in charge ability. It runs after ordinary movement and before separation. Only successfully penetrated enemy pairs bypass separation for the charge tick. The later area and projectile queries pad for the maximum trample shove as well as normal separation.

UNIT2 exposes the ordinary melee facing angle, cleave secondary target cap (0–50), and direct shockwave outer damage in the unit definition. Older resources using a shockwave edge fraction still load.
