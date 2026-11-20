// Handles the three-click bridge placement tool and its undoable world paint edits.
using System.Linq;
using Godot;
using Bridges = Godot.Collections.Array<Godot.Rect2I>;

namespace MassiveRts;

public partial class LevelEditor
{
    private Vector2I? bridgeStart, bridgeEnd;
    private int bridgeWidth = 3;
    private MeshInstance3D bridgePreview;
    private Rect2I? shownBridge;

    // =========================================================
    private bool HandleBridge(InputEvent input)
    {
        if (input is InputEventMouseMotion motion && bridgeStart.HasValue)
        {
            cursor.Hide();
            if (!bridgeEnd.HasValue)
            {
                if (WorldPoint(motion.Position, out var point)) ShowBridge(Cell(point));
                else bridgePreview?.Hide();
            }
            return true;
        }
        if (input is not InputEventMouseButton { Pressed: true } mouse) return false;
        if (mouse.ButtonIndex == MouseButton.Right)
        {
            CancelBridge();
            SetStatus("Click the first bridge endpoint.");
        }
        else if (bridgeEnd is { } end && mouse.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
        {
            int step = mouse.ButtonIndex == MouseButton.WheelUp ? 1 : -1;
            bridgeWidth = Mathf.Clamp(bridgeWidth + step, 1, MaxBridgeWidth(bridgeStart.Value, end));
            ShowBridge(end);
            SetStatus($"Bridge width: {bridgeWidth * Grid:0.##} · Left-click to place");
        }
        else if (mouse.ButtonIndex == MouseButton.Left)
        {
            if (bridgeEnd is { } fixedEnd)
                PlaceBridge(BridgeBounds(bridgeStart.Value, fixedEnd));
            else if (WorldPoint(mouse.Position, out var point))
            {
                Vector2I cell = Cell(point);
                if (!paint.Contains(cell)) return true;
                if (bridgeStart == null)
                {
                    bridgeStart = cell;
                    ShowBridge(cell);
                    SetStatus("Click the opposite bank to set the span.");
                }
                else if (bridgeStart.Value != cell)
                {
                    bridgeEnd = cell;
                    bridgeWidth = Mathf.Min(bridgeWidth, MaxBridgeWidth(bridgeStart.Value, cell));
                    camera.SuppressWheelZoom = true;
                    ShowBridge(cell);
                    SetStatus($"Bridge width: {bridgeWidth * Grid:0.##} · Scroll to adjust");
                }
            }
        }
        else return false;
        GetViewport().SetInputAsHandled();
        return true;
    }

    private static int MaxBridgeWidth(Vector2I start, Vector2I end)
    {
        Vector2I delta = end - start;
        return Mathf.Min(WorldPaint.MaxBridgeWidthCells, Mathf.Max(Mathf.Abs(delta.X), Mathf.Abs(delta.Y)) + 1);
    }

    private Rect2I BridgeBounds(Vector2I start, Vector2I end)
    {
        Vector2I delta = end - start;
        int width = Mathf.Min(bridgeWidth, MaxBridgeWidth(start, end));
        int side = (width - 1) / 2;
        if (Mathf.Abs(delta.X) >= Mathf.Abs(delta.Y))
            return new Rect2I(Mathf.Min(start.X, end.X), start.Y - side, Mathf.Abs(delta.X) + 1, width);
        return new Rect2I(start.X - side, Mathf.Min(start.Y, end.Y), width, Mathf.Abs(delta.Y) + 1);
    }

    private void ShowBridge(Vector2I end)
    {
        Rect2I bounds = BridgeBounds(bridgeStart.Value, end);
        if (bridgePreview == null)
        {
            bridgePreview = new MeshInstance3D { TopLevel = true, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            map.AddChild(bridgePreview, false, Node.InternalMode.Back);
            bridgePreview.GlobalTransform = Transform3D.Identity;
        }
        if (!paint.BridgeFits(bounds)) { bridgePreview.Hide(); return; }
        if (shownBridge != bounds)
        {
            bridgePreview.Mesh = BridgeDeck.Build(new[] { bounds }, paint.Surface, preview: true);
            shownBridge = bounds;
        }
        bridgePreview.Show();
    }

    private void PlaceBridge(Rect2I bounds)
    {
        var previous = paint.Data.Bridges;
        if (!paint.BridgeFits(bounds) || previous.Any(bridge => bridge.Intersects(bounds)))
        {
            SetStatus("Keep the bridge inside the map and clear of other bridges.");
            return;
        }
        var next = new Bridges(previous) { bounds };
        if (!paint.TryMerge(new(paint.Data.RockCells), new(paint.Data.WaterCells), out _, out _, next))
        {
            SetStatus("Bridge exceeds the map navigation budget.");
            return;
        }
        CancelBridge();
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
        bridgeStart = bridgeEnd = null;
        shownBridge = null;
        if (camera != null) camera.SuppressWheelZoom = false;
        if (bridgePreview == null) return;
        bridgePreview.GetParent()?.RemoveChild(bridgePreview);
        bridgePreview.QueueFree();
        bridgePreview = null;
    }
}
