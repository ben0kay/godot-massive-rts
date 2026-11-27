// Draws pooled Prism beams, muzzle flashes, and terrain-following ground glow.
using Godot;

namespace MassiveRts;

internal sealed class PrismBeamEffects
{
    private readonly record struct Shot(Vector3 From, Vector3 To, Color Tint, float Life, float Remaining);
    private readonly Shot[] shots;
    private readonly MultiMesh beams, muzzle;
    private readonly BeamGroundGlow groundGlow;
    private readonly TerrainHeightField terrain;
    private int count;

    internal PrismBeamEffects(Node3D parent, int capacity, TerrainHeightField terrain)
    {
        shots = new Shot[capacity];
        this.terrain = terrain;
        var material = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
        var beamMaterial = new ShaderMaterial
        {
            Shader = GD.Load<Shader>("res://WORLD/BUILDINGS/RENDERING/RailgunBeam.gdshader")
        };
        beams = Batch(parent, new PlaneMesh { Size = Vector2.One, Material = beamMaterial }, useCustomData: true);
        groundGlow = new BeamGroundGlow(parent, capacity, 3.0f);
        muzzle = Batch(parent, new SphereMesh { Material = material, RadialSegments = 8, Rings = 4 });
    }

    private MultiMesh Batch(Node3D parent, Mesh mesh, bool useCustomData = false)
    {
        var batch = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true, UseCustomData = useCustomData, Mesh = mesh, InstanceCount = shots.Length, VisibleInstanceCount = 0 };
        parent.AddChild(new MultiMeshInstance3D
        {
            Multimesh = batch, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
        return batch;
    }

    internal void Emit(Vector3 from, Vector3 to, byte faction, float duration)
    {
        if (count >= shots.Length || from.DistanceSquaredTo(to) < 0.000001f) return;
        shots[count] = new Shot(from, to, Factions.Color(faction).Lightened(0.32f), duration, duration);
        groundGlow.Write(count, from, to, shots[count].Tint, terrain);
        WritePose(count);
        WriteFade(count++);
        SyncCount();
    }

    internal void Advance(float dt)
    {
        if (count == 0) return;
        for (int i = count - 1; i >= 0; i--)
        {
            float remaining = shots[i].Remaining - dt;
            if (remaining <= 0f)
            {
                int last = --count;
                groundGlow.Remove(i, last);
                if (i == last) continue;
                shots[i] = shots[last];
                WritePose(i);
            }
            else shots[i] = shots[i] with { Remaining = remaining };
            WriteFade(i);
        }
        SyncCount();
    }

    private void SyncCount()
    {
        beams.VisibleInstanceCount = muzzle.VisibleInstanceCount = count;
        groundGlow.SetVisibleCount(count);
    }

    private void WritePose(int slot)
    {
        Shot shot = shots[slot];
        Vector3 delta = shot.To - shot.From;
        float length = delta.Length();
        Basis facing = Basis.LookingAt(delta, Mathf.Abs(delta.Y) > length * 0.98f ? Vector3.Right : Vector3.Up);
        Vector3 middle = (shot.From + shot.To) * 0.5f;
        beams.SetInstanceCustomData(slot, new Color(0f, 0f, 0f, 0f));
        beams.SetInstanceTransform(slot,
            new Transform3D(facing * Basis.FromScale(new Vector3(0.65f, 1f, length)), middle));
    }

    private void WriteFade(int slot)
    {
        Shot shot = shots[slot];
        float fade = shot.Remaining / shot.Life;
        muzzle.SetInstanceTransform(slot,
            new Transform3D(Basis.FromScale(Vector3.One * (0.4f + 0.55f * fade)), shot.From));
        Color tint = shot.Tint;
        tint.A = fade;
        beams.SetInstanceColor(slot, tint);
        groundGlow.SetTint(slot, tint);
        muzzle.SetInstanceColor(slot, new Color(0.65f, 0.94f, 1f, fade));
    }
}
