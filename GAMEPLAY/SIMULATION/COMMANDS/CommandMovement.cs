// Moves only soldiers with active orders, using shared obstacle routing when needed.
using System;
using Godot;

namespace MassiveRts;

internal sealed class CommandMovement
{
    public const int SquadSize = 20;
    public const int DefaultColumns = 5;
    private readonly UnitState units;
    private readonly int[] active;
    private readonly bool[] ordered, hasExit;
    private readonly Vector3[] exits;
    private int activeCount;
    private ObstacleField field;
    private ObstacleNavigation navigation;

    public CommandMovement(UnitState units)
    {
        this.units = units;
        int capacity = units.Position.Length;
        active = new int[capacity];
        ordered = new bool[capacity];
        hasExit = new bool[capacity];
        exits = new Vector3[capacity];
    }

    public void SetObstacles(ObstacleField obstacles)
    {
        field = obstacles;
        navigation = null;
    }

    public static Vector3 Slot(Vector3 front, Vector3 right, int columns, int index, int count, float spacing)
    {
        int row = index / columns;
        int rankSize = Mathf.Min(columns, count - row * columns);
        return front + right * ((index % columns - (rankSize - 1) * 0.5f) * spacing)
            + right.Cross(Vector3.Up) * (row * spacing);
    }

    public void Issue(ReadOnlySpan<int> ids, Vector3 front, Vector3 right, int columns, float spacing)
    {
        for (int i = 0; i < ids.Length; i++)
        {
            int id = ids[i];
            var goal = Slot(front, right, columns, i, ids.Length, spacing);
            float radius = units.Catalog[units.Type[id]].Footprint;
            if (units.Health[id] <= 0 || !CommandBuildings.OnMap(goal, radius) || field?.IntersectsCircle(ObstacleField.Xz(goal), radius) == true) continue;
            SetGoal(id, goal);
        }
    }

    public void SetGoal(int id, Vector3 goal, Vector3? exit = null)
    {
        units.Goal[id] = goal;
        hasExit[id] = exit.HasValue;
        exits[id] = exit.GetValueOrDefault();
        if (ordered[id]) return;
        ordered[id] = true;
        active[activeCount++] = id;
    }

    public bool Step(float delta)
    {
        if (activeCount == 0) return false;
        if (navigation?.QueueDepth > 0) navigation.BuildQueuedRoutes();
        else navigation?.AdvanceFrame();
        bool changed = false;
        for (int i = activeCount - 1; i >= 0; i--)
        {
            int id = active[i];
            Vector3 previous = units.Position[id];
            Vector3 goal = hasExit[id] ? exits[id] : units.Goal[id];
            if (units.Health[id] <= 0 || previous == goal && !hasExit[id])
            {
                ordered[id] = false;
                active[i] = active[--activeCount];
                continue;
            }
            if (previous == goal)
            {
                hasExit[id] = false;
                continue;
            }

            byte type = units.Type[id];
            ref readonly var stats = ref units.Catalog[type];
            Vector2 start = ObstacleField.Xz(previous);
            Vector2 waypoint = ObstacleField.Xz(goal);
            if (field?.BlocksCorridor(start, waypoint, stats.Footprint) == true)
            {
                navigation ??= new ObstacleNavigation(field, units.Catalog, active.Length, 12f, 128, 4);
                waypoint = navigation.Steer(id, type, start, waypoint, 0, out _, out _);
            }
            Vector2 next = start.MoveToward(waypoint, stats.Speed * delta);
            if (field != null) next = field.ClipMove(start, next, stats.Footprint);
            if (next == start) continue;
            units.Position[id] = new Vector3(next.X, 0, next.Y);
            units.Facing[id] = (next - start).Normalized();
            units.Dirty[id] = true;
            changed = true;
        }
        return changed;
    }
}
