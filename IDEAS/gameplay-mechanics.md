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

**Status:** Source implemented; current editor documentation specifies 15% slowdown for Tier 1, 25% Tier 2 and 40% Tier 3+, with stronger-of-forest/marsh overlap and no aircraft penalty. Original proposal below is retained for context; do not reimplement.

- **Proposal** — Shallow marshes, bogs and muddy ground remain passable but reduce ground movement speed, with stronger penalties for heavier tiers. Light infantry retains a relative mobility advantage. Deep water remains blocked under the existing water rules.
- **Goal** — Make terrain influence army composition and approach routes, especially for large siege vehicles.
- **Notes** — Use a paintable/generated marsh surface mask, configurable speed multipliers by mass/tier, and the existing movement pipeline (combine carefully with slope/forest modifiers). If routing supports weighted terrain, penalize marsh traversal without treating every marsh as a wall. No airborne movement penalty, per-soldier physics queries or new pathfinding engine. Visuals: muddy, subdued wet ground with scattered pools and optional reeds; cosmetic plants should be batched. Exact tuning and stacking rules remain open.

See [procedural map generation](level-editor.md#procedural-map-generator) for biome integration. Unimplemented terrain mechanics are independent proposals, not prerequisites for the generator.


## Additional terrain and combat mechanics

### Fragile ice
- **Proposal** — Light infantry cross intact thin ice; heavy units gradually fracture it. Decide whether failure slows, damages or blocks movement and how stranded units escape. Treat as a later extension of ice traction; avoid global route invalidation and dynamic physics.
- **Goal** — Make troop weight matter at crossings.

### Shallow water and fords
- **Proposal** — Paint or place traversable shallow water with a movement penalty, distinct from existing impassable water. Aircraft unaffected. Preserve bridge width rules and use existing terrain masks. Visual bank options belong in [visual updates](visual-updates.md).
- **Goal** — Add more flexible river crossings without requiring a bridge everywhere.

### Deep sand and corrosive ground
- **Proposal** — Deep sand slows heavy ground units more than light infantry. Chemical spill terrain damages exposed ground units using the same bounded hazard mechanism proposed for lava. Keep these separate surface types; no new per-unit physics.
- **Goal** — Differentiate desert and industrial battlefields.

### High-ground advantage
- **Proposal** — Elevated ranged units gain a modest range advantage against lower targets. Use actual height differences and existing acquisition/projectile rules, cap bonus and preserve LOS; decide thresholds and whether uphill attacks are penalized. Do not apply indiscriminately to aircraft.
- **Goal** — Make hills, ramps and plateau deployment meaningful rather than only visual.

### Destructible crossings and dams
- **Proposal** — Destroyable bridges can cut a route; a dam may release water into a predefined area. Start with bounded local navigation invalidation and safe handling of units already on affected ground; no fluid simulation. Defer until reliable rerouting/stranding behavior is proven.
- **Goal** — Allow changing battlefield geography and secondary objectives.

### Disruption and supply effects
- **Proposal** — Author or emit timed fields that suppress shield regeneration or selected special abilities; supply depots could reduce friendly attack cooldowns. Define stacking and eligibility; use compact status state and existing bounded spatial queries. See [Disruption Tower](structures-and-defenses.md#disruption-tower).
- **Goal** — Encourage combined arms and tactical support positioning.

### Environmental bombardment
- **Proposal** — Mark an area that receives periodic telegraphed explosions or other hazards during a battle. Use existing projectile/splash/effect budgets and deterministic scenario timings.
- **Goal** — Make dangerous approaches and timed events possible without scripted per-unit work.

### Future geothermal and trench refinements
- **Status** — GEO1 and TRENCH1 are source implemented. Keep neutral tier-scaled launch, bounded eruption updates, terrain-based trench access and concealment as existing foundations. Further balance or access changes require owner approval; inspect live behavior before editing.
