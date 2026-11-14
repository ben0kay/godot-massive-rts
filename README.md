# Godot Massive RTS

> **Status:** BENCH0 — **Source Implemented / Awaiting Owner Validation**. F5 opens the FRONT0 profile picker. F6 runs the standalone benchmark with its arena scene open. See [BENCHMARK/README.md](BENCHMARK/README.md) for launch instructions, controls, architecture and measurement limitations. No hardware performance targets have been validated.

## Vision

Create a **3D isometric, large-scale real-time army battle game** in **Godot 4 .NET / C#**, inspired by the *mass-army spectacle* of We March as One and the accessible tactical decisions of a simplified Total War battle.

The distinguishing goal is **very large battles using simple geometric units** rather than detailed character models. Blue and red forces should form lines, collide, fire volleys, take damage and visibly reshape the front line. Gameplay and readability come before decorative detail.

This is a **separate project** from the owner's sci-fi base-building / tower-defense projects. Do not merge their gameplay, folder structure or architecture into this game by default. Ideas and proven patterns can be adapted selectively.

## Visual and gameplay direction

- **World:** Real 3D battlefield with an **orthographic isometric Camera3D**; camera pan/zoom and clear large-scale readability. Start flat, but design positions and camera for future terrain height.
- **Art:** Minimalistic shapes (triangles, wedges, cubes, cylinders and similar primitives). Strong team colours: **blue vs red**, neutral/white damage feedback, modest readable effects. The reference screenshot is a visual direction, not an asset to reproduce verbatim.
- **Scale:** Thousands of simultaneous soldiers initially, with a stretch ambition of **tens of thousands and potentially 100,000** depending on measured hardware performance. No unverified FPS claims.
- **Units:** Start with melee and ranged/archer archetypes, each visibly distinct despite simple geometry. Ranged attacks have lightweight, visible projectiles; melee uses close-range engagement.
- **Control:** Army composition and formations, eventually including paint/draw deployment, formation placement and simple tactical orders. Automated individual combat should remain approachable.
- **Combat:** Health/damage, targeting and deaths, projectile volleys, faction separation and feedback. Visual-only feedback must not determine authoritative outcomes.
- **Terrain (later):** Hills, elevation, slopes, chokepoints and high-ground effects without rebuilding the simulation.
- **Progression, economy, campaigns and advanced formations:** Possible future features, **not approved for the first implementation**.

Do not copy another game's protected artwork, interface, audio or code. Build an original interpretation of the broad gameplay concepts.

## First milestone: BENCH0 — Stress Test & Benchmark Arena

**BENCH0 comes before gameplay production.** Codex's first implementation task should be an independently launchable benchmark/test scene, not the whole game. It should be useful even while the battle rules are incomplete.

### Benchmark arena requirements (source implemented; awaiting owner validation)

1. A simple, large, flat 3D battlefield and fixed-angle orthographic isometric camera with pan/zoom.
2. Basic blue/red primitive soldiers rendered in batches, ideally **MultiMeshInstance3D**, with no per-soldier scene-tree nodes or physics bodies.
3. Explicit, repeatable soldier counts (e.g. **1k / 5k / 10k / 25k / 50k / 100k**) with safe caps based on memory and actual performance. Spawn/despawn/reset controls and a deterministic seed.
4. Separate reproducible modes: **render-only**, **movement-only**, **melee/targeting**, and **ranged/projectiles**. This distinguishes renderer limits from simulation costs.
5. A visible debug overlay for FPS, frame time (including spikes), active units by faction, active projectiles, simulation/update time, rendering/instance counts where available, and relevant memory/allocations. Mark metrics unavailable rather than estimating them.
6. Simple representative stress scenarios: two opposing lines, clustered combat and high-count moving crowds. Avoid unfairly trivial static-only benchmarks.
7. Keep benchmark configuration together and clearly documented; the scene must remain usable as later systems evolve.

**Acceptance is based on the owner's real editor/gameplay tests and measured results.** Unit counts above are test sizes, not promised performance. Benchmarking is an explicit part of BENCH0, but later unrelated changes still follow the lightweight validation policy in `NOTES/CodePreferences.md`.

## Technical direction

- **Godot 4 .NET / C#**; native 3D world and orthographic camera.
- Maintain soldier simulation in a compact **XZ ground plane** (2D coordinates in world space) and derive the vertical Y from terrain height when elevation arrives.
- Favor **data-oriented, batched unit state** over one `CharacterBody3D`, `Node3D`, `_Process` callback or physics collision per soldier.
- Batch same-mesh/same-material drawing with **MultiMeshInstance3D** where appropriate; profile batch updates rather than assuming they are free.
- Shared, bounded spatial queries and targeting; avoid all-to-all O(n²) checks. Prefer a measured uniform grid or similar spatial index.
- Explicit lifecycle for spawning, dying, despawning, projectiles, effects and benchmark resets. Minimize allocations and spikes in hot loops.
- Fixed-step simulation decoupled from rendering where practical; profile correctness and visual smoothness at different frame rates.
- Keep simulation, rendering, formations, combat, UI and terrain modular, without overengineering a reusable engine before there is a need.
- Future elevation must account for more than visuals: movement cost, melee reach, projectile trajectories and line-of-sight can be added in later milestones.

### Folder structure — capitalized major systems

**Required convention:** Keep major game systems in **UPPERCASE top-level folders**, matching the owner's other Godot projects. Organize related subsystems, scenes, scripts, resources and assets **inside the owning major folder**, not as dozens of unrelated root files or global `Scripts/`, `Scenes/` and `Resources/` dumps.

Example **planned** ownership (create only folders needed by the authorized task):

```text
BENCHMARK/
    SCENES/           # BenchmarkArena.tscn and other benchmark scenes
    SCENARIOS/        # Line, cluster and distributed test setups
    UI/               # Benchmark controls and overlay
    CONFIG/           # Benchmark settings and presets
SIMULATION/
    UNITS/            # Runtime unit arrays/state
    MOVEMENT/         # Batched motion
    SPATIAL/          # Local queries / spatial index
RENDERING/
    UNITS/            # Instanced soldier geometry
    PROJECTILES/      # Batched projectile visuals
COMBAT/
    MELEE/
    RANGED/
UNITS/                # Unit archetypes, definitions and authored assets
FORMATIONS/           # Deployment and orders
TERRAIN/              # Terrain, height queries and future elevation
CAMERA/               # Isometric camera controls
UI/                   # Shared gameplay menus/HUD (not benchmark-specific UI)
DEBUG/                # Shared instrumentation/debug tools
NOTES/                # Plans and CodePreferences.md
TESTS/                # Small automated checks, only when needed
```

**BENCH0 specifically:** Place its arena, scenarios, controls and benchmark-only settings under `BENCHMARK/`. Reusable simulation, renderer and camera components belong under their own major system folders. Subfolders group related concerns; create them only when actual files exist. Avoid unnecessary nesting, empty folders and cross-system duplicates.

## Soldier roster

**UNIT0 is source implemented and awaiting owner validation.** Both factions can field Circle (basic melee), Square (stronger melee), Equilateral Triangle (ranged), Diamond (fast cavalry), Pentagon (large slow elite infantry), Hexagon (siege infantry with a radial stomp), and Chevron (heavy artillery). Their faceted meshes use faction tints and share instanced batches.

Edit the seven resources under `UNITS/DEFINITIONS/` in Godot. UNIT1 compiles the Inspector’s **Size Multiplier** into each type’s mesh dimensions, footprint and melee reach. Diamond Cavalry defaults to 1.25×. See the [catalogue guide](UNITS/README.md) and [starting roster](UNITS/DEFINITIONS/STARTING_ROSTER.md).

## ART1 — Heavy Artillery

Heavy Artillery is available in the benchmark's deployment painter and the Unit Editor. Its chevron body is 20% wider and taller than Elite Infantry, with a projecting barrel. It fires over cover at 12–70 units. Its 7-unit blast shares the shockwave damage query and expanding ring. See [PROJECTILES](PROJECTILES/README.md) for flight and impact tuning. Source implemented, awaiting owner validation.

## SEP1 — Soldier Separation

**Source implemented and awaiting owner validation.** BENCH0 offers live **Off / Normal / Strong** controls. SEP1 applies bounded footprint corrections across both armies and reports CPU time and query caps. Standard contacts split corrections by effective footprint radius. In a Circle–Pentagon pair, the Circle takes about 69% of the correction. The practical test target is about 10,000 vs 10,000, and larger benchmark sizes remain available. See the [SEP1 plan](SIMULATION/SEPARATION/SEP1_IMPLEMENTATION_PLAN.md).

## SEP2 — Allied crowd yielding

**Source implemented and awaiting owner validation.** With Allied archer yielding enabled, advancing melee can use half clearance against stationary friendly ranged units. Both factions follow the same rule. Opposing pairs keep SEP1 clearance. See the [SEP2 plan](SIMULATION/SEPARATION/SEP2_IMPLEMENTATION_PLAN.md).

## FB0 — Soldier Hit Feedback

**Source implemented and awaiting owner visual validation.** The [FEEDBACK](FEEDBACK/README.md) system owns the 0.12-second white damage flash and the grounded 6% visual pulse, which returns to normal over 0.18 seconds. Both effects use the existing MultiMesh instances.

## UNIT1 — Soldier size scaling (planned)

[UNIT1](UNITS/UNIT1_SIZE_SCALING_PLAN.md) proposes a per-type `SizeMultiplier` in existing soldier `.tres` definitions, compiling uniform visual size, gameplay footprint, melee/cleave reach and trample contact size once. The target is **1.25× Cavalry** with unchanged damage, movement speed and 2-second acceleration. **Plan only; not yet implemented.**

## OBST1 — Static obstacle navigation and line of sight

**Source implemented. Awaiting owner validation.** Paint dark-gray rectangular walls in the deployment editor or enable **Test wall in preset** for a repeatable benchmark. Walls block ground movement and direct attacks. See [OBSTACLES](OBSTACLES/README.md) and [NAVIGATION](NAVIGATION/README.md).

## Movement baseline

Edit **Movement Speed Multiplier** in [DefaultSimulationSettings.tres](SIMULATION/CONFIG/DefaultSimulationSettings.tres) and restart the arena. The default is **0.8×**. The arena compiles soldier, charge and projectile speeds from their authored values. Set it to **1.0×** for authored speed. Attack timing, damage and range use their own settings. A future battle time control can use a separate setting.

## TERR1 — Painted and procedural elevation (planned)

[TERR1](TERRAIN/TERR1_IMPLEMENTATION_PLAN.md) designs a shared chunked heightfield with **two producers**: painted flat plateaus/smooth slopes for Army Painter and deterministic gentle procedural landscapes with protected flats for Open RTS. Both eventually share slope-aware movement, high-ground bonuses and terrain LOS. **Planning only; flat simulation remains unchanged.** Build in phases A (heightfield), B (painter), C (procedural) and D (combat/nav).

## FRONT0 — Profiles, main menu and campaign levels

**Source implemented. Awaiting owner validation.** Run Project (F5) opens the local profile picker, main menu, campaign selector, and display options. Campaign opens a catalogue-driven mode picker with Fortress Defense and Preset Battles. Each has its own blank [Level 001 scene](LEVELS/README.md). Open `BENCHMARK/BenchmarkArena.tscn` and use Run Current Scene (F6) to test the Auto-Battle Benchmark. Open `BENCHMARK/RTS/CommandSandbox.tscn` with F6 to test Fortress Defense squad movement and formation commands. See the [UI guide](UI/README.md) for menu settings and save ownership.

## Development order (proposed, not completed)

1. **BENCH0:** Independently launchable benchmark room, batched visuals, repeatable unit counts and instrumentation.
2. **SIM1:** Compact moving units, faction state, shared spatial queries and deterministic fixed-step update.
3. **COMBAT1:** Melee, ranged units, lightweight projectiles, damage/death and clear feedback.
4. **FORM1:** Formation placement/painting, simple orders and readable battle control.
5. **TERRAIN1:** Terrain elevation, slopes, navigation constraints and appropriate combat interactions.
6. **GAME1+:** Battle objectives, army composition, progression and polish, as separately requested.

These milestone names establish planning vocabulary; they do **not** authorize implementing all stages at once.

## AI development workflow

Before a task, read the root README, [Code Preferences](NOTES/CodePreferences.md), any `AGENTS.md` and relevant feature documentation. Confirm the actual project state and work only on the requested milestone.

Prefer one focused commit per task; push authorized changes to the designated branch and report the SHA, affected files, basic checks and any owner-side Godot testing. Don't mistake a successful build for visual or performance acceptance. Keep the new project small, modular and measurable from day one.
