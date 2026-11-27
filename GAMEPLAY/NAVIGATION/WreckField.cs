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
    private readonly List<ObstacleRectangle> areas = new(Capacity);
    private readonly List<SettlingWreck> settling = new(Capacity);
    private RuntimeUnitCatalog catalog;
    private bool[] enabledTypes;
    private MultiMesh[] meshes;
    private int[] meshCounts;
    private bool changed;
    private float refreshDelay;

    private record struct SettlingWreck(MultiMesh Mesh, int Slot, Vector3 Ground, float Height, float Yaw, float Tilt, float Age);

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
        if (!enabledTypes[type] || areas.Count == Capacity) return false;
        ref readonly var stats = ref catalog[type];
        float yaw = facing == Vector2.Zero ? 0f : stats.Shape == UnitShape.Diamond
            ? Mathf.Atan2(-facing.Y, facing.X) : Mathf.Atan2(-facing.X, -facing.Y);
        float x = stats.Outline.RadiusX * 0.64f, z = stats.Outline.RadiusZ * 0.64f;
        float cosine = Mathf.Abs(Mathf.Cos(yaw)), sine = Mathf.Abs(Mathf.Sin(yaw));
        Vector2 halfSize = new(x * cosine + z * sine, x * sine + z * cosine);
        areas.Add(new ObstacleRectangle
        {
            WorldBounds = new Rect2(ObstacleField.Xz(ground) - halfSize, halfSize * 2f),
            AllowInteriorEscape = true
        });
        changed = true;

        var mesh = meshes[type] ??= CreateBatch(type);
        int slot = meshCounts[type]++;
        float tilt = slot % 2 == 0 ? 0.15f : -0.19f;
        var wreck = new SettlingWreck(mesh, slot, ground, stats.Height, yaw, tilt, 0f);
        settling.Add(wreck);
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
        for (int i = settling.Count - 1; i >= 0; i--)
        {
            var wreck = settling[i];
            wreck.Age += (float)delta;
            WritePose(wreck);
            if (wreck.Age >= SettleSeconds) settling.RemoveAt(i);
            else settling[i] = wreck;
        }
        if (settling.Count == 0) SetProcess(false);
    }

    private static void WritePose(SettlingWreck wreck)
    {
        float t = Mathf.Clamp(wreck.Age / SettleSeconds, 0f, 1f);
        float ease = t * (2f - t);
        var basis = new Basis(Vector3.Up, wreck.Yaw) * new Basis(Vector3.Forward, wreck.Tilt * (0.5f + ease * 0.5f));
        Vector3 center = wreck.Ground + Vector3.Up * (wreck.Height * Mathf.Lerp(0.39f, 0.13f, ease));
        wreck.Mesh.SetInstanceTransform(wreck.Slot, new Transform3D(basis, center));
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
        if (areas.Count == 0) return staticField;
        int count = staticField?.RectangleCount ?? 0;
        var rectangles = new ObstacleRectangle[count + areas.Count];
        if (staticField != null) staticField.Rectangles.CopyTo(rectangles);
        areas.CopyTo(rectangles, count);
        changed = false;
        return new ObstacleField(rectangles, staticField?.Boundary ?? boundary, staticField?.Bridges);
    }
}
