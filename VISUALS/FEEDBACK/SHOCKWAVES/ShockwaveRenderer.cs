// Batches expanding impact rings and warm explosive flashes.
using Godot;

namespace MassiveRts;

public partial class ShockwaveRenderer : MultiMeshInstance3D
{
    private const int Capacity = 256;
    private const int ChipsPerHit = 6;
    private const int Segments = 64;
    private const float Duration = 0.35f;
    private readonly record struct Ring(Vector3 Origin, float Radius, float Age, Vector3? BurstOrigin, byte? Faction, bool StructureChips);
    private readonly Ring[] rings = new Ring[Capacity];
    private MultiMesh bursts, chips;
    private MeleeEffects meleeEffects;
    private readonly CavalryDust cavalryDust = new();
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
            UseColors = true,
            Mesh = new QuadMesh { Material = flashMaterial },
            InstanceCount = Capacity,
            VisibleInstanceCount = 0
        };
        AddChild(new MultiMeshInstance3D
        {
            Multimesh = bursts, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            ExtraCullMargin = 64f
        });
        chips = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = new BoxMesh { Size = Vector3.One, Material = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.49f, 0.45f, 0.39f),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
            }},
            InstanceCount = Capacity * ChipsPerHit,
            VisibleInstanceCount = 0
        };
        AddChild(new MultiMeshInstance3D { Multimesh = chips, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    private static Vector3 Direction(int index)
    {
        float angle = index * Mathf.Tau / Segments;
        return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
    }

    public void Emit(Vector3 origin, float radius, Vector3? burstOrigin = null, byte? faction = null, bool structureChips = false)
    {
        if (count == Capacity || (radius <= 0f && !structureChips)) return;
        rings[count++] = new Ring(origin + Vector3.Up * 0.035f, radius, 0f, burstOrigin, faction, structureChips);
        dirty = true;
    }

    internal void EmitMelee(Vector3 contact, Vector2 direction, float footprint, bool cleave)
    {
        meleeEffects ??= new MeleeEffects(this);
        meleeEffects.Emit(contact, direction, Mathf.Clamp(footprint * 1.6f, 0.58f, 1.2f), 0);
        if (cleave)
            meleeEffects.Emit(contact, direction, Mathf.Clamp(footprint * 3.9f, 1.3f, 3.1f), 1);
    }

    internal void SampleCavalry(UnitState units, float delta) => cavalryDust.Sample(units, delta, this);

    internal void EmitCavalryDust(Vector3 position, float size)
    {
        meleeEffects ??= new MeleeEffects(this);
        meleeEffects.Emit(position, Vector2.Zero, size, 2);
    }

    public void Advance(float dt)
    {
        meleeEffects?.Advance(dt);
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
        meleeEffects?.Sync();
        if (!dirty) return;
        int ringCount = 0, flashCount = 0, chipCount = 0;
        for (int i = 0; i < count; i++)
        {
            Ring ring = rings[i];
            float progress = ring.Age / Duration;
            Color faction = ring.Faction is byte id ? Factions.Color(id) : Colors.White;
            if (ring.Radius > 0f)
            {
                float radius = ring.Radius * Mathf.Lerp(0.08f, 1f, progress);
                Multimesh.SetInstanceTransform(ringCount, new Transform3D(Basis.Identity.Scaled(new Vector3(radius, 1f, radius)), ring.Origin));
                Color ringColor = Colors.White.Lerp(faction, 0.22f);
                ringColor.A = 1f - progress * progress;
                Multimesh.SetInstanceColor(ringCount++, ringColor);
            }
            if (ring.BurstOrigin is not Vector3 impact) continue;
            if (ring.StructureChips)
                for (int chip = 0; chip < ChipsPerHit; chip++)
                {
                    float angle = impact.X * 0.42f + impact.Z * 0.17f + chip * 2.4f;
                    Vector3 kick = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    Vector3 point = impact + kick * (0.24f + progress * (0.9f + chip * 0.12f));
                    point.Y += 0.1f + 2.5f * progress - 2.8f * progress * progress;
                    float size = (0.08f + chip * 0.014f) * (1f - progress * progress);
                    var spin = new Basis(new Vector3(1f, 1f, 0.5f).Normalized(), angle + progress * 7f);
                    chips.SetInstanceTransform(chipCount++, new Transform3D(spin.Scaled(new Vector3(size, size * 0.65f, size)), point));
                }
            if (ring.Radius > 0f)
            {
                float extent = ring.Radius * 0.88f;
                Vector3 center = impact + Vector3.Up * (extent * 0.16f);
                bursts.SetInstanceTransform(flashCount, new Transform3D(Basis.Identity.Scaled(Vector3.One * extent), center));
                float seed = impact.X * 0.173f + impact.Z * 0.271f;
                seed -= Mathf.Floor(seed);
                faction.A = ring.Faction.HasValue ? 1f : 0f;
                bursts.SetInstanceColor(flashCount, faction);
                bursts.SetInstanceCustomData(flashCount++, new Color(progress, seed, 0f, 0f));
            }
        }
        Multimesh.VisibleInstanceCount = ringCount;
        chips.VisibleInstanceCount = chipCount;
        bursts.VisibleInstanceCount = flashCount;
        dirty = false;
    }

    public void Clear()
    {
        cavalryDust.Clear();
        meleeEffects?.Clear();
        count = 0;
        dirty = true;
        Sync();
    }
}
