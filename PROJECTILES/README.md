# PROJECTILES

`Shared/` owns the projectile resource, catalogue, pooled flight and collision logic, and batched rendering. `Types/BasicArrow/` owns the basic arrow definition and its short shaft, arrowhead, and fletching mesh. A ranged unit selects its projectile in its unit definition. Several units can share the same asset. Attack damage still belongs to the firing unit.

Open `Types/BasicArrow/BasicArrow.tres` in Godot to edit speed and arc. **Arc Height** caps the rise above the straight line between launch and aim. **Arc Height Per Distance** scales the rise down on short shots. The defaults are 0.65 world units maximum and 0.08 units of rise per horizontal world unit. Set Arc Height to zero for straight flight. Restart the arena after editing the resource.

To add another projectile, create a ProjectileDefinition resource in its own directory under `Types/` and assign it to a ranged unit. Set Visual Mesh to use a custom model with its tip at the origin pointing along local −Z. Definitions with no mesh use the shared arrow mesh. The renderer packs live instances by projectile definition. A shot follows its authored arc toward the aim position recorded at launch and tests for an enemy hit along each simulation segment.
