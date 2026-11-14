// Batches short-lived expanding impact rings in one MultiMesh.
using Godot;

namespace MassiveRts;

public partial class ShockwaveRenderer : MultiMeshInstance3D
{
    private const int Capacity = 256;
    private const int Segments = 64;
    private const float Duration = 0.35f;
    private readonly record struct Ring(Vector3 Origin, float Radius, float Age);
    private readonly Ring[] rings = new Ring[Capacity];
    private int count;
    private bool dirty;

    public override void _Ready()
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetMaterial(new StandardMaterial3D
        {
            AlbedoColor = Colors.White,
            VertexColorUseAsAlbedo = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        });
        for (int i = 0; i < Segments; i++)
        {
            Vector3 a = Direction(i), b = Direction(i + 1);
            surface.SetColor(Colors.White);
            surface.AddVertex(a);
            surface.AddVertex(b);
            surface.AddVertex(a * 0.985f);
            surface.AddVertex(a * 0.985f);
            surface.AddVertex(b);
            surface.AddVertex(b * 0.985f);
        }
        Multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = surface.Commit(),
            InstanceCount = Capacity,
            VisibleInstanceCount = 0
        };
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
    }

    private static Vector3 Direction(int index)
    {
        float angle = index * Mathf.Tau / Segments;
        return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
    }

    public void Emit(Vector3 origin, float radius)
    {
        if (count == Capacity) return;
        rings[count++] = new Ring(origin + Vector3.Up * 0.035f, radius, 0f);
        dirty = true;
    }

    public void Advance(float dt)
    {
        if (count == 0) return;
        dirty = true;
        for (int i = count - 1; i >= 0; i--)
        {
            float age = rings[i].Age + dt;
            if (age >= Duration) rings[i] = rings[--count];
            else rings[i] = rings[i] with { Age = age };
        }
    }

    public void Sync()
    {
        if (!dirty) return;
        for (int i = 0; i < count; i++)
        {
            Ring ring = rings[i];
            float progress = ring.Age / Duration;
            float radius = ring.Radius * Mathf.Lerp(0.08f, 1f, progress);
            Multimesh.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(new Vector3(radius, 1f, radius)), ring.Origin));
            Multimesh.SetInstanceColor(i, new Color(1f, 1f, 1f, 1f - progress * progress));
        }
        Multimesh.VisibleInstanceCount = count;
        dirty = false;
    }

    public void Clear()
    {
        count = 0;
        dirty = true;
        Sync();
    }
}
