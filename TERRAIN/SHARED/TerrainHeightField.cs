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

    private readonly Dictionary<Vector2I, float[]> chunks = new();
    public Vector2I FirstChunk { get; }
    public Vector2I LastChunk { get; }
    public int ChunkCount => chunks.Count;
    public Vector2 Min => Vertex(FirstChunk, 0, 0);
    public Vector2 Max => Vertex(LastChunk, ChunkCells, ChunkCells);

    public TerrainHeightField(Vector2 min, Vector2 max, Func<Vector2, float> sample)
    {
        FirstChunk = ChunkAt(min);
        LastChunk = ChunkAt(max);
        for (int z = FirstChunk.Y; z <= LastChunk.Y; z++)
        for (int x = FirstChunk.X; x <= LastChunk.X; x++)
        {
            var key = new Vector2I(x, z);
            var heights = new float[Stride * Stride];
            for (int row = 0; row < Stride; row++)
            for (int col = 0; col < Stride; col++)
                heights[row * Stride + col] = sample(Vertex(key, col, row));
            chunks.Add(key, heights);
        }
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
        float x = NextBoundary(from.X, delta.X, out float xStep);
        float z = NextBoundary(from.Z, delta.Z, out float zStep);
        float diagonal = NextBoundary(from.X + from.Z, delta.X + delta.Z, out float diagonalStep);
        float previous = 0f;
        float above = from.Y - At(from.X, from.Z);
        if (above <= 0f) return 0f;

        while (previous < limit)
        {
            float next = Mathf.Min(limit, Mathf.Min(diagonal, Mathf.Min(x, z)));
            Vector3 point = from + delta * next;
            float nextAbove = point.Y - At(point.X, point.Z);
            if (nextAbove <= 0f) return Mathf.Lerp(previous, next, above / (above - nextAbove));
            if (x <= next) x += xStep;
            if (z <= next) z += zStep;
            if (diagonal <= next) diagonal += diagonalStep;
            previous = next;
            above = nextAbove;
        }

        return float.PositiveInfinity;
    }

    private static float NextBoundary(float position, float delta, out float step)
    {
        step = delta == 0f ? float.PositiveInfinity : Spacing / Mathf.Abs(delta);
        if (delta == 0f) return step;
        float scaled = position / Spacing;
        float boundary = delta > 0f ? Mathf.Floor(scaled) + 1f : Mathf.Ceil(scaled) - 1f;
        return Mathf.Max(0f, (boundary * Spacing - position) / delta);
    }

    public float At(float x, float z)
    {
        var chunk = ChunkAt(new Vector2(x, z));
        if (!chunks.TryGetValue(chunk, out float[] heights)) return 0f;
        float localX = Mathf.Clamp(x / Spacing - chunk.X * ChunkCells, 0f, ChunkCells);
        float localZ = Mathf.Clamp(z / Spacing - chunk.Y * ChunkCells, 0f, ChunkCells);
        int col = Mathf.Min((int)localX, ChunkCells - 1), row = Mathf.Min((int)localZ, ChunkCells - 1);
        float u = localX - col, v = localZ - row;
        int a = row * Stride + col;
        float near = heights[a], east = heights[a + 1], south = heights[a + Stride];
        if (u + v <= 1f) return near + (east - near) * u + (south - near) * v;
        float far = heights[a + Stride + 1];
        return far + (south - far) * (1f - u) + (east - far) * (1f - v);
    }
}
