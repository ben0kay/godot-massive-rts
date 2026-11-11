# COMBAT

Benchmark-only rules, not final combat gameplay. `CombatSystem.cs` acquires local targets on staggered ticks and performs cooldown attacks. Damage mutates SIMULATION state. Ranged attacks launch pooled shots through [PROJECTILES](../PROJECTILES/README.md).

UNIT0 reads damage, armour, attack cadence and range from the compiled unit catalogue. Projectiles carry their shooter’s damage and use defender footprint in collision checks.

The Pentagon uses a 100° cleave centred on its current target. The primary takes full damage and the two closest additional enemies within the cone and melee reach take 65%, with ties broken by soldier ID. A coincident primary gives the swing no facing, so it receives the primary hit alone. Cleave is authored on the unit definition.

`AreaDamage` also provides a reusable XZ impact circle with linear falloff to a configurable edge multiplier. Both attacks query the existing enemy grid and apply damage through UnitState. Because that grid is built before movement, callers include maximum ordinary movement plus the separation push cap in the candidate search. Range and angle are checked against current positions.

[Trample](TRAMPLE/README.md) is an opt-in charge ability. It runs after ordinary movement and before separation. Only successfully penetrated enemy pairs bypass separation for the charge tick. The later area and projectile queries pad for the maximum trample shove as well as normal separation.
