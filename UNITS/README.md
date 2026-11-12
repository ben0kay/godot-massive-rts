# UNITS — Soldier Catalogue

**Status:** UNIT0–UNIT3 source implemented. Awaiting owner validation.

This **UPPERCASE major folder** owns the roster, unit definitions and catalogue lookup. `SIMULATION/` owns compact runtime soldier arrays; `COMBAT/` owns generic attacks and damage; `RENDERING/` owns faceted meshes and shared MultiMesh batches; `DEPLOYMENT/` owns painting and saved placements; `BENCHMARK/` owns test scenarios and controls.

- **[UNIT0 implementation plan](UNIT0_IMPLEMENTATION_PLAN.md)** — direct Codex instructions, architecture, migration, checks and performance requirements.
- **[UNIT1 size scaling plan](UNIT1_SIZE_SCALING_PLAN.md)** — per-type size tuning and owner checks.
- **[Starting roster and stats](DEFINITIONS/STARTING_ROSTER.md)** — the initial circle, square, triangle, diamond and pentagon with roles and tuning values.

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
    HexagonSiege.tres
  CATALOG/
    UnitCatalog.cs
    DefaultUnitCatalog.tres
```

The arena loads the catalogue once and builds a shared stat lookup and mesh set. Runtime byte indices are explicitly Circle 0, Square 1, Triangle 2, Diamond 3 and Pentagon 4 and Hexagon 5. Deployment saves use version 1 and stable textual IDs. Legacy arrays map kind 0 to Circle and kind 1 to Triangle.

Open a definition resource to tune its silhouette, dimensions, speed, health, armour, reach, damage or attack interval. The arena reads changes on restart. Both factions use the same definitions. Normal melee attacks require body contact based on both footprints. Authored melee reach still feeds acquisition and trample launch distance. Armour multiplies damage by the fraction left after reduction for both melee and projectiles.

**Size Multiplier** in each definition scales width, length, height, footprint radius and melee reach. At 1.25, each linear dimension is 25% larger. Diamond Cavalry defaults to 1.25 and Hexagon Siege Infantry defaults to 2.0, and the other four types default to 1.0. Positive values are clamped to 0.5–3.0. Zero, negative and nonfinite values fall back to 1.0. Restart the arena after changing a definition so the catalogue and meshes rebuild together.

The catalogue fits each circular collision footprint around the scaled mesh outline. Polygon faces may sit inside that circle. The legacy Diamond `FootprintRadius` property only controls the length of its silhouette. The painter checks the resulting circular clearance against neighbours and battlefield edges. SEP1/SEP2 use the same compiled footprints. Saved layouts still use stable type IDs and the existing format. Loading a layout after increasing a type’s size can reject placements that no longer fit.

## Tuning units

Edit the existing definition in the Inspector or enable the [full-screen Unit Editor](../addons/unit_editor/README.md). UNIT2 exposes the cleave target cap, ordinary melee facing angle and shockwave outer damage. The editor writes the selected unit resource and keeps its stable ID read-only.
