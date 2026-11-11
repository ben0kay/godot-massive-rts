// Reused linked-cell index with candidate-bounded local enemy queries.
using System;
using System.Collections.Generic;
using Godot;
namespace MassiveRts;

public sealed class SpatialGrid
{
    private readonly Dictionary<Vector3I, int> heads = new(32768);
    private readonly int[] next;
    private readonly float cellSize;
    public int LimitedQueries { get; private set; }
    public SpatialGrid(int capacity, float size) { next = new int[capacity]; cellSize = size; }
    private Vector2I Cell(Vector3 p) => new(Mathf.FloorToInt(p.X / cellSize), Mathf.FloorToInt(p.Z / cellSize));
    // =========================================================
    public void Rebuild(UnitState units)
    {
        heads.Clear(); LimitedQueries = 0;
        // Reverse insertion preserves low IDs at the head; rotating queries change their start cell.
        for (int i = units.Count - 1; i >= 0; i--)
        {
            if (units.Health[i] <= 0) continue;
            var cell = Cell(units.Position[i]);
            var key = new Vector3I(cell.X, cell.Y, units.Faction[i]);
            next[i] = heads.TryGetValue(key, out int first) ? first : -1; heads[key] = i;
        }
    }
    // =========================================================
    public int FindEnemy(UnitState units, int id, float radius, int budget, int tick)
    {
        Vector2I center = Cell(units.Position[id]);
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
                if (!heads.TryGetValue(new Vector3I(center.X + dx, center.Y + dz, 1 - units.Faction[id]), out int other)) continue;
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
}
