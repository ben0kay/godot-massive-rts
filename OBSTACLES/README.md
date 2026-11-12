# OBSTACLES — Static World Blockers

**Status:** OBST1 source implemented. Awaiting owner validation in Godot.

[DEPLOYMENT](../DEPLOYMENT/README.md) authors wall cells. ObstacleLayout merges them into deterministic rectangles while preserving holes and gaps. ObstacleField indexes the rectangles in a static bounding volume tree. Ground movement uses exact circle overlap and swept rounded-rectangle contact. Ordinary motion, charge travel, trample shoves and each separation pass all clip against this field. Rendering combines the rectangles into one dark-gray mesh with the same beveled face shading as the soldiers.

Melee, cleave and direct-fire weapons check cover at attack time. Direct projectiles compare the earliest wall and unit contact along each flight segment. Walls win ties. The initial cover test is 2D across XZ, including for shallow arrow arcs. **Obstacle Fire** and **Blocks On Obstacles** on the projectile definition separately control aim permission and projectile contact. Indirect fire bypasses the aiming gate. Height-aware projectile collisions are a future feature.

See [NAVIGATION](../NAVIGATION/README.md) for routing and the [OBST1 plan](OBST1_IMPLEMENTATION_PLAN.md) for the owner checks.

Ordinary movement and separation try a swept slide along a wall when a diagonal step is clipped. Charges and trample shoves retain their straight-line contact behavior.
