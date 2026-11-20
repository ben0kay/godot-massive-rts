// Builds one connected rubble shelf and a set of faceted rock columns from painted cells.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal static class WorldPaintMeshBuilder
{
    internal const float Step = WorldPaint.CellSize * 0.5f;
    internal const float MaxRockHeightScale = 1.2f;

    // =========================================================
    public static Mesh BuildRocks(HashSet<Vector2I> cells, TerrainHeightField terrain, float height, Color tint)
    {
        if (cells.Count == 0) return null;
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetMaterial(new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        });

        foreach (var cell in cells)
        {
            Vector3 a = ShelfPoint(cell), b = ShelfPoint(cell + Vector2I.Right);
            Vector3 c = ShelfPoint(cell + Vector2I.Down), d = ShelfPoint(cell + Vector2I.One);
            Triangle(surface, a, b, c, tint.Darkened(0.38f));
            Triangle(surface, b, d, c, tint.Darkened(0.42f));

            // Neighbouring cells meet at the same vertices. Only exposed edges get walls.
            bool north = cells.Contains(cell + Vector2I.Up), south = cells.Contains(cell + Vector2I.Down);
            bool west = cells.Contains(cell + Vector2I.Left), east = cells.Contains(cell + Vector2I.Right);
            if (!north) ShelfEdge(a, b);
            if (!east) ShelfEdge(b, d);
            if (!south) ShelfEdge(d, c);
            if (!west) ShelfEdge(c, a);

            int neighbours = (north ? 1 : 0) + (south ? 1 : 0) + (east ? 1 : 0) + (west ? 1 : 0);
            AddColumn(surface, cell, terrain, height, tint, neighbours);
        }
        return surface.Commit();

        Vector3 ShelfPoint(Vector2I corner)
        {
            uint seed = CellSeed(corner);
            float x = corner.X * WorldPaint.CellSize, z = corner.Y * WorldPaint.CellSize;
            return new Vector3(x, Ground(terrain, x, z) + height * Mathf.Lerp(0.11f, 0.21f, Next(ref seed)), z);
        }

        void ShelfEdge(Vector3 a, Vector3 b)
        {
            Vector3 footA = new(a.X, Ground(terrain, a.X, a.Z) - 0.025f, a.Z);
            Vector3 footB = new(b.X, Ground(terrain, b.X, b.Z) - 0.025f, b.Z);
            Color shade = tint.Darkened(0.55f);
            Triangle(surface, a, footA, b, shade);
            Triangle(surface, b, footA, footB, shade);
        }
    }

    // =========================================================
    // Build uneven polygon rings with a broad cap and a chipped upper bevel.
    // Only the relative height responds to neighbours as new brush strokes join a pile.
    private static void AddColumn(SurfaceTool surface, Vector2I cell, TerrainHeightField terrain,
        float height, Color tint, int neighbours)
    {
        uint seed = CellSeed(cell);
        float size = WorldPaint.CellSize;
        float x = (cell.X + Mathf.Lerp(0.45f, 0.55f, Next(ref seed))) * size;
        float z = (cell.Y + Mathf.Lerp(0.45f, 0.55f, Next(ref seed))) * size;
        float baseY = Ground(terrain, (cell.X + 0.5f) * size, (cell.Y + 0.5f) * size);
        float heightScale = Mathf.Lerp(0.38f, 1.12f, Next(ref seed)) + neighbours * 0.02f;
        float columnHeight = height * heightScale;
        float radius = size * Mathf.Lerp(0.32f, 0.437f, Next(ref seed));
        float stretch = Mathf.Lerp(0.76f, 1f, Next(ref seed));
        float inset = Mathf.Lerp(0.72f, 0.91f, Next(ref seed));
        float turn = Next(ref seed) * Mathf.Tau;
        int sides = Next(ref seed) < 0.55f ? 6 : 7;
        Color stone = tint.Lerp(tint.Lightened(0.22f), Next(ref seed)).Darkened(0.04f);
        var crown = new Vector3(x, baseY + columnHeight, z);

        System.Span<Vector3> foot = stackalloc Vector3[7];
        System.Span<Vector3> shoulder = stackalloc Vector3[7];
        System.Span<Vector3> cap = stackalloc Vector3[7];
        for (int i = 0; i < sides; i++)
        {
            float angle = turn + (i + Mathf.Lerp(-0.17f, 0.17f, Next(ref seed))) * Mathf.Tau / sides;
            float reach = radius * Mathf.Lerp(0.79f, 1.02f, Next(ref seed));
            float dx = Mathf.Cos(angle) * reach, dz = Mathf.Sin(angle) * reach * stretch;
            float fx = x + dx, fz = z + dz;
            foot[i] = new Vector3(fx, Ground(terrain, fx, fz) - 0.025f, fz);
            float topY = crown.Y - columnHeight * Mathf.Lerp(0.01f, 0.12f, Next(ref seed));
            shoulder[i] = new Vector3(x + dx * 0.98f, topY - columnHeight * 0.14f, z + dz * 0.98f);
            cap[i] = new Vector3(x + dx * inset, topY, z + dz * inset);
        }

        for (int i = 0; i < sides; i++)
        {
            int j = (i + 1) % sides;
            Vector3 edge = shoulder[j] - shoulder[i];
            float facing = (edge.Z - edge.X) / Mathf.Max(0.001f, new Vector2(edge.X, edge.Z).Length());
            Color wall = stone.Darkened(Mathf.Clamp(0.32f - facing * 0.11f, 0f, 1f));
            Color bevel = stone.Darkened(Mathf.Clamp(0.13f - facing * 0.07f, 0f, 1f));
            Triangle(surface, shoulder[i], foot[i], shoulder[j], wall);
            Triangle(surface, shoulder[j], foot[i], foot[j], wall);
            Triangle(surface, cap[i], shoulder[i], cap[j], bevel);
            Triangle(surface, cap[j], shoulder[i], shoulder[j], bevel);
            Triangle(surface, crown, cap[i], cap[j], stone.Lightened(Next(ref seed) * 0.10f));
        }
    }

    private static float Ground(TerrainHeightField terrain, float x, float z) => terrain?.At(x, z) ?? 0f;

    private static uint CellSeed(Vector2I cell) =>
        unchecked(((uint)cell.X * 0x9e3779b1u ^ (uint)cell.Y * 0x85ebca77u) | 1u);

    private static float Next(ref uint seed)
    {
        seed ^= seed << 13;
        seed ^= seed >> 17;
        seed ^= seed << 5;
        return (seed >> 8) * (1f / 16777216f);
    }

    private static void Triangle(SurfaceTool surface, Vector3 a, Vector3 b, Vector3 c, Color color)
    {
        surface.SetColor(color);
        surface.AddVertex(a);
        surface.AddVertex(b);
        surface.AddVertex(c);
    }
}
