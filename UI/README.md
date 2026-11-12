# UI — Front End

**FRONT0 source implemented. Awaiting owner validation.**

F5 opens the profile picker. Choose or create a commander to reach the main menu, then open Campaign or Options. Escape returns from Campaign and Options to Main, or from Main to the profile picker. Display mode and VSync are global and saved when changed.

`Menus/FrontMenu.cs` owns screen actions, `Menus/MenuNavigation.cs` owns scene changes, and `Shared/` owns button styling and layout. Edit `DefaultUIConfig.tres` in the Inspector to tune the palette, spacing, and typography.

See [LEVELS](../LEVELS/README.md) for campaign authoring and [SYSTEMS](../SYSTEMS/README.md) for local saves. Open `BENCHMARK/BenchmarkArena.tscn` and use F6 for the standalone benchmark.

## Planned FRONT1 — Campaign game-mode hierarchy (not yet implemented)

[FRONT1 design and Codex handoff](CAMPAIGN_MODE_STRUCTURE_PLAN.md): Campaign is the parent menu for **Open RTS → Level 001** and **Preset Battles → Level 001**. The catalog remains the source of mode/level choices, allowing future modes; both levels must have separate scenes. The Open RTS scene is reserved as an intentionally blank **owner-authored Godot map**. The current FRONT0 dropdown and single campaign remain unchanged until FRONT1 is implemented.
