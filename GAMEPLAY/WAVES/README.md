# Fortress Defense waves

The Command Sandbox owns wave setup through its inspected `WaveDefinitions` and `BetweenWavesSeconds` properties. Each wave maps stable soldier IDs to counts. The flat sandbox uses the authored `EnemySpawn` marker and a cached Citadel footprint. Future maps can position their own marker and pass a fixed Citadel footprint.

`FortressWaves` advances the wave state, uses the shared spatial grid for local soldier detection, issues movement through `CommandMovement`, and reuses soldier combat and projectile flight. Citadel-targeted shots resolve against the building and can be blocked by cover. They do not retarget to soldiers in flight. Wave units attack soldiers and the Citadel. Other buildings provide cover and remain outside attacker targeting in this pass. Enemy IDs are reserved up front so fabricating units cannot exhaust wave capacity.
