# Gameplay mechanics

Combat rules, movement, terrain interactions, and new ways to play.

## Ice terrain

- **Proposal** — Frozen lakes and icy ground can be traversable, but reduce ground-unit traction: slower acceleration/braking and less effective turning, especially for heavy vehicles. Solid ice is a distinct surface from ordinary impassable water.
- **Goal** — Give frozen battlefields meaningful maneuvering challenges rather than a purely cosmetic snow biome.
- **Notes** — Author a surface mask through existing terrain/paint tooling and sample the compiled surface type during movement. Apply configurable movement modifiers without changing base stats or duplicating navigation. Keep air units unaffected. Thin ice breaking, sliding damage and different weight limits are separate future possibilities, not required. Use restrained blue-gray/white surface shading; no per-unit physics bodies or continuous ice simulation.

## Volcanic terrain and lava damage

- **Proposal** — Volcanic wastelands feature dark basalt, lava flows and hazardous hot ground. Units on passable lava or designated hot-ground tiles take periodic damage while exposed; leaving the hazard stops damage. Deep lava rivers may instead be impassable, with bridges or safe crossings.
- **Goal** — Create hostile landscapes and force meaningful route choices without making every volcanic map inaccessible.
- **Notes** — Distinguish impassable lava from traversable damage zones in authored surface data; do not silently turn ordinary water into a damage source. Use a configurable hazard damage rate and bounded fixed-step damage cadence, shared damage/death feedback and compact terrain lookup. Aircraft at normal flight altitude are unaffected. Decide shield interaction, structure placement, damage rates and path cost/avoidance during implementation. Visual lava glow/flow can use shared materials and restrained emissive effects; no per-tile nodes, real lights or per-frame hazard scans.

## Marshland

- **Proposal** — Shallow marshes, bogs and muddy ground remain passable but reduce ground movement speed, with stronger penalties for heavier tiers. Light infantry retains a relative mobility advantage. Deep water remains blocked under the existing water rules.
- **Goal** — Make terrain influence army composition and approach routes, especially for large siege vehicles.
- **Notes** — Use a paintable/generated marsh surface mask, configurable speed multipliers by mass/tier, and the existing movement pipeline (combine carefully with slope/forest modifiers). If routing supports weighted terrain, penalize marsh traversal without treating every marsh as a wall. No airborne movement penalty, per-soldier physics queries or new pathfinding engine. Visuals: muddy, subdued wet ground with scattered pools and optional reeds; cosmetic plants should be batched. Exact tuning and stacking rules remain open.

See [procedural map generation](procedural-map-generator.md) for eventual biome integration. These terrain mechanics are independent proposals, not prerequisites for the first generator version.
