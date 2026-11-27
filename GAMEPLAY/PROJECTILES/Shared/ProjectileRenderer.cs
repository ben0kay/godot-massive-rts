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
    private const int GroundGlowLimit = 256;
    private MultiMesh groundGlow;
    private ProjectileCatalog groundGlowCatalog;
    private TerrainHeightField groundGlowTerrain;
    private ProjectileLaunchEffects launchEffects;
    private MuzzleSmoke smoke;
    private ProjectileSystem smokeSource;
    private ProjectileCatalog smokeCatalog;
    private ProjectileSystem beamSource;
    private PrismBeamEffects prismBeams;
    private int prismEffectCap = -1;

    public void Initialize(int maximum, ProjectileCatalog catalog, ProjectileSystem projectiles, Vector2 smokeWind,
        TerrainHeightField terrain = null)
    {
        capacity = maximum;
        if (catalog.HasGroundGlow && maximum > 0)
        {
            groundGlowCatalog = catalog;
            groundGlowTerrain = terrain;
            groundGlow = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                UseColors = true,
                Mesh = new PlaneMesh { Size = new Vector2(0.95f, 2.1f) },
                InstanceCount = Math.Min(maximum, GroundGlowLimit),
                VisibleInstanceCount = 0
            };
            AddChild(new MultiMeshInstance3D
            {
                Multimesh = groundGlow,
                MaterialOverride = new ShaderMaterial
                {
                    Shader = GD.Load<Shader>("res://GAMEPLAY/PROJECTILES/Shared/ProjectileGroundGlow.gdshader")
                },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            });
        }
        beamSource = projectiles;
        beamSource.PrismBeamFired += OnPrismBeam;
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

    private void OnPrismBeam(Vector3 from, Vector3 to, byte faction, float seconds)
    {
        if (prismEffectCap < 0) prismEffectCap = Mathf.Clamp(UnitVisualConfig.Load().MaxPrismBeamEffects, 0, 512);
        if (prismEffectCap == 0) return;
        prismBeams ??= new PrismBeamEffects(this, prismEffectCap, groundGlowTerrain);
        prismBeams.Emit(from, to, faction, seconds);
    }

    internal void AdvanceEffects(float delta)
    {
        smoke?.Advance(delta);
        prismBeams?.Advance(delta);
    }

    public override void _ExitTree()
    {
        if (smokeSource != null) smokeSource.ShotLaunched -= OnShotLaunched;
        if (beamSource != null) beamSource.PrismBeamFired -= OnPrismBeam;
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

        int glowCount = 0;
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
            if (groundGlow != null && glowCount < groundGlow.InstanceCount && groundGlowCatalog.UsesGroundGlow(type) &&
                WriteGroundGlow(glowCount, projectiles.Position[i], direction, ArmyRenderer.FactionColor(projectiles.FactionAt(i))))
                glowCount++;
            if (tintByFaction[type])
                batches[type].SetInstanceColor(slot, ArmyRenderer.FactionColor(projectiles.FactionAt(i)));
        }
        if (groundGlow != null) groundGlow.VisibleInstanceCount = glowCount;
        launchEffects?.Sync(projectiles);
        projectiles.Dirty = false;
    }
    // Sample the ground below the bolt and align the streak with the local flight path.
    private bool WriteGroundGlow(int slot, Vector3 position, Vector3 direction, Color tint)
    {
        float height = groundGlowTerrain?.At(position.X, position.Z) ?? 0f;
        float separation = position.Y - height;
        if (separation < 0f || separation >= 4f) return false;

        var forward = new Vector3(direction.X, 0f, direction.Z).Normalized();
        if (forward == Vector3.Zero) forward = Vector3.Forward;
        if (groundGlowTerrain != null)
        {
            float ahead = groundGlowTerrain.At(position.X + forward.X * 0.65f, position.Z + forward.Z * 0.65f);
            float behind = groundGlowTerrain.At(position.X - forward.X * 0.65f, position.Z - forward.Z * 0.65f);
            if (Mathf.Abs(ahead - behind) > 1f) return false;
            forward.Y = (ahead - behind) / 1.3f;
        }

        tint.A = 1f - separation / 4f;
        groundGlow.SetInstanceColor(slot, tint);
        groundGlow.SetInstanceTransform(slot, new Transform3D(Basis.LookingAt(forward, Vector3.Up),
            new Vector3(position.X, height + 0.06f, position.Z)));
        return true;
    }
}
