// Batches faceted platforms, enlarged rotating turret heads and sustained railgun beams without per-shot nodes.
using System;
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

public partial class DefenseRenderer : Node3D
{
    // Reuse the soldier meshes' baked faux lighting style; no real lights or shaders needed.
    private static readonly StandardMaterial3D Material = new()
    {
        VertexColorUseAsAlbedo = true, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
    };
    // Preserve the existing extended barrel and its laser/muzzle endpoint.
    private const float BarrelLength = 3.8f;
    private const float BarrelCenterForward = BarrelLength * 0.5f;
    private const float FlashSeconds = 0.11f;
    private static readonly Mesh BaseMesh = BuildBaseMesh();
    private static readonly Mesh HeadMesh = BuildHeadMesh();
    private static readonly Mesh BarrelMesh = BuildBarrelMesh();
    private static readonly ShaderMaterial RocketMaterial = new()
    {
        Shader = GD.Load<Shader>("res://WORLD/BUILDINGS/RENDERING/BroadsideArmor.gdshader")
    };
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
    private readonly bool[] rocketSlots = new bool[DeploymentLayout.MaxBuildings];
    private readonly int[] beamSlots = new int[DeploymentLayout.MaxBuildings];
    private readonly int[] beamOwners = new int[DeploymentLayout.MaxBuildings];
    private int liveBeams, liveFlashes;
    private MultiMesh shieldTowers;
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

    // =========================================================
    // Shared beveled chassis: darker outer plinth, recessed side faces, bright inset
    // top platform. Geometry is normalized and scaled to the authored footprint.
    private static Mesh BuildBaseMesh()
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetMaterial(Material);
        FacetedSoldierMeshes.AppendBeveledRectangle(surface, new Vector3(0f, -0.41f, 0f),
            new Vector3(1f, 0.18f, 1f), new Color(0.52f, 0.52f, 0.52f));
        FacetedSoldierMeshes.AppendBeveledRectangle(surface, new Vector3(0f, 0.04f, 0f),
            new Vector3(0.94f, 0.74f, 0.94f), new Color(0.80f, 0.80f, 0.80f));
        FacetedSoldierMeshes.AppendBeveledRectangle(surface, new Vector3(0f, 0.43f, 0f),
            new Vector3(0.82f, 0.16f, 0.82f), new Color(0.98f, 0.98f, 0.98f));
        return surface.Commit();
    }

    // Wider and taller beveled triangular head with a dark mounting collar.
    // Side-face tones follow the fake northwest light used by soldier meshes.
    private static Mesh BuildHeadMesh()
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetMaterial(Material);
        FacetedSoldierMeshes.AppendBeveledRectangle(surface, new Vector3(0f, -0.30f, 0f),
            new Vector3(1.75f, 0.16f, 1.75f), new Color(0.55f, 0.55f, 0.55f));

        Vector2[] outline = { new(0f, -1.22f), new(1.10f, 0.70f), new(-1.10f, 0.70f) };
        const float bottomY = -0.26f, shoulderY = 0.27f, topY = 0.44f;
        var bottom = new Vector3[3];
        var shoulder = new Vector3[3];
        var top = new Vector3[3];
        for (int i = 0; i < 3; i++)
        {
            Vector2 corner = outline[i];
            bottom[i] = new Vector3(corner.X, bottomY, corner.Y);
            shoulder[i] = new Vector3(corner.X, shoulderY, corner.Y);
            top[i] = new Vector3(corner.X * 0.82f, topY, corner.Y * 0.82f);
        }
        for (int i = 0; i < 3; i++)
        {
            int next = (i + 1) % 3;
            Vector3 edge = shoulder[next] - shoulder[i];
            Vector3 outward = new Vector3(edge.Z, 0f, -edge.X).Normalized();
            float side = Mathf.Clamp(0.73f - 0.10f * outward.X - 0.12f * outward.Z, 0.58f, 0.86f);
            float bevel = Mathf.Lerp(side, 1f, 0.58f);
            // Match the clockwise front-face convention from FacetedSoldierMeshes.
            AddFacet(surface, Vector3.Up * topY, top[next], top[i], Colors.White, Vector3.Up);
            AddFacetQuad(surface, bottom[i], shoulder[i], shoulder[next], bottom[next],
                new Color(side, side, side), outward);
            Vector3 slopeNormal = (outward * (topY - shoulderY) + Vector3.Up * 0.18f).Normalized();
            AddFacetQuad(surface, shoulder[i], top[i], top[next], shoulder[next],
                new Color(bevel, bevel, bevel), slopeNormal);
        }
        return surface.Commit();
    }

    // Faceted barrel and muzzle sleeve are baked into one shared mesh; the tip
    // remains at the same world position used by ShowBeam() and EmitFlash().
    private static Mesh BuildBarrelMesh()
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetMaterial(Material);
        FacetedSoldierMeshes.AppendBeveledRectangle(surface, Vector3.Zero,
            new Vector3(0.28f, 0.24f, BarrelLength), new Color(0.91f, 0.91f, 0.91f));
        FacetedSoldierMeshes.AppendBeveledRectangle(surface,
            new Vector3(0f, 0f, -BarrelCenterForward + 0.16f),
            new Vector3(0.38f, 0.29f, 0.32f), Colors.White);
        return surface.Commit();
    }

    private static void AddFacetQuad(SurfaceTool surface, Vector3 a, Vector3 b,
        Vector3 c, Vector3 d, Color tint, Vector3 normal)
    {
        AddFacet(surface, a, b, c, tint, normal);
        AddFacet(surface, a, c, d, tint, normal);
    }

    private static void AddFacet(SurfaceTool surface, Vector3 a, Vector3 b, Vector3 c,
        Color tint, Vector3 normal)
    {
        surface.SetNormal(normal);
        surface.SetColor(tint);
        surface.AddVertex(a);
        surface.AddVertex(c);
        surface.AddVertex(b);
    }

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
            Multimesh = batch, MaterialOverride = material ?? Material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
        return batch;
    }

    // =========================================================
    public void Initialize(IReadOnlyList<BuildingPlacement> placements, RuntimeBuildingCatalog catalog, float height, bool battle, TerrainHeightField terrain = null)
    {
        InitializeBatches();
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
                catalog[placement.Type].ProjectileType >= 0, catalog[placement.Type].ShieldEmitter.Enabled);
        }
        if (battle)
        {
            beams ??= Batch(BeamMesh, DeploymentLayout.MaxBuildings);
            Array.Fill(beamSlots, -1);
            flashes ??= Batch(FlashMesh, FlashCapacity, FlashMaterial);
            beams.VisibleInstanceCount = flashes.VisibleInstanceCount = 0;
            liveBeams = liveFlashes = 0;
        }
    }

    private void InitializeBatches()
    {
        bases ??= Batch(BaseMesh, DeploymentLayout.MaxBuildings);
        heads ??= Batch(HeadMesh, DeploymentLayout.MaxBuildings);
        barrels ??= Batch(BarrelMesh, DeploymentLayout.MaxBuildings);
        rocketBases ??= Batch(RocketBaseMesh, DeploymentLayout.MaxBuildings, RocketMaterial, useCustomData: true);
        rocketHeads ??= Batch(RocketHeadMesh, DeploymentLayout.MaxBuildings, RocketMaterial, useCustomData: true);
        rocketBarrels ??= Batch(RocketBarrelMesh, DeploymentLayout.MaxBuildings, RocketMaterial, useCustomData: true);
        rightRocketBarrels ??= Batch(RocketBarrelMesh, DeploymentLayout.MaxBuildings, RocketMaterial, useCustomData: true);
    }

    internal void ShowTower(int index, ObstacleRectangle area, byte faction, float height, float ground = 0f,
        bool rocket = false, bool shield = false)
    {
        if (shield)
        {
            HideTower(index);
            ShowShieldTower(index, area, faction, height, ground);
            return;
        }
        shieldTowers?.SetInstanceTransform(index, Hidden);
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
        if (rocket)
        {
            // Keep the baked material mask intact; the faction tint travels separately.
            SetRocketTint(rocketBases, index, color);
            SetRocketTint(rocketHeads, index, color);
            SetRocketTint(rocketBarrels, index, color);
            SetRocketTint(rightRocketBarrels, index, color);
        }
        else
        {
            activeBase.SetInstanceColor(index, color.Darkened(0.08f));
            activeHead.SetInstanceColor(index, color);
            activeBarrels.SetInstanceColor(index, color.Lightened(0.22f));
        }
        Aim(index, pivot, 0f);
        int count = Mathf.Max(bases.VisibleInstanceCount, index + 1);
        bases.VisibleInstanceCount = heads.VisibleInstanceCount = barrels.VisibleInstanceCount =
            rocketBases.VisibleInstanceCount = rocketHeads.VisibleInstanceCount =
                rocketBarrels.VisibleInstanceCount = rightRocketBarrels.VisibleInstanceCount = count;
    }

    // =========================================================
    // Set once on placement/reuse, not during aiming or recoil.
    private static void SetRocketTint(MultiMesh batch, int index, Color faction)
    {
        batch.SetInstanceColor(index, Colors.White);
        batch.SetInstanceCustomData(index, faction);
    }

    private void ShowShieldTower(int index, ObstacleRectangle area, byte faction, float height, float ground)
    {
        if (shieldTowers == null)
        {
            using var surface = new SurfaceTool();
            surface.Begin(Mesh.PrimitiveType.Triangles);
            surface.SetMaterial(Material);
            FacetedSoldierMeshes.AppendBeveledRectangle(surface, new Vector3(0f, 0.26f, 0f),
                new Vector3(0.95f, 0.52f, 0.95f), new Color(0.52f, 0.63f, 0.67f));
            FacetedSoldierMeshes.AppendBeveledRectangle(surface, new Vector3(0f, 0.63f, 0f),
                new Vector3(0.74f, 0.29f, 0.74f), new Color(0.79f, 0.93f, 0.96f));
            FacetedSoldierMeshes.AppendBeveledRectangle(surface, new Vector3(0f, 0.97f, 0f),
                new Vector3(0.31f, 0.58f, 0.31f), new Color(0.78f, 1f, 1f));
            for (int side = -1; side <= 1; side += 2)
                FacetedSoldierMeshes.AppendBeveledRectangle(surface, new Vector3(side * 0.29f, 0.83f, 0f),
                    new Vector3(0.13f, 0.53f, 0.37f), new Color(0.50f, 0.65f, 0.71f));
            shieldTowers = Batch(surface.Commit(), DeploymentLayout.MaxBuildings);
            for (int i = 0; i < shieldTowers.InstanceCount; i++) shieldTowers.SetInstanceTransform(i, Hidden);
            shieldTowers.VisibleInstanceCount = 0;
        }
        Vector2 size = area.Max - area.Min;
        Vector3 pivot = Pivot(area, height);
        shieldTowers.SetInstanceTransform(index, new Transform3D(
            Basis.Identity.Scaled(new Vector3(size.X, height, size.Y)), new Vector3(pivot.X, ground, pivot.Z)));
        shieldTowers.SetInstanceColor(index, Factions.Color(faction).Lightened(0.24f));
        shieldTowers.VisibleInstanceCount = Mathf.Max(shieldTowers.VisibleInstanceCount, index + 1);
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
