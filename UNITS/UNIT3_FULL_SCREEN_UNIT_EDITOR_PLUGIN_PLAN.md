# UNIT3 — Full-Screen Unit Editor Plugin

**Status:** Source implemented. Awaiting owner verification in Godot.
**Target:** `ben0kay/godot-massive-rts`, branch `main` (Godot 4.7 .NET / C#).  
**Purpose:** Let the owner pick a soldier and edit **all its authored parameters in a clear, full-size interface**, rather than using the narrow Godot Inspector. **Save changes directly to the existing `UnitDefinition` `.tres` resources.**

## 1. Reference: the existing Tower Defense Wave Editor

Study these files **in the separate reference repo** `ben0kay/godot-tower-defense`, branch `dev`:

- `addons/wave_editor/WaveEditorPlugin.cs` and `plugin.cfg` — `[Tool]` C# `EditorPlugin` with `_HasMainScreen() => true`, an editor main-screen tab beside 2D / 3D / Script, `EditorInterface.Singleton.GetEditorMainScreen().AddChild(panel)`, `_MakeVisible`, `_ExitTree`.
- `addons/wave_editor/WaveEditorPanel.cs` — full-height root `VBoxContainer`, toolbar, expandable `HSplitContainer`, left-hand `ItemList` selectors, scrollable details, aligned `SpinBox` / `OptionButton` / `CheckBox` fields, Save / Reload, dirty state, prompts, status footer.
- `addons/wave_editor/WaveEditorPanel.Presentation.cs` — clean dark panels, restrained accent headers, compact stat cards, reusable form/style helpers.
- `addons/wave_editor/PLAN.md` — documented **layout-collapse regressions**: do **not** nest anchored FullRect `Control` / `MarginContainer` incorrectly inside the main-screen container. Root panel is an expanding `VBoxContainer` **directly** in the editor main screen; only the middle split expands vertically.
- `addons/wave_editor/WaveEditorPanel.Schedule.cs` — event-driven visuals that update only on edits/selections, **not** per-frame.
- `addons/wave_editor/README.md` — save/refresh workflow and preview concept.

**Use these as a proven layout and behavior reference, not as a source to import into this game.** The new plugin belongs in this repo and edits soldier definitions, **not waves**.

Godot 4.7's official main-screen plugin documentation also describes this design: `https://docs.godotengine.org/en/4.7/tutorials/plugins/editor/making_main_screen_plugins.html`.

## 2. Desired experience

Enable the **Unit Editor** plugin in Godot's Project Settings → Plugins. It opens a full-size **Unit Editor** tab beside the normal 2D / 3D / Script tabs.

- The owner selects **Circle, Square, Triangle, Diamond, Pentagon or Hexagon** from a searchable list on the left.
- The middle contains well-organized **large parameter panels** rather than one long unstructured inspector.
- The right shows a **lightweight visual preview** of the shape, facing/cleave cone and radial shockwave, along with calculated effective measurements.
- Editing a field updates the preview and summary immediately **in the editor**, without starting a benchmark or spawning soldiers.
- **Save** writes only that unit's existing `.tres`; **Reload** discards pending edits after confirmation. Godot game/runtime uses updated definitions after the ordinary scene reload/restart, not live hot-reloading.

### Example full-size layout (illustrative, not exact pixels)

```text
┌────────────────────────────────────────────────────────────────────────────────────────────────────┐
│ UNIT EDITOR   •   Hexagon Siege                HexagonSiege.tres  * UNSAVED   [Save] [Reload]       │
├────────────────────┬─────────────────────────────────────────────────┬─────────────────────────────┤
│ UNITS              │ PARAMETERS                                      │ PREVIEW                     │
│ [Search units ...] │                                                 │                             │
│                    │ IDENTITY                 SIZE & APPEARANCE       │         FRONT ↑             │
│ ○ Circle Infantry  │ Name: Siege Infantry     Shape: Hexagon         │       / 100° cone \         │
│ □ Square Infantry  │ Role: Heavy melee...     Width: 1.6            │      |  hexagon   |         │
│ △ Triangle Archer  │                         Height: 0.825          │       \_________/           │
│ ◇ Diamond Cavalry  │ MOVEMENT                 Size: 2.0×             │    - - radius ring - -     │
│ ⬟ Pentagon Elite   │ Speed: 2.2               Derived footprint: ... │                             │
│ ⬡ Hexagon Siege ✓  │ Acceleration: ...        DEFENSE                │ SHOW [✓] Footprint         │
│                    │                         HP: 600                │      [✓] Cleave arc         │
│                    │ PRIMARY ATTACK          Armour: 20%           │      [✓] Shockwave          │
│                    │ Damage: 34              Charge resist: 2.4     │                             │
│                    │ Interval: 1.65s                                │ SUMMARY                     │
│                    │ MELEE CLEAVE            SHOCKWAVE              │ Normal swing: 100°         │
│                    │ Enabled: ✓              Every 5 attacks        │ Primary: 34 damage         │
│                    │ Angle: 100°             Centre damage: 25      │ Up to 2 secondary: 22.1 ea │
│                    │ Extra targets: 2        Outer damage: 15       │ Radial: 8-unit radius      │
│                    │ Extra damage: 65%       Effective radius: 8    │                             │
│                    │ TRAMPLE / RANGED / ADVANCED (as applicable)     │                             │
├────────────────────┴─────────────────────────────────────────────────┴─────────────────────────────┤
│ Select a unit • Values are saved to .tres • Unsaved changes must be saved or discarded              │
└────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

Use resizable main-screen `HSplitContainer` sections, a list pane around 220–260 px minimum, a flexible central scrollable form and a preview pane around 300–360 px. Put controls in a **two-column stat layout where space permits**; the parameter area must remain easy to scan at 1920×1080 and be usable when resized. No horizontal clipping, collapsed full-height layout or nested-scroll chaos. Favor Godot editor theme colors and matching Wave Editor-style subtle panels/accent labels; do not hard-code a garish custom game HUD.

## 3. Authoritative data and unit selection

- **Do not create an alternative stats database, JSON file or separate plugin-only soldier configuration.** Read and write the existing `UNITS/DEFINITIONS/*.tres` `UnitDefinition` resources.
- Populate the left unit list from **`UNITS/CATALOG/DefaultUnitCatalog.tres`** and its registered `Definitions`, not hard-coded six names, so newly registered unit definitions appear later.
- Display user-friendly name, shape/silhouette, and optional role; retain stable `Id` as read-only (changing stable IDs may break deployment saves). Clearly distinguish the current edited resource path.
- Support selecting another registered unit, filtering/searching by display name or ID, and refreshing the catalogue on demand. Missing, duplicated or invalid resources should show a useful warning rather than crash.
- Read values including inherited C# exported defaults, even when a property is absent as a literal line in the `.tres`. In particular, Hexagon/Pentagon inherit **100°** cleave and **65%** secondary damage until explicitly overridden.
- Preserve resources' script references, any future exported properties and existing author-authored values. The plugin should only mutate the selected `UnitDefinition` property values, without rewriting unrelated unit resources/catalogue state.
- Since the reference Wave Editor's resource classes are `[Tool]`, verify Godot editor-mode C# resource support here and add `[Tool]` to the appropriate `UnitDefinition` class only if needed, preserving normal runtime behavior.

## 4. Editable field groups and units

**V1 must cover all existing meaningful exported `UnitDefinition` properties** with correctly typed controls, visible units, and tooltips:

| Group | Primary authoring fields |
| --- | --- |
| Identity | Display name, role, stable ID **read-only**, shape |
| Size / appearance | Width, Height, SizeMultiplier; show actual calculated geometry/footprint read-only |
| Movement | Speed, AccelerationSecondsOverride (0 = shared/default), any present unit movement overrides |
| Defense | Health, Armour (**display 0.2 as 20%**), ChargeResistance (**raw multiplier/value, not a percent**) |
| Primary attack | Damage, Interval in seconds, Style (Melee/Ranged), Reach with explanation |
| Melee cleave | CleaveEnabled, CleaveAngleDegrees, CleaveSecondaryFraction (0.65 = **65%**), plus any later max-extra-target field |
| Shockwave | ShockwaveEveryAttacks, base radius, **effective radius = base × size**, centre damage, **outer-edge damage**, RespectsCover |
| Trample | Enabled, RunUp, Distance, Efficiency, DamageMultiplier, MaxHits |
| Ranged | Projectile resource/selection and its meaning; link/open referenced projectile resource rather than silently copying its data into this `.tres` |
| Additional / future | Every future exported property should have a visible fallback field or an explicit unsupported-property warning, not disappear silently |

- Use `SpinBox` for numeric edits, `CheckBox` for flags, `OptionButton` for enums, short text inputs for strings, and resource pick/open behavior for resource references. Use appropriate steps, min/max, integer-only fields, contextual tooltips, and clearly labeled percentage conversions.
- Avoid confusing global-vs-unit controls. **Ordinary melee currently requires physical contact**, with the separation gap and a small tolerance; do **not** describe `Reach` as the actual ordinary melee contact distance. It also participates in acquisition/other mechanics.
- **The current code hard-codes two additional cleave victims and a 100° non-cleave facing angle.** Do not display these as editable functioning settings until implemented. Before `UNIT2`, show them as read-only with a short explanation, if displayed at all.
- `ShockwaveEdgeDamageFraction` is the *current serialized backend*; owner-facing UI should show **Outer damage = 15** when centre = 25 and fraction = 0.6. For V1, calculate fraction when editing outer damage and update readouts when centre changes; guard against invalid/zero centres, non-finite values and inconsistent ranges. **Do not introduce a second conflicting serialized field just for the editor.** If `UNIT2` later adds an authoritative `ShockwaveOuterDamage` field, adapt the UI to that property and its migration instead.
- For disabled cleave, shockwave or trample abilities, visually dim/disable irrelevant controls while keeping underlying saved values intact.
- Build future-proof control grouping using a small typed field descriptor layer or well-named form builders. **Keep fields easily maintainable**; prefer a graceful "Additional parameters" fallback over a complex generic editor framework. No speculative resistances or attack mechanics are authorized by this plugin plan.

## 5. Lightweight visual preview and summary

**V1 goal: a useful, accurate, editor-only preview, not a mini battle simulator.**

- Preview the selected primitive silhouette and facing. Use the same `UnitOutline` / compiled geometry and size rules as `UNITS/CATALOG/UnitCatalog.cs` and `RENDERING/FacetedSoldierMeshes.cs`, or a minimal read-only projection of them. **Do not create conflicting approximate geometry formulas.**
- Overlay a clear **forward melee cleave arc** in degrees and a separate **radial shockwave ring** with optional toggles, scaled to the current resource. Label the wave as a **radius**, not a diameter.
- Show footprint/collision circle distinctly from the polygon mesh. Explain that it is the **bounding circular footprint derived from the silhouette**, not the exact mesh outline; ordinary melee is body contact.
- Summarize primary damage, secondary cleave percentage and target cap, effective shockwave radius, centre/outer damage, attack interval, effective speed/size, and a simple blue/red color toggle if convenient.
- If `UNIT2` settings are not implemented, reflect **actual current code behavior** and label non-editable facts read-only. Do not invent damage calculations or guaranteed kills.
- Refresh on selection/field changes only; no `_Process` or `_PhysicsProcess` polling, no live combat units, no extra scene, no thousands of soldiers and no runtime dependency on the plugin. A 2D `Control._Draw` diagram is fine for V1; a small isolated `SubViewport` 3D preview is optional future polish only if simple and reliable.

## 6. Save, reload and safety

Borrow the Wave Editor's explicit document controls and **avoid accidental source edits**:

1. Select a unit and load its `.tres` into a **draft/editor state**; do not silently save on each keystroke.
2. Edit fields → show clear **UNSAVED** indicator; refresh only affected summaries/preview.
3. **Save** validates values, writes the selected `.tres` through Godot's resource APIs and checks the result. Refresh the editor filesystem where needed.
4. **Reload / Revert** discards unsaved draft changes **only after confirmation**.
5. Switching units, changing source/catalog, or closing/deactivating while dirty must not silently lose unsaved changes; offer Save / Discard / Cancel where practical. A simple explicit Save-before-switch workflow, like the reference Wave Editor, is acceptable for V1 if closing/disabling is safely handled.
6. Detect and warn about external edits to the source `.tres` when feasible (e.g. source changed since loading), rather than overwriting VS Code/Codex changes silently.
7. Preserve stable resource path and ID; **no Add/Delete/Duplicate Unit** operations in V1. Do not mutate `DefaultUnitCatalog.tres` or any deployment saves.
8. Confirm what Godot `ResourceLoader` cache mode and `ResourceSaver.Save` mean for editor-shared external resources. Keep draft edits isolated until an explicit Save, and ensure Reload reflects actual disk values, not merely the modified cached instance.
9. Never save invalid inputs or alter values simply by selecting a unit. Validate numeric finiteness, ranges, zero/disabled conventions and schema-required fields. Use actual compiler/runtime validation rules where possible and show useful errors inline.
10. Disable/re-enable plugin without leaked children, timers, signals, pending file writes or crashes. No game/benchmark code should execute in editor mode.

## 7. Implementation ownership and architecture

Suggested new files (create only what is needed):

```text
addons/unit_editor/
    plugin.cfg
    UnitEditorPlugin.cs              # [Tool] EditorPlugin / main-screen registration
    UnitEditorPanel.cs               # expanding VBox, left list, central form, toolbar/save
    UnitEditorFields.cs              # typed field rows, percentages, validation (optional partial)
    UnitEditorPreview.cs             # small event-driven silhouette/cone/ring preview
    README.md                        # enable, usage, persistence, limitations
```

Godot convention uses lowercase `addons/` for editor plugins; the established project **UPPERCASE system-folder rule still applies to gameplay folders**. Unit definitions remain under `UNITS/DEFINITIONS/`; do not move them into the plugin.

- `[Tool]`, `#if TOOLS`, standard Godot `EditorPlugin` main-screen implementation; use the actual Godot 4.7 C# APIs. No game autoload, runtime singleton, soldier instance updates or `project.godot` plugins duplication.
- The panel **directly expands** within the Editor main screen, following the reference Wave Editor's fixed layout approach.
- Reuse existing `UnitDefinition`, `RuntimeUnitCatalog`, `UnitOutline`, `UnitStats` and existing defaults/normalization where practical. One canonical unit source; editor widgets are a view onto it.
- Do not pull in the Tower Defense project as a dependency or copy unrelated Wave Editor gameplay/data logic. Adapt only the tested presentation/save patterns.
- Prefer tiny, focused methods and reusable row builders, not a speculative standalone UI framework.
- No changes to soldier simulation, normal attacks, balance, per-frame code or unit count limits are required for the plugin itself.

## 8. Interaction with existing UNIT2 plan

`UNITS/UNIT2_UNIT_DEFINITION_TUNING_PLAN.md` separately proposes **new runtime-backed editable parameters** (max secondary cleave targets, non-cleave facing angle, intuitive serialized shockwave outer damage, etc.).

**UNIT3 (this plugin) and UNIT2 are related but not identical:**

- **UNIT3** provides the **full-screen interface** and edits the values that actually exist now.
- **UNIT2**, if separately authorized, makes additional previously hard-coded behavior truly customizable.
- UNIT3 should **not pretend unsupported values are editable**. Prefer implementing UNIT3 against current fields now and extending it after UNIT2, or do both **only if separately authorized**.
- Future resistance types/advanced secondary skills can be added to UnitDefinition and its form later; do not design a damage-type framework as part of this editor.

## 9. Suggested Codex execution and acceptance

**Milestone EDITOR1 (plugin only)**

1. Read `README.md`, `NOTES/CodePreferences.md`, `UNITS/README.md`, `UNITS/UNIT2_UNIT_DEFINITION_TUNING_PLAN.md`, the current `UnitDefinition.cs` / `UnitCatalog.cs`, relevant rendering code and the Tower Defense Wave Editor reference listed above. Confirm `AGENTS.md` if present.
2. Implement the **full-size Unit Editor plugin** in `addons/unit_editor/`. Enable it according to existing editor conventions. Do **not** implement UNIT2's combat changes in the same task without express approval.
3. Provide current-property forms, accurate derived readouts, a modest forward-cone/radius preview and explicit save/reload/dirty handling.
4. Verify (lightweight source review and one build if practical) that opening/closing editor and switching among **all six** unit types does not create unexpected resource writes or scene pollution.
5. Owner checks in Godot: the Unit Editor tab appears beside 2D / 3D / Script; fills the main area; unit search works; every field aligns; no scrolling/layout collapse; Hexagon preview shows a **100° normal cleave**, **5th-attack shockwave**, **25 centre / 15 outer**, and **8-unit shockwave radius**; editing these previews before Save; Save persists the exact correct `.tres`; Reload discards; changes take effect in a restarted benchmark; no balance changes caused merely by opening the tool.
6. Update plugin README and relevant `UNITS/README.md`, add **one short implementation entry to `devlog.md`**, commit and push to `main` once. Do not run extensive tests or unrelated refactors.

**Out of scope for EDITOR1:** full custom unit creation, drag-and-drop unit reordering, multiple-unit batch edits, live gameplay hot-reload, unit AI editor, combat simulator, new ability logic, damage-type resistance implementation, performance benchmarking, terrain/obstacle authoring, export/import spreadsheets and elaborate preview FX.

**Success:** The owner can open Godot, click **Unit Editor**, pick any current unit and comfortably rebalance its existing parameters in one spacious, coherent workspace. No need to use the narrow Inspector for routine soldier tuning.
