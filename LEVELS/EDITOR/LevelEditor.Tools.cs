// Edits the shared battlefield scene with grid brushes and reversible object operations.
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Cells = Godot.Collections.Array<Godot.Vector2I>;

namespace MassiveRts;

public partial class LevelEditor
{
    private enum Tool { Select, Move, Erase, Rock, Water, Hill, Wall, Soldiers, Deployment, Citadel, Fabricator, Carbon, Camera }
    private const float Grid = DeploymentLayout.CellSize;
    private Tool tool;
    private Node3D selected, layout;
    private MeshInstance3D cursor;
    private WorldPaint paint;
    private int brushRadius = 2;
    private Vector2I? previousCell;
    private (Cells Rocks, Cells Water, Cells Blue)? beforePaint;
    private HashSet<Vector2I> rocks, water, deployment;
    private bool paintChanged;
    private float hillRadius = 16, hillRise = 5;
    private float wallWidth = 10, wallDepth = 4, wallHeight = 2, yaw;
    private int unitIndex, groupSize = 1, depositCarbon = 5000;
    private bool red = true;

    // =========================================================
    private void SetTools()
    {
        toolPicker.Clear();
        foreach (Tool entry in Enum.GetValues<Tool>())
        {
            if (mode.Preset && entry is Tool.Citadel or Tool.Fabricator or Tool.Carbon) continue;
            if (!mode.Preset && entry == Tool.Deployment) continue;
            toolPicker.AddItem(entry switch
            {
                Tool.Rock => "Paint rocks", Tool.Water => "Paint water", Tool.Hill => "Place hill",
                Tool.Wall => "Place solid wall", Tool.Soldiers => "Place soldier formation",
                Tool.Deployment => "Paint Blue deployment zone", Tool.Move => "Move selection",
                Tool.Camera => "Place opening camera", Tool.Carbon => "Place Carbon deposit",
                Tool.Fabricator => "Place starting Fabricator", Tool.Citadel => "Move Citadel",
                _ => entry.ToString()
            }, (int)entry);
        }
        toolPicker.Select(0);
        UpdateTool();
    }

    private void EnsurePaint()
    {
        paint = map.GetNodeOrNull<WorldPaint>("WorldPaint");
        if (paint == null)
        {
            paint = new WorldPaint { Name = "WorldPaint", Data = new WorldPaintData { ResourceLocalToScene = true } };
            map.AddChild(paint);
            paint.Owner = map;
        }
        paint.Data ??= new WorldPaintData { ResourceLocalToScene = true };
    }

    private void FrameEditor()
    {
        if (camera == null || runningTest != null) return;
        camera.TopDown = planView.ButtonPressed;
        camera.MapOverviewEnabled = true;
        camera.SuspendPanWhileEditingText = true;
        camera.MaximumZoomSize = map.MapSize * 1.6f;
        camera.SetOpeningView(Vector3.Zero, map.MapSize * 0.95f, camera.TopDown ? 0 : 45);
    }

    private void RefreshMap(bool terrainChanged = false)
    {
        if (terrainChanged) map.GetNodeOrNull<CommandTerrain>("Terrain")?.Rebuild(force: true);
        map.UpdateGroundBounds();
        paint.Compile();
        RefreshLayout();
    }

    private void RefreshLayout()
    {
        if (layout != null)
        {
            map.RemoveChild(layout);
            layout.QueueFree();
        }
        layout = LevelLayoutPreview.Build(map);
        if (map.GetNodeOrNull<Node3D>("StartingFabricators") is Node3D fabricators)
            foreach (var child in fabricators.GetChildren())
                if (child is Node3D marker)
                {
                    var model = CommandStructure.MakePreview();
                    model.Visible = true;
                    layout.AddChild(model);
                    model.GlobalPosition = new Vector3(marker.GlobalPosition.X, Surface(marker.GlobalPosition), marker.GlobalPosition.Z);
                }
        if (map.GetNodeOrNull<Marker3D>("CameraStart") is Marker3D opening)
        {
            var label = new Label3D { Text = "Opening camera", FontSize = 28, PixelSize = 0.07f,
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true };
            layout.AddChild(label);
            label.GlobalPosition = new Vector3(opening.GlobalPosition.X, Surface(opening.GlobalPosition) + 2f, opening.GlobalPosition.Z);
            label.Visible = !previewing;
        }
    }

    // =========================================================
    private void UpdateTool()
    {
        tool = (Tool)toolPicker.GetSelectedId();
        foreach (var child in context.GetChildren())
        {
            context.RemoveChild(child);
            child.QueueFree();
        }
        switch (tool)
        {
            case Tool.Select:
            case Tool.Move:
                Heading(context, selected == null ? "Click an object to select it." : $"Selected · {selected.Name}", 15);
                if (selected != null)
                {
                    Button(context, "Move selected", () => SwitchTool(Tool.Move));
                    Button(context, "Delete selected", () => RemoveObject(selected));
                    EditSelected(selected);
                }
                break;
            case Tool.Rock:
            case Tool.Water:
            case Tool.Deployment:
            case Tool.Erase:
                Number(context, "Brush radius (cells)", brushRadius, 1, 14).ValueChanged += value => brushRadius = (int)value;
                Heading(context, tool == Tool.Erase ? "Click object to remove it.\nDrag over paint to erase it." : "Drag to paint. Right-drag to erase.", 15);
                break;
            case Tool.Hill:
                Number(context, "Radius", hillRadius, 8, 256).ValueChanged += value => hillRadius = (float)value;
                Number(context, "Rise", hillRise, 0.5, 80, 0.5).ValueChanged += value => hillRise = (float)value;
                break;
            case Tool.Wall:
                Number(context, "Width", wallWidth, 1, 500).ValueChanged += value => wallWidth = (float)value;
                Number(context, "Depth", wallDepth, 1, 500).ValueChanged += value => wallDepth = (float)value;
                Number(context, "Height", wallHeight, 0.5, 80, 0.5).ValueChanged += value => wallHeight = (float)value;
                break;
            case Tool.Soldiers:
                UnitPicker(context, unitIndex, index => unitIndex = index);
                if (!mode.Preset)
                    Choice(context, "Faction", new[] { "Blue", "Red" }, red ? 1 : 0, index => red = index == 1);
                Number(context, "Soldiers per formation", groupSize, 1, 256).ValueChanged += value => groupSize = (int)value;
                Number(context, "Facing (degrees)", yaw, -180, 360).ValueChanged += value => yaw = (float)value;
                Heading(context, "Click to place a formation.", 15);
                break;
            case Tool.Carbon:
                Number(context, "Initial Carbon", depositCarbon, 0, 1000000).ValueChanged += value => depositCarbon = (int)value;
                break;
            case Tool.Camera:
                Number(context, "Opening yaw", yaw, -180, 360).ValueChanged += value => yaw = (float)value;
                var zoom = Number(context, "Opening zoom", map.OpeningCameraSize, 30, 1800);
                zoom.ValueChanged += value => SetValue(map.OpeningCameraSize, (float)value, size => map.OpeningCameraSize = size);
                break;
        }
    }

    private void SwitchTool(Tool next)
    {
        toolPicker.Select(toolPicker.GetItemIndex((int)next));
        UpdateTool();
    }

    private void EditSelected(Node3D node)
    {
        switch (node)
        {
            case CommandUnitPlacement group:
                UnitPicker(context, Array.IndexOf(units, units.FirstOrDefault(unit => unit.Id == group.Unit?.Id)),
                    index => SetValue(group.Unit, units[index], value => group.Unit = value));
                Number(context, "Formation count", group.Count, 1, 256).ValueChanged += value =>
                    SetValue(group.Count, (int)value, count => group.Count = count);
                Number(context, "Facing", group.RotationDegrees.Y, -180, 360).ValueChanged += value =>
                    SetValue(group.RotationDegrees, new Vector3(0, (float)value, 0), rotation => group.RotationDegrees = rotation);
                break;
            case TerrainHill hill:
                Number(context, "Radius", hill.Radius, 8, 256).ValueChanged += value =>
                    SetValue(hill.Radius, (float)value, radius => hill.Radius = radius, true);
                Number(context, "Rise", hill.Rise, 0, 80).ValueChanged += value =>
                    SetValue(hill.Rise, (float)value, rise => hill.Rise = rise, true);
                break;
            case CarbonDeposit deposit:
                Number(context, "Initial Carbon", deposit.InitialCarbon, 0, 1000000).ValueChanged += value =>
                    SetValue(deposit.InitialCarbon, (int)value, amount => deposit.InitialCarbon = amount);
                break;
            case MeshInstance3D wall when wall.Mesh is BoxMesh box && wall.GetScript().AsGodotObject() != null:
                var dimensions = box.Size;
                void Resize(int axis, float size)
                {
                    Vector3 old = ((BoxMesh)wall.Mesh).Size;
                    Vector3 next = old;
                    next[axis] = size;
                    SetValue(old, next, dimension =>
                    {
                        wall.Set("wall_size", dimension);
                        PlaceAt(wall, wall.GlobalPosition);
                    });
                }
                Number(context, "Width", dimensions.X, 1, 500).ValueChanged += value => Resize(0, (float)value);
                Number(context, "Height", dimensions.Y, 0.5, 80, 0.5).ValueChanged += value => Resize(1, (float)value);
                Number(context, "Depth", dimensions.Z, 1, 500).ValueChanged += value => Resize(2, (float)value);
                break;
        }
    }

    // =========================================================
    public override void _UnhandledInput(InputEvent input)
    {
        if (map == null || !map.Visible || runningTest != null) return;
        if (input is InputEventKey { Pressed: true, Echo: false, CtrlPressed: true, Keycode: Key.Z } key)
        {
            if (key.ShiftPressed) ApplyHistory(redo, undo, false);
            else ApplyHistory(undo, redo, true);
            GetViewport().SetInputAsHandled();
        }
        if (previewing) return;
        if (input is InputEventMouseMotion motion)
        {
            if (WorldPoint(motion.Position, out var point))
            {
                ShowCursor(point);
                if (beforePaint != null) PaintTo(Cell(point), (motion.ButtonMask & MouseButtonMask.Right) != 0);
            }
            else { cursor.Hide(); previousCell = null; }
        }
        if (input is not InputEventMouseButton mouse || mouse.ButtonIndex is not (MouseButton.Left or MouseButton.Right)) return;
        if (!mouse.Pressed)
        {
            EndPaint();
            return;
        }
        if (!WorldPoint(mouse.Position, out var hit)) return;
        if (tool is Tool.Rock or Tool.Water or Tool.Deployment ||
            (tool == Tool.Erase && (mouse.ButtonIndex == MouseButton.Right || Pick(hit) == null)))
        {
            BeginPaint();
            PaintTo(Cell(hit), mouse.ButtonIndex == MouseButton.Right);
        }
        else if (mouse.ButtonIndex == MouseButton.Left)
        {
            try { Place(hit); }
            catch (Exception error) { SetStatus(error.Message); }
        }
        else return;
        GetViewport().SetInputAsHandled();
    }

    public override void _Process(double delta)
    {
        if (beforePaint != null && !Input.IsMouseButtonPressed(MouseButton.Left) && !Input.IsMouseButtonPressed(MouseButton.Right))
            EndPaint();
    }

    private bool WorldPoint(Vector2 screen, out Vector3 point)
    {
        var origin = camera.ProjectRayOrigin(screen);
        var direction = camera.ProjectRayNormal(screen);
        float height = 0;
        point = Vector3.Zero;
        for (int step = 0; step < 6; step++)
        {
            point = origin + direction * ((height - origin.Y) / direction.Y);
            height = Surface(point);
        }
        point.Y = height;
        return Mathf.Abs(point.X) < map.MapSize * 0.5f && Mathf.Abs(point.Z) < map.MapSize * 0.5f;
    }

    private float Surface(Vector3 point) =>
        map.GetNodeOrNull<CommandTerrain>("Terrain")?.SampleHeight(point.X, point.Z) ?? 0f;

    private static Vector2I Cell(Vector3 point) => new(Mathf.FloorToInt(point.X / Grid), Mathf.FloorToInt(point.Z / Grid));

    private Vector3 Snap(Vector3 point)
    {
        if (!snap.ButtonPressed) return point;
        var cell = Cell(point);
        return new Vector3((cell.X + 0.5f) * Grid, point.Y, (cell.Y + 0.5f) * Grid);
    }

    private void ShowCursor(Vector3 point)
    {
        bool brush = tool is Tool.Rock or Tool.Water or Tool.Deployment or Tool.Erase;
        var position = brush ? new Vector3((Cell(point).X + 0.5f) * Grid, 0, (Cell(point).Y + 0.5f) * Grid) : Snap(point);
        position.Y = Surface(position) + 0.13f;
        cursor.GlobalPosition = position;
        cursor.Scale = tool switch
        {
            Tool.Wall => new(wallWidth, 1, wallDepth),
            Tool.Citadel => new(10, 1, 10),
            Tool.Carbon or Tool.Fabricator => new(6, 1, 6),
            _ when brush => new(Grid * (brushRadius * 2 - 1), 1, Grid * (brushRadius * 2 - 1)),
            _ => Vector3.One * 1.5f
        };
        cursor.Show();
    }

    // =========================================================
    private void BeginPaint()
    {
        EndPaint();
        beforePaint = (paint.Data.RockCells, paint.Data.WaterCells, map.PresetRules?.BlueDeploymentCells);
        rocks = new(paint.Data.RockCells);
        water = new(paint.Data.WaterCells);
        deployment = map.PresetRules == null ? new() : new(map.PresetRules.BlueDeploymentCells);
        paintChanged = false;
    }

    private void PaintTo(Vector2I cell, bool erase)
    {
        if (previousCell == cell) return;
        var from = previousCell ?? cell;
        int steps = Math.Max(Math.Abs(cell.X - from.X), Math.Abs(cell.Y - from.Y));
        for (int i = 0; i <= steps; i++)
        {
            float t = steps == 0 ? 0 : i / (float)steps;
            var center = new Vector2I(Mathf.RoundToInt(Mathf.Lerp(from.X, cell.X, t)), Mathf.RoundToInt(Mathf.Lerp(from.Y, cell.Y, t)));
            for (int z = 1 - brushRadius; z < brushRadius; z++)
            for (int x = 1 - brushRadius; x < brushRadius; x++)
            {
                var next = center + new Vector2I(x, z);
                if (x * x + z * z >= brushRadius * brushRadius || !paint.Contains(next)) continue;
                if (tool == Tool.Erase || (erase && tool == Tool.Rock)) paintChanged |= rocks.Remove(next);
                if (tool == Tool.Erase || (erase && tool == Tool.Water)) paintChanged |= water.Remove(next);
                if (tool == Tool.Erase || (erase && tool == Tool.Deployment)) paintChanged |= deployment.Remove(next);
                if (erase || tool == Tool.Erase) continue;
                if (tool == Tool.Rock) { paintChanged |= water.Remove(next); paintChanged |= rocks.Add(next); }
                if (tool == Tool.Water) { paintChanged |= rocks.Remove(next); paintChanged |= water.Add(next); }
                if (tool == Tool.Deployment) paintChanged |= deployment.Add(next);
            }
        }
        previousCell = cell;
        if (!paintChanged) return;
        if (!paint.TryMerge(new(rocks), new(water), out _, out _) || deployment.Count > WorldPaint.MaxPaintedCells)
        {
            var previous = beforePaint.Value;
            beforePaint = null;
            previousCell = null;
            SetPaint(previous);
            SetStatus("Stroke exceeded the map paint budget and was reverted.");
            return;
        }
        SetPaint((Sorted(rocks), Sorted(water), Sorted(deployment)));
    }

    private static Cells Sorted(HashSet<Vector2I> cells) => new(cells.OrderBy(cell => cell.Y).ThenBy(cell => cell.X));

    private void EndPaint()
    {
        if (beforePaint is not { } previous) return;
        beforePaint = null;
        previousCell = null;
        if (!paintChanged) return;
        (Cells, Cells, Cells) next = (paint.Data.RockCells, paint.Data.WaterCells, map.PresetRules?.BlueDeploymentCells);
        Remember(() => SetPaint(previous), () => SetPaint(next));
    }

    private void SetPaint((Cells Rocks, Cells Water, Cells Blue) cells)
    {
        paint.Data.RockCells = cells.Rocks;
        paint.Data.WaterCells = cells.Water;
        if (map.PresetRules != null) map.PresetRules.BlueDeploymentCells = cells.Blue;
        RefreshMap();
    }

    // =========================================================
    private IEnumerable<Node3D> Objects()
    {
        foreach (string path in new[] { "Obstacles", "Terrain", "StartingUnits", "Resources", "StartingFabricators" })
            if (map.GetNodeOrNull<Node3D>(path) is Node3D container)
                foreach (var child in container.GetChildren())
                    if (child is Node3D node) yield return node;
        foreach (string path in new[] { "Citadel", "CameraStart", "HostileFabricator" })
            if (map.GetNodeOrNull<Node3D>(path) is Node3D node) yield return node;
    }

    private Node3D Pick(Vector3 point)
    {
        Node3D nearest = null;
        float best = float.PositiveInfinity;
        foreach (var node in Objects())
        {
            var offset = point - node.GlobalPosition;
            float distance = new Vector2(offset.X, offset.Z).Length();
            bool hit = node switch
            {
                MeshInstance3D mesh when mesh.Mesh is BoxMesh => (mesh.GlobalTransform * mesh.GetAabb()).HasPoint(
                    new Vector3(point.X, mesh.GlobalPosition.Y, point.Z)),
                TerrainHill hill => distance <= Mathf.Min(8, hill.Radius),
                _ => distance <= (node.Name == "Citadel" ? 7 : 4)
            };
            if (hit && distance < best) { best = distance; nearest = node; }
        }
        return nearest;
    }

    private void Place(Vector3 hit)
    {
        Vector3 point = Snap(hit);
        switch (tool)
        {
            case Tool.Select:
                selected = Pick(hit);
                UpdateTool();
                return;
            case Tool.Move:
                if (selected == null) { selected = Pick(hit); UpdateTool(); }
                else Move(selected, point);
                return;
            case Tool.Erase:
                RemoveObject(Pick(hit));
                return;
            case Tool.Citadel:
                Move(map.GetNode<Node3D>("Citadel"), point);
                return;
            case Tool.Camera:
                var marker = map.GetNode<Marker3D>("CameraStart");
                var oldTransform = marker.Transform;
                var next = new Transform3D(new Basis(Vector3.Up, Mathf.DegToRad(yaw)), new Vector3(point.X, 0, point.Z));
                SetValue(oldTransform, next, value => marker.Transform = value);
                return;
        }
        Node3D node;
        string parent;
        switch (tool)
        {
            case Tool.Hill:
                var hill = LoadNode<TerrainHill>("res://WORLD/TERRAIN/AUTHORING/TerrainHill.tscn");
                hill.Radius = hillRadius;
                hill.Rise = hillRise;
                node = hill; parent = "Terrain";
                break;
            case Tool.Wall:
                node = LoadNode<MeshInstance3D>("res://WORLD/OBSTACLES/CommandWall.tscn");
                node.Set("wall_size", new Vector3(wallWidth, wallHeight, wallDepth));
                parent = "Obstacles";
                break;
            case Tool.Soldiers:
                var formation = LoadNode<CommandUnitPlacement>("res://GAMEPLAY/DEPLOYMENT/COMMAND/CommandUnitPlacement.tscn");
                formation.Unit = units[unitIndex];
                formation.Faction = mode.Preset || red ? CommandUnitPlacement.Team.Red : CommandUnitPlacement.Team.Blue;
                formation.Count = groupSize;
                formation.RotationDegrees = new Vector3(0, yaw, 0);
                node = formation; parent = "StartingUnits";
                break;
            case Tool.Fabricator:
                if (!map.HasNode("StartingFabricators"))
                {
                    var starting = new Node3D { Name = "StartingFabricators" };
                    map.AddChild(starting);
                    starting.Owner = map;
                }
                node = new Marker3D { Name = "Fabricator" };
                parent = "StartingFabricators";
                break;
            case Tool.Carbon:
                var deposit = LoadNode<CarbonDeposit>("res://WORLD/RESOURCES/CARBON/CarbonDeposit.tscn");
                deposit.InitialCarbon = depositCarbon;
                node = deposit; parent = "Resources";
                break;
            default: return;
        }
        var container = map.GetNode<Node3D>(parent);
        void Add()
        {
            container.AddChild(node, true);
            node.Owner = map;
            PlaceAt(node, point);
            RefreshMap(node is TerrainHill);
        }
        void Remove()
        {
            container.RemoveChild(node);
            if (selected == node) selected = null;
            RefreshMap(node is TerrainHill);
        }
        detached.Add(node);
        Add();
        Remember(Remove, Add);
    }

    private void Move(Node3D node, Vector3 point)
    {
        Vector3 old = node.GlobalPosition;
        void Apply(Vector3 at)
        {
            PlaceAt(node, at);
            RefreshMap(node is TerrainHill);
        }
        Apply(point);
        Remember(() => { node.GlobalPosition = old; RefreshMap(node is TerrainHill); }, () => Apply(point));
    }

    private void PlaceAt(Node3D node, Vector3 point)
    {
        point.Y = node is TerrainHill || node.Name == "CameraStart" ? 0 : Surface(point);
        if (node is MeshInstance3D mesh && mesh.Mesh is BoxMesh box) point.Y += box.Size.Y * 0.5f;
        node.GlobalPosition = point;
    }

    private void RemoveObject(Node3D node)
    {
        if (node == null) return;
        if (node.Name == "Citadel" || node.Name == "CameraStart")
        {
            SetStatus("Move this required marker with the Move tool.");
            return;
        }
        var parent = node.GetParent();
        int index = node.GetIndex();
        void Remove() { parent.RemoveChild(node); selected = null; RefreshMap(node is TerrainHill); }
        void Restore()
        {
            parent.AddChild(node, true);
            parent.MoveChild(node, index);
            node.Owner = map;
            RefreshMap(node is TerrainHill);
        }
        detached.Add(node);
        Remove();
        Remember(Restore, Remove);
        UpdateTool();
    }

    private static T LoadNode<T>(string path) where T : Node3D => GD.Load<PackedScene>(path).Instantiate<T>();

    private void SetValue<T>(T old, T value, Action<T> setter, bool terrainChanged = false)
    {
        if (EqualityComparer<T>.Default.Equals(old, value)) return;
        void Apply(T item) { setter(item); RefreshMap(terrainChanged); }
        Apply(value);
        Remember(() => Apply(old), () => Apply(value));
    }

    private void Remember(Action back, Action forward)
    {
        undo.Push((back, forward));
        redo.Clear();
        dirty = true;
        SetStatus();
    }

    private void ApplyHistory(Stack<(Action Undo, Action Redo)> from, Stack<(Action Undo, Action Redo)> to, bool back)
    {
        if (runningTest != null || map == null) return;
        EndPaint();
        if (!from.TryPop(out var edit)) return;
        if (back) edit.Undo(); else edit.Redo();
        to.Push(edit);
        UpdateTool();
        dirty = true;
        SetStatus();
    }
}
