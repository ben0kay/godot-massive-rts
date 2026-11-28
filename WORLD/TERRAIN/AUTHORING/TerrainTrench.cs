// Saves a straight trench and describes its floor, ramps and movement-only blockers.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

[Tool]
public partial class TerrainTrench : Node3D
{
    internal const int MaxPerMap = 16;
    internal const float SideWidth = TerrainHeightField.Spacing;
    [Export] public Vector2 Start { get; set; }
    [Export] public Vector2 End { get; set; }
    [Export] public float Width { get; set; } = 8f;
    [Export] public float Depth { get; set; } = 2.4f;
    [Export] public float Ramp { get; set; } = 8f;

    internal bool AlongX => Mathf.Abs(End.X - Start.X) >= Mathf.Abs(End.Y - Start.Y);
    internal float Length => Start.DistanceTo(End);
    internal Rect2 Floor => AlongX
        ? new Rect2(Start.Min(End) - new Vector2(0f, Width * 0.5f), new Vector2(Length, Width))
        : new Rect2(Start.Min(End) - new Vector2(Width * 0.5f, 0f), new Vector2(Width, Length));
    internal Rect2 Bounds => Floor.Grow(SideWidth);
    internal float FloorHeight { get; set; }

    internal bool Fits(float half)
    {
        if (!float.IsFinite(Width) || !float.IsFinite(Depth) || !float.IsFinite(Ramp) ||
            !float.IsFinite(Start.X) || !float.IsFinite(Start.Y) || !float.IsFinite(End.X) || !float.IsFinite(End.Y) ||
            Width is < 6f or > 24f || Depth is < 0.5f or > 8f || Ramp is < 4f or > 32f ||
            Length < 2f * Ramp + TerrainHeightField.Spacing * 2f ||
            (AlongX ? Start.Y != End.Y : Start.X != End.X)) return false;
        return Bounds.Position.X >= -half && Bounds.Position.Y >= -half &&
            Bounds.End.X <= half && Bounds.End.Y <= half;
    }

    internal float Blend(Vector2 point)
    {
        float along = AlongX ? point.X : point.Y;
        var floor = Floor;
        float endDistance = AlongX ? Mathf.Min(along - floor.Position.X, floor.End.X - along)
            : Mathf.Min(along - floor.Position.Y, floor.End.Y - along);
        if (endDistance <= 0f) return 0f;
        float side = AlongX ? Mathf.Abs(point.Y - Start.Y) : Mathf.Abs(point.X - Start.X);
        return Mathf.SmoothStep(0f, Ramp, endDistance) *
            (1f - Mathf.SmoothStep(Width * 0.5f, Width * 0.5f + SideWidth, side));
    }

    internal bool Conceals(Vector2 point)
    {
        if (!Floor.HasPoint(point)) return false;
        float along = AlongX ? Mathf.Abs(point.X - Start.X) : Mathf.Abs(point.Y - Start.Y);
        return along >= Ramp && along <= Length - Ramp;
    }

    internal void AddMovementBlockers(List<ObstacleRectangle> result)
    {
        var floor = Floor;
        Vector2 thickness = AlongX ? new Vector2(0f, SideWidth) : new Vector2(SideWidth, 0f);
        Vector2 length = AlongX ? new Vector2(floor.Size.X, 0f) : new Vector2(0f, floor.Size.Y);
        result.Add(new ObstacleRectangle { WorldBounds = new Rect2(floor.Position - thickness, length + thickness) });
        result.Add(new ObstacleRectangle { WorldBounds = new Rect2(floor.End - length, length + thickness) });
        result.Add(new ObstacleRectangle { WorldBounds = floor, MinimumBlockingTier = 1.01f, AllowInteriorEscape = true });
    }
}
