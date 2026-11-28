// Draws engineered trench materials over the same triangles used by the terrain.
using Godot;

namespace MassiveRts;

internal static class TrenchLining
{
    internal static MeshInstance3D Build(TerrainTrench trench, TerrainHeightField terrain)
    {
        var material = new ShaderMaterial
        {
            Shader = GD.Load<Shader>("res://WORLD/TERRAIN/RENDERING/TrenchLining.gdshader")
        };
        material.SetShaderParameter("start", trench.Start);
        material.SetShaderParameter("axis", trench.Axis);
        material.SetShaderParameter("dimensions", new Vector4(trench.Length, trench.FloorWidth * .5f, trench.Ramp, trench.EntryWidth));
        using var mesh = new SurfaceTool();
        mesh.Begin(Mesh.PrimitiveType.Triangles);
        const float spacing = TerrainHeightField.Spacing;
        Rect2 bounds = trench.Bounds;
        int x0 = Mathf.FloorToInt(bounds.Position.X / spacing), x1 = Mathf.CeilToInt(bounds.End.X / spacing);
        int z0 = Mathf.FloorToInt(bounds.Position.Y / spacing), z1 = Mathf.CeilToInt(bounds.End.Y / spacing);
        for (int z = z0; z < z1; z++)
        for (int x = x0; x < x1; x++)
        {
            Vector2 center = new((x + .5f) * spacing, (z + .5f) * spacing);
            float along = (center - trench.Start).Dot(trench.Axis);
            float across = Mathf.Abs((center - trench.Start).Dot(trench.Across));
            if (across > trench.FloorWidth * .5f + 2f * spacing &&
                along > trench.EntryWidth + spacing && along < trench.Length - trench.EntryWidth - spacing) continue;
            var a = Point(x, z);
            var b = Point(x + 1, z);
            var c = Point(x, z + 1);
            var d = Point(x + 1, z + 1);
            mesh.AddVertex(a); mesh.AddVertex(b); mesh.AddVertex(c);
            mesh.AddVertex(b); mesh.AddVertex(d); mesh.AddVertex(c);
        }
        return new MeshInstance3D { Mesh = mesh.Commit(), MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };

        Vector3 Point(int x, int z) => new(x * spacing, terrain.At(x * spacing, z * spacing) + .035f, z * spacing);
    }
}
