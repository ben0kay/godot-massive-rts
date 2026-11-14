// Shared unlit ground-shadow geometry for soldier batches and static obstacles.
using Godot;

namespace MassiveRts;

internal static class GroundShadowMeshes
{
    private static StandardMaterial3D Material() => new()
    {
        // About twice the previous opacity for stronger near-black contact shadows.
        AlbedoColor = new Color(0.005f, 0.008f, 0.012f, 0.52f),
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled
    };

    public static Mesh Soldier()
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetMaterial(Material());
        const int sides = 10;
        for (int i = 0; i < sides; i++)
        {
            float angle = Mathf.Tau * i / sides;
            float next = Mathf.Tau * (i + 1) / sides;
            surface.AddVertex(Vector3.Zero);
            surface.AddVertex(new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)));
            surface.AddVertex(new Vector3(Mathf.Cos(next), 0f, Mathf.Sin(next)));
        }
        return surface.Commit();
    }

    public static Mesh Obstacles(ObstacleField field, float height)
    {
        if (field == null || field.RectangleCount == 0) return null;
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetMaterial(Material());
        // Toward screen-right/down at the default 45-degree camera, as if light
        // falls from the upper-left. Keep the obstacle/tower shadow compact.
        Vector2 offset = new Vector2(0.55f, -0.20f) * height;
        foreach (ObstacleRectangle rect in field.Rectangles)
        {
            Vector2 inset = (rect.Max - rect.Min) * 0.05f;
            Vector2 min = rect.Min + inset + offset, max = rect.Max - inset + offset;
            Vector3 a = new(min.X, 0.02f, min.Y), b = new(max.X, 0.02f, min.Y);
            Vector3 c = new(max.X, 0.02f, max.Y), d = new(min.X, 0.02f, max.Y);
            surface.AddVertex(a); surface.AddVertex(b); surface.AddVertex(c);
            surface.AddVertex(a); surface.AddVertex(c); surface.AddVertex(d);
        }
        return surface.Commit();
    }
}
