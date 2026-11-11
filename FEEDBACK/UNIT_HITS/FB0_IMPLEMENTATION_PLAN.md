# FB0 — Centralized Soldier Hit Feedback (White Flash + Size Pulse)

**Repository:** \`ben0kay/godot-massive-rts\` · **Branch:** \`main\`
**Status:** PLANNED — this file is an implementation brief, not live code.
**Owner:** \`FEEDBACK/UNIT_HITS/\`

## Objective

Move the existing soldier *damage visual* logic into a discoverable, modular feedback system. Preserve the current immediate **white hit flash** and add a **very subtle, short-lived physical-looking scale pulse** on nonlethal damage. Keep combat and positional simulation authoritative. Design the \`FEEDBACK/\` root to later contain \`UNIT_DEATHS/\` (debris and particles), \`CAMERA_SHAKE/\`, \`IMPACTS/\`, etc., but **do not implement or create empty future folders now**.

This is a focused gameplay/organization job: implement only **FB0**, not SEP2, new particles, corpses, camera shake, damage numbers, general shader rewrites, animation trees, physics effects or gameplay size changes.

## Suggested folder hierarchy (use actual content, not placeholders)

\`\`\`text
FEEDBACK/
  README.md
  UNIT_HITS/
    FB0_IMPLEMENTATION_PLAN.md
    HitFeedbackSystem.cs         # hit registration, lifetime, visual evaluation
    HitFeedbackSettings.cs       # Inspector-editable settings/resource
    HitFeedbackSettings.tres     # optional shared default asset, if useful
  UNIT_DEATHS/                  # FUTURE, do not create yet
  CAMERA_SHAKE/                 # FUTURE, do not create yet
  IMPACTS/                      # FUTURE, do not create yet
\`\`\`

Keep major root folders uppercase, as per project policy. \`FEEDBACK/\` owns **visual feedback behaviour and styling**. \`SIMULATION/\` owns authoritative unit health and compact per-unit state, \`COMBAT/\` owns damage/hits, and \`RENDERING/\` still owns MultiMesh GPU uploads. Do not duplicate core combat/renderers in \`FEEDBACK/\` or create a Godot Node per soldier.

## Read live code before coding

- \`README.md\`, \`NOTES/CodePreferences.md\`, any \`AGENTS.md\`.
- \`SIMULATION/UnitState.cs\`: \`Flash[id]\`, \`Dirty[id]\`, and \`Damage(int id, float damage, float flashSeconds)\`; this currently resets flash to the passed duration.
- \`SIMULATION/MovementSystem.cs\`: currently decrements \`Flash[id]\` inside its all-units movement loop, even for stationary soldiers.
- \`RENDERING/ArmyRenderer.cs\`: \`Write\` selects \`Colors.White\` when \`Flash[id] > 0\`; \`Upload\` has *two* paths: direct 16-float \`MultiMesh.Buffer\` upload for dense changes, and \`SetInstanceTransform\` plus \`SetInstanceColor\` for sparse changes.
- \`BENCHMARK/CONFIG/BenchmarkConfig.cs\`: currently exposes \`FlashSeconds = 0.12\`.
- \`BENCHMARK/BenchmarkArena.cs\`: fixed-step ordering, pausing/reset, and renderer sync.
- \`COMBAT/CombatSystem.cs\` and \`PROJECTILES/\` damage call sites: hit triggers must include melee and projectile damage; do not attach hit feedback only to one damage type.
- \`UNITS/CATALOG/UnitCatalog.cs\` and \`RENDERING/FacetedSoldierMeshes.cs\`: authored per-type height and centered prism mesh generation.

Use the current \`main\`, which may have additional SEP2/projectile changes after this plan was authored. Do not undo them.

## Expected visuals — editable starting values

| Setting | Default | Meaning |
| --- | ---: | --- |
| WhiteFlashDuration | **0.12 seconds** | Keep the original immediate white damage flash |
| PulseDuration | **0.18 seconds** | Total visual expansion + return |
| PulseMaxScale | **1.06×** | 6% larger at the peak; should look subtle |
| PulseRiseFraction | **0.20** | Grow rapidly during the first 20% of the pulse |
| PulseEnabled | **true** | Easy to disable for comparative benchmarks |

These should be editable in a single Inspector-visible \`HitFeedbackSettings\` resource owned by \`FEEDBACK/UNIT_HITS/\`, ideally one shared config for both factions and all five types. Do **not** add independent resources per soldier.

- Start the white flash **immediately** on a positive hit.
- Make the sprite/mesh grow quickly, then ease smoothly back to exactly **1.0×** original scale.
- Scale *the rendered 3D instance only*. **Do not change** \`UnitStats.Width\`, \`Height\`, \`Footprint\`, attack ranges, separation radii, targeting, projectile collision, deployment clearance, hit points, or gameplay coordinates.
- A pulse should expand approximately equally on X/Z/Y; it must appear **ground-anchored**, not sink through the battlefield. Because the mesh is authored about its center, place the instance centre at \`Position.Y + halfHeight * scale\`. Nonuniform XZ-only scale could be a later stylistic choice but is **not** requested now.
- Flash tint should retain the existing baked vertex-face shading; setting MultiMesh instance tint to white still leaves the side faces darker, just as today.
- Consecutive hits while an effect is active should refresh the visual response cleanly, not accumulate multiplicative scale or grow without bounds. A hit while fully flashing should maintain/restart white rather than flicker for one frame.
- On death, the renderer currently removes the soldier; no separate death puff/debris yet. Ensure pulse state does not resurrect, delay deletion, or leave stale effects.
- Pause freezes simulation-driven animation; reset clears all feedback state. Rendering-only should keep baseline visuals and avoid feedback update overhead when no combat hits occur.

## Centralized ownership and minimal wiring

Provide a small \`HitFeedbackSystem\` owning the **meaning** of a hit effect:

1. **OnHit** (per-soldier event) — reset/retrigger flash and pulse timers after actual damage.
2. **Advance** (sim time) — tick active timers down, clamp to 0, and set \`Dirty\` while the instance's visual must update. Ensure the final return to normal is written at least once.
3. **Evaluate** — return whether white flash is active and a *visual-only* \`scale >= 1\` for a soldier, using cheap math. Keep this independently testable.

Store state in compact arrays keyed by soldier ID, either inside \`UnitState\` (e.g., existing \`Flash\` and a new pulse timer) or in a single feedback-system object sized to capacity. Avoid one allocated object per hit or soldier. Trigger \`OnHit\` in the authoritative damage path so projectile and melee hits share the same effect without duplicated rules.

Integrate \`Advance\` with the fixed-step tick in \`BenchmarkArena\`; remove the timer decrement currently embedded in \`MovementSystem\` after centralizing it. A simple bounded O(N) scan of compact timer arrays each tick is acceptable initially, but profile it against high unit counts. If it is measurably expensive at 20k units, consider a deduplicated active-ID array/list; avoid a big new event framework or complex manager prematurely. Do not use Godot \`Timer\`, \`AnimationPlayer\`, a new shader per soldier, C# lambdas per hit, \`Node3D\` per unit, or dynamic materials.

**Important compatibility:** Existing \`UnitState.Damage\` receives \`flashSeconds\`, and \`BenchmarkConfig\` exposes \`FlashSeconds\`. Decide one authoritative settings owner in \`FEEDBACK/UNIT_HITS/\`. Adjust call sites safely or retain a documented compatibility bridge; do not leave two conflicting flash duration controls. Never change actual applied damage during refactoring.

### Suggested pulse envelope (illustrative, not compiled)

Use a cheap piecewise envelope. If \`p = elapsed / pulseDuration\` in [0,1], with \`riseFraction = 0.20\`:

\`\`\`csharp
float rise = Mathf.Clamp(p / riseFraction, 0f, 1f);
float x = Mathf.Clamp((1f - p) / (1f - riseFraction), 0f, 1f);
float easedFall = x * x * (3f - 2f * x);
float scale = 1f + (pulseMaxScale - 1f) * rise * easedFall;
\`\`\`

The unit returns to scale 1 at p=1. Clamp all inputs; ensure pulse duration cannot be zero/NaN. Compute only while the pulse is active; otherwise return exactly 1.

## Mandatory \`ArmyRenderer.cs\` integration — both upload paths

Current MultiMesh instance buffers use a **16-float stride**: 12 floats of 3D transform (three affine rows with scale diagonals at offsets **0, 5, 10** and Y position at offset **7**), followed by RGBA at 12–15. Current transforms are identity-scaled and have \`Position.Y + halfHeights[type]\` as their center.

**The easy-to-miss bug:** implementing a scale only in \`SetInstanceTransform\` updates sparse instances but **not** dense buffer uploads. Changing only the dense buffer diagonals likewise breaks sparse updates.

Required in \`Write(id,batch,slot)\`:
- Ask \`HitFeedbackSystem\` for the current visual scale for this ID, clamp near [1, 1.06].
- Set packed transform diagonals \`buffer[offset + 0]\`, \`[offset + 5]\`, \`[offset + 10]\` all to \`scale\`.
- Set Y translation \`buffer[offset + 7] = units.Position[id].Y + halfHeights[type] * scale\`, keeping the mesh's bottom at the ground.
- Keep X/Z translations and packed faction/white colours unchanged except for current flash effect.
- Ensure slot-swap after death writes the relocated soldier's correct scale, and restores 1.0 after a pulse ends.

Required in sparse \`Upload(batch)\`:
- Reconstruct each instance's **actual scale basis**, not \`Basis.Identity\`, e.g., \`Basis.Identity.Scaled(Vector3.One * scale)\`, where the scale comes from the packed buffer diagonal.
- Pass the buffer's correct XYZ translation to \`Transform3D\`; keep per-instance colour.
- Bulk \`MultiMesh.Buffer\` uploads already contain the scale diagonals/Y translation if \`Write\` updated them properly.
- Do not change the GPU rendering architecture or disable sparse/dense optimization. Use shared meshes, shared material and existing faction-colour/flash multipliers.

No scaling is to be applied to authoritative \`units.Position\`. Renderer transforms alone own the visual pulse.

## Performance and stress precautions

- The primary practical target remains up to **10k blue + 10k red** (~20k total), including busy ranged combat and SEP1 collision separation. The 25k/50k/100k benchmark options remain accessible; no FPS target is promised.
- A 0.18-second pulse at 30 Hz lasts roughly 5–6 simulation ticks; visual updates for active hits are reasonable. Avoid marking every soldier dirty unconditionally.
- OnHit, Advance and Evaluate should allocate **zero objects per hit/tick**, except optional initial capacity arrays. Do not use a scene node, particle emitter or signal connection for each unit.
- If a melee clash flashes many units simultaneously, the existing renderer may transition from sparse per-instance updates to dense buffer uploads, which is expected. Do not rebuild meshes or material resources per hit.
- Retain \`ArmyRenderer.UploadMilliseconds\`, \`DenseUploads\`, \`SparseUploads\`, \`ChangedWrites\` and BENCH0 overlay metrics. Optionally show number of actively pulsing units if cheap; no new full-field stats scan solely for the overlay.
- Compare white-only baseline vs white+scale under the same battle seed and living counts, especially 5k/10k/20k total. Only state FPS differences if measured on owner machine.
- Avoid a shader-based pulse unless measurements show it is better; current direct per-instance transform code already supports it without new material architecture.

## Owner acceptance checks

1. Circle, Square, Triangle, Diamond and Pentagon all flash white on **nonlethal melee or ranged damage** as before.
2. Every hit produces a brief **~6% scale pulse**, peaking rapidly and smoothly returning to *exactly* its original visible size.
3. No soldier becomes permanently giant, underground, floating, or permanently white after repeated hits.
4. Stationary ranged soldiers still pulse even if they never change movement position.
5. Dense battles, death slot-swap, reset, pause and repeat spawning work; dying soldiers disappear as before.
6. SEP1/SEP2 crowd separation and targeting do not react to the visual-only size pulse. No change to combat damage or foot-space.
7. Check both **sparse uploads** (few visible hits) and **dense uploads** (many simultaneous hits), including post-pulse reset.
8. BENCH0 rendering/latency metrics still work and there is no new per-soldier scene node or timer.

## Delivery instructions for Codex

Read the root README, \`NOTES/CodePreferences.md\`, current source listed above and this plan before editing. Implement **FB0 only** with minimal edits to damage trigger, lifetime update and renderer visual transform. Document \`FEEDBACK/\` ownership and effects, link from relevant renderer/benchmark READMEs, and append a concise **FB0 source implemented / awaiting owner visual validation** note to the root \`devlog.md\` in the implementation commit. Perform light source review and at most one readily available \`dotnet build\`; owner will test in Godot. Commit once and push to \`main\` without rewriting unrelated work. Report SHA and owner-side checks.

**Do not mark FB0 complete until code exists.**
