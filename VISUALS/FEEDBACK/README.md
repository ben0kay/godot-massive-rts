# FEEDBACK — Visual and Camera Response Systems

**Status:** FB0 and FX2 are source implemented and await owner visual validation.

**[VIS-IDEAS1: Unit and Battlefield Visual Ideas](UNIT_VISUAL_IDEAS_REVIEW.md)** is a **planning-only Codex review backlog** covering melee sparks, battlefield dust, directional hit wobble, large-unit ground disturbance, temporary explosion scars, projectile near-miss detail, extra armour wear and reactive forests. None of these ideas are authorized for implementation by the backlog itself. It builds on existing FX2 cracks/crumbling instead of replacing them.

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
  UNIT_DEATHS/                  # Pooled fragments and ground splats
  CAMERA_SHAKE/                 # Future camera effects — do not create yet
  IMPACTS/                      # Future impact flashes / particles — do not create yet
```

Combat and simulation keep authoritative health, damage, world position and unit IDs. FEEDBACK decides when and how a presentation response is shown. RENDERING continues batching the final appearance using the existing MultiMesh instance buffers; do not move the renderer into FEEDBACK or create per-soldier actors.

Positive nonlethal damage registers a hit with the battle's feedback system. A packed list tracks active IDs so each tick advances only soldiers with active feedback. A new hit refreshes white and starts its pulse from the current visual size. Death removes the instance through the normal renderer path. Pause freezes the effect in simulation time, and respawn creates fresh state.

The shared `UNIT_HITS/HitFeedbackSettings.tres` is used by Fortress Defense and assigned in the default benchmark resource. Defaults are a 0.12-second white flash and a 1.06× pulse over 0.18 seconds, with a fast rise over the first 20%. Disable Pulse Enabled for a white-only comparison. Settings are read when the next battle starts. The grounded scale is applied to the packed renderer transform only. Visual changes are sampled on the 60 Hz core simulation ticks.

## Ranged recoil (visual-only)

[UNIT_RECOIL/RangedRecoilSystem.cs](UNIT_RECOIL/RangedRecoilSystem.cs) tracks only active shooters after successful projectile spawns. A small backwards world-space offset is applied to the rendered MultiMesh transforms, then eased to zero over the configured per-unit recovery duration. Authoritative positions, navigation, collisions and projectile origins are unchanged. The controller is allocated only in ranged battles if at least one catalogue type enables recoil. No per-soldier scene nodes or physics bodies.

## FX2 — Unit fracture

Open [UnitVisualConfig.tres](../../GAMEPLAY/UNITS/UnitVisualConfig.tres) in the Godot Inspector. **Use Unit Fracture** ships enabled and applies to cracks and crumbling on scene reload. Turning it off gives units their original surface material and preserves hit flash, pulse and ground splats.

First Crack Health and Heavy Crack Health default to 75% and 25%. The intermediate threshold is halfway between them. The shared crack mask adds progressive dark fractures to upper facets in mesh-local space, with a stable orientation derived from each unit ID. Crack Darkness sets the maximum shading and defaults to 0.84. The existing opaque MultiMesh alpha channel carries stage and orientation, retaining the 16-float instance stride. The white hit flash temporarily hides the cracks.

[UNIT_DEATHS](UNIT_DEATHS/README.md) owns fragments and splats. Benchmark, Fortress Defense and the Level Editor's combat tests use these shared visuals. The AutoBattler campaign layout view is static.

### Reactive buildings and treads

ShockwaveRenderer samples low-health structures at 0.7-second intervals (up to 12 smoke emissions per sample, with a subset emitting contact-pool sparks), and existing destruction cleanup triggers one building explosion, chips, scorch and three dark smoke puffs. Shared smoke shader supports dark puffs without changing ordinary muzzle smoke. Siege Ram alone opts into `LeavesTreadMarks` (Entity Editor → Appearance and size); other unit types can opt in later. TreadMarks tracks eligible moving units at 0.16-second cadence, emits paired ground marks by distance, and caps the lazy batch at 512 marks with 12-second lifetime and 4-second fade. Ground slope is approximated; sharp slopes skip marks. No navigation or combat changes.

### Projectile contact feedback

`ShockwaveRenderer.EmitBulletImpact` reuses `MeleeEffects` (kind 3, 0.15-second metallic sparks, shared 256-effect cap and spatial cooldown). `ShieldRicochetEffects` is one lazy 256-instance MultiMesh of short faction-tinted cosmetic tracers. Reflection uses the existing hit point and approximate unit collision sphere, with independent cosmetic scatter. The pool is advanced, synchronized and cleared alongside the other battle feedback; it drops excess effects rather than queueing them. No new spatial queries or per-projectile nodes. Owner validation pending.

### Rally indicators and building aftermath

The existing 384-slot rally marker batch now shows small camera-facing faction chevrons over living buff recipients, softly pulsing in shader and fading toward the real buff expiry. No additional unit queries. Building destruction keeps the existing explosion/smoke/scorch event while WreckField owns the temporary burnt husk mesh and collision lifecycle (see navigation guide).

### Heavy projectile impact reaction

The shared HitFeedbackSettings resource controls a .40-second directional
rock for surviving Tier 2+ units. The active list updates only during the
reaction. ArmyRenderer applies it to the existing instance basis, including
the final neutral pose. The Tank's integrated turret moves with the rendered
hull, and separate Dreadnaught weapon models share its tilt and ground pivot.
