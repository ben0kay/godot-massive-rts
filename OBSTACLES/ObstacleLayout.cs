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
    [JsonIgnore] public Vector2 Min => new Vector2(X, Z) * DeploymentLayout.CellSize;
    [JsonIgnore] public Vector2 Max => new Vector2(X + Width, Z + Depth) * DeploymentLayout.CellSize;

    [JsonIgnore] public bool IsValid => Width > 0 && Depth > 0 &&
        X >= -DeploymentLayout.HalfWidth && Z >= -DeploymentLayout.HalfWidth &&
        (long)X + Width <= DeploymentLayout.HalfWidth && (long)Z + Depth <= DeploymentLayout.HalfWidth;
}

public sealed class ObstacleLayout
{
    public const int MaxCells = 65536;
    public const int MaxRectangles = 64;
    private readonly HashSet<Vector2I> cells;
    public ObstacleField Field { get; }

    public ObstacleLayout() : this(new(), Array.Empty<ObstacleRectangle>()) { }

    private ObstacleLayout(HashSet<Vector2I> cells, ObstacleRectangle[] rectangles)
    {
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
        return !changed || TryCompile(draft, out edited);
    }

    public static bool TryLoad(ObstacleRectangle[] rectangles, out ObstacleLayout layout)
    {
        layout = null;
        if (rectangles == null || rectangles.Length > MaxRectangles) return false;
        var draft = new HashSet<Vector2I>();
        foreach (ObstacleRectangle rect in rectangles)
        {
            if (!rect.IsValid || (long)rect.Width * rect.Depth > MaxCells - draft.Count) return false;
            for (int z = rect.Z; z < rect.Z + rect.Depth; z++)
            for (int x = rect.X; x < rect.X + rect.Width; x++)
                if (!draft.Add(new Vector2I(x, z))) return false;
        }
        return TryCompile(draft, out layout);
    }

    private static bool TryCompile(HashSet<Vector2I> cells, out ObstacleLayout layout)
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
            rectangles.Add(new ObstacleRectangle(first.X, first.Y, width, depth));
        }
        layout = new ObstacleLayout(cells, rectangles.ToArray());
        return true;
    }
}
