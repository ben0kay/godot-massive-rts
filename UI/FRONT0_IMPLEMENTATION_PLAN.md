# FRONT0 — Profiles, Local Campaign Progress, Consistent Menus and Level Launch

**Repository:** ben0kay/godot-massive-rts
**Branch:** main
**Status:** PLANNED — documentation only. There is no implemented front end/profile/campaign system from this planning commit.
**Scope:** A thin and usable game shell in front of the existing Godot 4 C# 3D benchmark. ONE complete minimum vertical slice; do not implement the eventual full RTS game.

## 1. Owner intent and observed repo

The user wants the structural simplicity of godot-tower-defense's local profile picker, main menu, level selection and shared button styling, but with a more minimalist vector/geometry UI for this 3D mass-army game.

Desired normal launch flow:

Boot / Choose or Create Profile
    -> Main Menu: Campaign / Change Profile / Options / Exit
    -> Campaign Menu: choose authored campaign and click a level button
    -> Level 1: launch the current benchmark battle scene for testing
    -> Return to Campaign / Main Menu.

The existing benchmark scene MUST continue to work on its own in the Godot editor. Do not move BENCHMARK into LEVELS, duplicate it, or initialize the simulation during Boot/Menu.

Current project facts when this was planned:
- project.godot sets run/main_scene to res://BENCHMARK/BenchmarkArena.tscn.
- BENCHMARK/BenchmarkArena.cs is a Node3D with performance and battle lifecycle, and BENCHMARK/UI/BenchmarkPanel.cs is a CanvasLayer with many developer controls.
- BENCHMARK/BenchmarkArena.tscn is the existing playable testing/placeholder level.
- The project already has separate capitalized BENCHMARK, SIMULATION, RENDERING, DEPLOYMENT, TERRAIN, OBSTACLES, etc. roots.
- Godot C# 4.7 project, with lightweight build/check preference.
- endgoal.md distinguishes Army Painter campaign mode and future Open RTS mode. FRONT0 targets only the former as a minimal front end; do not implement the future base-builder.

Check live main at implementation time; these facts may have changed.

## 2. Tower Defense reference — reuse patterns, not its complexity

Reference repository: ben0kay/godot-tower-defense, branch dev. Read these files through GitHub (if accessible) or use the file list below as a pattern:

- SYSTEMS/Profiles/PlayerProfile.cs
- SYSTEMS/Profiles/ProfileStore.cs
- SYSTEMS/Saving/CampaignStore.cs and CampaignData.cs (architectural reference ONLY)
- UI/Shared/UIButtonFactory.cs
- UI/Shared/MenuLayout.cs
- UI/UIMasterConfig.cs and UI/DefaultUIConfig.tres
- UI/Menus/MenuNavigation.cs
- UI/Menus/Boot/BootScreen.cs
- UI/Menus/Main/MainMenu.cs
- UI/Menus/Campaign/CampaignMenu.cs
- LEVELS/Catalog/MissionDefinition.cs and MissionCatalog.cs

Tower Defense uses a GUID-based profile index, validated atomic JSON saves with backups, a Boot picker, scene routing, an Inspector-editable master UI theme, reusable factory buttons, and data-only mission catalog entries. Adapt those CONCEPTS.

Do NOT copy its tower UI, wave checkpoint state, CampaignSession, research, XP, loading screens, audio manager, results rewards, save fingerprints or heavy mission loader. No separate economy data, AI campaign progress or gameplay-save snapshots in FRONT0.

## 3. Ownership and proposed folder structure

Create major root folders only as code is implemented, keeping the structure shallow:

UI/
  README.md
  FRONT0_IMPLEMENTATION_PLAN.md
  UIMasterConfig.cs
  DefaultUIConfig.tres
  Shared/
    UIButtonFactory.cs
    MenuLayout.cs
  Menus/
    MenuNavigation.cs
    Boot/
      BootScreen.cs
      BootScreen.tscn
    Main/
      MainMenu.cs
      MainMenu.tscn
    Campaign/
      CampaignMenu.cs
      CampaignMenu.tscn
    Options/
      OptionsMenu.cs
      OptionsMenu.tscn

SYSTEMS/
  Profiles/
    PlayerProfile.cs
    ProfileStore.cs
    README.md
  Saving/
    AtomicJsonFile.cs
    CampaignProgress.cs
    CampaignProgressStore.cs
    README.md
  Settings/
    GameOptions.cs
    OptionsStore.cs

LEVELS/
  README.md
  Catalog/
    LevelDefinition.cs
    CampaignDefinition.cs
    CampaignCatalog.cs
    DefaultCampaignCatalog.tres
  Campaign001/
    Campaign001.tres
  Level001/
    Level001.tres
  Runtime/
    LevelLauncher.cs
    LevelSession.cs

BENCHMARK/ must stay in its current location.
Existing README.md, endgoal.md and project.godot may need small updates.

Use res:// in resource paths and the existing .tscn/.tres conventions. Names are suggestions; keep naming consistent and avoid unnecessary wrappers.

## 4. Profile identity and safe persistence

ProfileStore is a small local profile/index service; PlayerProfile contains only:
- Id: stable GUID (N-format string), never derived from display name.
- Name: trimmed, 1–32 characters, no control characters; reject duplicate display names case-insensitively.
- CreatedUtc.

File layout:

user://Profiles/profiles.json
user://Profiles/{profile-guid}/campaign_progress.json
user://Settings/options.json

profiles.json is a versioned local index (Version=1). On normal startup, Boot presents saved profiles and Create Profile. Do NOT silently create or autoselect a profile. Newly created profile is persisted before it becomes active.

Boot operations:
- Select by GUID -> Main Menu.
- Create via modal/LineEdit -> persist, select, Main Menu.
- Empty state explains how to create a profile.
- Invalid and duplicate names show visible messages, not crashes.
- Exit is available.
- Main Menu Change Profile clears only in-memory selection/current session and returns to Boot; do not remove any disk data.
- Deleting/renaming profiles is OUT OF SCOPE; can be added later.

All files live under Godot's user:// folder and work in a Windows exported build. Use safe GUID validation before constructing paths; never use untrusted profile names as paths. Keep read/write I/O on create, select and progress changes — no polling on _Process / 30 Hz combat ticks.

Prefer simple System.Text.Json DTOs with explicit Version and finite file size bounds. Share an AtomicJsonFile helper for writing a temp file, flushing, replacing and maintaining a valid backup, instead of duplicating persistence code. On read errors, recover from a validated .bak if present; if both damaged, preserve files and show a non-destructive error to the user. Don't silently reset/corrupt data.

Implement actual validation for version, IDs, array lengths/counts and well-formed progress entries. Do not expand to sophisticated checkpoint-migration machinery.

## 5. Campaign progress vs active battle saves

In FRONT0, save **progress**, NOT every soldier or live battle state.

A versioned CampaignProgress record (per profile) may contain:
- Version = 1
- ProfileId, matching selected profile GUID
- CompletedLevelIds keyed by CampaignId (sets/arrays of stable level IDs)
- LastPlayedCampaignId and LastPlayedLevelId (optional)
- UpdatedUtc (optional, valid UTC).

Do NOT store unlocked status independently: derive it from catalog order + completion state (first level unlocked; a later level unlocked after preceding level completed). The catalog is source of truth for names and order. Preserve unknown completed IDs gracefully for content changes; never grant a level completion from merely opening or exiting a battle.

Allow multiple campaigns to share one profile via stable CampaignId keys, even if only one campaign is authored now. Selection between campaigns is a simple menu selector/card; the Campaign Menu shows levels for the chosen campaign.

The benchmark does not currently have final victory/defeat rules. Therefore:
- Level 1 launches BENCHMARK/BenchmarkArena.tscn as a TEMPORARY playable level.
- Provide a clearly marked development-only command such as "MARK TEST LEVEL COMPLETE" in campaign-launched benchmark mode, gated by an explicit development config flag, and save completion through LevelSession/ProgressStore.
- Provide "RETURN TO CAMPAIGN" without marking completion.
- Do not show fake win/results or automatically mark Level 1 complete when the player exits.
- Future legitimate level victory/defeat will call a single LevelSession.CompleteCurrentLevel(result) API; a loss or abandonment must not mark completion.
- No mission rewards, player XP, research, multiple battle checkpoints or autosave of units yet.

Save campaign progress only on an explicit completion or legitimate progress mutation, with atomic write and error feedback. Switching to a different profile switches progress and never leaks the previous profile's completion state.

## 6. Data-driven campaign and level definitions

Use Godot Inspector-editable Resource classes under LEVELS/Catalog:

LevelDefinition:
- Stable Id (e.g. "Level001"), display name, order, optional short description.
- PackedScene/scene resource or validated scene path, e.g. res://BENCHMARK/BenchmarkArena.tscn.
- Enabled / IsPlayable flag.
- Optional prototype benchmark tag for temporary use, not a separate gameplay engine.
- No resource content is itself a save file.

CampaignDefinition:
- Stable Id (e.g. "Campaign001"), display name, short description.
- Ordered list of LevelDefinition resources.

CampaignCatalog:
- Authored list of CampaignDefinition resources.
- Resolve IDs, validate missing/null/duplicate IDs, sort order and validate referenced scene paths.
- Data-only: loading a menu must NOT instantiate all battle scenes.
- Invalid authoring produces a visible error rather than silently launching an unintended scene.

At initial implementation:
- One Main Campaign (Campaign001).
- One Level 1 (Level001), title such as "Battlefield Test", scene = existing BENCHMARK/BenchmarkArena.tscn.
- Show one usable button for Level 1; more actual playable level buttons appear automatically when new LevelDefinition .tres resources are added.
- Future disabled/locked entries should have consistent disabled styling and clear "LOCKED" or "COMING SOON" labels; don't pretend unfinished levels are implemented.

Don't manually hardcode Level001/002/003 cases into menu actions. The menu generates buttons from catalog entries. A profile's completion state controls unlock presentation, not the resource's existence.

This design can later add authored battle maps in LEVELS/Level002/ and procedurally generated level definitions without rewriting profile, menu or navigation systems.

## 7. Shared UI design, consistency and menus

Visual direction: minimal dark charcoal/navy, clean vector-style panels, subtle cyan/blue accent and readable text. No glossy sci-fi tower HUD, elaborate art, dynamic backgrounds, animated particle menu FX, audio dependencies or expensive 3D menu scene.

Create an Inspector-editable UIMasterConfig Resource + DefaultUIConfig.tres with:
- background, panel, button, accent, text and muted text colors
- panel width, common button height, margins/spacing, corner radius and typography sizes
- optional focus/hover/disabled colors or derived shades.

All menus should use shared UIButtonFactory.Create(text, action, style) for dynamic buttons and UIButtonFactory.Apply(existingButton, style) for authored buttons, plus a common MenuLayout.Create(root, heading, subtitle). Styles: Primary, Secondary, Compact, Danger, Disabled/Locked (or equivalent).

BUTTON RULE: a single factory is responsible for standard size, padding, backgrounds, StyleBoxFlat normal/hover/pressed/focus/disabled, focus flags and hover state. Individual screens own button actions and labels only. Do not hand-copy style overrides across each screen. Prefer shared cached StyleBoxes/Theme where safe; no per-frame button rebuilding. Support keyboard/gamepad focus and readable hover/focus indicators. Button styling should remain usable at 1920x1080 and smaller window sizes.

Boot screen:
- "SELECT PROFILE", scrollable list of saved profiles, Create, Exit.
- Profile create modal with validation.
- No game world loaded.

Main menu:
- prominent profile/commander name
- CAMPAIGN
- CHANGE PROFILE
- OPTIONS
- EXIT GAME
- one shared Back/Escape policy: Main goes Boot, Campaign/Options go Main. A modal should consume Escape before parent navigation.

Campaign menu:
- campaign selection (dropdown/buttons, scales beyond one campaign later)
- scrollable list/grid of levels generated from campaign catalog
- show "Level 1 — Battlefield Test" and completion/locked labels as applicable
- Back to Main Menu
- no XP/research/continue checkpoint button yet.

Options menu:
- make it FUNCTIONAL, but tiny: Windowed/Fullscreen and VSync On/Off, with Back to Main.
- Persist to user://Settings/options.json, global rather than per-profile.
- Apply once on startup/change, not each frame.
- Sound sliders only if a real bus exists; do not invent audio systems.

Keep options, UI style, profiles, campaign unlocks and benchmark config conceptually separate. The benchmark's developer gameplay toggles must remain in BENCHMARK/UI/BenchmarkPanel.cs, not be rehomed into Options.

## 8. Navigation, level launch and return path

Build UI/Menus/MenuNavigation as the sole scene-transition helper. Centralize paths to Boot/Main/Campaign/Options, reset SceneTree.Paused appropriately, preserve or restore valid UI if a scene is missing, avoid double-click launches. ChangeSceneToFile or a PackedScene path is fine.

Create LEVELS/Runtime/LevelLauncher + LevelSession as a minimal ownership boundary:
- LevelLauncher validates selected profile, selected campaign/level, unlock status, enabled flag and packed scene existence before transition.
- Store temporary in-memory session context: selected ProfileId, CampaignId, LevelId, scene path, prototype flag. Keep context small, no live troop data.
- Change the current scene to the existing BENCHMARK/BenchmarkArena.tscn when Level001 is selected.
- When the player returns to Campaign, clear session safely, tear down the benchmark scene normally, and display the same selected profile/campaign.
- If benchmark scene is run directly from editor via Run Current Scene/F6, it has NO campaign session; it should continue operating as the standalone benchmark, and campaign-only Return/Test Complete controls should be hidden.
- Preserve benchmark overlays, painted deployment, terrain test plateau, obstacle navigation and 20k troop performance. No UI/profile/campaign polling inside the simulation loops.
- The final LevelSession API can later accept a real level completion event, but don't fake victory/defeat for this benchmark placeholder.

IMPORTANT: Update project.godot run/main_scene to BootScreen.tscn for normal Run Project/F5. Do not edit the benchmark scene resource path or prevent editor Run Current Scene from bypassing Boot. All menu scenes are 2D Control layers; battle remains 3D.

Do NOT add gameplay data to ProfileStore (identity only) or make BENCHMARK own campaign JSON writes. Keep system boundaries explicit.

## 9. Implementation ordering for Codex

Aim for ONE coherent usable vertical slice, not three incompatible skeletons. Suggested internal order:

A. **Profile and saving foundation:** PlayerProfile, ProfileStore, AtomicJsonFile, campaign progress DTO/store, options DTO/store. Validate error recovery, profile isolation and persistence.
B. **Shared UI:** UIMasterConfig.tres, factory, layout, navigation; Boot, Main and Options screens. Set Boot as Run Project main scene. Ensure direct benchmark remains runnable.
C. **Campaign catalog + menu:** resources, one campaign and Level001 entry, dynamically generated buttons, sequential unlock logic.
D. **Level launch integration:** LevelLauncher/LevelSession, main menu launch, benchmark conditional Return to Campaign, clearly development-only Mark Test Complete command. No fake battle results.
E. **Documentation:** update UI, SYSTEMS, LEVELS and root README; one concise devlog.md entry when source is actually implemented.

Prefer one focused implementation commit/push, minimal source review and one readily available dotnet build, per NOTES/CodePreferences.md. Do not attempt full automated Godot testing or expensive benchmarks; owner will test UI and battle launch. Do not change soldier balance, unit code, rendering, terrain, obstacles or navigation except what is strictly necessary for scene transitions.

## 10. Owner acceptance checklist

- Start game with F5/Run Project: profile Boot appears, no giant army spawned.
- Create profile "Commander A", quit/restart, select same profile. Name persists.
- Create "Commander B"; selected profile and campaign progress remain isolated.
- Duplicate/invalid names rejected; corrupt profile/progress JSON (with/without valid backup) shows a clear recoverable error and does not overwrite originals.
- Main shows Campaign/Change Profile/Options/Exit. Change Profile returns to Boot without deleting either profile.
- Options windowed/fullscreen and VSync persist across restart. All screens use identical button styling, hover, focus and widths, including disabled states.
- Campaign menu lists Level 1 from data-only .tres resources. Clicking Level 1 launches existing benchmark scene.
- Return to Campaign never marks success. Mark Test Complete (only in explicit development mode) saves completion and returns. Relaunch/reselect same profile -> completion remains. Other profile not completed.
- Level 2 can later be added with only resource/scene authoring and automatically gets a button; unlocking depends on completion of its predecessor. No code-specific Level2 button.
- F6/Run Current Scene directly on BENCHMARK/BenchmarkArena.tscn works without profile, level context or a menu runtime.
- Profile creation and menu transitions do not add any per-tick I/O, nodes or changes to core 10k/20k benchmark soldier simulation.
- If a referenced level scene is missing/invalid, menu displays an error and stays usable.

## 11. Non-goals

No live-battle save/load, troop capture/recovery, gameplay XP, research, unlock economy, research points, levels 2+ content, procedural campaign generator, full victory/defeat rules, rewards, custom graphics, multiplayer, cloud accounts, automatic sign-in, delete/rename profiles, achievements or extensive music/audio. All of these can be introduced later without changing the FRONT0 profile GUID and catalog ID formats.

**This document is a Codex handoff plan only. Do not claim source implementation until Codex completes it.**
