// Deployment-only selection and editable attack-move routes.
using System.Collections.Generic;
using Godot;

namespace MassiveRts;

internal sealed partial class DeploymentOrderOverlay : Node2D
{
    internal DeploymentPainter Painter { get; init; }
    internal IReadOnlyList<SoldierPlacement> AuthoredSoldiers { get; init; }
    internal Camera3D EditorCamera { get; init; }
    internal System.Func<Vector3, Vector3> EditorSurface { get; init; }
    internal delegate bool GroundPicker(Vector2 screen, out Vector3 point);
    internal GroundPicker EditorGround { get; init; }
    internal System.Action OrdersChanged { get; init; }
    private IReadOnlyList<SoldierPlacement> Soldiers => Painter?.Layout.Placements ?? AuthoredSoldiers;
    private byte Faction => Painter == null ? Factions.Red : Factions.Blue;
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

    private PanelContainer orderPopup;
    private Button move;
    private bool placingRoute;

    internal void AddControls(VBoxContainer parent)
    {
        var row = new HBoxContainer();
        parent.AddChild(row);
        var tools = new ButtonGroup();
        AddTool("Brush", 0);
        AddTool("Rectangle", 1);
        AddTool("Select", 2);

        void AddTool(string label, int mode)
        {
            var button = UIButtonFactory.Create(label, () =>
            {
                Selecting = mode == 2;
                placingRoute = false;
                anchor = null;
                editing = null;
                if (Selecting) Painter.CancelDrag();
                else Painter.SetSoldierPaintType(mode);
                QueueRedraw();
            }, UIButtonStyle.Compact);
            button.ToggleMode = true;
            button.ButtonGroup = tools;
            button.SetPressedNoSignal(mode == 0);
            row.AddChild(button);
        }

        BuildPopup();
    }

    internal void BuildPopup()
    {
        Selecting = Painter == null || Selecting;
        orderPopup = new PanelContainer { Visible = false };
        orderPopup.AddThemeStyleboxOverride("panel", UIButtonFactory.Box(UIMasterConfig.Current.Panel,
            UIMasterConfig.Current.Accent, 8));
        var layer = new CanvasLayer { Layer = 4 };
        AddChild(layer);
        layer.AddChild(orderPopup);
        var actions = new HBoxContainer();
        orderPopup.AddChild(actions);
        move = UIButtonFactory.Create("Order move", () => placingRoute = !placingRoute, UIButtonStyle.Compact);
        actions.AddChild(move);
        var seconds = new SpinBox { MinValue = 1, MaxValue = 299, Value = 5, Suffix = "s" };
        actions.AddChild(seconds);
        actions.AddChild(UIButtonFactory.Create("Wait", () =>
        {
            placingRoute = false;
            var order = new Order { Hold = (float)seconds.Value };
            foreach (var soldier in selected) orders[soldier] = order;
            OrdersChanged?.Invoke();
        }, UIButtonStyle.Compact));
        actions.AddChild(UIButtonFactory.Create("Clear", () =>
        {
            placingRoute = false;
            foreach (var soldier in selected) orders.Remove(soldier);
            OrdersChanged?.Invoke();
        }, UIButtonStyle.Compact));
    }

    private void PlaceRoute(Vector3 end)
    {
        Vector3 center = Vector3.Zero;
        foreach (var soldier in selected) center += soldier.Position();
        var order = new Order { Start = center / selected.Count, End = end };
        foreach (var soldier in selected) orders[soldier] = order;
        placingRoute = false;
        OrdersChanged?.Invoke();
    }

    public override void _Ready() { if (Painter != null) Painter.Changed += Prune; }

    public override void _ExitTree() { if (Painter != null) Painter.Changed -= Prune; }

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

    internal void Reset() { placingRoute = false; selected.Clear(); orders.Clear(); anchor = null; editing = null; }
    internal void LoadAuthoredOrders(AutoBattlerRules rules)
    {
        orders.Clear();
        var saved = rules.ReadRedOrders();
        foreach (var soldier in Soldiers)
            if (saved.TryGetValue(AutoBattlerRules.RedOrderKey(soldier.Position()), out var order)) orders[soldier] = order;
    }

    internal Vector3[] SaveAuthoredOrders() => AutoBattlerRules.WriteRedOrders(orders);

    private bool TryGround(Vector2 screen, out Vector3 point) => Painter != null
        ? Painter.TryGround(screen, out point) : EditorGround(screen, out point);

    internal Order For(SoldierPlacement soldier) => orders.GetValueOrDefault(soldier);

    internal bool Handle(InputEvent input)
    {
        if (!Selecting) return false;
        if (input is InputEventMouseMotion motion)
        {
            if (editing != null && TryGround(motion.Position, out Vector3 bend))
            {
                editing.Bent = true;
                editing.Bend = bend * 2f - (editing.Start + editing.End) * 0.5f;
            }
            return true;
        }
        if (input is not InputEventMouseButton mouse) return false;
        if (mouse.ButtonIndex == MouseButton.Right)
        {
            if (mouse.Pressed && placingRoute && selected.Count > 0 && TryGround(mouse.Position, out Vector3 end))
                PlaceRoute(end);
            return true;
        }
        if (mouse.ButtonIndex != MouseButton.Left) return false;
        if (mouse.Pressed && placingRoute && selected.Count > 0)
        {
            if (TryGround(mouse.Position, out Vector3 end)) PlaceRoute(end);
            return true;
        }
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
                foreach (var soldier in Soldiers)
                    if (soldier.Faction == Faction && box.HasPoint(Screen(soldier.Position()))) selected.Add(soldier);
            }
            anchor = null;
            if (editing != null) OrdersChanged?.Invoke();
            editing = null;
        }
        return true;
    }

    private Vector2 Screen(Vector3 point) => Painter != null
        ? Painter.Camera.UnprojectPosition(Painter.OnPreviewTerrain(point) + Vector3.Up * 0.1f)
        : EditorCamera.UnprojectPosition(EditorSurface(point) + Vector3.Up * 0.1f);

    public override void _Process(double delta)
    {
        if (Painter != null) Visible = Painter.Visible;
        UpdatePopup();
        if (!Visible || (!Selecting && orders.Count == 0)) return;
        if (!Input.IsMouseButtonPressed(MouseButton.Left))
        {
            anchor = null;
            if (editing != null) OrdersChanged?.Invoke();
            editing = null;
        }
        QueueRedraw();
    }

    private void UpdatePopup()
    {
        if (orderPopup == null) return;
        orderPopup.Visible = Visible && Selecting && selected.Count > 0 && anchor == null && editing == null;
        if (!orderPopup.Visible) return;
        move.Text = placingRoute ? "Click destination" : "Order move";
        Vector2 bounds = GetViewportRect().Size;
        float bottom = float.NegativeInfinity, left = float.PositiveInfinity, right = float.NegativeInfinity;
        foreach (var soldier in selected)
        {
            Vector2 point = Screen(soldier.Position());
            left = Mathf.Min(left, point.X);
            right = Mathf.Max(right, point.X);
            bottom = Mathf.Max(bottom, point.Y);
        }
        orderPopup.Position = new Vector2(
            Mathf.Clamp((left + right - orderPopup.Size.X) * 0.5f, 8f, Mathf.Max(8f, bounds.X - orderPopup.Size.X - 8f)),
            Mathf.Clamp(bottom + 20f, 8f, Mathf.Max(8f, bounds.Y - orderPopup.Size.Y - 175f)));
    }

    public override void _Draw()
    {
        if (!Visible || (Painter != null && (!Painter.Visible || Painter.Camera == null)) || (!Selecting && orders.Count == 0)) return;
        if (anchor is Vector2 start)
        {
            var box = new Rect2(start, GetGlobalMousePosition() - start).Abs();
            DrawRect(box, new Color(0.26f, 0.73f, 1f, 0.15f));
            DrawRect(box, new Color(0.53f, 0.85f, 1f, 0.9f), false, 1f);
        }
        drawn.Clear();
        foreach (var soldier in Soldiers)
        {
            if (Selecting && selected.Contains(soldier))
                DrawArc(Screen(soldier.Position()), 6f, 0f, Mathf.Tau, 16, Factions.Color(Faction).Lightened(0.4f), 1.5f, true);
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
