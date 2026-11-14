// One shared MultiMesh batch per projectile definition, oriented along the flight tangent.
using System;
using Godot;

namespace MassiveRts;

public partial class ProjectileRenderer : Node3D
{
    private MultiMesh[] batches;
    private int[] counts;
    private int capacity;

    public void Initialize(int maximum, ProjectileCatalog catalog)
    {
        capacity = maximum;
        batches = new MultiMesh[catalog.Count];
        counts = new int[catalog.Count];
        for (int i = 0; i < batches.Length; i++)
        {
            var mesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = catalog.Mesh(i) };
            batches[i] = mesh;
            AddChild(new MultiMeshInstance3D
            {
                Multimesh = mesh,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            });
        }
    }

    public void Sync(ProjectileSystem projectiles)
    {
        if (!projectiles.Dirty) return;
        Array.Clear(counts);
        for (int i = 0; i < projectiles.Count; i++) counts[projectiles.Type[i]]++;
        for (int i = 0; i < batches.Length; i++)
        {
            MultiMesh mesh = batches[i];
            if (mesh.InstanceCount < counts[i])
                mesh.InstanceCount = Math.Min(capacity, Math.Max(counts[i], Math.Max(64, mesh.InstanceCount * 2)));
            mesh.VisibleInstanceCount = counts[i];
        }

        Array.Clear(counts);
        for (int i = 0; i < projectiles.Count; i++)
        {
            int type = projectiles.Type[i];
            Vector3 direction = projectiles.Direction[i].Normalized();
            Basis basis = Basis.Identity;
            if (direction.LengthSquared() > 0f)
            {
                Vector3 up = Mathf.Abs(direction.Y) > 0.99f ? Vector3.Right : Vector3.Up;
                basis = Basis.LookingAt(direction, up);
            }
            batches[type].SetInstanceTransform(counts[type]++, new Transform3D(basis, projectiles.Position[i]));
        }
        projectiles.Dirty = false;
    }
}
