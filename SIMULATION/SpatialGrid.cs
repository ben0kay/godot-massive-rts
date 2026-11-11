// Reused linked-cell index with candidate-bounded local enemy queries.
using System;
using System.Collections.Generic;
using Godot;
namespace MassiveRts;

public sealed class SpatialGrid
{
    private readonly Dictionary<Vector3I, int> overflowHeads = new();
    private readonly int[] heads, occupied;
    private readonly int halfWidth, width;
    private int occupiedCount;
    private readonly int[] next;
    private readonly float cellSize;
    private readonly Vector2[] factionMin = new Vector2[2];
    private readonly Vector2[] factionMax = new Vector2[2];
    public int LimitedQueries { get; private set; }
    public SpatialGrid(int capacity, float size)
    {
        next = new int[capacity];
        occupied = new int[capacity];
        cellSize = size;
        halfWidth = Mathf.CeilToInt(640f / size);
        width = halfWidth * 2;
        heads = new int[width * width * 2];
    }

    // Zero denotes an empty cell. Stored IDs are offset by one.
    private int DenseIndex(int x, int z, int faction)
    {
        x += halfWidth;
        z += halfWidth;
        return (uint)x < (uint)width && (uint)z < (uint)width ? (z * width + x) * 2 + faction : -1;
    }

    private int Head(int x, int z, int faction)
    {
        int index = DenseIndex(x, z, faction);
        if (index >= 0) return heads[index] - 1;
        return overflowHeads.TryGetValue(new Vector3I(x, z, faction), out int id) ? id : -1;
    }
    private Vector2I Cell(Vector3 p) => new(Mathf.FloorToInt(p.X / cellSize), Mathf.FloorToInt(p.Z / cellSize));
    // =========================================================
    public void Rebuild(UnitState units)
    {
        for (int i = 0; i < occupiedCount; i++) heads[occupied[i]] = 0;
        occupiedCount = 0;
        overflowHeads.Clear();
        LimitedQueries = 0;
        for (int faction = 0; faction < 2; faction++)
        {
            factionMin[faction] = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            factionMax[faction] = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        }
        // Reverse insertion preserves low IDs at the head; rotating queries change their start cell.
        for (int i = units.Count - 1; i >= 0; i--)
        {
            if (units.Health[i] <= 0) continue;
            Vector3 position = units.Position[i];
            int faction = units.Faction[i];
            var point = new Vector2(position.X, position.Z);
            factionMin[faction] = factionMin[faction].Min(point);
            factionMax[faction] = factionMax[faction].Max(point);
            var cell = Cell(position);
            int index = DenseIndex(cell.X, cell.Y, faction);
            if (index >= 0)
            {
                int previous = heads[index];
                if (previous == 0) occupied[occupiedCount++] = index;
                next[i] = previous - 1;
                heads[index] = i + 1;
            }
            else
            {
                var key = new Vector3I(cell.X, cell.Y, faction);
                next[i] = overflowHeads.TryGetValue(key, out int first) ? first : -1;
                overflowHeads[key] = i;
            }
        }
    }
    // =========================================================
    public int FindEnemy(UnitState units, int id, float radius, int budget, int tick)
    {
        Vector3 position = units.Position[id];
        int enemy = 1 - units.Faction[id];
        Vector2 min = factionMin[enemy], max = factionMax[enemy];
        // A disjoint XZ interval proves the spherical query cannot find a target.
        if (position.X + radius < min.X || position.X - radius > max.X ||
            position.Z + radius < min.Y || position.Z - radius > max.Y) return -1;
        Vector2I center = Cell(position);
        int cells = Mathf.CeilToInt(radius / cellSize), examined = 0, best = -1;
        float bestDistance = radius * radius;
        // Search center outwards so dense distant cells cannot consume the entire query first.
        for (int ring = 0; ring <= cells; ring++)
        {
            int width = ring * 2 + 1, perimeter = ring == 0 ? 1 : ring * 8;
            int start = (id + tick) % perimeter;
            for (int k = 0; k < perimeter; k++)
            {
                int edge = (k + start) % perimeter;
                int dx, dz;
                if (ring == 0) { dx = 0; dz = 0; }
                else if (edge < width) { dx = edge - ring; dz = -ring; }
                else if (edge < width + width - 1) { dx = ring; dz = edge - width - ring + 1; }
                else if (edge < width * 3 - 2) { dx = ring - 1 - (edge - (width * 2 - 1)); dz = ring; }
                else { dx = -ring; dz = ring - 1 - (edge - (width * 3 - 2)); }
                int other = Head(center.X + dx, center.Y + dz, enemy);
                while (other >= 0)
                {
                    if (++examined > budget) { LimitedQueries++; return best; }
                    if (units.Health[other] > 0 && units.Faction[other] != units.Faction[id])
                    {
                        float distance = units.Position[id].DistanceSquaredTo(units.Position[other]);
                        if (distance < bestDistance) { bestDistance = distance; best = other; }
                    }
                    other = next[other];
                }
            }
        }
        return best;
    }

    // Search pre-movement cells padded by the maximum distance a soldier moved this tick.
    public int FirstProjectileHit(UnitState units, int enemy, Vector3 from, Vector3 segment, float radius, float movementPadding)
    {
        float padding = radius + movementPadding;
        Vector3 end = from + segment;
        var min = Cell(new Vector3(Mathf.Min(from.X, end.X) - padding, 0, Mathf.Min(from.Z, end.Z) - padding));
        var max = Cell(new Vector3(Mathf.Max(from.X, end.X) + padding, 0, Mathf.Max(from.Z, end.Z) + padding));
        float lengthSquared = segment.LengthSquared();
        float radiusSquared = radius * radius;
        float earliest = float.PositiveInfinity;
        int hit = -1;
        for (int z = min.Y; z <= max.Y; z++)
        for (int x = min.X; x <= max.X; x++)
        {
            for (int id = Head(x, z, enemy); id >= 0; id = next[id])
            {
                if (units.Health[id] <= 0) continue;
                Vector3 relative = units.Position[id] + Vector3.Up * 0.7f - from;
                float outside = relative.LengthSquared() - radiusSquared;
                float entry = 0;
                if (outside > 0)
                {
                    float projection = relative.Dot(segment);
                    float discriminant = projection * projection - lengthSquared * outside;
                    if (lengthSquared <= 0 || projection <= 0 || discriminant < 0) continue;
                    entry = (projection - Mathf.Sqrt(discriminant)) / lengthSquared;
                    if (entry > 1) continue;
                }
                if (entry >= earliest) continue;
                earliest = entry;
                hit = id;
                if (entry == 0) return hit;
            }
        }
        return hit;
    }
}
