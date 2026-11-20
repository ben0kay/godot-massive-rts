// Validates Benchmark movement against occupied soldier footprints and gives blocked melee units room to pass allies.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal sealed class HardSeparation
{
    private const int SearchRings = 32;
    private const int MaxYieldChain = 4;
    private readonly record struct YieldMove(int Id, Vector2 From);
    private readonly YieldMove[] yieldMoves = new YieldMove[MaxYieldChain];
    private readonly bool[] yielded;
    private int yieldCount, firstMover;
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
        yielded = new bool[capacity];
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
            if (Fits(units, origin, radius, obstacles, boundary, out _)) Add(id, origin);
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
        System.Array.Clear(yielded, 0, units.Count);
        int id = firstMover;
        for (int visited = 0; visited < units.Count; visited++, id = (id + 1) % units.Count)
        {
            if (units.Health[id] <= 0) continue;
            Vector2 from = accepted[id], to = ObstacleField.Xz(units.Position[id]);
            Vector2 move = to - from;
            if (move.LengthSquared() < 0.00000001f)
            {
                Apply(units, id, from);
                continue;
            }

            float radius = units.Catalog[units.Type[id]].Footprint;
            Remove(id, from);
            if (!Fits(units, to, radius, obstacles, boundary, out int blocker))
            {
                BlockedMoves++;
                Vector2 forward = move.Normalized();
                Vector2 side = new(-forward.Y, forward.X);
                // Consistent side preference avoids switching directions every tick.
                if ((id & 1) != 0) side = -side;

                if (blocker >= 0 && !units.Catalog[units.Type[id]].Ranged)
                {
                    // Keep the mover's starting space reserved while making room. Small
                    // successful nudges can accumulate even when this move still can't fit.
                    Add(id, from);
                    float nudge = Mathf.Min(move.Length(), 0.08f);
                    if (!Yield(units, blocker, units.Faction[id], side * nudge, obstacles, boundary))
                        Yield(units, blocker, units.Faction[id], -side * nudge, obstacles, boundary);
                    Remove(id, from);
                }

                if (!Fits(units, to, radius, obstacles, boundary, out _))
                {
                    // Try around the obstruction, within the original movement budget.
                    Vector2 step = side * move.Length();
                    to = from;
                    for (int attempt = 0; attempt < 4; attempt++)
                    {
                        Vector2 offset = attempt < 2
                            ? (move + step) * 0.70710678f : step;
                        if ((attempt & 1) != 0) offset -= step * (attempt < 2 ? 1.41421356f : 2f);
                        Vector2 candidate = from + offset;
                        if (!FitsStep(units, from, candidate, radius, obstacles, boundary, out _)) continue;
                        to = candidate;
                        break;
                    }
                }
            }
            Add(id, to);
            Apply(units, id, to);
        }
        firstMover = (firstMover + 1) % units.Count;
    }

    private bool Yield(UnitState units, int id, byte faction, Vector2 shift, ObstacleField obstacles, float boundary)
    {
        yieldCount = 0;
        bool moved = TryYield(units, id, faction, shift, obstacles, boundary, MaxYieldChain);
        if (!moved)
        {
            // Restore the failed attempt in reverse order. None of it reaches the unit state.
            for (int i = yieldCount - 1; i >= 0; i--)
            {
                var step = yieldMoves[i];
                Remove(step.Id, accepted[step.Id]);
                Add(step.Id, step.From);
            }
            return false;
        }
        for (int i = 0; i < yieldCount; i++)
        {
            int shifted = yieldMoves[i].Id;
            yielded[shifted] = true;
            Apply(units, shifted, accepted[shifted]);
        }
        return true;
    }

    private bool TryYield(UnitState units, int id, byte faction, Vector2 shift,
        ObstacleField obstacles, float boundary, int remaining)
    {
        if (remaining == 0 || yielded[id] || units.Faction[id] != faction ||
            !units.Catalog[units.Type[id]].Ranged) return false;

        Vector2 from = accepted[id], to = from + shift;
        float radius = units.Catalog[units.Type[id]].Footprint;
        Remove(id, from);
        bool fits = FitsStep(units, from, to, radius, obstacles, boundary, out int blocker);
        if (!fits && blocker >= 0 && TryYield(units, blocker, faction, shift, obstacles, boundary, remaining - 1))
            fits = FitsStep(units, from, to, radius, obstacles, boundary, out _);

        Add(id, fits ? to : from);
        if (fits) yieldMoves[yieldCount++] = new YieldMove(id, from);
        return fits;
    }

    private bool FitsStep(UnitState units, Vector2 from, Vector2 to, float radius,
        ObstacleField obstacles, float boundary, out int blocker)
    {
        blocker = -1;
        if (obstacles != null && obstacles.ClipMove(from, to, radius) != to) return false;
        return Fits(units, to, radius, obstacles, boundary, out blocker);
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
            if (!Fits(units, candidate, radius, obstacles, boundary, out _)) continue;
            Add(id, candidate);
            return true;
        }
        return false;
    }

    private bool Fits(UnitState units, Vector2 point, float radius, ObstacleField obstacles, float boundary, out int blocker)
    {
        blocker = -1;
        if (Mathf.Abs(point.X) + radius > boundary || Mathf.Abs(point.Y) + radius > boundary) return false;
        Vector2I cell = Cell(point);
        for (int z = -1; z <= 1; z++)
        for (int x = -1; x <= 1; x++)
        {
            if (!cells.TryGetValue(cell + new Vector2I(x, z), out int other)) continue;
            while (other >= 0)
            {
                float clearance = radius + units.Catalog[units.Type[other]].Footprint;
                if (point.DistanceSquaredTo(accepted[other]) < clearance * clearance)
                {
                    blocker = other;
                    return false;
                }
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
