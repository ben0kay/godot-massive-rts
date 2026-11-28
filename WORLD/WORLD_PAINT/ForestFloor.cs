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
        bool hasTriangles = false;
        foreach (var cell in cells)
        {
            for (int edge = 0; edge < 4; edge++)
                exposed[edge] = !cells.Contains(cell + Neighbors[edge]);
            Vector2 center = ((Vector2)cell + Vector2.One * 0.5f) * size;
            var centerSample = Sample(center);
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
                var aSample = Sample(a);
                var bSample = Sample(b);
                var midSample = Sample(middle);
                // Cliff caps replace the sampled height abruptly. Connecting a cap to
                // the base here would stretch the painted floor across the rock face.
                if (centerSample.Plateau == midSample.Plateau)
                {
                    if (centerSample.Plateau == aSample.Plateau)
                    {
                        Vertex(center, centerSample.Height, 1f);
                        Vertex(a, aSample.Height, aFill);
                        Vertex(middle, midSample.Height, edgeFill);
                    }
                    if (centerSample.Plateau == bSample.Plateau)
                    {
                        Vertex(center, centerSample.Height, 1f);
                        Vertex(middle, midSample.Height, edgeFill);
                        Vertex(b, bSample.Height, bFill);
                    }
                }
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
        return hasTriangles ? surface.Commit() : null;

        (float Height, int Plateau) Sample(Vector2 point) => terrain?.SampleGroundCover(point) ?? (0f, -1);

        void Vertex(Vector2 point, float height, float fill)
        {
            surface.SetUV(new Vector2(fill, 0f));
            surface.AddVertex(new Vector3(point.X, height + heightOffset, point.Y));
            hasTriangles = true;
        }
    }

    private static float Variation(Vector2I cell, int seed)
    {
        uint hash = unchecked((uint)cell.X * 0x9E3779B9u ^ (uint)cell.Y * 0x85EBCA6Bu ^ (uint)seed * 0xC2B2AE35u);
        hash = unchecked((hash ^ (hash >> 16)) * 0x7FEB352Du);
        return (hash & 65535u) / 32767.5f - 1f;
    }
}
