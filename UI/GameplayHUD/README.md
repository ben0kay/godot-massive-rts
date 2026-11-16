# Gameplay HUD

A small, presentation-only HUD area independent of benchmark tooling and menu screens.

## Army balance bar

`ArmyBalanceHud.cs` / `ArmyBalanceHud.tscn` draw a 12px-high, centered top-of-screen army-strength strip. **Blue fills from the left; Red fills from the right.** In three-faction matches, Green appears between them with its own live count. The segment widths represent **proportions of living soldiers** rather than each faction's original troop count or total health.

Examples:
- Blue 5,000, Red 1,000: Blue occupies 83.3%, Red 16.7%.
- Blue 4,800, Red 200: Blue 96%, Red 4%.
- A wiped-out team has no remaining segment. If every faction is eliminated, only the dark background remains.

The controlling arena calls `BeginBattle(UnitState)` after incremental troop initialization is complete, `RefreshCounts()` at its existing 0.3-second diagnostics cadence, and `Reset()` when clearing/re-editing. Refresh reads the **three cached `UnitState.FactionAlive` counters**—no per-soldier pass, no extra death hooks, no progress I/O, and no changes to gameplay math.

Proportions interpolate with a lightweight exponential ease only after count changes; `_Process` disables itself when the fractions settle. Counts update directly. A live comparison stays consistent in paused, render-only and movement-only modes. The bar disappears while spawning and during deployment painting.

The HUD is one `Control` inside one `CanvasLayer` above the benchmark's two side panels; drawing is pointer-transparent. Its screen-space width adapts to the viewport, reserving space for left/right panels. Combat data is supplied explicitly; it does not reach into the benchmark scene by path.

**Source implemented; pending owner visual validation.** Check the bar in blue/red and green-included battles, respawns, casualties, pause, and different window widths. No performance guarantee is asserted.

## Selection bar

The Command Sandbox uses this army balance bar and adds `SelectionHud.cs` along the bottom. The left side lists the number of selected living units and a count per unit type. Health divides the sum of their current HP by the sum of their maximum HP, so each unit contributes according to its HP pool. The HUD receives the selected unit IDs and supports mixed types. The sandbox currently selects one squad of 20 Circles.

## FAB1 controls

`FabricationHud.cs` adds Carbon at the top left, a Ctrl-toggled build menu, and a bottom panel for selected structures. Fabricator controls show the next recipe, current batch progress, automatic production and rally status. The existing unit selection panel returns when selecting soldiers.

**CommandBottomHud** owns one viewport-anchored bottom frame for army selection, structures and Carbon deposits. Ctrl toggles the build tray beside the active context. The Fabricator roster scrolls within its fixed list area. The Citadel context shows the Worker queue, progress and any block reason. Select either the Citadel or a completed friendly Fabricator and right-click clear terrain to set its rally point. Use **Clear rally** to send future units to the default exit area.

In Fortress Defense, tap Ctrl and choose **Laser Tower** to place one for 750 Carbon. Left-click a clear, level site on the build grid, or right-click to cancel. The tower activates on placement and uses the same stats and line-of-sight rules as Laser Tower 001 in the benchmark. Select it to see its health.

## CARBON2 selection

The shared bottom HUD now describes Carbon Deposits as untapped, assigned, under construction, active (Carbon/s), or depleted. The **Carbon Extractor** entry in the Ctrl build tray lets you choose a deposit or unfinished rig. Placement assigns the nearest available Worker. Selected Extractors display building HP and active income or construction progress in the same existing bottom HUD frame; no second panel was added.
