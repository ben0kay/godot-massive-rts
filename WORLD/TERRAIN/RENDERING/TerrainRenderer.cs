// Builds static terrain chunk meshes and flat ground around the sampled region.
using Godot;

namespace MassiveRts;

public static class TerrainRenderer
{
    private static readonly Color Plain = new(0.29f, 0.30f, 0.32f);
    private static readonly Color High = new(0.41f, 0.43f, 0.45f);

    public static Node3D Build(TerrainHeightField field, float rise, float mapHalfWidth, bool includeSurround = true)
    {
        var root = new Node3D { Name = "PlateauTerrain" };
        var material = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
        foreach (var chunk in field.Chunks)
        {
            using var surface = new SurfaceTool();
            surface.Begin(Mesh.PrimitiveType.Triangles);
            surface.SetMaterial(material);
            for (int row = 0; row < TerrainHeightField.ChunkCells; row++)
            for (int col = 0; col < TerrainHeightField.ChunkCells; col++)
                AddQuad(surface, field.MeshVertex(chunk, col, row), field.MeshVertex(chunk, col + 1, row),
                    field.MeshVertex(chunk, col, row + 1), field.MeshVertex(chunk, col + 1, row + 1), rise);
            root.AddChild(new MeshInstance3D { Mesh = surface.Commit() });
        }

        if (!includeSurround) return root;

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

    // =========================================================
    // Replace the original flat plane with a shared surface and leave holes for
    // occupied terrain chunks. The same ground shader spans every join.
    internal static Node3D BuildCommandGround(TerrainHeightField field, ShaderMaterial material, Rect2 ground)
    {
        var root = new Node3D { Name = "SculptedGround" };
        var rows = new System.Collections.Generic.SortedDictionary<int, System.Collections.Generic.List<int>>();
        foreach (var chunk in field.Chunks)
        {
            if (!rows.TryGetValue(chunk.Y, out var columns)) rows.Add(chunk.Y, columns = new());
            columns.Add(chunk.X);
            using var surface = new SurfaceTool();
            surface.Begin(Mesh.PrimitiveType.Triangles);
            const int cells = TerrainHeightField.ChunkCells;
            const int stride = cells + 1;
            for (int row = 0; row <= cells; row++)
            for (int col = 0; col <= cells; col++)
                surface.AddVertex(field.MeshVertex(chunk, col, row));
            for (int row = 0; row < cells; row++)
            for (int col = 0; col < cells; col++)
            {
                int a = row * stride + col;
                surface.AddIndex(a);
                surface.AddIndex(a + 1);
                surface.AddIndex(a + stride);
                surface.AddIndex(a + 1);
                surface.AddIndex(a + stride + 1);
                surface.AddIndex(a + stride);
            }
            AddMesh(surface.Commit());
        }

        using var flat = new SurfaceTool();
        flat.Begin(Mesh.PrimitiveType.Triangles);
        bool hasFlat = false;
        float ChunkEdge(int index) => index * TerrainHeightField.ChunkCells * TerrainHeightField.Spacing;
        float lastZ = ground.Position.Y;
        foreach (var (row, columns) in rows)
        {
            float near = ChunkEdge(row), far = ChunkEdge(row + 1);
            if (far <= ground.Position.Y || near >= ground.End.Y) continue;
            Flat(ground.Position.X, lastZ, ground.End.X, near);
            columns.Sort();
            float lastX = ground.Position.X;
            foreach (int col in columns)
            {
                Flat(lastX, near, ChunkEdge(col), far);
                lastX = Mathf.Max(lastX, ChunkEdge(col + 1));
            }
            Flat(lastX, near, ground.End.X, far);
            lastZ = far;
        }
        Flat(ground.Position.X, lastZ, ground.End.X, ground.End.Y);
        if (hasFlat) AddMesh(flat.Commit());
        return root;

        void Flat(float x0, float z0, float x1, float z1)
        {
            x0 = Mathf.Max(x0, ground.Position.X); z0 = Mathf.Max(z0, ground.Position.Y);
            x1 = Mathf.Min(x1, ground.End.X); z1 = Mathf.Min(z1, ground.End.Y);
            if (x0 >= x1 || z0 >= z1) return;
            AddFlat(flat, x0, z0, x1, z1, 1f);
            hasFlat = true;
        }

        void AddMesh(Mesh mesh) => root.AddChild(new MeshInstance3D
        {
            Mesh = mesh, MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
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
