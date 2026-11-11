// Builds one static mesh for the wall layout using the soldier bevel and baked face tones.
using Godot;

namespace MassiveRts;

public static class ObstacleRenderer
{
    public static Mesh Build(ObstacleField field, float height, Color tint)
    {
        if (field == null) return null;
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetMaterial(new StandardMaterial3D
        {
            AlbedoColor = Colors.White,
            VertexColorUseAsAlbedo = true,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        });
        foreach (ObstacleRectangle rect in field.Rectangles)
        {
            Vector2 middle = (rect.Min + rect.Max) * 0.5f, size = rect.Max - rect.Min;
            FacetedSoldierMeshes.AppendBeveledRectangle(surface, new Vector3(middle.X, height * 0.5f, middle.Y),
                new Vector3(size.X, height, size.Y), tint);
        }
        return surface.Commit();
    }
}
