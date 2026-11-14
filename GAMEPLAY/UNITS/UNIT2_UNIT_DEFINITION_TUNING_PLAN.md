# UNIT2 — Per-unit combat and balance parameters

**Status:** Source implemented. Awaiting owner verification in Godot.
**Repository:** `ben0kay/godot-massive-rts` · **Branch:** `main`
**Goal:** Make each `UNITS/DEFINITIONS/*.tres` the convenient single source for a soldier's gameplay numbers, especially melee arc/cleave and abilities, without sacrificing the compact high-unit-count simulation.

## Existing behavior — read before editing

- `GAMEPLAY/UNITS/DEFINITIONS/UnitDefinition.cs` already exports base dimensions, size multiplier, HP, speed, acceleration override, `Damage`, `Interval`, `Reach`, `Armour`, cleave angle/fraction, shockwave frequency/radius/damage/edge fraction, charge resistance and trample fields.
- `CleaveAngleDegrees` already defaults to **100°**, and `CleaveSecondaryFraction` to **0.65**, under the **Melee cleave** Inspector group. They do **not** appear as literal lines in `HexagonSiege.tres` or `PentagonElite.tres` because both inherit C# defaults. Explicit per-unit values such as `CleaveAngleDegrees = 140.0` work now.
- `GAMEPLAY/COMBAT/AreaDamage.cs` hard-codes **up to two additional cleave targets**. `GAMEPLAY/COMBAT/CombatSystem.cs` hard-codes the **100° single-target melee facing cone**, as well as a **0.08 contact tolerance**. The cleave itself uses the exported angle.
- **Normal melee lands at physical footprint contact**, with shared separation gap and small tolerance. `Reach` remains relevant to acquisition/other mechanics; it does **not** currently extend ordinary melee attack contact distance. Do not silently change that rule.
- The Hexagon's current shockwave values are **every 5th eligible attack**, **25 central damage**, **15 outer-edge damage**, **8 world-unit effective radius** (4 base × 2 size). Its ordinary melee damage is 34; the normal cleave has 100° arc, 65% secondary damage and two extra victims.
- The project compiles authored `UnitDefinition` resources **once per unit type** into compact `UnitStats` in `GAMEPLAY/UNITS/CATALOG/UnitCatalog.cs`; maintain this model. An ability should not require a per-soldier Godot node.

## Requested small implementation

1. **Improve the Inspector layout, not the runtime architecture.** Organize the existing serialized fields with `[ExportGroup]` for **Identity / Appearance & Size / Movement / Defense / Primary Attack / Melee Cleave / Shockwave / Trample** (and ranged/projectile fields where relevant). Keep existing property names and serialized values stable to avoid breaking `.tres` assets, scenes and saves. Inspector range hints should make percentage/angle units clear. Document ambiguous meanings, especially `Reach`, `Armour`, `ChargeResistance`, `SizeMultiplier` and interval.
2. **Explicitly author inherited combat defaults in the relevant `.tres` assets** so someone reading or editing the raw files can see them: at minimum Pentagon and Hexagon `CleaveAngleDegrees = 100.0` and `CleaveSecondaryFraction = 0.65`. Keep all current damage, sizes, speeds and attacks exactly the same by default.
3. **Expose the number of secondary cleave victims** with a per-unit `CleaveMaxAdditionalTargets` integer (default **2**, reasonable small bounded maximum such as **4**). A value of 0 means primary only. Replace the two-slot special case in `AreaDamage.Cleave` with a bounded, allocation-free nearest-candidate selection. Preserve current behavior and tie ordering when set to 2; do not replace the shared spatial grid or introduce arbitrary victim lists/allocations.
4. **Expose ordinary melee facing angle** via `MeleeFacingAngleDegrees` (default **100°**) for *non-cleave directional melee*. Leave `CleaveAngleDegrees` as the forward arc for cleave-enabled melee, both for facing/attack permission and secondary-victim eligibility. Keep Circle Infantry's intentional facing optimization; do not accidentally make round soldiers calculate render-facing rotations.
5. **Make shockwave edge damage intuitive.** Currently users must compute a fraction: `ShockwaveDamage = 25`, `ShockwaveEdgeDamageFraction = 0.6` gives 15 at the edge. Prefer adding a directly editable **`ShockwaveOuterDamage`** value so the Hexagon can show **25 centre / 15 outer** in its resource. Preserve existing resources that use the fraction through a careful compatible migration/fallback; avoid exposing two contradictory active settings. Retain current linear falloff, radial cover logic, radius scaling and ability cadence. If a clean compatible direct-damage field would require extensive changes, document the fraction field clearly and keep this enhancement as a separate small follow-up instead of rewriting combat.
6. **Defense now, resistances later.** Keep current **Armour** (general damage reduction) and **ChargeResistance** separately labeled. *Future* damage-type resistance fields (e.g., melee, arrow, area/shockwave) are design ideas, **not an instruction to implement a damage-type matrix** in UNIT2. When that feature is authorized, author it on the unit definition and compile it per archetype.

## Performance and correctness constraints

- Compile any new angles to cosine/cosine-squared and other derived values **once per unit type**; do not call trigonometry each attack.
- Preserve the existing fixed-step simulation, shared grid, melee contact rules, targeting, separation, MultiMesh, damage feedback, saves and benchmark modes. No per-soldier resources, nodes, physics bodies, signals, allocations or new hot-path virtual calls.
- Keep the new max-cleave-target loop bounded and measured; do not expand search radius, candidate budgets or scan every soldier on each hit.
- All six unit resources should remain compatible; units without cleave or shockwaves should not incur extra meaningful work.
- If the user changes a `.tres` at authoring time, changes can take effect on the next arena restart/reload. **Live hot-editing is not required.**
- Don't change game balance in this task. Preserve current Hexagon 100° / 34 primary / 65% secondary / 2 extra targets / shockwave every 5, 25→15; Pentagon 100° and two secondary victims; other existing stats.

## Owner acceptance checklist (lightweight)

- Open `HexagonSiege.tres` in Godot Inspector; find size/HP/speed, primary attack, **cleave angle**, **secondary target count/damage**, **shockwave centre/outer damage** and resistances without opening C#.
- Temporarily adjust Hexagon cleave from 100° to 140°, play a small battle, restore to 100°. Check that damage only occurs at valid body contact and inside the intended forward cone.
- Verify default two-extra-target cleave still behaves like before; changing the cap to 0 or 3 affects only that unit type.
- Confirm Hexagon shockwave still has 25 centre / 15 outer, every fifth successful attack, radius 8, and does not attack through covered obstacles.
- Compare a representative dense fight against the previous commit if profiling is warranted; note any measurable new spikes or allocations.

## Codex task handoff

Read `README.md`, `NOTES/CodePreferences.md`, `GAMEPLAY/UNITS/README.md`, `GAMEPLAY/COMBAT/README.md` and the source paths above. Implement **UNIT2 only** as a focused, backward-compatible improvement to **per-unit parameter authoring**; no unrelated AI, pathfinding, new attack types, real-time editor reloads, or economies. Follow the usual minimal-validation policy. Update the relevant README and **append one concise** implementation entry to `devlog.md`, then commit and push to `main` in one change. Report modified files, commit SHA, Inspector properties and owner-side test suggestions.
