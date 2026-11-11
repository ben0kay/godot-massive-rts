// Builds one shared arrow mesh. Its point is at the origin and faces local -Z.
using Godot;

namespace MassiveRts;

public static class BasicArrowMesh
{
    public static Mesh Create()
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetMaterial(new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            VertexColorUseAsAlbedo = true
        });
        var shaft = new Color(0.69f, 0.47f, 0.27f);
        var head = new Color(0.89f, 0.91f, 0.95f);
        var fletching = new Color(0.78f, 0.72f, 0.61f);
        // Two crossed silhouettes keep the arrow readable from overhead and side views.
        foreach (Vector3 side in new[] { Vector3.Right, Vector3.Up })
        {
            Vector3 rear = Vector3.Back * 0.68f;
            Vector3 neck = Vector3.Back * 0.16f;
            Vector3 edge = side * 0.023f;
            Triangle(surface, rear - edge, rear + edge, neck + edge, shaft);
            Triangle(surface, rear - edge, neck + edge, neck - edge, shaft);
            Triangle(surface, Vector3.Zero, neck + side * 0.068f, neck - side * 0.068f, head);
            Triangle(surface, rear + side * 0.074f, rear - side * 0.074f, Vector3.Back * 0.48f, fletching);
        }
        return surface.Commit();
    }

    private static void Triangle(SurfaceTool surface, Vector3 a, Vector3 b, Vector3 c, Color color)
    {
        surface.SetColor(color); surface.AddVertex(a);
        surface.SetColor(color); surface.AddVertex(b);
        surface.SetColor(color); surface.AddVertex(c);
    }
}
