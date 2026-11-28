// Saves a straight trench and describes its floor, ramps and movement-only blockers.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

[Tool]
public partial class TerrainTrench : Node3D
{
    internal const int MaxPerMap = 16;
    internal const float SideWidth = 1.65f;
    [Export] public Vector2 Start { get; set; }
    [Export] public Vector2 End { get; set; }
    [Export] public float Width { get; set; } = 8f;
    [Export] public float Depth { get; set; } = 2.4f;
    [Export] public float Ramp { get; set; } = 8f;

    internal bool AlongX => Mathf.Abs(End.X - Start.X) >= Mathf.Abs(End.Y - Start.Y);
    internal float Length => Start.DistanceTo(End);
    // Width is stored in the original authoring units for existing maps.
    internal float FloorWidth => Width * .5f;
    internal Vector2 Axis => (End - Start).Normalized();
    internal Vector2 Across => new(-Axis.Y, Axis.X);
    internal float EntryWidth => Mathf.Min(6f, Mathf.Max(4.2f, FloorWidth));
    internal Vector2 RampInner(bool far) =>
        (far ? End - Axis * (EntryWidth * .5f) : Start + Axis * (EntryWidth * .5f)) +
        Across * (FloorWidth * .5f * (far ? -1f : 1f));
    internal Vector2 RampOuter(bool far) => RampInner(far) + Across * (Ramp * (far ? -1f : 1f));
    internal Rect2 Floor => Between(Start - Across * (FloorWidth * .5f), End + Across * (FloorWidth * .5f));
    internal Rect2 RampBounds(bool far) =>
        Between(RampInner(far) - Axis * (EntryWidth * .5f), RampOuter(far) + Axis * (EntryWidth * .5f));
    internal Rect2 Bounds => Floor.Merge(RampBounds(false)).Merge(RampBounds(true)).Grow(SideWidth);

    private static Rect2 Between(Vector2 a, Vector2 b) => new(a.Min(b), a.Max(b) - a.Min(b));
    internal float FloorHeight { get; set; }

    internal bool Fits(float half)
    {
        if (!float.IsFinite(Width) || !float.IsFinite(Depth) || !float.IsFinite(Ramp) ||
            !float.IsFinite(Start.X) || !float.IsFinite(Start.Y) || !float.IsFinite(End.X) || !float.IsFinite(End.Y) ||
            Width is < 6f or > 24f || Depth is < 0.5f or > 8f || Ramp is < 4f or > 32f ||
            Length < 2f * EntryWidth + TerrainHeightField.Spacing ||
            (AlongX ? Start.Y != End.Y : Start.X != End.X)) return false;
        return Bounds.Position.X >= -half && Bounds.Position.Y >= -half &&
            Bounds.End.X <= half && Bounds.End.Y <= half;
    }

    internal float Blend(Vector2 point)
    {
        Vector2 offset = point - Start;
        float along = offset.Dot(Axis), side = offset.Dot(Across);
        float end = Mathf.Max(-along, along - Length);
        float edge = Mathf.Abs(side) - FloorWidth * .5f;
        float floor = (1f - Mathf.SmoothStep(0f, SideWidth, end)) *
            (1f - Mathf.SmoothStep(0f, SideWidth, edge));
        return Mathf.Max(floor, Mathf.Max(RampBlend(point, false), RampBlend(point, true)));
    }

    private float RampBlend(Vector2 point, bool far)
    {
        Vector2 offset = point - RampInner(far);
        float distance = offset.Dot(Across) * (far ? -1f : 1f);
        if (distance < 0f || distance >= Ramp) return 0f;
        float shoulder = Mathf.Abs(offset.Dot(Axis)) - EntryWidth * .5f;
        return (1f - Mathf.SmoothStep(0f, Ramp, distance)) *
            (1f - Mathf.SmoothStep(0f, SideWidth, shoulder));
    }

    internal bool IsRamp(Vector2 point) => RampBounds(false).HasPoint(point) || RampBounds(true).HasPoint(point);
    internal bool Conceals(Vector2 point) => Floor.HasPoint(point);

    internal void AddMovementBlockers(List<ObstacleRectangle> result)
    {
        Vector2 half = Across * (FloorWidth * .5f);
        Vector2 wall = Across * SideWidth;
        Vector2 end = Axis * SideWidth;
        // The two side gaps alternate. Both short ends are closed.
        Wall(Start + half + Axis * EntryWidth, End + half + wall);
        Wall(Start - half - wall, End - half - Axis * EntryWidth);
        Wall(Start - half - end, Start + half);
        Wall(End - half, End + half + end);
        Heavy(Floor);
        Heavy(RampBounds(false));
        Heavy(RampBounds(true));

        void Wall(Vector2 a, Vector2 b) => result.Add(new ObstacleRectangle { WorldBounds = Between(a, b) });
        void Heavy(Rect2 area) => result.Add(new ObstacleRectangle
            { WorldBounds = area, MinimumBlockingTier = 1.01f, AllowInteriorEscape = true });
    }
}
