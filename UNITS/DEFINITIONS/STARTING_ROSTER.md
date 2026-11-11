# UNIT0 — Starting Soldier Roster

**Status:** Current tuning values authored in the six `UnitDefinition` resources. Units should be independently editable in Godot's Inspector. Shape silhouette is from the orthographic/isometric top view. The table lists base dimensions and reach before Size Multiplier is applied.

| Unit (stable ID) | Shape | Combat | HP | Damage/hit | Attack interval | Speed | Reach | Armour reduction | Height | Mesh scale vs square |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Basic Infantry (`circle_infantry`) | Near-circle cylinder | Melee | 100 | **3** | 0.90 s | 5.0 | 1.6 | 0% | 0.4125 | ~1.0× |
| Strong Infantry (`square_infantry`) | Square prism | Melee | **45** | 26 | 1.10 s | 4.0 | 1.8 | 15% | 0.4125 | 1.0× |
| Archer (`triangle_archer`) | Equilateral triangle prism | Ranged | **15** | 16 | 1.25 s | 4.5 | 32 | 0% | 0.4125 | ~1.0× |
| Cavalry (`diamond_cavalry`) | Long narrow diamond / rhombus | Melee | **65** | **14** | 0.80 s | 9.0 | 1.9 | 0% | 0.300 | ~1.5× long, ~0.8× wide |
| Elite Infantry (`pentagon_elite`) | Regular pentagon prism | Melee | 300 | 34 | 1.65 s | 2.7 | 2.3 | 20% | 0.825 | **2.0× linear dimensions** |
| Siege Infantry (`hexagon_siege`) | Hexagon prism | Melee | 600 | 34 | 1.65 s | 2.2 | 2.3 | 20% | 0.825 | 2× the Pentagon scale |

- These values are **current owner-requested starting values** and may be revised through the definition resources. All five heights were first reduced by 25%, then halved again on 2026-11-11. Widths, footprints, speeds, health, damage and attack ranges remain unchanged by this second adjustment.
- **Circle**: durable baseline with deliberately low 3-damage melee hits. **Square**: hard-hitting and armoured but only 45 HP, making it more fragile overall than the circle. **Triangle**: high-risk ranged damage dealer with just 15 HP, very fragile if reached. **Diamond**: fast hit-and-run/flanking shock unit; lower sustained melee damage and HP leave it vulnerable in prolonged fights, while its existing trample charge remains active. **Pentagon**: tanky and imposing, slow and vulnerable to being outmaneuvered.
- Armour reduction is **multiplicative** after attack damage and applies consistently to melee and ranged. Example: square takes 85% of nominal incoming damage. No special damage types or resistance matrix in UNIT0.
- **2× pentagon** means roughly twice the width, depth **and** height of the square, not merely twice the volume. Square is 0.4125 high and pentagon is 0.825 high. This implies ~4× ground footprint and must be reflected in placement clearance and reach.
- VIS2 shared mesh shaping: circle uses 16 sides; square, equilateral triangle and pentagon retain their recognisable polygon outlines; cavalry is an elongated X-axis rhombus (~0.64 wide × ~1.21 long) rather than a rotated square. Every soldier has a small top-edge bevel with an intermediate pseudo-lit face tint; all use the existing unshaded shared material and MultiMesh batches.
- UNIT1 sets Cavalry to 1.25× and the other four types to 1.0×. Cavalry compiles to 0.80 width, about 1.51 length, 0.375 height, 0.7875 footprint radius and 2.375 melee reach before adding the defender’s radius.
- Cavalry balance: **65 HP, 14 standard melee damage, 3.0-second acceleration to the unchanged top speed 9.0, 1.0× trample damage multiplier**. Its 1.25× size, 0.80-second attack interval, charge momentum, range, victim limit and trample mechanics are unchanged.
- `Attack interval` is seconds between attacks (smaller is faster); `Speed` is world units per second. Ranged triangle references the [Basic Arrow](../../PROJECTILES/Types/BasicArrow/BasicArrow.tres) definition for projectile speed, arc and visuals.
- Faction colours remain blue and red; no separate stats for factions. Avoid persistent `byte Kind` interpretation when saving; new saves should use stable string IDs or explicitly versioned mappings.
