// Batches short-lived muzzle smoke independently of projectile travel and impact.
using Godot;

namespace MassiveRts;

internal sealed class MuzzleSmoke
{
    internal static readonly Vector2 DefaultWind = new(1.0f, 0f);
    private const int Capacity = 512;
    private const float Delay = 0.065f;
    private const float Lifetime = 2.4f;
    private readonly record struct Puff(Vector3 Origin, float Born, float Seed, float Scale);
    private readonly Puff[] puffs = new Puff[Capacity];
    private readonly MultiMesh batch;
    private readonly Vector3 wind;
    private int count;
    private uint sequence;
    private float clock;

    internal MuzzleSmoke(Node3D parent, Vector2 wind)
    {
        if (!float.IsFinite(wind.X) || !float.IsFinite(wind.Y)) wind = DefaultWind;
        this.wind = new Vector3(wind.X, 0f, wind.Y);
        var material = new ShaderMaterial
        {
            Shader = GD.Load<Shader>("res://GAMEPLAY/PROJECTILES/Shared/MuzzleSmoke.gdshader")
        };
        batch = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseCustomData = true, Mesh = new QuadMesh { Material = material },
            InstanceCount = Capacity, VisibleInstanceCount = 0
        };
        parent.AddChild(new MultiMeshInstance3D
        {
            Multimesh = batch, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            ExtraCullMargin = 3f
        });
    }

    internal void Emit(Vector3 muzzle, float scale = 1f)
    {
        if (count == Capacity) return;
        float seed = (unchecked(++sequence * 2654435761u) >> 16) / 65535f;
        puffs[count++] = new Puff(muzzle, clock + Delay, seed, scale);
    }

    internal void Advance(float delta)
    {
        if (count == 0) return;
        clock += delta;
        int visible = 0;
        for (int i = count - 1; i >= 0; i--)
        {
            var puff = puffs[i];
            float age = clock - puff.Born;
            if (age >= Lifetime)
            {
                puffs[i] = puffs[--count];
                continue;
            }
            if (age < 0f) continue;

            float progress = age / Lifetime;
            Vector3 center = puff.Origin + wind * (age * (0.52f + puff.Seed * 0.35f))
                + Vector3.Up * (age * 0.38f);
            batch.SetInstanceTransform(visible, new Transform3D(Basis.Identity, center));
            batch.SetInstanceCustomData(visible++, new Color(progress, puff.Seed, puff.Scale, 0f));
        }
        batch.VisibleInstanceCount = visible;
        if (count == 0) clock = 0f;
    }
}
