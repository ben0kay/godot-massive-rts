# COMBAT

Benchmark-only rules, not final combat gameplay. `CombatSystem.cs` acquires local targets on staggered ticks and performs cooldown attacks. Damage mutates SIMULATION state. `ProjectileSystem.cs` owns a fixed-capacity pool with swap removal, straight launch-time trajectories, lifetime expiry and swept first-enemy collision checks using the shared grid. It exposes pool drops. BENCHMARK supplies settings and calls shared passes; RENDERING only displays results. A shot deals damage once and returns to the pool on impact.
