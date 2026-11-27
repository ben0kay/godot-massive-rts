// Retains opted-in heavy casualties and supplies their footprints to ground navigation.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal sealed partial class WreckField : Node3D
{
    // The corner route graph has quadratic edge construction. Bound this first pass.
    private const int Capacity = 32;
    private const float SettleSeconds = 0.24f;
    private const float RefreshInterval = 0.15f;
    private readonly List<Wreck> wrecks = new(Capacity);
    private float lowestTier = float.PositiveInfinity;
    private Rect2 bounds;
    private RuntimeUnitCatalog catalog;
    private bool[] enabledTypes;
    private MultiMesh[] meshes;
    private int[] meshCounts;
    private bool changed;
    private float refreshDelay;

    private record struct Wreck(ObstacleRectangle Area, byte Type, int Slot, Vector3 Ground, float Height, float Yaw, float Tilt, float Age);

    internal static WreckField Create(Node3D parent, RuntimeUnitCatalog catalog)
    {
        var enabled = new bool[catalog.Count];
        bool any = false;
        for (int type = 0; type < enabled.Length; type++)
        {
            enabled[type] = catalog[type].SizeTier >= 3f && catalog.Definition(type).LeavesNavigationWreck;
            any |= enabled[type];
        }
        if (!any) return null;
        var wrecks = new WreckField
        {
            catalog = catalog, enabledTypes = enabled,
            meshes = new MultiMesh[catalog.Count], meshCounts = new int[catalog.Count]
        };
        parent.AddChild(wrecks);
        wrecks.SetProcess(false);
        return wrecks;
    }

    internal bool TryAdd(Vector3 ground, byte type, Vector2 facing)
    {
        if (!enabledTypes[type] || wrecks.Count == Capacity) return false;
        ref readonly var stats = ref catalog[type];
        float yaw = facing == Vector2.Zero ? 0f : stats.Shape == UnitShape.Diamond
            ? Mathf.Atan2(-facing.Y, facing.X) : Mathf.Atan2(-facing.X, -facing.Y);
        float x = stats.Outline.RadiusX * 0.64f, z = stats.Outline.RadiusZ * 0.64f;
        float cosine = Mathf.Abs(Mathf.Cos(yaw)), sine = Mathf.Abs(Mathf.Sin(yaw));
        Vector2 halfSize = new(x * cosine + z * sine, x * sine + z * cosine);
        var area = new ObstacleRectangle
        {
            WorldBounds = new Rect2(ObstacleField.Xz(ground) - halfSize, halfSize * 2f),
            AllowInteriorEscape = true, WreckTier = stats.SizeTier
        };
        changed = true;

        var mesh = meshes[type] ??= CreateBatch(type);
        int slot = meshCounts[type]++;
        float tilt = slot % 2 == 0 ? 0.15f : -0.19f;
        var wreck = new Wreck(area, type, slot, ground, stats.Height, yaw, tilt, 0f);
        var footprint = area.WorldBounds.Value;
        bounds = wrecks.Count == 0 ? footprint : bounds.Merge(footprint);
        lowestTier = Mathf.Min(lowestTier, stats.SizeTier);
        wrecks.Add(wreck);
        WritePose(wreck);
        mesh.VisibleInstanceCount = meshCounts[type];
        SetProcess(true);
        return true;
    }

    private MultiMesh CreateBatch(byte type)
    {
        var mesh = new MultiMesh
        {
            Mesh = FacetedSoldierMeshes.CreatePlacementPreview(catalog.Definition(type), new Color("#343940")),
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            InstanceCount = Capacity, VisibleInstanceCount = 0
        };
        AddChild(new MultiMeshInstance3D { Multimesh = mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        return mesh;
    }

    public override void _Process(double delta)
    {
        bool stillSettling = false;
        for (int i = 0; i < wrecks.Count; i++)
        {
            var wreck = wrecks[i];
            if (wreck.Age >= SettleSeconds) continue;
            wreck.Age += (float)delta;
            WritePose(wreck);
            wrecks[i] = wreck;
            stillSettling |= wreck.Age < SettleSeconds;
        }
        if (!stillSettling) SetProcess(false);
    }

    private void WritePose(Wreck wreck)
    {
        float t = Mathf.Clamp(wreck.Age / SettleSeconds, 0f, 1f);
        float ease = t * (2f - t);
        var basis = new Basis(Vector3.Up, wreck.Yaw) * new Basis(Vector3.Forward, wreck.Tilt * (0.5f + ease * 0.5f));
        Vector3 center = wreck.Ground + Vector3.Up * (wreck.Height * Mathf.Lerp(0.39f, 0.13f, ease));
        meshes[wreck.Type].SetInstanceTransform(wreck.Slot, new Transform3D(basis, center));
    }

    internal void CrushAlong(Vector2 from, Vector2 to, in UnitStats mover)
    {
        if (mover.SizeTier <= lowestTier || from == to) return;
        Vector2 start = from.Min(to), end = from.Max(to);
        if (!bounds.Grow(mover.Footprint).Intersects(new Rect2(start, end - start), true)) return;

        bool crushed = false;
        for (int i = wrecks.Count - 1; i >= 0; i--)
        {
            var wreck = wrecks[i];
            if (mover.SizeTier <= wreck.Area.WreckTier ||
                !ObstacleField.TouchesSweep(wreck.Area, from, to, mover.Footprint)) continue;
            int lastSlot = --meshCounts[wreck.Type];
            if (wreck.Slot != lastSlot)
            {
                for (int other = 0; other < wrecks.Count; other++)
                {
                    var moved = wrecks[other];
                    if (moved.Type != wreck.Type || moved.Slot != lastSlot) continue;
                    moved.Slot = wreck.Slot;
                    wrecks[other] = moved;
                    WritePose(moved);
                    break;
                }
            }
            meshes[wreck.Type].VisibleInstanceCount = lastSlot;
            wrecks.RemoveAt(i);
            crushed = true;
        }
        if (!crushed) return;
        lowestTier = float.PositiveInfinity;
        for (int i = 0; i < wrecks.Count; i++)
        {
            var area = wrecks[i].Area;
            var footprint = area.WorldBounds.Value;
            bounds = i == 0 ? footprint : bounds.Merge(footprint);
            lowestTier = Mathf.Min(lowestTier, area.WreckTier);
        }
        changed = true;
        refreshDelay = 0f;
    }

    internal bool ConsumeObstacleChange(float delta)
    {
        refreshDelay = Mathf.Max(0f, refreshDelay - delta);
        if (!changed || refreshDelay > 0f) return false;
        changed = false;
        refreshDelay = RefreshInterval;
        return true;
    }

    internal ObstacleField Combine(ObstacleField staticField, float boundary)
    {
        changed = false;
        if (wrecks.Count == 0) return staticField;
        int count = staticField?.RectangleCount ?? 0;
        var rectangles = new ObstacleRectangle[count + wrecks.Count];
        if (staticField != null) staticField.Rectangles.CopyTo(rectangles);
        for (int i = 0; i < wrecks.Count; i++) rectangles[count + i] = wrecks[i].Area;
        return new ObstacleField(rectangles, staticField?.Boundary ?? boundary, staticField?.Bridges);
    }
}
