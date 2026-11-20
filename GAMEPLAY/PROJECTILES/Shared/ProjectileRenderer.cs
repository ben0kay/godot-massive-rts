// One shared MultiMesh batch per projectile definition, oriented along the flight tangent.
using System;
using Godot;

namespace MassiveRts;

public partial class ProjectileRenderer : Node3D
{
    private MultiMesh[] batches;
    private int[] counts;
    private bool[] tintByFaction;
    private float[] visualScales;
    private int capacity;
    private ProjectileLaunchEffects launchEffects;
    private MuzzleSmoke smoke;
    private ProjectileSystem smokeSource;
    private ProjectileCatalog smokeCatalog;

    public void Initialize(int maximum, ProjectileCatalog catalog, ProjectileSystem projectiles, Vector2 smokeWind)
    {
        capacity = maximum;
        var visuals = catalog.HasLaunchEffects ? GD.Load<UnitVisualConfig>(UnitVisualConfig.DefaultPath) : null;
        if (visuals?.UseProjectileLaunchEffects == true)
        {
            launchEffects = new ProjectileLaunchEffects(this, catalog, maximum);
        }
        if (visuals?.UseMuzzleSmoke == true)
        {
            smoke = new MuzzleSmoke(this, smokeWind);
            smokeSource = projectiles;
            smokeCatalog = catalog;
            smokeSource.ShotLaunched += OnShotLaunched;
        }
        batches = new MultiMesh[catalog.Count];
        counts = new int[catalog.Count];
        visualScales = new float[catalog.Count];
        tintByFaction = new bool[catalog.Count];
        for (int i = 0; i < batches.Length; i++)
        {
            visualScales[i] = catalog.VisualScale(i);
            tintByFaction[i] = catalog.TintsByFaction(i);
            // Colors are enabled only for definitions that use them (such as Laser Bolt).
            var mesh = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                UseColors = tintByFaction[i], Mesh = catalog.Mesh(i)
            };
            batches[i] = mesh;
            AddChild(new MultiMeshInstance3D
            {
                Multimesh = mesh,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            });
        }
    }

    private void OnShotLaunched(Vector3 muzzle, int projectileType)
    {
        if (smokeCatalog.UsesLaunchEffects(projectileType)) smoke.Emit(muzzle);
    }

    internal void AdvanceSmoke(float delta) => smoke?.Advance(delta);

    public override void _ExitTree()
    {
        if (smokeSource != null) smokeSource.ShotLaunched -= OnShotLaunched;
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
            if (visualScales[type] != 1f) basis = basis.Scaled(Vector3.One * visualScales[type]);
            int slot = counts[type]++;
            batches[type].SetInstanceTransform(slot, new Transform3D(basis, projectiles.Position[i]));
            if (tintByFaction[type])
                batches[type].SetInstanceColor(slot, ArmyRenderer.FactionColor(projectiles.FactionAt(i)));
        }
        launchEffects?.Sync(projectiles);
        projectiles.Dirty = false;
    }
}
