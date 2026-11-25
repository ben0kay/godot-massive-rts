// Deployment-only selection and editable attack-move routes.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal sealed partial class DeploymentOrderOverlay : Node2D
{
    internal DeploymentPainter Painter { get; init; }
    internal bool Selecting { get; private set; }
    private readonly HashSet<SoldierPlacement> selected = new();
    private readonly Dictionary<SoldierPlacement, Order> orders = new();
    private readonly HashSet<Order> drawn = new();
    private Vector2? anchor;
    private Order editing;
    internal sealed class Order
    {
        internal Vector3 Start, End, Bend;
        internal float Hold;
        internal bool Bent;
        internal Vector3 Point(float t) => Bent
            ? Start.Lerp(Bend, t).Lerp(Bend.Lerp(End, t), t)
            : Start.Lerp(End, t);
    }

    internal void AddControls(VBoxContainer parent)
    {
        var row = new HBoxContainer();
        parent.AddChild(row);
        var select = new CheckButton { Text = "Select troops" };
        row.AddChild(select);
        select.Toggled += on => { Selecting = on; anchor = null; editing = null; Painter.CancelDrag(); QueueRedraw(); };
        parent.AddChild(new Label { Text = "Box-select · Right-click to route · Drag route to bend" });
        var hold = new HBoxContainer();
        parent.AddChild(hold);
        var seconds = new SpinBox { MinValue = 1, MaxValue = 299, Value = 5, Suffix = "s" };
        hold.AddChild(seconds);
        hold.AddChild(UIButtonFactory.Create("Hold", () =>
        {
            var order = new Order { Hold = (float)seconds.Value };
            foreach (var soldier in selected) orders[soldier] = order;
        }, UIButtonStyle.Compact));
        hold.AddChild(UIButtonFactory.Create("Clear orders", () =>
        {
            foreach (var soldier in selected) orders.Remove(soldier);
        }, UIButtonStyle.Compact));
    }

    public override void _Ready() => Painter.Changed += Prune;

    public override void _ExitTree() => Painter.Changed -= Prune;

    private void Prune()
    {
        selected.RemoveWhere(soldier => !StillPlaced(soldier));
        removed.Clear();
        foreach (var soldier in orders.Keys)
            if (!StillPlaced(soldier)) removed.Add(soldier);
        foreach (var soldier in removed) orders.Remove(soldier);
    }

    private readonly List<SoldierPlacement> removed = new();
    private bool StillPlaced(SoldierPlacement soldier) =>
        Painter.Layout.TryGet(new Vector2I(soldier.X, soldier.Z), out var current) && current == soldier;

    internal void Reset() { selected.Clear(); orders.Clear(); anchor = null; editing = null; }
    internal Order For(SoldierPlacement soldier) => orders.GetValueOrDefault(soldier);

    internal bool Handle(InputEvent input)
    {
        if (!Selecting) return false;
        if (input is InputEventMouseMotion motion)
        {
            if (editing != null && Painter.TryGround(motion.Position, out Vector3 bend))
            {
                editing.Bent = true;
                editing.Bend = bend * 2f - (editing.Start + editing.End) * 0.5f;
            }
            return true;
        }
        if (input is not InputEventMouseButton mouse) return false;
        if (mouse.ButtonIndex == MouseButton.Right)
        {
            if (mouse.Pressed && selected.Count > 0 && Painter.TryGround(mouse.Position, out Vector3 end))
            {
                Vector3 center = Vector3.Zero;
                foreach (var soldier in selected) center += soldier.Position();
                var order = new Order { Start = center / selected.Count, End = end };
                foreach (var soldier in selected) orders[soldier] = order;
            }
            return true;
        }
        if (mouse.ButtonIndex != MouseButton.Left) return false;
        if (mouse.Pressed)
        {
            drawn.Clear();
            foreach (var soldier in selected)
            {
                var order = For(soldier);
                if (order == null || order.Hold > 0f || !drawn.Add(order)) continue;
                for (int i = 0; i < 16; i++)
                {
                    Vector2 a = Screen(order.Point(i / 16f)), b = Screen(order.Point((i + 1) / 16f));
                    if (Geometry2D.GetClosestPointToSegment(mouse.Position, a, b).DistanceSquaredTo(mouse.Position) > 81f) continue;
                    editing = order;
                    return true;
                }
            }
            anchor = mouse.Position;
        }
        else
        {
            if (anchor is Vector2 start)
            {
                selected.Clear();
                var box = new Rect2(start, mouse.Position - start).Abs().Grow(4f);
                foreach (var soldier in Painter.Layout.Placements)
                    if (soldier.Faction == Factions.Blue && box.HasPoint(Screen(soldier.Position()))) selected.Add(soldier);
            }
            anchor = null;
            editing = null;
        }
        return true;
    }

    private Vector2 Screen(Vector3 point) => Painter.Camera.UnprojectPosition(Painter.OnPreviewTerrain(point) + Vector3.Up * 0.1f);

    public override void _Process(double delta)
    {
        Visible = Painter.Visible;
        if (!Visible || (!Selecting && orders.Count == 0)) return;
        if (!Input.IsMouseButtonPressed(MouseButton.Left)) { anchor = null; editing = null; }
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!Painter.Visible || Painter.Camera == null || (!Selecting && orders.Count == 0)) return;
        if (anchor is Vector2 start)
        {
            var box = new Rect2(start, GetGlobalMousePosition() - start).Abs();
            DrawRect(box, new Color(0.26f, 0.73f, 1f, 0.15f));
            DrawRect(box, new Color(0.53f, 0.85f, 1f, 0.9f), false, 1f);
        }
        drawn.Clear();
        foreach (var soldier in Painter.Layout.Placements)
        {
            if (Selecting && selected.Contains(soldier))
                DrawArc(Screen(soldier.Position()), 6f, 0f, Mathf.Tau, 16, new Color(0.55f, 0.89f, 1f), 1.5f, true);
            var order = For(soldier);
            if (order == null || !drawn.Add(order)) continue;
            if (order.Hold > 0f)
                DrawString(ThemeDB.FallbackFont, Screen(soldier.Position()) + new Vector2(8f, -8f), $"Hold {order.Hold:0}s");
            else
            {
                const float spacing = 9f;
                float next = 0f;
                Vector2 a = Screen(order.Start);
                for (int i = 1; i <= 24; i++)
                {
                    Vector2 b = Screen(order.Point(i / 24f));
                    float length = a.DistanceTo(b);
                    if (length > 0.001f)
                    {
                        for (; next < length; next += spacing)
                            DrawCircle(a.Lerp(b, next / length), 1.4f, new Color(0.94f, 0.97f, 1f, 0.84f));
                        next -= length;
                    }
                    a = b;
                }
                DrawCircle(Screen(order.End), 3.5f, Colors.White);
            }
        }
    }
}
