// Mouse placement and an instanced preview for the benchmark deployment layout.
using System.Text.Json;
using Godot;

namespace MassiveRts;

public partial class DeploymentPainter : Node3D
{
    public DeploymentLayout Layout { get; private set; }
    private RuntimeUnitCatalog catalog;
    private RuntimeBuildingCatalog buildings;
    private DefenseRenderer defensePreview;
    public int DefenseType { get; set; }
    private DeploymentPreview[] previews;
    public Camera3D Camera { get; set; }
    public byte BrushFaction { get; set; }
    public byte BrushType { get; set; }
    // Number of placement cells per side; defaults to the original 3×3 brush.
    public float SoldierBrushSpread { get; set; } = 1f;
    private bool fillSoldierRectangle;

    public void SetSoldierPaintType(long index)
    {
        CancelDrag();
        fillSoldierRectangle = index == 1;
    }

    public int SoldierBrushSize { get; set; } = 3;
    public event System.Action Changed;
    public event System.Action<string> EditRejected;
    private MeshInstance3D walls, rectanglePreview, placementGrid;
    private RectangleDimensions dimensions;
    private bool dragging;
    private long paintTool;
    private MouseButton dragButton;
    private Vector2I dragStart;
    public float ObstacleHeight { get; set; }
    public Color ObstacleTint { get; set; }
    public Mesh ObstacleMesh => walls.Mesh;

    public void SetPaintTool(long tool)
    {
        paintTool = tool;
        CancelDrag();
    }

    public void CancelDrag()
    {
        dragging = false;
        dimensions?.Clear();
        rectanglePreview.Hide();
    }

    // =========================================================
    // The parent DeploymentPainter is itself hidden outside edit mode.
    public void SetPlacementGridVisible(bool show)
    {
        if (placementGrid != null) placementGrid.Visible = show;
    }

    public void Initialize(RuntimeUnitCatalog unitCatalog, Mesh[] shapes, RuntimeBuildingCatalog buildingCatalog)
    {
        catalog = unitCatalog;
        buildings = buildingCatalog;
        Layout = new DeploymentLayout(catalog, buildings);
        defensePreview = new DefenseRenderer();
        AddChild(defensePreview);
        previews = new DeploymentPreview[catalog.Count];
        for (int i = 0; i < previews.Length; i++)
            previews[i] = new DeploymentPreview(this, shapes[i], catalog[i].Height);
        // Shader draws the same cell boundaries used by TryCell and obstacle footprints.
        // One flat surface; no per-cell nodes, physics, or mesh rebuilds while panning.
        var gridMaterial = new ShaderMaterial
        {
            Shader = GD.Load<Shader>("res://DEPLOYMENT/PlacementGrid.gdshader")
        };
        gridMaterial.SetShaderParameter("cell_size", Layout.GridSize);
        placementGrid = new MeshInstance3D
        {
            Name = "PlacementGrid",
            Mesh = new PlaneMesh { Size = new Vector2(5000f, 5000f) },
            MaterialOverride = gridMaterial,
            Position = new Vector3(0f, 0.035f, 0f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        AddChild(placementGrid);

        walls = new MeshInstance3D { CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(walls);
        rectanglePreview = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = Vector3.One }, Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.67f, 0.75f, 0.84f, 0.32f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
            }
        };
        AddChild(rectanglePreview);
        dimensions = new RectangleDimensions();
        AddChild(dimensions);
        dimensions.Clear();
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (!Visible) return;
        if (paintTool == 2)
        {
            HandleDefenseInput(input);
            return;
        }
        if (paintTool == 1)
        {
            HandleRectangleInput(input, soldiers: false);
            return;
        }
        if (fillSoldierRectangle)
        {
            HandleRectangleInput(input, soldiers: true);
            return;
        }
        Vector2 screen;
        bool erase;
        if (input is InputEventMouseButton button && button.Pressed &&
            (button.ButtonIndex == MouseButton.Left || button.ButtonIndex == MouseButton.Right))
        {
            screen = button.Position;
            erase = button.ButtonIndex == MouseButton.Right;
        }
        else if (input is InputEventMouseMotion motion &&
            (motion.ButtonMask & (MouseButtonMask.Left | MouseButtonMask.Right)) != 0)
        {
            screen = motion.Position;
            erase = (motion.ButtonMask & MouseButtonMask.Right) != 0;
        }
        else return;

        if (!TryGround(screen, out Vector3 ground)) return;
        bool changed = false;
        int side = Mathf.Clamp(SoldierBrushSize, 1, 10);
        int subdivisions = DeploymentLayout.SoldierSubdivisions;
        float spread = Mathf.Clamp(SoldierBrushSpread, 0f, 3f);
        int stride = Mathf.Max(Mathf.RoundToInt(spread * subdivisions),
            Mathf.CeilToInt(catalog[BrushType].Footprint * 2f / Layout.SoldierStep));
        // Keep the original cell alignment at default spacing.
        float xCell = ground.X / Layout.GridSize, zCell = ground.Z / Layout.GridSize;
        Vector2I center = spread == 1f
            ? new Vector2I(Mathf.FloorToInt(xCell), Mathf.FloorToInt(zCell)) * subdivisions
            : new Vector2I(Mathf.RoundToInt((xCell - 0.5f) * subdivisions / stride),
                Mathf.RoundToInt((zCell - 0.5f) * subdivisions / stride)) * stride;
        int first = -(side - 1) / 2;
        if (erase)
        {
            int min = first * stride - stride / 2;
            int max = (first + side) * stride - stride / 2;
            for (int i = Layout.Placements.Count - 1; i >= 0; i--)
            {
                SoldierPlacement previous = Layout.Placements[i];
                int x = previous.X - center.X, z = previous.Z - center.Y;
                if (x < min || x >= max || z < min || z >= max) continue;
                var point = new Vector2I(previous.X, previous.Z);
                Layout.TryErase(point, out _);
                previews[previous.Type].Remove(point);
                changed = true;
            }
        }
        else
        {
            for (int z = first; z < first + side; z++)
            for (int x = first; x < first + side; x++)
            {
                changed |= PaintSoldier(center + new Vector2I(x * stride, z * stride));
            }
        }
        if (changed) Changed?.Invoke();
    }

    private void HandleDefenseInput(InputEvent input)
    {
        if ((uint)DefenseType >= (uint)buildings.Count) return;
        if (input is InputEventMouseMotion motion && TryCell(motion.Position, out var hover))
        {
            var draft = new BuildingPlacement(hover.X, hover.Y, BrushFaction, DefenseType, Layout.GridSize);
            if (Layout.CanPlaceDefense(draft)) ShowRectangle(draft.Footprint(buildings));
            else rectanglePreview.Hide();
            return;
        }

        if (input is not InputEventMouseButton button || !button.Pressed ||
            !TryCell(button.Position, out var cell)) return;
        bool changed;
        if (button.ButtonIndex == MouseButton.Left)
        {
            changed = Layout.TryPlaceDefense(new BuildingPlacement(cell.X, cell.Y, BrushFaction, DefenseType, Layout.GridSize));
            if (!changed) EditRejected?.Invoke("Defense overlaps a footprint, is outside the field, or the defense limit was reached.");
        }
        else if (button.ButtonIndex == MouseButton.Right) changed = Layout.TryEraseDefense(cell);
        else return;

        if (!changed) return;
        rectanglePreview.Hide();
        RefreshDefenses();
        Changed?.Invoke();
    }

    private void RefreshDefenses() => defensePreview.Initialize(Layout.Defenses, buildings, ObstacleHeight, battle: false);

    private bool TryCell(Vector2 screen, out Vector2I cell)
    {
        cell = default;
        if (!TryGround(screen, out Vector3 ground)) return false;
        cell = new Vector2I(Mathf.FloorToInt(ground.X / Layout.GridSize),
            Mathf.FloorToInt(ground.Z / Layout.GridSize));
        return true;
    }

    private bool TryGround(Vector2 screen, out Vector3 ground)
    {
        ground = default;
        Vector3 origin = Camera.ProjectRayOrigin(screen);
        Vector3 direction = Camera.ProjectRayNormal(screen);
        if (direction.Y >= 0 || origin.Y < 0) return false;
        ground = origin - direction * (origin.Y / direction.Y);
        return true;
    }

    // Release may be consumed by a GUI control. Cancel it if it never reaches _UnhandledInput.
    public override void _Input(InputEvent input)
    {
        if (dragging && input is InputEventMouseButton button && !button.Pressed && button.ButtonIndex == dragButton)
            Callable.From(() => { if (dragging && !Input.IsMouseButtonPressed(dragButton)) CancelDrag(); }).CallDeferred();
    }

    private void HandleRectangleInput(InputEvent input, bool soldiers)
    {
        if (input is InputEventMouseButton button &&
            (button.ButtonIndex == MouseButton.Left || button.ButtonIndex == MouseButton.Right))
        {
            if (button.Pressed)
            {
                if (dragging || !TryCell(button.Position, out dragStart)) return;
                var first = new ObstacleRectangle(dragStart.X, dragStart.Y, 1, 1) { GridSize = Layout.GridSize };
                if (!first.IsValid) return;
                dragging = true;
                dragButton = button.ButtonIndex;
                ShowDragRectangle(first, soldiers);
            }
            else if (dragging && button.ButtonIndex == dragButton)
            {
                CancelDrag();
                if (!TryCell(button.Position, out Vector2I end)) return;
                var area = DragArea(end);
                bool erase = dragButton == MouseButton.Right;
                if (soldiers)
                {
                    EditSoldierRectangle(area, erase);
                    return;
                }
                if (Layout.TryEditObstacles(area, erase, out string message))
                {
                    RebuildWalls();
                    Changed?.Invoke();
                }
                else if (message != null) EditRejected?.Invoke(message);
            }
        }
        else if (dragging && input is InputEventMouseMotion motion && TryCell(motion.Position, out Vector2I hover))
            ShowDragRectangle(DragArea(hover), soldiers);
    }

    private void EditSoldierRectangle(ObstacleRectangle area, bool erase)
    {
        bool changed = false;
        if (erase)
        {
            for (int i = Layout.Placements.Count - 1; i >= 0; i--)
            {
                SoldierPlacement previous = Layout.Placements[i];
                Vector2 point = ObstacleField.Xz(previous.Position());
                if (point.X < area.Min.X || point.Y < area.Min.Y || point.X >= area.Max.X || point.Y >= area.Max.Y) continue;
                var key = new Vector2I(previous.X, previous.Z);
                Layout.TryErase(key, out _);
                previews[previous.Type].Remove(key);
                changed = true;
            }
        }
        else
        {
            int subdivisions = DeploymentLayout.SoldierSubdivisions;
            int stride = Mathf.Max(Mathf.RoundToInt(SoldierBrushSpread * subdivisions),
                Mathf.CeilToInt(catalog[BrushType].Footprint * 2f / Layout.SoldierStep));
            int spanX = area.Width * subdivisions - subdivisions / 2;
            int spanZ = area.Depth * subdivisions - subdivisions / 2;
            long columns = (spanX + stride - 1) / stride;
            long rows = (spanZ + stride - 1) / stride;
            if (columns * rows > 100_000)
            {
                EditRejected?.Invoke("Rectangle exceeds 100,000 placement positions. Use a smaller area or wider spacing.");
                return;
            }
            // Anchor the pattern to the first cell inside the rectangle, regardless of drag direction.
            for (int z = area.Z * subdivisions; z < (area.Z + area.Depth) * subdivisions - subdivisions / 2; z += stride)
            for (int x = area.X * subdivisions; x < (area.X + area.Width) * subdivisions - subdivisions / 2; x += stride)
                changed |= PaintSoldier(new Vector2I(x, z));
        }
        if (changed) Changed?.Invoke();
    }

    private bool PaintSoldier(Vector2I point)
    {
        bool existed = Layout.TryGet(point, out SoldierPlacement previous);
        var placement = new SoldierPlacement(point.X, point.Y, BrushFaction, BrushType, Layout.GridSize);
        if (!Layout.TryPaint(placement, out _)) return false;
        if (existed) previews[previous.Type].Remove(point);
        previews[placement.Type].Add(placement);
        return true;
    }

    private void ShowDragRectangle(ObstacleRectangle area, bool soldiers)
    {
        ShowRectangle(area);
        dimensions.Display(Camera, area);
        if (!soldiers) return;
        rectanglePreview.Scale = new Vector3(rectanglePreview.Scale.X, 0.02f, rectanglePreview.Scale.Z);
        rectanglePreview.Position = new Vector3(rectanglePreview.Position.X, 0.055f, rectanglePreview.Position.Z);
    }

    private ObstacleRectangle DragArea(Vector2I end)
    {
        end.X = Mathf.Clamp(end.X, -Layout.HalfWidth, Layout.HalfWidth - 1);
        end.Y = Mathf.Clamp(end.Y, -Layout.HalfWidth, Layout.HalfWidth - 1);
        return new ObstacleRectangle(Mathf.Min(dragStart.X, end.X), Mathf.Min(dragStart.Y, end.Y),
            Mathf.Abs(dragStart.X - end.X) + 1, Mathf.Abs(dragStart.Y - end.Y) + 1) { GridSize = Layout.GridSize };
    }

    private void ShowRectangle(ObstacleRectangle area)
    {
        Vector2 size = area.Max - area.Min, center = (area.Min + area.Max) * 0.5f;
        rectanglePreview.Scale = new Vector3(size.X, ObstacleHeight + 0.04f, size.Y);
        rectanglePreview.Position = new Vector3(center.X, ObstacleHeight * 0.5f, center.Y);
        rectanglePreview.Show();
    }

    private void RebuildWalls() => walls.Mesh = ObstacleRenderer.Build(Layout.Obstacles.Field, ObstacleHeight, ObstacleTint);

    public void Clear()
    {
        CancelDrag();
        Layout = new DeploymentLayout(catalog, buildings);
        ((ShaderMaterial)placementGrid.MaterialOverride).SetShaderParameter("cell_size", Layout.GridSize);
        RebuildWalls();
        RefreshDefenses();
        foreach (DeploymentPreview preview in previews) preview.Clear();
        Changed?.Invoke();
    }

    private const string SavePath = "user://deployment.json";

    private sealed record SavedPlacement(int X, int Z, byte Faction, string UnitId);
    private sealed record SavedDefense(int X, int Z, byte Faction, string BuildingId);
    public sealed record BattleSettings(int Seed, GuardOrder[] Orders, float GuardRadius);
    private sealed record SavedDeployment(int Version, SavedPlacement[] Placements, ObstacleRectangle[] Obstacles,
        SavedDefense[] Defenses, BattleSettings Battle = null, float GridSize = DeploymentLayout.LegacyCellSize);
    private readonly record struct LegacyPlacement(int X, int Z, byte Faction, byte Kind);

    public string Save() => Save(SavePath, null);

    public string Save(string path, BattleSettings battle)
    {
        var saved = new SavedPlacement[Layout.Placements.Count];
        for (int i = 0; i < saved.Length; i++)
        {
            SoldierPlacement placement = Layout.Placements[i];
            saved[i] = new SavedPlacement(placement.X, placement.Z, placement.Faction, catalog[placement.Type].Id);
        }
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file == null) return $"Save failed ({FileAccess.GetOpenError()}).";
        var defenses = new SavedDefense[Layout.Defenses.Count];
        for (int i = 0; i < defenses.Length; i++)
        {
            var building = Layout.Defenses[i];
            defenses[i] = new SavedDefense(building.X, building.Z, building.Faction, buildings[building.Type].Id);
        }
        file.StoreString(JsonSerializer.Serialize(new SavedDeployment(5, saved, Layout.Obstacles.Field == null
            ? System.Array.Empty<ObstacleRectangle>() : Layout.Obstacles.Field.Rectangles.ToArray(), defenses, battle, Layout.GridSize)));
        return file.GetError() == Error.Ok ? "Deployment saved." : "Could not write deployment.";
    }

    public string Load()
    {
        string message = ReadDeployment(SavePath, out var loaded, out _);
        if (loaded == null) return message;
        CancelDrag();
        Layout = loaded;
        ((ShaderMaterial)placementGrid.MaterialOverride).SetShaderParameter("cell_size", Layout.GridSize);
        RebuildWalls();
        RefreshDefenses();
        foreach (DeploymentPreview preview in previews) preview.Clear();
        foreach (SoldierPlacement placement in Layout.Placements) previews[placement.Type].Add(placement);
        Changed?.Invoke();
        return message;
    }

    public string ReadDeployment(string path, out DeploymentLayout layout, out BattleSettings battle)
    {
        layout = null;
        battle = null;
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null) return "No readable saved deployment.";

        DeploymentLayout validated;
        try
        {
            using var document = JsonDocument.Parse(file.GetAsText());
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                validated = new DeploymentLayout(catalog, buildings, DeploymentLayout.LegacyCellSize);
                var legacy = document.RootElement.Deserialize<LegacyPlacement[]>();
                foreach (LegacyPlacement placement in legacy)
                {
                    byte type = placement.Kind switch
                    {
                        0 => RuntimeUnitCatalog.Circle,
                        1 => RuntimeUnitCatalog.Triangle,
                        _ => byte.MaxValue
                    };
                    if (!TryLoadPlacement(validated, placement.X, placement.Z, placement.Faction, type))
                        return "Saved deployment contains invalid placements.";
                }
            }
            else
            {
                var saved = document.RootElement.Deserialize<SavedDeployment>();
                if (saved == null || saved.Version is not (1 or 2 or 3 or 4 or 5) || saved.Placements == null) return "Unsupported deployment format.";
                float gridSize = saved.Version < 5 ? DeploymentLayout.LegacyCellSize : saved.GridSize;
                if (gridSize != DeploymentLayout.CellSize && gridSize != DeploymentLayout.LegacyCellSize)
                    return "Unsupported deployment grid size.";
                validated = new DeploymentLayout(catalog, buildings, gridSize);
                if (saved.Version >= 2)
                {
                    if (!ObstacleLayout.TryLoad(saved.Obstacles, gridSize, out ObstacleLayout obstacles))
                        return "Saved deployment contains invalid obstacle rectangles.";
                    validated.SetLoadedObstacles(obstacles);
                }
                if (saved.Version >= 3)
                {
                    if (saved.Defenses == null || saved.Defenses.Length > DeploymentLayout.MaxBuildings)
                        return "Saved deployment contains invalid defenses.";
                    foreach (var defense in saved.Defenses)
                        if (defense == null || !buildings.TryResolve(defense.BuildingId, out int type) ||
                            !validated.TryPlaceDefense(new BuildingPlacement(defense.X, defense.Z, defense.Faction, type, gridSize)))
                            return "Saved deployment contains invalid defenses.";
                }
                if (saved.Battle != null)
                {
                    if (saved.Battle.Seed < 0 || saved.Battle.Orders?.Length != Factions.Count ||
                        !float.IsFinite(saved.Battle.GuardRadius) || saved.Battle.GuardRadius < 1f || saved.Battle.GuardRadius > 100f)
                        return "Saved battle settings are invalid.";
                    foreach (GuardOrder order in saved.Battle.Orders)
                        if (!System.Enum.IsDefined(order)) return "Saved battle order is invalid.";
                }
                battle = saved.Battle;
                foreach (SavedPlacement placement in saved.Placements)
                {
                    if (placement == null || !catalog.TryResolve(placement.UnitId, out byte type) ||
                        !TryLoadPlacement(validated, placement.X, placement.Z, placement.Faction, type, saved.Version >= 4))
                        return "Saved deployment contains invalid placements.";
                }
            }
        }
        catch (JsonException)
        {
            return "Saved deployment is invalid.";
        }

        layout = validated;
        return "Deployment loaded.";
    }

    private bool TryLoadPlacement(DeploymentLayout draft, int x, int z, byte faction, byte type, bool fine = false)
    {
        if (!fine)
        {
            if (x < -draft.HalfWidth || x >= draft.HalfWidth ||
                z < -draft.HalfWidth || z >= draft.HalfWidth) return false;
            x *= DeploymentLayout.SoldierSubdivisions;
            z *= DeploymentLayout.SoldierSubdivisions;
        }
        return !draft.TryGet(new Vector2I(x, z), out _) &&
            draft.TryPaint(new SoldierPlacement(x, z, faction, type, draft.GridSize), out _);
    }
}
