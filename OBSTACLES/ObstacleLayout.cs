// Compiles bounded, non-overlapping painter cells into deterministic rectangles.
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Godot;

namespace MassiveRts;

public readonly record struct ObstacleRectangle(
    [property: JsonRequired] int X, [property: JsonRequired] int Z,
    [property: JsonRequired] int Width, [property: JsonRequired] int Depth)
{
    [JsonIgnore] public float GridSize { get; init; } = DeploymentLayout.CellSize;
    [JsonIgnore] private int HalfWidth => DeploymentLayout.CellHalfWidth(GridSize);
    [JsonIgnore] public Vector2 Min => new Vector2(X, Z) * GridSize;
    [JsonIgnore] public Vector2 Max => new Vector2(X + Width, Z + Depth) * GridSize;

    [JsonIgnore] public bool IsValid => Width > 0 && Depth > 0 &&
        X >= -HalfWidth && Z >= -HalfWidth &&
        (long)X + Width <= HalfWidth && (long)Z + Depth <= HalfWidth;
}

public sealed class ObstacleLayout
{
    public const int MaxCells = 65536;
    public const int MaxRectangles = 64;
    private readonly HashSet<Vector2I> cells;
    private readonly float gridSize;
    public ObstacleField Field { get; }

    public ObstacleLayout(float gridSize = DeploymentLayout.CellSize) : this(new(), Array.Empty<ObstacleRectangle>(), gridSize) { }

    private ObstacleLayout(HashSet<Vector2I> cells, ObstacleRectangle[] rectangles, float gridSize)
    {
        this.gridSize = gridSize;
        this.cells = cells;
        Field = rectangles.Length == 0 ? null : new ObstacleField(rectangles);
    }

    public bool TryEdit(ObstacleRectangle area, bool erase, out ObstacleLayout edited)
    {
        edited = this;
        if (!area.IsValid || (long)area.Width * area.Depth > MaxCells) return false;
        var draft = new HashSet<Vector2I>(cells);
        bool changed = false;
        for (int z = area.Z; z < area.Z + area.Depth; z++)
        for (int x = area.X; x < area.X + area.Width; x++)
        {
            var cell = new Vector2I(x, z);
            changed |= erase ? draft.Remove(cell) : draft.Add(cell);
            if (draft.Count > MaxCells) return false;
        }
        return !changed || TryCompile(draft, gridSize, out edited);
    }

    public static bool TryLoad(ObstacleRectangle[] rectangles, float gridSize, out ObstacleLayout layout)
    {
        layout = null;
        if (rectangles == null || rectangles.Length > MaxRectangles) return false;
        var draft = new HashSet<Vector2I>();
        foreach (ObstacleRectangle saved in rectangles)
        {
            var rect = saved with { GridSize = gridSize };
            if (!rect.IsValid || (long)rect.Width * rect.Depth > MaxCells - draft.Count) return false;
            for (int z = rect.Z; z < rect.Z + rect.Depth; z++)
            for (int x = rect.X; x < rect.X + rect.Width; x++)
                if (!draft.Add(new Vector2I(x, z))) return false;
        }
        return TryCompile(draft, gridSize, out layout);
    }

    private static bool TryCompile(HashSet<Vector2I> cells, float gridSize, out ObstacleLayout layout)
    {
        layout = null;
        var remaining = new HashSet<Vector2I>(cells);
        var ordered = new List<Vector2I>(cells);
        ordered.Sort((a, b) => a.Y == b.Y ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));
        var rectangles = new List<ObstacleRectangle>();
        foreach (var first in ordered)
        {
            if (!remaining.Contains(first)) continue;
            if (rectangles.Count == MaxRectangles) return false;
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
        layout = new ObstacleLayout(cells, rectangles.ToArray(), gridSize);
        return true;
    }
}
