# Fortress Defense waves

Command levels expose `WaveDefinitions`, `BetweenWavesSeconds`, and `EnemySpawnEdge` on the scene root. Each `WaveDefinition.cs` resource maps stable soldier IDs to counts. `FortressWaves.cs` releases troops just inside the selected boundary and leads them toward the Citadel. South (+Z) is the default. Lanes spread along the edge near the Citadel's coordinate. Leave open space at the entry so the wave can release.

Enable **Wave Editor** in **Project Settings → Plugins** after building the C# project. Open the Command Sandbox or an inherited level and click **Wave Editor** in the bottom panel. Edit the edge, interval, and counts, or add and remove waves. Save the open scene to keep its settings. The Inspector remains another way to edit the same data.

A centered Citadel is roughly 2,500 world units from an edge. On the current sandbox that means several minutes of travel before contact. EnemySpawn markers from older scenes are obsolete.

`FortressWaves` advances the wave state, uses the shared spatial grid for local soldier detection, issues movement through `CommandMovement`, and reuses soldier combat and projectile flight. Citadel-targeted shots resolve against the building and can be blocked by cover. They do not retarget to soldiers in flight. Legacy wave units attack soldiers and fall back to their scripted Citadel assault. Units configured to target structures instead use the shared footprint-based acquisition and attack logic, including attacks on player Fabricators. The sandbox includes a red practice Fabricator for blue structure-capable units. Enemy IDs are reserved up front so fabricating units cannot exhaust wave capacity.
