// Draws temporary ground scorch marks in a single fixed-size instance batch.
using Godot;

namespace MassiveRts;

internal sealed class ScorchMarks
{
    private const int Capacity = 128;
    private const float Lifetime = 9f;
    private const float FadeSeconds = 4f;
    private readonly record struct Mark(Transform3D Pose, float Expires, float Seed);
    private readonly Mark[] marks = new Mark[Capacity];
    private readonly MultiMesh mesh;
    private int count, recycle;
    private uint sequence;
    private float clock, untilRefresh;

    internal ScorchMarks(Node3D parent)
    {
        var material = new ShaderMaterial
        {
            Shader = GD.Load<Shader>("res://VISUALS/FEEDBACK/SHOCKWAVES/ScorchMark.gdshader")
        };
        mesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseCustomData = true,
            Mesh = new PlaneMesh { Size = new Vector2(2f, 2f), Material = material },
            InstanceCount = Capacity,
            VisibleInstanceCount = 0
        };
        parent.AddChild(new MultiMeshInstance3D
        {
            Multimesh = mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 6f
        });
    }

    internal void Emit(Vector3 impact, float blastRadius, TerrainHeightField terrain)
    {
        float radius = Mathf.Clamp(blastRadius * 0.64f, 0.65f, 5f);
        float slopeX = 0f, slopeZ = 0f;
        if (terrain != null)
        {
            float probe = Mathf.Min(0.65f, radius);
            slopeX = (terrain.At(impact.X + probe, impact.Z) - terrain.At(impact.X - probe, impact.Z)) / (2f * probe);
            slopeZ = (terrain.At(impact.X, impact.Z + probe) - terrain.At(impact.X, impact.Z - probe)) / (2f * probe);
            impact.Y = terrain.At(impact.X, impact.Z);
            // Keep marks compact on sharp changes in slope instead of spanning cliffs.
            radius /= Mathf.Max(1f, Mathf.Sqrt(slopeX * slopeX + slopeZ * slopeZ) * 2f);
        }
        float seed = (unchecked(++sequence * 2654435761u) >> 16) / 65535f;
        var normal = new Vector3(-slopeX, 1f, -slopeZ).Normalized();
        var basis = Basis.LookingAt(new Vector3(0f, -slopeZ, -1f), normal)
            * new Basis(Vector3.Up, seed * Mathf.Tau)
            * Basis.FromScale(new Vector3(radius, 1f, radius * Mathf.Lerp(0.76f, 1f, seed)));
        int slot = count < Capacity ? count++ : recycle++ % Capacity;
        marks[slot] = new Mark(new Transform3D(basis, impact + Vector3.Up * 0.045f), clock + Lifetime, seed);
        Write(slot);
        mesh.VisibleInstanceCount = count;
    }

    internal void Advance(float delta)
    {
        if (count == 0) return;
        clock += delta;
        untilRefresh -= delta;
        if (untilRefresh > 0f) return;
        untilRefresh = 0.08f;
        for (int slot = count - 1; slot >= 0; slot--)
        {
            float remaining = marks[slot].Expires - clock;
            if (remaining <= 0f)
            {
                int last = --count;
                if (last == slot) continue;
                marks[slot] = marks[last];
                Write(slot);
            }
            else if (remaining < FadeSeconds) SetOpacity(slot, remaining / FadeSeconds);
        }
        mesh.VisibleInstanceCount = count;
        if (count == 0) Clear();
    }

    private void Write(int slot)
    {
        mesh.SetInstanceTransform(slot, marks[slot].Pose);
        SetOpacity(slot, Mathf.Clamp((marks[slot].Expires - clock) / FadeSeconds, 0f, 1f));
    }

    private void SetOpacity(int slot, float opacity) =>
        mesh.SetInstanceCustomData(slot, new Color(opacity, marks[slot].Seed, 0f, 0f));

    internal void Clear()
    {
        count = recycle = 0;
        clock = untilRefresh = 0f;
        mesh.VisibleInstanceCount = 0;
    }
}
