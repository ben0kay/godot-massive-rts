// Immutable terrain samples. Grounding uses the same diagonal as the rendered triangles.
using System;
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

public sealed class TerrainHeightField
{
    public const int ChunkCells = 32;
    public const float Spacing = 2.1f;
    private const int Stride = ChunkCells + 1;
    private const float ChunkSize = ChunkCells * Spacing;

    internal readonly record struct Plateau(Vector2[] Outline, Rect2 Bounds, float Height);
    private Plateau[] plateaus = Array.Empty<Plateau>();

    internal void SetPlateaus(Plateau[] value) => plateaus = value;

    private bool TryPlateau(Vector2 point, out float height)
    {
        foreach (var plateau in plateaus)
            if (plateau.Bounds.HasPoint(point) && InsideOutline(point, plateau.Outline))
            {
                height = plateau.Height;
                return true;
            }
        height = 0f;
        return false;
    }

    internal bool TryPlateauRay(Vector3 origin, Vector3 direction, out Vector3 hit)
    {
        hit = default;
        if (direction.Y >= -0.0001f) return false;
        float nearest = float.PositiveInfinity;
        foreach (var plateau in plateaus)
        {
            float distance = (plateau.Height - origin.Y) / direction.Y;
            if (distance < 0f || distance >= nearest) continue;
            var point = origin + direction * distance;
            var xz = ObstacleField.Xz(point);
            if (!plateau.Bounds.HasPoint(xz) || !InsideOutline(xz, plateau.Outline)) continue;
            nearest = distance;
            hit = point;
        }
        return float.IsFinite(nearest);
    }

    private static bool InsideOutline(Vector2 point, Vector2[] outline)
    {
        bool inside = false;
        Vector2 previous = outline[^1];
        foreach (var current in outline)
        {
            if ((current.Y > point.Y) != (previous.Y > point.Y) &&
                point.X < (previous.X - current.X) * (point.Y - current.Y) / (previous.Y - current.Y) + current.X)
                inside = !inside;
            previous = current;
        }
        return inside;
    }

    private readonly Dictionary<Vector2I, float[]> chunks = new();
    public Vector2I FirstChunk { get; }
    public Vector2I LastChunk { get; }
    public int ChunkCount => chunks.Count;
    public Vector2 Min => Vertex(FirstChunk, 0, 0);
    public Vector2 Max => Vertex(LastChunk, ChunkCells, ChunkCells);

    public IEnumerable<Vector2I> Chunks => chunks.Keys;

    public TerrainHeightField(Vector2 min, Vector2 max, Func<Vector2, float> sample)
        : this(new[] { new Rect2(min, max - min) }, sample) { }

    public TerrainHeightField(IReadOnlyList<Rect2> regions, Func<Vector2, float> sample)
    {
        if (regions.Count == 0) throw new ArgumentException("Terrain requires at least one region.");
        var first = ChunkAt(regions[0].Position);
        var last = ChunkAt(regions[0].End);
        foreach (var region in regions)
        {
            Vector2I start = ChunkAt(region.Position), end = ChunkAt(region.End);
            first = new Vector2I(Math.Min(first.X, start.X), Math.Min(first.Y, start.Y));
            last = new Vector2I(Math.Max(last.X, end.X), Math.Max(last.Y, end.Y));
            for (int z = start.Y; z <= end.Y; z++)
            for (int x = start.X; x <= end.X; x++)
            {
                var key = new Vector2I(x, z);
                if (chunks.ContainsKey(key)) continue;
                var heights = new float[Stride * Stride];
                for (int row = 0; row < Stride; row++)
                for (int col = 0; col < Stride; col++)
                    heights[row * Stride + col] = sample(Vertex(key, col, row));
                chunks.Add(key, heights);
            }
        }
        FirstChunk = first;
        LastChunk = last;
    }

    private static Vector2I ChunkAt(Vector2 point) =>
        new(Mathf.FloorToInt(point.X / ChunkSize), Mathf.FloorToInt(point.Y / ChunkSize));

    private static Vector2 Vertex(Vector2I chunk, int x, int z) =>
        new((chunk.X * ChunkCells + x) * Spacing, (chunk.Y * ChunkCells + z) * Spacing);

    public Vector3 MeshVertex(Vector2I chunk, int x, int z)
    {
        Vector2 point = Vertex(chunk, x, z);
        return new Vector3(point.X, chunks[chunk][z * Stride + x], point.Y);
    }

    // =========================================================
    // Along a segment the terrain is linear until a cell edge or triangle diagonal.
    public float SurfaceHit(Vector3 from, Vector3 delta, float limit)
    {
        float capHit = TryPlateauRay(from, delta, out var plateauHit)
            ? (plateauHit.Y - from.Y) / delta.Y : float.PositiveInfinity;
        limit = Mathf.Min(limit, capHit);
        float x = NextBoundary(from.X, delta.X, out float xStep);
        float z = NextBoundary(from.Z, delta.Z, out float zStep);
        float diagonal = NextBoundary(from.X + from.Z, delta.X + delta.Z, out float diagonalStep);
        float previous = 0f;
        // The rim uses obstacle collision. Avoid interpolating across the height jump at its edge.
        if (plateaus.Length != 0 && TryPlateau(ObstacleField.Xz(from), out float startHeight) && from.Y <= startHeight)
            return 0f;
        float above = from.Y - BaseHeight(from.X, from.Z);
        if (above <= 0f) return 0f;

        while (previous < limit)
        {
            float next = Mathf.Min(limit, Mathf.Min(diagonal, Mathf.Min(x, z)));
            Vector3 point = from + delta * next;
            float nextAbove = point.Y - BaseHeight(point.X, point.Z);
            if (nextAbove <= 0f) return Mathf.Lerp(previous, next, above / (above - nextAbove));
            if (x <= next) x += xStep;
            if (z <= next) z += zStep;
            if (diagonal <= next) diagonal += diagonalStep;
            previous = next;
            above = nextAbove;
        }

        return capHit <= limit ? capHit : float.PositiveInfinity;
    }

    private static float NextBoundary(float position, float delta, out float step)
    {
        step = delta == 0f ? float.PositiveInfinity : Spacing / Mathf.Abs(delta);
        if (delta == 0f) return step;
        float scaled = position / Spacing;
        float boundary = delta > 0f ? Mathf.Floor(scaled) + 1f : Mathf.Ceil(scaled) - 1f;
        return Mathf.Max(0f, (boundary * Spacing - position) / delta);
    }

    public float FootprintHeight(Vector2 min, Vector2 max, out float relief)
    {
        int columns = Mathf.Max(1, Mathf.CeilToInt((max.X - min.X) / Spacing));
        int rows = Mathf.Max(1, Mathf.CeilToInt((max.Y - min.Y) / Spacing));
        float low = float.PositiveInfinity, high = float.NegativeInfinity;
        for (int row = 0; row <= rows; row++)
        for (int column = 0; column <= columns; column++)
        {
            float height = At(Mathf.Lerp(min.X, max.X, column / (float)columns),
                Mathf.Lerp(min.Y, max.Y, row / (float)rows));
            low = Mathf.Min(low, height);
            high = Mathf.Max(high, height);
        }
        relief = high - low;
        return high;
    }

    // Use the gradient of the same terrain triangle used for grounding.
    internal float Grade(Vector3 position, Vector2 direction)
    {
        if (plateaus.Length != 0 && TryPlateau(ObstacleField.Xz(position), out _)) return 0f;
        if (!TryCell(position.X, position.Z, out var heights, out int a, out float u, out float v)) return 0f;
        float near = heights[a], east = heights[a + 1], south = heights[a + Stride];
        if (u + v <= 1f)
            return ((east - near) * direction.X + (south - near) * direction.Y) / Spacing;
        float far = heights[a + Stride + 1];
        return ((far - south) * direction.X + (far - east) * direction.Y) / Spacing;
    }

    public float At(float x, float z)
    {
        if (plateaus.Length != 0 && TryPlateau(new Vector2(x, z), out float plateauHeight)) return plateauHeight;
        return BaseHeight(x, z);
    }

    private float BaseHeight(float x, float z)
    {
        if (!TryCell(x, z, out var heights, out int a, out float u, out float v)) return 0f;
        float near = heights[a], east = heights[a + 1], south = heights[a + Stride];
        if (u + v <= 1f) return near + (east - near) * u + (south - near) * v;
        float far = heights[a + Stride + 1];
        return far + (south - far) * (1f - u) + (east - far) * (1f - v);
    }

    private bool TryCell(float x, float z, out float[] heights, out int a, out float u, out float v)
    {
        var chunk = ChunkAt(new Vector2(x, z));
        a = 0;
        u = v = 0f;
        if (!chunks.TryGetValue(chunk, out heights)) return false;
        float localX = Mathf.Clamp(x / Spacing - chunk.X * ChunkCells, 0f, ChunkCells);
        float localZ = Mathf.Clamp(z / Spacing - chunk.Y * ChunkCells, 0f, ChunkCells);
        int col = Mathf.Min((int)localX, ChunkCells - 1), row = Mathf.Min((int)localZ, ChunkCells - 1);
        u = localX - col;
        v = localZ - row;
        a = row * Stride + col;
        return true;
    }
}
