# PROJECTILES

`Shared/` owns the projectile resource, catalogue, pooled flight and collision logic, and batched rendering. `Types/BasicArrow/` owns the basic arrow definition and its short shaft, arrowhead, and fletching mesh. A ranged unit selects its projectile in its unit definition. Several units can share the same asset. Attack damage still belongs to the firing unit.

Open `Types/BasicArrow/BasicArrow.tres` in Godot to edit base speed and arc. At the default 0.8× movement multiplier, Basic Arrow flies at 36 world units per second from an authored speed of 45. The lifetime cap scales inversely to preserve the distance an arrow can travel before expiry. **Arc Height** caps the rise above the straight line between launch and aim. **Arc Height Per Distance** scales the rise down on short shots. The defaults are 0.65 world units maximum and 0.08 units of rise per horizontal world unit. Set Arc Height to zero for straight flight. Restart the arena after editing the resource.

To add another projectile, create a ProjectileDefinition resource in its own directory under `Types/` and assign it to a ranged unit. Set Visual Mesh to use a custom model with its tip at the origin pointing along local −Z. Definitions with no mesh use the shared arrow mesh. The renderer packs live instances by projectile definition. A shot follows its authored arc toward the aim position recorded at launch and tests for an enemy hit along each simulation segment.


## ART1 — Artillery Shell

The reusable `Types/ArtilleryShell/ArtilleryShell.tres` enables **Indirect Fire** and **Explodes on Impact**. The aim point is the target's ground position at launch. A shell can clear a wall if its swept segment is above the wall. It detonates on contact with an enemy, a blocking wall, or the terrain surface. Terrain tests follow the same heightfield triangles as the renderer. If an airborne shell expires before reaching its aim point, it is removed.

The explosion uses the same XZ enemy-only damage query as the Hexagon shockwave. The default shell radius is 7 units and its edge fraction is 31.25% of the firing unit's damage. With Heavy Artillery's 80 damage, that gives 25 at the edge before armour. The blast can reach enemies across nearby cover. The brief ring reuses the shockwave batch and is drawn at ground height at the impact center.
