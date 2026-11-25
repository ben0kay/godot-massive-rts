// Edits the shared battlefield scene with grid brushes and reversible object operations.
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Cells = Godot.Collections.Array<Godot.Vector2I>;

namespace MassiveRts;

public partial class LevelEditor
{
    private enum Tool { Select, Move, Erase, Rock, Water, Forest, Elevation, Wall, Soldiers, Deployment, Citadel, Fabricator, Carbon, Camera, Defense, DeploymentLine, Bridge, ReserveEntry }
    private const float Grid = DeploymentLayout.CellSize;
    private Tool tool;
    private Node3D selected, layout;
    private LevelLayoutPreview.PlacementBatches placementBatches;
    private MeshInstance3D cursor;
    private WorldPaint paint;
    private int brushRadius = 2;
    private Vector2I? previousCell;
    private (Cells Rocks, Cells Water, Cells Forest, Cells Blue)? beforePaint;
    private HashSet<Vector2I> rocks, water, forest, deployment;
    private const double PaintPreviewInterval = 0.125;
    private bool paintChanged, paintPreviewPending;
    private double paintPreviewCooldown;
    private float elevationRadius = 16f, elevationStrength = 2f, elevationTarget;
    private bool setElevationHeight;
    private float yaw;
    private int unitIndex, groupSize = 1, depositCarbon = 5000;
    private bool red = true;

    // =========================================================
    private void SetTools()
    {
        SetPlacementPaletteMode();
        toolPicker.Clear();
        foreach (Tool entry in Enum.GetValues<Tool>())
        {
            if (entry is Tool.Soldiers or Tool.Defense or Tool.ReserveEntry) continue;
            if (mode.IsAutoBattler && entry is Tool.Citadel or Tool.Fabricator or Tool.Carbon) continue;
            if (!mode.IsAutoBattler && entry is Tool.Deployment or Tool.DeploymentLine) continue;
            toolPicker.AddItem(entry switch
            {
                Tool.Bridge => "Draw bridge",
                Tool.DeploymentLine => "Set deployment line",
                Tool.Rock => "Paint rocks", Tool.Water => "Paint water", Tool.Forest => "Paint forest", Tool.Elevation => "Paint elevation",
                Tool.Wall => "Industrial Walls",
                Tool.Deployment => "Paint Blue deployment zone", Tool.Move => "Move selection",
                Tool.Camera => "Place opening camera", Tool.Carbon => "Place Carbon deposit",
                Tool.Fabricator => "Place starting Fabricator", Tool.Citadel => "Move Citadel",
                _ => entry.ToString()
            }, (int)entry);
        }
        SwitchTool(Tool.Select);
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
        camera.ArrowsAlwaysPan = true;
        camera.MaximumZoomSize = map.MapSize * 1.6f;
        camera.SetOpeningView(editingReserve ? ReserveOrigin : Vector3.Zero,
            editingReserve ? RedReserveFormation.HalfWidth * 2.1f : map.MapSize * 0.95f, camera.TopDown ? 0 : 45);
    }

    private void UpdateEditorGround() => map?.UpdateGroundBounds(showGrid.ButtonPressed && !previewing);

    private void RefreshMap(bool terrainChanged = false)
    {
        if (terrainChanged) map.GetNodeOrNull<CommandTerrain>("Terrain")?.Rebuild(force: true);
        UpdateEditorGround();
        map.ReadAuthoredWalls(updateVisuals: true);
        paint.Compile();
        RefreshLayout();
    }

    private void RefreshPlacement(Node3D node)
    {
        if ((node is CommandUnitPlacement or CommandLaserPlacement) && !previewReserveEntry)
        {
            RefreshPlacementBatches();
            RefreshStrength();
        }
        else RefreshMap(node is TerrainHill);
    }

    private void RefreshPlacementBatches()
    {
        if (editingReserve) ReserveContainer().Position = ReserveOrigin;
        if (placementBatches == null)
        {
            var root = new Node3D { Name = "PlacementBatches" };
            map.AddChild(root, false, InternalMode.Back);
            placementBatches = new LevelLayoutPreview.PlacementBatches(root);
        }
        placementBatches.Refresh(map, redOnly: false, formationContainer: UnitContainerName);
    }

    private void RefreshLayout()
    {
        if (layout != null)
        {
            map.RemoveChild(layout);
            layout.QueueFree();
        }
        RefreshPlacementBatches();
        layout = LevelLayoutPreview.Build(map, formationContainer: UnitContainerName, decorationsOnly: true);
        if (editingReserve) DrawReserveGrid(layout);
        else if (previewReserveEntry) DrawReserveEntry(layout);
        RefreshStrength();
        if (editingReserve) return;
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
        rectangleStart = null;
        foreach (var section in new[] { mapToolsContext, unitContext, defenseContext })
            foreach (var child in section.GetChildren())
            {
                section.RemoveChild(child);
                child.QueueFree();
            }
        context = tool switch
        {
            Tool.Soldiers => unitContext,
            Tool.Defense => defenseContext,
            _ => mapToolsContext
        };
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
            case Tool.Bridge:
                Heading(context, "Drag a rectangle to place the bridge. Right-click cancels.", 15);
                break;
            case Tool.Rock:
            case Tool.Water:
            case Tool.Forest:
            case Tool.Deployment:
            case Tool.Erase:
                Number(context, "Brush radius (cells)", brushRadius, 1, 14).ValueChanged += value => brushRadius = (int)value;
                Heading(context, tool == Tool.Erase ? "Click object to remove it.\nDrag over paint to erase it." : "Drag to paint. Right-drag to erase.", 15);
                break;
            case Tool.Elevation:
                var elevationMode = new OptionButton();
                elevationMode.AddItem("Raise / Lower");
                elevationMode.AddItem("Set Height");
                elevationMode.Select(setElevationHeight ? 1 : 0);
                context.AddChild(elevationMode);
                elevationMode.ItemSelected += choice =>
                {
                    EndElevation();
                    setElevationHeight = choice == 1;
                    UpdateTool();
                };
                Number(context, "Radius", elevationRadius, 4, 128).ValueChanged += value => elevationRadius = (float)value;
                if (setElevationHeight)
                {
                    Number(context, "Target height (0 = map baseline)", elevationTarget, -80, 80, 0.1)
                        .ValueChanged += value => elevationTarget = (float)value;
                    Heading(context, "Left-click to level the brush area with a smooth slope around it.", 15);
                }
                else
                {
                    Number(context, "Strength", elevationStrength, 0.1, 20, 0.1).ValueChanged += value => elevationStrength = (float)value;
                    Heading(context, "Left-click or drag to raise. Right-click or drag to lower.", 15);
                }
                break;
            case Tool.ReserveEntry:
                Heading(context, "Click near a battlefield edge to set the Red entry.", 15);
                break;
            case Tool.Wall:
                Choice(context, "Paint mode", new[] { "1×1 Brush", "Line", "Rectangle Fill" },
                    (int)wallPaintMode, index => { CancelWallStroke(); wallPaintMode = (WallPaintMode)index; });
                Number(context, "Wall height", paint.Data.IndustrialWallHeight, 0.5, 80, 0.5).ValueChanged += value =>
                    SetWallValue(paint.Data.IndustrialWallHeight, (float)value, height => paint.Data.IndustrialWallHeight = height);
                Heading(context, "Left-drag to paint. Right-drag to erase.", 15);
                break;
            case Tool.DeploymentLine:
                DeploymentLineTools();
                break;
            case Tool.Defense:
                Heading(context, "Click the map to place this defense.", 15);
                if (!mode.IsAutoBattler) Choice(context, "Faction", new[] { "Blue", "Red" }, redTower ? 1 : 0, index => redTower = index == 1);
                break;
            case Tool.Soldiers:
                Choice(context, "Placement", new[] { "Formation", "Rectangle fill" }, fillRectangle ? 1 : 0,
                    index => { fillRectangle = index == 1; UpdateTool(); });
                if (!mode.IsAutoBattler)
                    UnitPicker(context, unitIndex, index => unitIndex = index);
                if (!mode.IsAutoBattler)
                    Choice(context, "Faction", new[] { "Blue", "Red" }, red ? 1 : 0, index => red = index == 1);
                if (fillRectangle)
                    Number(context, "Density scale (lower is denser)", densityScale, 0, 3, 0.05).ValueChanged += value => densityScale = (float)value;
                else Number(context, "Soldiers per formation", groupSize, 1, 256).ValueChanged += value => groupSize = (int)value;
                Number(context, "Facing (degrees)", yaw, -180, 360).ValueChanged += value => yaw = (float)value;
                Heading(context, fillRectangle ? "Drag to fill. Right-drag to erase." : "Click to place a formation.", 15);
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

    private BoxMesh squareCursor;
    private CylinderMesh elevationCursor;

    private void SwitchTool(Tool next)
    {
        EndPaint();
        CancelBridge();
        CancelWallStroke();
        if (cursor?.Mesh is BoxMesh box && squareCursor != box)
        {
            squareCursor = box;
            elevationCursor = new CylinderMesh
            {
                TopRadius = 0.5f, BottomRadius = 0.5f, Height = 0.035f,
                RadialSegments = 48, Material = box.Material
            };
        }
        if (cursor != null)
        {
            cursor.Mesh = next == Tool.Elevation ? elevationCursor : squareCursor;
            cursor.Hide();
        }
        tool = next;
        toolPicker.Select(toolPicker.GetItemIndex((int)next));
        if (next != Tool.Defense && !mode.IsAutoBattler) defensePicker.Select(0);
        if (mode.IsAutoBattler) SyncPaletteTool(next);
        UpdateTool();
    }

    private void EditSelected(Node3D node)
    {
        switch (node)
        {
            case CommandUnitPlacement group:
                UnitPicker(context, Array.IndexOf(units, units.FirstOrDefault(unit => unit.Id == group.Unit?.Id)),
                    index => SetValue(group.Unit, units[index], value => group.Unit = value));
                if (group.PaintedOffsets.Length == 0) Number(context, "Formation count", group.Count, 1, 256).ValueChanged += value =>
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
        if (editingReserve && input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } reserveClick &&
            (tool is Tool.Select or Tool.Move or Tool.Erase) && WorldPoint(reserveClick.Position, out var reservePoint))
        {
            var group = ReserveContainer().GetChildren().OfType<CommandUnitPlacement>().FirstOrDefault(item =>
                item.Unit != null && item.Positions(UnitOutline.FromDefinition(item.Unit, UnitOutline.EffectiveSize(item.Unit)).Footprint)
                    .Any(point => point.DistanceSquaredTo(reservePoint) <= 2f));
            if (tool == Tool.Erase && group != null) RemoveObject(group);
            else if (tool == Tool.Move && selected is CommandUnitPlacement formation)
            {
                var destination = Snap(reservePoint);
                if (FitsStagingFormation(formation, destination))
                    SetValue(formation.Position, destination - ReserveOrigin, at => formation.Position = at);
                else SetStatus("Keep the full formation within the staging grid.");
            }
            else { selected = group; UpdateTool(); }
            GetViewport().SetInputAsHandled();
            return;
        }
        if (editingReserve && tool is not Tool.Soldiers) return;
        if (tool == Tool.Wall && HandleWallPaint(input)) return;
        if (tool == Tool.Bridge && HandleBridge(input)) return;
        if (tool == Tool.Elevation && HandleElevation(input)) return;
        if (tool == Tool.Soldiers && fillRectangle && HandleSoldierRectangle(input)) return;
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
        if (tool == Tool.Erase && mouse.ButtonIndex == MouseButton.Left && EraseBridge(Cell(hit)))
        {
            GetViewport().SetInputAsHandled();
            return;
        }
        if (tool is Tool.Rock or Tool.Water or Tool.Forest or Tool.Deployment ||
            (tool == Tool.Erase && (mouse.ButtonIndex == MouseButton.Right || Pick(hit) == null)))
        {
            BeginPaint();
            PaintTo(Cell(hit), mouse.ButtonIndex == MouseButton.Right);
        }
        else if (mouse.ButtonIndex == MouseButton.Left)
        {
            try { Place(hit); }
            catch (Exception error)
            {
                GD.PushError($"Map placement failed: {error}");
                SetStatus(error.Message);
            }
        }
        else return;
        GetViewport().SetInputAsHandled();
    }

    public override void _Process(double delta)
    {
        UpdateElevation(delta);
        if (bridgeStart.HasValue && !Input.IsMouseButtonPressed(MouseButton.Left)) CancelBridge();
        if (wallStrokeStart.HasValue && !Input.IsMouseButtonPressed(wallStrokeButton)) CancelWallStroke();
        if (paintPreviewCooldown > 0) paintPreviewCooldown -= delta;
        if (paintPreviewPending && paintPreviewCooldown <= 0) PublishPaintPreview();
        if (rectangleStart != null && !Input.IsMouseButtonPressed(MouseButton.Left) && !Input.IsMouseButtonPressed(MouseButton.Right))
            rectangleStart = null;
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
        Vector3 local = point - PaintOrigin;
        return Mathf.Abs(local.X) < PaintHalfWidth && Mathf.Abs(local.Z) < PaintHalfWidth;
    }

    private float Surface(Vector3 point) =>
        editingReserve ? 0f : map.GetNodeOrNull<CommandTerrain>("Terrain")?.SampleHeight(point.X, point.Z) ?? 0f;

    private static Vector2I Cell(Vector3 point) => new(Mathf.FloorToInt(point.X / Grid), Mathf.FloorToInt(point.Z / Grid));

    private Vector3 Snap(Vector3 point)
    {
        if (!snap.ButtonPressed) return point;
        var cell = Cell(point);
        return new Vector3((cell.X + 0.5f) * Grid, point.Y, (cell.Y + 0.5f) * Grid);
    }

    private void ShowCursor(Vector3 point)
    {
        bool brush = tool is Tool.Wall or Tool.Rock or Tool.Water or Tool.Forest or Tool.Deployment or Tool.Erase;
        var position = brush ? new Vector3((Cell(point).X + 0.5f) * Grid, 0, (Cell(point).Y + 0.5f) * Grid) : Snap(point);
        position.Y = Surface(position) + 0.13f;
        cursor.GlobalPosition = position;
        cursor.Scale = tool switch
        {
            Tool.Wall => new(Grid, 1, Grid),
            Tool.Citadel => new(10, 1, 10),
            Tool.Defense => TowerCursor(),
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
        beforePaint = (paint.Data.RockCells, paint.Data.WaterCells, paint.Data.ForestCells, map.AutoBattlerRules?.BlueDeploymentCells);
        rocks = new(paint.Data.RockCells);
        water = new(paint.Data.WaterCells);
        forest = new(paint.Data.ForestCells);
        deployment = map.AutoBattlerRules == null ? new() : new(map.AutoBattlerRules.BlueDeploymentCells);
        paintChanged = paintPreviewPending = false;
        paintPreviewCooldown = 0;
    }

    private void PaintTo(Vector2I cell, bool erase)
    {
        if (previousCell == cell) return;
        var from = previousCell ?? cell;
        int steps = Math.Max(Math.Abs(cell.X - from.X), Math.Abs(cell.Y - from.Y));
        bool changed = false;
        for (int i = 0; i <= steps; i++)
        {
            float t = steps == 0 ? 0 : i / (float)steps;
            var center = new Vector2I(Mathf.RoundToInt(Mathf.Lerp(from.X, cell.X, t)), Mathf.RoundToInt(Mathf.Lerp(from.Y, cell.Y, t)));
            for (int z = 1 - brushRadius; z < brushRadius; z++)
            for (int x = 1 - brushRadius; x < brushRadius; x++)
            {
                var next = center + new Vector2I(x, z);
                if (x * x + z * z >= brushRadius * brushRadius || !paint.Contains(next)) continue;
                if (tool == Tool.Erase || (erase && tool == Tool.Rock)) changed |= rocks.Remove(next);
                if (tool == Tool.Erase || (erase && tool == Tool.Water)) changed |= water.Remove(next);
                if (tool == Tool.Erase || (erase && tool == Tool.Forest)) changed |= forest.Remove(next);
                if (tool == Tool.Erase || (erase && tool == Tool.Deployment)) changed |= deployment.Remove(next);
                if (erase || tool == Tool.Erase) continue;
                if (tool == Tool.Rock) { changed |= forest.Remove(next); changed |= water.Remove(next); changed |= rocks.Add(next); }
                if (tool == Tool.Water) { changed |= forest.Remove(next); changed |= rocks.Remove(next); changed |= water.Add(next); }
                if (tool == Tool.Forest) { changed |= rocks.Remove(next); changed |= water.Remove(next); changed |= forest.Add(next); }
                if (tool == Tool.Deployment && map.AutoBattlerRules.AllowsSide(
                    new Vector3((next.X + 0.5f) * Grid, 0, (next.Y + 0.5f) * Grid), Grid * 0.5f, blue: true))
                    changed |= deployment.Add(next);
            }
        }
        previousCell = cell;
        if (!changed) return;
        paintPreviewPending = true;
        if (paintPreviewCooldown <= 0) PublishPaintPreview();
    }

    // Mouse motion can arrive much faster than the renderer can replace forest batches.
    private void PublishPaintPreview()
    {
        if (!paintPreviewPending || beforePaint is not { } previous) return;
        paintPreviewPending = false;
        paintPreviewCooldown = PaintPreviewInterval;
        if (!paint.TryMerge(new(rocks), new(water), out _, out _) || forest.Count > WorldPaint.MaxPaintedCells || deployment.Count > WorldPaint.MaxPaintedCells)
        {
            beforePaint = null;
            previousCell = null;
            SetPaint(previous);
            SetStatus("Stroke exceeded the map paint budget and was reverted.");
            return;
        }
        paintChanged = true;
        SetPaint((Sorted(rocks), Sorted(water), Sorted(forest), Sorted(deployment)),
            refreshLayout: tool is Tool.Deployment or Tool.Erase);
    }

    private static Cells Sorted(HashSet<Vector2I> cells) => new(cells.OrderBy(cell => cell.Y).ThenBy(cell => cell.X));

    private void EndPaint()
    {
        EndElevation();
        PublishPaintPreview();
        if (beforePaint is not { } previous) return;
        beforePaint = null;
        previousCell = null;
        if (!paintChanged) return;
        (Cells, Cells, Cells, Cells) next = (paint.Data.RockCells, paint.Data.WaterCells, paint.Data.ForestCells, map.AutoBattlerRules?.BlueDeploymentCells);
        bool refreshLayout = tool is Tool.Deployment or Tool.Erase;
        Remember(() => SetPaint(previous, refreshLayout), () => SetPaint(next, refreshLayout));
    }

    private void SetPaint((Cells Rocks, Cells Water, Cells Forest, Cells Blue) cells, bool refreshLayout = true)
    {
        paint.Data.RockCells = cells.Rocks;
        paint.Data.WaterCells = cells.Water;
        paint.Data.ForestCells = cells.Forest;
        if (map.AutoBattlerRules != null) map.AutoBattlerRules.BlueDeploymentCells = cells.Blue;
        if (refreshLayout || previewReserveEntry) RefreshMap();
        else paint.Compile();
    }

    // =========================================================
    private IEnumerable<Node3D> Objects()
    {
        foreach (string path in new[] { "Obstacles", "Terrain", "StartingUnits", "Resources", "StartingFabricators", "StartingDefenses" })
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
                CommandUnitPlacement group when group.PaintedOffsets.Length > 0 => distance <= 4 ||
                    group.Positions(0).Any(position => new Vector2(position.X - point.X, position.Z - point.Z).LengthSquared() <= 4),
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
            case Tool.DeploymentLine:
                SetLine(map.AutoBattlerRules.LineAxis == AutoBattlerRules.DeploymentAxis.Disabled
                    ? AutoBattlerRules.DeploymentAxis.Z : map.AutoBattlerRules.LineAxis, point);
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
            case Tool.ReserveEntry:
                SetReserveEntry(point);
                return;
            case Tool.Soldiers:
                var formation = LoadNode<CommandUnitPlacement>("res://GAMEPLAY/DEPLOYMENT/COMMAND/CommandUnitPlacement.tscn");
                formation.Unit = units[unitIndex];
                formation.Faction = mode.IsAutoBattler || red ? CommandUnitPlacement.Team.Red : CommandUnitPlacement.Team.Blue;
                formation.Count = groupSize;
                formation.RotationDegrees = new Vector3(0, yaw, 0);
                node = formation; parent = UnitContainerName;
                break;
            case Tool.Defense:
                node = new CommandLaserPlacement { Name = "Defense", Definition = SelectedDefense, Red = mode.IsAutoBattler || redTower };
                parent = "StartingDefenses";
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
        if (!TryAddPlacement(node, parent, point)) node.Free();
    }

    private bool TryAddPlacement(Node3D node, string parent, Vector3 point)
    {
        if (node is CommandLaserPlacement tower)
        {
            point = tower.SnapCenter(point);
            if (!TowerSiteClear(tower, point)) return false;
            if (Objects().Count(item => item is CommandLaserPlacement) >= CommandSandbox.MaxStartingTowers)
            {
                SetStatus($"A level supports up to {CommandSandbox.MaxStartingTowers} starting defense towers.");
                return false;
            }
        }
        if (parent != RedReserveFormation.ContainerName && !FitsPresetSide(node, point))
        {
            SetStatus("Place the entire enemy formation or defense on the Red side of the deployment line.");
            return false;
        }
        if (parent == RedReserveFormation.ContainerName && node is CommandUnitPlacement reserve &&
            reserve.Unit != null && !FitsStagingFormation(reserve, point))
        {
            SetStatus("Keep the full formation within the staging grid.");
            return false;
        }
        if (map.GetNodeOrNull<Node3D>(parent) is not Node3D container)
        {
            container = new Node3D { Name = parent };
            map.AddChild(container);
            container.Owner = map;
        }
        Vector3 stagingPosition = parent == RedReserveFormation.ContainerName ? point - ReserveOrigin : Vector3.Zero;
        void Add()
        {
            container.AddChild(node, true);
            node.Owner = map;
            if (parent == RedReserveFormation.ContainerName) node.Position = stagingPosition;
            else PlaceAt(node, point);
            RefreshPlacement(node);
        }
        void Remove()
        {
            container.RemoveChild(node);
            if (selected == node) selected = null;
            RefreshPlacement(node);
        }
        detached.Add(node);
        Add();
        Remember(Remove, Add);
        return true;
    }

    private void Move(Node3D node, Vector3 point)
    {
        if (node is CommandLaserPlacement tower)
        {
            point = tower.SnapCenter(point);
            if (!TowerSiteClear(tower, point)) return;
        }
        if (!FitsPresetSide(node, point))
        {
            SetStatus("Move the entire enemy formation or defense onto the Red side.");
            return;
        }
        Vector3 old = node.GlobalPosition;
        void Apply(Vector3 at)
        {
            PlaceAt(node, at);
            RefreshPlacement(node);
        }
        Apply(point);
        Remember(() => { node.GlobalPosition = old; RefreshPlacement(node); }, () => Apply(point));
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
        void Remove() { parent.RemoveChild(node); selected = null; RefreshPlacement(node); }
        void Restore()
        {
            parent.AddChild(node, true);
            parent.MoveChild(node, index);
            node.Owner = map;
            RefreshPlacement(node);
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
        CancelBridge();
        CancelWallStroke();
        if (!from.TryPop(out var edit)) return;
        if (back) edit.Undo(); else edit.Redo();
        to.Push(edit);
        UpdateTool();
        dirty = true;
        SetStatus();
    }
}
