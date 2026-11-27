// Paints absolute elevation plateaus into the shared elevation samples.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

public partial class LevelEditor
{
    private int elevationTier = 4;
    private float tierRadius = 16f, tierSlope = 16f;
    private bool tierRectangle;
    private Vector2? tierStart;
    private Label tierReadout;
    private readonly Dictionary<Vector2I, float> tierWeights = new();

    // =========================================================
    private void TieredElevationControls()
    {
        var terrain = map.GetNode<CommandTerrain>("Terrain");
        var levels = new OptionButton();
        for (int level = 1; level <= 7; level++)
            levels.AddItem($"Level {level} · {terrain.TierHeight(level):0.##}", level);
        levels.Select(elevationTier - 1);
        levels.ItemSelected += index => { EndElevation(); elevationTier = levels.GetItemId((int)index); };
        context.AddChild(levels);
        var rectangle = new CheckBox { Text = "Rectangle fill", ButtonPressed = tierRectangle };
        rectangle.Toggled += enabled =>
        {
            EndElevation();
            tierRectangle = enabled;
            cursor.Mesh = enabled ? squareCursor : elevationCursor;
            cursor.Hide();
        };
        context.AddChild(rectangle);
        Number(context, "Brush radius", tierRadius, 4, 128).ValueChanged += value => { EndElevation(); tierRadius = (float)value; };
        Number(context, "Minimum slope width", tierSlope, 4, 128).ValueChanged += value => { EndElevation(); tierSlope = (float)value; };
        tierReadout = Heading(context, "Move over terrain to sample height.", 15);
    }

    // =========================================================
    private bool HandleTieredElevation(InputEvent input)
    {
        if (input is InputEventMouseMotion motion)
        {
            if (!ElevationPoint(motion.Position, out var point))
            {
                cursor.Hide();
                lastElevationPoint = null;
                return true;
            }

            var terrain = map.GetNode<CommandTerrain>("Terrain");
            Vector2 at = ObstacleField.Xz(point);
            float target = terrain.TierHeight(elevationTier);
            int nearest = 1;
            for (int level = 2; level <= 7; level++)
                if (Mathf.Abs(point.Y - terrain.TierHeight(level)) < Mathf.Abs(point.Y - terrain.TierHeight(nearest)))
                    nearest = level;
            tierReadout.Text = $"Ground {point.Y:0.00} · nearest Level {nearest}\nPaint Level {elevationTier} · {target:0.##}";
            Vector2 min = tierStart?.Min(at) ?? (at - Vector2.One * tierRadius);
            Vector2 max = tierStart?.Max(at) ?? (at + Vector2.One * tierRadius);
            Vector2 center = (min + max) * 0.5f;
            cursor.GlobalPosition = new Vector3(center.X, Mathf.Max(target, Surface(new Vector3(center.X, 0f, center.Y))) + 0.15f, center.Y);
            cursor.Scale = new Vector3(Mathf.Max(0.5f, max.X - min.X), 1f, Mathf.Max(0.5f, max.Y - min.Y));
            cursor.Show();
            if (workingElevation != null && !tierRectangle) TierBrush(at);
            return true;
        }

        if (input is not InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse) return false;
        if (mouse.Pressed && workingElevation == null && ElevationPoint(mouse.Position, out var pointAtClick))
        {
            elevationTerrain = map.GetNode<CommandTerrain>("Terrain");
            beforeElevation = elevationTerrain.Elevation;
            beforeElevationField = elevationTerrain.Field;
            workingElevation = new();
            foreach (var entry in beforeElevation) workingElevation.Add(entry.Key, entry.Value);
            elevationButton = MouseButton.Left;
            Vector2 at = ObstacleField.Xz(pointAtClick);
            if (tierRectangle) tierStart = at;
            else TierBrush(at);
        }
        else if (!mouse.Pressed)
        {
            if (tierStart is { } start && ElevationPoint(mouse.Position, out var end))
            {
                Vector2 at = ObstacleField.Xz(end);
                PaintTier(start.Min(at), start.Max(at), 0f);
            }
            EndElevation();
        }
        GetViewport().SetInputAsHandled();
        return true;
    }

    // =========================================================
    private void TierBrush(Vector2 point)
    {
        Vector2 from = lastElevationPoint ?? point;
        lastElevationPoint = point;
        int steps = Mathf.Max(1, Mathf.CeilToInt(from.DistanceTo(point) / TerrainHeightField.Spacing));
        for (int i = 1; i <= steps; i++)
        {
            Vector2 center = from.Lerp(point, i / (float)steps);
            PaintTier(center, center, tierRadius);
        }
    }

    private void PaintTier(Vector2 min, Vector2 max, float radius)
    {
        const float spacing = TerrainHeightField.Spacing;
        float target = elevationTerrain.TierHeight(elevationTier);
        float half = map.MapSize * 0.5f;
        float pad = radius + spacing * Mathf.Sqrt(2f);
        float outer = pad + 128f;
        int left = Mathf.CeilToInt(Mathf.Max(-half, min.X - outer) / spacing);
        int right = Mathf.FloorToInt(Mathf.Min(half, max.X + outer) / spacing);
        int near = Mathf.CeilToInt(Mathf.Max(-half, min.Y - outer) / spacing);
        int far = Mathf.FloorToInt(Mathf.Min(half, max.Y + outer) / spacing);
        for (int z = near; z <= far; z++)
        for (int x = left; x <= right; x++)
        {
            var key = new Vector2I(x, z);
            Vector2 at = (Vector2)key * spacing;
            Vector2 clamped = at.Max(min).Min(max);
            float distance = at.DistanceTo(clamped) - pad;
            float height = beforeElevationField?.At(at.X, at.Y) ?? 0f;
            float slope = Mathf.Clamp(Mathf.Max(tierSlope, Mathf.Abs(target - height) * 3f), tierSlope, 128f);
            float t = Mathf.Clamp(distance / slope, 0f, 1f);
            float weight = 1f - t * t * (3f - 2f * t);
            if (weight <= 0f || (tierWeights.TryGetValue(key, out float oldWeight) && weight <= oldWeight)) continue;
            tierWeights[key] = weight;
            float delta = (target - height) * weight;
            if (Mathf.Abs(delta) < 0.0001f) continue;
            beforeElevation.TryGetValue(key, out float original);
            float next = original + delta;
            if (Mathf.Abs(next) < 0.0001f) workingElevation.Remove(key);
            else workingElevation[key] = next;
            elevationDirty = true;
        }
    }
}
