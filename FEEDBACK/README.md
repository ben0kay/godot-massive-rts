# FEEDBACK — Visual and Camera Response Systems

**Status:** FB0 planned; no feedback files beyond documentation have been implemented yet.

This major root folder will house reusable **presentation-only** effects. The first family is [UNIT_HITS / FB0](UNIT_HITS/FB0_IMPLEMENTATION_PLAN.md): move existing white soldier damage flashes into a dedicated system and add a fast, subtle ~6% size pulse, with a shared settings resource and no per-unit nodes or materials.

## Ownership and future growth

```text
FEEDBACK/
  README.md
  UNIT_HITS/
    FB0_IMPLEMENTATION_PLAN.md
    HitFeedbackSystem.cs         # Create when implementing FB0
    HitFeedbackSettings.cs       # Create when implementing FB0
  UNIT_DEATHS/                  # Future debris / death particles — do not create yet
  CAMERA_SHAKE/                 # Future camera effects — do not create yet
  IMPACTS/                      # Future impact flashes / particles — do not create yet
```

Combat and simulation keep authoritative health, damage, world position and unit IDs. FEEDBACK decides when and how a presentation response is shown. RENDERING continues batching the final appearance using the existing MultiMesh instance buffers; do not move the renderer into FEEDBACK or create per-soldier actors.

The earlier white-flash code currently spans `SIMULATION/UnitState.cs`, `SIMULATION/MovementSystem.cs`, `RENDERING/ArmyRenderer.cs` and `BENCHMARK/CONFIG/BenchmarkConfig.cs`. FB0 should centralize timer/configuration/visual semantics while keeping combat logic and GPU upload responsibilities in their existing major systems.
