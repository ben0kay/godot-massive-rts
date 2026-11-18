// Generates connected terrain-following rock meshes from occupied cells.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal static class WorldPaintMeshBuilder
{
    internal const float Step = WorldPaint.CellSize * 0.5f;

    // =========================================================
    public static Mesh BuildRocks(HashSet<Vector2I> cells, TerrainHeightField terrain, float height, Color tint)
    {
        if (cells.Count == 0) return null;
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetMaterial(new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        });
        foreach (var cell in cells)
        {
            float x = cell.X * WorldPaint.CellSize, z = cell.Y * WorldPaint.CellSize;
            for (int row = 0; row < 2; row++)
            for (int col = 0; col < 2; col++)
            {
                float left = x + col * Step, near = z + row * Step;
                Vector3 a = Point(left, near), b = Point(left + Step, near);
                Vector3 c = Point(left, near + Step), d = Point(left + Step, near + Step);
                float variation = ((cell.X * 17 + cell.Y * 31 + col * 7 + row * 11) & 7) * 0.011f;
                Triangle(surface, a, b, c, tint.Lightened(variation));
                Triangle(surface, b, d, c, tint.Darkened(variation));
                if (row == 0 && !cells.Contains(cell + Vector2I.Up)) Skirt(a, b);
                if (row == 1 && !cells.Contains(cell + Vector2I.Down)) Skirt(d, c);
                if (col == 0 && !cells.Contains(cell + Vector2I.Left)) Skirt(c, a);
                if (col == 1 && !cells.Contains(cell + Vector2I.Right)) Skirt(b, d);
            }
        }
        return surface.Commit();

        Vector3 Point(float x, float z) => new(x, (terrain?.At(x, z) ?? 0f) + height, z);

        void Skirt(Vector3 a, Vector3 b)
        {
            var bottom = Vector3.Down * (height + 0.025f);
            Color face = tint.Darkened(a.X == b.X ? 0.32f : 0.23f);
            Triangle(surface, a, a + bottom, b, face);
            Triangle(surface, b, a + bottom, b + bottom, face.Darkened(0.08f));
        }
    }

    // =========================================================
    private static void Triangle(SurfaceTool surface, Vector3 a, Vector3 b, Vector3 c, Color color)
    {
        surface.SetColor(color);
        surface.AddVertex(a);
        surface.AddVertex(b);
        surface.AddVertex(c);
    }
}
