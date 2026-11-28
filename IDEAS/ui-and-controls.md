# UI and controls

HUD, menus, deployment controls, editor tools, and accessibility.


Editor-specific UI and tools are tracked in [level editor](level-editor.md).

## Battle interface refinements

- **Status:** HUD1 is source implemented, including faction-tinted panels, battle clock, Citadel health, combat statistics and an interactive minimap. Do not reimplement the base HUD.
- **Expandable live roster breakdown:** Show remaining units by type and faction, with compact icons and optional casualties; derive from cached counts or a bounded refresh, not a full army scan each rendered frame.
- **Optional HUD display preferences:** Consider compact/expanded battle overview, visibility toggles and minimap detail settings after reviewing what HUD1 already provides. Preserve existing input shortcuts and deployment transitions.
- **Scenario event notices:** Compact, nonblocking alerts for reinforcements, objectives and environmental events; suppress duplicates and respect simulation pause/speed.
