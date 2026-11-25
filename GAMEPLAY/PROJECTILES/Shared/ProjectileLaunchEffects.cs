// Renders arcing trails, muzzle flashes, and hot projectile cores in shared batches.
using Godot;

namespace MassiveRts;

internal sealed class ProjectileLaunchEffects
{
    private const float TrailSeconds = 0.32f;
    private const float FlashSeconds = 0.10f;
    private readonly ProjectileCatalog catalog;
    private readonly MultiMesh trails, flashes, glows, smoke;

    internal ProjectileLaunchEffects(Node3D parent, ProjectileCatalog catalog, int capacity)
    {
        this.catalog = catalog;
        var material = new ShaderMaterial
        {
            Shader = GD.Load<Shader>("res://GAMEPLAY/PROJECTILES/Shared/ProjectileLaunchEffects.gdshader")
        };
        trails = CreateBatch(parent, new QuadMesh { SubdivideWidth = 11, Material = material }, capacity, useColors: true);
        flashes = CreateBatch(parent, new QuadMesh { Material = material }, capacity);
        glows = CreateBatch(parent, new QuadMesh { Material = material }, capacity);
        if (catalog.HasFlightSmoke)
        {
            var smokeMaterial = new ShaderMaterial
            {
                Shader = GD.Load<Shader>("res://GAMEPLAY/PROJECTILES/Shared/ProjectileFlightSmoke.gdshader")
            };
            smoke = CreateBatch(parent, new QuadMesh { SubdivideWidth = 11, Material = smokeMaterial }, capacity);
        }
    }

    private static MultiMesh CreateBatch(Node3D parent, Mesh mesh, int capacity, bool useColors = false)
    {
        var batch = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseCustomData = true, UseColors = useColors, Mesh = mesh, InstanceCount = capacity, VisibleInstanceCount = 0
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
        int trailCount = 0, flashCount = 0, glowCount = 0, smokeCount = 0;
        for (int i = 0; i < projectiles.Count; i++)
        {
            int type = projectiles.Type[i];
            if (!catalog.UsesLaunchEffects(type)) continue;
            float scale = catalog.VisualScale(type);
            Vector3 origin = projectiles.Position[i];
            bool hasSmoke = catalog.UsesFlightSmoke(type);
            Vector3 glowPosition = hasSmoke ? origin + projectiles.Direction[i].Normalized() * (0.48f * scale) : origin;
            glows.SetInstanceTransform(glowCount, new Transform3D(Basis.Identity, glowPosition));
            glows.SetInstanceCustomData(glowCount++, new Color(hasSmoke ? scale * 0.72f : scale, 0f, 0f, -2f));
            float age = projectiles.AgeAt(i);
            if (age > 0f)
            {
                trails.SetInstanceTransform(trailCount, new Transform3D(Basis.Identity, projectiles.Position[i]));
                trails.SetInstanceColor(trailCount, Factions.Color(projectiles.FactionAt(i)));
                trails.SetInstanceCustomData(trailCount++, projectiles.TrailCurveAt(i, TrailSeconds));
                if (hasSmoke)
                {
                    smoke.SetInstanceTransform(smokeCount, new Transform3D(Basis.Identity, origin));
                    smoke.SetInstanceCustomData(smokeCount++, projectiles.TrailCurveAt(i, 0.16f));
                }
            }
            if (age < FlashSeconds)
            {
                flashes.SetInstanceTransform(flashCount, new Transform3D(Basis.Identity, projectiles.MuzzleAt(i)));
                flashes.SetInstanceCustomData(flashCount++, new Color(age / FlashSeconds, 0f, 0f, -1f));
            }
        }
        trails.VisibleInstanceCount = trailCount;
        flashes.VisibleInstanceCount = flashCount;
        glows.VisibleInstanceCount = glowCount;
        if (smoke != null) smoke.VisibleInstanceCount = smokeCount;
    }
}
