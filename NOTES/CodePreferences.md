# Code Preferences — Godot Massive RTS

> **Applies only to:** `ben0kay/godot-massive-rts` (Godot 4 .NET / C#, isometric **3D**).
> **Default branch:** `main`, unless the owner explicitly specifies another branch.
> **Start here:** Read the [root README](../README.md) for the game's direction, then the relevant feature README/notes and code. Reference repositories are examples, not authorization to import their architectures.

Adapted from the owner's Godot Tower Defense coding preferences. These rules guide AI-assisted and manual development. **Keep this mass-army game modular, approachable and performance-conscious.** Prefer a simple, understandable solution over a large framework. The **BENCH0 benchmark arena is the first milestone**; do not prematurely build the entire strategy game.

## Before every AI task

1. Confirm the **repository, branch, requested scope and actual files** before editing. Read the root README, this file and relevant feature documentation; check current source instead of trusting old plans. Read `AGENTS.md` if present.
2. Identify the current owner of the feature and reuse shared systems, scene patterns and configuration rather than adding parallel implementations.
3. Keep the change **focused**. Preserve unrelated work, test scenes, authored content, balance and visuals unless explicitly asked to change them. Avoid speculative features and unrelated refactoring.
4. Prefer **one Git commit per authorized job**, not many small commits. Push authorized work by default unless the owner says not to; report the commit SHA and changed files. Never force-reset, force-push or overwrite unrelated changes.
5. Follow the **minimal validation policy** below. Do not let testing or cleanup expand a small task.

## Validation and testing — IMPORTANT OWNER PREFERENCE

**The owner handles extensive testing in the Godot editor. AI agents must not spend lots of time running tests or repeatedly validating work unless the owner explicitly requests it.**

- **Default:** Inspect relevant edits/diff and do a brief source sanity check. For C# changes, **one basic `dotnet build` if readily available and practical** is enough in most cases; for focused risky changes, one small directly relevant smoke check may help.
- **Do not run large regression suites, repeated build/test cycles, extended simulations, prolonged headless Godot runs, visual acceptance passes or performance benchmarks by default.** Leave those to the owner unless explicitly authorized.
- **Exception:** A task explicitly implementing **BENCH0** or requesting profiling/benchmarks can include **bounded, representative benchmark checks** needed to implement the task; do not interpret that as permission to run hours of simulations or claim the owner's hardware FPS.
- For **documentation-only, configuration-only or very small localized** changes, a focused file/diff review is normally sufficient; no game build is needed to prove a Markdown change.
- If Godot/.NET is unavailable or checks would cost disproportionate time, stop at source review. Do not install tooling or start a large troubleshooting effort just for validation.
- **Never claim something was tested or works if it wasn't.** Report what changed, what checks ran, what could not be verified and what the owner should inspect in Godot.
- This policy overrides generic legacy notes requesting extensive regression work. A specific owner instruction for a task takes precedence.

The goal is **careful implementation with minimal AI validation overhead**, followed by the owner's gameplay, visual and FPS testing. A source-implemented feature is not automatically accepted.

## Coding style and readability

- Put a short explanatory comment at the **top of each new script** describing its responsibility.
- Prefer comments **above methods/functions**, with a `// =========================================================` divider for easier navigation in Godot/VS Code.
- Use C# `#region` / `#endregion` for substantial related groups when helpful, not for every tiny method.
- Prefer **moderately vertically compact**, readable C#. Avoid gratuitous abstraction or cleverness that obscures performance/correctness.
- Use descriptive names, focused methods and shared helpers where duplication would otherwise accumulate. Reuse shared update schedulers and spatial queries instead of duplicating them per soldier.
- Group related settings with typed structs/resources and Godot Inspector `[ExportGroup]` / appropriate export attributes. Keep regularly tuned values out of buried hard-coded constants.
- Respect existing naming and folder conventions. Create major feature folders as features are actually implemented; don't reorganize whole directories without a request.

## Folder ownership, scenes and configuration

- Document major implemented feature folders with a brief `README.md`: ownership, important files, data flow, configs and known limitations. Read existing docs first; update when behavior changes.
- Prefer **feature-level master config** for commonly tuned settings using typed C# `Resource` classes / `.tres` where appropriate. Avoid competing configuration systems.
- Keep systems modular and their lifecycles explicit. Plain `Node` containers are fine for scene organization, but container-only nodes don't need processing scripts. Avoid unnecessary autoloads and monolithic managers.
- Prefer explicit signals, interfaces, callbacks or well-defined services over hard-coded scene-tree paths between features.
- Keep **unit definitions** separate from **per-soldier runtime data**, and authoritative simulation outcomes separate from visual presentation.
- Possible ownership: `BENCHMARK/` for reproducible scenarios, `SIMULATION/` for batched state/update scheduling, `UNITS/` for unit archetypes, `COMBAT/` for targeting/damage/projectiles, `RENDERING/` for MultiMesh/feedback, `FORMATIONS/` for deployment/orders, `TERRAIN/` for terrain and height, `UI/` for menus/HUD, `DEBUG/` for tooling, and `TESTS/` for bounded checks. **This list is guidance, not a mandate to create empty folders.**
- Keep BENCH0 independently runnable and useful, even after real battle scenes exist. Don't couple foundational logic to one test arena, one archetype or one camera instance.
- The separate sci-fi survival / tower-defense games are **reference projects only**; don't import their towers, economy, scene architecture or unrelated mechanics by default.

## Performance-first implementation rules

**Before coding ask:** *What wakes this feature? What runs per unit, per batch or per frame? What stops? What is its idle cost? What happens at 1k, 10k, 50k and 100k units?*

- **High unit counts are the core constraint.** Never default to one `Node3D`, `CharacterBody3D`, `_Process`, `_PhysicsProcess`, `Timer`, signal connection or physics collision shape **per soldier**. Exceptional small-count objects need an explicit reason.
- Use **compact data-oriented unit state** and shared simulation passes for movement, target selection, melee, damage, death and render sync. Choose the simplest representation that is correct and measurable.
- Use **MultiMeshInstance3D** or equivalent batching for repeated geometric unit visuals. Separate simulation state from render instances, and update visible transforms/material attributes efficiently. Avoid per-soldier material duplication.
- Avoid O(n²) all-to-all searches. Prefer a shared spatial grid/index and bounded local queries, with correct invalidation when units move or die. Measure before adding complex caches.
- Keep simulation and rendering rates separate where useful; consider fixed-step simulation, interpolation and staggered low-priority work without harming battlefield responsiveness.
- **Idle means dormant.** Don't keep scanning, animating, redrawing or rewriting GPU properties for disabled/hidden/finished systems. Provide a clear event-driven wake path.
- Minimize per-frame allocations, scene-tree searches, resource loads, physics queries, object churn, full-map scans and hot-loop virtual calls. Pool or batch only where it improves observed costs and readability.
- Favor inexpensive data-driven projectiles, bounded effects and stable spawn/despawn. Don't launch a separate scene or physics body for every arrow in massive volleys.
- Maintain deterministic benchmark seeds and modes. Distinguish **render-only, movement, targeting/melee and ranged/projectile** costs. Report FPS, frame time, simulation time, memory and active counts **only when measured**, with test configuration/hardware.
- Avoid spawning, formation placement, reset and mass-casualty frame spikes; budget non-urgent work where practical while preserving immediate combat feedback.
- Keep UI event-driven where possible; don't rebuild counters and panels more often than their data demands.
- Optimize demonstrated hot paths, not imaginary ones. Respect the owner's limited default testing policy; explicit benchmark tasks can run bounded tests.

## 3D isometric world and gameplay reliability

- Match the Godot 4/.NET API actually present. Don't invent APIs, signals, renderer assumptions or thread-safety guarantees.
- Use C# `foreach (var item in items)` and conventional indexed loops `for (int i = 0; i < count; i++)`. Inspect unfamiliar syntax before committing.
- Treat **XZ** as the principal gameplay plane and **Y** as height. Do not couple the authoritative unit position to screen pixels or camera angle. This makes later elevation, camera variation and tactical queries viable.
- The initial camera is **orthographic isometric**, not fake 2D isometric. Keep camera transforms, panning, zoom and world selection consistent with the simulation coordinate system.
- Begin with flat ground, but avoid assuming terrain height is permanently zero in every subsystem. Later terrain changes should pass through a well-defined height/query interface rather than forcing a rewrite.
- Keep targeting, movement, melee range and projectile hit logic coherent across elevation changes; don't let decorative visuals silently alter combat outcomes.
- Keep scene-tree and rendering mutations on allowed threads. If background jobs are introduced later, bound/cancel them and discard stale results when scenes reset or unload.
- Preserve stable resource paths, scene references, signals, exported properties and IDs where saves/authored content/tests depend on them.
- Respect pause, benchmark reset, unit removal, scene transitions and cleanup. Timers, projectiles, render entries and callbacks must not continue consuming work after their owners disappear.
- Plan clear combat feedback: distinct red/blue factions and white/light damage state, with high readability at large camera distances. Damage appearance must reflect simulation state, not drive it.

## Documentation, plans and AI handoff

- Consult relevant feature notes and any active plans/backlog when touching planned milestones. **Old notes may be superseded**; distinguish code that exists from designs or unverified claims.
- For substantial tasks, divide into a few independently reviewable phases with **scope, affected owners, expected behavior, tuning knobs, performance considerations and owner acceptance checks**. Do not implement future milestones without authorization.
- Use clear status labels: **Planned / In Progress / Source Implemented / Awaiting Owner Validation / Accepted**. A commit, build or headless run does not equal gameplay or FPS acceptance.
- Update relevant feature README and small status notes after an authorized implementation. Don't turn localized work into a full documentation audit.
- When reporting back, include **what changed, key settings/paths, commit SHA, lightweight checks performed, measured results only if actually measured, and specific owner-side tests**.

## Reference projects

The owner's 2D tower-defense, other Godot projects and public mass-army/RTS games are useful inspiration. **Adapt proven performance patterns; don't copy assets, protected UI, unreviewed third-party add-ons, unrelated mechanics or entire architectures.** The actual files in `godot-massive-rts` and this game's goals are authoritative.

**Bottom line:** Build the 3D isometric mass-army game as a clean, maintainable, high-scale simulation. Start with BENCH0, keep changes focused, validate proportionally, prefer one commit and leave full gameplay/performance acceptance to the owner.
