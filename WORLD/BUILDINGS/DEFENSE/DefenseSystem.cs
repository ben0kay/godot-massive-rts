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

    public DefenseSystem(IReadOnlyList<BuildingPlacement> placements, RuntimeBuildingCatalog catalog,
        float height, DefenseRenderer renderer)
    {
        this.renderer = renderer;
        towers = new Tower[placements.Count];
        for (int i = 0; i < towers.Length; i++)
        {
            var placement = placements[i];
            var area = placement.Footprint(catalog);
            towers[i] = new Tower { Stats = catalog[placement.Type], Faction = placement.Faction,
                Pivot = DefenseRenderer.Pivot(area, height), Footprint = area, Target = -1 };
        }
    }

    // =========================================================
    // Run before soldiers move so range queries use the positions indexed this tick.
    public void Step(UnitState units, SpatialGrid grid, ObstacleField obstacles, float dt, int acquisitionStep, float acquisitionSeconds)
    {
        int refreshInterval = Mathf.Max(1, Mathf.RoundToInt(0.2f / acquisitionSeconds));
        for (int i = 0; i < towers.Length; i++)
        {
            ref Tower tower = ref towers[i];
            if (!tower.Stats.AttacksSoldiers) continue;
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
            units.Damage(target, tower.Stats.Damage);
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
