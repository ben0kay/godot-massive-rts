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
    private const float HuskLifetime = 30f, HuskTier = 4f;
    private readonly List<Husk> husks = new(Capacity);
    private MultiMesh huskMesh;
    private static Mesh sharedHuskMesh;
    private record struct Husk(ObstacleRectangle Area, Transform3D Pose, float Remaining);
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
        for (int type = 0; type < enabled.Length; type++)
        {
            enabled[type] = catalog[type].SizeTier >= 3f && catalog.Definition(type).LeavesNavigationWreck;
        }
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
        if (!enabledTypes[type] || wrecks.Count + husks.Count >= Capacity) return false;
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
        bounds = wrecks.Count + husks.Count == 0 ? footprint : bounds.Merge(footprint);
        lowestTier = Mathf.Min(lowestTier, stats.SizeTier);
        wrecks.Add(wreck);
        WritePose(wreck);
        mesh.VisibleInstanceCount = meshCounts[type];
        SetProcess(true);
        return true;
    }

    // =========================================================
    // Keep the former building footprint as movement-only rubble. All husks and
    // heavy unit wrecks share the existing 32-obstacle route-graph budget.
    internal bool TryAddBuilding(StructureHealth building, float ground)
    {
        if (wrecks.Count + husks.Count >= Capacity) return false;
        Vector2 min = building.Area.Min, size = building.Area.Max - min;
        var footprint = new Rect2(min, size);
        var area = new ObstacleRectangle
        {
            WorldBounds = footprint, AllowInteriorEscape = true, WreckTier = HuskTier
        };
        Vector2 center = min + size * 0.5f;
        float height = Mathf.Clamp(Mathf.Min(size.X, size.Y) * 0.27f, 0.65f, 2.0f);
        // Rotate a shared asymmetrical rubble layout by half a turn only, keeping
        // its normalized rectangular foundation within the original bounds.
        float yaw = (Mathf.FloorToInt(center.X + center.Y) & 1) == 0 ? 0f : Mathf.Pi;
        var pose = new Transform3D(new Basis(Vector3.Up, yaw) * Basis.FromScale(new Vector3(size.X, height, size.Y)),
            new Vector3(center.X, ground, center.Y));
        huskMesh ??= CreateHuskBatch();
        husks.Add(new Husk(area, pose, HuskLifetime));
        huskMesh.SetInstanceTransform(husks.Count - 1, pose);
        huskMesh.VisibleInstanceCount = husks.Count;
        RecalculateBounds();
        changed = true;
        return true;
    }

    internal bool OverlapsBuildingHusk(ObstacleRectangle area)
    {
        var rect = new Rect2(area.Min, area.Max - area.Min);
        foreach (var husk in husks)
            if (rect.Intersects(husk.Area.WorldBounds.Value)) return true;
        return false;
    }

    internal static Mesh SharedHuskMesh()
    {
        if (sharedHuskMesh == null)
        {
            using var surface = new SurfaceTool();
            surface.Begin(Mesh.PrimitiveType.Triangles);
            surface.SetMaterial(new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                VertexColorUseAsAlbedo = true
            });
            void Slab(float x, float y, float z, float w, float h, float d, Color tint, float pitch = 0f) =>
                FacetedSoldierMeshes.AppendBeveledRectangle(surface, new Vector3(x, y, z), new Vector3(w, h, d), tint,
                    sideShade: 0.80f, pitch: pitch);
            Color baseMetal = new("#292b2d"), charred = new("#353638"), steel = new("#4a4947"), burnt = new("#302723");
            // Low square foundation, collapsed armor slabs, exposed dark core
            // and jagged leaning remnants, all baked into one mesh.
            TowerArmorGeometry.Plate(surface, new Vector3(0f, 0.10f, 0f), new Vector3(1f, 0.20f, 1f), 0.08f, 0.98f, baseMetal);
            Slab(-0.21f, 0.34f, -0.14f, 0.48f, 0.39f, 0.60f, charred, 0.28f);
            Slab(0.24f, 0.32f, 0.16f, 0.42f, 0.34f, 0.57f, steel, -0.35f);
            Slab(-0.12f, 0.49f, 0.26f, 0.36f, 0.64f, 0.30f, burnt, -0.30f);
            Slab(0.24f, 0.23f, -0.29f, 0.38f, 0.18f, 0.26f, charred, 0.16f);
            Slab(-0.35f, 0.18f, 0.30f, 0.20f, 0.10f, 0.23f, steel);
            Slab(0.05f, 0.22f, 0.04f, 0.27f, 0.25f, 0.32f, new Color("#1e1c1b"));
            Slab(0.05f, 0.36f, -0.04f, 0.16f, 0.035f, 0.12f, new Color("#815033"));
            sharedHuskMesh = surface.Commit();
        }
        return sharedHuskMesh;
    }

    private MultiMesh CreateHuskBatch()
    {
        var batch = new MultiMesh
        {
            Mesh = SharedHuskMesh(), TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            InstanceCount = Capacity, VisibleInstanceCount = 0
        };
        AddChild(new MultiMeshInstance3D { Name = "BuildingHusks", Multimesh = batch,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        return batch;
    }

    private void RemoveHusk(int index)
    {
        int last = husks.Count - 1;
        if (index != last)
        {
            husks[index] = husks[last];
            huskMesh.SetInstanceTransform(index, husks[index].Pose);
        }
        husks.RemoveAt(last);
        huskMesh.VisibleInstanceCount = husks.Count;
    }

    private void RecalculateBounds()
    {
        lowestTier = float.PositiveInfinity;
        bool first = true;
        void Include(ObstacleRectangle area)
        {
            bounds = first ? area.WorldBounds.Value : bounds.Merge(area.WorldBounds.Value);
            first = false;
            lowestTier = Mathf.Min(lowestTier, area.WreckTier);
        }
        foreach (var wreck in wrecks) Include(wreck.Area);
        foreach (var husk in husks) Include(husk.Area);
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
        if (mover.SizeTier > HuskTier)
            for (int i = husks.Count - 1; i >= 0; i--)
            {
                if (!ObstacleField.TouchesSweep(husks[i].Area, from, to, mover.Footprint)) continue;
                RemoveHusk(i);
                crushed = true;
            }
        if (!crushed) return;
        RecalculateBounds();
        changed = true;
        refreshDelay = 0f;
    }

    internal bool ConsumeObstacleChange(float delta)
    {
        // Same battle-frame clock as obstacle refresh; paused battles do not age.
        bool expired = false;
        for (int i = husks.Count - 1; i >= 0; i--)
        {
            var husk = husks[i];
            husk.Remaining -= delta;
            if (husk.Remaining <= 0f) { RemoveHusk(i); expired = true; }
            else husks[i] = husk;
        }
        if (expired)
        {
            RecalculateBounds();
            changed = true;
            refreshDelay = 0f;
        }
        refreshDelay = Mathf.Max(0f, refreshDelay - delta);
        if (!changed || refreshDelay > 0f) return false;
        changed = false;
        refreshDelay = RefreshInterval;
        return true;
    }

    internal ObstacleField Combine(ObstacleField staticField, float boundary)
    {
        changed = false;
        if (wrecks.Count + husks.Count == 0) return staticField;
        int count = staticField?.RectangleCount ?? 0;
        var rectangles = new ObstacleRectangle[count + wrecks.Count + husks.Count];
        if (staticField != null) staticField.Rectangles.CopyTo(rectangles);
        for (int i = 0; i < wrecks.Count; i++) rectangles[count + i] = wrecks[i].Area;
        for (int i = 0; i < husks.Count; i++) rectangles[count + wrecks.Count + i] = husks[i].Area;
        return new ObstacleField(rectangles, staticField?.Boundary ?? boundary, staticField?.Bridges);
    }
}
