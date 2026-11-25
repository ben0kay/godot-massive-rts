// Builds ground-seated faceted boulder clusters and their contact shadows from painted cells.
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
        surface.SetMaterial(new ShaderMaterial
        {
            Shader = GD.Load<Shader>("res://WORLD/WORLD_PAINT/RockSurface.gdshader")
        });

        using var shadows = new SurfaceTool();
        shadows.Begin(Mesh.PrimitiveType.Triangles);
        shadows.SetMaterial(GroundShadowMeshes.SilhouetteMaterial(0.30f));
        tint = tint.Lerp(new Color("#384256"), 0.65f);
        foreach (var cell in cells)
        {
            bool north = cells.Contains(cell + Vector2I.Up), south = cells.Contains(cell + Vector2I.Down);
            bool west = cells.Contains(cell + Vector2I.Left), east = cells.Contains(cell + Vector2I.Right);
            int neighbours = (north ? 1 : 0) + (south ? 1 : 0) + (east ? 1 : 0) + (west ? 1 : 0);
            // A full aligned 2×2 patch gets one broad boulder surrounded by smaller fragments.
            var anchor = new Vector2I(cell.X & ~1, cell.Y & ~1);
            bool broad = cells.Contains(anchor) && cells.Contains(anchor + Vector2I.Right) &&
                cells.Contains(anchor + Vector2I.Down) && cells.Contains(anchor + Vector2I.One);
            AddBoulder(surface, shadows, cell, terrain, broad ? height * 0.37f : height, tint, neighbours, broad: false);
            if (broad && cell == anchor) AddBoulder(surface, shadows, cell, terrain, height, tint, neighbours, broad: true);
        }
        return shadows.Commit(surface.Commit());
    }

    // =========================================================
    // Build uneven polygon rings with a broad cap and a chipped upper bevel.
    // Only the relative height responds to neighbours as new brush strokes join a pile.
    private static void AddBoulder(SurfaceTool surface, SurfaceTool shadows,
        Vector2I cell, TerrainHeightField terrain,
        float height, Color tint, int neighbours, bool broad)
    {
        uint seed = CellSeed(cell);
        float size = WorldPaint.CellSize;
        float x = (cell.X + Mathf.Lerp(0.38f, 0.62f, Next(ref seed))) * size;
        float z = (cell.Y + Mathf.Lerp(0.38f, 0.62f, Next(ref seed))) * size;
        if (broad) { x = (cell.X + 1f) * size; z = (cell.Y + 1f) * size; }
        float baseY = Ground(terrain, x, z);
        float heightScale = Mathf.Lerp(0.25f, 1.12f, Next(ref seed)) + neighbours * 0.02f;
        float columnHeight = height * heightScale;
        float radius = size * (broad ? Mathf.Lerp(0.69f, 0.93f, Next(ref seed)) : Mathf.Lerp(0.23f, 0.46f, Next(ref seed)));
        float stretch = Mathf.Lerp(0.59f, 1f, Next(ref seed));
        float inset = Mathf.Lerp(0.21f, 0.73f, Next(ref seed));
        float turn = Next(ref seed) * Mathf.Tau;
        int sides = 5 + Mathf.FloorToInt(3 * Next(ref seed));
        Color stone = tint.Lerp(tint.Lightened(0.22f), Next(ref seed)).Darkened(0.04f);
        var crown = new Vector3(x + radius * Mathf.Lerp(-0.18f, 0.18f, Next(ref seed)),
            baseY + columnHeight, z + radius * Mathf.Lerp(-0.18f, 0.18f, Next(ref seed)));
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
            shoulder[i] = new Vector3(x + dx * 0.98f, topY - columnHeight * 0.40f, z + dz * 0.98f);
            cap[i] = new Vector3(x + dx * inset, topY, z + dz * inset);
        }

        var projected = new Vector2[sides * 3 + 1];
        for (int i = 0; i < sides; i++)
        {
            projected[i] = Project(foot[i]);
            projected[sides + i] = Project(shoulder[i]);
            projected[sides * 2 + i] = Project(cap[i]);
        }
        projected[^1] = Project(crown);
        Vector2[] outline = Geometry2D.ConvexHull(projected);
        if (outline.Length > 1 && outline[0] == outline[^1])
            System.Array.Resize(ref outline, outline.Length - 1);
        if (outline.Length >= 3)
        {
            var center = Vector2.Zero;
            foreach (var point in outline) center += point;
            GroundShadowMeshes.AppendSilhouette(shadows, outline, center / outline.Length, terrain, groundOffset: 0.035f);
        }

        Vector2 Project(Vector3 vertex) => new Vector2(vertex.X, vertex.Z) +
            GroundShadowMeshes.SceneryOffset(Mathf.Max(0f, vertex.Y - baseY));

        for (int i = 0; i < sides; i++)
        {
            int j = (i + 1) % sides;
            Color wall = stone.Darkened(0.10f);
            Color bevel = stone.Darkened(0.04f);
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
        surface.SetNormal((c - a).Cross(b - a).Normalized());
        surface.SetColor(color);
        surface.AddVertex(a);
        surface.AddVertex(b);
        surface.AddVertex(c);
    }
}
