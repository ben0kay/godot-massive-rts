// Handles rectangle bridge painting and undoable edits to world paint.
using Godot;
using Bridges = Godot.Collections.Array<Godot.Rect2I>;

namespace MassiveRts;

public partial class LevelEditor
{
    private Vector2I? bridgeStart;

    private bool HandleBridge(InputEvent input)
    {
        if (input is InputEventMouseMotion motion && bridgeStart.HasValue)
        {
            if (WorldPoint(motion.Position, out var point)) ShowBridge(Cell(point));
            else cursor.Hide();
            return true;
        }
        if (input is not InputEventMouseButton mouse) return false;
        if (mouse.ButtonIndex == MouseButton.Right && mouse.Pressed)
            CancelBridge();
        else if (mouse.ButtonIndex == MouseButton.Left)
        {
            if (mouse.Pressed)
            {
                if (WorldPoint(mouse.Position, out var point) && paint.Contains(Cell(point)))
                {
                    bridgeStart = Cell(point);
                    ShowBridge(bridgeStart.Value);
                }
            }
            else if (bridgeStart is { } start)
            {
                CancelBridge();
                if (WorldPoint(mouse.Position, out var point))
                {
                    var area = RectangleArea(start, Cell(point));
                    PlaceBridge(new Rect2I(area.X, area.Z, area.Width, area.Depth));
                }
            }
        }
        else return false;
        GetViewport().SetInputAsHandled();
        return true;
    }

    private void ShowBridge(Vector2I end)
    {
        var area = RectangleArea(bridgeStart.Value, end);
        Vector2 center = (area.Min + area.Max) * 0.5f;
        var point = new Vector3(center.X, 0f, center.Y);
        point.Y = Surface(point) + 0.15f;
        cursor.GlobalPosition = point;
        cursor.Scale = new Vector3(area.Width * Grid, 1f, area.Depth * Grid);
        cursor.Show();
    }

    private void PlaceBridge(Rect2I bounds)
    {
        if (!paint.BridgeFits(bounds))
        {
            SetStatus("Bridge exceeds the map paint bounds or budget.");
            return;
        }

        var previous = paint.Data.Bridges;
        if (previous.Contains(bounds)) return;
        var next = new Bridges(previous) { bounds };
        if (!paint.TryMerge(new(paint.Data.RockCells), new(paint.Data.WaterCells), out _, out _, next))
        {
            SetStatus("Bridge exceeds the map navigation budget.");
            return;
        }
        SetValue(previous, next, value => paint.Data.Bridges = value);
    }

    private bool EraseBridge(Vector2I cell)
    {
        var previous = paint.Data.Bridges;
        for (int i = previous.Count - 1; i >= 0; i--)
        {
            if (!previous[i].HasPoint(cell)) continue;
            var next = new Bridges(previous);
            next.RemoveAt(i);
            if (!paint.TryMerge(new(paint.Data.RockCells), new(paint.Data.WaterCells), out _, out _, next))
            {
                SetStatus("Removing the bridge would exceed the map navigation budget.");
                return true;
            }
            SetValue(previous, next, value => paint.Data.Bridges = value);
            return true;
        }
        return false;
    }

    private void CancelBridge()
    {
        if (!bridgeStart.HasValue) return;
        bridgeStart = null;
        cursor?.Hide();
    }
}
