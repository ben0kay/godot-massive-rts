// Converts click, box select and right-drag gestures into squad orders.
using System;
using System.Runtime.InteropServices;
using Godot;

namespace MassiveRts;

public partial class SquadCommandInput : Node
{
    private const float DragThreshold = 6f;
    private CommandOverlay overlay;
    private MouseButton held;
    private Vector3 dragStart;
    private bool rightShift, freeformDrag, attackFormationDrag;

    private CommandMovement movement;
    private CommandBuildings buildings;
    private CommandBottomHud hud;
    private MeshInstance3D ghost;
    private bool placing;
    private CommandBuildType buildType;
    private Mesh fabricatorPreview;
    private readonly BoxMesh towerPreview = new();
    private bool ctrlCandidate;
    private bool leftControl, rightControl;

    internal void Initialize(CommandOverlay display, CommandMovement orders, CommandBuildings world, CommandBottomHud fabrication)
    {
        overlay = display;
        movement = orders;
        buildings = world;
        hud = fabrication;
        ghost = CommandStructure.MakePreview();
        GetParent().AddChild(ghost);
        fabricatorPreview = ghost.Mesh;
        hud.PlaceRequested = type =>
        {
            CancelGesture();
            overlay.AttackMove = false;
            placing = true;
            buildType = type;
            ghost.Mesh = type == CommandBuildType.LaserTower ? towerPreview : fabricatorPreview;
            hud.MenuOpen = false;
        };
    }

    // Observe combinations before GUI handling, without consuming CTRL or its shortcuts.
    public override void _Input(InputEvent input)
    {
        // Only the physical right Shift activates freeform mode; left Shift is unchanged.
        if (input is InputEventKey shift && shift.Keycode == Key.Shift)
        {
            if (shift.Location == KeyLocation.Right) rightShift = shift.Pressed;
            return;
        }
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
                    if (TryGround(click.Position, out Vector3 point) && buildings.Place(buildings.Snap(point, buildType), buildType))
                        EndPlacement(true);
                }
                else return;
                GetViewport().SetInputAsHandled();
            }
            else if (input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape, ShiftPressed: false })
            {
                EndPlacement(true);
                GetViewport().SetInputAsHandled();
            }
            return;
        }

        if (input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.K } &&
            overlay.Selection.Count > 0 && held == MouseButton.None)
        {
            overlay.AttackMove = !overlay.AttackMove;
            overlay.QueueRedraw();
            GetViewport().SetInputAsHandled();
            return;
        }

        // K arms attack-move. Its left-click now shares the existing drag/release
        // lifecycle so a tap keeps the centered attack-move and a drag defines the front.
        if (overlay.AttackMove && input is InputEventMouseButton
            { Pressed: true, ButtonIndex: MouseButton.Right })
        {
            overlay.AttackMove = false;
            overlay.QueueRedraw();
        }

        if (buildings.CanSetRally(overlay.Building) &&
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
            if (held == MouseButton.Right || attackFormationDrag) UpdateFormation(motion.Position);
            GetViewport().SetInputAsHandled();
        }
        else if (input is InputEventMouseButton button &&
            (button.ButtonIndex == MouseButton.Left || button.ButtonIndex == MouseButton.Right))
        {
            if (button.Pressed)
            {
                if (held != MouseButton.None) return;
                if (button.ButtonIndex == MouseButton.Right && (overlay.Selection.Count == 0 || !TryGround(button.Position, out dragStart))) return;
                if (button.ButtonIndex == MouseButton.Left && overlay.AttackMove)
                {
                    if (overlay.Selection.Count == 0 || !TryGround(button.Position, out dragStart)) return;
                    attackFormationDrag = true;
                }
                held = button.ButtonIndex;
                overlay.Press = overlay.Pointer = button.Position;
                // K + left-drag is an order preview, not a selection box.
                overlay.Selecting = held == MouseButton.Left && !attackFormationDrag;
                freeformDrag = held == MouseButton.Right && rightShift;
                overlay.FreeformPreview = freeformDrag;
                if (freeformDrag) overlay.Freeform.Begin(dragStart);
            }
            else if (button.ButtonIndex == held)
            {
                if (held == MouseButton.Left && !attackFormationDrag) Select(button.Position);
                else if (UpdateFormation(button.Position, finish: true))
                {
                    ReadOnlySpan<int> selected = CollectionsMarshal.AsSpan(overlay.Selection);
                    if (freeformDrag && overlay.Preview)
                        movement.IssueSlots(selected, CollectionsMarshal.AsSpan(overlay.Freeform.Slots));
                    else
                        movement.Issue(selected, overlay.Front, overlay.Right, overlay.Columns, overlay.Spacing,
                            attack: attackFormationDrag);
                }
                if (attackFormationDrag) overlay.AttackMove = false;
                CancelGesture();
            }
            else return;
            GetViewport().SetInputAsHandled();
        }
        else if (input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape, ShiftPressed: false })
        {
            bool consumed = held != MouseButton.None || overlay.AttackMove || overlay.Selection.Count > 0 ||
                overlay.Building != null || GodotObject.IsInstanceValid(overlay.Deposit) || hud.MenuOpen;
            CancelGesture();
            if (hud.MenuOpen) hud.MenuOpen = false;
            else if (overlay.AttackMove)
            {
                overlay.AttackMove = false;
                overlay.QueueRedraw();
            }
            else ClearSelection();
            if (consumed) GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        if (held != MouseButton.None && !Input.IsMouseButtonPressed(held)) CancelGesture();
        if (held == MouseButton.Right || attackFormationDrag) UpdateFormation(overlay.Pointer);
        if (placing)
        {
            ghost.Visible = TryGround(GetViewport().GetMousePosition(), out Vector3 point);
            if (ghost.Visible)
            {
                var area = buildings.Snap(point, buildType);
                ghost.Position = buildings.Center(area);
                if (buildType == CommandBuildType.LaserTower)
                {
                    Vector2 size = area.Max - area.Min;
                    var dimensions = new Vector3(size.X, 1f, size.Y);
                    if (towerPreview.Size != dimensions) towerPreview.Size = dimensions;
                    ghost.Position += Vector3.Up * 0.5f;
                }
                ((StandardMaterial3D)ghost.MaterialOverride).AlbedoColor = buildings.CanPlace(area, buildType)
                    ? new Color(0.2f, 0.92f, 1f, 0.38f) : new Color(1f, 0.22f, 0.24f, 0.40f);
            }
        }
        if (overlay.Building?.Destroyed == true ||
            overlay.Deposit != null && !GodotObject.IsInstanceValid(overlay.Deposit)) ClearSelection();
        if (overlay.Selection.Count > 0 || overlay.Building != null || held != MouseButton.None) overlay.QueueRedraw();
    }

    private void CancelGesture()
    {
        held = MouseButton.None;
        freeformDrag = attackFormationDrag = false;
        overlay.FreeformPreview = false;
        overlay.Freeform.Clear();
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
            overlay.Building = buildings.Pick(overlay.View, point, out overlay.Deposit);
            if (overlay.Building != null || overlay.Deposit != null) return;
        }

        int nearest = -1;
        float bestDistance = float.PositiveInfinity;
        Vector3 right = CameraRight();
        for (int i = 0; i < overlay.Units.Count; i++)
        {
            Vector3 position = overlay.Units.Position[i];
            if (overlay.Units.Faction[i] != Factions.Blue || overlay.Units.Health[i] <= 0 || overlay.View.IsPositionBehind(position)) continue;
            Vector2 center = overlay.View.UnprojectPosition(position);
            float pixels = center.DistanceTo(overlay.View.UnprojectPosition(position + right * overlay.Radius(i)));
            if (boxSelect)
            {
                if (!box.Grow(pixels).HasPoint(center)) continue;
                overlay.Selection.Add(i);
                continue;
            }
            float distance = center.DistanceTo(point);
            if (distance > Mathf.Max(6f, pixels) || distance >= bestDistance) continue;
            nearest = i;
            bestDistance = distance;
        }
        if (nearest >= 0) overlay.Selection.Add(nearest);
        float radius = 0;
        foreach (int id in overlay.Selection) radius = Mathf.Max(radius, overlay.Radius(id));
        overlay.Spacing = radius * 2f + 0.3f;
    }

    private void ClearSelection()
    {
        overlay.AttackMove = false;
        overlay.Selection.Clear();
        overlay.Building = null;
        overlay.Deposit = null;
        overlay.QueueRedraw();
    }

    private Vector3 CameraRight()
    {
        Vector3 right = overlay.View.GlobalTransform.Basis.X;
        return new Vector3(right.X, 0f, right.Z).Normalized();
    }

    private bool UpdateFormation(Vector2 pointer, bool finish = false)
    {
        if (!TryGround(pointer, out Vector3 end))
        {
            overlay.Preview = false;
            return false;
        }
        if (freeformDrag)
        {
            bool changed = overlay.Freeform.Add(end, final: finish);
            if (!changed && !finish) return true;
            overlay.LineStart = dragStart;
            overlay.LineEnd = end;
            // Arc length also supports a loop whose end lies next to its start.
            overlay.Preview = overlay.Freeform.Length >= Mathf.Max(0.4f, overlay.Spacing * 0.5f);
            if (overlay.Preview)
                overlay.Freeform.BuildSlots(overlay.Selection.Count, overlay.Spacing);
            else CenterFormation(end);
            return true;
        }
        float radius = (overlay.Spacing - 0.3f) * 0.5f;
        Vector3 line = end - dragStart;
        line.Y = 0f;
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
        else CenterFormation(end);
        return true;
    }

    private void CenterFormation(Vector3 end)
    {
        overlay.Right = CameraRight();
        overlay.Columns = Mathf.CeilToInt(Mathf.Sqrt(overlay.Selection.Count));
        int lastRow = (overlay.Selection.Count - 1) / overlay.Columns;
        overlay.Front = end - overlay.Right.Cross(Vector3.Up) * (lastRow * 0.5f * overlay.Spacing);
    }

    private bool TryGround(Vector2 point, out Vector3 ground) =>
        buildings.TryGround(overlay.View, point, out ground) && buildings.OnMap(ground);
}
