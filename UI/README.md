# UI — Shared Front End and Master Styles

**Status:** FRONT0 planned, not implemented.

The [FRONT0 plan](FRONT0_IMPLEMENTATION_PLAN.md) describes a lightweight profile picker, main menu, campaign level picker and functional options screen for this 3D mass-RTS. All menu buttons use one UIButtonFactory and Inspector-editable DefaultUIConfig.tres; MenuLayout creates a consistent panel and MenuNavigation routes between scenes.

The frontend stays separate from BENCHMARK. Existing BENCHMARK/BenchmarkArena.tscn remains a directly launchable Godot test scene and becomes the temporary Level 1 destination in the first campaign. SYSTEMS owns persisted profile GUIDs and campaign completion; LEVELS owns campaign catalog resources and the level-launch context.

Use the reference patterns in ben0kay/godot-tower-defense (branch dev) without copying its complex tower-defense HUD, wave checkpoints, XP, research or audio systems.
