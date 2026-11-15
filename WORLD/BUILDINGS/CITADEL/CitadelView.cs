// Shares the Citadel model between the 3D editor and command gameplay.
using Godot;

namespace MassiveRts;

[Tool]
public partial class CitadelView : MeshInstance3D
{
    private static Mesh sharedMesh;

    public override void _Ready() => Mesh = sharedMesh ??= BuildMesh();

    private static Mesh BuildMesh()
    {
        using var surface = new SurfaceTool();
        surface.Begin(Godot.Mesh.PrimitiveType.Triangles);
        var material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            VertexColorUseAsAlbedo = true
        };
        surface.SetMaterial(material);
        var dark = new Color("#273644");
        var steel = new Color("#465b6d");
        void Layer(float y, float width, float height, Color color) =>
            FacetedSoldierMeshes.AppendBeveledRectangle(surface, new Vector3(0f, y, 0f),
                new Vector3(width, height, width), color);
        Layer(0.36f, 9.7f, 0.72f, dark);
        Layer(1.15f, 7.8f, 1.1f, steel);
        Layer(2.1f, 6.4f, 0.8f, new Color("#2679f5"));
        Layer(3.1f, 4.6f, 1.4f, dark);
        Layer(4.0f, 2.8f, 0.6f, new Color("#40e5ff"));
        return surface.Commit();
    }
}
