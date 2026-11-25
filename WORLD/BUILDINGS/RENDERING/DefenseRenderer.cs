// Batches faceted platforms, enlarged rotating turret heads and sustained railgun beams without per-shot nodes.
using System;
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

public partial class DefenseRenderer : Node3D
{
    // Keep the railgun beam visible independently of battlefield lighting.
    private static readonly StandardMaterial3D Material = new()
    {
        VertexColorUseAsAlbedo = true, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
    };
    // Preserve the existing extended barrel and its laser/muzzle endpoint.
    private const float BarrelLength = 3.8f;
    private const float BarrelCenterForward = BarrelLength * 0.5f;
    private const float FlashSeconds = 0.11f;
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
    private static readonly Mesh BeamMesh = new BoxMesh { Size = Vector3.One };
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
    private MultiMesh bases, heads, barrels, rocketBases, rocketHeads, rocketBarrels, rightRocketBarrels, beams, flashes;
    private readonly int[] crackStages = new int[DeploymentLayout.MaxBuildings];
    private readonly bool[] rocketSlots = new bool[DeploymentLayout.MaxBuildings];
    private readonly int[] beamSlots = new int[DeploymentLayout.MaxBuildings];
    private readonly int[] beamOwners = new int[DeploymentLayout.MaxBuildings];
    private int liveBeams, liveFlashes;
    private MultiMesh shieldTowers;
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
        shieldDomes?.Clear();
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
                catalog[placement.Type].ShieldEmitter.Radius);
        }
        if (battle)
        {
            beams ??= Batch(BeamMesh, DeploymentLayout.MaxBuildings, Material);
            Array.Fill(beamSlots, -1);
            flashes ??= Batch(FlashMesh, FlashCapacity, FlashMaterial);
            beams.VisibleInstanceCount = flashes.VisibleInstanceCount = 0;
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
        bool rocket = false, bool shield = false, float shieldRadius = 0f)
    {
        crackStages[index] = 0;
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
        MultiMesh batch = shield ? shieldTowers : rocketSlots[index] ? rocketBases : bases;
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

    internal void HideTower(int index)
    {
        var hidden = new Transform3D(Basis.Identity.Scaled(Vector3.Zero), Vector3.Zero);
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
        if (rocketSlots[index])
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

    internal void ShowBeam(int index, Vector3 from, Vector3 to, byte faction)
    {
        Vector3 direction = to - from;
        float length = direction.Length();
        if (beams == null) return;
        if (length < 0.001f) { HideBeam(index); return; }
        Vector3 up = Mathf.Abs(direction.Y) / length > 0.98f ? Vector3.Right : Vector3.Up;
        Basis basis = Basis.LookingAt(direction, up) * new Basis(new Vector3(0.28f, 0, 0), new Vector3(0, 0.28f, 0), new Vector3(0, 0, length));
        int slot = beamSlots[index];
        if (slot < 0)
        {
            slot = liveBeams++;
            beamSlots[index] = slot;
            beamOwners[slot] = index;
            beams.SetInstanceColor(slot, Factions.Color(faction).Lightened(0.55f));
            beams.VisibleInstanceCount = liveBeams;
        }
        beams.SetInstanceTransform(slot, new Transform3D(basis, (from + to) * 0.5f));
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
        }
        beams.VisibleInstanceCount = liveBeams;
    }

    // =========================================================
    internal void EmitFlash(Vector3 muzzle, float yaw)
    {
        if (flashes == null || liveFlashes >= FlashCapacity) return;
        int i = liveFlashes++;
        flashPose[i] = new Transform3D(new Basis(Vector3.Up, yaw), muzzle + Vector3.Up * 0.11f);
        flashTime[i] = FlashSeconds;
        WriteFlash(i);
        flashes.VisibleInstanceCount = liveFlashes;
    }

    public void Advance(float dt)
    {
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

    // Only live flashes are updated; scale and opacity fall off over 0.11 seconds.
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
