// Converts click, box select and right-drag gestures into squad orders.
using Godot;

namespace MassiveRts;

public partial class SquadCommandInput : Node
{
    private const float DragThreshold = 6f;
    private CommandOverlay overlay;
    private MouseButton held;
    private Vector3 dragStart;

    internal void Initialize(CommandOverlay display) => overlay = display;

    public override void _UnhandledInput(InputEvent input)
    {
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
                if (button.ButtonIndex == MouseButton.Right && (!overlay.Selected || !TryGround(button.Position, out dragStart))) return;
                held = button.ButtonIndex;
                overlay.Press = overlay.Pointer = button.Position;
                overlay.Selecting = held == MouseButton.Left;
            }
            else if (button.ButtonIndex == held)
            {
                if (held == MouseButton.Left) Select(button.Position);
                else if (UpdateFormation(button.Position))
                    overlay.Movement.Issue(overlay.Front, overlay.Right, overlay.Columns);
                CancelGesture();
            }
            else return;
            GetViewport().SetInputAsHandled();
        }
        else if (input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            CancelGesture();
            overlay.Selected = false;
        }
    }

    public override void _Process(double delta)
    {
        if (held != MouseButton.None && !Input.IsMouseButtonPressed(held)) CancelGesture();
        if (held == MouseButton.Right) UpdateFormation(overlay.Pointer);
        if (overlay.Selected || held != MouseButton.None) overlay.QueueRedraw();
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
        overlay.Selected = false;
        for (int i = 0; i < overlay.Units.Count; i++)
        {
            Vector3 position = overlay.Units.Position[i];
            if (overlay.View.IsPositionBehind(position)) continue;
            Vector2 center = overlay.View.UnprojectPosition(position);
            float pixels = center.DistanceTo(overlay.View.UnprojectPosition(position + CameraRight() * overlay.Movement.Radius));
            if (!(boxSelect ? box.Grow(pixels).HasPoint(center) : center.DistanceTo(point) <= Mathf.Max(6f, pixels))) continue;
            overlay.Selected = true;
            break;
        }
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
        Vector3 line = end - dragStart;
        overlay.Preview = pointer.DistanceTo(overlay.Press) >= DragThreshold && line.LengthSquared() > 0.0001f;
        if (overlay.Preview)
        {
            overlay.Right = line.Normalized();
            overlay.Columns = Mathf.Clamp(1 + Mathf.FloorToInt((line.Length() - overlay.Movement.Radius * 2f) /
                overlay.Movement.Spacing), 1, CommandMovement.SquadSize);
            overlay.LineStart = dragStart;
            overlay.LineEnd = end;
            overlay.Front = (dragStart + end) * 0.5f +
                overlay.Right.Cross(Vector3.Up) * overlay.Movement.Radius;
        }
        else
        {
            overlay.Right = CameraRight();
            overlay.Columns = CommandMovement.DefaultColumns;
            overlay.Front = end - overlay.Right.Cross(Vector3.Up) * (1.5f * overlay.Movement.Spacing);
        }
        return true;
    }

    private bool TryGround(Vector2 point, out Vector3 ground)
    {
        Vector3 origin = overlay.View.ProjectRayOrigin(point);
        Vector3 ray = overlay.View.ProjectRayNormal(point);
        ground = default;
        if (ray.Y >= -0.00001f || origin.Y < 0f) return false;
        ground = origin - ray * (origin.Y / ray.Y);
        ground.Y = 0f;
        return true;
    }
}
