# COMBAT

Benchmark-only rules, not final combat gameplay. `CombatSystem.cs` acquires local targets on staggered ticks and performs cooldown attacks. Damage mutates SIMULATION state. `ProjectileSystem.cs` owns a fixed-capacity pool with swap removal, straight launch-time trajectories, lifetime expiry and swept intended-target checks. It exposes pool drops. BENCHMARK supplies settings and calls shared passes; RENDERING only displays results. No physics nodes, ballistics or intervening-target collision.
