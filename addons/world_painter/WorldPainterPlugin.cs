// Paints the open level from the 3D viewport and records one undo action per gesture.
#if TOOLS
using System.Collections.Generic;
using System.Linq;
using Godot;
using Cells = Godot.Collections.Array<Godot.Vector2I>;

namespace MassiveRts;

[Tool]
public partial class WorldPainterPlugin : EditorPlugin
{
    private const int PassInput = (int)AfterGuiInput.Pass, StopInput = (int)AfterGuiInput.Stop;
    private static readonly int[] BrushSizes = { 1, 3, 5, 10 };
    private EditorDock dock;
    private CheckButton paintMode, showGrid;
    private OptionButton layer, tool, size, shape, action;
    private Label status;
    private MeshInstance3D cursor;
    private WorldPaint strokeTarget;
    private WorldPaintData originalData;
    private HashSet<Vector2I> rocks, water;
    private Vector2I start, last;
    private bool waterStroke, rectangleStroke, eraseStroke, roundStroke, overBudget;
    private int strokeSize, activeCells;

    // =========================================================
    public override void _EnterTree()
    {
        dock = new EditorDock
        {
            Title = "World Painter",
            LayoutKey = "MassiveRtsWorldPainter",
            DefaultSlot = EditorDock.DockSlot.RightUl,
            AvailableLayouts = EditorDock.DockLayout.Vertical
        };
        var form = new VBoxContainer { CustomMinimumSize = new Vector2(220, 0) };
        paintMode = new CheckButton { Text = "Paint in viewport" };
        form.AddChild(paintMode);
        layer = AddOptions(form, "Layer", "Rock / Cliff", "Deep Water");
        tool = AddOptions(form, "Tool", "Brush", "Filled rectangle");
        size = AddOptions(form, "Brush size", "1 × 1", "3 × 3", "5 × 5", "10 × 10");
        shape = AddOptions(form, "Brush shape", "Square", "Circle");
        action = AddOptions(form, "Action", "Paint", "Erase");
        showGrid = new CheckButton { Text = "Local grid", ButtonPressed = true };
        form.AddChild(showGrid);
        status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        form.AddChild(status);
        dock.AddChild(form);
        AddDock(dock);
        SetInputEventForwardingAlwaysEnabled();
        SceneChanged += OnSceneChanged;
        dock.VisibilityChanged += OnDockVisibilityChanged;
        paintMode.Toggled += OnPaintModeToggled;
        RefreshScene();
    }

    public override void _ExitTree()
    {
        ResetStroke();
        ClearCursor();
        SceneChanged -= OnSceneChanged;
        paintMode.Toggled -= OnPaintModeToggled;
        dock.VisibilityChanged -= OnDockVisibilityChanged;
        RemoveDock(dock);
        dock.QueueFree();
    }

    private static OptionButton AddOptions(VBoxContainer form, string label, params string[] values)
    {
        form.AddChild(new Label { Text = label });
        var choices = new OptionButton();
        foreach (var value in values) choices.AddItem(value);
        form.AddChild(choices);
        return choices;
    }

    private WorldPaint CurrentPaint()
    {
        var root = EditorInterface.Singleton.GetEditedSceneRoot();
        return root is CommandSandbox ? root.GetNodeOrNull<WorldPaint>("WorldPaint") : null;
    }

    private void RefreshScene()
    {
        paintMode.SetPressedNoSignal(false);
        paintMode.Disabled = CurrentPaint() == null;
        status.Text = paintMode.Disabled ? "Open a command level with a WorldPaint node." :
            "Enable painting, then drag in the 3D viewport.";
    }

    private void OnSceneChanged(Node scene)
    {
        ResetStroke();
        ClearCursor();
        RefreshScene();
    }

    private void OnDockVisibilityChanged()
    {
        if (!dock.IsVisibleInTree()) OnPaintModeToggled(false);
    }

    private void OnPaintModeToggled(bool enabled)
    {
        if (enabled) return;
        paintMode.SetPressedNoSignal(false);
        ResetStroke();
        ClearCursor();
    }

    // =========================================================
    public override int _Forward3DGuiInput(Camera3D camera, InputEvent @event)
    {
        if (!paintMode.ButtonPressed || !dock.IsVisibleInTree()) return PassInput;
        var target = CurrentPaint();
        if (target == null)
        {
            OnPaintModeToggled(false);
            RefreshScene();
            return PassInput;
        }
        if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            OnPaintModeToggled(false);
            return StopInput;
        }
        if (@event is not InputEventMouse mouse) return PassInput;
        if (@event is InputEventMouseButton button && button.ButtonIndex != MouseButton.Left)
        {
            if (button.Pressed && strokeTarget != null) ResetStroke();
            ClearCursor();
            return PassInput;
        }

        bool hit = TryCell(camera, mouse.Position, target, out var cell);
        bool released = @event is InputEventMouseButton { Pressed: false };
        if (strokeTarget != null && (released || @event is InputEventMouseMotion &&
            (mouse.ButtonMask & MouseButtonMask.Left) == 0))
        {
            if (hit && released) UpdateStroke(cell);
            CommitStroke();
            ClearCursor();
            return released ? StopInput : PassInput;
        }

        if (@event is InputEventMouseButton { Pressed: true })
        {
            if (hit && target.Contains(cell))
            {
                BeginStroke(target, cell);
                DrawCursor(target, cell);
            }
            return StopInput;
        }

        if (!hit)
        {
            ClearCursor();
            return strokeTarget == null ? PassInput : StopInput;
        }
        if (strokeTarget != null) UpdateStroke(cell);
        DrawCursor(target, cell);
        return strokeTarget == null ? PassInput : StopInput;
    }

    private static bool TryCell(Camera3D camera, Vector2 pointer, WorldPaint target, out Vector2I cell)
    {
        Vector3 origin = camera.ProjectRayOrigin(pointer);
        Vector3 ray = camera.ProjectRayNormal(pointer);
        cell = default;
        if (ray.Y >= -0.00001f || origin.Y < 0f) return false;
        var toFlat = ray * (-origin.Y / ray.Y);
        float fraction = Mathf.Min(1f, target.Surface?.SurfaceHit(origin, toFlat, 1f) ?? 1f);
        var point = origin + toFlat * fraction;
        float edge = target.MapHalfWidth;
        // Limit off-map drags to one cell beyond the playable bounds.
        float margin = edge + WorldPaint.CellSize;
        cell = new Vector2I(Mathf.FloorToInt(Mathf.Clamp(point.X, -margin, margin) / WorldPaint.CellSize),
            Mathf.FloorToInt(Mathf.Clamp(point.Z, -margin, margin) / WorldPaint.CellSize));
        return true;
    }

    // =========================================================
    private void BeginStroke(WorldPaint target, Vector2I cell)
    {
        strokeTarget = target;
        originalData = target.Data;
        rocks = new HashSet<Vector2I>(originalData?.RockCells ?? new Cells());
        water = new HashSet<Vector2I>(originalData?.WaterCells ?? new Cells());
        activeCells = rocks.Count(target.Contains) + water.Count(target.Contains);
        start = last = cell;
        waterStroke = layer.Selected == 1;
        rectangleStroke = tool.Selected == 1;
        eraseStroke = action.Selected == 1;
        roundStroke = shape.Selected == 1;
        strokeSize = BrushSizes[size.Selected];
        overBudget = false;
        if (!rectangleStroke) Stamp(cell);
        if (overBudget) ShowBudgetError();
        else if (!rectangleStroke) target.Preview(rocks, water);
    }

    private void UpdateStroke(Vector2I cell)
    {
        if (cell == last || overBudget) return;
        if (!rectangleStroke)
        {
            // Sample at half-cell intervals and connect diagonals with a cardinal step.
            Vector2 offset = (Vector2)(cell - last);
            int steps = 2 * Mathf.Max(Mathf.Abs(cell.X - last.X), Mathf.Abs(cell.Y - last.Y));
            Vector2I previous = last;
            for (int i = 1; i <= steps && !overBudget; i++)
            {
                Vector2 point = (Vector2)last + offset * (i / (float)steps);
                Vector2I next = new(Mathf.RoundToInt(point.X), Mathf.RoundToInt(point.Y));
                if (next.X != previous.X && next.Y != previous.Y)
                    Stamp(next.X > previous.X ? new Vector2I(next.X, previous.Y) : new Vector2I(previous.X, next.Y));
                if (!overBudget && next != previous) Stamp(next);
                previous = next;
            }
            if (overBudget)
            {
                strokeTarget.Preview(null, null);
                ShowBudgetError();
            }
            else strokeTarget.Preview(rocks, water);
        }
        last = cell;
    }

    private void Stamp(Vector2I cell)
    {
        int lower = -(strokeSize - 1) / 2;
        for (int z = 0; z < strokeSize && !overBudget; z++)
        for (int x = 0; x < strokeSize && !overBudget; x++)
            if (InsideBrush(x, z, strokeSize, roundStroke))
                EditCell(cell + new Vector2I(lower + x, lower + z));
    }

    private static bool InsideBrush(int x, int z, int width, bool round)
    {
        if (!round) return true;
        var offset = new Vector2(x, z) - Vector2.One * ((width - 1) * 0.5f);
        return offset.LengthSquared() <= width * width * 0.25f;
    }

    private void EditCell(Vector2I cell)
    {
        if (!strokeTarget.Contains(cell)) return;
        var selected = waterStroke ? water : rocks;
        if (eraseStroke)
        {
            if (selected.Remove(cell)) activeCells--;
            return;
        }
        var other = waterStroke ? rocks : water;
        if (other.Remove(cell)) activeCells--;
        if (selected.Add(cell)) activeCells++;
        overBudget = activeCells > WorldPaint.MaxPaintedCells;
    }

    private void CommitStroke()
    {
        var target = strokeTarget;
        if (target == null) return;
        if (rectangleStroke)
        {
            var rect = ClippedRectangle(target, start, last);
            for (int z = rect.Position.Y; z < rect.End.Y && !overBudget; z++)
            for (int x = rect.Position.X; x < rect.End.X && !overBudget; x++)
                EditCell(new Vector2I(x, z));
        }

        if (overBudget || !target.TryMerge(new(rocks), new(water), out _, out _))
        {
            ShowBudgetError();
            ResetStroke();
            return;
        }
        if ((originalData == null && rocks.Count == 0 && water.Count == 0) ||
            (originalData != null && rocks.SetEquals(originalData.RockCells) && water.SetEquals(originalData.WaterCells)))
        {
            ResetStroke();
            return;
        }

        var data = new WorldPaintData { ResourceLocalToScene = true, RockCells = Sorted(rocks), WaterCells = Sorted(water) };
        if (originalData != null)
        {
            data.RockHeight = originalData.RockHeight;
            data.RockColor = originalData.RockColor;
            data.WaterColor = originalData.WaterColor;
        }
        string label = (eraseStroke ? "Erase " : "Paint ") + (waterStroke ? "deep water" : "rocks");
        var history = GetUndoRedo();
        var before = originalData;
        ResetStroke();
        history.CreateAction(label, UndoRedo.MergeMode.Disable, EditorInterface.Singleton.GetEditedSceneRoot());
        history.AddDoProperty(target, nameof(WorldPaint.Data), data);
        history.AddUndoProperty(target, nameof(WorldPaint.Data), before);
        history.CommitAction();
        status.Text = label + " complete.";
    }

    private static Cells Sorted(HashSet<Vector2I> cells) =>
        new(cells.OrderBy(cell => cell.Y).ThenBy(cell => cell.X));

    private void ShowBudgetError() =>
        status.Text = $"Stroke exceeds the level limit of {WorldPaint.MaxPaintedCells:N0} cells or {WorldPaint.MaxPaintedRectangles} merged regions. Try a smaller area or simplify the outline.";

    private void ResetStroke()
    {
        if (IsInstanceValid(strokeTarget)) strokeTarget.Preview(null, null);
        strokeTarget = null;
        originalData = null;
        rocks = water = null;
    }

    // =========================================================
    private static Rect2I ClippedRectangle(WorldPaint target, Vector2I a, Vector2I b)
    {
        int edge = target.GridEdge;
        var min = new Vector2I(Mathf.Clamp(Mathf.Min(a.X, b.X), -edge, edge),
            Mathf.Clamp(Mathf.Min(a.Y, b.Y), -edge, edge));
        var end = new Vector2I(Mathf.Clamp(Mathf.Max(a.X, b.X) + 1, -edge, edge),
            Mathf.Clamp(Mathf.Max(a.Y, b.Y) + 1, -edge, edge));
        return new Rect2I(min, end - min);
    }

    private void DrawCursor(WorldPaint target, Vector2I cell)
    {
        if (strokeTarget == null && !target.Contains(cell))
        {
            ClearCursor();
            return;
        }
        if (!IsInstanceValid(cursor))
        {
            cursor = new MeshInstance3D { Name = "WorldPaintCursor", TopLevel = true };
            target.AddChild(cursor, false, Node.InternalMode.Back);
            cursor.GlobalTransform = Transform3D.Identity;
        }

        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Lines);
        surface.SetMaterial(new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            NoDepthTest = true
        });
        bool hasLines = false;
        Color outline = (strokeTarget != null ? eraseStroke : action.Selected == 1) ? Colors.OrangeRed : Colors.LightCyan;
        if (showGrid.ButtonPressed)
        {
            var grid = ClippedRectangle(target, cell - Vector2I.One * 6, cell + Vector2I.One * 6);
            var gridColor = new Color("#657481");
            for (int z = grid.Position.Y; z <= grid.End.Y; z++)
                Line(new(grid.Position.X, z), new(grid.End.X, z), gridColor);
            for (int x = grid.Position.X; x <= grid.End.X; x++)
                Line(new(x, grid.Position.Y), new(x, grid.End.Y), gridColor);
        }

        if (strokeTarget != null ? rectangleStroke : tool.Selected == 1)
        {
            var area = ClippedRectangle(target, strokeTarget == null ? cell : start, cell);
            var a = area.Position;
            var b = new Vector2I(area.End.X, a.Y);
            var c = new Vector2I(a.X, area.End.Y);
            Line(a, b, outline); Line(b, area.End, outline);
            Line(area.End, c, outline); Line(c, a, outline);
            if (strokeTarget != null && !overBudget)
                status.Text = $"Rectangle {area.Size.X} × {area.Size.Y} cells";
        }
        else
        {
            int width = strokeTarget == null ? BrushSizes[size.Selected] : strokeSize;
            bool round = strokeTarget == null ? shape.Selected == 1 : roundStroke;
            int lower = -(width - 1) / 2;
            var footprint = new HashSet<Vector2I>();
            for (int z = 0; z < width; z++)
            for (int x = 0; x < width; x++)
            {
                var item = cell + new Vector2I(lower + x, lower + z);
                if (InsideBrush(x, z, width, round) && target.Contains(item)) footprint.Add(item);
            }
            foreach (var a in footprint)
            {
                var b = a + Vector2I.Right;
                var c = a + Vector2I.Down;
                var d = a + Vector2I.One;
                if (!footprint.Contains(a + Vector2I.Up)) Line(a, b, outline);
                if (!footprint.Contains(b)) Line(b, d, outline);
                if (!footprint.Contains(c)) Line(c, d, outline);
                if (!footprint.Contains(a + Vector2I.Left)) Line(a, c, outline);
            }
        }
        cursor.Mesh = hasLines ? surface.Commit() : null;

        void Line(Vector2I a, Vector2I b, Color color)
        {
            int segments = Mathf.Max(Mathf.Abs(b.X - a.X), Mathf.Abs(b.Y - a.Y));
            if (segments == 0) return;
            hasLines = true;
            surface.SetColor(color);
            for (int i = 0; i < segments; i++)
            {
                surface.AddVertex(Point(i / (float)segments));
                surface.AddVertex(Point((i + 1) / (float)segments));
            }

            Vector3 Point(float t)
            {
                Vector2 xz = ((Vector2)a).Lerp((Vector2)b, t) * WorldPaint.CellSize;
                return new Vector3(xz.X, (target.Surface?.At(xz.X, xz.Y) ?? 0f) + 0.16f, xz.Y);
            }
        }
    }

    private void ClearCursor()
    {
        if (IsInstanceValid(cursor)) cursor.QueueFree();
        cursor = null;
    }
}
#endif
