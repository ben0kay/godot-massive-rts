# End Goal — Godot Massive RTS

> **Document type:** Long-term game vision and design guardrails, **not** authorization to implement features.
>
> **Status:** Proposed game modes; simulation/benchmark development remains the active priority.
>
> **Last refined:** 2026-11-11 · **Repository:** `ben0kay/godot-massive-rts` · **Branch:** `main`

## 1. The game in one sentence

**Ultimate Epic Battle Simulator 2-style massive, mostly autonomous battles, combined with a *lightweight* set of Age of Empires-style RTS mechanics, built around simple 3D shapes, modest maps and extreme unit counts.**

The goal is not to reproduce either game. The appeal is watching enormous armies collide while still having satisfying, accessible decisions about deployment, composition, base growth, defense and occasional group movement orders.

### Design priorities

1. **Massive battles first:** thousands to tens of thousands of soldiers must remain practical; larger counts are experimental, not promised.
2. **One scalable simulation:** rendering, movement, separation, targeting, damage and projectiles should work across both modes without separate engines.
3. **Autonomous individual behavior:** troops can find, pursue and fight enemies without constant player input.
4. **Player strategy, not heavy micromanagement:** deployment in one mode; base building and occasional group orders in the other.
5. **Simple visual scope:** primitive shapes for soldiers and buildings, readable blue/red (and optional other) factions, white/light damage feedback, clear isometric camera, basic battlefield geometry and restrained effects.
6. **Measure before adding complexity:** gameplay additions must justify their idle, per-tick and worst-case costs.

Use a true **3D isometric/orthographic Godot 4 C#** world. Begin with simple, mostly flat maps. Terrain elevation, varied levels, elaborate art, animation, sophisticated formations and detailed economies are **possible later**, not prerequisites. The emphasis is on the simulation, not realistic graphics or enormous maps.

## 2. Current focus — benchmark the simulation, not build the full game

The existing `BENCHMARK/` arena is the proving ground. Continue refining the compact unit-state, batched rendering and shared combat/movement architecture before committing to full game loops. The existing painter is a useful deployment **prototype**, not yet the complete first game mode.

**Immediate planned experiment:** place a **large, simple rectangular/blob-like static obstacle** in the battlefield and test the performance and behavior of large armies moving and fighting around it.

Questions this experiment should help answer:

- Do units route around an obstruction rather than attempt to walk through it or become stuck?
- What happens when thousands of allies converge near the same obstacle edges or chokepoint?
- Does local separation create bottlenecks, jitter, excessive detours or needless CPU work?
- How much do obstacle-aware movement/navigation, targeting, separation and rendering each cost compared with the equivalent unobstructed scenario?
- Can any expensive route work be shared and/or staggered instead of recalculated for every soldier?

Start with one deliberately simple obstacle and controlled benchmark scenarios, not a complex terrain editor or a full navigation system by assumption. Compare like-for-like counts, unit compositions, seeds and camera settings, including dense moving/combat crowds. Measure simulation tick time, frame time/spikes, separation/navigation costs, skipped ticks and living unit counts where available.

Owner observations suggest some 20k–25k-unit scenarios have run well, while larger/heavier scenarios can deteriorate sharply. These are **scenario-dependent observations, not guaranteed targets or validated benchmark results**. Preserve useful 1k/10k/25k/50k+ stress presets and the independent benchmark scene.

**Nothing in this document requests immediate code changes.**

## 3. Game mode A — Army Painter / tactical battle levels

### Core loop

1. Load a **finite, authored battlefield/level** with an enemy army already positioned.
2. Receive a **limited troop supply or budget**.
3. Choose from available unit types and **paint/place the friendly army** in permitted deployment areas.
4. Press **Start Battle**.
5. Watch the armies **move, acquire targets and fight automatically**. Outcome depends primarily on composition, placement and the shared simulation.
6. Resolve victory/defeat; replay or try a different deployment.

This is the more focused, simulation-forward mode. It can be enjoyable without production buildings or micromanagement.

**Intentionally absent in the initial Army Painter design:**
- RTS-style box selection and right-click orders.
- Barracks, workers, resource gathering, a build menu or a recurring economy.
- RTS-specific command processing running silently in the background.

A campaign of handcrafted challenges, different enemy arrangements, terrain obstacles and objectives is conceivable, but level progression and exact victory conditions remain to be decided.

**Existing foundation:** the BENCH0 painted deployment editor already supports placing basic soldier types and starting a battle. It should be reused/adapted rather than mistaken for a fully finished game mode.

## 4. Game mode B — Open RTS / autonomous base-and-army warfare

### Intended match flow

1. Start with a **small base**, possibly a few walls, and a central **Citadel**.
2. Place a **Barracks** that produces basic soldiers (a simple first example is continuous **Circle Infantry** production).
3. Build or recruit basic **workers/miners**, potentially represented by a shape with a **pickaxe icon**.
4. Workers **mine resource deposits and/or chop trees**, supporting more production and construction.
5. Expand defenses with **walls and stationary towers**.
6. Armies grow and **fight automatically**, while the player can optionally select large groups and issue simple tactical movement orders.
7. A central strategic objective is to **destroy the opposing faction's Citadel**.

Think **Age of Empires mechanics at their simplest useful level**, overlaid on a UEBS2-like mass simulation. The world and buildings may be made almost entirely of basic shapes; the map does not need to be sprawling or visually detailed.

### Specific propositions

- **Citadel:** each team owns a central structure; destroying the enemy Citadel is the proposed principal win condition.
- **Barracks:** a placeable production building that emits units over time; Circle Infantry is the initial illustrative output. Additional unit types/buildings are optional future additions.
- **Workers/miners:** lightweight resource gatherers; a pickaxe symbol is enough to communicate their job. Mining and chopping trees are proposed resource activities.
- **Walls:** simple defensive obstacles, meaningful for both units and performance testing.
- **Defensive towers:** stationary structures that attack enemies and help guard a base.
- **Autonomous military units:** default behavior remains automatic fighting, even without player orders.
- **Optional intervention:** players can reposition large selected groups when they want to change the battle.

The starting economy should be deliberately small and easy to understand. Resource types, costs, harvesting/delivery details, production rates/caps, construction rules, tower variants, AI opponents and upgrades are **open design decisions**. Do not build a full conventional RTS economy merely because Age of Empires is an inspiration.

At huge unit counts, production should eventually have sensible pacing/limits and measurable simulation impact; a theoretically unbounded soldier faucet is not an implementation requirement.

## 5. Group selection and right-click orders — the player's performance tradeoff

**This control system is proposed for Open RTS, not Army Painter.**

### Example: select 3,000 soldiers

1. The player **click-drags a selection rectangle** around 3,000 troops.
2. On release, the selection is known and displayed (for example, a selected-unit count).
3. The player **right-clicks a destination**.
4. The order is **acknowledged immediately**, but individual soldiers **do not all need to calculate or adopt their new movement behavior in that same frame**.
5. A bounded amount of necessary per-soldier work is processed across subsequent simulation ticks/frames; troops begin responding progressively.
6. They move using the existing shared movement/separation systems and **fight enemies they encounter**.
7. After local combat, they can continue toward the ordered destination, subject to whatever simple Move/Attack Move semantics are chosen later.

### The owner's explicit preference

**Smooth performance is more important than instant group response.** For a 3,000-unit command, it is acceptable if fully distributing/processing the command takes **roughly several seconds (three seconds was given as an acceptable example)**.

The owner's illustration was **10 soldiers processed in one frame, then 10 more the next**, and so forth. This is a concept for staggering work, **not a required batch size**. For perspective, at 60 rendered FPS, 10 per frame takes five seconds for 3,000 soldiers; at a 30 Hz simulation rate, 10 per simulation tick takes ten seconds. A measured configurable work/time budget is more useful than hard-coding either rule or guaranteeing a particular completion time.

**Important distinction:** accepting a command, assigning a shared group destination, performing costly per-unit routing/formation work, and executing normal movement are different operations. A single shared order can be cheap to record immediately. Only work that genuinely costs time needs spreading out. Soldiers still require their normal ongoing movement/combat simulation; staggering a command is **not** permission to freeze ordinary correctness-critical simulation for most of the army.

### Proposed technical guardrails

- Represent orders with **compact shared/group data** where feasible: destination, order/version, selected IDs or membership, optional formation offsets.
- Prefer **one shared destination/path/waypoint field** where appropriate over 3,000 independent pathfinding requests. Do not assume each soldier needs a separate new target calculation.
- Use a **bounded queue or scheduler** for expensive order expansion, navigation refreshes and formation placement. Make budgets configurable and measure actual cost.
- An individual soldier can temporarily continue its previous valid behavior until the newer order reaches it.
- **New commands supersede stale queued work**. Reset, death, despawn, mode changes and canceled orders must invalidate relevant work safely.
- Keep a distinct **strategic movement goal/order** and **transient combat target**. Enemy pursuit must not permanently erase the destination. Reacquisition and resumption should be predictable.
- Start with basic **Move** behavior that permits automatic combat along the way. Separate **Attack Move**, stance or pursuit rules are possible later, not settled requirements.
- Selection rectangle checks can be throttled during dragging; avoid wasteful every-frame UI scans. Final selection must be accurate enough for the chosen visual behavior.
- No individual `Node3D`, `NavigationAgent3D`, physics body, `Timer`, script tick or independent A* search per soldier.
- Keep the simple 3D `XZ` movement plane and camera/world-selection mapping coherent; preserve room for future terrain height.

**User experience target:** thousands of soldiers feel collectively responsive without a single giant CPU spike when the player issues an order. Not every soldier needs to turn on exactly the same frame.

## 6. Pathing, crowding and combat must remain shared

The main scaling challenge is likely not the selection rectangle itself; it is **large groups navigating obstacles and one another while still acquiring enemies and fighting**.

- Treat static obstacles, walls and future buildings through a reusable terrain/obstruction representation where possible.
- Keep local soldier separation distinct from high-level **route planning**: separation alone is not a reliable way to lead 3,000 soldiers around a giant rectangle.
- Investigate **shared/group waypoints, cached routes or flow fields** if profiling shows they are appropriate; avoid thousands of independent global searches.
- Route updates can be staggered, but avoid persistent stale routes into newly blocked terrain.
- Reuse unit-specific motion and combat behavior, including different melee/ranged reach, acceleration, cavalry momentum/trample and projectiles.
- Budget dense targeting and local-neighbor work; avoid all-to-all checks or wide map searches every tick.
- Simple, readable outcomes matter more than perfect military formation fidelity or ideal path optimality.
- Keep authoritative health, movement, collisions/obstacle rules and damage independent of visual meshes.

These are architectural constraints and experiments, **not a request to implement a particular pathfinding algorithm now**.

## 7. One simulation, separate optional mode systems

Both games should use the same core:
- Compact living-unit state and unit definitions.
- Fixed-step/batched movement and combat.
- Shared spatial index, targeting, projectiles and separation.
- Batched geometric rendering and damage feedback.
- Terrain/obstacle queries and diagnostics.

**Army Painter** adds authored levels, finite army allocation, painting/deployment, battle start/retry and level outcomes.

**Open RTS** adds selection/orders, production, resource workers, buildings, walls, defense, Citadels and match victory.

**Benchmark** remains an independent stress-test harness.

Mode-only systems should be **uninitialized, disabled or otherwise genuinely dormant** when a mode does not use them—especially order queues, selection scans, economy ticks, production schedulers and UI refreshes. Do not scatter mode checks throughout every soldier's hot loop if a higher-level configuration can keep the common update path clean.

Keep major systems in **UPPERCASE top-level folders** per `NOTES/CodePreferences.md`. `SIMULATION/`, `COMBAT/`, `RENDERING/`, `BENCHMARK/`, `DEPLOYMENT/` and `UNITS/` already provide relevant ownership. Future `FORMATIONS/`, `BUILDINGS/`, `ECONOMY/`, `GAME_MODES/` or selection-specific folders are only **possibilities**; create them when code actually belongs there, not to satisfy this document. Reuse established systems instead of duplicating them.

Small-count buildings may have scene nodes if appropriate; the prohibition is on **one full Godot object graph per soldier** at enormous scale.

## 8. Boundaries — what this project is *not* trying to be

- Not a feature-complete Age of Empires clone.
- Not a visually realistic UEBS2 clone.
- Not a Total War-scale command/formation simulator by default.
- Not a deeply simulated economy with elaborate villagers, tech trees and diplomacy at the outset.
- Not a giant procedural world required before battles can be fun.
- Not a new engine/framework designed in advance of measured problems.
- Not two games with separate, competing soldier simulations.

**Keep the gameplay meaningful but the implementation intentionally plain.** It should be possible to recognize every type of soldier/building from a silhouette or icon and still enjoy the scale of the battle.

## 9. Illustrative future experiences

**Army Painter example:** a level gives the player a limited set of Circle Infantry, archers and cavalry. The enemy is already assembled behind a simple obstacle. The player paints their formation, starts the battle and watches it unfold without issuing live movement orders.

**Open RTS example:** the player starts beside a Citadel and a few walls. A Barracks starts producing Circle Infantry. Pickaxe workers gather resources; a tower helps defend the approach. Later, the player drag-selects 3,000 soldiers and right-clicks a route toward the rival base. The command is received immediately, unit-level responses are budgeted over time, the troops fight en route, and the opposing Citadel remains the strategic target.

These examples illustrate the **intended feel**, not locked specifications for unit counts, pacing, economy or AI.

## 10. Suggested progression — planning only

1. **Now: simple obstacle benchmark.** Measure how armies move, crowd and fight around one large rectangular obstruction; identify actual navigation bottlenecks.
2. **Continue shared simulation optimization.** Address measured movement, separation, targeting and rendering hot spots while preserving current benchmarking.
3. **Prototype lightweight selected-group orders** when authorized: screen-space drag selection, terrain right-click, shared goals and staggered expensive processing. Measure it at 3k selected units and in much larger total battles.
4. **Formalize mode boundaries** without duplicating the soldier simulation; ensure unused RTS systems cost nothing in Army Painter.
5. **Build the minimal Army Painter loop** around existing deployment: budget, authored level, start/retry, outcome.
6. **Build a tiny Open RTS vertical slice:** Citadels, one Barracks producing Circle Infantry, walls and automatic attacking.
7. **Add the smallest useful economy/defense loop:** pickaxe workers, gathering/mining or trees, construction costs and static defensive towers.
8. **Only then consider refinement:** more unit/building types, formation behaviors, sophisticated routes, elevation, progression and polish, based on measured performance and fun.

Order beyond the immediate benchmark is illustrative; priorities can change after testing. Do **not** implement these milestones without a separate user request.

## 11. Unsettled design questions

Leave these open until prototypes reveal what feels good:

- Is ordinary right-click **Move** closer to move-and-defend, or should it aggressively pursue threats like **Attack Move**?
- Are selected soldiers simply a temporary box-selected set, or should persistent battalion groups exist?
- How much economy depth is worthwhile—one resource versus several; automatic versus explicit worker assignment?
- How fast should Barracks produce, and what caps/resources prevent uncontrollable unit growth?
- What are the Army Painter objective types and deployment restrictions?
- How big should maps become, and when is elevation worth its simulation cost?
- What order/route response budget feels best once the actual 3,000-soldier case is profiled?

**Guiding rule:** build the lightest thing that creates convincing mass battles and meaningful RTS choices, and preserve the ability to measure where the time goes.
