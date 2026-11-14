# PROJ-GLOW1 — Fake Ground Lighting for Low-Flying Projectiles

**Repository:** `ben0kay/godot-massive-rts`  
**Branch:** `main`  
**Status:** **PLANNED ONLY — not implemented or authorized by this note**  
**Priority:** Later, when a travelling laser/plasma/energy projectile type is added.

## Intent

Give selected **low-flying, luminous projectiles** (future travelling laser bolts, plasma shots or energy orbs) a soft pool of colored light on the ground beneath them, **without adding real `OmniLight3D` nodes or modifying the lighting engine**.

The owner supplied a Godot shader technique from a video: render a flat quad below the projectile, with a radial alpha falloff and additive blending. The quad follows the projectile's XZ position and stays near the terrain surface. It creates the **appearance** of a projectile illuminating the ground.

**Important exclusions:**
- **Do not enable this on existing high-arc Chevron Artillery / `ArtilleryShell.tres`.** A ground glow chasing a shell far above the battlefield would look misleading.
- Do not enable it by default for ordinary arrows or all ranged weapons.
- Do not change the existing Laser Tower's brief **hitscan beam** into a travelling projectile. This plan concerns future physical/travelling energy projectiles, not instantaneous beam effects.
- Do not implement this feature now. This is an implementation handoff for a future Codex task.

## Reference shader supplied by owner

The following is the **starting reference**, not a claim that it has been compiled or tested in this project. Adapt it as needed for batching, per-instance color and alpha control.

```glsl
shader_type spatial;

render_mode unshaded, blend_add, depth_draw_never, cull_disabled;

uniform vec4 light_color : source_color = vec4(1.0, 0.45, 0.08, 1.0);
uniform float intensity : hint_range(0.0, 5.0) = 1.0;
uniform float softness : hint_range(0.1, 8.0) = 2.5;

void fragment()
{
    vec2 p = UV - vec2(0.5);

    float dist = length(p) * 2.0;

    float glow = 1.0 - clamp(dist, 0.0, 1.0);
    glow = pow(glow, softness);

    ALBEDO = light_color.rgb;
    EMISSION = light_color.rgb * intensity * glow;
    ALPHA = glow * light_color.a;
}
```

Godot shader details to verify at implementation: correct quad UVs, world-plane orientation, effective additive intensity/opacity, transparency sorting/depth testing, and whether instance-color or instance-custom data is preferable for per-projectile color/intensity. Keep the shader simple; do not assume it creates actual illumination on units, walls or other geometry.

## Architecture and placement

Existing source to inspect first:
- `GAMEPLAY/PROJECTILES/Shared/ProjectileDefinition.cs`: per-type editable authoring.
- `GAMEPLAY/PROJECTILES/Shared/ProjectileCatalog.cs`: compiles shared runtime parameters.
- `GAMEPLAY/PROJECTILES/Shared/ProjectileSystem.cs`: pooled positions/types/lifetimes, swap-removal.
- `GAMEPLAY/PROJECTILES/Shared/ProjectileRenderer.cs`: existing per-type `MultiMeshInstance3D` projectile batches.
- `WORLD/TERRAIN/SHARED/TerrainHeightField.cs`: sampled terrain surface, if available in the active battlefield.
- `BENCHMARK/BenchmarkArena.cs`: owns projectile renderer lifecycle and fixed 60 Hz simulation.

**Proposed:** Implement an optional, shared `ProjectileGroundGlow` presentation path owned by `GAMEPLAY/PROJECTILES/`, preferably within or next to `ProjectileRenderer`. Its quads should be backed by **one reusable quad mesh** and **batched `MultiMeshInstance3D` instances** per compatible shader/material configuration. No per-projectile `Node3D`, `MeshInstance3D`, material, light or physics body.

Glow positions are visual only and derive from active projectile positions. On every necessary render sync:
1. Read the active projectile X/Z and projectile type.
2. If that projectile definition has ground glow enabled, find the terrain surface Y below it (or 0 on flat ground).
3. Measure height **above the sampled ground**, not above world Y=0.
4. Skip the glow entirely above its maximum effective height; fade it with height below that threshold.
5. Place a **horizontal** quad slightly above the ground, oriented in XZ (Godot world up is Y). Keep the quad clear of z-fighting.
6. Render the radial additive glow underneath the projectile, with color, diameter and opacity from its authored definition.

On sloped/stepped terrain, a flat quad may intersect or float above the ground: document that limitation initially; avoid a complex decal/terrain-projection engine in this milestone. Ground projection should respect normal depth testing so walls can occlude the glow rather than allowing it to shine through everything.

Correctness on pool compaction is critical: `ProjectileSystem.Remove` swaps the final projectile into a deleted index. Rebuild/reorder active glow instance mapping with the **current** active projectile set; a glow must never remain at a dead shell's position or jump to a different projectile's color/type.

Ensure the glow renderer is cleaned up on battle reset, pause/clear and scene exit. When a projectile impacts or expires, its glow disappears promptly. Do not alter projectile collision or damage.

## Optional settings in projectile `.tres` definitions

Add an opt-in **Ground Glow** section to `ProjectileDefinition` and compile validated values into shared runtime visual settings, without introducing allocations in the simulation hot path.

Suggested initial fields/defaults (tuning proposals only):

| Field | Suggested default | Meaning |
| --- | --- | --- |
| `GroundGlowEnabled` | `false` | Off for all existing projectiles, including Artillery Shell |
| `GroundGlowColor` | cyan / blue-white | Color of projected glow for a future energy bolt |
| `GroundGlowDiameter` | `3.5` world units | Full quad diameter, not radius |
| `GroundGlowIntensity` | `0.9` | Additive brightness multiplier |
| `GroundGlowSoftness` | `2.5` | Falloff exponent |
| `GroundGlowFullBelowHeight` | `1.0` world units | Full strength near ground |
| `GroundGlowMaxHeight` | `3.0` world units | Zero glow above this ground clearance |
| `GroundGlowGroundOffset` | `0.03` world units | Small z-fighting clearance above terrain |

Hide irrelevant subfields when disabled, if the project’s existing editor conventions make this straightforward. Clamp or reject invalid/nonfinite values. Existing projectile resources must remain valid and behave identically.

**Alternative:** A single `ProjectileGroundGlowStyle` Resource referenced by the definition may be preferable if several energy projectiles share styling; do not build a heavy generic VFX framework for one effect.

## Performance guardrails

- **No extra work for disabled projectile types:** compile a fast opt-in flag and skip glow-related terrain samples and writes.
- Share a single quad geometry; use batched drawing and shader/material reuse.
- Avoid per-instance `ShaderMaterial` creation, per-shot scene nodes, per-frame arrays/lists or repeated resource lookup.
- Only update active glow instances. Respect the existing projectile pool lifetime and capacity.
- Keep transparent quads modestly sized to avoid expensive additive **overdraw** in dense volleys. Many overlapping glows may brighten/saturate the image: limit maximum intensity and/or visible glow count if needed.
- If a capacity limit is used, **drop only cosmetic glows**, never gameplay projectiles; preserve deterministic/correct combat.
- Perform terrain height queries only for active, enabled, low-altitude glow instances. Profile before introducing complex caching.
- Keep the normal 60 Hz simulation and existing batched rendering behavior; this effect must never introduce a second projectile simulation or change projectile speed/trajectory.
- A small benchmark scenario with glow on/off should make it easy to compare render cost; no FPS target is promised.

## Suggested future implementation scope

**PROJ-GLOW1** should include:
1. One reusable additive radial shader under `GAMEPLAY/PROJECTILES/Shared/` or a suitably named projectile-visual subfolder.
2. Opt-in definition settings and catalogue compilation.
3. Batched ground-glow renderer attached to the existing projectile visual lifecycle.
4. Height-aware position and falloff over flat ground, plus conservative behavior on authored heightfields.
5. Correct spawn/expiry/swap-removal semantics.
6. README and devlog updates; lightweight source/build checks per `CodePreferences.md`.

**Do not** add a laser/plasma unit, rewrite tower hitscan beams, add real dynamic lights, modify existing artillery visuals, invent new weapon balance, or implement complex decal projection merely to exercise the effect. A separate future energy-projectile task can enable the glow once there is an appropriate projectile to display.

## Future Codex handoff

> Implement **PROJ-GLOW1** in `ben0kay/godot-massive-rts`, branch `main`. Read `CodePreferences.md`, `GAMEPLAY/PROJECTILES/README.md`, and this plan. Add optional, pooled/batched fake ground glow quads for **future low-flying travelling laser/plasma projectiles**. Keep it disabled for existing arrows and **high-arc ArtilleryShell**. Preserve damage, movement, collision, simulation and the existing renderer. Use an additive radial ShaderMaterial on horizontally placed quads, terrain-relative height fading, and one shared per-type presentation batch where feasible. Follow lightweight validation; update docs/devlog; commit and push. Do not implement a new projectile type as part of this effect-only task.

## Acceptance criteria (when implemented)

- An appropriate low-flying projectile with the option enabled casts a soft colored ground halo while moving.
- Glow follows X/Z and the **terrain surface**, fades as the projectile rises, and vanishes on impact/expiry.
- Ordinary arrows and high-arc artillery have **no glow and no additional per-projectile cost**.
- Pausing, removing projectiles, resetting battles and pool slot swaps do not leave stale halos.
- Soldier hit logic, projectile speed, range, collision and explosions are unchanged.
- Multiple glow-enabled projectiles render via shared batches with no per-shot nodes/material allocations.
- Overlapping glows remain reasonably readable at normal gameplay zoom; verify visually in Godot.
