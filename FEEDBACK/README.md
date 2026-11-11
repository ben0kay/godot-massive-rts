# FEEDBACK — Visual and Camera Response Systems

**Status:** FB0 source implemented and awaiting owner visual validation.

This major root folder houses reusable **presentation-only** effects. The first family is [UNIT_HITS / FB0](UNIT_HITS/FB0_IMPLEMENTATION_PLAN.md): centralizes the white damage flash and adds a fast, subtle 6% size pulse, with a shared settings resource and no per-unit nodes or materials.

## Ownership and future growth

```text
FEEDBACK/
  README.md
  UNIT_HITS/
    FB0_IMPLEMENTATION_PLAN.md
    HitFeedbackSystem.cs
    HitFeedbackSettings.cs
    HitFeedbackSettings.tres
  UNIT_DEATHS/                  # Future debris / death particles — do not create yet
  CAMERA_SHAKE/                 # Future camera effects — do not create yet
  IMPACTS/                      # Future impact flashes / particles — do not create yet
```

Combat and simulation keep authoritative health, damage, world position and unit IDs. FEEDBACK decides when and how a presentation response is shown. RENDERING continues batching the final appearance using the existing MultiMesh instance buffers; do not move the renderer into FEEDBACK or create per-soldier actors.

Positive nonlethal damage registers a hit with the battle's feedback system. A packed list tracks active IDs so each tick advances only soldiers with active feedback. A new hit refreshes white and starts its pulse from the current visual size. Death removes the instance through the normal renderer path. Pause freezes the effect in simulation time, and respawn creates fresh state.

The shared `UNIT_HITS/HitFeedbackSettings.tres` is assigned in the default benchmark resource. Defaults are a 0.12-second white flash and a 1.06× pulse over 0.18 seconds, with a fast rise over the first 20%. Disable Pulse Enabled for a white-only comparison. Settings are read when the next battle starts. The grounded scale is applied to the packed renderer transform only. At 30 Hz, visual changes are sampled on simulation ticks.
