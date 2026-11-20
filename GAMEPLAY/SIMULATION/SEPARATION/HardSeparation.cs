// Validates Benchmark movement against occupied soldier footprints without soft pushing.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal sealed class HardSeparation
{
    private const int SearchRings = 32;
    private readonly Dictionary<Vector2I, int> cells = new();
    private readonly Vector2[] accepted;
    private readonly int[] next;
    private float cellWidth;

    internal bool Ready { get; private set; }
    private bool placementFailed;
    internal int BlockedMoves { get; private set; }

    internal HardSeparation(int capacity)
    {
        accepted = new Vector2[capacity];
        next = new int[capacity];
    }

    // Build a valid starting layout transactionally when entering the mode.
    internal bool Begin(UnitState units, ObstacleField obstacles, float boundary)
    {
        if (placementFailed) return false;
        cells.Clear();
        cellWidth = Mathf.Max(0.1f, units.MaxFootprint * 2f);
        BlockedMoves = 0;
        if (Ready)
        {
            for (int id = 0; id < units.Count; id++)
                if (units.Health[id] > 0) Add(id, ObstacleField.Xz(units.Position[id]));
            return true;
        }

        for (int id = 0; id < units.Count; id++)
        {
            if (units.Health[id] <= 0) continue;
            Vector2 origin = ObstacleField.Xz(units.Position[id]);
            float radius = units.Catalog[units.Type[id]].Footprint;
            if (Fits(units, origin, radius, obstacles, boundary)) Add(id, origin);
            else if (!FindRoom(units, id, origin, radius, obstacles, boundary))
            {
                placementFailed = true;
                return false;
            }
        }
        for (int id = 0; id < units.Count; id++)
            if (units.Health[id] > 0) Apply(units, id, accepted[id]);
        Ready = true;
        return true;
    }

    internal void End(UnitState units, ObstacleField obstacles, float boundary)
    {
        for (int id = 0; id < units.Count; id++)
        {
            if (units.Health[id] <= 0) continue;
            Vector2 from = accepted[id], to = ObstacleField.Xz(units.Position[id]);
            if (from == to) continue;
            float radius = units.Catalog[units.Type[id]].Footprint;
            Remove(id, from);
            if (!Fits(units, to, radius, obstacles, boundary))
            {
                BlockedMoves++;
                to = from;
            }
            Add(id, to);
            Apply(units, id, to);
        }
    }

    private bool FindRoom(UnitState units, int id, Vector2 origin, float radius, ObstacleField obstacles, float boundary)
    {
        float spacing = Mathf.Max(radius * 2f, 0.1f);
        for (int ring = 1; ring <= SearchRings; ring++)
        for (int edge = 0; edge < 4; edge++)
        for (int along = -ring; along < ring; along++)
        {
            var offset = edge switch
            {
                0 => new Vector2(along, -ring), 1 => new Vector2(ring, along),
                2 => new Vector2(-along, ring), _ => new Vector2(-ring, -along)
            };
            Vector2 candidate = origin + offset * spacing;
            if (!Fits(units, candidate, radius, obstacles, boundary)) continue;
            Add(id, candidate);
            return true;
        }
        return false;
    }

    private bool Fits(UnitState units, Vector2 point, float radius, ObstacleField obstacles, float boundary)
    {
        if (Mathf.Abs(point.X) + radius > boundary || Mathf.Abs(point.Y) + radius > boundary) return false;
        Vector2I cell = Cell(point);
        for (int z = -1; z <= 1; z++)
        for (int x = -1; x <= 1; x++)
        {
            if (!cells.TryGetValue(cell + new Vector2I(x, z), out int other)) continue;
            while (other >= 0)
            {
                float clearance = radius + units.Catalog[units.Type[other]].Footprint;
                if (point.DistanceSquaredTo(accepted[other]) < clearance * clearance) return false;
                other = next[other];
            }
        }
        return obstacles?.IntersectsCircle(point, radius) != true;
    }

    private Vector2I Cell(Vector2 p) => new(Mathf.FloorToInt(p.X / cellWidth), Mathf.FloorToInt(p.Y / cellWidth));

    private void Add(int id, Vector2 point)
    {
        Vector2I cell = Cell(point);
        next[id] = cells.TryGetValue(cell, out int head) ? head : -1;
        cells[cell] = id;
        accepted[id] = point;
    }

    private void Remove(int id, Vector2 point)
    {
        Vector2I cell = Cell(point);
        int other = cells[cell];
        if (other == id)
        {
            if (next[id] < 0) cells.Remove(cell);
            else cells[cell] = next[id];
            return;
        }
        while (next[other] != id) other = next[other];
        next[other] = next[id];
    }

    private static void Apply(UnitState units, int id, Vector2 point)
    {
        Vector3 position = units.Position[id];
        if (point == ObstacleField.Xz(position)) return;
        units.Position[id] = new Vector3(point.X, position.Y, point.Y);
        units.Dirty[id] = true;
        units.Grounding?.MarkMoved(id);
    }
}
