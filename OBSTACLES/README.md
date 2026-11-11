# OBSTACLES — Static World Blockers

**Status: OBST1 planned, not source implemented.**

[OBST1 implementation plan](OBST1_IMPLEMENTATION_PLAN.md) describes the first obstacle system: paintable/savable dark-gray rectangular walls, compiled static XZ box queries for movement and visibility, and pooled-projectile collision. It integrates with a separate shared `NAVIGATION/` subsystem instead of a per-soldier pathfinder.

On implementation, OBSTACLES should own obstacle authoring data, merged AABB compilation, static query index, visibility rules and shared rendering. NAVIGATION should own cached, radius-aware routes; the deployment editor remains under DEPLOYMENT; combat/projectile systems decide which attack types obey LOS.

Initial obstacles block all ground movement and direct arrows; future artillery can use explicitly selected indirect fire. No damageable obstacles, elevations, navmesh agents, destructibility or artillery have been implemented yet.

Only this README and the plan exist until Codex implements OBST1; create proposed script files and `NAVIGATION/` as the feature is coded.
