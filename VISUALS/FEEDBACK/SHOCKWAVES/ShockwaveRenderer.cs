// Batches expanding impact rings and warm explosive flashes.
using Godot;

namespace MassiveRts;

public partial class ShockwaveRenderer : MultiMeshInstance3D
{
    private const int Capacity = 256;
    private const int Segments = 64;
    private const float Duration = 0.35f;
    private readonly record struct Ring(Vector3 Origin, float Radius, float Age, Vector3? BurstOrigin);
    private readonly Ring[] rings = new Ring[Capacity];
    private MultiMesh bursts;
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
        var flashMaterial = new ShaderMaterial
        {
            Shader = GD.Load<Shader>("res://VISUALS/FEEDBACK/SHOCKWAVES/ExplosionBurst.gdshader")
        };
        bursts = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseCustomData = true,
            Mesh = new QuadMesh { Material = flashMaterial },
            InstanceCount = Capacity,
            VisibleInstanceCount = 0
        };
        AddChild(new MultiMeshInstance3D
        {
            Multimesh = bursts, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            ExtraCullMargin = 64f
        });
    }

    private static Vector3 Direction(int index)
    {
        float angle = index * Mathf.Tau / Segments;
        return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
    }

    public void Emit(Vector3 origin, float radius, Vector3? burstOrigin = null)
    {
        if (count == Capacity) return;
        rings[count++] = new Ring(origin + Vector3.Up * 0.035f, radius, 0f, burstOrigin);
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
        int flashCount = 0;
        for (int i = 0; i < count; i++)
        {
            Ring ring = rings[i];
            float progress = ring.Age / Duration;
            float radius = ring.Radius * Mathf.Lerp(0.08f, 1f, progress);
            Multimesh.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(new Vector3(radius, 1f, radius)), ring.Origin));
            Multimesh.SetInstanceColor(i, new Color(1f, 1f, 1f, 1f - progress * progress));
            if (ring.BurstOrigin is Vector3 impact)
            {
                float extent = ring.Radius * 0.88f;
                Vector3 center = impact + Vector3.Up * (extent * 0.16f);
                bursts.SetInstanceTransform(flashCount, new Transform3D(Basis.Identity.Scaled(Vector3.One * extent), center));
                float seed = impact.X * 0.173f + impact.Z * 0.271f;
                seed -= Mathf.Floor(seed);
                bursts.SetInstanceCustomData(flashCount++, new Color(progress, seed, 0f, 0f));
            }
        }
        Multimesh.VisibleInstanceCount = count;
        bursts.VisibleInstanceCount = flashCount;
        dirty = false;
    }

    public void Clear()
    {
        count = 0;
        dirty = true;
        Sync();
    }
}
