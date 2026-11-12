// Builds static terrain chunk meshes and flat ground around the sampled region.
using Godot;

namespace MassiveRts;

public static class TerrainRenderer
{
    private static readonly Color Plain = new(0.075f, 0.085f, 0.10f);
    private static readonly Color High = new(0.16f, 0.20f, 0.23f);

    public static Node3D Build(TerrainHeightField field, float rise, float mapHalfWidth)
    {
        var root = new Node3D { Name = "PlateauTerrain" };
        var material = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
        for (int z = field.FirstChunk.Y; z <= field.LastChunk.Y; z++)
        for (int x = field.FirstChunk.X; x <= field.LastChunk.X; x++)
        {
            var chunk = new Vector2I(x, z);
            using var surface = new SurfaceTool();
            surface.Begin(Mesh.PrimitiveType.Triangles);
            surface.SetMaterial(material);
            for (int row = 0; row < TerrainHeightField.ChunkCells; row++)
            for (int col = 0; col < TerrainHeightField.ChunkCells; col++)
                AddQuad(surface, field.MeshVertex(chunk, col, row), field.MeshVertex(chunk, col + 1, row),
                    field.MeshVertex(chunk, col, row + 1), field.MeshVertex(chunk, col + 1, row + 1), rise);
            root.AddChild(new MeshInstance3D { Mesh = surface.Commit() });
        }

        using var surround = new SurfaceTool();
        surround.Begin(Mesh.PrimitiveType.Triangles);
        surround.SetMaterial(material);
        Vector2 min = field.Min, max = field.Max;
        AddFlat(surround, -mapHalfWidth, -mapHalfWidth, min.X, mapHalfWidth, rise);
        AddFlat(surround, max.X, -mapHalfWidth, mapHalfWidth, mapHalfWidth, rise);
        AddFlat(surround, min.X, -mapHalfWidth, max.X, min.Y, rise);
        AddFlat(surround, min.X, max.Y, max.X, mapHalfWidth, rise);
        root.AddChild(new MeshInstance3D { Mesh = surround.Commit() });
        return root;
    }

    private static void AddFlat(SurfaceTool surface, float x0, float z0, float x1, float z1, float rise) =>
        AddQuad(surface, new Vector3(x0, 0, z0), new Vector3(x1, 0, z0),
            new Vector3(x0, 0, z1), new Vector3(x1, 0, z1), rise);

    private static void AddQuad(SurfaceTool surface, Vector3 a, Vector3 b, Vector3 c, Vector3 d, float rise)
    {
        // Clockwise as seen from above. The b–c diagonal is also used by TerrainHeightField.At.
        AddVertex(surface, a, rise);
        AddVertex(surface, b, rise);
        AddVertex(surface, c, rise);
        AddVertex(surface, b, rise);
        AddVertex(surface, d, rise);
        AddVertex(surface, c, rise);
    }

    private static void AddVertex(SurfaceTool surface, Vector3 point, float rise)
    {
        surface.SetColor(Plain.Lerp(High, Mathf.Clamp(point.Y / rise, 0f, 1f)));
        surface.AddVertex(point);
    }
}
