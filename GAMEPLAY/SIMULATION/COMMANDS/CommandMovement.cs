// Moves only soldiers with active orders, using shared obstacle routing when needed.
using System;
using Godot;

namespace MassiveRts;

internal sealed class CommandMovement
{
    // Speed up only the Fabricator's first gate-to-exit leg; normal orders use authored speed.
    private const float FabricatorExitSpeedMultiplier = 1.5f;
    public const int SquadSize = 20;
    public const int DefaultColumns = 5;
    private readonly UnitState units;
    private readonly int[] active;
    private readonly bool[] ordered, hasExit, pursuing, attackMove, engaged;
    private readonly Vector3[] exits, engagementGoals;
    private int activeCount;
    public int ActiveCount => activeCount;
    public float MapHalfWidth { get; }
    public bool OnMap(Vector3 point, float radius = 0f) =>
        Mathf.Abs(point.X) + radius <= MapHalfWidth && Mathf.Abs(point.Z) + radius <= MapHalfWidth;
    private ObstacleField field;
    private ObstacleNavigation navigation;

    public CommandMovement(UnitState units, float mapHalfWidth)
    {
        this.units = units;
        MapHalfWidth = mapHalfWidth;
        int capacity = units.Position.Length;
        active = new int[capacity];
        ordered = new bool[capacity];
        hasExit = new bool[capacity];
        pursuing = new bool[capacity];
        attackMove = new bool[capacity];
        engaged = new bool[capacity];
        engagementGoals = new Vector3[capacity];
        exits = new Vector3[capacity];
    }

    public void SetObstacles(ObstacleField obstacles)
    {
        field = obstacles;
        navigation = null;
    }

    internal ObstacleNavigation StructureNavigation() =>
        field == null ? null : (navigation ??= new ObstacleNavigation(field, units.Catalog, active.Length, 12f, 128, 4));

    internal void PrepareBridgeRoutes()
    {
        if (field?.Bridges.Length > 0)
            navigation ??= new ObstacleNavigation(field, units.Catalog, active.Length, 12f, 128, 4);
    }

    public static Vector3 Slot(Vector3 front, Vector3 right, int columns, int index, int count, float spacing)
    {
        int row = index / columns;
        int rankSize = Mathf.Min(columns, count - row * columns);
        return front + right * ((index % columns - (rankSize - 1) * 0.5f) * spacing)
            + right.Cross(Vector3.Up) * (row * spacing);
    }

    public void Issue(ReadOnlySpan<int> ids, Vector3 front, Vector3 right, int columns, float spacing, bool attack = false)
    {
        for (int i = 0; i < ids.Length; i++)
            IssueOne(ids[i], Slot(front, right, columns, i, ids.Length, spacing), attack);
    }

    // Explicit freeform destinations use the existing move-order checks and state.
    public void IssueSlots(ReadOnlySpan<int> ids, ReadOnlySpan<Vector3> goals, bool attack = false)
    {
        for (int i = 0; i < ids.Length && i < goals.Length; i++)
            IssueOne(ids[i], goals[i], attack);
    }

    private void IssueOne(int id, Vector3 goal, bool attack)
    {
        ref readonly var stats = ref units.Catalog[units.Type[id]];
        float radius = stats.Footprint;
        if (units.Health[id] <= 0 || !OnMap(goal, radius) ||
            field?.ForTier(stats.SizeTier).IntersectsCircle(ObstacleField.Xz(goal), radius) == true) return;
        SetGoal(id, goal);
        attackMove[id] = attack;
        units.Target[id] = -1;
    }

    public void SetGoal(int id, Vector3 goal, Vector3? exit = null)
    {
        pursuing[id] = attackMove[id] = engaged[id] = false;
        units.Goal[id] = units.OnTerrain(goal);
        hasExit[id] = exit.HasValue;
        exits[id] = exit.GetValueOrDefault();
        if (ordered[id]) return;
        ordered[id] = true;
        active[activeCount++] = id;
    }

    public bool HasCommand(int id) => ordered[id] && !pursuing[id];

    public bool IsAttackMove(int id) => ordered[id] && attackMove[id];

    public void Engage(int id, Vector3 goal)
    {
        if (IsAttackMove(id))
        {
            engagementGoals[id] = goal;
            engaged[id] = true;
        }
        else SetPursuitGoal(id, goal);
    }

    public void Disengage(int id)
    {
        engaged[id] = false;
        EndPursuit(id);
    }

    public void SetPursuitGoal(int id, Vector3 goal)
    {
        if (HasCommand(id)) return;
        SetGoal(id, goal);
        pursuing[id] = true;
    }

    public void EndPursuit(int id)
    {
        if (pursuing[id]) SetGoal(id, units.Position[id]);
    }

    public bool Step(float delta)
    {
        if (navigation?.QueueDepth > 0) navigation.BuildQueuedRoutes();
        else navigation?.AdvanceFrame();
        if (activeCount == 0) return false;
        bool changed = false;
        var turnSteps = units.TurnSteps(delta);
        for (int i = activeCount - 1; i >= 0; i--)
        {
            int id = active[i];
            Vector3 previous = units.Position[id];
            ref readonly var stats = ref units.Catalog[units.Type[id]];
            Vector2 start = ObstacleField.Xz(previous);
            // Separation can displace a soldier before it lands exactly on a shared exit.
            float exitTolerance = Mathf.Max(0.1f, stats.Footprint);
            if (hasExit[id] && start.DistanceSquaredTo(ObstacleField.Xz(exits[id])) <= exitTolerance * exitTolerance)
                hasExit[id] = false;
            Vector3 goal = hasExit[id] ? exits[id] : engaged[id] ? engagementGoals[id] : units.Goal[id];
            Vector2 waypoint = ObstacleField.Xz(goal);
            bool arrived = start.IsEqualApprox(waypoint);
            if (units.Health[id] <= 0 || arrived && !hasExit[id] && !engaged[id])
            {
                ordered[id] = pursuing[id] = attackMove[id] = engaged[id] = false;
                active[i] = active[--activeCount];
                continue;
            }
            if (arrived) continue;

            byte type = units.Type[id];
            var movementField = field?.ForTier(stats.SizeTier);
            if (movementField?.BlocksCorridor(start, waypoint, stats.Footprint) == true)
            {
                navigation ??= new ObstacleNavigation(field, units.Catalog, active.Length, 12f, 128, 4);
                waypoint = navigation.Steer(id, type, start, waypoint, 0, out _, out _);
            }
            float speed = units.TravelSpeed(id, stats) * (hasExit[id] ? FabricatorExitSpeedMultiplier : 1f);
            if (units.Grounding != null) speed *= units.SlopeSpeedFactor(id, stats, (waypoint - start).Normalized());
            Vector2 next = start.MoveToward(waypoint, speed * delta);
            if (movementField != null) next = movementField.ClipMove(start, next, stats.Footprint);
            if (next == start) continue;
            units.Wrecks?.CrushAlong(start, next, stats);
            units.Position[id] = new Vector3(next.X, previous.Y, next.Y);
            units.Grounding?.MarkMoved(id);
            if (stats.Shape != UnitShape.Circle)
                SoldierFacing.Turn(units, id, (next - start).Normalized(), turnSteps[type]);
            units.Dirty[id] = true;
            changed = true;
        }
        return changed;
    }
}
