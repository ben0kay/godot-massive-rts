// Paints signed terrain sample offsets and records each stroke as one undoable edit.
using Godot;
using Samples = Godot.Collections.Dictionary<Godot.Vector2I, float>;

namespace MassiveRts;

public partial class LevelEditor
{
    private CommandTerrain elevationTerrain;
    private Samples beforeElevation, workingElevation;
    private MouseButton elevationButton;
    private Vector2? lastElevationPoint;
    private bool elevationDirty;
    private double elevationPreviewClock;

    // =========================================================
    private bool HandleElevation(InputEvent input)
    {
        if (input is InputEventMouseMotion motion)
        {
            if (ElevationPoint(motion.Position, out var point))
            {
                ShowCursor(point);
                cursor.Scale = new Vector3(elevationRadius * 2f, 1f, elevationRadius * 2f);
                if (workingElevation != null) PaintElevation(ObstacleField.Xz(point));
            }
            else
            {
                cursor.Hide();
                lastElevationPoint = null;
            }
            return true;
        }
        if (input is not InputEventMouseButton mouse || mouse.ButtonIndex is not (MouseButton.Left or MouseButton.Right))
            return false;
        if (!mouse.Pressed)
        {
            if (mouse.ButtonIndex == elevationButton) EndElevation();
        }
        else if (workingElevation == null && ElevationPoint(mouse.Position, out var point) &&
            map.GetNodeOrNull<CommandTerrain>("Terrain") is CommandTerrain terrain)
        {
            elevationTerrain = terrain;
            beforeElevation = terrain.Elevation;
            workingElevation = new();
            foreach (var entry in beforeElevation) workingElevation.Add(entry.Key, entry.Value);
            elevationButton = mouse.ButtonIndex;
            PaintElevation(ObstacleField.Xz(point));
            FlushElevation();
        }
        GetViewport().SetInputAsHandled();
        return true;
    }

    private bool ElevationPoint(Vector2 screen, out Vector3 point)
    {
        var field = map.GetNodeOrNull<CommandTerrain>("Terrain")?.Field;
        if (field == null) return WorldPoint(screen, out point);
        Vector3 from = camera.ProjectRayOrigin(screen), direction = camera.ProjectRayNormal(screen);
        // Legacy hills reach 80 and painted offsets are clamped to +/-80.
        float start = Mathf.Max(0f, (161f - from.Y) / direction.Y);
        float end = (-81f - from.Y) / direction.Y;
        from += direction * start;
        Vector3 delta = direction * Mathf.Max(0f, end - start);
        float hit = field.SurfaceHit(from, delta, 1f);
        point = from + delta * (float.IsFinite(hit) ? hit : 0f);
        float half = map.MapSize * 0.5f;
        return float.IsFinite(hit) && Mathf.Abs(point.X) < half && Mathf.Abs(point.Z) < half;
    }

    // =========================================================
    // Use the strongest dab in this stroke at each sample, independent of mouse-event rate.
    private void PaintElevation(Vector2 point)
    {
        Vector2 start = lastElevationPoint ?? point;
        lastElevationPoint = point;
        float interval = Mathf.Min(TerrainHeightField.Spacing, elevationRadius * 0.35f);
        int steps = Mathf.Max(1, Mathf.CeilToInt(start.DistanceTo(point) / interval));
        for (int step = 1; step <= steps; step++) Dab(start.Lerp(point, step / (float)steps));
    }

    private void Dab(Vector2 center)
    {
        float spacing = TerrainHeightField.Spacing;
        float half = map.MapSize * 0.5f;
        int left = Mathf.CeilToInt(Mathf.Max(-half, center.X - elevationRadius) / spacing);
        int right = Mathf.FloorToInt(Mathf.Min(half, center.X + elevationRadius) / spacing);
        int near = Mathf.CeilToInt(Mathf.Max(-half, center.Y - elevationRadius) / spacing);
        int far = Mathf.FloorToInt(Mathf.Min(half, center.Y + elevationRadius) / spacing);
        bool lower = elevationButton == MouseButton.Right;
        for (int z = near; z <= far; z++)
        for (int x = left; x <= right; x++)
        {
            float weight = 1f - center.DistanceTo(new Vector2(x, z) * spacing) / elevationRadius;
            if (weight <= 0f) continue;
            var key = new Vector2I(x, z);
            beforeElevation.TryGetValue(key, out float original);
            workingElevation.TryGetValue(key, out float current);
            float change = elevationStrength * weight * weight * (3f - 2f * weight);
            float next = Mathf.Clamp(original + (lower ? -change : change), -80f, 80f);
            if (lower ? next >= current : next <= current) continue;
            if (Mathf.Abs(next) < 0.0001f) workingElevation.Remove(key);
            else workingElevation[key] = next;
            elevationDirty = true;
        }
    }

    private void UpdateElevation(double delta)
    {
        if (workingElevation == null) return;
        if (!Input.IsMouseButtonPressed(elevationButton))
        {
            EndElevation();
            return;
        }
        elevationPreviewClock += delta;
        if (elevationPreviewClock >= 0.067) FlushElevation();
    }

    private void FlushElevation()
    {
        elevationPreviewClock = 0;
        if (!elevationDirty) return;
        elevationDirty = false;
        elevationTerrain.Elevation = workingElevation;
        RefreshMap(terrainChanged: true);
    }

    private void EndElevation()
    {
        if (workingElevation == null) return;
        FlushElevation();
        var terrain = elevationTerrain;
        var before = beforeElevation;
        var after = workingElevation;
        elevationTerrain = null;
        beforeElevation = workingElevation = null;
        if (terrain.Elevation != after) return;

        void Apply(Samples samples)
        {
            terrain.Elevation = samples;
            RefreshMap(terrainChanged: true);
        }
        Remember(() => Apply(before), () => Apply(after));
    }
}
