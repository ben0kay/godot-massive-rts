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
