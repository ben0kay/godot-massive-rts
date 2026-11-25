// Short contact effects, sharing one capped batch and a fixed spatial cooldown table.
using Godot;

namespace MassiveRts;

internal sealed class MeleeEffects
{
    private const int Capacity = 256;
    private const int CooldownBuckets = 1024;
    private readonly record struct Effect(Vector3 Point, Vector2 Direction, float Size, float Duration, int Kind, float Age);
    private readonly Effect[] effects = new Effect[Capacity];
    private readonly float[] nextEmission = new float[CooldownBuckets];
    private readonly MultiMesh batch;
    private int count;
    private float clock;
    private bool dirty;

    internal MeleeEffects(Node3D parent)
    {
        var material = new ShaderMaterial
        {
            Shader = GD.Load<Shader>("res://VISUALS/FEEDBACK/SHOCKWAVES/MeleeEffects.gdshader")
        };
        batch = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseCustomData = true,
            Mesh = new QuadMesh { Material = material },
            InstanceCount = Capacity,
            VisibleInstanceCount = 0
        };
        parent.AddChild(new MultiMeshInstance3D
        {
            Multimesh = batch, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            ExtraCullMargin = 4f
        });
    }

    internal void Emit(Vector3 point, Vector2 direction, float size, int kind)
    {
        if (count == Capacity) return;
        int x = Mathf.FloorToInt(point.X / 3f), z = Mathf.FloorToInt(point.Z / 3f);
        int bucket = unchecked((x * 73856093 ^ z * 19349663 ^ kind * 83492791) & (CooldownBuckets - 1));
        if (clock < nextEmission[bucket]) return;
        nextEmission[bucket] = clock + (kind == 2 ? 0.22f : 0.13f);
        float duration = kind == 2 ? 0.35f : kind == 1 ? 0.17f : 0.13f;
        effects[count++] = new Effect(point, direction.Normalized(), size, duration, kind, 0f);
        dirty = true;
    }

    internal void Advance(float delta)
    {
        clock += delta;
        if (count == 0) return;
        dirty = true;
        for (int i = count - 1; i >= 0; i--)
        {
            float age = effects[i].Age + delta;
            if (age >= effects[i].Duration) effects[i] = effects[--count];
            else effects[i] = effects[i] with { Age = age };
        }
    }

    internal void Sync()
    {
        if (!dirty) return;
        for (int i = 0; i < count; i++)
        {
            Effect effect = effects[i];
            batch.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(Vector3.One * effect.Size), effect.Point));
            batch.SetInstanceCustomData(i,
                new Color(effect.Age / effect.Duration, effect.Kind, effect.Direction.X, effect.Direction.Y));
        }
        batch.VisibleInstanceCount = count;
        dirty = false;
    }

    internal void Clear()
    {
        count = 0;
        clock = 0f;
        System.Array.Clear(nextEmission);
        dirty = true;
        Sync();
    }
}
