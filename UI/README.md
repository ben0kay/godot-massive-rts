# UI — Front End

**FRONT0 source implemented. Awaiting owner validation.**

F5 opens the profile picker. Choose or create a commander to reach the main menu, then open Campaign or Options. Escape returns from Campaign and Options to Main, or from Main to the profile picker. Display mode and VSync are global and saved when changed.

`Menus/FrontMenu.cs` owns screen actions, `Menus/MenuNavigation.cs` owns scene changes, and `Shared/` owns button styling and layout. Edit `DefaultUIConfig.tres` in the Inspector to tune the palette, spacing, and typography.

See [LEVELS](../LEVELS/README.md) for campaign authoring and [SYSTEMS](../SYSTEMS/README.md) for local saves. Open `BENCHMARK/BenchmarkArena.tscn` and use F6 for the standalone benchmark.
