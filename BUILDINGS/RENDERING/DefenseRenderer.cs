// Batches fixed bases, rotating heads and short laser pulses without per-shot nodes.
using System;
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

public partial class DefenseRenderer : Node3D
{
    private static readonly Mesh BaseMesh = new BoxMesh { Size = Vector3.One };
    private static readonly Mesh HeadMesh = new CylinderMesh { RadialSegments = 3, Rings = 0, Height = 0.45f, TopRadius = 0.68f, BottomRadius = 0.75f };
    private static readonly Mesh BarrelMesh = new BoxMesh { Size = new Vector3(0.18f, 0.16f, 1.25f) };
    private static readonly Mesh BeamMesh = new BoxMesh { Size = Vector3.One };
    private static readonly StandardMaterial3D Material = new()
    {
        VertexColorUseAsAlbedo = true, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
    };
    private const int BeamCapacity = 256;
    private readonly float[] beamTime = new float[BeamCapacity];
    private readonly Transform3D[] beamPose = new Transform3D[BeamCapacity];
    private readonly Color[] beamColor = new Color[BeamCapacity];
    private MultiMesh bases, heads, barrels, beams;
    private int liveBeams;

    public static Vector3 Muzzle(Vector3 pivot, float yaw) =>
        pivot + new Vector3(0f, 0f, -1.45f).Rotated(Vector3.Up, yaw);

    private MultiMesh Batch(Mesh mesh, int capacity)
    {
        var batch = new MultiMesh
        {
            Mesh = mesh, TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true,
            InstanceCount = capacity
        };
        AddChild(new MultiMeshInstance3D
        {
            Multimesh = batch, MaterialOverride = Material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
        return batch;
    }

    // =========================================================
    public void Initialize(IReadOnlyList<BuildingPlacement> placements, RuntimeBuildingCatalog catalog, float height, bool battle)
    {
        bases ??= Batch(BaseMesh, DeploymentLayout.MaxBuildings);
        heads ??= Batch(HeadMesh, DeploymentLayout.MaxBuildings);
        barrels ??= Batch(BarrelMesh, DeploymentLayout.MaxBuildings);
        bases.VisibleInstanceCount = heads.VisibleInstanceCount = barrels.VisibleInstanceCount = placements.Count;
        for (int i = 0; i < placements.Count; i++)
        {
            var placement = placements[i];
            var area = placement.Footprint(catalog);
            Vector2 size = area.Max - area.Min;
            Vector3 pivot = Pivot(area, height);
            Color color = ArmyRenderer.FactionColor(placement.Faction);
            bases.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(new Vector3(size.X, height, size.Y)),
                new Vector3(pivot.X, height * 0.5f, pivot.Z)));
            bases.SetInstanceColor(i, color.Darkened(0.3f));
            heads.SetInstanceColor(i, color);
            barrels.SetInstanceColor(i, color.Lightened(0.3f));
            Aim(i, pivot, 0f);
        }
        if (battle)
        {
            beams ??= Batch(BeamMesh, BeamCapacity);
            beams.VisibleInstanceCount = 0;
        }
    }

    public static Vector3 Pivot(ObstacleRectangle area, float height)
    {
        Vector2 center = (area.Min + area.Max) * 0.5f;
        return new Vector3(center.X, height + 0.25f, center.Y);
    }

    public void Aim(int index, Vector3 pivot, float yaw)
    {
        Basis basis = new Basis(Vector3.Up, yaw);
        heads.SetInstanceTransform(index, new Transform3D(basis, pivot));
        barrels.SetInstanceTransform(index, new Transform3D(basis, pivot + basis * new Vector3(0, 0, -0.825f)));
    }

    public void Fire(Vector3 from, Vector3 to, byte faction, float duration)
    {
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
    }

    private void WriteBeam(int i)
    {
        beams.SetInstanceTransform(i, beamPose[i]);
        beams.SetInstanceColor(i, beamColor[i]);
    }
}
