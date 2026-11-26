// Batches faceted platforms, enlarged rotating turret heads and sustained railgun beams without per-shot nodes.
using System;
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

public partial class DefenseRenderer : Node3D
{
    // Preserve the existing extended barrel and its laser/muzzle endpoint.
    private const float BarrelLength = 3.8f;
    private const float BarrelCenterForward = BarrelLength * 0.5f;
    private const float FlashSeconds = 0.16f;
    private const float GroundFlashSeconds = 0.32f;
    private const int GroundFlashCapacity = 64;
    private static readonly ShaderMaterial LitTowerMaterial = new()
    {
        Shader = GD.Load<Shader>("res://WORLD/BUILDINGS/RENDERING/TowerArmor.gdshader")
    };
    private static readonly Mesh BaseMesh = RailgunTowerMeshes.CreateBase();
    private static readonly Mesh HeadMesh = RailgunTowerMeshes.CreateHead();
    private static readonly Mesh BarrelMesh = RailgunTowerMeshes.CreateBarrel(BarrelLength);
    // Shared neutral metal / faction paint / emissive trim for authored two-tone towers.
    private static readonly ShaderMaterial TwoToneTowerMaterial = CreateTowerArmor();

    private static ShaderMaterial CreateTowerArmor()
    {
        var material = new ShaderMaterial
        {
            Shader = GD.Load<Shader>("res://WORLD/BUILDINGS/RENDERING/BroadsideArmor.gdshader")
        };
        material.SetShaderParameter("crack_mask",
            GD.Load<Texture2D>("res://VISUALS/FEEDBACK/UNIT_HITS/UnitCrackMask.svg"));
        return material;
    }
    private static readonly Mesh RocketBaseMesh = BroadsideTowerMeshes.CreateBase();
    private static readonly Mesh RocketHeadMesh = BroadsideTowerMeshes.CreateHead();
    private static readonly Mesh RocketBarrelMesh = BroadsideTowerMeshes.CreateBarrel();
    private static readonly Mesh BeamMesh = new PlaneMesh { Size = Vector2.One };
    private static readonly Mesh BeamGlowMesh = new QuadMesh();
    private static readonly ShaderMaterial BeamMaterial = new()
    {
        Shader = GD.Load<Shader>("res://WORLD/BUILDINGS/RENDERING/RailgunBeam.gdshader")
    };
    private static readonly Mesh FlashMesh = BuildFlashMesh();
    private static readonly StandardMaterial3D FlashMaterial = new()
    {
        VertexColorUseAsAlbedo = true,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled
    };
    private const int FlashCapacity = 256;
    private readonly float[] flashTime = new float[FlashCapacity];
    private readonly Transform3D[] flashPose = new Transform3D[FlashCapacity];
    private MultiMesh groundFlashes;
    private int liveGroundFlashes;
    private readonly float[] groundFlashTime = new float[GroundFlashCapacity];
    private readonly Transform3D[] groundFlashPose = new Transform3D[GroundFlashCapacity];
    private readonly Color[] groundFlashTint = new Color[GroundFlashCapacity];
    private MultiMesh bases, heads, barrels, rocketBases, rocketHeads, rocketBarrels, rightRocketBarrels, beams, beamGlows, flashes;
    private readonly int[] crackStages = new int[DeploymentLayout.MaxBuildings];
    private readonly bool[] rocketSlots = new bool[DeploymentLayout.MaxBuildings];
    private readonly int[] beamSlots = new int[DeploymentLayout.MaxBuildings];
    private readonly int[] beamOwners = new int[DeploymentLayout.MaxBuildings];
    private int liveBeams, liveFlashes;
    private MultiMesh shieldTowers;
    private MultiMesh minigunBase, minigunHead, minigunBarrels;
    private static Mesh minigunBaseMesh, minigunHeadMesh, minigunBarrelMesh;
    private readonly bool[] minigunSlots = new bool[DeploymentLayout.MaxBuildings];
    private readonly MultiMesh[] shelterBatches = new MultiMesh[PrefabShelterMeshes.VariantCount];
    private static readonly Mesh[] ShelterMeshes = new Mesh[PrefabShelterMeshes.VariantCount];
    private readonly bool[] shelterSlots = new bool[DeploymentLayout.MaxBuildings];
    private readonly int[] shelterVariants = new int[DeploymentLayout.MaxBuildings];
    private ShieldFieldDomeRenderer shieldDomes;
    private bool showShieldDomes = true;
    private static Mesh shieldTowerMesh;
    private static readonly Transform3D Hidden = new(Basis.Identity.Scaled(Vector3.Zero), Vector3.Zero);

    // =========================================================
    private static Basis RailBarrelBasis(Basis yaw, float slope) =>
        yaw * new Basis(Vector3.Right, Mathf.Atan(slope));

    // Keep the beam origin on the tip as the barrel pitches and recoils.
    public static Vector3 Muzzle(Vector3 pivot, float yaw, float recoil, float slope) =>
        pivot + RailBarrelBasis(new Basis(Vector3.Up, yaw), slope) * new Vector3(0f, 0f, recoil - BarrelLength);

    private static readonly Basis RocketBarrelPitch = new(Vector3.Right, Mathf.DegToRad(15f));

    // Share the barrel transform so the shot and recoil follow the same raised axis.
    private static Transform3D RocketBarrelTransform(Vector3 pivot, Basis yaw, int side, float recoil)
    {
        Basis barrel = yaw * RocketBarrelPitch;
        Vector3 mount = new(side == 0 ? -0.7f : 0.7f, 0.9f, 0f);
        return new Transform3D(barrel, pivot + yaw * mount + barrel * (Vector3.Back * recoil));
    }

    public static Vector3 RocketMuzzle(Vector3 pivot, float yaw, int side, float recoil = 0f) =>
        RocketBarrelTransform(pivot, new Basis(Vector3.Up, yaw), side, recoil) * (Vector3.Forward * 4.7f);

    // A small white-core/cyan-tip starburst inspired by the short flash style in Tower Defense.
    // Geometry is shared, so firing requires no nodes or mesh construction.
    private static Mesh BuildFlashMesh()
    {
        Vector3[] points =
        {
            new(0f, 0f, -0.83f), new(0.12f, 0f, -0.16f),
            new(0.42f, 0f, 0f), new(0.12f, 0f, 0.13f),
            new(0f, 0f, 0.39f), new(-0.12f, 0f, 0.13f),
            new(-0.42f, 0f, 0f), new(-0.12f, 0f, -0.16f)
        };
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < points.Length; i++)
        {
            surface.SetColor(Colors.White);
            surface.AddVertex(new Vector3(0f, 0f, -0.08f));
            surface.SetColor(new Color(0.48f, 0.86f, 1f, 0.7f));
            surface.AddVertex(points[i]);
            surface.AddVertex(points[(i + 1) % points.Length]);
        }
        return surface.Commit();
    }

    private MultiMesh Batch(Mesh mesh, int capacity, Material material = null, bool useCustomData = false)
    {
        var batch = new MultiMesh
        {
            Mesh = mesh, TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true,
            UseCustomData = useCustomData, InstanceCount = capacity
        };
        AddChild(new MultiMeshInstance3D
        {
            Multimesh = batch, MaterialOverride = material ?? LitTowerMaterial, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
        return batch;
    }

    // =========================================================
    public void Initialize(IReadOnlyList<BuildingPlacement> placements, RuntimeBuildingCatalog catalog, float height, bool battle, TerrainHeightField terrain = null)
    {
        InitializeBatches();
        liveGroundFlashes = 0;
        if (groundFlashes != null) groundFlashes.VisibleInstanceCount = 0;
        shieldDomes?.Clear();
        ResetMinigunBatch(minigunBase);
        ResetMinigunBatch(minigunHead);
        ResetMinigunBatch(minigunBarrels);
        foreach (var batch in shelterBatches)
        {
            if (batch == null) continue;
            for (int i = 0; i < batch.VisibleInstanceCount; i++) batch.SetInstanceTransform(i, Hidden);
            batch.VisibleInstanceCount = 0;
        }
        if (shieldTowers != null)
        {
            for (int i = 0; i < shieldTowers.VisibleInstanceCount; i++) shieldTowers.SetInstanceTransform(i, Hidden);
            shieldTowers.VisibleInstanceCount = 0;
        }
        bases.VisibleInstanceCount = heads.VisibleInstanceCount = barrels.VisibleInstanceCount =
            rocketBases.VisibleInstanceCount = rocketHeads.VisibleInstanceCount = rocketBarrels.VisibleInstanceCount = rightRocketBarrels.VisibleInstanceCount = placements.Count;
        for (int i = 0; i < placements.Count; i++)
        {
            var placement = placements[i];
            var area = placement.Footprint(catalog);
            ShowTower(i, area, placement.Faction, height, terrain?.FootprintHeight(area.Min, area.Max, out _) ?? 0f,
                catalog[placement.Type].ProjectileType >= 0, catalog[placement.Type].ShieldEmitter.Enabled,
                catalog[placement.Type].ShieldEmitter.Radius, catalog[placement.Type].Id == PrefabShelterMeshes.Id,
                catalog[placement.Type].Id == TwinMinigunTowerMeshes.Id);
        }
        if (battle)
        {
            beams ??= Batch(BeamMesh, DeploymentLayout.MaxBuildings, BeamMaterial, useCustomData: true);
            beamGlows ??= Batch(BeamGlowMesh, DeploymentLayout.MaxBuildings * 2, BeamMaterial, useCustomData: true);
            Array.Fill(beamSlots, -1);
            flashes ??= Batch(FlashMesh, FlashCapacity, FlashMaterial);
            beams.VisibleInstanceCount = beamGlows.VisibleInstanceCount = flashes.VisibleInstanceCount = 0;
            liveBeams = liveFlashes = 0;
        }
    }

    private void InitializeBatches()
    {
        bases ??= Batch(BaseMesh, DeploymentLayout.MaxBuildings, TwoToneTowerMaterial, useCustomData: true);
        heads ??= Batch(HeadMesh, DeploymentLayout.MaxBuildings, TwoToneTowerMaterial, useCustomData: true);
        barrels ??= Batch(BarrelMesh, DeploymentLayout.MaxBuildings, TwoToneTowerMaterial, useCustomData: true);
        rocketBases ??= Batch(RocketBaseMesh, DeploymentLayout.MaxBuildings, TwoToneTowerMaterial, useCustomData: true);
        rocketHeads ??= Batch(RocketHeadMesh, DeploymentLayout.MaxBuildings, TwoToneTowerMaterial, useCustomData: true);
        rocketBarrels ??= Batch(RocketBarrelMesh, DeploymentLayout.MaxBuildings, TwoToneTowerMaterial, useCustomData: true);
        rightRocketBarrels ??= Batch(RocketBarrelMesh, DeploymentLayout.MaxBuildings, TwoToneTowerMaterial, useCustomData: true);
    }

    internal void ShowTower(int index, ObstacleRectangle area, byte faction, float height, float ground = 0f,
        bool rocket = false, bool shield = false, float shieldRadius = 0f, bool shelter = false, bool minigun = false)
    {
        crackStages[index] = 0;
        shelterSlots[index] = shelter;
        minigunSlots[index] = minigun;
        if (minigun)
        {
            HideTower(index);
            ShowMinigun(index, area, faction, height, ground);
            return;
        }
        HideMinigun(index);
        if (shelter)
        {
            HideTower(index);
            ShowShelter(index, area, faction, height, ground);
            return;
        }
        HideShelter(index);
        if (shield)
        {
            HideTower(index);
            ShowShieldTower(index, area, faction, height, ground, shieldRadius);
            return;
        }
        shieldTowers?.SetInstanceTransform(index, Hidden);
        shieldDomes?.Hide(index);
        rocketSlots[index] = rocket;
        Vector2 size = area.Max - area.Min;
        Vector3 pivot = Pivot(area, height) + Vector3.Up * ground;
        Color color = ArmyRenderer.FactionColor(faction);
        var activeBase = rocket ? rocketBases : bases;
        var activeHead = rocket ? rocketHeads : heads;
        var activeBarrels = rocket ? rocketBarrels : barrels;
        var inactiveBase = rocket ? bases : rocketBases;
        var inactiveHead = rocket ? heads : rocketHeads;
        var inactiveBarrels = rocket ? barrels : rocketBarrels;
        var hidden = new Transform3D(Basis.Identity.Scaled(Vector3.Zero), Vector3.Zero);
        inactiveBase.SetInstanceTransform(index, hidden);
        inactiveHead.SetInstanceTransform(index, hidden);
        inactiveBarrels.SetInstanceTransform(index, hidden);
        if (!rocket) rightRocketBarrels.SetInstanceTransform(index, hidden);
        activeBase.SetInstanceTransform(index, new Transform3D(Basis.Identity.Scaled(new Vector3(size.X, height, size.Y)),
            new Vector3(pivot.X, ground + height * 0.5f, pivot.Z)));
        // Keep the baked material mask intact; the faction tint travels separately.
        SetArmorTint(activeBase, index, color);
        SetArmorTint(activeHead, index, color);
        SetArmorTint(activeBarrels, index, color);
        if (rocket) SetArmorTint(rightRocketBarrels, index, color);
        Aim(index, pivot, 0f);
        int count = Mathf.Max(bases.VisibleInstanceCount, index + 1);
        bases.VisibleInstanceCount = heads.VisibleInstanceCount = barrels.VisibleInstanceCount =
            rocketBases.VisibleInstanceCount = rocketHeads.VisibleInstanceCount =
                rocketBarrels.VisibleInstanceCount = rightRocketBarrels.VisibleInstanceCount = count;
    }

    // =========================================================
    // Set once on placement/reuse, not during aiming or recoil.
    private static void SetArmorTint(MultiMesh batch, int index, Color faction)
    {
        batch.SetInstanceColor(index, Colors.White);
        faction.A = 0f;
        batch.SetInstanceCustomData(index, faction);
    }

    internal void SetDamageCracks(int index, float fraction, bool shield)
    {
        int stage = fraction <= 0.25f ? 3 : fraction <= 0.50f ? 2 : fraction <= 0.75f ? 1 : 0;
        if (crackStages[index] == stage) return;
        crackStages[index] = stage;
        MultiMesh batch = minigunSlots[index] ? minigunBase : shelterSlots[index] ? shelterBatches[shelterVariants[index]] : shield ? shieldTowers : rocketSlots[index] ? rocketBases : bases;
        Color data = batch.GetInstanceCustomData(index);
        data.A = stage / 3f;
        batch.SetInstanceCustomData(index, data);
    }

    // =========================================================
    // The static generator reuses the same armor mask as the weapon towers.
    private void ShowShieldTower(int index, ObstacleRectangle area, byte faction, float height, float ground, float radius)
    {
        if (shieldTowers == null)
        {
            shieldTowerMesh ??= ShieldGeneratorMeshes.Create();
            shieldTowers = Batch(shieldTowerMesh, DeploymentLayout.MaxBuildings, TwoToneTowerMaterial, useCustomData: true);
            for (int i = 0; i < shieldTowers.InstanceCount; i++) shieldTowers.SetInstanceTransform(i, Hidden);
            shieldTowers.VisibleInstanceCount = 0;
        }
        Vector2 size = area.Max - area.Min;
        Vector3 pivot = Pivot(area, height);
        Vector3 center = new(pivot.X, ground, pivot.Z);
        Color color = Factions.Color(faction);
        shieldTowers.SetInstanceTransform(index, new Transform3D(
            Basis.Identity.Scaled(new Vector3(size.X, height, size.Y)), center));
        SetArmorTint(shieldTowers, index, color);
        shieldTowers.VisibleInstanceCount = Mathf.Max(shieldTowers.VisibleInstanceCount, index + 1);

        if (radius > 0f)
        {
            shieldDomes ??= new ShieldFieldDomeRenderer(this, showShieldDomes);
            shieldDomes.Show(index, center, radius, color);
        }
    }

    // Benchmark's visual toggle is independent of the individual soldier shells.
    internal void SetShieldDomesVisible(bool visible)
    {
        showShieldDomes = visible;
        shieldDomes?.SetVisible(visible);
    }

    public static Vector3 Pivot(ObstacleRectangle area, float height)
    {
        Vector2 center = (area.Min + area.Max) * 0.5f;
        return new Vector3(center.X, height + 0.25f, center.Y);
    }

    private void ShowShelter(int index, ObstacleRectangle area, byte faction, float height, float ground)
    {
        int variant = PrefabShelterMeshes.Variant(area);
        shelterVariants[index] = variant;
        MultiMesh batch = shelterBatches[variant];
        if (batch == null)
        {
            ShelterMeshes[variant] ??= PrefabShelterMeshes.Create(variant);
            batch = shelterBatches[variant] = Batch(ShelterMeshes[variant], DeploymentLayout.MaxBuildings,
                TwoToneTowerMaterial, useCustomData: true);
            for (int i = 0; i < batch.InstanceCount; i++) batch.SetInstanceTransform(i, Hidden);
            batch.VisibleInstanceCount = 0;
        }
        var size = area.Max - area.Min;
        var center = (area.Min + area.Max) * 0.5f;
        batch.SetInstanceTransform(index, new Transform3D(Basis.Identity.Scaled(new Vector3(size.X, height, size.Y)),
            new Vector3(center.X, ground, center.Y)));
        SetArmorTint(batch, index, ArmyRenderer.FactionColor(faction));
        batch.VisibleInstanceCount = Mathf.Max(batch.VisibleInstanceCount, index + 1);
    }

    private void HideShelter(int index)
    {
        // Only placement/reuse/destruction paths call this, not simulation ticks.
        foreach (var batch in shelterBatches) batch?.SetInstanceTransform(index, Hidden);
    }

    // Three lazy shared batches, with both barrels baked into the pitched assembly.
    private static void ResetMinigunBatch(MultiMesh batch)
    {
        if (batch == null) return;
        for (int i = 0; i < batch.VisibleInstanceCount; i++) batch.SetInstanceTransform(i, Hidden);
        batch.VisibleInstanceCount = 0;
    }

    private MultiMesh CreateMinigunBatch(Mesh mesh)
    {
        MultiMesh batch = Batch(mesh, DeploymentLayout.MaxBuildings, TwoToneTowerMaterial, useCustomData: true);
        for (int i = 0; i < batch.InstanceCount; i++) batch.SetInstanceTransform(i, Hidden);
        batch.VisibleInstanceCount = 0;
        return batch;
    }

    private void ShowMinigun(int index, ObstacleRectangle area, byte faction, float height, float ground)
    {
        minigunBase ??= CreateMinigunBatch(minigunBaseMesh ??= TwinMinigunTowerMeshes.CreateBase());
        minigunHead ??= CreateMinigunBatch(minigunHeadMesh ??= TwinMinigunTowerMeshes.CreateHead());
        minigunBarrels ??= CreateMinigunBatch(minigunBarrelMesh ??= TwinMinigunTowerMeshes.CreateBarrels());
        Vector2 size = area.Max - area.Min;
        Vector3 pivot = Pivot(area, height) + Vector3.Up * ground;
        minigunBase.SetInstanceTransform(index, new Transform3D(Basis.Identity.Scaled(new Vector3(size.X, height, size.Y)),
            new Vector3(pivot.X, ground + height * 0.5f, pivot.Z)));
        Color color = ArmyRenderer.FactionColor(faction);
        SetArmorTint(minigunBase, index, color);
        SetArmorTint(minigunHead, index, color);
        SetArmorTint(minigunBarrels, index, color);
        Aim(index, pivot, 0f);
        int count = Mathf.Max(minigunBase.VisibleInstanceCount, index + 1);
        minigunBase.VisibleInstanceCount = minigunHead.VisibleInstanceCount = minigunBarrels.VisibleInstanceCount = count;
    }

    private void HideMinigun(int index)
    {
        minigunBase?.SetInstanceTransform(index, Hidden);
        minigunHead?.SetInstanceTransform(index, Hidden);
        minigunBarrels?.SetInstanceTransform(index, Hidden);
    }

    internal static Vector3 MinigunMuzzle(Vector3 pivot, float yaw, float slope, int side) =>
        pivot + RailBarrelBasis(new Basis(Vector3.Up, yaw), slope) *
            new Vector3(side == 0 ? -TwinMinigunTowerMeshes.BarrelOffset : TwinMinigunTowerMeshes.BarrelOffset,
                0f, -TwinMinigunTowerMeshes.BarrelLength);

    internal void HideTower(int index)
    {
        var hidden = new Transform3D(Basis.Identity.Scaled(Vector3.Zero), Vector3.Zero);
        HideShelter(index);
        HideMinigun(index);
        bases.SetInstanceTransform(index, hidden);
        heads.SetInstanceTransform(index, hidden);
        barrels.SetInstanceTransform(index, hidden);
        rocketBases.SetInstanceTransform(index, hidden);
        rocketHeads.SetInstanceTransform(index, hidden);
        rocketBarrels.SetInstanceTransform(index, hidden);
        rightRocketBarrels.SetInstanceTransform(index, hidden);
        shieldTowers?.SetInstanceTransform(index, Hidden);
        shieldDomes?.Hide(index);
        HideBeam(index);
    }

    public void Aim(int index, Vector3 pivot, float yaw, float leftRecoil = 0f, float rightRecoil = 0f, float beamSlope = 0f)
    {
        Basis basis = new Basis(Vector3.Up, yaw);
        if (minigunSlots[index])
        {
            minigunHead.SetInstanceTransform(index, new Transform3D(basis, pivot));
            minigunBarrels.SetInstanceTransform(index, new Transform3D(RailBarrelBasis(basis, beamSlope), pivot));
        }
        else if (rocketSlots[index])
        {
            rocketHeads.SetInstanceTransform(index, new Transform3D(basis, pivot));
            rocketBarrels.SetInstanceTransform(index, RocketBarrelTransform(pivot, basis, 0, leftRecoil));
            rightRocketBarrels.SetInstanceTransform(index, RocketBarrelTransform(pivot, basis, 1, rightRecoil));
        }
        else
        {
            heads.SetInstanceTransform(index, new Transform3D(basis, pivot));
            Basis barrelBasis = RailBarrelBasis(basis, beamSlope);
            barrels.SetInstanceTransform(index, new Transform3D(barrelBasis,
                pivot + barrelBasis * new Vector3(0, 0, leftRecoil - BarrelCenterForward)));
        }
    }

    internal void ShowBeam(int index, Vector3 from, Vector3 to, byte faction, Vector3? impact)
    {
        Vector3 direction = to - from;
        float length = direction.Length();
        if (beams == null) return;
        if (length < 0.001f) { HideBeam(index); return; }
        Vector3 up = Mathf.Abs(direction.Y) / length > 0.98f ? Vector3.Right : Vector3.Up;
        Basis basis = Basis.LookingAt(direction, up) * new Basis(new Vector3(0.92f, 0, 0), Vector3.Up, new Vector3(0, 0, length));
        int slot = beamSlots[index];
        if (slot < 0)
        {
            slot = liveBeams++;
            beamSlots[index] = slot;
            beamOwners[slot] = index;
            Color tint = Factions.Color(faction);
            beams.SetInstanceColor(slot, tint);
            beamGlows.SetInstanceColor(slot * 2, tint);
            beamGlows.SetInstanceColor(slot * 2 + 1, tint);
            beamGlows.SetInstanceCustomData(slot * 2, new Color(1f, 0f, 0f, 0f));
            beamGlows.VisibleInstanceCount = liveBeams * 2;
            beams.VisibleInstanceCount = liveBeams;
        }
        beams.SetInstanceTransform(slot, new Transform3D(basis, (from + to) * 0.5f));
        beamGlows.SetInstanceTransform(slot * 2, new Transform3D(Basis.Identity, from));
        beamGlows.SetInstanceCustomData(slot * 2 + 1, new Color(2f, impact.HasValue ? 1f : 0f, 0f, 0f));
        beamGlows.SetInstanceTransform(slot * 2 + 1, new Transform3D(Basis.Identity,
            (impact ?? to) - direction * (0.15f / length)));
    }

    internal void HideBeam(int index)
    {
        if (beams == null || beamSlots[index] < 0) return;
        int slot = beamSlots[index], last = --liveBeams;
        beamSlots[index] = -1;
        if (slot != last)
        {
            int moved = beamOwners[last];
            beamOwners[slot] = moved;
            beamSlots[moved] = slot;
            beams.SetInstanceTransform(slot, beams.GetInstanceTransform(last));
            beams.SetInstanceColor(slot, beams.GetInstanceColor(last));
            for (int i = 0; i < 2; i++)
            {
                int destination = slot * 2 + i, source = last * 2 + i;
                beamGlows.SetInstanceTransform(destination, beamGlows.GetInstanceTransform(source));
                beamGlows.SetInstanceColor(destination, beamGlows.GetInstanceColor(source));
                beamGlows.SetInstanceCustomData(destination, beamGlows.GetInstanceCustomData(source));
            }
        }
        beams.VisibleInstanceCount = liveBeams;
        beamGlows.VisibleInstanceCount = liveBeams * 2;
    }

    // =========================================================
    internal void EmitFlash(Vector3 muzzle, float yaw, float size = 1f)
    {
        if (flashes == null || liveFlashes >= FlashCapacity) return;
        int i = liveFlashes++;
        flashPose[i] = new Transform3D(new Basis(Vector3.Up, yaw).Scaled(Vector3.One * size), muzzle + Vector3.Up * 0.11f);
        flashTime[i] = FlashSeconds;
        WriteFlash(i);
        flashes.VisibleInstanceCount = liveFlashes;
    }

    // Brief broad glow at the actual firing muzzle's XZ position. Reuse the
    // projectile glow shader with an independent stronger intensity, not a light.
    internal void EmitGroundFlash(Vector3 muzzle, byte faction, TerrainHeightField terrain)
    {
        if (liveGroundFlashes >= GroundFlashCapacity) return;
        if (groundFlashes == null)
        {
            var material = new ShaderMaterial
            {
                Shader = GD.Load<Shader>("res://GAMEPLAY/PROJECTILES/Shared/ProjectileGroundGlow.gdshader")
            };
            material.SetShaderParameter("intensity", 0.72f);
            groundFlashes = Batch(new PlaneMesh { Size = new Vector2(7f, 7f) },
                GroundFlashCapacity, material);
            groundFlashes.VisibleInstanceCount = 0;
        }

        float height = terrain?.At(muzzle.X, muzzle.Z) ?? 0f;
        // Sample the local slope only at launch; the flash remains stationary.
        float dx = 0f, dz = 0f;
        if (terrain != null)
        {
            dx = (terrain.At(muzzle.X + 1f, muzzle.Z) - terrain.At(muzzle.X - 1f, muzzle.Z)) * 0.5f;
            dz = (terrain.At(muzzle.X, muzzle.Z + 1f) - terrain.At(muzzle.X, muzzle.Z - 1f)) * 0.5f;
        }
        Vector3 normal = new Vector3(-dx, 1f, -dz).Normalized();
        Basis basis = Basis.LookingAt(new Vector3(0f, -dz, -1f), normal);
        int slot = liveGroundFlashes++;
        groundFlashPose[slot] = new Transform3D(basis,
            new Vector3(muzzle.X, height + 0.09f, muzzle.Z));
        groundFlashTint[slot] = Factions.Color(faction).Lerp(Colors.White, 0.12f);
        groundFlashTime[slot] = GroundFlashSeconds;
        WriteGroundFlash(slot);
        groundFlashes.VisibleInstanceCount = liveGroundFlashes;
    }

    private void WriteGroundFlash(int slot)
    {
        float remaining = Mathf.Clamp(groundFlashTime[slot] / GroundFlashSeconds, 0f, 1f);
        float scale = 1.12f - 0.20f * remaining;
        Transform3D pose = groundFlashPose[slot];
        groundFlashes.SetInstanceTransform(slot, new Transform3D(
            pose.Basis.Scaled(Vector3.One * scale), pose.Origin));
        Color tint = groundFlashTint[slot];
        tint.A = remaining * remaining;
        groundFlashes.SetInstanceColor(slot, tint);
    }

    private void AdvanceGroundFlashes(float dt)
    {
        if (liveGroundFlashes == 0) return;
        int before = liveGroundFlashes;
        for (int i = liveGroundFlashes - 1; i >= 0; i--)
        {
            groundFlashTime[i] -= dt;
            if (groundFlashTime[i] > 0f)
            {
                WriteGroundFlash(i);
                continue;
            }
            liveGroundFlashes--;
            if (i == liveGroundFlashes) continue;
            groundFlashTime[i] = groundFlashTime[liveGroundFlashes];
            groundFlashPose[i] = groundFlashPose[liveGroundFlashes];
            groundFlashTint[i] = groundFlashTint[liveGroundFlashes];
            WriteGroundFlash(i);
        }
        if (before != liveGroundFlashes) groundFlashes.VisibleInstanceCount = liveGroundFlashes;
    }

    public void Advance(float dt)
    {
        AdvanceGroundFlashes(dt);
        int before = liveFlashes;
        for (int i = liveFlashes - 1; i >= 0; i--)
        {
            flashTime[i] -= dt;
            if (flashTime[i] > 0f)
            {
                WriteFlash(i);
                continue;
            }
            liveFlashes--;
            if (i == liveFlashes) continue;
            flashTime[i] = flashTime[liveFlashes];
            flashPose[i] = flashPose[liveFlashes];
            WriteFlash(i);
        }
        if (liveFlashes != before) flashes.VisibleInstanceCount = liveFlashes;
    }

    // Only live flashes are updated; scale and opacity fall off over FlashSeconds.
    private void WriteFlash(int i)
    {
        float remaining = Mathf.Clamp(flashTime[i] / FlashSeconds, 0f, 1f);
        float scale = 0.4f + remaining * 0.6f;
        Transform3D pose = flashPose[i];
        flashes.SetInstanceTransform(i, new Transform3D(
            pose.Basis.Scaled(new Vector3(scale, 1f, scale)), pose.Origin));
        flashes.SetInstanceColor(i, new Color(1f, 1f, 1f, remaining));
    }

}
