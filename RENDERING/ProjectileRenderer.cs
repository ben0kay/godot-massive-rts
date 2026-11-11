// One reusable instanced projectile batch, updated only when projectile state changes.
using Godot;
namespace MassiveRts;

public partial class ProjectileRenderer : MultiMeshInstance3D
{
    // =========================================================
    public void Initialize(int capacity)
    {
        var material = new StandardMaterial3D { AlbedoColor = new Color(1f, 0.85f, 0.35f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
        Multimesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = new BoxMesh { Size = new Vector3(0.18f, 0.18f, 0.8f), Material = material } };
        Multimesh.InstanceCount = capacity; Multimesh.VisibleInstanceCount = 0;
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
    }
    // =========================================================
    public void Sync(ProjectileSystem projectiles)
    {
        if (!projectiles.Dirty) return;
        for (int i = 0; i < projectiles.Count; i++)
            Multimesh.SetInstanceTransform(i, new Transform3D(Basis.Identity, projectiles.Position[i]));
        Multimesh.VisibleInstanceCount = projectiles.Count; projectiles.Dirty = false;
    }
}
