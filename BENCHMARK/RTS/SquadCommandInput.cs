// Converts click, box select and right-drag gestures into squad orders.
using Godot;
using System.Runtime.InteropServices;

namespace MassiveRts;

public partial class SquadCommandInput : Node
{
    private const float DragThreshold = 6f;
    private CommandOverlay overlay;
    private MouseButton held;
    private Vector3 dragStart;

    private CommandMovement movement;
    private CommandBuildings buildings;
    private FabricationHud hud;
    private MeshInstance3D ghost;
    private bool placing;
    private bool ctrlCandidate;
    private bool leftControl, rightControl;

    internal void Initialize(CommandOverlay display, CommandMovement orders, CommandBuildings world, FabricationHud fabrication)
    {
        overlay = display;
        movement = orders;
        buildings = world;
        hud = fabrication;
        ghost = CommandStructure.MakePreview();
        GetParent().AddChild(ghost);
        hud.PlaceRequested = () =>
        {
            CancelGesture();
            placing = true;
            hud.MenuOpen = false;
        };
    }

    // Observe combinations before GUI handling, without consuming CTRL or its shortcuts.
    public override void _Input(InputEvent input)
    {
        if (input is InputEventKey key && key.Keycode == Key.Ctrl)
        {
            if (key.Echo) return;
            bool otherControl = key.Location == KeyLocation.Right ? leftControl : rightControl;
            if (key.Location == KeyLocation.Right) rightControl = key.Pressed;
            else leftControl = key.Pressed;
            if (key.Pressed)
                ctrlCandidate = !key.ShiftPressed && !key.AltPressed && !key.MetaPressed && !otherControl;
            else if (ctrlCandidate)
            {
                ctrlCandidate = false;
                if (placing) EndPlacement(false);
                else hud.MenuOpen = !hud.MenuOpen;
            }
            return;
        }
        if (input is InputEventKey { Pressed: true } or InputEventMouseButton { Pressed: true })
            ctrlCandidate = false;
    }

    private void EndPlacement(bool reopen)
    {
        placing = false;
        ghost.Hide();
        hud.MenuOpen = reopen;
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (placing)
        {
            if (input is InputEventMouseButton { Pressed: true } click)
            {
                if (click.ButtonIndex == MouseButton.Right) EndPlacement(true);
                else if (click.ButtonIndex == MouseButton.Left)
                {
                    if (TryGround(click.Position, out Vector3 point) && buildings.Place(CommandBuildings.Snap(point)))
                        EndPlacement(true);
                }
                else return;
                GetViewport().SetInputAsHandled();
            }
            else if (input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
            {
                EndPlacement(true);
                GetViewport().SetInputAsHandled();
            }
            return;
        }

        if (overlay.Building?.Production != null && overlay.Building.Ready &&
            input is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true } rallyClick)
        {
            if (TryGround(rallyClick.Position, out Vector3 point) && buildings.CanRally(point))
                overlay.Building.Rally = point;
            GetViewport().SetInputAsHandled();
            return;
        }

        if (input is InputEventMouseMotion motion && held != MouseButton.None)
        {
            overlay.Pointer = motion.Position;
            if (held == MouseButton.Right) UpdateFormation(motion.Position);
            GetViewport().SetInputAsHandled();
        }
        else if (input is InputEventMouseButton button &&
            (button.ButtonIndex == MouseButton.Left || button.ButtonIndex == MouseButton.Right))
        {
            if (button.Pressed)
            {
                if (held != MouseButton.None) return;
                if (button.ButtonIndex == MouseButton.Right && (overlay.Selection.Count == 0 || !TryGround(button.Position, out dragStart))) return;
                held = button.ButtonIndex;
                overlay.Press = overlay.Pointer = button.Position;
                overlay.Selecting = held == MouseButton.Left;
            }
            else if (button.ButtonIndex == held)
            {
                if (held == MouseButton.Left) Select(button.Position);
                else if (UpdateFormation(button.Position))
                    movement.Issue(CollectionsMarshal.AsSpan(overlay.Selection), overlay.Front, overlay.Right, overlay.Columns, overlay.Spacing);
                CancelGesture();
            }
            else return;
            GetViewport().SetInputAsHandled();
        }
        else if (input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            CancelGesture();
            ClearSelection();
        }
    }

    public override void _Process(double delta)
    {
        if (held != MouseButton.None && !Input.IsMouseButtonPressed(held)) CancelGesture();
        if (held == MouseButton.Right) UpdateFormation(overlay.Pointer);
        if (placing)
        {
            ghost.Visible = TryGround(GetViewport().GetMousePosition(), out Vector3 point);
            if (ghost.Visible)
            {
                var area = CommandBuildings.Snap(point);
                ghost.Position = CommandBuildings.Center(area);
                ((StandardMaterial3D)ghost.MaterialOverride).AlbedoColor = buildings.CanPlace(area)
                    ? new Color(0.2f, 0.92f, 1f, 0.38f) : new Color(1f, 0.22f, 0.24f, 0.40f);
            }
        }
        if (overlay.Building?.Destroyed == true) ClearSelection();
        if (overlay.Selection.Count > 0 || overlay.Building != null || held != MouseButton.None) overlay.QueueRedraw();
    }

    private void CancelGesture()
    {
        held = MouseButton.None;
        overlay.Selecting = overlay.Preview = false;
        overlay.QueueRedraw();
    }

    private void Select(Vector2 point)
    {
        bool boxSelect = overlay.Press.DistanceTo(point) >= DragThreshold;
        var box = new Rect2(overlay.Press, point - overlay.Press).Abs();
        ClearSelection();
        if (!boxSelect)
        {
            overlay.Building = buildings.Pick(overlay.View, point);
            if (overlay.Building != null) return;
        }

        int nearest = -1;
        float bestDistance = float.PositiveInfinity;
        bool includeStartingSquad = false;
        Vector3 right = CameraRight();
        for (int i = 0; i < overlay.Units.Count; i++)
        {
            Vector3 position = overlay.Units.Position[i];
            if (overlay.Units.Health[i] <= 0 || overlay.View.IsPositionBehind(position)) continue;
            Vector2 center = overlay.View.UnprojectPosition(position);
            float pixels = center.DistanceTo(overlay.View.UnprojectPosition(position + right * overlay.Radius(i)));
            if (boxSelect)
            {
                if (!box.Grow(pixels).HasPoint(center)) continue;
                if (i < CommandMovement.SquadSize) includeStartingSquad = true;
                else overlay.Selection.Add(i);
                continue;
            }
            float distance = center.DistanceTo(point);
            if (distance > Mathf.Max(6f, pixels) || distance >= bestDistance) continue;
            nearest = i;
            bestDistance = distance;
        }
        if (nearest >= CommandMovement.SquadSize) overlay.Selection.Add(nearest);
        if (includeStartingSquad || nearest >= 0 && nearest < CommandMovement.SquadSize)
            for (int id = 0; id < CommandMovement.SquadSize; id++)
                if (overlay.Units.Health[id] > 0) overlay.Selection.Add(id);
        float radius = 0;
        foreach (int id in overlay.Selection) radius = Mathf.Max(radius, overlay.Radius(id));
        overlay.Spacing = radius * 2f + 0.3f;
    }

    private void ClearSelection()
    {
        overlay.Selection.Clear();
        overlay.Building = null;
        overlay.QueueRedraw();
    }

    private Vector3 CameraRight()
    {
        Vector3 right = overlay.View.GlobalTransform.Basis.X;
        return new Vector3(right.X, 0f, right.Z).Normalized();
    }

    private bool UpdateFormation(Vector2 pointer)
    {
        if (!TryGround(pointer, out Vector3 end))
        {
            overlay.Preview = false;
            return false;
        }
        float radius = (overlay.Spacing - 0.3f) * 0.5f;
        Vector3 line = end - dragStart;
        overlay.Preview = pointer.DistanceTo(overlay.Press) >= DragThreshold && line.LengthSquared() > 0.0001f;
        if (overlay.Preview)
        {
            overlay.Right = line.Normalized();
            overlay.Columns = Mathf.Clamp(1 + Mathf.FloorToInt((line.Length() - radius * 2f) /
                overlay.Spacing), 1, overlay.Selection.Count);
            overlay.LineStart = dragStart;
            overlay.LineEnd = end;
            overlay.Front = (dragStart + end) * 0.5f +
                overlay.Right.Cross(Vector3.Up) * radius;
        }
        else
        {
            overlay.Right = CameraRight();
            overlay.Columns = Mathf.CeilToInt(Mathf.Sqrt(overlay.Selection.Count));
            int lastRow = (overlay.Selection.Count - 1) / overlay.Columns;
            overlay.Front = end - overlay.Right.Cross(Vector3.Up) * (lastRow * 0.5f * overlay.Spacing);
        }
        return true;
    }

    private bool TryGround(Vector2 point, out Vector3 ground) =>
        CommandBuildings.TryGround(overlay.View, point, out ground) && CommandBuildings.OnMap(ground);
}
