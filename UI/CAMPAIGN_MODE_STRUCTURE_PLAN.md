# FRONT1 — Campaign Game Modes, Menu Hierarchy and Blank Open RTS Level 001

**Repository:** `ben0kay/godot-massive-rts`  
**Branch:** `main`  
**Status:** Source implemented. Awaiting owner validation.
**Owner intent:** Reorganize the existing FRONT0 Campaign menu to present two game modes, each with its own Level 001. Future modes should require data/catalogue authoring rather than additional hard-coded navigation branches.

## Required player-facing hierarchy

```text
Select / create profile
└── Main Menu
    ├── Campaign
    │   ├── Open RTS
    │   │   └── Level 001
    │   └── Preset Battles
    │       └── Level 001
    ├── Change Profile
    ├── Options
    └── Exit Game
```

**Campaign is a category**, not itself a battle type. **Open RTS** and **Preset Battles** are the two current planned game modes. "Preset Battles" is the new player-facing label for the previously discussed **Army Painter / tactical battle** concept. Additional modes can join Campaign later.

## FRONT0 baseline before implementation

- `project.godot` launches `UI/Menus/BootScreen.tscn` via F5; the benchmark remains directly runnable via F6.
- `UI/Menus/FrontMenu.cs` currently uses `MenuScreen.Campaign` to show one `OptionButton` of campaigns followed immediately by level buttons. It already derives both from `CampaignCatalog.Load()`, not a list of hard-coded UI entries.
- `LEVELS/Catalog/CampaignDefinition.cs` has `Id`, `DisplayName`, `Description`, `Levels`. `CampaignCatalog.cs` loads and validates the catalog and orders levels. Current `LEVELS/Catalog/DefaultCampaignCatalog.tres` registers only `LEVELS/Campaign001/Campaign001.tres`, displayed as "Main Campaign".
- The existing `LEVELS/Level001/Level001.tscn` is an intentionally blank `Node3D`. `LevelLauncher` uses campaign + level IDs and attaches temporary return/development completion controls.
- `CampaignProgressStore` keys completed levels by **campaign ID and level ID**, so a `Level001` ID can exist in each mode without collisions if the campaign IDs differ. Existing profile progress must be preserved.

## Required implementation — menu and catalog only

### A. Campaign mode selection

Change **Campaign** to show **one visible mode choice per registered `CampaignDefinition`**, instead of immediately presenting levels under the campaign selector. On choosing a mode, show **that mode's levels**. A simple two-stage view within `FrontMenu` is fine; do not build an elaborate frontend framework.

- Buttons/cards should be generated from `DefaultCampaignCatalog.tres` in authored order. Do not hard-code "Open RTS" and "Preset Battles" in a switch or duplicate lists in UI scripts.
- Level buttons remain generated from the selected campaign's `Levels` resource list.
- Preserve shared `UIButtonFactory`, `MenuLayout`, `UIMasterConfig`, selected profile checks, enabled/unavailable/locked labels, level descriptions and error reporting.
- Preserve keyboard/gamepad focus and Back/Escape behavior:
  - Main -> Campaign (mode selection)
  - Campaign mode selection -> Main
  - A mode's level list -> Campaign mode selection
  - Launched level -> the same mode's level list, ideally remembering the last selected mode
- Future game modes added to the catalog must appear without new hard-coded screen code.

### B. Backward-compatible resources and level scenes

**Recommended minimally disruptive mapping** (adjust only if current content has changed; preserve all stable authored references):

| Role | Catalog identity | Resources |
|---|---|---|
| Preset Battles | **Keep existing** `Campaign001` ID to preserve saved progress; rename **DisplayName** to `Preset Battles` | Keep `LEVELS/Campaign001/Campaign001.tres`, `LEVELS/Level001/Level001.tres`, and existing blank `LEVELS/Level001/Level001.tscn`. This is the future owner-authored tactical battle/Army Painter map. |
| Open RTS | New stable campaign ID, e.g. `OpenRTS` | New `LEVELS/OpenRTS/OpenRTS.tres`, `LEVELS/OpenRTS/Level001/Level001.tres`, `LEVELS/OpenRTS/Level001/Level001.tscn`. The `Level001` scene starts as a **blank, editable Godot scene** (e.g. root `Node3D`) and is where the owner will author the Open RTS battlefield later. |

- Register both campaign resources in `LEVELS/Catalog/DefaultCampaignCatalog.tres`. Preserve existing IDs, references and completion data.
- `Open RTS Level 001` is intentionally a **different scene** from `Preset Battles Level 001`; don't route both menu choices to one placeholder.
- Keep all levels data-driven, with no `Level001`/mode-specific launch branches except where genuinely necessary for mode-specific gameplay later.
- Both Level 001 buttons can launch their blank scenes and show the existing return/dev completion overlay as an interim navigation check. Do **not** claim the RTS game loop or preset battle experience exists just because the level scene loads.
- Ensure no level is marked complete merely for launching/returning; preserve dev-completion gating and per-profile progress isolation.

### C. Open RTS Level 001 is specifically hand-authored

The owner wants to **design the Open RTS map manually in Godot**. Reserve the blank scene for subsequent placement of terrain, obstacles, Carbon deposits, faction fabrication cores/Citadels, spawn areas and future structures.

- **Do not** generate random terrain/resource coordinates in the first map.
- **Do not** populate the level with default trees, woodland, medieval villages, existing benchmark armies or a fully working RTS system.
- Leave placement to normal Godot scene authoring; when map/building/resource systems are separately authorized, use editor-placeable scenes/resources with clear exported settings.
- Reuse shared `TERRAIN/`, `OBSTACLES/`, `UNITS/`, `SIMULATION/`, `COMBAT/` and `RENDERING/` infrastructure. The blank scene should not instantiate tens of thousands of units merely to test menu routing.
- `BENCHMARK/BenchmarkArena.tscn` remains untouched and independent.

## Open RTS direction — reference, not implementation scope

Read [`endgoal.md`](../endgoal.md), **Open RTS visual identity** subsection. The chosen visual direction is a **minimal futuristic vector-virus / competing machine-swarm** aesthetic: geometric red/blue troops, angular machine-like structures, dark vector terrain, sparse graphite-like **Carbon** deposits. Initially **one resource — Carbon** — supports gather -> return -> unit fabrication/basic construction. Triangle/arrow and chevron shapes visually connect ranged units. No medieval trees, forest economy or detailed machinery. Charge, Data, salvage, upgrades and other economy layers are future options only.

**This menu task does NOT implement workers, gathering, production, buildings, AI bases, map content, map editor, victory logic, or gameplay mode switches.** It only creates the scalable entry points and a level scene the owner can edit later.

## Target code owners

- `UI/Menus/FrontMenu.cs` — Campaign mode selection and level-list navigation.
- `UI/Menus/MenuNavigation.cs` — keep central scene navigation, adapt only if needed.
- `LEVELS/Catalog/DefaultCampaignCatalog.tres` and campaign/level `.tres` — register two modes and unique level scene references.
- `LEVELS/Runtime/LevelLauncher.cs` — preserve/adjust return-to-mode UX without compromising launch validation.
- `LEVELS/OpenRTS/` — new campaign resource + editable Level 001 scene and level definition.
- `LEVELS/README.md`, `UI/README.md`, `devlog.md` — update after implementation.

Maintain uppercase top-level folders and existing shared UI architecture. Do not create a second campaign registry, profile system, or menu implementation.

## Acceptance checks for Codex / owner

1. F5 -> profile picker -> Main -> Campaign shows **Open RTS** and **Preset Battles** as two distinct selectable modes.
2. Open RTS -> **Level 001** opens its own **blank** editable Open RTS scene; return takes the user to the Open RTS level list.
3. Preset Battles -> **Level 001** opens the current blank `LEVELS/Level001/Level001.tscn`; return goes to the Preset Battles list.
4. Back/Escape navigation works through the hierarchy without losing the selected profile, changing progress on return, or creating stuck focus.
5. Both modes and levels come from catalog resources; adding a third `CampaignDefinition` should add a third mode without editing menu labels or switches.
6. Old saved completion data for `Campaign001/Level001` remains associated with Preset Battles; no profile/progress migration or destructive reset.
7. Direct F6 run of `BENCHMARK/BenchmarkArena.tscn` still works; no extra per-soldier work or game-scene initialization from menus.
8. Minimal source review / one feasible `dotnet build`, per `CodePreferences.md`. Owner validates in Godot.

**Suggested handoff:** Implement **FRONT1 menu/catalog and blank Open RTS Level 001 only**, update docs and devlog, single commit/push to `main`, report SHA and validation. Defer the actual Carbon economy and hand-authored map placement to follow-up tasks.
