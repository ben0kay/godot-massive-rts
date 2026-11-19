# Preset Battles — Campaign Design

> **Status:** Planned design / documentation only; no gameplay implementation is authorized by this file.
> **Date:** 2026-11-19
> **Game mode:** Preset Battles (stable campaign ID `Campaign001`)
> **Owner:** `LEVELS/PresetBattles/`
>
> This expands the Preset Battles vision in [endgoal.md](../../endgoal.md). The two owner-provided reference screenshots communicate the desired **experience**: a deployment screen facing a visible enemy force and an between-level recruiting/upgrades screen. They are inspiration, not UI, assets, or code to reproduce.

## 1. The game mode in one sentence

A sequence of increasingly challenging authored **automatic battles**: the player commands the **Blue** army by choosing its composition and deployment **before** the fight, sees the **Red** defending army and battlefield, then watches the shared combat simulation play out. Victories earn campaign credits used between levels to grow and improve the army.

This is intentionally distinct from **Fortress Defense / Open RTS**, which has real-time orders, Carbon gathering, Fabricators, buildings and waves. Preset Battles needs no real-time economy or soldier-by-soldier orders.

## 2. Current foundation vs. intended gameplay

**Present in the repository:**
- A catalog-driven **Campaign -> Preset Battles -> Levels** frontend, with extensible campaign definitions and ordered levels.
- Stable Preset Battles campaign ID `Campaign001`; the existing `Level001` resource and scene are registered. Do not rename the ID and break progress.
- Per-profile campaign progress currently records **completed level IDs**; the next level unlocks after its predecessor is complete.
- `BENCHMARK/` has an **experimental painted deployment editor**, soldier catalogue, formation painting, battle start, shared mass-army combat, relative army HUD and estimated Battle Rating/strength display.

**Not yet implemented as a Preset Battles campaign loop:**
- `LEVELS/PresetBattles/Level001/Level001.tscn` is currently an **empty Node3D**.
- No campaign deployment inventory/limit, authored Red vs player-only Blue editing, campaign win/loss/results integration, credits, recruitment, upgrades, survival rewards or headquarters.
- The benchmark painter is a **reference/prototype**, not the finished player-facing campaign editor. Its unrestricted team editing and developer controls must not leak into a campaign battle.

Keep these status distinctions accurate as development proceeds.

## 3. Player experience / core loop

1. **Campaign headquarters / level selection:** Inspect available levels, current campaign credits, owned/recruited army and purchased upgrades.
2. **Choose a level:** Load a distinct authored 3D map with its Red defenders already deployed. Initial levels provide reliable enemy intelligence; hidden elements can be introduced later.
3. **Prepare:** Allocate Blue troops from the owned roster (or a level-supplied allowance in early prototypes) within allowed deployment zones. Enforce a per-level deployment limit. Display quantities and an estimated friendly/enemy strength comparison as useful *guidance*, not a win prediction.
4. **Start Fight:** Freeze the deployment into a battle snapshot; normal autonomous movement, targeting, projectiles, damage and deaths run without active RTS commands.
5. **Resolve the outcome:** Detect victory or defeat from clearly authored objectives (initial default: defeat the enemy army). Show surviving units, casualties and the outcome.
6. **Grant rewards on victory:** Award level credits, potentially including a bonus for preserving more of Blue's starting force. Unlock the next authored level.
7. **Return to headquarters:** Spend credits on recruits/upgrades, choose the next challenge, or replay an earlier level.

Losing a match should allow **retry/redeployment** and should not falsely complete the level or grant victory rewards.

## 4. Levels, map authoring and escalating difficulty

Each campaign level should be independently editable in Godot and referenced through `LevelDefinition` / `CampaignDefinition`; do not bake map lists, faction armies or routes into menu code.

Proposed level-authored ingredients:
- 3D battlefield and camera setup; authored obstacles, painted rocks/water, elevation, chokepoints and/or defensible positions as shared world systems allow.
- Red soldier composition, quantity and exact initial placements; Red is **locked** to the player before battle.
- Blue deployment zones: bounded regions where player can paint soldiers, with size/footprint constraints, terrain obstruction checks and map boundaries.
- Player deployment allowance: a unit-count cap, Battle Rating/strength budget, specific type quotas or a hybrid. **Choose the first implementation rule after prototyping; not settled.**
- Optional objectives, starting conditions, battle outcome rules and pre-battle intelligence flags, introduced only as needed.

Difficulty should increase through interesting **composition, terrain, space and counter** challenges, not merely ever-larger Red counts. Examples: a river crossing with one usable route, ranged soldiers behind cover, stronger defenders on a flank, split attacks, and later concealed reserves. Avoid requiring high unit counts on every level.

**Enemy information:** Initially the player sees the existing Red force and can plan around it ("knowing is half the battle"). **Future optional trickiness:** true fog of war, hidden reserve groups, delayed reinforcements, partial scouting or incomplete enemy composition. These are optional later mechanics, not part of the first playable level. They should remain fair and readable and not require RTS-style real-time micro.

**Future modes:** `Campaign` remains an umbrella. Additional modes should be registered through the existing campaign catalogue and can have independent progression/economy policies without duplicating the common soldier simulation.

## 5. Persistent army, recruiting and deployment limits

Treat the following as **three separate data concepts**:
1. **Owned army / recruit pool:** quantities of each unlocked unit archetype purchased with campaign credits, persisted per profile.
2. **Battle allocation:** temporary subset of that pool selected for the current level; cannot exceed owned quantities or the level's allowance.
3. **On-map deployment:** exact placements of that allocated subset within Blue's permitted areas. Removing painted soldiers refunds them to the current *allocation*, not the overall campaign currency.

Recruitment between levels increases the available quantity of specific troops. Upgrades may unlock types or improve selected attributes, but should use shared definitions/configuration rather than mutating benchmark unit assets permanently.

**Suggested initial casualty policy (not finalized):** Soldiers killed during a battle **do not permanently disappear** from the purchased campaign roster. Casualties influence the results screen and survival reward. This makes experimentation/retry practical. A later optional campaign rule could add permanent losses, replenishment costs or a hardcore mode; do not impose those before playtesting.

Persist roster quantities and unlocked/purchased options by **stable unit IDs**, not indices that could shift as the catalogue grows.

## 6. Rewards and credits

Use a distinct **Preset Battles campaign credit balance** (example label: `Credits`). It must not be confused with Fortress Defense's in-battle **Carbon** economy.

**Proposed reward ingredients:**
- A fixed one-time or best-earned level victory payout.
- A **casualty/survival bonus**: more of the deployed Blue army surviving increases the reward.
- Optional first-clear and difficulty bonuses later.

A transparent conceptual formula is:

`Reward = VictoryBase + SurvivalBonus(BlueSurvivors / BlueDeployedAtStart)`

This is **not a locked calculation** or balance. Define meaningful cases for zero-deployment exploits, optional summoned units, multi-faction maps and alternate victory objectives before implementation. Do not let players earn limitless credits by repeatedly replaying an easy level unless that is explicitly desired. **Recommended first policy:** award the base clear reward once, and on replay grant only any *improvement over the previously saved best survival bonus*. Persist the best result per level. A simpler one-time payout is acceptable for CAMPAIGN2.

On defeat: no victory base payout or next-level unlock. Whether consolation rewards exist remains open.

Results should show the same quantities used for the reward calculation, making the system understandable.

## 7. Upgrades and recruit interface

The second screenshot suggests an appealing **between-level army headquarters** screen. Build an original UI, not a copy.

**Recruit panel:** list available Blue unit types, owned/available counts, per-unit or bundle recruit cost, and a purchase button disabled when unaffordable. Allow new units to unlock progressively if desirable.

**Upgrade panel:** a compact connected **technology/upgrade graph**, with nodes representing new unit availability, higher recruitment capacity, small persistent stat improvements or deployment capabilities. Prerequisites, credit costs and owned/unlocked states should be data-driven. Keep bonuses understandable and avoid large hidden multipliers that undermine authored battlefield balance.

**Campaign navigation:** choose replayable completed levels or the next unlocked challenge; retain the existing frontend profile/campaign/menu conventions.

**Suggested MVP:** recruitment plus a simple small number of upgrade choices. A large skill tree, dynamic layouts, multiple currencies, respec and dozens of modifiers are not prerequisites.

## 8. Ownership and persistence design constraints

Reuse the current:
- `LEVELS/Catalog/CampaignDefinition.cs`, `LevelDefinition.cs` and `CampaignCatalog.cs` for mode/level registration and ordering.
- `LEVELS/Runtime/LevelLauncher.cs`, `LevelSession.cs` for launch identity and explicit successful completion.
- `SYSTEMS/Profiles/` and `SYSTEMS/Saving/CampaignProgressStore.cs` for selected-profile and existing completion storage.

Additional campaign data will be needed for **credits, owned units, unlocks/upgrades and best results**. Introduce versioned, validated per-profile persistence using the existing atomic-save pattern when implementing CAMPAIGN2. It may be a Preset Battles-specific save alongside generic completion progress rather than forcing unrelated Fortress Defense systems into the same economy file.

**Compatibility rules:** preserve `Campaign001` and existing completed `Level001` entries, do not reset or corrupt existing profiles, and distinguish launching/retrying/leaving from genuine victory. A completed level should remain replayable.

Separate level-authored Red deployments from temporary Blue editable placements; do not write the player's pre-battle drafts back into the level scene or the author's original enemy layout.

## 9. Shared simulation and performance constraints

Preset Battles and Fortress Defense use the **same** registered unit definitions, combat, target acquisition, projectiles, separation, navigation, terrain queries and batched rendering.

Adapt reusable parts of `BENCHMARK/` and `GAMEPLAY/DEPLOYMENT/` into the campaign; do not make a second battle engine or require benchmark diagnostics during gameplay.

- Avoid per-soldier nodes, extra recurring campaign logic in mass combat hot loops, unnecessary per-frame UI recomputation and expensive all-to-all logic.
- Pre-battle quotas, save writes, recruitment transactions and upgrade calculations happen outside the live fight wherever possible.
- Resolve upgrade effects before combat into runtime stats/configuration rather than repeatedly checking the entire upgrade tree per attack.
- Mode-only systems should stay dormant when the other mode is active.
- Preserve BENCH0 as a runnable, independent diagnostic sandbox.

## 10. Suggested incremental implementation (not authorized by this plan)

### CAMPAIGN1 — First playable Preset Battle
- Replace the blank Preset Battles Level 001 with a real authored Red army and Blue-only deployment phase.
- Add valid Blue deployment region(s) and a simple finite deployment allowance.
- Start Fight, run the shared automatic combat, detect victory/defeat, and offer retry/results.
- Use temporary level-supplied Blue units if persistent recruitment is not ready.
- Do not add upgrades or a full economy yet.

**Owner check:** Can I preview enemy composition, position a limited Blue force, watch an automatic fight, and replay or advance only after a real victory?

### CAMPAIGN2 — Rewards, roster and persistence
- Add campaign credits, basic recruitment, player-owned unit quantities and survival-aware result rewards.
- Add a small headquarters view between levels and validated per-profile saves.
- Decide and implement anti-farming/replay reward policy; preserve old completion records.

**Owner check:** Can I earn credits, expand the owned force, select a legal deployment and resume later without losing purchases or replay progress?

### CAMPAIGN3 — Upgrades and varied challenges
- Add a small data-driven upgrade graph, additional authored maps and more varied enemy compositions/terrain.
- Explore optional fog/hidden reserves and distinct objectives after the initial loop feels good.
- Expand level and mode content through the catalogue.

**Owner check:** Do upgrades/recruit choices change tactics without making early levels trivial or disrupting shared simulation performance?

## 11. Open decisions for the owner / Codex

- Deployment restriction: unit count, Battle Rating, type-specific allowance or combination?
- Does each level provide temporary troops, only purchased troops, or a mixture?
- Should losing soldiers only affect rewards (recommended for first pass), or also require recruitment to replenish?
- Exact recruit prices, upgrade costs, reward formula and whether replays award incremental bonuses.
- Should upgrades alter shared base stats, add level-specific modifiers, or unlock new unit types first?
- Initial victory rule (enemy wipe) vs objectives, time limits or protecting a formation.
- When to introduce hidden enemies, fog or surprise reinforcements without frustrating preparation-based play?
- How many levels and how steep should difficulty increase?

**Implementation guidance:** Build the smallest complete loop first. Codex should inspect the latest sources and refine each milestone before separately authorized implementation; this document is a design reference, not a request to implement CAMPAIGN1–3 now.
