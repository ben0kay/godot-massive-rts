# RENDERING

`ArmyRenderer.cs` creates six MultiMesh batches (two factions × three shapes), allocates exact deployment capacities and packs live slots on death. Dirty entries update transforms/colors; unmodified entries are not resubmitted. `ProjectileRenderer.cs` owns one capped MultiMesh for the projectile pool. All materials are unshaded; shadows are disabled. Render sync runs on the main thread and is measured separately from simulation. Whole-batch culling, no interpolation/LOD; no per-soldier nodes. BENCHMARK owns renderer creation/disposal.
