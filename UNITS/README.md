# UNITS — Soldier Catalogue

**Status:** UNIT0 source implemented and awaiting owner validation.

This **UPPERCASE major folder** owns the roster, unit definitions and catalogue lookup. `SIMULATION/` owns compact runtime soldier arrays; `COMBAT/` owns generic attacks and damage; `RENDERING/` owns faceted meshes and shared MultiMesh batches; `DEPLOYMENT/` owns painting and saved placements; `BENCHMARK/` owns test scenarios and controls.

- **[UNIT0 implementation plan](UNIT0_IMPLEMENTATION_PLAN.md)** — direct Codex instructions, architecture, migration, checks and performance requirements.
- **[Five-unit roster and proposed stats](DEFINITIONS/STARTING_ROSTER.md)** — the initial circle, square, triangle, diamond and pentagon with roles and tuning values.

## Folder structure

```text
UNITS/
  README.md
  UNIT0_IMPLEMENTATION_PLAN.md
  DEFINITIONS/
    STARTING_ROSTER.md
    UnitDefinition.cs
    CircleInfantry.tres
    SquareInfantry.tres
    TriangleArcher.tres
    DiamondCavalry.tres
    PentagonElite.tres
  CATALOG/
    UnitCatalog.cs
    DefaultUnitCatalog.tres
```

The arena loads the catalogue once and builds a shared stat lookup and mesh set. Runtime byte indices are explicitly Circle 0, Square 1, Triangle 2, Diamond 3 and Pentagon 4. Deployment saves use version 1 and stable textual IDs. Legacy arrays map kind 0 to Circle and kind 1 to Triangle.

Open a definition resource to tune its silhouette, dimensions, footprint, speed, health, armour, reach, damage or attack interval. The arena reads changes on restart. Both factions use the same definitions. Melee reach adds the defender’s footprint radius to the authored reach. Armour multiplies damage by the fraction left after reduction for both melee and projectiles.

The painter enforces circular body clearance. Two default pentagons cannot occupy edge-adjacent cells. Movement respects footprint when stopping to attack, and full crowd separation is a future system.
