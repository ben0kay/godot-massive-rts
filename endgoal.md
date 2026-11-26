# End Goal — Godot Massive RTS

**Design direction updated 2026-11-26.** The strategic Campaign and standalone Skirmishes describe the intended direction. Existing playable scenes and menus are the starting point for that work.

## Vision

Build a 3D isometric Godot .NET strategy game where players compose and paint large armies, issue opening orders, and watch thousands of autonomous units fight. Readable silhouettes, clear faction colors, and smooth battles at scale are central to the experience.

## Play modes

| Mode | Core loop | Army and progression |
| --- | --- | --- |
| **Campaign** | Move armies on a strategic map, capture territories, build fortresses, and resolve encounters. | Shared unit catalogue, persistent faction forces, recruitment, Credits, and research. |
| **Skirmishes** | Pick a standalone Clash, Offense, or Defense scenario, place the supplied army, and fight. | Authored quantities and base stats. Replay to try a different deployment or opening order. |
| **Fortress Defense** | Build and hold a base through a longer session of progressive enemy waves. | Its own scenario pacing, build and production rules, and in-match economy. Carbon is the established resource direction. |

## Strategic Campaign

1. Move army stacks between connected territories. Territories track ownership and fortress state. A fortress can hold a garrison.
2. Opposing forces meeting on a territory create a battle. The territory and who controls its fortress determine the objective from the player's perspective.
3. Choose **Auto-resolve** for a quick result or **Fight Battle** to deploy the army and watch the tactical simulation.
4. Return the result to the strategic map. Apply territory control, surviving forces and changes to the fortress once, then continue the campaign.

| Battle type | Encounter | Player objective |
| --- | --- | --- |
| **Clash** | Opposing armies meet on open territory. | Defeat the opposing field army. |
| **Offense** | The player's army attacks an enemy fortress. | Advance on and destroy the enemy Citadel, engaging defenders along the way. |
| **Defense** | An enemy army attacks the player's fortress. | Hold the player's Citadel against the assault in a short battle. |

**Auto-resolve** should compare the participating forces using unit strengths and relevant modifiers, such as research and a fortress advantage. A bounded amount of variation can keep results from feeling mechanical. Showing the likely outcome and casualty range before committing is a proposed UI.

**Fight Battle** uses the units brought to the encounter, with legal deployment areas and optional opening orders. Campaign research modifies the faction's eligible units across Clash, Offense, and Defense. Both resolution paths should produce the same result format so the map can handle them consistently.

Recruitment adds units to the faction's forces for assignment to armies or garrisons. The campaign tracks ownership, locations, and research using stable unit IDs from the shared catalogue. Carrying casualties forward into the army stack is proposed. The final casualty, replenishment, and retreat rules remain open.

## Standalone Skirmishes

Skirmishes reuse the short battle types and battlefield tools. Each authored scenario supplies preset unit quantities and base stats, with its own terrain, defenses, legal deployment area and objective. Players can replay the same setup to learn what changes when they paint a different formation or give a different opening order. Campaign recruitment, Credits and research stay in Campaign.

## Fortress Defense

Fortress Defense is the longer progressive-wave mode. The player defends a Citadel, builds defenses and production, and manages the in-match economy as waves escalate. The map can be hand authored with terrain, Carbon deposits, enemy approaches and starting structures. Short **Defense** battles share combat systems with Fortress Defense and use their own short encounter rules.

## Fortress Defense group orders

These tactical controls apply to Fortress Defense. Campaign map movement operates on whole army stacks.

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

## Shared simulation and performance

- Share the unit catalogue, fixed-step battle simulation, damage rules, projectiles, terrain and obstacle queries, placement UI and batched rendering across applicable modes. Keep the strategic map, skirmish setup, and progressive wave systems scoped to their own modes.
- Represent soldiers and aircraft with compact data and batched visuals. Use separate air and ground spatial indexes and explicit target eligibility.
- Budget targeting, pathfinding, and separation. Route around obstacles with shared or staggered work where practical. Keep terrain height authoritative for movement and combat.
- Maintain Benchmark as an independent stress-test harness. Measure like-for-like battles with consistent counts, rosters, seeds, and camera settings before making performance claims.
- Preserve authored levels and stable save IDs when evolving the menu and campaign structure.

## Suggested first campaign prototype

Use a small connected territory map with a player army, an opposing army, and one fortress. Prove that the player can enter an open-territory Clash, assault the fortress, and later defend it in a short Defense battle. Verify that both Auto-resolve and Fight Battle update the map correctly. Expand recruitment, research, map size, and AI as the loop earns them.

Open decisions include turn-based versus timed map movement, casualty persistence and replenishment, fortress construction costs, and the amount of variation in auto-resolve. See [CodePreferences.md](CodePreferences.md) for implementation and validation conventions.
