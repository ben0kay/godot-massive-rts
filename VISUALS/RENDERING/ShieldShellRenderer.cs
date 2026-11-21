using Godot;

namespace MassiveRts;

// Shield shells reuse the army's rendered pose, including interpolation and terrain offsets.
internal sealed class ShieldShellRenderer(Node3D owner, UnitState units, Mesh[] soldierMeshes, int limit)
{
    private readonly Vector3[] centers = new Vector3[soldierMeshes.Length];
    private readonly Vector3[] radii = new Vector3[soldierMeshes.Length];
    private MultiMesh batch;
    private int visible;
    internal int VisibleCount => visible;

    internal void Hide()
    {
        if (visible == 0) return;
        visible = 0;
        batch.VisibleInstanceCount = 0;
    }

    internal void Sync(ShieldSystem shields, int[] slots, float[][] buffers, int stride)
    {
        bool hasShells = shields.ShieldedCount > 0 || shields.HasFlashes;
        if (!hasShells && visible == 0) return;
        visible = 0;
        var camera = owner.GetViewport().GetCamera3D();
        for (int i = 0; hasShells && i < shields.ActiveCount && visible < limit; i++)
        {
            int id = shields.IdAt(i);
            int slot = slots[id];
            if (slot < 0) continue;
            float strength = shields.Strength(id), flash = shields.Flash(id);
            if (strength <= 0f && flash <= 0f) continue;
            int type = units.Type[id];
            float[] buffer = buffers[units.Faction[id] * soldierMeshes.Length + type];
            int offset = slot * stride;
            Vector3 center = new(buffer[offset + 3], buffer[offset + 7], buffer[offset + 11]);
            if (camera != null && !camera.IsPositionInFrustum(center)) continue;

            batch ??= CreateBatch();
            if (radii[type] == Vector3.Zero) FitBubble(type);

            Basis pose = new(
                new Vector3(buffer[offset], 0f, buffer[offset + 8]),
                new Vector3(0f, buffer[offset + 5], 0f),
                new Vector3(buffer[offset + 2], 0f, buffer[offset + 10]));
            Vector3 radius = radii[type] * (1f + 0.04f * flash);
            Basis bubble = new(pose.X * radius.X, pose.Y * radius.Y, pose.Z * radius.Z);
            batch.SetInstanceTransform(visible, new Transform3D(bubble, center + pose * centers[type]));
            batch.SetInstanceColor(visible, Factions.Color(units.Faction[id]));
            batch.SetInstanceCustomData(visible, new Color(strength, flash, (id % 31) / 31f, 0f));
            visible++;
        }

        if (batch != null && batch.VisibleInstanceCount != visible)
            batch.VisibleInstanceCount = visible;
    }

    private void FitBubble(int type)
    {
        var mesh = soldierMeshes[type];
        Aabb bounds = mesh.GetAabb();
        Vector3 center = bounds.GetCenter();
        Vector3 half = bounds.Size * 0.5f;
        float horizontal = Mathf.Max(half.X, half.Z);
        Vector3 radius = new(
            Mathf.Max(half.X, horizontal * 0.70f),
            Mathf.Max(half.Y, horizontal * 0.72f),
            Mathf.Max(half.Z, horizontal * 0.70f));
        // Fit the visible vertices once per type, including details such as fixed barrels.
        float fitSquared = 1f;
        foreach (Vector3 vertex in mesh.GetFaces())
            fitSquared = Mathf.Max(fitSquared, ((vertex - center) / radius).LengthSquared());
        centers[type] = center;
        radii[type] = radius * (Mathf.Sqrt(fitSquared) * 1.10f);
    }

    private MultiMesh CreateBatch()
    {
        var material = new ShaderMaterial
        {
            Shader = GD.Load<Shader>("res://VISUALS/RENDERING/ShieldShell.gdshader")
        };
        var mesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            UseCustomData = true,
            Mesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 20, Rings = 8 },
            InstanceCount = limit,
            VisibleInstanceCount = 0
        };
        owner.AddChild(new MultiMeshInstance3D
        {
            Multimesh = mesh, MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
        return mesh;
    }
}
