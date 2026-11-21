using Godot;

namespace MassiveRts;

// Shield shells reuse the army's rendered pose, including interpolation and terrain offsets.
internal sealed class ShieldShellRenderer(Node3D owner, UnitState units, int limit)
{
    private readonly MultiMesh[] meshes = new MultiMesh[units.Catalog.Count];
    private readonly int[] counts = new int[units.Catalog.Count];
    private ShaderMaterial material;
    private int visible;
    internal int VisibleCount => visible;

    internal void Hide()
    {
        if (visible == 0) return;
        visible = 0;
        foreach (var mesh in meshes)
            if (mesh != null && mesh.VisibleInstanceCount > 0) mesh.VisibleInstanceCount = 0;
    }

    internal void Sync(ShieldSystem shields, int[] slots, float[][] buffers, int stride)
    {
        bool hasShells = shields.ShieldedCount > 0 || shields.HasFlashes;
        if (!hasShells && visible == 0) return;
        System.Array.Clear(counts);
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
            float[] buffer = buffers[units.Faction[id] * meshes.Length + type];
            int offset = slot * stride;
            Vector3 center = new(buffer[offset + 3], buffer[offset + 7], buffer[offset + 11]);
            if (camera != null && !camera.IsPositionInFrustum(center)) continue;

            var mesh = meshes[type] ??= CreateBatch(type);
            int index = counts[type]++;
            float scale = 1.08f + 0.04f * flash;
            Basis basis = new(
                new Vector3(buffer[offset], 0f, buffer[offset + 8]) * scale,
                new Vector3(0f, buffer[offset + 5], 0f) * scale,
                new Vector3(buffer[offset + 2], 0f, buffer[offset + 10]) * scale);
            mesh.SetInstanceTransform(index, new Transform3D(basis, center));
            mesh.SetInstanceColor(index, Factions.Color(units.Faction[id]));
            mesh.SetInstanceCustomData(index, new Color(strength, flash, 0f, 0f));
            visible++;
        }

        for (int i = 0; i < meshes.Length; i++)
            if (meshes[i] is { } mesh && mesh.VisibleInstanceCount != counts[i])
                mesh.VisibleInstanceCount = counts[i];
    }

    private MultiMesh CreateBatch(int type)
    {
        material ??= new ShaderMaterial
        {
            Shader = GD.Load<Shader>("res://VISUALS/RENDERING/ShieldShell.gdshader")
        };
        var mesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            UseCustomData = true,
            Mesh = FacetedSoldierMeshes.CreateShieldShell(units.Catalog[type]),
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
