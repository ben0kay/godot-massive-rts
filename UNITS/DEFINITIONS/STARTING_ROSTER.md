# UNIT0 — Starting Soldier Roster

**Status:** Proposed tuning values for the future `UnitDefinition` resources. These are *not* currently active in-game. Units should be independently editable in Godot's Inspector. Shape silhouette is from the orthographic/isometric top view.

| Unit (stable ID) | Shape | Combat | HP | Damage/hit | Attack interval | Speed | Reach | Armour reduction | Mesh scale vs square |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Basic Infantry (`circle_infantry`) | Near-circle cylinder | Melee | 100 | 20 | 0.90 s | 5.0 | 1.6 | 0% | ~1.0× |
| Strong Infantry (`square_infantry`) | Square prism | Melee | 165 | 26 | 1.10 s | 4.0 | 1.8 | 15% | 1.0× |
| Archer (`triangle_archer`) | Equilateral triangle prism | Ranged | 70 | 16 | 1.25 s | 4.5 | 32 | 0% | ~1.0× |
| Cavalry (`diamond_cavalry`) | 45°-rotated square prism | Melee | 95 | 25 | 0.80 s | 9.0 | 1.9 | 0% | ~1.1× |
| Elite Infantry (`pentagon_elite`) | Regular pentagon prism | Melee | 300 | 34 | 1.65 s | 2.7 | 2.3 | 20% | **2.0× linear dimensions** |

- These values are **first-playtest proposals**, intended to give each unit an immediate role and to be revised by the owner.
- **Circle**: dependable baseline; loses a straight duel to the square. **Square**: tougher and slower but lower mobility. **Triangle**: dangerous behind a screen, fragile if reached. **Diamond**: fast hit-and-run/flanking potential, relatively fragile; a separate charge mechanic can come later. **Pentagon**: tanky and imposing, slow and vulnerable to being outmaneuvered.
- Armour reduction is **multiplicative** after attack damage and applies consistently to melee and ranged. Example: square takes 85% of nominal incoming damage. No special damage types or resistance matrix in UNIT0.
- **2× pentagon** means roughly twice the width, depth **and** height of the square, not merely twice the volume. This implies ~4× ground footprint and must be reflected in placement clearance and reach.
- Initial mesh shaping: circle as 12-sided cylinder; square as axis-aligned cube/prism; equilateral triangle as 3-sided prism; diamond as square prism rotated 45°; pentagon as regular 5-sided prism. Flat brighter top and darker pseudo-lit sides, no dynamic lighting.
- `Attack interval` is seconds between attacks (smaller is faster); `Speed` is world units per second. Ranged triangle uses the project's pooled projectile system with its existing global projectile speed initially.
- Faction colours remain blue and red; no separate stats for factions. Avoid persistent `byte Kind` interpretation when saving; new saves should use stable string IDs or explicitly versioned mappings.
