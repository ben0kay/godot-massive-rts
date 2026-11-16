// Shared targeting, footprint approach and attacks for destructible structures.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal sealed class StructureCombat
{
    private readonly UnitState units;
    private readonly IReadOnlyList<StructureHealth> structures;
    private readonly StructureHealth[] targets;
    private readonly Vector3[] approachGoals;
    private readonly float mapHalfWidth;

    public StructureCombat(UnitState units, IReadOnlyList<StructureHealth> structures, float mapHalfWidth = DeploymentLayout.WorldHalfWidth)
    {
        this.units = units;
        this.structures = structures;
        this.mapHalfWidth = mapHalfWidth;
        targets = new StructureHealth[units.Position.Length];
        approachGoals = new Vector3[targets.Length];
    }

    public StructureHealth Acquire(int id, in UnitStats type, float range, bool refresh,
        bool canApproach, ObstacleField field, ObstacleField sight)
    {
        Vector2 position = ObstacleField.Xz(units.Position[id]);
        float best = range * range;
        StructureHealth target = targets[id];
        if (target?.Destroyed != false || position.DistanceSquaredTo(Edge(target.Area, position)) > best)
            target = null;
        if (refresh)
        {
            target = null;
            foreach (var candidate in structures)
            {
                if (candidate.Destroyed || candidate.Faction == units.Faction[id]) continue;
                float distance = position.DistanceSquaredTo(Edge(candidate.Area, position));
                if (distance > best || (distance == best && target != null)) continue;
                Vector3 goal = units.Position[id];
                if (!CanHit(id, type, candidate, sight) &&
                    (!canApproach || !TryApproach(id, type, candidate, field, sight, out goal))) continue;
                approachGoals[id] = goal;
                best = distance;
                target = candidate;
            }
        }
        return targets[id] = target;
    }

    public void SetTarget(int id, StructureHealth target) => targets[id] = target;
    public StructureHealth Target(int id) => targets[id];
    public Vector3 ApproachGoal(int id) => approachGoals[id];

    public static ObstacleField Sight(in UnitStats type, UnitState units, ObstacleField field) =>
        !type.Ranged || units.Catalog.Projectiles.Flight(type.ProjectileType).ObstacleFire ==
            ObstacleFireMode.RequiresDirectLineOfSight ? field : null;

    public static Vector2 Edge(ObstacleRectangle area, Vector2 position) => position.Max(area.Min).Min(area.Max);

    public static float Reach(in UnitStats type) =>
        type.Ranged ? type.Reach : Mathf.Max(type.Reach, type.Footprint + 0.08f);

    public bool CanHit(int id, in UnitStats type, StructureHealth target, ObstacleField sight) =>
        CanHit(units.Position[id], type, target, sight);

    private static bool CanHit(Vector3 position, in UnitStats type, StructureHealth target, ObstacleField sight)
    {
        Vector2 from = ObstacleField.Xz(position);
        Vector2 edge = Edge(target.Area, from);
        float squared = from.DistanceSquaredTo(edge);
        float reach = Reach(type);
        return squared <= reach * reach && squared >= type.MinimumFireRange * type.MinimumFireRange &&
            sight?.BlocksSightExcept(position, Ground(edge), target.Area) != true;
    }

    private bool TryApproach(int id, in UnitStats type, StructureHealth target, ObstacleField field, ObstacleField sight, out Vector3 goal)
    {
        Vector3 position = units.Position[id];

        Vector2 p = ObstacleField.Xz(position);
        Vector2 min = target.Area.Min, max = target.Area.Max;
        float clearance = type.Footprint + 0.025f;
        float standOff = type.Ranged
            ? Mathf.Max(clearance, Mathf.Max(type.MinimumFireRange + 0.02f, type.Reach * 0.9f))
            : clearance;
        float best = float.PositiveInfinity;
        goal = position;
        // Sample the projected position and both ends of each face. A blocked direct line
        // can still use the command mover's shared obstacle routes.
        for (int face = 0; face < 4; face++)
        {
            bool alongZ = face < 2;
            float low = alongZ ? min.Y : min.X;
            float high = alongZ ? max.Y : max.X;
            float fixedCoordinate = (face & 1) == 0
                ? (alongZ ? min.X : min.Y) - standOff
                : (alongZ ? max.X : max.Y) + standOff;
            for (int sample = 0; sample < 3; sample++)
            {
                float along = sample == 0 ? Mathf.Clamp(alongZ ? p.Y : p.X, low, high) : sample == 1 ? low : high;
                Vector2 candidate = alongZ ? new Vector2(fixedCoordinate, along) : new Vector2(along, fixedCoordinate);
                float distance = p.DistanceSquaredTo(candidate);
                Vector3 world = Ground(candidate);
                if (distance >= best || Mathf.Abs(candidate.X) + type.Footprint > mapHalfWidth ||
                    Mathf.Abs(candidate.Y) + type.Footprint > mapHalfWidth ||
                    field?.IntersectsCircle(candidate, type.Footprint) == true || !CanHit(world, type, target, sight)) continue;
                best = distance;
                goal = world;
            }
        }
        return float.IsFinite(best);
    }

    public bool Attack(int id, ProjectileSystem projectiles, float dt, ObstacleField sight)
    {
        StructureHealth target = targets[id];
        if (target?.Destroyed != false || units.Health[id] <= 0f || units.Cooldown[id] > 0f) return false;
        ref readonly var type = ref units.Catalog[units.Type[id]];
        if (!CanHit(id, type, target, sight)) return false;
        Vector2 from = ObstacleField.Xz(units.Position[id]);
        Vector2 edge = Edge(target.Area, from);
        Vector2 aim = (edge - from).Normalized();
        if (type.Shape != UnitShape.Circle && aim != Vector2.Zero && units.Facing[id].Dot(aim) < 0.97f)
        {
            SoldierFacing.Turn(units, id, aim, SoldierFacing.TurnStep(dt, type.TurnDegreesPerSecond));
            return units.Dirty[id] = true;
        }

        if (type.Ranged)
        {
            if (!projectiles.FireAtStructure(id, units, units.OnTerrain(Ground(edge)) + Vector3.Up * 1.5f, target)) return false;
        }
        else target.Damage(type.AgainstStructures(type.Damage), type.StructureDamageType);
        units.Cooldown[id] = type.Interval;
        return units.Dirty[id] = true;
    }

    private static Vector3 Ground(Vector2 point) => new(point.X, 0f, point.Y);
}
