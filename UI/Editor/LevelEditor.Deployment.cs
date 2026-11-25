// Handles campaign rectangle fills, deployment line editing and live soldier strength.
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace MassiveRts;

public partial class LevelEditor
{
    private enum SoldierPaintMode { Brush, Rectangle, Formation }
    private SoldierPaintMode soldierPaintMode = SoldierPaintMode.Rectangle;
    private int editorBrushSize = 1;
    private bool fillRectangle = true, redTower;
    private float densityScale = 1;
    private Vector2I? rectangleStart;
    private MouseButton rectangleButton;
    private RuntimeUnitCatalog placementCatalog;

    private DeploymentOrderOverlay redOrderOverlay;
    private readonly DisruptorGrid.LineTool redDisruptorLine = new();
    private MultiMeshInstance3D redDisruptorView, redDisruptorPreview;

    private bool HandleRedDisruptors(InputEvent input)
    {
        var rules = map.AutoBattlerRules;
        return redDisruptorLine.Handle(input, WorldPoint, rules.RedDisruptorCells, DisruptorGrid.MaxTiles,
            cell => DisruptorGrid.Inside(cell, map.MapSize) &&
                rules.AllowsSide(DisruptorGrid.Center(cell), DisruptorGrid.Size * 0.5f, blue: false),
            draft => SetValue(rules.RedDisruptorCells, new Godot.Collections.Array<Vector2I>(draft),
                value => rules.RedDisruptorCells = value), SetStatus, PreviewRedDisruptors);
    }

    private void RefreshRedDisruptors()
    {
        if (map.AutoBattlerRules is not { } rules) return;
        if (redDisruptorView == null)
        {
            redDisruptorView = new MultiMeshInstance3D();
            redDisruptorPreview = new MultiMeshInstance3D { Visible = false };
            map.AddChild(redDisruptorView, false, InternalMode.Back);
            map.AddChild(redDisruptorPreview, false, InternalMode.Back);
        }
        DisruptorGridVisual.Refresh(redDisruptorView, rules.RedDisruptorCells.ToArray(), Factions.Red, Surface);
    }

    private void PreviewRedDisruptors(IReadOnlyCollection<Vector2I> cells)
    {
        if (redDisruptorPreview != null)
            DisruptorGridVisual.Refresh(redDisruptorPreview, cells, Factions.Red, Surface);
    }

    private void CloseRedOrders()
    {
        if (redOrderOverlay == null) return;
        redOrderOverlay.GetParent().RemoveChild(redOrderOverlay);
        redOrderOverlay.QueueFree();
        redOrderOverlay = null;
    }

    private IEnumerable<SoldierPlacement> AuthoredRedSoldiers()
    {
        foreach (var group in map.GetNode("StartingUnits").GetChildren().OfType<CommandUnitPlacement>())
        {
            if (group.Unit == null || group.Faction != CommandUnitPlacement.Team.Red) continue;
            float footprint = UnitOutline.FromDefinition(group.Unit, UnitOutline.EffectiveSize(group.Unit)).Footprint;
            foreach (var point in group.Positions(footprint))
                yield return new SoldierPlacement(0, 0, Factions.Red, 0) { AuthoredPosition = point };
        }

    }

    private void OpenRedOrders()
    {
        var rules = map.AutoBattlerRules;
        var overlay = new DeploymentOrderOverlay
        {
            AuthoredSoldiers = AuthoredRedSoldiers().ToArray(), EditorCamera = camera, EditorGround = WorldPoint,
            EditorSurface = point => new Vector3(point.X, Surface(point), point.Z),
            OffenseEditor = mode.CampaignId == "Offense", ZIndex = 10, OrdersChanged = SaveOrders
        };
        redOrderOverlay = overlay;
        interfaceRoot.AddChild(overlay);
        overlay.BuildPopup();
        overlay.LoadAuthoredOrders(rules);

        void SaveOrders()
        {
            var before = rules.RedOpeningOrders;
            var after = redOrderOverlay.SaveAuthoredOrders();
            if (before.SequenceEqual(after)) return;
            void Apply(Vector3[] data)
            {
                rules.RedOpeningOrders = data;
                redOrderOverlay?.LoadAuthoredOrders(rules);
            }
            rules.RedOpeningOrders = after;
            Remember(() => Apply(before), () => Apply(after));
        }
    }

    // =========================================================
    public override void _Input(InputEvent input)
    {
        if (map?.Visible != true || runningTest != null) return;
        if (input is InputEventKey { PhysicalKeycode: Key.Left or Key.Right or Key.Up or Key.Down })
            GetViewport().SetInputAsHandled();
    }

    private void DeploymentLineTools()
    {
        var rules = map.AutoBattlerRules;
        Heading(context, "Blue and Red occupy opposite sides.", 15);
        Choice(context, "Split along coordinate", new[] { "Off", "X (left / right)", "Z (north / south)" },
            (int)rules.LineAxis, value =>
            {
                SetValue(rules.LineAxis, (AutoBattlerRules.DeploymentAxis)value, axis => rules.LineAxis = axis);
                UpdateTool();
            });
        if (rules.LineAxis == AutoBattlerRules.DeploymentAxis.Disabled) return;
        float half = map.MapSize * 0.5f;
        Number(context, "Line coordinate", rules.LineOffset, -half + Grid, half - Grid, 1).ValueChanged += value =>
            SetValue(rules.LineOffset, (float)value, position => rules.LineOffset = position);
        Button(context, rules.FlipBlueSide ? "Flip Blue to lower coordinates" : "Flip Blue to higher coordinates", () =>
        {
            SetValue(rules.FlipBlueSide, !rules.FlipBlueSide, flip => rules.FlipBlueSide = flip);
            UpdateTool();
        });
        Heading(context, "Click the map to position the line.", 15);
    }

    private void SetLine(AutoBattlerRules.DeploymentAxis axis, Vector3 point)
    {
        var rules = map.AutoBattlerRules;
        float half = map.MapSize * 0.5f;
        float offset = Mathf.Clamp(axis == AutoBattlerRules.DeploymentAxis.X ? point.X : point.Z, -half + Grid, half - Grid);
        SetValue((rules.LineAxis, rules.LineOffset), (axis, offset), value =>
        {
            rules.LineAxis = value.Item1;
            rules.LineOffset = value.Item2;
        });
        UpdateTool();
    }

    private BuildingDefinition SelectedDefense => mode.CampaignId == "Offense" && paletteDefenseIndex == defenseDefinitions.Length
        ? GD.Load<BuildingDefinition>("res://WORLD/BUILDINGS/DEFINITIONS/Citadel.tres")
        : defenseDefinitions[mode.IsAutoBattler ? paletteDefenseIndex : defensePicker.Selected - 1];

    private Vector3 TowerCursor()
    {
        var area = CommandLaserPlacement.FootprintAt(Vector3.Zero, SelectedDefense);
        Vector2 size = area.Max - area.Min;
        return new Vector3(size.X, 1, size.Y);
    }

    private bool TowerSiteClear(CommandLaserPlacement tower, Vector3 point)
    {
        var area = tower.Footprint(point);
        float half = map.MapSize * 0.5f;
        var (rocks, water) = paint.Compile();
        var obstacles = map.ReadAuthoredWalls().Concat(rocks).Concat(water)
            .Concat(Objects().OfType<CommandLaserPlacement>().Where(other => other != tower)
                .Select(other => other.Footprint(other.GlobalPosition)));
        var terrain = map.GetNodeOrNull<CommandTerrain>("Terrain")?.Field;
        float relief = 0;
        terrain?.FootprintHeight(area.Min, area.Max, out relief);
        if (area.Min.X >= -half && area.Min.Y >= -half && area.Max.X <= half && area.Max.Y <= half &&
            relief <= 0.5f && !obstacles.Any(other => area.Min.X < other.Max.X && area.Max.X > other.Min.X &&
                area.Min.Y < other.Max.Y && area.Max.Y > other.Min.Y)) return true;
        SetStatus("A defense needs a clear, flat footprint inside the map.");
        return false;
    }

    private bool FitsPresetSide(Node3D node, Vector3 center)
    {
        var rules = map.AutoBattlerRules;
        if (rules == null) return true;
        if (node is CommandLaserPlacement tower) return tower.FitsSide(rules, center);
        if (node is not CommandUnitPlacement group) return true;
        return group.FitsSide(rules, center);
    }

    // =========================================================
    private bool HandleSoldierBrush(InputEvent input)
    {
        ObstacleRectangle Stamp(Vector3 point)
        {
            var first = ClampedCell(point) - Vector2I.One * ((editorBrushSize - 1) / 2);
            return new ObstacleRectangle(first.X, first.Y, editorBrushSize, editorBrushSize);
        }

        if (input is InputEventMouseMotion motion)
        {
            if (WorldPoint(motion.Position, out var hover))
            {
                var stamp = Stamp(hover);
                Vector2 center = (stamp.Min + stamp.Max) * 0.5f;
                cursor.GlobalPosition = new Vector3(center.X, Surface(hover) + 0.15f, center.Y) + PaintOrigin;
                cursor.Scale = new Vector3(stamp.Max.X - stamp.Min.X, 1, stamp.Max.Y - stamp.Min.Y);
                cursor.Show();
            }
            else cursor.Hide();
            return true;
        }
        if (input is not InputEventMouseButton mouse || mouse.ButtonIndex is not (MouseButton.Left or MouseButton.Right))
            return false;
        if (!mouse.Pressed || !WorldPoint(mouse.Position, out var point)) return true;
        var area = Stamp(point);
        try { EditSoldierRectangle(area, mouse.ButtonIndex == MouseButton.Right); }
        catch (Exception error)
        {
            GD.PushError($"Soldier brush failed: {error}");
            SetStatus(error.Message);
        }
        return true;
    }

    private bool HandleSoldierRectangle(InputEvent input)
    {
        if (input is InputEventMouseMotion motion && rectangleStart is Vector2I start)
        {
            WorldPoint(motion.Position, out var point);
            var area = RectangleArea(start, ClampedCell(point));
            Vector2 center = (area.Min + area.Max) * 0.5f;
            cursor.GlobalPosition = new Vector3(center.X, Surface(point) + 0.15f, center.Y) + PaintOrigin;
            cursor.Scale = new Vector3(area.Max.X - area.Min.X, 1, area.Max.Y - area.Min.Y);
            cursor.Show();
            return true;
        }
        if (input is not InputEventMouseButton mouse || mouse.ButtonIndex is not (MouseButton.Left or MouseButton.Right)) return false;
        if (mouse.Pressed)
        {
            if (WorldPoint(mouse.Position, out var point) && rectangleStart == null)
            {
                rectangleStart = ClampedCell(point);
                rectangleButton = mouse.ButtonIndex;
                ShowCursor(point);
            }
            return true;
        }
        if (rectangleStart is not Vector2I first || rectangleButton != mouse.ButtonIndex) return true;
        rectangleStart = null;
        cursor.Hide();
        WorldPoint(mouse.Position, out var last);
        try { EditSoldierRectangle(RectangleArea(first, ClampedCell(last)), mouse.ButtonIndex == MouseButton.Right); }
        catch (Exception error)
        {
            GD.PushError($"Soldier rectangle failed: {error}");
            SetStatus(error.Message);
        }
        return true;
    }

    private Vector2I ClampedCell(Vector3 point)
    {
        int edge = Mathf.FloorToInt(PaintHalfWidth / Grid);
        var cell = Cell(point - PaintOrigin);
        return new Vector2I(Mathf.Clamp(cell.X, -edge, edge - 1), Mathf.Clamp(cell.Y, -edge, edge - 1));
    }

    private static ObstacleRectangle RectangleArea(Vector2I a, Vector2I b) => new(
        Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X) + 1, Math.Abs(a.Y - b.Y) + 1);

    private void EditSoldierRectangle(ObstacleRectangle area, bool erase)
    {
        var groups = map.GetNode(UnitContainerName).GetChildren().OfType<CommandUnitPlacement>().ToArray();
        if (erase) { EraseRectangle(area, groups); return; }
        placementCatalog ??= GD.Load<UnitCatalog>("res://GAMEPLAY/UNITS/CATALOG/DefaultUnitCatalog.tres").Compile(1f);
        var candidates = new DeploymentLayout(placementCatalog, null);
        var unit = units[unitIndex];
        if (!placementCatalog.TryResolve(unit.Id, out byte type)) return;
        float radius = placementCatalog[type].Footprint;
        float half = PaintHalfWidth;
        byte faction = mode.IsAutoBattler || red ? Factions.Red : Factions.Blue;
        int subdivisions = DeploymentLayout.SoldierSubdivisions;
        int limit = mode.IsAutoBattler ? 100000 : CommandSandbox.NonWaveUnitCapacity - (map.SpawnDefaultBlueSquad ? CommandMovement.SquadSize : 0);
        int existing = 0;
        foreach (var group in groups)
        {
            existing += group.SoldierCount;
            if (!placementCatalog.TryResolve(group.Unit?.Id, out byte previousType)) continue;
            foreach (var point in group.Positions(placementCatalog[previousType].Footprint))
            {
                Vector3 local = point - PaintOrigin;
                int x = Mathf.RoundToInt((local.X / Grid - 0.5f) * subdivisions);
                int z = Mathf.RoundToInt((local.Z / Grid - 0.5f) * subdivisions);
                candidates.TryPaint(new SoldierPlacement(x, z, (byte)group.Faction, previousType), out _);
            }
        }
        var (rocks, water) = paint.Compile();
        var walls = map.ReadAuthoredWalls();
        var blocked = rocks.Concat(water).Concat(walls)
            .Concat(Objects().OfType<CommandLaserPlacement>().Select(tower => tower.Footprint(tower.GlobalPosition))).ToArray();
        var collision = editingReserve || blocked.Length == 0 ? null : new ObstacleField(blocked, half);
        Vector2 center = (area.Min + area.Max) * 0.5f;
        var offsets = new List<Vector2>();
        foreach (var cell in DeploymentLayout.RectangleSoldierSlots(area, densityScale, radius))
        {
            var slot = new SoldierPlacement(cell.X, cell.Y, faction, type);
            var point = slot.Position();
            if (Mathf.Abs(point.X) + radius > half || Mathf.Abs(point.Z) + radius > half ||
                (!editingReserve && map.AutoBattlerRules?.AllowsSide(point, radius, blue: false) == false) ||
                collision?.IntersectsCircle(ObstacleField.Xz(point), radius) == true ||
                candidates.TryGet(cell, out _) || !candidates.TryPaint(slot, out _)) continue;
            if (existing + offsets.Count >= limit) throw new InvalidOperationException($"This fill would exceed the level limit of {limit:N0} authored soldiers.");
            offsets.Add(ObstacleField.Xz(point) - center);
        }
        if (offsets.Count == 0) { SetStatus("No clear positions in this rectangle."); return; }
        var patch = LoadNode<CommandUnitPlacement>("res://GAMEPLAY/DEPLOYMENT/COMMAND/CommandUnitPlacement.tscn");
        patch.Name = "SoldierRectangle";
        patch.Unit = unit;
        patch.Faction = (CommandUnitPlacement.Team)faction;
        patch.PaintedOffsets = offsets.ToArray();
        if (!TryAddPlacement(patch, UnitContainerName, new Vector3(center.X, 0, center.Y) + PaintOrigin)) patch.Free();
    }

    private void EraseRectangle(ObstacleRectangle area, CommandUnitPlacement[] groups)
    {
        var changes = new List<(CommandUnitPlacement Group, Vector2[] Before, Vector2[] After)>();
        foreach (var group in groups)
        {
            if (group.Unit == null) continue;
            float footprint = UnitOutline.FromDefinition(group.Unit, UnitOutline.EffectiveSize(group.Unit)).Footprint;
            var remaining = group.Positions(footprint)
                .Where(world =>
                {
                    Vector3 point = world - PaintOrigin;
                    return point.X < area.Min.X || point.X >= area.Max.X || point.Z < area.Min.Y || point.Z >= area.Max.Y;
                })
                .Select(point => ObstacleField.Xz(group.ToLocal(point))).ToArray();
            if (remaining.Length == group.SoldierCount) continue;
            changes.Add((group, group.PaintedOffsets, remaining));
        }
        if (changes.Count == 0) return;
        var parent = map.GetNode(UnitContainerName);
        void Apply(bool erase)
        {
            foreach (var change in changes)
            {
                if (change.After.Length == 0)
                {
                    if (erase) { parent.RemoveChild(change.Group); if (selected == change.Group) selected = null; }
                    else { parent.AddChild(change.Group, true); change.Group.Owner = map; }
                }
                else change.Group.PaintedOffsets = erase ? change.After : change.Before;
            }
            RefreshPlacement(changes[0].Group);
        }
        detached.AddRange(changes.Where(change => change.After.Length == 0).Select(change => change.Group));
        Apply(true);
        Remember(() => Apply(false), () => Apply(true));
    }

    private void RefreshStrength()
    {
        var groups = map.GetNode("StartingUnits").GetChildren().OfType<CommandUnitPlacement>().ToArray();
        string Team(CommandUnitPlacement.Team team)
        {
            var army = groups.Where(group => group.Faction == team && group.Unit != null);
            return $"{team}  {army.Sum(group => (long)group.SoldierCount):N0} units  ·  {army.Sum(group => (long)group.SoldierCount * Mathf.Clamp(group.Unit.BattleRating, 0, UnitDefinition.MaxBattleRating)):N0} strength";
        }
        string blue = mode.IsAutoBattler ? $"Blue allowance  {map.AutoBattlerRules.BlueUnitLimit:N0} units · strength set by deployment" : Team(CommandUnitPlacement.Team.Blue);
        strength.Text = $"{map.MapSize:0} × {map.MapSize:0}     |     {blue}     |     {Team(CommandUnitPlacement.Team.Red)}";
    }
}
