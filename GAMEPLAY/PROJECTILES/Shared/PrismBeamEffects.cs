// Draws pooled, fading Prism beam flashes using three shared MultiMesh batches.
using Godot;

namespace MassiveRts;

internal sealed class PrismBeamEffects
{
    private readonly record struct Shot(Vector3 From, Vector3 To, Color Tint, float Life, float Remaining);
    private readonly Shot[] shots;
    private readonly MultiMesh core, halo, muzzle;
    private int count;

    internal PrismBeamEffects(Node3D parent, int capacity)
    {
        shots = new Shot[capacity];
        var material = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
        core = Batch(parent, new BoxMesh { Material = material });
        halo = Batch(parent, new BoxMesh { Material = material });
        muzzle = Batch(parent, new SphereMesh { Material = material, RadialSegments = 8, Rings = 4 });
    }

    private MultiMesh Batch(Node3D parent, Mesh mesh)
    {
        var batch = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true, Mesh = mesh, InstanceCount = shots.Length, VisibleInstanceCount = 0 };
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
        Write(count++);
        core.VisibleInstanceCount = halo.VisibleInstanceCount = muzzle.VisibleInstanceCount = count;
    }

    internal void Advance(float dt)
    {
        if (count == 0) return;
        for (int i = count - 1; i >= 0; i--)
        {
            float remaining = shots[i].Remaining - dt;
            if (remaining <= 0f)
            {
                shots[i] = shots[--count];
                if (i == count) continue;
            }
            else shots[i] = shots[i] with { Remaining = remaining };
            Write(i);
        }
        core.VisibleInstanceCount = halo.VisibleInstanceCount = muzzle.VisibleInstanceCount = count;
    }

    private void Write(int slot)
    {
        Shot shot = shots[slot];
        float fade = shot.Remaining / shot.Life;
        Vector3 delta = shot.To - shot.From;
        float length = delta.Length();
        Basis facing = Basis.LookingAt(delta, Mathf.Abs(delta.Y) > length * 0.98f ? Vector3.Right : Vector3.Up);
        Vector3 middle = (shot.From + shot.To) * 0.5f;
        core.SetInstanceTransform(slot, new Transform3D(facing * Basis.Identity.Scaled(new Vector3(0.17f, 0.17f, length)), middle));
        halo.SetInstanceTransform(slot, new Transform3D(facing * Basis.Identity.Scaled(new Vector3(0.52f, 0.52f, length)), middle));
        muzzle.SetInstanceTransform(slot, new Transform3D(Basis.Identity.Scaled(Vector3.One * (0.4f + 0.55f * fade)), shot.From));
        core.SetInstanceColor(slot, new Color(1f, 1f, 1f, fade));
        Color tint = shot.Tint;
        tint.A = fade * 0.48f;
        halo.SetInstanceColor(slot, tint);
        muzzle.SetInstanceColor(slot, new Color(0.65f, 0.94f, 1f, fade));
    }
}
