// Shared nearest-enemy index over occupied grid cells. Empty space needs no storage.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal sealed class OccupiedCellTree
{
    private readonly record struct CellEntry(int X, int Z, int Head);
    private readonly record struct Node(Vector2 Min, Vector2 Max, int Start, int End, int Left, int Right);
    private readonly List<CellEntry> cells = new();
    private readonly List<Node> nodes = new();
    private readonly float cellSize;

    public OccupiedCellTree(float cellSize) => this.cellSize = cellSize;

    public void Clear()
    {
        cells.Clear();
        nodes.Clear();
    }

    public void Add(int x, int z, int head) => cells.Add(new CellEntry(x, z, head));

    public void Build()
    {
        if (cells.Count > 0) BuildRange(0, cells.Count);
    }

    private int BuildRange(int start, int end)
    {
        CellEntry first = cells[start];
        int minX = first.X, maxX = first.X, minZ = first.Z, maxZ = first.Z;
        for (int i = start + 1; i < end; i++)
        {
            CellEntry cell = cells[i];
            minX = System.Math.Min(minX, cell.X);
            maxX = System.Math.Max(maxX, cell.X);
            minZ = System.Math.Min(minZ, cell.Z);
            maxZ = System.Math.Max(maxZ, cell.Z);
        }

        int index = nodes.Count;
        nodes.Add(default);
        int left = -1, right = -1;
        if (end - start > 8)
        {
            bool splitX = (long)maxX - minX >= (long)maxZ - minZ;
            int low = splitX ? minX : minZ;
            int high = splitX ? maxX : maxZ;
            if (low < high)
            {
                int midpoint = low + (int)(((long)high - low) / 2);
                int split = start;
                for (int i = start; i < end; i++)
                {
                    CellEntry cell = cells[i];
                    if ((splitX ? cell.X : cell.Z) > midpoint) continue;
                    cells[i] = cells[split];
                    cells[split++] = cell;
                }
                left = BuildRange(start, split);
                right = BuildRange(split, end);
            }
        }

        // Expand slightly to cover rounding in the position-to-cell conversion.
        Vector2 min = new(minX * cellSize, minZ * cellSize);
        Vector2 max = new((maxX + 1f) * cellSize, (maxZ + 1f) * cellSize);
        var margin = Vector2.One * (0.001f + Mathf.Max(min.Abs().Length(), max.Abs().Length()) * 0.000001f);
        nodes[index] = new Node(min - margin, max + margin, start, end, left, right);
        return index;
    }

    public int FindNearest(UnitState units, int[] next, int seeker, int previous, bool pruneCells)
    {
        if (nodes.Count == 0) return -1;
        Vector3 origin = units.Position[seeker];
        int best = previous >= 0 && units.Health[previous] > 0 ? previous : -1;
        float bestDistance = best >= 0 ? origin.DistanceSquaredTo(units.Position[best]) : float.PositiveInfinity;
        Search(0, units, next, origin, pruneCells, ref best, ref bestDistance);
        return best;
    }

    private static float Bound(Node node, Vector3 origin) => Bound(node.Min, node.Max, origin);

    private static float Bound(Vector2 min, Vector2 max, Vector3 origin)
    {
        float dx = Mathf.Max(0, Mathf.Max(min.X - origin.X, origin.X - max.X));
        float dz = Mathf.Max(0, Mathf.Max(min.Y - origin.Z, origin.Z - max.Y));
        return dx * dx + dz * dz;
    }

    // =========================================================
    // XZ distance to the padded cell is a lower bound on every soldier's 3D distance.
    private float CellBound(CellEntry cell, Vector3 origin)
    {
        Vector2 min = new(cell.X * cellSize, cell.Z * cellSize);
        Vector2 max = new((cell.X + 1f) * cellSize, (cell.Z + 1f) * cellSize);
        // Use an upper bound on the node's rounding margin without a square root.
        Vector2 extent = min.Abs().Max(max.Abs());
        Vector2 margin = Vector2.One * (0.001f + (extent.X + extent.Y) * 0.000001f);
        return Bound(min - margin, max + margin, origin);
    }

    private void Search(int index, UnitState units, int[] next, Vector3 origin, bool pruneCells, ref int best, ref float bestDistance)
    {
        Node node = nodes[index];
        if (Bound(node, origin) >= bestDistance) return;
        if (node.Left >= 0)
        {
            int near = node.Left, far = node.Right;
            if (Bound(nodes[far], origin) < Bound(nodes[near], origin)) (near, far) = (far, near);
            Search(near, units, next, origin, pruneCells, ref best, ref bestDistance);
            Search(far, units, next, origin, pruneCells, ref best, ref bestDistance);
            return;
        }

        for (int i = node.Start; i < node.End; i++)
        {
            CellEntry cell = cells[i];
            if (pruneCells && CellBound(cell, origin) >= bestDistance) continue;
            for (int id = cell.Head; id >= 0; id = next[id])
            {
                if (units.Health[id] <= 0) continue;
                float distance = origin.DistanceSquaredTo(units.Position[id]);
                if (distance >= bestDistance) continue;
                best = id;
                bestDistance = distance;
                if (distance == 0) return;
            }
        }
    }
}
