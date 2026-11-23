# AutoBattler — Persistent Campaign Progression Implementation Plan

> **Status:** CAMPAIGN1-A and CAMPAIGN1-B source implemented and awaiting owner validation. Later stages are planned.
> **Date:** 2026-11-22
> **Repository:** ben0kay/godot-massive-rts
> **Branch:** main
> **Campaign:** AutoBattler, stable campaign ID **Campaign001**
> **Ownership:** LEVELS/AutoBattler/; shared profile/save and front-end code remains in its current owning folders.
> **Implementation:** Five explicitly gated stages. Implement **only the requested stage**, commit/push to main, then await owner playtesting before continuing.

This is the **authoritative plan for campaign progression**. Read AUTOBATTLER_CAMPAIGN_DESIGN.md for its original vision, but where it differs, **this document supersedes its temporary free-army proposals, per-level Blue troop limits, casualty uncertainty and separate-currency possibilities**. The owner explicitly confirmed the decisions below.

## 0. Non-negotiable owner decisions

1. **Starting Credits:** A new AutoBattler campaign starts with **500 Credits** and **zero purchased soldiers**. Starting recruitment options are Circle, Square and Triangle. No free starter soldiers.
2. **Permanent roster:** Once recruited, units belong to the campaign profile permanently. Battle deaths are restored when a battle ends; defeat, victory, retry and leaving the battle never consume purchased roster quantities. No replenishment or repair tax.
3. **Unrestricted owned-army deployment:** Players may deploy **all the units they own** (or any subset). **No gameplay supply cap, count quota, Battle Rating ceiling, or growing level allowance**. Existing terrain, collision, legal Blue deployment zone, actual simulation capacity and resource limitations are valid technical boundaries, not economic limits. When the engine cannot fit a proposed placement, explain why instead of silently cutting the roster.
4. **Single shared campaign currency:** Both recruitment and permanent research use **Credits**. No secondary research currency. Painting/erasing owned troops during a deployment costs no Credits.
5. **Mode isolation:** The AutoBattler economy and research belong to **Campaign001 only**. Fortress Defense retains its separate **OpenRTS** campaign ID, Carbon economy, workers, Fabricators, towers, waves, RTS controls and standard unit stats.
6. **Autonomous combat:** The player purchases and places Blue troops before battle; combat then runs through the shared mass-army simulation without RTS micromanagement.
7. **Editor ownership:** Red formations, defenses, terrain, map layout and Blue deployment zones are owner-authored. Do not overwrite or auto-generate the owner's levels just to populate the campaign.

**Stable IDs are mandatory:** Save unit quantities and research ownership by stable string IDs, not runtime byte indices. Keep Campaign001 and OpenRTS identifiers unchanged.

## 1. Intended player journey

**Profile → Campaign → AutoBattler → Headquarters → choose a level → briefing → deploy owned Blue army → Start Battle → victory/defeat → results → Headquarters / next unlocked level / retry.**

- Headquarters displays balance, owned unit counts, estimated army strength, recruitment and research.
- Level briefing displays the authored Red forces, available Blue roster and level objectives.
- Deployment paints only owned Blue units into legal Blue zones. Red placements and towers are not editable by players.
- On Start Battle, freeze the deployment; launch shared targeting, movement, separation, projectiles, damage and death.
- On defeat, return to deployment or Headquarters with **the complete roster intact**.
- On victory, give a one-time level reward and potentially a higher best-survival bonus; unlock the next registered eligible level.

Keep all UI original in styling rather than recreating another game's copyrighted assets. Reuse the current dark/navy/aqua UI theme and UI helpers.

## 2. Relevant current source (verify afresh at the start of each stage)

Read README.md, CodePreferences.md, endgoal.md, this plan, AUTOBATTLER_CAMPAIGN_DESIGN.md and AGENTS.md **if present**. Then inspect stage-relevant code:

| Concern | Existing ownership |
| --- | --- |
| Campaign/ordered level definitions | LEVELS/Catalog/CampaignDefinition.cs; LevelDefinition.cs; CampaignCatalog.cs |
| Campaign session/launch | LEVELS/Runtime/LevelLauncher.cs; LevelSession.cs |
| Preset authoring rules | LEVELS/AutoBattler/AutoBattlerRules.cs |
| Existing progress | SYSTEMS/Saving/CampaignProgress.cs; CampaignProgressStore.cs |
| Atomic writes/backups | SYSTEMS/Saving/AtomicJsonFile.cs |
| Player profile | SYSTEMS/Profiles/ |
| Menus/briefing | UI/Menus/FrontMenu.cs; LevelBriefing.cs |
| Preset test foundation | BENCHMARK/BenchmarkArena.AutoBattlerPlaytest.cs and BENCHMARK/BenchmarkArena.cs |
| Deployment | GAMEPLAY/DEPLOYMENT/ and relevant BENCHMARK painter/UI |
| Unit stats/catalogue | GAMEPLAY/UNITS/DEFINITIONS/; GAMEPLAY/UNITS/CATALOG/ |
| Level editing | UI/Editor/LevelEditor.cs and UI/Editor/SAVING/ |

At time of planning:
- The catalogue has a AutoBattler mode and authored levels, including gaps in numbered levels.
- A mode-specific Level Editor and Blue deployment-region data already exist.
- The current Benchmark preset playtest is **an editor/test harness**, not the finished campaign experience.
- AutoBattlerRules exposes an older BlueUnitLimit setting (default 200); **do not enforce it as a player-campaign limit**.
- CampaignCatalog.IsUnlocked currently has a Level001/debug-only shortcut; CAMPAIGN2 must implement real sequential progression for AutoBattler without changing Fortress Defense.
- The briefing already has a disabled "Upgrades / Recruit" placeholder.
- Unit catalogue definitions and balance can evolve, so discover current roster instead of freezing an old list.
- Some authored levels might have no Red soldiers: do not award a free victory for a blank test map.

## 3. Economy and roster specification

### 3.1 Data-driven recruitment

Create a small AutoBattler-only recruitment catalogue/config Resource, or reuse an appropriate existing config pattern, that maps stable unit IDs to prices and unlocks. The authoritative unit list should still come from the current UnitCatalog. Some units (such as workers or support machines) may be excluded or require special access: decide from their actual role and document any exclusion, rather than blindly selling every definition.

**Provisional starting prices (editable and subject to owner balancing):**

| Unit | Credits per soldier | Initially unlocked? |
| --- | ---: | --- |
| Circle Infantry | 5 | Yes |
| Square Infantry | 12 | Yes |
| Triangle Archer | 10 | Yes |
| Pikeman | 14 | Research |
| Diamond Cavalry | 22 | Research |
| Long Triangle Marksman | 24 | Research |
| Pentagon Elite | 40 | Research |
| H Breacher | 25 | Research |
| Twin Rocket Launcher | 45 | Research |
| Prism Lance | 85 | Research |
| Hexagon Siege | 100 | Research |
| Light Artillery | 120 | Research |
| Dreadnaught | 500 | Research |

Recheck names and current unit definitions at implementation time. Additional roster members (e.g. Mobile Generator) require an intentional purchase/unlock policy, not an assumed price. **Battle Rating measures estimated power; it is not the recruit price and never limits deployment.**

The recruit UI should show name, role, Size Tier, price, owned count, Battle Rating estimate and unlock state. Provide convenient 1/10/50/100 quantities, with custom quantity if reasonable. Calculate costs with checked integer arithmetic; validate stock, ID, balance, profile and limits before committing. A single purchase commits both the debit and roster increase atomically. Refresh UI only once the save succeeds.

### 3.2 Permanent campaign save

Use an explicitly versioned, validated **AutoBattler-specific** per-profile state. Suggested location and naming:

- LEVELS/AutoBattler/PROGRESSION/ for feature logic/data configuration.
- Existing SYSTEMS/Saving/ for shared persistence helpers where appropriate.
- A dedicated per-profile state file under user://Profiles/{profileId}/, separate from Fortress Carbon saves.

Recommended saved fields:
- version; profile ID; credit balance (initial 500)
- owned unit quantities by stable unit ID
- purchased research node IDs
- completed AutoBattler level IDs
- first-clear reward claim status by level ID
- best claimed survival bonus by level ID
- reserved space for versioned deployment templates (future only)

Use AtomicJsonFile, its backup pattern, bounded validation and safe migration. Treat missing save as fresh; do not reset valid saves. Reject corrupt/unrecoverable files with an actionable error and preserve them.

**Compatibility:** The existing CampaignProgressStore owns generic completion records. Migrate existing Campaign001 completions into the new state once without deleting other campaigns' progress. Treat any migrated, previously completed levels as already having received their first-clear base reward, to avoid retroactive double payouts; document this policy. Use one authoritative transaction for AutoBattler victory payout + completion + best bonus; any synchronization back to the old completion store must be idempotent/recoverable and cannot create double rewards. Leave OpenRTS completion records intact.

### 3.3 Reward rules (implemented in CAMPAIGN2)

Base victory payout is **one-time per level per profile**. Best-survival bonus may improve on replays; only the incremental improvement is paid. Defeats earn zero Credits initially.

Suggested initial editable values:

| Level | First-clear base | Maximum survival bonus |
| --- | ---: | ---: |
| Level001 | 150 | 50 |
| Level002 | 175 | 60 |
| Level003 | 200 | 70 |
| Level004 | 225 | 80 |
| Level005 | 250 | 90 |

These are **proposed values for future authored levels**, not instructions to create missing level scenes. Store actual reward parameters in appropriate AutoBattler level metadata/rules and expose them in Map Settings once CAMPAIGN2 is authorized.

Suggested formula: earned survival bonus = round(max bonus × surviving originally deployed Blue / originally deployed Blue). Count original deployed soldiers only; do not inflate survival with spawned/summoned units. For replay, grant max(0, newly earned bonus - previously saved best bonus). Guard against zero initial deployment and inconsistent casualties.

Persist credited payout, claim status, best bonus and completed-level state in **one authoritative save operation**. Repeated callbacks, retry, restarting or clicking Continue twice must not duplicate Credits.

## 4. Staged implementation roadmap (one requested stage at a time)

**Stage order is binding**. Each stage has its own reviewable scope and explicit owner acceptance gate. Codex must not implement later stages merely because they are described here. When the owner asks Codex to start a stage, that request authorizes **only that stage**.

### CAMPAIGN1-A — Persistent Credits, owned roster, Headquarters and recruitment

**Goal:** Make Credits and permanently owned units real and persistent before shipping battle functionality.

Tasks:
1. Introduce AutoBattler-only, versioned per-profile campaign state with fresh default **500 Credits, 0 troops**, no research, and no completions; preserve existing completions via safe migration.
2. Add robust purchase transactions and a data-driven recruit-price/unlock catalogue.
3. Implement an appropriately styled Headquarters UI reachable via the existing AutoBattler menu/briefing; show Credits, owned army, estimated strength and locked units.
4. Enable purchasing Circle, Square and Triangle by quantity; show owned counts and remaining Credits. Additional types can appear as locked placeholders.
5. Restore state after reopening game or changing/reselecting profiles. A failed write must not show a confirmed purchase.
6. Do not implement research purchases, combat rewards, a second currency, or campaign deployment in this stage.
7. Keep Fortress Defense and Benchmark behavior unchanged.

**Owner validation:** New profile has 500 Credits and 0 owned units. Buy 20 Circles at 5 each → 400 Credits and 20 owned. Restart and confirm persistence. Change profiles and confirm isolation. Ensure Fortress Defense still uses Carbon.

**Codex prompt to run when ready:**
> Read LEVELS/AutoBattler/CAMPAIGN_PROGRESSION_IMPLEMENTATION_PLAN.md and the current README, CodePreferences and source. Implement **CAMPAIGN1-A ONLY** (500-credit AutoBattler economy, persistent owned roster, data-driven recruitment and Headquarters UI). Preserve Fortress Defense and all later phases as planned. Perform lightweight validation, update documentation/devlog, commit and push to main. Report SHA, changes and owner test steps. Stop before CAMPAIGN1-B.

### CAMPAIGN1-B — Inventory-backed Blue deployment and real autonomous battles

**Requires owner acceptance of CAMPAIGN1-A.**

**Goal:** Turn the existing experimental AutoBattler painter into a player-facing campaign battle, funded solely by owned units.

Tasks:
1. Integrate level briefing, authored Red forces and fixed Blue deployment areas with the shared mass-army simulation; reuse Benchmark/Deployment building blocks, not a second battle engine.
2. Show **Owned / Deployed / Available** per unit type and overall Blue/Red Battle Rating estimates. Permit any subset or **all owned troops**; do not impose an extra campaign count/supply/strength cap.
3. Painting consumes temporary deployment availability, **not Credits**; erasing restores deployment availability. Reject attempts beyond owned quantity; enforce authored Blue zones, footprints, obstacles and actual array/render capacity with clear feedback.
4. Retire/ignore AutoBattlerRules.BlueUnitLimit for player campaign play while preserving serialized compatibility with old authored resources and editor tests. Any special AllowedBlueUnitIds rule must be explicitly visible and author-intentional; never silently override roster ownership.
5. Freeze Blue and Red deployment when Fight begins; run normal autonomous targeting/movement/separation/projectiles/effects. No in-combat player RTS commands.
6. Track starting Blue IDs/counts, surviving originals and remaining enemies. Implement correct battle-end outcomes: default Eliminate Enemy, Blue wipe as defeat, and safe handling of empty enemy maps and impossible/stalemated combats; do not auto-complete a blank map.
7. Present preliminary victory/defeat results and Retry / Return. On retry restore original armies, positions, health and transient combat state. **Never decrement permanent owned quantities.**
8. This stage can display a provisional results screen; **do not credit reward payouts or unlock additional campaign levels** until CAMPAIGN2.
9. Leave the authoring Level Editor and its unrestricted playtest features intact. Do not overwrite existing authored levels.

**Owner validation:** Buy 100 Circles, deploy 60, erase 10, repaint 10, fight, lose or win, then retry. Exactly 100 remain permanently owned; no free troop injection; valid Red formations remain untouched. Verify rock/water/terrain deployment limits and large-but-valid rosters.

**Codex prompt to run when ready:**
> Read LEVELS/AutoBattler/CAMPAIGN_PROGRESSION_IMPLEMENTATION_PLAN.md and current source. Implement **CAMPAIGN1-B ONLY** (owned-roster Blue painting, legal deployment, autonomous battle, actual victory/defeat state and free retry). Do not introduce supply caps or permanent casualties. Do not implement rewards/research yet. Preserve Fortress Defense, Benchmark and authored levels. Follow CodePreferences, commit/push to main, report SHA and owner tests. Stop before CAMPAIGN2.

### CAMPAIGN2 — Victory rewards, one-time claims, sequential level unlocks

**Requires owner acceptance of CAMPAIGN1-B.**

**Goal:** Finish the credit-earning campaign loop and true progression.

Tasks:
1. Build results showing Blue deployed, original survivors, casualties, Red destroyed, survival ratio, one-time base Credits, newly earned best-survival bonus and updated balance.
2. Implement idempotent, atomic base-reward and survival-bonus claims, with zero payout on defeat and no duplicate claims on replay/reload.
3. Introduce editable authored reward values for each AutoBattler level (without changing Fortress Defense level rules). Keep campaign economic state authoritative.
4. Replace the AutoBattler Level001-only unlock shortcut with next-eligible-registered-level logic based on CampaignCatalog.OrderedLevels; handle gaps (e.g. Level002 followed by Level008), disabled or missing scenes, and already-completed levels. Preserve the debug-profile development bypass.
5. Completed levels stay replayable; locked levels remain visible. Continue / Headquarters / Replay must handle unsaved transitions safely.
6. Keep old progress records recoverable/migratable and never award retroactive base rewards twice.
7. **No research tree or unrelated level content in this stage.**

**Owner validation:** Clear Level001 → get only one base payout and unlock the next eligible registered level. Replay with worse survival → zero extra payout; replay with higher best survival → only difference. Restart and verify balance/completion; Fortress Defense progression unchanged.

**Codex prompt to run when ready:**
> Read LEVELS/AutoBattler/CAMPAIGN_PROGRESSION_IMPLEMENTATION_PLAN.md. Implement **CAMPAIGN2 ONLY** (victory results, first-clear Credits, best-survival replay rewards, true sequential unlocks and save safety). Preserve permanent roster, unrestricted owned deployment and Fortress Defense. Follow CodePreferences, commit/push to main, report SHA and testing steps. Stop before CAMPAIGN3-A.

### CAMPAIGN3-A — Permanent research tree using the same Credits

**Requires owner acceptance of CAMPAIGN2.**

**Goal:** Give players a meaningful choice between buying **more soldiers** and upgrading their existing permanent army.

Tasks:
1. Add small data-driven research definitions with stable ID, label, description, cost, prerequisites, eligible unit IDs/tags, effect type/amount and optional level-clear prerequisite.
2. Show an original compact connected tree or equivalent organized research UI within Headquarters. Nodes indicate Locked / Available / Purchased and affordability; no respec or second currency in this version.
3. Research purchase and Credit deduction must be one atomic persistent transaction; prevent duplicates and invalid prerequisite chains.
4. Include exemplar branches: Infantry (+10% HP, later +10% HP), Ranged (+10% damage, +8% range), Mobility (unlock Cavalry, then movement bonus), Heavy Weapons (unlock Pentagon/Hexagon/Artillery/Dreadnaught), Precision/Prism (unlock Prism Lance). Pricing and eligibility are editable starting balance proposals; refine from actual current roster.
5. Keep the starting Circle/Square/Triangle roster available with no research purchase. Newly unlocked recruitables appear in the existing Recruit UI.
6. **Factions must remain isolated:** Player Blue research improves the player's campaign Blue army only. Red soldiers, Fortress Defense and Benchmark retain their base stats. Never edit global UnitDefinition .tres assets as a way to apply research.
7. Resolve the purchased research into an efficient **per-faction campaign runtime modifier snapshot** before starting each battle; do not look up research trees in per-soldier combat hot loops. Preserve original stats for non-campaign modes and all other factions.
8. Define and cap stacking rules explicitly; upgrades must remain deterministic and explainable.

**Owner validation:** Buy an infantry HP upgrade; Blue infantry benefits in the campaign while Red infantry and Fortress Defense do not. Credits persist. Unlock Prism Lance and purchase it. Restart and verify purchases; current Benchmark tuning remains unchanged.

**Codex prompt to run when ready:**
> Read LEVELS/AutoBattler/CAMPAIGN_PROGRESSION_IMPLEMENTATION_PLAN.md and current source. Implement **CAMPAIGN3-A ONLY** (data-driven research tree, Credit-funded purchases, permanent tech unlocks and efficient Blue-only campaign combat modifiers). Do not modify shared UnitDefinition resources or Fortress Defense stats. Follow CodePreferences, commit/push to main, report SHA and owner checks. Stop before CAMPAIGN3-B.

### CAMPAIGN3-B — Authored-content support, balance tools and deployment templates

**Requires owner acceptance of CAMPAIGN3-A.**

**Goal:** Scale campaign content without introducing a parallel engine.

Tasks:
1. Improve authoring affordances for AutoBattler enemy compositions, legal Blue zones, optional level objectives, rewards and army intelligence as needed; preserve all current authored maps.
2. Help the owner balance early/simple formations, middle terrain/counterplay, and late combined arms using current units including Prism Lance and Dreadnaught; **do not auto-create or overwrite levels**.
3. Optionally add reusable player-side deployment templates; load only as many soldiers as actually owned and physically legal, with clear omissions.
4. Provide lightweight campaign stats, clearer results, and a few safe menu refinements only if useful during owner validation.
5. Leave advanced fog of war, permanent casualties, extra currencies, procedural campaigns and Fortress Defense integration outside this plan unless separately approved.

**Owner validation:** Several authored levels can be played with different researched armies, carried across profiles/sessions, without changing core mass-army behavior or losing saved work.

**Codex prompt to run when ready:**
> Read LEVELS/AutoBattler/CAMPAIGN_PROGRESSION_IMPLEMENTATION_PLAN.md. Implement **CAMPAIGN3-B ONLY** after reviewing the owner's current level content and latest gameplay source. Improve campaign authoring/UX and (if appropriate) safe deployment templates, without overwriting authored scenes or adding unintended mechanics. Follow CodePreferences, commit/push to main, report SHA and owner checks.

## 5. Cross-stage acceptance and engineering constraints

- **No gameplay cap on owned soldiers deployed.** Still reject impossible placements and report true engine capacity limits rather than crashing or silently truncating.
- **No permanent casualties** and no recruiting expense during deployment or battle.
- **One campaign currency**: Credits for recruits and research; Carbon only in Fortress Defense.
- **No double payments:** All reward state and balance changes are one authoritative operation, including previously claimed/best-bonus accounting. Victory events must be safe to repeat.
- **Compatibility:** Stable campaign/level/unit IDs and existing profile progress must survive; the editor may inspect locked levels, but normal players cannot launch them before unlock.
- **Author intent:** Keep Red formations, map metadata, defenses, terrain and custom level registration untouched unless the relevant phase specifically requires a compatible metadata addition.
- **Shared systems:** Reuse UnitCatalog, UnitState, SpatialGrid, painter, navigation, combat, rendering and effects. No per-soldier Node3D, physics body or extra campaign update loop.
- **Performance:** Compile modifiers before battle; event-driven menus and transactional purchases; bounded placement/validation; no repeated all-army UI refresh every simulation frame.
- **Role separation:** F5 player's campaign differs from F6 Game Editor/Benchmark; developer painter and debug controls stay available without leaking into player mode.
- **Difficulty:** Increase through composition, positioning, terrain and counters as well as larger armies. No assumption that adding more enemy soldiers alone balances unlimited owned deployment.
- **Validation:** Follow CodePreferences.md: focused source/diff review, at most one practical dotnet build for C# when feasible, no lengthy simulations or automated benchmark suites without explicit permission. Owner does in-game visual/FPS testing.
- **Commit policy:** One focused implementation commit **per separately authorized stage**, pushed to main, plus a concise append-only devlog.md entry in that stage's commit. Report commit SHA, actual changed paths, what was/wasn't checked and owner-side testing steps. **Do not mark a stage Accepted before the owner validates it.**

## 6. Stage tracker — update only as each phase is completed

| Stage | Status | Commit | Owner validation |
| --- | --- | --- | --- |
| CAMPAIGN1-A — Economy & HQ | **Source Implemented / Awaiting Owner Validation** | See implementation commit | Pending |
| CAMPAIGN1-B — Deployment & battle | **Source Implemented / Awaiting Owner Validation** | See implementation commit | Pending |
| CAMPAIGN2 — Rewards & unlocks | **Planned** | — | Pending |
| CAMPAIGN3-A — Research | **Planned** | — | Pending |
| CAMPAIGN3-B — Expansion & polish | **Planned** | — | Pending |

Statuses should advance through **Planned → Source Implemented / Awaiting Owner Validation → Accepted** after each independently approved pass.

**Next gate:** Owner validation of CAMPAIGN1-A and CAMPAIGN1-B. Begin CAMPAIGN2 only on explicit authorization.
