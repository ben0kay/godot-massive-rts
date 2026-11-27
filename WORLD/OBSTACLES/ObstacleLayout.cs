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
    // Authored scene meshes can supply exact world bounds. Painted saves use cells.
    [JsonIgnore] public Vector2? AirborneHeightRange { get; init; }
    [JsonIgnore] internal float WreckTier { get; init; }
    [JsonIgnore] internal bool AllowInteriorEscape { get; init; }
    [JsonIgnore] public Rect2? WorldBounds { get; init; }
    [JsonIgnore] public Vector2 Min => WorldBounds?.Position ?? new Vector2(X, Z) * GridSize;
    [JsonIgnore] public Vector2 Max => WorldBounds?.End ?? new Vector2(X + Width, Z + Depth) * GridSize;

    [JsonIgnore] public bool IsValid => WorldBounds is Rect2 bounds
        ? bounds.Position.IsFinite() && bounds.End.IsFinite() && bounds.Size.X > 0f && bounds.Size.Y > 0f
        : Width > 0 && Depth > 0 &&
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
        var rectangles = ObstacleCellCompiler.Merge(cells, gridSize, MaxRectangles);
        if (rectangles == null) return false;
        layout = new ObstacleLayout(cells, rectangles, gridSize);
        return true;
    }
}
