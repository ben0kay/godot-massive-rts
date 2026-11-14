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
    public int SoldierBrushSize { get; set; } = 3;
    public event System.Action Changed;
    public event System.Action<string> EditRejected;
    private MeshInstance3D walls, rectanglePreview;
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
        rectanglePreview.Hide();
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
            HandleObstacleInput(input);
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

        if (!TryCell(screen, out Vector2I cell)) return;
        bool changed = false;
        int side = Mathf.Clamp(SoldierBrushSize, 1, 10);
        // Even-width brushes use four cells before and five after the cursor at 10×10.
        int first = -(side - 1) / 2;
        for (int z = first; z < first + side; z++)
        for (int x = first; x < first + side; x++)
        {
            var point = cell + new Vector2I(x, z);
            bool existed = Layout.TryGet(point, out SoldierPlacement previous);
            var placement = new SoldierPlacement(point.X, point.Y, BrushFaction, BrushType);
            bool edited = erase ? Layout.TryErase(point, out _) : Layout.TryPaint(placement, out _);
            if (!edited) continue;
            if (existed) previews[previous.Type].Remove(point);
            if (!erase) previews[placement.Type].Add(placement);
            changed = true;
        }
        if (changed) Changed?.Invoke();
    }

    private void HandleDefenseInput(InputEvent input)
    {
        if ((uint)DefenseType >= (uint)buildings.Count) return;
        if (input is InputEventMouseMotion motion && TryCell(motion.Position, out var hover))
        {
            var draft = new BuildingPlacement(hover.X, hover.Y, BrushFaction, DefenseType);
            if (Layout.CanPlaceDefense(draft)) ShowRectangle(draft.Footprint(buildings));
            else rectanglePreview.Hide();
            return;
        }

        if (input is not InputEventMouseButton button || !button.Pressed ||
            !TryCell(button.Position, out var cell)) return;
        bool changed;
        if (button.ButtonIndex == MouseButton.Left)
        {
            changed = Layout.TryPlaceDefense(new BuildingPlacement(cell.X, cell.Y, BrushFaction, DefenseType));
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
        Vector3 origin = Camera.ProjectRayOrigin(screen);
        Vector3 direction = Camera.ProjectRayNormal(screen);
        if (direction.Y >= 0 || origin.Y < 0) return false;
        Vector3 ground = origin - direction * (origin.Y / direction.Y);
        cell = new Vector2I(Mathf.FloorToInt(ground.X / DeploymentLayout.CellSize),
            Mathf.FloorToInt(ground.Z / DeploymentLayout.CellSize));
        return true;
    }

    // Release may be consumed by a GUI control. Cancel it if it never reaches _UnhandledInput.
    public override void _Input(InputEvent input)
    {
        if (dragging && input is InputEventMouseButton button && !button.Pressed && button.ButtonIndex == dragButton)
            Callable.From(() => { if (dragging && !Input.IsMouseButtonPressed(dragButton)) CancelDrag(); }).CallDeferred();
    }

    private void HandleObstacleInput(InputEvent input)
    {
        if (input is InputEventMouseButton button &&
            (button.ButtonIndex == MouseButton.Left || button.ButtonIndex == MouseButton.Right))
        {
            if (button.Pressed)
            {
                if (dragging || !TryCell(button.Position, out dragStart)) return;
                var first = new ObstacleRectangle(dragStart.X, dragStart.Y, 1, 1);
                if (!first.IsValid) return;
                dragging = true;
                dragButton = button.ButtonIndex;
                ShowRectangle(first);
            }
            else if (dragging && button.ButtonIndex == dragButton)
            {
                CancelDrag();
                if (!TryCell(button.Position, out Vector2I end)) return;
                if (Layout.TryEditObstacles(DragArea(end), dragButton == MouseButton.Right, out string message))
                {
                    RebuildWalls();
                    Changed?.Invoke();
                }
                else if (message != null) EditRejected?.Invoke(message);
            }
        }
        else if (dragging && input is InputEventMouseMotion motion && TryCell(motion.Position, out Vector2I hover))
            ShowRectangle(DragArea(hover));
    }

    private ObstacleRectangle DragArea(Vector2I end)
    {
        end.X = Mathf.Clamp(end.X, -DeploymentLayout.HalfWidth, DeploymentLayout.HalfWidth);
        end.Y = Mathf.Clamp(end.Y, -DeploymentLayout.HalfWidth, DeploymentLayout.HalfWidth);
        return new ObstacleRectangle(Mathf.Min(dragStart.X, end.X), Mathf.Min(dragStart.Y, end.Y),
            Mathf.Abs(dragStart.X - end.X) + 1, Mathf.Abs(dragStart.Y - end.Y) + 1);
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
        Layout.Clear();
        RebuildWalls();
        RefreshDefenses();
        foreach (DeploymentPreview preview in previews) preview.Clear();
        Changed?.Invoke();
    }

    private const string SavePath = "user://deployment.json";

    private sealed record SavedPlacement(int X, int Z, byte Faction, string UnitId);
    private sealed record SavedDefense(int X, int Z, byte Faction, string BuildingId);
    private sealed record SavedDeployment(int Version, SavedPlacement[] Placements, ObstacleRectangle[] Obstacles, SavedDefense[] Defenses);
    private readonly record struct LegacyPlacement(int X, int Z, byte Faction, byte Kind);

    public string Save()
    {
        var saved = new SavedPlacement[Layout.Placements.Count];
        for (int i = 0; i < saved.Length; i++)
        {
            SoldierPlacement placement = Layout.Placements[i];
            saved[i] = new SavedPlacement(placement.X, placement.Z, placement.Faction, catalog[placement.Type].Id);
        }
        using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
        if (file == null) return $"Save failed ({FileAccess.GetOpenError()}).";
        var defenses = new SavedDefense[Layout.Defenses.Count];
        for (int i = 0; i < defenses.Length; i++)
        {
            var building = Layout.Defenses[i];
            defenses[i] = new SavedDefense(building.X, building.Z, building.Faction, buildings[building.Type].Id);
        }
        file.StoreString(JsonSerializer.Serialize(new SavedDeployment(3, saved, Layout.Obstacles.Field == null
            ? System.Array.Empty<ObstacleRectangle>() : Layout.Obstacles.Field.Rectangles.ToArray(), defenses)));
        return file.GetError() == Error.Ok ? "Deployment saved." : "Could not write deployment.";
    }

    public string Load()
    {
        using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
        if (file == null) return "No readable saved deployment.";

        var validated = new DeploymentLayout(catalog, buildings);
        try
        {
            using var document = JsonDocument.Parse(file.GetAsText());
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
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
                if (saved == null || saved.Version is not (1 or 2 or 3) || saved.Placements == null) return "Unsupported deployment format.";
                if (saved.Version >= 2)
                {
                    if (!ObstacleLayout.TryLoad(saved.Obstacles, out ObstacleLayout obstacles))
                        return "Saved deployment contains invalid obstacle rectangles.";
                    validated.SetLoadedObstacles(obstacles);
                }
                if (saved.Version == 3)
                {
                    if (saved.Defenses == null || saved.Defenses.Length > DeploymentLayout.MaxBuildings)
                        return "Saved deployment contains invalid defenses.";
                    foreach (var defense in saved.Defenses)
                        if (defense == null || !buildings.TryResolve(defense.BuildingId, out int type) ||
                            !validated.TryPlaceDefense(new BuildingPlacement(defense.X, defense.Z, defense.Faction, type)))
                            return "Saved deployment contains invalid defenses.";
                }
                foreach (SavedPlacement placement in saved.Placements)
                {
                    if (placement == null || !catalog.TryResolve(placement.UnitId, out byte type) ||
                        !TryLoadPlacement(validated, placement.X, placement.Z, placement.Faction, type))
                        return "Saved deployment contains invalid placements.";
                }
            }
        }
        catch (JsonException)
        {
            return "Saved deployment is invalid.";
        }

        CancelDrag();
        Layout = validated;
        RebuildWalls();
        RefreshDefenses();
        foreach (DeploymentPreview preview in previews) preview.Clear();
        foreach (SoldierPlacement placement in Layout.Placements) previews[placement.Type].Add(placement);
        Changed?.Invoke();
        return "Deployment loaded.";
    }

    private bool TryLoadPlacement(DeploymentLayout draft, int x, int z, byte faction, byte type)
    {
        return !draft.TryGet(new Vector2I(x, z), out _) &&
            draft.TryPaint(new SoldierPlacement(x, z, faction, type), out _);
    }
}
