# UNIT0 — Starting Soldier Roster

**Status:** Current tuning values authored in the nine `UnitDefinition` resources. Units should be independently editable in Godot's Inspector. Shape silhouette is from the orthographic/isometric top view. The table lists base dimensions and reach before Size Multiplier is applied.

| Unit (stable ID) | Shape | Combat | HP | Damage/hit | Attack interval | Speed | Reach | Armour reduction | Height | Mesh scale vs square |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Basic Infantry (`circle_infantry`) | Near-circle cylinder | Melee | 50 | **5** | 0.90 s | 5.0 | 1.6 | 0% | 0.4125 | ~1.0× |
| Strong Infantry (`square_infantry`) | Square prism | Melee | **85** | 12 | 1.10 s | 4.0 | 1.8 | 15% | 0.4125 | 1.0× |
| Archer (`triangle_archer`) | Equilateral triangle prism | Ranged | **25** | 9 | 1.25 s | 4.5 | 32 | 0% | 0.4125 | ~1.0× |
| Long Triangle Marksman (`long_triangle_marksman`) | Narrow isosceles triangle, 2× nose-to-tail | Ranged | **65** | 13 | 1.70 s | 3.2 | 48 | 0% | 0.4125 | 2× normal triangle length |
| Cavalry (`diamond_cavalry`) | Long narrow diamond / rhombus | Melee | **105** | **14** | 0.80 s | 9.0 | 1.9 | 0% | 0.300 | ~2.0× long, ~1.1× wide |
| Elite Infantry (`pentagon_elite`) | Regular pentagon prism | Melee | 285 | 22 | 1.65 s | 2.7 | 2.3 | 20% | 0.825 | **1.8× square (0.9 size multiplier)** |
| Siege Infantry (`hexagon_siege`) | Hexagon prism | Melee | 600 | 36 | 1.65 s | 2.2 | 2.3 | 20% | 0.825 | ~2.2× current Pentagon scale |
| Heavy Artillery (`chevron_artillery`) | Chevron with barrel | Ranged explosive | 350 | 80 | 4.50 s | 1.5 | 70 | 15% | 0.99 | ~2.4× square width |
| H Breacher (`h_breacher`) | Twin-ram H frame | Melee vs units/structures | 110 | 20 (10 vs units) | 1.50 s | 2.8 | 0.82 | 0% | 0.44 | 0.91-unit width |

- These are **current authored tuning values**; the definition resources are authoritative. Base dimensions shown above are before Size Multiplier. The 2026-11-14 balance pass adjusts six units' HP/damage and the Pentagon's multiplier, without changing authored attack intervals or other unit settings.
- **Circle**: baseline with 50 HP and 5-damage melee hits. **Square**: armoured 85-HP melee with 12-damage hits. **Triangle**: fragile 25-HP ranged unit with 9-damage travelling laser bolts. **Diamond**: fast 105-HP flanker with 14-damage melee and its existing trample charge. **Pentagon**: 285-HP armoured elite with 22-damage cleave and 0.9× size. **Hexagon**: heavy 600-HP siege unit with 36-damage melee and its unchanged shockwave.
- Armour reduction is **multiplicative** after attack damage and applies consistently to melee and ranged. Example: square takes 85% of nominal incoming damage. No special damage types or resistance matrix in UNIT0.
- **Pentagon size multiplier is 0.9×**, applied to its 1.6-wide/0.825-high base dimensions (effective 1.44 wide and 0.7425 high); its melee reach and collision footprint scale with it. The Hexagon remains at 2.0× its own 1.6-wide/0.825-high base, not 2× the now-reduced Pentagon.
- VIS2 shared mesh shaping: circle uses 16 sides; square, equilateral triangle and pentagon retain their recognisable polygon outlines; cavalry is an elongated X-axis rhombus (~0.64 wide × ~1.21 long) rather than a rotated square. Every soldier has a small top-edge bevel with an intermediate pseudo-lit face tint; all use the existing unshaded shared material and MultiMesh batches.
- Cavalry uses 1.35× size. Cavalry compiles to 0.864 width, about 1.633 length, 0.405 height, about 0.816 footprint radius and 2.565 authored reach after scaling. Ordinary melee uses body contact.
- Cavalry balance: **105 HP, 14 standard melee damage, 3.0-second acceleration to unchanged authored top speed 9.0, 1.0× trample damage multiplier**. Its existing 1.35× size, 0.80-second attack interval, charge momentum, range, victim limit and trample mechanics are unchanged.
- `Attack interval` is seconds between attacks (smaller is faster); `Speed` is world units per second. Ranged Triangle references the [Laser Bolt](../../PROJECTILES/Types/LaserBolt/LaserBolt.tres) definition. Its short rectangular visual replaces the old arrow without changing the authored travel speed, shallow arc, damage or collision.
- Faction colours remain blue and red; no separate stats for factions. Avoid persistent `byte Kind` interpretation when saving; new saves should use stable string IDs or explicitly versioned mappings.

- **UNIT4:** Long Triangle Marksman is a separate ranged archetype with a stable ID and new `LongTriangle` shape. Its short back edge remains 0.8 units wide while its front tip extends twice as far as the regular Triangle, creating an isosceles wedge (two longer equal sides). Its 48-unit range is 50% longer, but movement speed and fire cadence are slower. A dedicated 0.64-unit laser bolt is 25% longer than the regular Triangle's resized 0.51-unit bolt; both use the same subtle blue/red/green tint shader approach. Existing unit stats and saved indices 0–7 are unchanged.
