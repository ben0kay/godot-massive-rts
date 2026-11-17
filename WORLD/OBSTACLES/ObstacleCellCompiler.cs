// Merges occupied XZ cells into exact, deterministic rectangles.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal static class ObstacleCellCompiler
{
    public static ObstacleRectangle[] Merge(HashSet<Vector2I> cells, float gridSize, int maxRectangles)
    {
        var remaining = new HashSet<Vector2I>(cells);
        var ordered = new List<Vector2I>(cells);
        ordered.Sort((a, b) => a.Y == b.Y ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));
        var rectangles = new List<ObstacleRectangle>();
        foreach (var first in ordered)
        {
            if (!remaining.Contains(first)) continue;
            if (rectangles.Count == maxRectangles) return null;
            int width = 1, depth = 1;
            while (remaining.Contains(first + new Vector2I(width, 0))) width++;
            while (true)
            {
                int x = 0;
                while (x < width && remaining.Contains(first + new Vector2I(x, depth))) x++;
                if (x < width) break;
                depth++;
            }
            for (int z = 0; z < depth; z++)
            for (int x = 0; x < width; x++)
                remaining.Remove(first + new Vector2I(x, z));
            rectangles.Add(new ObstacleRectangle(first.X, first.Y, width, depth) { GridSize = gridSize });
        }
        return rectangles.ToArray();
    }
}
