# Game ideas

A categorized backlog of ideas worth revisiting after a design discussion. Entries are proposals, not permission to implement. Mark source-implemented features separately from future work and verify live code before planning. Owner acceptance is separate from implementation.

| Category | What belongs there |
| --- | --- |
| [Level editor](level-editor.md) | Map painting, tools, prefabs, procedural generation, authoring and validation |
| [Units](units.md) | Ground units, aircraft, roles and balance |
| [Structures and defenses](structures-and-defenses.md) | Towers, walls, utility buildings, support and fortification concepts |
| [Gameplay mechanics](gameplay-mechanics.md) | Combat, movement, terrain interactions, hazards and rules |
| [Visual updates](visual-updates.md) | Scenery, models, materials, animation, weather, lighting and effects |
| [Maps and modes](maps-and-modes.md) | Skirmish designs, objectives, events, campaign and progression |
| [UI and controls](ui-and-controls.md) | Player-facing HUD, menus, deployment controls and accessibility |
| [Audio](audio.md) | Sound effects, ambience, alerts and music |

The old standalone procedural generator proposal is preserved in Level Editor; the old Units and Defenses file was split by category. Keep related detail in one canonical entry and link across categories rather than duplicating long plans.

## Adding an idea

- **Proposal** — What would change and how the player would experience it.
- **Goal** — What it improves.
- **Notes** — Constraints, tuning, performance or unresolved decisions.
- **Status** — If partially or fully implemented, record the known state with a source/documentation reference where practical.

Preserve owner decisions; label speculative defaults. Before implementing an approved task, read root README, CodePreferences, endgoal and relevant feature documentation. Reuse shared data-oriented systems and avoid unbounded per-unit work. Do not treat this backlog as an implementation roadmap automatically.

Prompt for another chat:

> Add this idea to the appropriate category in IDEAS/. Keep agreed details and open questions. Update documentation only.
