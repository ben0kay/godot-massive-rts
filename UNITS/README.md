# UNITS — Soldier Catalogue

**Status:** UNIT0 planned; the five-unit catalogue and definition files are not implemented yet.

This **UPPERCASE major folder** owns the roster, unit definitions and catalogue lookup. `SIMULATION/` owns compact runtime soldier arrays; `COMBAT/` owns generic attacks and damage; `RENDERING/` owns faceted meshes and shared MultiMesh batches; `DEPLOYMENT/` owns painting and saved placements; `BENCHMARK/` owns test scenarios and controls.

- **[UNIT0 implementation plan](UNIT0_IMPLEMENTATION_PLAN.md)** — direct Codex instructions, architecture, migration, checks and performance requirements.
- **[Five-unit roster and proposed stats](DEFINITIONS/STARTING_ROSTER.md)** — the initial circle, square, triangle, diamond and pentagon with roles and tuning values.

## Intended structure after Codex implementation

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

Add other nested folders only when real authored content justifies them. Do **not** move `CombatSystem`, `ArmyRenderer` or `UnitState` here; integrate through typed, shared catalogue data. New soldiers should be added by creating new definitions and matching reusable geometry **without creating one Node per soldier**. Stable definition IDs must survive catalogue reordering and deployment save/load.
