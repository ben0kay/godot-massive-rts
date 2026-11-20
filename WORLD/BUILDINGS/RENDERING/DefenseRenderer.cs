// Batches faceted platforms, enlarged rotating turret heads and short laser pulses without per-shot nodes.
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
    private static readonly Mesh RocketBaseMesh = BuildRocketBaseMesh();
    private static readonly Mesh RocketHeadMesh = BuildRocketHeadMesh();
    private static readonly Mesh RocketBarrelMesh = BuildRocketBarrelsMesh();
    private static readonly Mesh BeamMesh = new BoxMesh { Size = Vector3.One };
    private static readonly Mesh FlashMesh = BuildFlashMesh();
    private static readonly StandardMaterial3D FlashMaterial = new()
    {
        VertexColorUseAsAlbedo = true,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled
    };
    private const int BeamCapacity = 256;
    private const int FlashCapacity = 256;
    private readonly float[] beamTime = new float[BeamCapacity];
    private readonly Transform3D[] beamPose = new Transform3D[BeamCapacity];
    private readonly Color[] beamColor = new Color[BeamCapacity];
    private readonly float[] flashTime = new float[FlashCapacity];
    private readonly Transform3D[] flashPose = new Transform3D[FlashCapacity];
    private MultiMesh bases, heads, barrels, rocketBases, rocketHeads, rocketBarrels, beams, flashes;
    private readonly bool[] rocketSlots = new bool[DeploymentLayout.MaxBuildings];
    private int liveBeams, liveFlashes;

    // =========================================================
    // Keep the laser origin exactly on the protruding barrel's forward end.
    public static Vector3 Muzzle(Vector3 pivot, float yaw) =>
        pivot + new Vector3(0f, 0f, -BarrelLength).Rotated(Vector3.Up, yaw);

    // The twin muzzle ends lie beyond the 3x3 footprint even at a diagonal yaw,
    // preventing the arcing projectiles from immediately hitting their own tower.
    public static Vector3 RocketMuzzle(Vector3 pivot, float yaw, int side) =>
        pivot + new Vector3(side == 0 ? -0.7f : 0.7f, 0.9f, -4.7f).Rotated(Vector3.Up, yaw);

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
    // remains at the same world position used by Fire() and EmitFlash().
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

    // =========================================================
    // Shared heavyweight armor and twin launch tubes. The wider base remains a
    // single faceted MultiMesh instance, while only its head and barrels rotate.
    private static Mesh BuildRocketBaseMesh()
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetMaterial(Material);
        FacetedSoldierMeshes.AppendBeveledRectangle(surface, new Vector3(0f, -0.43f, 0f),
            new Vector3(1f, 0.30f, 1f), new Color(0.46f, 0.46f, 0.46f));
        FacetedSoldierMeshes.AppendBeveledRectangle(surface, new Vector3(0f, -0.05f, 0f),
            new Vector3(0.95f, 0.81f, 0.95f), new Color(0.78f, 0.78f, 0.78f));
        FacetedSoldierMeshes.AppendBeveledRectangle(surface, new Vector3(0f, 0.40f, 0f),
            new Vector3(0.82f, 0.16f, 0.82f), Colors.White);
        for (int side = -1; side <= 1; side += 2)
            FacetedSoldierMeshes.AppendBeveledRectangle(surface,
                new Vector3(side * 0.38f, 0.50f, 0f),
                new Vector3(0.15f, 0.07f, 0.52f), new Color(0.68f, 0.77f, 0.89f));
        return surface.Commit();
    }

    private static Mesh BuildRocketHeadMesh()
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetMaterial(Material);
        FacetedSoldierMeshes.AppendBeveledRectangle(surface, new Vector3(0f, -0.17f, 0.18f),
            new Vector3(2.65f, 0.30f, 2.20f), new Color(0.56f, 0.56f, 0.56f));
        FacetedSoldierMeshes.AppendBeveledRectangle(surface, new Vector3(0f, 0.18f, 0.20f),
            new Vector3(2.16f, 0.50f, 1.62f), new Color(0.89f, 0.89f, 0.89f));
        for (int side = -1; side <= 1; side += 2)
        {
            FacetedSoldierMeshes.AppendBeveledRectangle(surface,
                new Vector3(side * 0.70f, 0.52f, -0.60f),
                new Vector3(0.92f, 0.76f, 1.66f), Colors.White);
            FacetedSoldierMeshes.AppendBeveledRectangle(surface,
                new Vector3(side * 0.70f, 0.95f, -0.62f),
                new Vector3(0.67f, 0.07f, 1.20f), new Color(0.75f, 0.84f, 0.94f));
        }
        return surface.Commit();
    }

    // Local tube tips end at the exact RocketMuzzle offsets.
    private static Mesh BuildRocketBarrelsMesh()
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetMaterial(Material);
        for (int side = -1; side <= 1; side += 2)
        {
            FacetedSoldierMeshes.AppendBeveledRectangle(surface,
                new Vector3(side * 0.70f, 0.90f, -3.12f),
                new Vector3(0.37f, 0.31f, 3.16f), new Color(0.84f, 0.84f, 0.84f));
            FacetedSoldierMeshes.AppendBeveledRectangle(surface,
                new Vector3(side * 0.70f, 0.90f, -4.48f),
                new Vector3(0.54f, 0.42f, 0.44f), Colors.White);
        }
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

    private MultiMesh Batch(Mesh mesh, int capacity, Material material = null)
    {
        var batch = new MultiMesh
        {
            Mesh = mesh, TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true,
            InstanceCount = capacity
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
        bases.VisibleInstanceCount = heads.VisibleInstanceCount = barrels.VisibleInstanceCount =
            rocketBases.VisibleInstanceCount = rocketHeads.VisibleInstanceCount = rocketBarrels.VisibleInstanceCount = placements.Count;
        for (int i = 0; i < placements.Count; i++)
        {
            var placement = placements[i];
            var area = placement.Footprint(catalog);
            ShowTower(i, area, placement.Faction, height, terrain?.FootprintHeight(area.Min, area.Max, out _) ?? 0f,
                catalog[placement.Type].ProjectileType >= 0);
        }
        if (battle)
        {
            beams ??= Batch(BeamMesh, BeamCapacity);
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
        rocketBases ??= Batch(RocketBaseMesh, DeploymentLayout.MaxBuildings);
        rocketHeads ??= Batch(RocketHeadMesh, DeploymentLayout.MaxBuildings);
        rocketBarrels ??= Batch(RocketBarrelMesh, DeploymentLayout.MaxBuildings);
    }

    internal void ShowTower(int index, ObstacleRectangle area, byte faction, float height, float ground = 0f,
        bool rocket = false)
    {
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
        activeBase.SetInstanceTransform(index, new Transform3D(Basis.Identity.Scaled(new Vector3(size.X, height, size.Y)),
            new Vector3(pivot.X, ground + height * 0.5f, pivot.Z)));
        activeBase.SetInstanceColor(index, color.Darkened(rocket ? 0.16f : 0.08f));
        activeHead.SetInstanceColor(index, color);
        activeBarrels.SetInstanceColor(index, color.Lightened(rocket ? 0.10f : 0.22f));
        Aim(index, pivot, 0f);
        int count = Mathf.Max(bases.VisibleInstanceCount, index + 1);
        bases.VisibleInstanceCount = heads.VisibleInstanceCount = barrels.VisibleInstanceCount =
            rocketBases.VisibleInstanceCount = rocketHeads.VisibleInstanceCount =
                rocketBarrels.VisibleInstanceCount = count;
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
    }

    public void Aim(int index, Vector3 pivot, float yaw)
    {
        Basis basis = new Basis(Vector3.Up, yaw);
        if (rocketSlots[index])
        {
            rocketHeads.SetInstanceTransform(index, new Transform3D(basis, pivot));
            rocketBarrels.SetInstanceTransform(index, new Transform3D(basis, pivot));
        }
        else
        {
            heads.SetInstanceTransform(index, new Transform3D(basis, pivot));
            barrels.SetInstanceTransform(index, new Transform3D(basis, pivot + basis * new Vector3(0, 0, -BarrelCenterForward)));
        }
    }

    public void Fire(Vector3 from, Vector3 to, byte faction, float duration, float yaw)
    {
        EmitFlash(from, yaw);
        Vector3 direction = to - from;
        float length = direction.Length();
        if (liveBeams == BeamCapacity || length < 0.001f) return;
        Vector3 up = Mathf.Abs(direction.Y) / length > 0.98f ? Vector3.Right : Vector3.Up;
        Basis basis = Basis.LookingAt(direction, up) * new Basis(new Vector3(0.055f, 0, 0), new Vector3(0, 0.055f, 0), new Vector3(0, 0, length));
        int i = liveBeams++;
        beamPose[i] = new Transform3D(basis, (from + to) * 0.5f);
        beamColor[i] = ArmyRenderer.FactionColor(faction).Lightened(0.45f);
        beamTime[i] = duration;
        WriteBeam(i);
        beams.VisibleInstanceCount = liveBeams;
    }

    // =========================================================
    private void EmitFlash(Vector3 muzzle, float yaw)
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
        int before = liveBeams;
        for (int i = liveBeams - 1; i >= 0; i--)
        {
            beamTime[i] -= dt;
            if (beamTime[i] > 0f) continue;
            liveBeams--;
            if (i == liveBeams) continue;
            beamTime[i] = beamTime[liveBeams];
            beamPose[i] = beamPose[liveBeams];
            beamColor[i] = beamColor[liveBeams];
            WriteBeam(i);
        }
        if (liveBeams != before) beams.VisibleInstanceCount = liveBeams;

        before = liveFlashes;
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

    private void WriteBeam(int i)
    {
        beams.SetInstanceTransform(i, beamPose[i]);
        beams.SetInstanceColor(i, beamColor[i]);
    }
}
