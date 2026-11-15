// Updates stationary defenses against the shared soldier grid and damage pipeline.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal sealed class DefenseSystem
{
    private struct Tower
    {
        public BuildingStats Stats;
        public byte Faction;
        public Vector3 Pivot;
        public ObstacleRectangle Footprint;
        public int Target;
        public float Yaw, Cooldown;
    }

    private readonly Tower[] towers;
    private readonly DefenseRenderer renderer;
    private readonly StructureHealth[] structures;
    private readonly ObstacleField staticWalls;
    private readonly bool[] removed;
    public IReadOnlyList<StructureHealth> Structures => structures;

    public DefenseSystem(IReadOnlyList<BuildingPlacement> placements, RuntimeBuildingCatalog catalog,
        float height, DefenseRenderer renderer, DamageRules rules, ObstacleField staticWalls)
    {
        this.renderer = renderer;
        this.staticWalls = staticWalls;
        structures = new StructureHealth[placements.Count];
        removed = new bool[placements.Count];
        towers = new Tower[placements.Count];
        for (int i = 0; i < towers.Length; i++)
        {
            var placement = placements[i];
            var area = placement.Footprint(catalog);
            var stats = catalog[placement.Type];
            structures[i] = new StructureHealth(area, placement.Faction, stats.Health, rules, stats.ArmourClass, stats.Armour);
            towers[i] = new Tower { Stats = stats, Faction = placement.Faction,
                Pivot = DefenseRenderer.Pivot(area, height), Footprint = area, Target = -1 };
        }
    }

    public bool RemoveDestroyed(out ObstacleField field)
    {
        field = null;
        bool changed = false;
        int alive = 0;
        for (int i = 0; i < structures.Length; i++)
        {
            if (!structures[i].Destroyed) { alive++; continue; }
            if (removed[i]) continue;
            removed[i] = true;
            renderer.HideTower(i);
            changed = true;
        }
        if (!changed) return false;
        field = staticWalls;
        if (alive == 0) return true;

        int wallCount = staticWalls?.RectangleCount ?? 0;
        var rectangles = new ObstacleRectangle[wallCount + alive];
        if (staticWalls != null) staticWalls.Rectangles.CopyTo(rectangles);
        foreach (var structure in structures)
            if (!structure.Destroyed) rectangles[wallCount++] = structure.Area;
        field = new ObstacleField(rectangles);
        return true;
    }

    // =========================================================
    // Run before soldiers move so range queries use the positions indexed this tick.
    public void Step(UnitState units, SpatialGrid grid, ObstacleField obstacles, float dt, int acquisitionStep, float acquisitionSeconds)
    {
        int refreshInterval = Mathf.Max(1, Mathf.RoundToInt(0.2f / acquisitionSeconds));
        for (int i = 0; i < towers.Length; i++)
        {
            ref Tower tower = ref towers[i];
            if (structures[i].Destroyed || !tower.Stats.AttacksSoldiers) continue;
            tower.Cooldown = tower.Cooldown > 0f ? tower.Cooldown - dt : 0f;
            int target = tower.Target;
            if (target < 0 || !Valid(in tower, units, target, obstacles))
            {
                tower.Target = -1;
                tower.Cooldown = Mathf.Max(0f, tower.Cooldown);
                if (acquisitionStep < 0 || i % refreshInterval != acquisitionStep % refreshInterval) continue;
                float nearest = float.PositiveInfinity;
                var candidates = grid.GetCandidates(tower.Pivot, tower.Stats.MaximumRange, tower.Faction);
                while (candidates.MoveNext())
                {
                    int id = candidates.Current;
                    if (!InRange(in tower, units, id, out float distance) || distance >= nearest ||
                        !Visible(in tower, units.Position[id], obstacles)) continue;
                    tower.Target = id;
                    nearest = distance;
                }
                if (tower.Target < 0) continue;
            }

            target = tower.Target;
            Vector3 offset = units.Position[target] - tower.Pivot;
            float desired = Mathf.Atan2(-offset.X, -offset.Z);
            float error = Mathf.Wrap(desired - tower.Yaw, -Mathf.Pi, Mathf.Pi);
            float step = tower.Stats.TurnRate * dt;
            if (Mathf.Abs(error) > 0.0001f)
            {
                tower.Yaw = Mathf.Wrap(tower.Yaw + Mathf.Clamp(error, -step, step), -Mathf.Pi, Mathf.Pi);
                renderer.Aim(i, tower.Pivot, tower.Yaw);
            }

            if (tower.Cooldown > 0f || Mathf.Abs(error) > step) continue;
            units.Damage(target, tower.Stats.Damage, tower.Stats.DamageType);
            Vector3 end = units.Position[target] + Vector3.Up * (units.Catalog[units.Type[target]].Height * 0.5f);
            renderer.Fire(DefenseRenderer.Muzzle(tower.Pivot, tower.Yaw), end, tower.Faction,
                tower.Stats.BeamSeconds, tower.Yaw);
            tower.Cooldown = Mathf.Max(0f, tower.Cooldown + tower.Stats.Interval);
        }
    }

    private static bool Valid(in Tower tower, UnitState units, int id, ObstacleField obstacles) =>
        InRange(in tower, units, id, out _) && Visible(in tower, units.Position[id], obstacles);

    private static bool InRange(in Tower tower, UnitState units, int id, out float distance)
    {
        distance = ObstacleField.Xz(units.Position[id] - tower.Pivot).LengthSquared();
        return units.Health[id] > 0f && units.Faction[id] != tower.Faction &&
            distance >= tower.Stats.MinimumRange * tower.Stats.MinimumRange &&
            distance <= tower.Stats.MaximumRange * tower.Stats.MaximumRange;
    }

    private static bool Visible(in Tower tower, Vector3 target, ObstacleField obstacles) =>
        !tower.Stats.RequiresLineOfSight || obstacles == null ||
        !obstacles.BlocksSightExcept(tower.Pivot, target, tower.Footprint);
}
