// Indexes recessed trench floors for neutral ground targeting and reveal checks.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal sealed class TrenchField
{
    private const float Bucket = 8f;
    private readonly Dictionary<Vector2I, List<TerrainTrench>> cells = new();
    private readonly TerrainHeightField terrain;
    private readonly List<TerrainTrench> trenches;
    private readonly Rect2 bounds;

    internal TrenchField(List<TerrainTrench> trenches, TerrainHeightField terrain)
    {
        this.terrain = terrain;
        this.trenches = trenches;
        bounds = trenches[0].Bounds;
        foreach (var trench in trenches) bounds = bounds.Merge(trench.Bounds);
        foreach (var trench in trenches)
        {
            var min = Cell(trench.Floor.Position);
            var max = Cell(trench.Floor.End);
            for (int z = min.Y; z <= max.Y; z++)
            for (int x = min.X; x <= max.X; x++)
            {
                var key = new Vector2I(x, z);
                if (!cells.TryGetValue(key, out var bucket)) cells.Add(key, bucket = new());
                bucket.Add(trench);
            }
        }
    }
    private static Vector2I Cell(Vector2 point) => new(Mathf.FloorToInt(point.X / Bucket), Mathf.FloorToInt(point.Y / Bucket));

    internal TerrainTrench Occupied(Vector3 position, bool includeRamps = false)
    {
        Vector2 point = ObstacleField.Xz(position);
        if (!cells.TryGetValue(Cell(point), out var bucket)) return null;
        foreach (var trench in bucket)
            if (includeRamps ? trench.Floor.HasPoint(point) && Mathf.Abs(position.Y - terrain.At(point.X, point.Y)) < 0.55f
                : trench.Conceals(point) && Mathf.Abs(position.Y - trench.FloorHeight) < 0.55f) return trench;
        return null;
    }

    internal bool MayCross(Vector3 from, Vector3 to)
    {
        Vector2 a = ObstacleField.Xz(from), b = ObstacleField.Xz(to);
        var range = new Rect2(a.Min(b), (a.Max(b) - a.Min(b))).Grow(0.01f);
        if (!bounds.Intersects(range)) return false;
        foreach (var trench in trenches)
            if (trench.Bounds.Intersects(range)) return true;
        return false;
    }

    internal bool BlocksFire(Vector3 from, Vector3 to) =>
        MayCross(from, to)
        && terrain.SurfaceHit(from, to - from, 1f) < 0.999f;
}
