# Shields

**Shield Generator** is a buildable support tower with a 21-unit radius and a default price of 1,000 Carbon. **Mobile Generator** is a slow Tier 2 melee support unit with a 14-unit radius. Paint either generator in the appropriate Benchmark or Level Editor deployment tool. In Fortress Defense, build the tower from the build menu or train the mobile unit at a Fabricator. The mobile unit accepts movement orders in Fortress Defense and uses standard melee targeting in auto battles, advancing toward enemies and shielding allies as it moves. Its base speed is 1.8 and it deals 8 base damage every 1.4 seconds.

Both emitters grant nearby living allies a temporary 20 HP shield, recharging at 4 HP/s and decaying at 2 HP/s outside coverage. The mobile unit cannot charge itself. Range uses horizontal distance and works across elevation. Overlaps choose one emitter by capacity, then recharge rate, then registration order. Excess charge decays if a weaker source takes over.

`ShieldSystem` uses the combat grid for coverage and updates a packed list of recipients every 0.2 simulation seconds by default. A complete refresh is published together. The cost of a single refresh grows with the number of overlapping sources and candidates. Scenes without emitters have no shield pools or shell batches. Damage passes through the existing armour calculation first. Shells use a lighter tint from each faction’s palette, with brighter edges and a faction-tinted flash on shield-only hits, while damage carried through to health also triggers normal body feedback.

Tune emitter strength, radius, rates and tower price in the Entity Editor or the definitions. Benchmark's **Shields** section controls the mechanic, shell visibility, generator radii and refresh interval. The CPU performance panel shows emitter and recipient counts, timings and visible shell counts. In Fortress Defense, select a generator to see its field or select soldiers to see their combined shield points.

`GAMEPLAY/UNITS/UnitVisualConfig.tres` controls the independent shell visibility and the global visible shell cap (512 by default). Shared MultiMesh shell batches reuse the army poses and one inexpensive translucent material. The cap only affects presentation.

Owner validation should cover shield-only and overflow hits, moving or destroyed emitters, overlapping radii, and the coverage/shell timing difference with a dense army at 10,000–20,000 soldiers.
