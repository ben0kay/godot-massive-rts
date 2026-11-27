// Fading reflected tracers. Pure presentation: no projectile pool entries or collision queries.
using Godot;

namespace MassiveRts;

internal sealed class ShieldRicochetEffects
{
    private const int Capacity = 256;
    private const float Duration = 0.25f;
    private readonly record struct Bounce(Vector3 Origin, Vector3 Direction, Basis Pose, Vector3 Size, Color Tint, float Distance, float Age);
    private readonly Bounce[] active = new Bounce[Capacity];
    private readonly MultiMesh batch;
    private int count;
    private uint sequence;
    private bool dirty;

    internal ShieldRicochetEffects(Node3D owner)
    {
        var material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            VertexColorUseAsAlbedo = true
        };
        batch = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = new BoxMesh { Size = Vector3.One, Material = material },
            InstanceCount = Capacity,
            VisibleInstanceCount = 0
        };
        owner.AddChild(new MultiMeshInstance3D
        {
            Multimesh = batch, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            ExtraCullMargin = 4f
        });
    }

    // =========================================================
    // Reuse the swept unit hit sphere as an approximation of the rendered
    // shell. Reflect its incoming tangent, with a small outward/upward scatter.
    // Cosmetic random sequence is independent of the simulation's shot RNG.
    internal void Emit(Vector3 contact, Vector3 incoming, Vector3 center, float radius, byte faction, Vector3 size)
    {
        if (count >= Capacity || size == Vector3.Zero) return;
        Vector3 direction = incoming.Normalized();
        Vector3 normal = (contact - center).Normalized();
        if (normal.LengthSquared() < 0.001f || direction.Dot(normal) >= -0.01f)
            normal = -direction;
        Vector3 reflected = direction - 2f * direction.Dot(normal) * normal;
        float seed = (unchecked(++sequence * 2654435761u) >> 16) / 65535f;
        Vector3 side = normal.Cross(Vector3.Up).Normalized();
        reflected = (reflected + normal * 0.20f + side * ((seed - 0.5f) * 0.65f)
            + Vector3.Up * 0.24f).Normalized();
        // Ensure the visual always travels away from the hit shell.
        if (reflected.Dot(normal) < 0.15f)
            reflected = (reflected + normal * 0.75f).Normalized();
        Vector3 up = Mathf.Abs(reflected.Dot(Vector3.Up)) > 0.98f ? Vector3.Right : Vector3.Up;
        Basis pose = Basis.LookingAt(reflected, up);
        Vector3 start = center + normal * (radius + Mathf.Clamp(radius * 0.08f, 0.035f, 0.14f));
        // Use the original tracer dimensions and a pale version of its firing faction.
        Color tint = new Color(1f, 0.94f, 0.83f).Lerp(Factions.Color(faction), 0.35f);
        active[count++] = new Bounce(start, reflected, pose,
            new Vector3(Mathf.Max(size.X, 0.045f), Mathf.Max(size.Y, 0.045f), Mathf.Max(size.Z, 0.30f)),
            tint, Mathf.Lerp(2f, 4f, seed), 0f);
        dirty = true;
    }

    internal void Advance(float dt)
    {
        if (count == 0) return;
        dirty = true;
        for (int i = count - 1; i >= 0; i--)
        {
            float age = active[i].Age + dt;
            if (age >= Duration) active[i] = active[--count];
            else active[i] = active[i] with { Age = age };
        }
    }

    internal void Sync()
    {
        if (!dirty) return;
        for (int i = 0; i < count; i++)
        {
            var bounce = active[i];
            float progress = Mathf.Clamp(bounce.Age / Duration, 0f, 1f);
            float fade = 1f - progress;
            float travel = bounce.Distance * (1f - fade * fade);
            Vector3 position = bounce.Origin + bounce.Direction * travel;
            Vector3 size = bounce.Size * Mathf.Lerp(0.25f, 1f, fade);
            batch.SetInstanceTransform(i, new Transform3D(bounce.Pose * Basis.FromScale(size), position));
            Color tint = bounce.Tint;
            tint.A = fade * fade;
            batch.SetInstanceColor(i, tint);
        }
        batch.VisibleInstanceCount = count;
        dirty = false;
    }

    internal void Clear()
    {
        count = 0;
        sequence = 0;
        dirty = false;
        batch.VisibleInstanceCount = 0;
    }
}
