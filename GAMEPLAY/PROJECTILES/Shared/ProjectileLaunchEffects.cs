// Renders arcing trails and muzzle flashes from existing projectile pool state in two shared batches.
using Godot;

namespace MassiveRts;

internal sealed class ProjectileLaunchEffects
{
    private const float TrailSeconds = 0.32f;
    private const float FlashSeconds = 0.10f;
    private readonly ProjectileCatalog catalog;
    private readonly MultiMesh trails, flashes;

    internal ProjectileLaunchEffects(Node3D parent, ProjectileCatalog catalog, int capacity)
    {
        this.catalog = catalog;
        var material = new ShaderMaterial
        {
            Shader = GD.Load<Shader>("res://GAMEPLAY/PROJECTILES/Shared/ProjectileLaunchEffects.gdshader")
        };
        trails = CreateBatch(parent, new QuadMesh { SubdivideWidth = 11, Material = material }, capacity);
        flashes = CreateBatch(parent, new QuadMesh { Material = material }, capacity);
    }

    private static MultiMesh CreateBatch(Node3D parent, Mesh mesh, int capacity)
    {
        var batch = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseCustomData = true, Mesh = mesh, InstanceCount = capacity, VisibleInstanceCount = 0
        };
        parent.AddChild(new MultiMeshInstance3D
        {
            Multimesh = batch, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            ExtraCullMargin = 40f
        });
        return batch;
    }

    internal void Sync(ProjectileSystem projectiles)
    {
        int trailCount = 0, flashCount = 0;
        for (int i = 0; i < projectiles.Count; i++)
        {
            if (!catalog.UsesLaunchEffects(projectiles.Type[i])) continue;
            float age = projectiles.AgeAt(i);
            if (age > 0f)
            {
                trails.SetInstanceTransform(trailCount, new Transform3D(Basis.Identity, projectiles.Position[i]));
                trails.SetInstanceCustomData(trailCount++, projectiles.TrailCurveAt(i, TrailSeconds));
            }
            if (age < FlashSeconds)
            {
                flashes.SetInstanceTransform(flashCount, new Transform3D(Basis.Identity, projectiles.MuzzleAt(i)));
                flashes.SetInstanceCustomData(flashCount++, new Color(age / FlashSeconds, 0f, 0f, -1f));
            }
        }
        trails.VisibleInstanceCount = trailCount;
        flashes.VisibleInstanceCount = flashCount;
    }
}
