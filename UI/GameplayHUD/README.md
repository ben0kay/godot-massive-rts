# Gameplay HUD

A small, presentation-only HUD area independent of benchmark tooling and menu screens.

## Tactical battle HUD

**HUD1 — Source implemented / Awaiting owner validation.** Skirmishes and Missions use
`TacticalBattleHud` after deployment. `TacticalHudStyle` supplies the shared frames
and typography. The benchmark and command sandbox continue to use
`ArmyBalanceHud.tscn` for ratio diagnostics.

The shared arena supplies the level title and objective, the actual Citadel health
reference, reinforcement status, and executed fixed-step time. The speed control
cycles 0.5×, 1×, 2× and 4× on the same fixed-step scheduler. F1 toggles the tactical
HUD during battle. The Battle Overview remembers its collapsed state for the
session. Larger faction rosters are paged four at a time.

Ground and air statistics are updated at existing spawn, damage and death
boundaries. Strength sums living BattleRating values and health is the ratio of
current to maximum living HP. Casualties count deaths independently of arrivals.
The HUD polls the cached totals roughly every 0.3 seconds without scanning units.

`TacticalMinimap` caches a low-resolution terrain texture from the authored
heightfield, cover, water and bridges. At up to 4 Hz it bins living ground units
into a 48×48 density texture, and draws aircraft and structures separately.
Enemy trench concealment uses the shared visibility rule from the outside-ground
observer's perspective. The camera outline follows the current view and left
click or drag pans the existing camera. The HUD creates no second 3D view.

Owner checks include the three objectives, reinforcement and casualty totals,
pause/speed, retry, minimap clicks and drag, 1080p and 1440p layout, and large-army
frame timing with the HUD shown and hidden.

## Selection bar

The Command Sandbox uses this army balance bar and adds `SelectionHud.cs` along the bottom. The left side lists the number of selected living units and a count per unit type. Health divides the sum of their current HP by the sum of their maximum HP, so each unit contributes according to its HP pool. The HUD receives the selected unit IDs and supports mixed types. The sandbox currently selects one squad of 20 Circles.

## FAB1 controls

`FabricationHud.cs` adds Carbon at the top left, a Ctrl-toggled build menu, and a bottom panel for selected structures. Fabricator controls show the next recipe, current batch progress, automatic production and rally status. The existing unit selection panel returns when selecting soldiers.

**CommandBottomHud** owns one viewport-anchored bottom frame for army selection, structures and Carbon deposits. Ctrl toggles the build tray beside the active context. The Fabricator roster scrolls within its fixed list area. The Citadel context shows the Worker queue, progress and any block reason. Select either the Citadel or a completed friendly Fabricator and right-click clear terrain to set its rally point. Use **Clear rally** to send future units to the default exit area.

In Fortress Defense, tap Ctrl and choose **Railgun Tower** to place one for 750 Carbon. Left-click a clear, level site on the build grid, or right-click to cancel. The tower activates on placement and uses the same stats and line-of-sight rules as Railgun Tower in the benchmark. Select it to see its health.

## CARBON2 selection

The shared bottom HUD now describes Carbon Deposits as untapped, assigned, under construction, active (Carbon/s), or depleted. The **Carbon Extractor** entry in the Ctrl build tray lets you choose a deposit or unfinished rig. Placement assigns the nearest available Worker. Selected Extractors display building HP and active income or construction progress in the same existing bottom HUD frame; no second panel was added.

### Citadel objectives

Offense shows the Red Citadel and Defense shows the Blue Citadel. Clash omits the
objective panel. The level editor's benchmark playtest retains its existing
objective strip.
