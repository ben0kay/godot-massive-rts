# Maps and modes

Skirmish scenarios, scenario authoring, Fortress Defense waves, campaign and progression. Current design hierarchy is authoritative in [endgoal.md](../endgoal.md).

Procedural terrain generation and authoring tools now live in [level editor](level-editor.md#procedural-map-generator).

## Scenario events and objectives

- **Conditional reinforcements:** Reuse existing timed reserves, adding optional triggers such as Citadel health below 50%, structure destruction, objective capture or a defined elapsed time. Support bounded one-shot events, clear staging locations, preview and reset/retry.
- **Dormant armies and ambush zones:** Groups remain inactive or hold their positions until enemies enter a defined zone; activate once, with existing orders and spatial queries.
- **Guard zones and patrols:** Authored group guard areas and waypoint routes; distinguish from existing hold-ground modes, avoid per-unit path searches.
- **Starting conditions:** Allow authored starting structure damage and shield charge; validate against current max values.
- **Restricted deployment:** Optional zones limited to specified units or tiers; maintain valid roster allocation and placement rules.
- **Time limits and survival:** Optional scenario time limit, defend until reinforcements arrive, or win with a specified surviving share. Keep default Clash/Offense/Defense conditions unchanged unless explicitly configured.
- **Secondary objectives:** Capture a neutral outpost, destroy a power/communications station, eliminate artillery or secure a bridge. Define objective priority and victory evaluation before coding.
- **Moving capture zones:** Later optional objective requiring armies to advance toward changing positions. Avoid automatic hard-coded mode changes.
- **Scripted hazards:** Author bombardment locations and event timing via the editor; see [gameplay mechanics](gameplay-mechanics.md#environmental-bombardment).

## Example skirmishes

| Scenario | Mode | Concept |
| --- | --- | --- |
| First Encounter | Clash | Open plains with basic infantry. |
| Frozen Crossing | Clash | Fight across icy ground with vulnerable heavy routes. |
| Death Valley | Clash | Narrow canyon funnels opposing armies. |
| Broken City | Clash | Industrial ruins and fragmented lines of sight. |
| Last Bridge | Defense | Hold a river crossing against waves or timed reserves. |
| Iron Fortress | Offense | Assault elevated defenses and layered barricades. |
| Behind Enemy Lines | Offense | Destroy support buildings before the Citadel. |
| Last Stand | Defense | Survive until friendly reinforcements arrive. |
| Volcanic Divide | Clash | Hazardous lava and geothermal vents divide the front. |
| Great Siege | Offense | Large combined-arms assault including aircraft. |

Start with roughly 30 increasingly difficult Clash skirmishes using the existing standalone fixed-roster model. Introduce mechanics gradually, reserve larger and more complex combinations for later maps, and use authored Offense/Defense objectives as appropriate. These examples are not approved level assets.

## Progression and later modes

Preserve the existing intended separation: standalone Skirmishes use fixed authored rosters; Missions add mission-local purchases/upgrades; Conquest adds persistent territory, armies and economy; Fortress Defense is a longer wave mode. See endgoal.md for details. Do not introduce new modes or alter progression as part of individual terrain tasks.
