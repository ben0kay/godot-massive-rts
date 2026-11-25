// Shared targeting, footprint approach and attacks for destructible structures.
using System;
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
    private const int TowerRetryTicks = 120;
    private const float RecheckDistanceSquared = 0.25f;
    private readonly record struct TowerApproach(Vector3 Origin, Vector3? Goal, int RetryTick, bool Confirmed = false);
    private Dictionary<(int Soldier, StructureHealth Tower), TowerApproach> towerApproaches;
    private ObstacleField approachField;

    public StructureCombat(UnitState units, IReadOnlyList<StructureHealth> structures, float mapHalfWidth = DeploymentLayout.WorldHalfWidth)
    {
        this.units = units;
        this.structures = structures;
        this.mapHalfWidth = mapHalfWidth;
        targets = new StructureHealth[units.Position.Length];
        approachGoals = new Vector3[targets.Length];
    }

    public StructureHealth Acquire(int id, in UnitStats type, float range, bool refresh,
        bool canApproach, ObstacleField field, ObstacleField sight, int tick,
        ObstacleNavigation navigation = null, CommandMovement commands = null)
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
                    (!canApproach || !TryApproach(id, type, candidate, field, sight, tick, navigation, commands, out goal))) continue;
                approachGoals[id] = goal;
                best = distance;
                target = candidate;
            }
        }
        return targets[id] = target;
    }

    internal bool TryPrioritize(int id, in UnitStats type, StructureHealth objective, ObstacleField sight,
        ObstacleField field, int tick, ObstacleNavigation navigation, bool approach)
    {
        Vector3 goal = units.Position[id];
        if (!CanHit(id, type, objective, sight) &&
            (!approach || !TryApproach(id, type, objective, field, sight, tick, navigation, null, out goal)))
            return false;
        approachGoals[id] = goal;
        targets[id] = objective;
        return true;
    }

    public void SetTarget(int id, StructureHealth target) => targets[id] = target;
    public StructureHealth Target(int id) => targets[id];
    public Vector3 ApproachGoal(int id) => approachGoals[id];

    public static ObstacleField Sight(in UnitStats type, UnitState units, ObstacleField field) =>
        type.DirectFire(units.Catalog.Projectiles) ? field : null;

    public static Vector2 Edge(ObstacleRectangle area, Vector2 position) => position.Max(area.Min).Min(area.Max);

    public static float Reach(in UnitStats type) =>
        type.Ranged ? type.Reach : Mathf.Max(type.Reach, type.Footprint + 0.08f);

    public bool CanHit(int id, in UnitStats type, StructureHealth target, ObstacleField sight) =>
        CanHit(units.Position[id], type, target, sight);

    private bool CanHit(Vector3 position, in UnitStats type, StructureHealth target, ObstacleField sight)
    {
        Vector2 from = ObstacleField.Xz(position);
        Vector2 edge = Edge(target.Area, from);
        float squared = from.DistanceSquaredTo(edge);
        float reach = type.Ranged ? units.RangedReach(position, type) : Reach(type);
        return squared <= reach * reach && squared >= type.MinimumFireRange * type.MinimumFireRange &&
            sight?.BlocksSightExcept(position, Ground(edge), target.Area) != true;
    }

    private bool TryApproach(int id, in UnitStats type, StructureHealth target, ObstacleField field,
        ObstacleField sight, int tick, ObstacleNavigation navigation, CommandMovement commands, out Vector3 goal)
    {
        Vector3 position = units.Position[id];
        bool checkRoute = target.IsTower && field != null && (navigation != null || commands != null);
        var key = (id, target);
        if (checkRoute && !ReferenceEquals(approachField, field))
        {
            towerApproaches?.Clear();
            approachField = field;
        }

        goal = position;
        Vector3 origin = position;
        if (checkRoute && towerApproaches != null && towerApproaches.TryGetValue(key, out var previous))
        {
            if (previous.Confirmed && previous.Goal is Vector3 reachable && targets[id] == target &&
                CanHit(reachable, type, target, sight))
            {
                goal = reachable;
                return true;
            }
            if (ObstacleField.Xz(position).DistanceSquaredTo(ObstacleField.Xz(previous.Origin)) <= RecheckDistanceSquared &&
                tick < previous.RetryTick)
            {
                if (previous.Goal == null) return false;
                origin = previous.Origin;
            }
            else towerApproaches.Remove(key);
        }

        Span<Vector3> candidates = stackalloc Vector3[12];
        int count = CollectApproaches(id, type, target, field, sight, origin, candidates);
        if (count == 0) return false;
        goal = candidates[0];
        if (!checkRoute || !field.BlocksCorridor(ObstacleField.Xz(position), ObstacleField.Xz(goal), type.Footprint))
        {
            if (checkRoute) towerApproaches?.Remove(key);
            return true;
        }

        for (int i = 1; i < count; i++)
        {
            if (field.BlocksCorridor(ObstacleField.Xz(position), ObstacleField.Xz(candidates[i]), type.Footprint)) continue;
            goal = candidates[i];
            towerApproaches?.Remove(key);
            return true;
        }

        // Try the closest position first, then the remaining faces in sample order.
        navigation ??= commands?.StructureNavigation();
        if (navigation == null) return true;
        towerApproaches ??= new();
        // A pending request retains a fixed origin so separation cannot generate a
        // fresh cache key each tick. Discard that origin if the soldier moves away.
        for (int i = 0; i < count; i++)
        {
            Vector3 candidate = candidates[i];
            bool? reachable = navigation.ProbeStructureRoute(id, units.Type[id], ObstacleField.Xz(origin), ObstacleField.Xz(candidate));
            if (reachable == false) continue;
            towerApproaches[key] = new TowerApproach(origin, candidate, tick + TowerRetryTicks, reachable == true);
            if (reachable == null) return false;
            goal = candidate;
            return true;
        }
        towerApproaches[key] = new TowerApproach(origin, null, tick + TowerRetryTicks);
        return false;
    }

    private int CollectApproaches(int id, in UnitStats type, StructureHealth target, ObstacleField field,
        ObstacleField sight, Vector3 position, Span<Vector3> candidates)
    {
        Vector2 p = ObstacleField.Xz(position);
        Vector2 min = target.Area.Min, max = target.Area.Max;
        float clearance = type.Footprint + 0.025f;
        float standOff = type.Ranged
            ? Mathf.Max(clearance, Mathf.Max(type.MinimumFireRange + 0.02f, units.RangedReach(id, type) * 0.9f))
            : clearance;
        int count = 0;
        float nearest = float.PositiveInfinity;
        // Sample the projected position and both ends of each face.
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
                if (Mathf.Abs(candidate.X) + type.Footprint > mapHalfWidth ||
                    Mathf.Abs(candidate.Y) + type.Footprint > mapHalfWidth ||
                    field?.IntersectsCircle(candidate, type.Footprint) == true || !CanHit(world, type, target, sight)) continue;
                if (distance < nearest)
                {
                    if (count > 0) candidates[count] = candidates[0];
                    candidates[0] = world;
                    nearest = distance;
                }
                else candidates[count] = world;
                count++;
            }
        }
        return count;
    }

    public bool Attack(int id, ProjectileSystem projectiles, float dt, ObstacleField sight)
    {
        StructureHealth target = targets[id];
        if (target?.Destroyed != false || units.Health[id] <= 0f || units.Cooldown[id] > 0f) return false;
        ref readonly var type = ref units.Catalog[units.Type[id]];
        if (!CanHit(id, type, target, sight)) return false;
        Vector2 from = ObstacleField.Xz(units.Position[id]);
        Vector2 edge = Edge(target.Area, from);
        if (!SoldierFacing.Aim(units, id, edge - from, dt, type.Outline.IsChevron ? 0.995f : 0.97f))
            return true;

        if (type.Ranged)
        {
            if (!projectiles.FireAtStructure(id, units, units.OnTerrain(Ground(edge)) + Vector3.Up * 1.5f, target)) return false;
        }
        else target.Damage(type.AgainstStructures(type.Damage), type.StructureDamageType);
        units.HitFeedback?.OnAttack(units, id);
        units.Cooldown[id] = type.Interval;
        return units.Dirty[id] = true;
    }

    private static Vector3 Ground(Vector2 point) => new(point.X, 0f, point.Y);
}
