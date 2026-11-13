# UI — Front End

**FRONT0 and FRONT1 source implemented. Awaiting owner validation.**

F5 opens the profile picker. Choose or create a commander to reach the main menu, then open Campaign or Options. Escape returns from a level to its mode’s level list, from a level list to the Campaign mode picker, from Campaign or Options to Main, and from Main to the profile picker. Display mode and VSync are global and saved when changed.

`Menus/FrontMenu.cs` owns screen actions, `Menus/MenuNavigation.cs` owns scene changes, and `Shared/` owns button styling and layout. Edit `DefaultUIConfig.tres` in the Inspector to tune the palette, spacing, and typography.

See [LEVELS](../LEVELS/README.md) for campaign authoring and [SYSTEMS](../SYSTEMS/README.md) for local saves. Open `BENCHMARK/BenchmarkArena.tscn` and use F6 for the standalone benchmark.

## FRONT1 — Campaign game modes

Campaign shows **Open RTS** and **Preset Battles**, each with its own **Level 001**. Mode buttons follow the order in `LEVELS/Catalog/DefaultCampaignCatalog.tres`. A launched level returns to the level list for its own mode. See the [FRONT1 plan](CAMPAIGN_MODE_STRUCTURE_PLAN.md) for the compatibility mapping and owner checks.

## Gameplay HUD

[UI/GameplayHUD](GameplayHUD/README.md) owns the new **army balance bar** used by BENCHMARK. Unlike the development diagnostics panels, the bar is reusable presentation UI: Blue on the left, Red on the right, optional Green in the middle, smoothly tracking surviving unit shares from cached faction totals. It stays hidden during deployment editing and generation.
