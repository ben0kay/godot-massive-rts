// Finds the enclosed faces formed by axis-aligned authored industrial walls.
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace MassiveRts;

internal static class IndustrialWallLoops
{
    internal static List<ObstacleRectangle> Fill(IReadOnlyList<ObstacleRectangle> walls)
    {
        var fills = new List<ObstacleRectangle>();
        if (walls.Count < 4) return fills;
        float[] xs = Coordinates(walls, x: true), zs = Coordinates(walls, x: false);
        int width = xs.Length - 1, depth = zs.Length - 1;
        var cells = new int[width, depth];
        for (int i = 0; i < walls.Count; i++)
        {
            var wall = walls[i];
            int right = System.Array.BinarySearch(xs, wall.Max.X), bottom = System.Array.BinarySearch(zs, wall.Max.Y);
            for (int z = System.Array.BinarySearch(zs, wall.Min.Y); z < bottom; z++)
            for (int x = System.Array.BinarySearch(xs, wall.Min.X); x < right; x++)
            {
                int prior = cells[x, z];
                if (prior == 0 || walls[prior - 1].AirborneHeightRange.Value.Y < wall.AirborneHeightRange.Value.Y)
                    cells[x, z] = i + 1;
            }
        }

        var frontier = new Queue<Vector2I>();
        var region = new List<Vector2I>();
        for (int z = 0; z < depth; z++)
        for (int x = 0; x < width; x++)
        {
            if (cells[x, z] != 0) continue;
            bool open = false;
            float floor = float.PositiveInfinity, roof = float.NegativeInfinity;
            region.Clear();
            Visit(x, z);
            while (frontier.TryDequeue(out var cell))
            {
                region.Add(cell);
                Visit(cell.X - 1, cell.Y);
                Visit(cell.X + 1, cell.Y);
                Visit(cell.X, cell.Y - 1);
                Visit(cell.X, cell.Y + 1);
            }
            if (open) continue;
            foreach (var rectangle in ObstacleCellCompiler.Merge(new HashSet<Vector2I>(region), 1f, region.Count))
            {
                var min = new Vector2(xs[rectangle.X], zs[rectangle.Z]);
                var max = new Vector2(xs[rectangle.X + rectangle.Width], zs[rectangle.Z + rectangle.Depth]);
                fills.Add(new ObstacleRectangle(0, 0, 0, 0)
                    { WorldBounds = new Rect2(min, max - min), AirborneHeightRange = new Vector2(floor, roof) });
            }

            void Visit(int col, int row)
            {
                if (col < 0 || row < 0 || col >= width || row >= depth) { open = true; return; }
                int state = cells[col, row];
                if (state < 0) return;
                if (state > 0)
                {
                    var height = walls[state - 1].AirborneHeightRange.Value;
                    floor = Mathf.Min(floor, height.X);
                    roof = Mathf.Max(roof, height.Y);
                    return;
                }
                cells[col, row] = -1;
                frontier.Enqueue(new Vector2I(col, row));
            }
        }
        return fills;
    }

    private static float[] Coordinates(IReadOnlyList<ObstacleRectangle> walls, bool x) =>
        walls.SelectMany(wall => x ? new[] { wall.Min.X, wall.Max.X } : new[] { wall.Min.Y, wall.Max.Y })
            .Distinct().OrderBy(value => value).ToArray();
}
