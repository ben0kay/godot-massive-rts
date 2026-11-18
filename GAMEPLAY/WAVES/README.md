# Fortress Defense waves

Command levels expose `WaveDefinitions`, `BetweenWavesSeconds`, `EnemySpawnEdge`, `EnemySpawnWidth`, and `EnemySpawnRate` on the scene root. Each `WaveDefinition.cs` resource maps stable soldier IDs to counts. `FortressWaves.cs` releases troops just inside the selected boundary and leads them toward the Citadel. South (+Z) is the default. Spawn width is measured in world units along the selected edge, centred near the Citadel and shifted to fit the map. The default on Level 001 is 256 units.

Enable **Wave Editor** in **Project Settings → Plugins** after building the C# project. Open the Command Sandbox or an inherited level and click **Wave Editor** in the right dock beside **World Painter**. Edit the edge, spawn width, time between waves, spawn rate, and counts, or add and remove waves. Save the open scene to keep its settings. The Inspector remains another way to edit the same data.

A centered Citadel is roughly 2,500 world units from an edge. On the current sandbox that means several minutes of travel before contact. EnemySpawn markers from older scenes are obsolete.

`FortressWaves` advances the wave state, uses the shared spatial grid for local soldier detection, issues movement through `CommandMovement`, and reuses soldier combat and projectile flight. Citadel-targeted shots resolve against the building and can be blocked by cover. They do not retarget to soldiers in flight. Legacy wave units attack soldiers and fall back to their scripted Citadel assault. Units configured to target structures instead use the shared footprint-based acquisition and attack logic, including attacks on player Fabricators. The sandbox includes a red practice Fabricator for blue structure-capable units. Enemy IDs are reserved up front so fabricating units cannot exhaust wave capacity.

### Spawn patterns

**Stream** and **Scatter** release units at the configured spawn rate. **Rows** releases a batch before waiting the configured row delay. Each pattern picks random positions continuously along the cached openings. The game finds openings for each soldier footprint and requires a short clear approach past rocks, water, and buildings. It refreshes the cache when the movement geometry changes. Nearby live units can temporarily block a spot, so spawning retries without consuming the queued soldier. A fully blocked band appears in the wave HUD.
