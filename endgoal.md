# End Goal — Godot Massive RTS

**Status:** Long-term design proposals only — not approved implementation tasks.
**Updated:** 2026-11-11
**Repository:** `ben0kay/godot-massive-rts` (`main`)

## Current priority: simulation and performance

The project is currently a high-unit-count Godot 4 C# 3D isometric army simulation and benchmark. Continue improving and measuring the existing shared simulation before building either complete game mode.

**Next intended experiment:** [OBST1](OBSTACLES/OBST1_IMPLEMENTATION_PLAN.md) now provides a scoped implementation plan for paintable large, simple dark-gray rectangular obstacles, shared navigation around them and direct-fire line of sight. Use it to observe movement, crowding, routing and FPS/CPU costs with large armies. **Plan only; not yet implemented**. This does not authorize the other future game modes.

Preserve the existing benchmark room, compact unit arrays, batched MultiMesh rendering, shared spatial queries, separation, melee/ranged combat, cavalry, and profiling tools. The owner has reported good performance around 20,000–25,000 units in some workloads, but that does not establish performance for dense combat, obstacle routing, or RTS control.

## Overall vision

Two distinct game modes built on **one shared authoritative soldier simulation**, not two independently maintained movement/combat engines. Soldiers are simple geometric shapes in huge blue/red (and potentially additional faction) armies. Prioritize accessible large-scale strategy, visual clarity, automated individual combat, and performance.

### Mode 1 — Army Painter / finite-level tactical battles

- Authored levels contain an already-positioned enemy army and battlefield.
- Player receives a **finite supply or budget of troops**, choosing unit types, counts, and where to paint/deploy them.
- Tactical challenge: composition, placement, terrain, and timing before battle.
- Start the battle and let troops **fight automatically** using existing movement, targeting, separation, combat, and projectile systems.
- Basic vision does **not** require box selection, right-click movement orders, building production, mining, or an RTS economy.
- Potential objectives include defeating the enemy force or protecting a location; exact victory rules are future design decisions.
- Existing painted-deployment tooling is a starting point, not a finished game mode.

### Mode 2 — Open RTS / autonomous army production

- Player begins with a **small base**, possibly with a few defensive walls, and grows an army and economy.
- Build **barracks** that automatically manufacture/spawn geometric soldiers; a basic barracks could continuously produce **Circle Infantry**.
- Add resource-gathering workers/miners, perhaps visually represented by a **pickaxe symbol**. They can mine deposits and/or chop trees and bring resources into the economy.
- Players place additional buildings, walls, and **stationary defensive towers** to protect their base.
- Each faction has a central **Citadel**; a core proposed victory condition is destroying the opposing team's Citadel.
- Units should generally be autonomous: they leave production buildings, find enemies, and fight without requiring constant micromanagement.
- Optionally give players **lightweight direct control of selected groups** for tactical positioning. This should supplement rather than replace automatic combat.
- Potential future systems: different production buildings and unit types, economy tuning, build limits, defensive layouts, additional resources, upgrades, and multiple factions. None is committed to implementation yet.

## Lightweight group selection and commands (Open RTS only)

Desired user interaction:

1. Left-click and drag a rectangle to select soldiers (for example **3,000**).
2. Right-click a point on the terrain to issue a movement destination for the selected soldiers.
3. Soldiers head toward that point while preserving lightweight separation and their existing combat behavior.
4. If enemies are within engagement range or encountered en route, soldiers can fight; a future distinction between ordinary Move and Attack Move can determine whether they stop to pursue enemies.
5. After a local engagement, surviving soldiers can resume their destination order. Avoid expensive per-soldier pathfinding.

**Responsiveness is not required to be instantaneous.** The owner explicitly accepts spreading order processing over many frames, even if a very large group takes **up to roughly three seconds** to fully respond. Example concept: process 10 soldiers on one frame, another 10 on the next, etc. These are illustrative batch sizes, **not fixed implementation parameters**: 3,000 / 10 per frame = 300 frames, which is five seconds at 60 FPS. To finish within three seconds at 60 FPS requires an average of about 17 soldiers per frame; at 30 FPS, about 34. Prefer a configurable **time/work budget** and measure it rather than hard-coding a number of soldiers per rendered frame.

### Performance-first design guidelines

- **Record the player's group command immediately**, then queue and stagger costly per-soldier assignments, formation offsets, navigation decisions, or route updates. A shared destination or group order can avoid many independent target calculations entirely.
- Keep selection and order data compact: soldier IDs, group/order IDs, shared destinations, and small per-unit flags/offsets where needed. Do not create one Godot Node, NavigationAgent, physics body, timer, or pathfinding object per soldier.
- Separate **movement destination/order** from **combat target**; currently combat pursuit often uses an enemy target directly, while benchmark movement uses `Goal`. Orders should coexist with autonomous enemy acquisition.
- Reuse `SIMULATION/` movement, spatial queries, separation, and combat; a proposed `FORMATIONS/` or orders subsystem should decide intentions, not duplicate the simulation.
- Stagger *expensive work*, not essential correctness: units should remain valid, dead IDs must be ignored, and new commands must cancel/supersede obsolete queued commands. A selected unit may continue its previous behavior until its new order is processed.
- Avoid unnecessary full-field selection scans every rendered frame. Throttle drag-selection previews as needed and finalize on release.
- Prefer shared routes, group-level waypoints, or flow fields for future obstacle-aware navigation; avoid per-soldier A* at army scale.
- Preserve unit-specific mechanics such as acceleration, cavalry momentum/trample, ranged standoff, and crowd separation.
- Ensure disabled gameplay systems truly sleep or are not instantiated in modes that do not use them.
- Keep benchmark scenarios independent so new features can be compared at 1k, 10k, 25k, 50k and beyond; no guaranteed FPS claims.

## Proposed mode architecture

- **Shared:** compact unit state, movement, targeting, spatial grid, separation, combat, projectiles, rendering, terrain/height queries.
- **Army Painter:** finite troop budgets, painted deployment, authored enemy formations, level objectives, automatic battle.
- **Open RTS:** box selection, optional group orders, buildings, barracks production, worker harvesting, resources, walls, defensive towers, Citadels and victory rules.
- **Mode configuration:** explicitly enable relevant systems. Army Painter should not pay for inactive RTS selection, order processing, economy, or building production.
- Preserve the independently runnable `BENCHMARK/` arena and existing profiling.
- Follow `NOTES/CodePreferences.md`: uppercase major folders, modest modular changes, data-oriented hot paths, tunable resources, lightweight validation, and no speculative implementation without approval.

## Possible future development sequence (not authorization)

1. **Simulation obstacle experiment:** a large rectangular obstacle and measured crowd/navigation costs.
2. **Basic orders prototype:** drag-select, right-click shared destination, budgeted/staggered command processing, and autonomous fighting along the way.
3. **Game-mode separation:** two gameplay configurations using the same simulation; keep BENCH0 independent.
4. **Army Painter game loop:** finite troop supply, authored levels, start/retry, and victory/defeat.
5. **Open RTS prototype:** Citadels, a basic barracks producing Circle Infantry, simple walls, and automatic attack behavior.
6. **Economy and defense:** pickaxe workers, mining/tree harvesting, resource delivery, additional buildings, and static towers.
7. **Later sophistication:** shared obstacle navigation, formations, smarter orders, terrain elevation, production/economy balance, and polish.

**Design principle:** prioritize very large, mostly autonomous battles. Give the player strategic control at the group/base level without turning every soldier into an expensive independent agent.

## Style and inspiration — UEBS2 meets Age of Empires

The intended feel is **Ultimate Epic Battle Simulator 2-scale mass battles mixed with simplified Age of Empires-style RTS mechanics**.

- **From Ultimate Epic Battle Simulator 2:** enormous armies, dramatic clashes, satisfying swarms and emergent battle outcomes, with performance and unit count taking priority over visual fidelity.
- **From Age of Empires:** approachable base construction, barracks production, resource gathering, workers, walls, defensive towers, group selection, right-click orders, and opposing Citadels as strategic objectives.
- **Deliberately lightweight:** units remain basic geometric shapes, buildings can be simple primitives, maps are relatively modest/basic, and visuals/UI/economy are intentionally simpler than either reference game. No requirement for complex character models, detailed animations, sprawling worlds, or a full Age of Empires feature set.
- **Core identity:** a huge, mostly self-running battle simulation with a small amount of meaningful player strategy layered on top. Simulation scale and responsiveness matter more than graphical complexity or micro-management.

These are **design references, not a commitment to reproduce either game's full mechanics or scope**.
