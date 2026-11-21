// Paints signed terrain sample offsets and records each stroke as one undoable edit.
using Godot;
using Samples = Godot.Collections.Dictionary<Godot.Vector2I, float>;

namespace MassiveRts;

public partial class LevelEditor
{
    private CommandTerrain elevationTerrain;
    private TerrainHeightField beforeElevationField;
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
        else if (workingElevation == null && (!setElevationHeight || mouse.ButtonIndex == MouseButton.Left) &&
            ElevationPoint(mouse.Position, out var point) &&
            map.GetNodeOrNull<CommandTerrain>("Terrain") is CommandTerrain terrain)
        {
            elevationTerrain = terrain;
            beforeElevation = terrain.Elevation;
            beforeElevationField = terrain.Field;
            workingElevation = new();
            foreach (var entry in beforeElevation) workingElevation.Add(entry.Key, entry.Value);
            elevationButton = mouse.ButtonIndex;
            if (setElevationHeight)
            {
                SetElevationHeight(ObstacleField.Xz(point));
                EndElevation();
            }
            else
            {
                PaintElevation(ObstacleField.Xz(point));
                FlushElevation();
            }
        }
        GetViewport().SetInputAsHandled();
        return true;
    }

    private bool ElevationPoint(Vector2 screen, out Vector3 point)
    {
        var field = map.GetNodeOrNull<CommandTerrain>("Terrain")?.Field;
        if (field == null) return WorldPoint(screen, out point);
        Vector3 from = camera.ProjectRayOrigin(screen), direction = camera.ProjectRayNormal(screen);
        // Legacy maps can combine a hill of 80 with a painted offset of 80.
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
            float originalHeight = beforeElevationField?.At(x * spacing, z * spacing) ?? 0f;
            float limit = lower ? Mathf.Min(-80f, originalHeight) : Mathf.Max(80f, originalHeight);
            float nextHeight = lower ? Mathf.Max(limit, originalHeight - change) : Mathf.Min(limit, originalHeight + change);
            float next = original + (nextHeight - originalHeight);
            if (lower ? next >= current : next <= current) continue;
            if (Mathf.Abs(next) < 0.0001f) workingElevation.Remove(key);
            else workingElevation[key] = next;
            elevationDirty = true;
        }
    }

    private void SetElevationHeight(Vector2 center)
    {
        const float spacing = TerrainHeightField.Spacing;
        const float maxSlopeWidth = 128f;
        float half = map.MapSize * 0.5f;
        // Include boundary triangles so interpolation preserves the target inside the cursor.
        float plateauRadius = elevationRadius + spacing * Mathf.Sqrt(2f);
        float slopeWidth = spacing * 2f;
        for (int pass = 0; pass < 3; pass++)
        {
            float requiredWidth = slopeWidth;
            for (int i = 0; i < 16; i++)
            {
                Vector2 edge = Vector2.Right.Rotated(Mathf.Tau * i / 16f);
                for (int ring = 0; ring <= 2; ring++)
                {
                    Vector2 sample = center + edge * (plateauRadius + slopeWidth * ring * 0.5f);
                    if (Mathf.Abs(sample.X) > half || Mathf.Abs(sample.Y) > half) continue;
                    float height = beforeElevationField?.At(sample.X, sample.Y) ?? 0f;
                    requiredWidth = Mathf.Max(requiredWidth, 2.5f * Mathf.Abs(elevationTarget - height));
                }
            }
            if (requiredWidth <= slopeWidth) break;
            slopeWidth = Mathf.Min(requiredWidth, maxSlopeWidth);
            if (slopeWidth == maxSlopeWidth) break;
        }

        float outer = plateauRadius + slopeWidth;
        int left = Mathf.CeilToInt(Mathf.Max(-half, center.X - outer) / spacing);
        int right = Mathf.FloorToInt(Mathf.Min(half, center.X + outer) / spacing);
        int near = Mathf.CeilToInt(Mathf.Max(-half, center.Y - outer) / spacing);
        int far = Mathf.FloorToInt(Mathf.Min(half, center.Y + outer) / spacing);
        for (int z = near; z <= far; z++)
        for (int x = left; x <= right; x++)
        {
            Vector2 point = new Vector2(x, z) * spacing;
            float distance = center.DistanceTo(point);
            if (distance >= outer) continue;
            float t = Mathf.Clamp((distance - plateauRadius) / slopeWidth, 0f, 1f);
            float weight = 1f - t * t * (3f - 2f * t);
            float height = beforeElevationField?.At(point.X, point.Y) ?? 0f;
            float delta = (elevationTarget - height) * weight;
            if (Mathf.Abs(delta) < 0.0001f) continue;

            var key = new Vector2I(x, z);
            beforeElevation.TryGetValue(key, out float original);
            float offset = original + delta;
            if (Mathf.Abs(offset) < 0.0001f) workingElevation.Remove(key);
            else workingElevation[key] = offset;
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
        beforeElevationField = null;
        lastElevationPoint = null;
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
