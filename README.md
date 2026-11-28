# Godot Massive RTS

**A large-scale 3D isometric tactical auto-battler, army-building sandbox and strategy game built with Godot 4.7 .NET / C#.**

Compose armies, paint their formations onto a battlefield, issue opening orders and watch thousands of autonomous ground units and aircraft fight. The emphasis is on mass-army spectacle, tactical experimentation, readable combat and smooth performance rather than intensive individual-unit micromanagement.

> **Development status — 2026-11-28:** The project has progressed beyond the original BENCH0 prototype. Shared battle simulation, a 22-unit catalogue, tactical encounters, Fortress Defense, map/entity authoring, procedural terrain and numerous combat/visual systems are source implemented. Campaign progression is evolving; the Conquest territory map is planned and currently has a placeholder menu. Individual features remain subject to owner gameplay, visual and performance validation. No hardware FPS or maximum playable army size is guaranteed.

## Vision

Inspired by the mass-army spectacle of *We March as One* and the accessible tactical decisions of a simplified *Total War* battle, the game combines:

- **Mass autonomous warfare:** infantry lines, cavalry, armored vehicles, siege units, artillery, aircraft and support units fighting in the same battlefield.
- **Creative battle authoring:** paint armies, sculpt terrain, build fortified positions, set opening orders and experiment with different compositions and terrain.
- **Tactical depth without excessive micromanagement:** positioning, unit counters, terrain, chokepoints, support coverage and army composition influence the outcome.
- **Distinctive minimalist sci-fi presentation:** geometric silhouettes, faction-colored armor, faceted mechanical models, gunmetal structures and readable combat effects.
- **Multiple strategic experiences:** standalone tactical battles, authored missions, longer base-defense games and a future territory-conquest campaign.

The long-term ambition is to support tens of thousands of units, with 100,000 as a stretch benchmark workload rather than a promised playable count. Gameplay, readability and measured performance take priority over decorative complexity.

This is a separate project from the owner's other Godot tower-defense and base-building projects. Adapt proven patterns selectively; do not import unrelated architecture or protected game assets.

## Launching the project

Import the root `project.godot` using **Godot 4.7 .NET** with the required .NET SDK. The standard non-.NET editor cannot compile the C# project.

| Launch | Purpose |
| --- | --- |
| **F5** | Profile picker and player-facing menus. |
| **F6 — `UI/Editor/GameEditor.tscn`** | Game Editor hub: Level Editor, Entity Editor and Benchmark. |
| **F6 — `UI/Editor/LevelEditor.tscn`** | Direct map authoring and temporary tactical playtests. |
| **F6 — `BENCHMARK/BenchmarkArena.tscn`** | Independent simulation and rendering stress tests. |
| **F6 — `BENCHMARK/RTS/CommandSandbox.tscn`** | Fortress Defense command and gameplay test scene. |

See the [Level Editor guide](UI/Editor/LevelEditor.md), [level authoring guide](LEVELS/README.md) and [Benchmark guide](BENCHMARK/README.md) for detailed controls.

## Game modes

The current player-facing menu separates Campaign, Skirmishes and Fortress Defense. The [end goal](endgoal.md), updated 2026-11-27, defines the intended experience and remaining design decisions.

| Mode | Intended experience | Current development state |
| --- | --- | --- |
| **Campaign → Conquest** | Move persistent armies between territories, capture regions, build fortresses, recruit and research. Auto-resolve encounters or fight them live. | Menu placeholder; strategic territory map and encounter/result loop planned. |
| **Campaign → Missions** | Authored Clash, Offense and Defense missions. Starting armies, mission-specific credit allowances, unit purchases and upgrades. | Menu and authoring destinations exist; reuses existing campaign systems. Mission-specific economy and progression still need alignment with the end goal. |
| **Skirmishes — Instant Action** | Replay standalone battles using fixed authored armies and base stats. Win through deployment, composition already supplied by the scenario, and opening orders. | Standalone menu, level destinations, preset rosters and tactical encounter systems are source implemented. Scenario content and gameplay validation ongoing. |
| **Fortress Defense** | Build and defend a Citadel through escalating waves, using Carbon, workers, production and defenses. | Existing playable command mode and authoring tools; continued gameplay and performance development. |
| **Level Editor / battle sandbox** | Create custom maps, paint large armies and test hypothetical battles with unrestricted editor deployment. | Available through the Game Editor, with temporary playtests. Not a separate player-facing main-menu mode. |

### Shared tactical battle types

- **Clash:** opposing field armies meet; defeat the enemy army.
- **Offense:** advance on an enemy fortress and destroy its Citadel.
- **Defense:** defend the friendly Citadel against a short enemy assault.

These short encounters are shared by Skirmishes and Missions and are intended to become the tactical layer of Conquest. Short Defense battles are distinct from the longer progressive-wave Fortress Defense mode.

The future Conquest map will pass participating armies and encounter objectives into the same tactical simulation, or use a fast auto-resolve path. Both resolution paths should return a common battle result. Persistent casualties, replenishment, retreat rules and map timing remain open decisions.

## Unit catalogue

The [default unit catalogue](GAMEPLAY/UNITS/CATALOG/DefaultUnitCatalog.tres) currently registers **22 unit definitions: 20 ground units and 2 aircraft**. Units use shared authored definitions rather than faction-specific copies.

| Category | Units |
| --- | --- |
| Core infantry | Circle Infantry, Square Infantry, Pikeman, Triangle Archer, Long Triangle Marksman |
| Mobile and elite assault | Diamond Cavalry, Pentagon Elite, Obliterator |
| Siege and ranged specialists | Hexagon Siege Infantry, Light Artillery (Chevron), H Breacher, Twin Rocket Launcher, Prism Lance, Siege Ram |
| Heavy armor | Tank, Dreadnaught |
| Support and utility | Mobile Generator, Rally Commander, Worker |
| Ground anti-air | Interceptor |
| Aircraft | V-wing, Heavy Bomber |

The roster spans basic infantry through Tier 5 super-heavy units. Roles include direct and indirect fire, charge attacks, cleave and area damage, siege attacks against structures, guided anti-air missiles, shielding, rally buffs, air interception and bombing runs.

Unit size, footprint, health, damage, attack timing, speed, range, weapons and visual settings are data-driven. Edit definitions in `GAMEPLAY/UNITS/DEFINITIONS/` or use the Entity Editor as appropriate. Not every unit is available for recruitment, fabrication or authored waves in every mode; eligibility and production recipes are separate from catalogue registration.

For implementation and tuning, see the [unit guide](GAMEPLAY/UNITS/README.md) and mandatory [Unit Implementation Standard](GAMEPLAY/UNITS/UNIT_IMPLEMENTATION_STANDARD.md).

## Combat and tactical systems

The shared battle systems have evolved substantially beyond the original melee/ranged prototype.

- Autonomous targeting, melee engagement, ranged volleys, projectile interception/hits, splash damage and unit-specific attack abilities.
- Ground navigation, obstacle-aware pursuit, line of sight, footprint-aware crowd separation, allied yielding and tier-aware displacement.
- Per-faction opening orders, including Auto Attack and Hold Ground variants; tactical movement goals and local combat behavior.
- Ground and air targeting eligibility, aircraft flight and bombing, guided anti-air weapons and aircraft crash feedback. Aircraft-enabled tactical battles exist; Fortress Defense air waves are not implemented.
- Mobile and stationary shield generators, timed commander rally pulses, structure targeting and siege-specific units.
- Independent weapon mounts on supported units, including the Tank's primary turret and roof minigun and the Dreadnaught's multiple weapons.
- Tactical combat HUD with faction counts, army balance, Citadel health, time/speed controls and a terrain/density minimap in applicable encounters.

### Structures and Fortress Defense

Fortress Defense includes a Citadel, Carbon deposits, workers, extractors, Fabricators, rally/production controls, defensive structures and authored enemy waves. The defensive catalogue includes direct-fire, artillery, rocket, railgun, minigun, shield and anti-air options, including the Missile Barrage and Long-Range AA towers. Walls, barricades and wrecks add battlefield obstacles.

See the [building guide](WORLD/BUILDINGS/README.md) and [Tower Implementation Standard](WORLD/BUILDINGS/TOWER_IMPLEMENTATION_STANDARD.md). Build eligibility, wave support and balance are mode-dependent.

## Level Editor and battle sandbox

The in-game Level Editor is the main authoring environment for custom battlefields, tactical scenarios and Fortress Defense levels. It supports separate Skirmishes, Missions and Fortress Defense save destinations.

| Toolset | Current capabilities |
| --- | --- |
| Army deployment | Faction/unit painting, brushes and rectangle fills, density controls, formation placement, legal deployment areas, deployment lines, army totals and strength estimates. |
| Terrain shaping | Organic elevation brushes, absolute Set Height, tiered elevation, cliff outlines and heightfield terrain. |
| Obstacles and crossings | Water, rocks, forests, industrial walls, light barricades, bridges with footprint-aware crossing limits, and trenches. |
| Scenery and terrain layers | Roads, gameplay marsh, decorative Grassland/Rocky/Marsh undergrowth, geothermal vents and destroyed building husks. |
| Scenario setup | Starting forces, defenses, Citadels, camera settings, timed reinforcement staging, Fortress wave definitions and map rules. |
| Procedural maps | Seeded Temperate presets: Open Plains, River Divide, Mountain Pass and Cliffs, with adjustable elevation, forest and rock coverage, preview and editable generated maps. |
| Editing workflow | Select, move, erase, undo/redo buttons and shortcuts, map sizing, preview, save/reopen and temporary playtests. |

The Clash editor playtest allows unrestricted Blue deployment inside the authored legal area and supports unlimited test airstrikes and repair pods during battle. These are sandbox test controls, not a statement that the same unlimited abilities are available in normal campaign play.

Gameplay-affecting marsh is distinct from decorative Marsh undergrowth. Terrain and obstacle rules vary: water restricts ground navigation, bridges have clearance requirements, and marsh slows ground units according to tier. Terrain height is shared by authoring and applicable battle systems.

See [Level Editor](UI/Editor/LevelEditor.md), [Deployment](GAMEPLAY/DEPLOYMENT/README.md) and [Levels](LEVELS/README.md).

## Visual direction and feedback

Use a real 3D world with an orthographic isometric camera and readable faction colors. Preserve distinct silhouettes while evolving the original geometric primitives into faceted mechanical infantry, vehicles, aircraft and structures.

Source-implemented visual systems include:

- Batched enhanced unit meshes, selected walking/attack animations and two-tone armor.
- White damage flashes, attack pulses, fracture cracks and bounded death fragments.
- Projectile trails, muzzle effects, explosive impacts, scorch marks and shell casing ejection on supported weapons.
- Tank barrel recoil, hull pitch, track effects and independently aimed turrets.
- Directional visual rocking from Heavy projectile damage on surviving, unshielded Tier 2+ units.
- Shield domes, animated energy cores, aircraft exhaust/crashes, varied pine meshes and decorative undergrowth.

Visual feedback must represent simulation events, not determine authoritative combat results. Preserve optional effects, reusable batches and bounded pools where appropriate.

Follow the [Faction Visual Authoring Guide](VISUALS/authoring.md) and [Feedback guide](VISUALS/FEEDBACK/README.md).

## Technical architecture and performance

Large unit counts are the primary technical constraint.

- **Godot 4.7 .NET / C#**, real 3D terrain and camera; authoritative horizontal simulation uses XZ with Y derived from terrain.
- **Data-oriented simulation:** compact unit state, shared update passes and a 60 Hz core fixed-step benchmark configuration, with rendering and expensive work scheduled separately where appropriate.
- **Batched rendering:** MultiMesh instances and shared geometry/materials; avoid individual soldier nodes, physics bodies, timers and independent per-unit processing.
- **Bounded spatial work:** shared ground/air indexes, staggered target acquisition, budgeted long-range queries, route reuse and measured separation costs.
- **Explicit lifecycles:** controlled spawning, death, reset, pooled projectiles/effects and clean scene transitions. Disabled/finished features should become dormant.
- **Shared mechanics:** common unit catalogue, combat, navigation, terrain and rendering across applicable modes; mode-specific economy, objectives and progression remain separate.

For Fortress Defense group commands, immediate order acknowledgement matters more than immediate completion of thousands of costly per-unit operations. Shared group goals and staggered expensive routing are preferred over mass simultaneous pathfinding. See [endgoal.md](endgoal.md) for the detailed command design.

### Benchmark and validation

BENCH0 remains independently runnable and is a permanent development tool, not the current overall project milestone.

It provides repeatable counts from 1k to 100k total units, deterministic seeds, rendering-only, movement, melee and ranged workloads, painted battles and diagnostics for FPS, frame time, simulation cost, targeting, separation, projectile activity and other available measurements.

Keep comparisons like-for-like: same seed, count, roster, settings, terrain, camera and hardware. A successful build or a supported spawn count does **not** establish a playable FPS target. Report performance only when actually measured; the owner performs extensive gameplay and performance acceptance.

See [Benchmark](BENCHMARK/README.md), [Navigation](GAMEPLAY/NAVIGATION/README.md), [Obstacles](WORLD/OBSTACLES/README.md) and [Projectiles](GAMEPLAY/PROJECTILES/README.md).

## Repository layout

```text
GAMEPLAY/    Units, simulation, navigation, combat, projectiles, deployment
WORLD/       Terrain, obstacles, buildings, resources and scenery
LEVELS/      Playable maps, catalogs, runtime and mode-specific progression
VISUALS/     Rendering, combat feedback and camera systems
UI/          Menus, Game Editor, Level Editor and gameplay HUD
SYSTEMS/     Profiles, persistence and shared services
BENCHMARK/   Independent stress tests, scenarios and diagnostics
addons/      Godot editor plugins
```

Keep shared systems with their owning feature rather than duplicating them by mode. Preserve stable IDs, authored levels and existing save compatibility.

## Development direction

The current foundation is the shared simulation and authoring tools. The next stages should build upon that foundation without assuming every roadmap feature is authorized or complete.

- Continue tuning mass-battle performance, navigation, separation, targeting, unit balance and visual readability.
- Expand authored Skirmishes and validate the short Clash, Offense and Defense encounter loops.
- Align the existing Missions campaign with its intended mission-specific starting armies, credit allowances, upgrade rules and progression.
- Prototype Conquest with a small connected territory map, participating army stacks, one fortress and shared live/auto-resolved battle results before expanding its scope.
- Continue Fortress Defense production, commands, wave content and mode-specific integration as requested.

See [endgoal.md](endgoal.md) for the authoritative future design. Historical milestone plans and feature READMEs may predate current implementations; inspect actual source and recent development history before treating old status labels as authoritative.

## AI development workflow

Before editing, read this README, [CodePreferences.md](CodePreferences.md), any applicable AGENTS.md and the relevant feature documentation/source.

- Confirm repository, branch and requested scope; default branch is `main`.
- For new or materially changed units, follow the [Unit Implementation Standard](GAMEPLAY/UNITS/UNIT_IMPLEMENTATION_STANDARD.md). For structures, follow the [Tower Implementation Standard](WORLD/BUILDINGS/TOWER_IMPLEMENTATION_STANDARD.md). For visual changes, consult [VISUALS/authoring.md](VISUALS/authoring.md).
- Reuse existing systems; keep changes focused, modular, performance-conscious and compatible with existing authored content.
- Follow the owner's minimal validation policy. Prefer source/diff review and one basic C# build where practical; extensive gameplay, visual and performance testing belongs to the owner unless explicitly requested.
- Append one concise entry to [devlog.md](devlog.md) for completed source implementations or bug fixes, in the same commit. Documentation-only housekeeping does not require a log entry.
- Prefer one focused commit per authorized task, push to the designated branch unless told otherwise, and report the commit SHA, changes, checks and remaining owner validation.

The root README is a current project overview, not a substitute for feature documentation or a claim of completed gameplay acceptance.
