// Builds one terrain-following ground-cover mesh with irregular exterior edges.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal static class ForestFloor
{
    private static readonly Vector2I[] Corners =
    {
        Vector2I.Zero, Vector2I.Right, Vector2I.One, Vector2I.Down
    };
    private static readonly Vector2I[] Neighbors =
    {
        Vector2I.Up, Vector2I.Right, Vector2I.Down, Vector2I.Left
    };

    internal static Mesh Build(HashSet<Vector2I> cells, TerrainHeightField terrain, float heightOffset = 0.018f)
    {
        if (cells.Count == 0) return null;
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        const float size = WorldPaint.CellSize;
        System.Span<bool> exposed = stackalloc bool[4];
        foreach (var cell in cells)
        {
            for (int edge = 0; edge < 4; edge++)
                exposed[edge] = !cells.Contains(cell + Neighbors[edge]);
            Vector2 center = ((Vector2)cell + Vector2.One * 0.5f) * size;
            for (int edge = 0; edge < 4; edge++)
            {
                int next = (edge + 1) % 4;
                Vector2 a = Corner(edge), b = Corner(next);
                Vector2 middle = (a + b) * 0.5f;
                if (exposed[edge])
                    middle += (Vector2)Neighbors[edge] * Variation(cell, edge) * size * 0.22f;
                float aFill = IsOuterCorner(edge) ? 0f : 1f;
                float bFill = IsOuterCorner(next) ? 0f : 1f;
                float edgeFill = exposed[edge] ? 0f : 1f;
                Vertex(center, 1f); Vertex(a, aFill); Vertex(middle, edgeFill);
                Vertex(center, 1f); Vertex(middle, edgeFill); Vertex(b, bFill);
            }
            bool IsOuterCorner(int index) =>
                !cells.Contains(cell + Neighbors[index]) && !cells.Contains(cell + Neighbors[(index + 3) % 4]);
            Vector2 Corner(int index)
            {
                Vector2 point = (Vector2)(cell + Corners[index]) * size;
                if (IsOuterCorner(index))
                    point += (point - center).Normalized() * Variation(cell, index + 4) * size * 0.16f;
                return point;
            }
        }
        return surface.Commit();

        void Vertex(Vector2 point, float fill)
        {
            surface.SetUV(new Vector2(fill, 0f));
            float y = (terrain?.At(point.X, point.Y) ?? 0f) + heightOffset;
            surface.AddVertex(new Vector3(point.X, y, point.Y));
        }
    }

    private static float Variation(Vector2I cell, int seed)
    {
        uint hash = unchecked((uint)cell.X * 0x9E3779B9u ^ (uint)cell.Y * 0x85EBCA6Bu ^ (uint)seed * 0xC2B2AE35u);
        hash = unchecked((hash ^ (hash >> 16)) * 0x7FEB352Du);
        return (hash & 65535u) / 32767.5f - 1f;
    }
}
